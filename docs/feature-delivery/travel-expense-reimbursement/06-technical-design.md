# 差旅报销 Web：技术设计

## 架构

```text
Vue 3 Web
    |
ASP.NET Core API
    |-- PostgreSQL: 账号、报销、审批、审计、附件元数据
    |-- MinIO 私有桶: JPG/PNG/PDF 凭证文件
```

## 领域模型

| 表/实体 | 核心字段与规则 |
| --- | --- |
| `users` | Identity 用户；启用状态、显示名、邮箱、创建时间。 |
| `roles`、`user_roles` | `Applicant`、`Reviewer`、`Administrator`；首期由管理员分配。 |
| `system_settings` | `registration_mode`：`Open`、`ApprovalRequired`、`Closed`；单行配置和并发版本。 |
| `registration_requests` | 姓名、邮箱、密码哈希、状态、审核意见、审核人、审核时间；仅审核通过后创建 `users`。 |
| `reimbursement_claims` | 报销单号、申请人、类型 `Travel`/`General`、说明、总额、状态、提交/审核时间、并发版本。 |
| `travel_itineraries` | 与 `Travel` 报销单一对一；出发地、目的地、出发/返回日期。 |
| `expense_items` | 报销单、可选行程、类别、金额、币种、费用日期、商户、备注；类别含 `DepartureTransport`、`ReturnTransport`、`Lodging`、`OfficeSupplies`、`Meal`、`Other`。 |
| `attachments` | 费用条目、私有对象键、原始文件名、内容类型、大小、哈希、扫描状态；不保存公开 URL。 |
| `approval_records` | 报销单、动作 `Submitted`/`Approved`/`Rejected`/`Resubmitted`、操作人、意见、前后状态、时间；只追加。 |
| `audit_logs` | 用户、动作、对象类型/标识、时间、请求追踪号、必要的脱敏上下文；只追加。 |

索引：`reimbursement_claims(applicant_id, status, created_at)`、`registration_requests(status, created_at)`、`expense_items(claim_id, category)`、`attachments(expense_item_id)`、`approval_records(claim_id, created_at)`。

## 状态机

```text
报销单：Draft -> Submitted -> Approved
                    |
                    -> Rejected -> Draft -> Submitted

注册申请：Pending -> Approved -> 创建可登录用户
                    -> Rejected
```

- `Travel` 在 `Draft -> Submitted` 时校验：说明非空、去程交通至少一项、回程交通至少一项、所有金额大于零、每个费用条目至少一个扫描合格的附件；允许出发日期等于返程日期。
- `Lodging` 不参与提交校验，始终可为零项，不因跨日或日期相同而改变。
- `General` 在提交时校验：说明非空、至少一项金额大于零且附件合格的费用条目。
- 审核人只能处理 `Submitted`；申请人只能编辑 `Draft`/`Rejected`；驳回意见必填。
- 更新报销单时，费用条目可携带可选 `id`：已有条目原地更新并保留其私有附件；新条目新增；只有无附件条目允许删除。这样驳回单可以修改金额、说明或新增费用后重提，同时不产生孤儿附件。

## API 契约（首期）

