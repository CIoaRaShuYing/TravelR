# 报销治理升级：技术设计

## 方案目标

在保留 ASP.NET Core、EF Core、PostgreSQL、Vue 3、Element Plus 和私有附件能力的前提下，完成注册审批、项目归属、版本化报销、独立发放状态和管理员全量汇总；附件统一写入本地持久目录，不引入 MinIO、微服务或外部财务系统。

## 总体结构

```text
Vue 3 + Vue Router
        |
ASP.NET Core API
        |-- PostgreSQL：用户、项目、报销主记录、版本、审批、发放、审计
        |-- 本地私有目录：不可变附件文件（数据库保存相对对象键）
```

## 领域模型

### 枚举

```text
ClaimStatus: Draft, Submitted, Rejected, Approved, Cancelled
PayoutStatus: NotApplicable, Pending, Paid
RegistrationRequestStatus: Pending, Approved, Rejected
AttachmentBindingStatus: Staged, Bound
```

`Superseded` 不作为当前审批状态；旧版本由 `CurrentVersionId` 和 `ClaimVersion.SupersededAt` 判断。

### 表与实体

| 实体 | 核心字段 | 规则 |
| --- | --- | --- |
| `Project` | `Id`、`Code`、`NormalizedCode`、`Name`、`Description`、`IsActive`、创建/更新时间 | 编码全局唯一且创建后不可修改；名称全局唯一；有业务引用后只能停用。 |
| `ReimbursementClaim` | `Id`、`ClaimNumber`、`ApplicantId`、`Type`、`CurrentVersionId`、`Status`、`PayoutStatus`、`ConcurrencyToken`、流程时间 | 稳定业务身份；不保存可版本化内容。 |
| `ClaimVersion` | `Id`、`ClaimId`、`VersionNumber`、`ProjectId`、项目编码/名称快照、`Description`、`TotalAmount`、`CreatedById`、`CreatedAt`、`SupersededAt` | 内容快照创建后不原地修改；总额服务端计算。 |
| `TravelItinerary` | `ClaimVersionId`、可空行程字段 | 草稿允许不完整；提交时验证完整。 |
| `ExpenseItem` | `ClaimVersionId`、`ClientKey`、类别、可空金额/日期/商户、备注 | 草稿允许不完整；提交时校验。 |
| `AttachmentAsset` | `OwnerId`、可空 `BoundClaimId`、`ObjectKey`、文件名、类型、大小、哈希、扫描/绑定状态、创建时间 | 文件不可变；绑定后只能在同一报销的版本间复用。 |
| `ExpenseItemAttachment` | `ExpenseItemId`、`AttachmentAssetId` | 保存版本时创建关联；旧版本关联不删除。 |
| `ApprovalRecord` | `ClaimId`、`ClaimVersionId`、前后状态、`ActorId`、意见、时间 | 每次提交、撤回、替换、批准、驳回只追加。 |
| `PayoutRecord` | `ClaimId`、`ApprovedVersionId`、金额、`ConfirmedById`、备注、时间 | `ClaimId` 唯一；不提供撤销。 |
| `AuditLog` | 保留现有字段，Context 增加版本、项目和状态 | 只追加。 |

### 索引与外键

- `Project.NormalizedCode` 唯一，`Project.Name` 唯一，`(IsActive, Name)` 普通索引。
- `ReimbursementClaim.ClaimNumber` 唯一，`CurrentVersionId` 唯一。
- `ReimbursementClaim(ApplicantId, Status, UpdatedAt)`、`(PayoutStatus, UpdatedAt)`。
- `ClaimVersion(ClaimId, VersionNumber)` 唯一，`(ProjectId, CreatedAt)`。
- `AttachmentAsset.ObjectKey` 唯一，`(OwnerId, BindingStatus, CreatedAt)`。
- `ExpenseItemAttachment(ExpenseItemId, AttachmentAssetId)` 联合唯一。
- `ApprovalRecord(ClaimId, ClaimVersionId, CreatedAt)`。
- `PayoutRecord.ClaimId` 唯一。
- 注册申请建立同一手机号仅一条 `Pending` 的 PostgreSQL 部分唯一索引。
- 用户、项目、报销主记录之间使用 `Restrict`；版本子树不提供运行期物理删除。