| 分组 | 路径 | 用途 |
| --- | --- | --- |
| 认证 | `POST /api/auth/register` | 按当前注册模式创建用户或注册申请。 |
| 认证 | `GET /api/registration-settings` | 返回注册模式及 `initialAdministratorRegistration`；该状态只用于页面引导，提交时重新判定。 |
| 认证 | `POST /api/auth/login` | 仅允许启用账号登录。 |
| 管理 | `GET/PUT /api/admin/registration-settings` | 查询/切换注册模式。 |
| 管理 | `GET /api/admin/registration-requests` | 分页查询待审注册。 |
| 管理 | `POST /api/admin/registration-requests/{id}/approve` | 批准并创建账号。 |
| 管理 | `POST /api/admin/registration-requests/{id}/reject` | 拒绝并记录理由。 |
| 报销 | `GET/POST /api/claims` | 查询自己的报销单、创建草稿。 |
| 报销 | `GET/PUT /api/claims/{id}` | 查询、编辑允许修改的报销单。 |
| 报销 | `POST /api/claims/{id}/submit` | 运行服务端完整性校验并提交。 |
| 附件 | `POST /api/expense-items/{id}/attachments` | 鉴权上传，进行类型和内容校验。 |
| 附件 | `GET /api/attachments/{id}/download` | 对象级鉴权后下载私有文件。 |
| 审核 | `GET /api/review/claims` | 查询授权范围内的已提交报销单。 |
| 审核 | `POST /api/review/claims/{id}/approve` | 审批通过。 |
| 审核 | `POST /api/review/claims/{id}/reject` | 驳回，意见必填。 |

## 页面清单

| 页面 | 用户 | 核心操作 |
| --- | --- | --- |
| 登录/注册 | 访客 | 根据注册模式展示可注册、待审核或关闭提示。 |
| 我的报销 | 申请人 | 创建、筛选、查看状态、继续编辑草稿/驳回单。 |
| 差旅行程编辑 | 申请人 | 行程信息、去程、回程、可选住宿、费用和凭证上传、提交前校验。 |
| 单据报销编辑 | 申请人 | 独立费用条目、凭证、说明、提交前校验。 |
| 审核工作台 | 审核人 | 筛选已提交单据、预览凭证、批准或驳回。 |
| 管理后台 | 管理员 | 注册模式、注册申请审核、用户角色和报销查询。 |

### multipart 上传安全边界

- 附件上传端点使用 JWT Bearer 鉴权，不接受 Cookie 会话身份；因此该端点显式禁用 ASP.NET Core 防伪校验，避免 `IFormFile` 绑定触发无防伪中间件的 500。
- 该豁免不放宽访问控制：上传仍要求有效 Bearer 令牌、申请人本人、且报销单状态为 `Draft` 或 `Rejected`；下载继续执行对象所属报销单的授权校验。

## 安全、审计与部署

- 密码只交由 ASP.NET Core Identity 哈希存储；JWT 采用短有效期和刷新令牌轮换，密钥只来自部署环境变量。
- 所有报销单、审批、附件 API 均做角色和对象级授权；不可将对象存储桶配置为公开读取。
- 数据库迁移由 EF Core 生成；首期为新库初始化，不含破坏性迁移。生产迁移前需备份并先在预发布验证。
- Docker Compose 包含 Web、API、PostgreSQL、MinIO；Nginx 终止 TLS。生产密钥、数据库密码、对象存储凭据均不得提交仓库。

## 首位管理员注册交互

- 服务端公开设置通过角色关联表判断是否已有 `Administrator`，无管理员时返回 `initialAdministratorRegistration: true`。
- `POST /api/auth/register` 先在串行化事务内处理首位管理员，再对已有管理员场景应用 `Open`、`ApprovalRequired`、`Closed` 策略；避免关闭模式造成系统无法初始化。
- 注册页的视觉签名是琥珀色“系统初始化”提示条，直接写明“当前注册将创建首位管理员”，并列出三种角色权限；其余表单保持现有绿色差旅账视觉体系，避免多个强调元素竞争。
- 立即创建账号成功后，前端无条件切换到登录表单、带入刚注册的手机号、清空注册密码；提交待审核申请时保持注册页并显示等待审核提示。

## 验证计划

- API 单元测试：注册模式、旅行提交校验、状态机、对象级授权。
- API 集成测试：PostgreSQL Testcontainers、MinIO 替身或测试桶、上传/下载鉴权。
- 前端组件测试：动态表单、错误提示、角色路由守卫。
- 端到端测试：四种注册路径、当天往返、含住宿行程、普通报销、驳回重提、附件越权访问。