## 状态机与事务

### 新建和保存

1. 新建报销要求项目和类型，允许说明、行程、费用和附件不完整。
2. 新建生成报销主记录和 v1，状态 `Draft`、发放状态 `NotApplicable`。
3. 编辑 `Draft`、`Submitted` 或 `Rejected` 时创建 vN+1，旧版本写 `SupersededAt`，当前状态回到 `Draft`。
4. 如果项目已停用，原报销可以继续保留该项目；新报销或切换项目时只能选择启用项目。
5. 保存事务校验申请人、当前版本、并发戳、项目和附件归属，提交后轮换并发戳。

### 提交、撤回和审批

- 提交只允许当前 `Draft`，执行现有差旅/普通报销完整性校验，成功后变 `Submitted`。
- 删除/撤回允许当前 `Draft`、`Submitted`、`Rejected`，状态变 `Cancelled`，不物理删除。
- 批准只允许管理员处理当前 `Submitted` 版本；同一事务写 `Approved`、`PayoutStatus=Pending`、审批记录和审计。
- 驳回只允许管理员处理当前 `Submitted` 版本，原因必填。
- 用户保存新版本后，旧 Submitted 版本不再是 `CurrentVersionId`，管理员审批旧版本返回 409。

### 发放

- 确认发放只允许管理员处理 `Approved + Pending`。
- 同一事务写 `PayoutStatus=Paid`、唯一 `PayoutRecord` 和审计。
- 不实现 `Paid -> Pending` 接口，重复确认返回 409。

## 附件流程

1. `POST /api/attachments/staged` 校验文件并写不可变对象，成功后创建申请人名下 `Staged` 元数据。
2. 如果元数据保存失败，调用内部 `DeleteAsync` 补偿删除对象。
3. 创建报销或新版本时，费用条目用稳定 `ClientKey` 绑定附件 ID；服务端验证所有权和报销归属。
4. 首次绑定时设置 `BoundClaimId`；后续只能被同一报销的新版本复用。
5. 过期未绑定附件由后台清理任务删除，默认保留 24 小时。
6. 版本中移除附件只是不创建新关联，不删除旧版本关联或对象。

### 本地目录设计

- 唯一配置为 `FileStorage:LocalPath` 和 `FileStorage:StagedRetentionHours`，不保留 Provider、Endpoint、Bucket 或存储密钥。
- 相对路径基于 API `ContentRootPath` 解析；开发配置使用 `../../private-uploads`，容器配置使用绝对路径 `/data/private-uploads`。
- `LocalPrivateFileStore` 以单例注册，启动清理任务首次解析服务时完成目录创建和写探针；失败抛出包含目标目录的明确配置错误。
- 数据库 `AttachmentAsset.ObjectKey` 继续保存 `yyyy/MM/<guid>.<ext>` 相对键；任何解析后逃逸根目录的键都被拒绝。
- Compose 将持久卷 `attachments_data` 挂载到 `/data/private-uploads`，删除 MinIO 服务、依赖、SDK 和环境密钥。

## API 合同

### 通用响应

```text
PagedResult<T>: items, page, pageSize, total
ApiError: code, message, errors?, traceId?
```

版本敏感请求必须携带 `expectedCurrentVersionId` 和 `concurrencyToken`。

### 申请人

```text
GET  /api/projects/available
GET  /api/projects/mine
GET  /api/claims?projectId=&status=&page=&pageSize=
POST /api/claims
GET  /api/claims/{claimId}
POST /api/claims/{claimId}/versions
POST /api/claims/{claimId}/submit
POST /api/claims/{claimId}/cancel
GET  /api/claims/{claimId}/versions
GET  /api/claims/{claimId}/versions/{versionId}
POST /api/attachments/staged
GET  /api/attachments/{attachmentId}/download
```

### 管理员

```text
GET  /api/admin/registration-requests?status=&page=&pageSize=
POST /api/admin/registration-requests/{id}/approve  body: { concurrencyToken }
POST /api/admin/registration-requests/{id}/reject   body: { concurrencyToken }

GET  /api/admin/projects?status=&keyword=&page=&pageSize=
POST /api/admin/projects
PUT  /api/admin/projects/{id}
POST /api/admin/projects/{id}/enable
POST /api/admin/projects/{id}/disable

GET  /api/admin/claims?projectId=&applicantId=&status=&payoutStatus=&createdFrom=&createdTo=&page=&pageSize=
GET  /api/admin/claims/group-summary?groupBy=project|applicant&...
POST /api/admin/claims/{claimId}/versions/{versionId}/approve
POST /api/admin/claims/{claimId}/versions/{versionId}/reject
POST /api/admin/claims/{claimId}/payout/confirm
```

- 注册审批请求不再包含 `comment`，注册申请响应和数据库模型不再包含 `ReviewComment`；全局 JSON 请求启用未知字段拒绝，旧客户端发送废弃字段返回 400。
- 待审批查询只返回 `Submitted` 且当前版本 `SupersededAt` 为空的报销；审批服务在写入前再次验证当前版本未作废。

管理员全部报销响应额外返回筛选范围内 `claimCount` 和 `totalAmount`，不能使用当前页前端求和。默认排除 `Cancelled`，明确筛选时可查看。

## 服务边界与代码组织

后端不整体重写现有项目，按功能抽取受影响逻辑。实现阶段确认当前项目仍是单文件 Minimal API，路由规模尚未达到必须拆分 Endpoint 文件的程度；为避免在业务重构之外同时引入目录级重写，本轮保持路由集中在 `Program.cs`，把状态机和版本事务抽到服务层：

```text
Domain/Entities.cs
Data/AppDbContext.cs
Contracts/
Program.cs
Services/ClaimWorkflowService.cs
Services/PrivateFileStore.cs
Services/StagedAttachmentCleanupService.cs
```

- `ClaimWorkflowService` 负责版本快照、当前版本切换、提交、撤回、审批和发放状态机。
- 后台清理服务负责过期 `Staged` 附件的元数据和对象双侧清理。
- Endpoint 只做身份、DTO、错误映射，不直接散落事务逻辑。
- PostgreSQL 业务冲突不自动重试，统一映射为 409。

## 角色、认证和初始化

- 种子角色仅为 `Applicant`、`Administrator`，管理员同时拥有两者。
- 删除 `Reviewer` 菜单、种子、授权组和判断。
- `BootstrapAdmin` 改为 `PhoneNumber`、`DisplayName`、`Password`；示例配置只保存占位符。
- Compose 部署要求在对外启动前创建手机号管理员；开发环境仍可保留现有首位管理员注册引导。
- JWT 验证时检查用户存在且启用，数据库清空后旧 JWT 即失效。

## 前端信息架构

```text
/claims                         我的报销；新建/编辑使用 ClaimEditorDialog，详情和版本历史使用 ClaimDetailDrawer
/admin/registrations            注册审批
/admin/projects                 项目管理
/admin/claims                   报销管理：待审批/待发放/全部
/admin/settings                 注册策略
```

- 使用 `vue-router`，不引入 Pinia。
- `App.vue` 只保留会话和路由外壳；`AppShell.vue` 负责桌面侧栏与移动抽屉导航。
- `MyClaimsView.vue` 在单页内使用 `ClaimEditorDialog.vue` 完成新建和编辑，使用 `ClaimDetailDrawer.vue` 展示当前详情、版本历史和审批记录；不增加单独的新建、编辑、详情路由。
- `AdminClaimsView.vue` 复用详情抽屉，并集中提供待审批、待发放、全部报销、组合筛选和项目/人员分组。
- 待审批入口的详情抽屉只展示当前有效版本；“全部报销”和申请人详情仍可查看只读历史版本。
- 登录响应完整会话写入 `sessionStorage` 并在应用初始化时同步恢复 JWT、用户和角色；JWT 到期、401 和主动退出统一清理，标签页关闭后不继续持久化。
- 保存草稿只要求项目和类型；提交执行完整校验。
- 编辑 Submitted 前警告，实际保存成功后旧版本才失效。
- 草稿/驳回显示“删除”，待审显示“撤回并删除”；Approved/Paid 只读。
- 管理员注册批准/拒绝使用无原因确认对话框；报销驳回和发放确认使用 Element Plus 对话框，不使用 `window.prompt`。
- 管理员移动端菜单改为抽屉，避免当前横向菜单继续膨胀。

## 破坏式数据库与附件重建

1. 先在唯一命名的临时 PostgreSQL 数据库和临时附件桶验证新基线迁移及 E2E。
2. 进入维护窗口，停止 API/Web 写入。
3. 只读解析最终配置，展示脱敏后的主机、数据库名、存储 Provider、桶名或本地绝对目录。
4. 精确核对 Compose 项目和卷；记录数据库、附件元数据和对象数量。
5. 移除旧业务迁移，生成并验证单一 `InitialGovernanceRebuild`。
6. 清空确认过的数据库和对应本地附件目录；不得只清一侧。
7. 不在启动代码中加入自动删库；清理由一次性、显式运维步骤执行。
8. 应用新迁移，种子化两个角色，初始化手机号管理员。
9. 确认旧数据和对象均为零，创建首个启用项目后执行验收。

回滚边界：用户已放弃旧数据，因此不提供旧数据恢复；代码或迁移失败时保持服务停止，修复后重新从空库部署。

## 验证计划

### 自动验证

```powershell
dotnet tool restore
dotnet restore TravelReimbursement.slnx
dotnet build TravelReimbursement.slnx --no-restore
dotnet test TravelReimbursement.slnx --no-build --no-restore
Set-Location frontend
npm.cmd run build
```

- 增加项目级 `dotnet-ef` tool manifest，版本与 EF Core 包一致。
- 后端增加状态机、项目、附件归属、角色和并发测试。
- 关键并发测试使用真实 PostgreSQL；EF InMemory 不能代替事务和唯一约束验证。
- 不强制引入 Pinia、Playwright 或完整前端测试框架；浏览器真实验收覆盖关键交互。

### 真实验收

- 注册申请 -> 管理员审批 -> 用户登录。
- 管理员创建/停用项目，所有用户可选择启用项目。
- 不完整草稿保存、提交校验、编辑、撤回和版本历史。
- 管理员打开 v1、用户保存 v2、管理员审批 v1 返回 409。
- 批准后待发放、管理员确认、重复发放失败、不可回退。
- 两个项目、三个用户、多状态数据的分页、筛选、分组、总数和金额核对。
- 附件新增、复用、移除、下载、越权和旧版本查看。
- 1440px、1024px、768px、390px 页面检查；本地代理和 Docker Nginx 各验证一次。

## 明确不做

- 不迁移或恢复现有业务数据库和附件。
- 不引入独立审核人、多级审批、项目成员关系、预算、OCR、自动付款或 ERP。
- 不提供项目、报销、版本、审批、发放记录的运行期物理删除。
- 不提供已发放回退接口。
- 不切换为 Cookie 认证，也不使用 `localStorage` 建立跨标签页关闭的长期浏览器会话。
- 不支持多个 API 实例各自写入不同本地附件目录；需要横向扩容时重新设计共享存储。
