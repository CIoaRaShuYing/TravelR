# 报销治理升级：实施日志

## 2026-08-03 11:55 +08:00 - M1 新领域模型与安全重建准备

### 完成任务

- `TASK-201`：完成项目、稳定报销主记录、不可变版本、附件资产、版本化审批、独立发放记录和并发戳模型。
- `TASK-202`：完成空库基线迁移、手机号管理员初始化配置、Compose 调整和破坏式切换说明；实际业务库和附件尚未清理。

### 变更文件

- `backend/TravelReimbursement.Api/Domain/Entities.cs`
- `backend/TravelReimbursement.Api/Data/AppDbContext.cs`
- `backend/TravelReimbursement.Api/Data/AppDbContextFactory.cs`
- `backend/TravelReimbursement.Api/Data/Migrations/20260803032725_InitialGovernanceRebuild.cs`
- `backend/TravelReimbursement.Api/Data/Migrations/20260803032725_InitialGovernanceRebuild.Designer.cs`
- `backend/TravelReimbursement.Api/Data/Migrations/AppDbContextModelSnapshot.cs`
- `backend/TravelReimbursement.Api/Contracts/Requests.cs`
- `backend/TravelReimbursement.Api/Services/ClaimSubmissionValidator.cs`
- `backend/TravelReimbursement.Api/Services/ClaimWorkflowService.cs`
- `backend/TravelReimbursement.Api/Services/PrivateFileStore.cs`
- `backend/TravelReimbursement.Api/Program.cs`
- `backend/TravelReimbursement.Api.Tests/ClaimSubmissionValidatorTests.cs`
- `.config/dotnet-tools.json`
- `.env.example`
- `docker-compose.yml`
- `README.md`

### 验证命令与结果

```powershell
dotnet build TravelReimbursement.slnx --no-restore -nodeReuse:false
dotnet test TravelReimbursement.slnx --no-build --no-restore
```

- 后端构建：0 错误；NuGet 漏洞源因本机 SSL 不可达产生 2 个 `NU1900` 警告。
- 单元测试：10/10 通过。

```powershell
$env:ConnectionStrings__DefaultConnection='<临时 PostgreSQL 连接>'
.\.tools\dotnet-ef.exe database update --project backend\TravelReimbursement.Api --startup-project backend\TravelReimbursement.Api --no-build
```

- 唯一命名临时 PostgreSQL 成功应用 `20260803032725_InitialGovernanceRebuild`。
- `public` schema 共 20 张表，其中 19 张业务/Identity 表，另 1 张 `__EFMigrationsHistory`。
- 设计时 DbContext 原先硬编码连接串，已改为优先读取 `ConnectionStrings__DefaultConnection`，防止验证命令误连默认数据库。

隔离 HTTP 冒烟从空临时库和空临时 MinIO 桶执行，结果：

- 健康检查通过。
- 注册批准、注册拒绝和申请人权限隔离通过。
- 管理员创建/停用项目、所有用户选择启用项目、停用项目禁止新建报销通过。
- PDF staged 上传、绑定、跨版本复用和下载通过。
- 差旅 v1 提交后保存 v2，v1 写入 `SupersededAt`，旧版本审批返回 409。
- v2 批准后发放状态为 `Pending`，确认后为 `Paid`；重复发放和已批准报销删除均返回 409。
- 草稿删除写入 `Cancelled`，默认列表排除作废数据。
- 申请人项目筛选、管理员全量列表、按项目/申请人分组均通过；汇总金额为当前 v2 的 1600.00，未重复计算 v1。

```powershell
docker compose config --quiet
```

- Compose 配置校验成功；沙箱无权读取用户 Docker CLI 配置文件，出现不影响配置解析的警告。

### 实现中发现并修复的缺口

- 报销列表原先在投影成 `ClaimListRow` 后排序，Npgsql 无法翻译并返回 500；已把排序前移到实体查询并在真实 PostgreSQL 上复验申请人和管理员列表。
- Windows 沙箱无法读取/写入用户级 ASP.NET Core DataProtection key 目录，启动时有环境性日志；当前系统使用 JWT，业务接口和附件流程未受影响，Docker/Linux 验收仍需观察。
- 原方案计划拆分多个 Endpoint 和版本服务文件。结合当前小型 Minimal API 结构，实际保持路由在 `Program.cs`，将版本和状态机事务集中到 `ClaimWorkflowService`，已回写技术方案。

### 安全状态

- 实际业务 PostgreSQL 未清空。
- 实际 MinIO 桶和本地附件目录未删除。
- 临时 PostgreSQL 和 MinIO 使用唯一容器名、回环端口和 tmpfs，无持久卷。
- 当前目录不是 Git 仓库，没有提交或推送。

### 下一步

- 进入 M2 `TASK-203`：完成管理员注册审批、项目管理、路由导航和移动端管理入口。

## 2026-08-03 13:30 +08:00 - M2 至 M4 业务纵切片完成

### 完成任务

- `TASK-203`：管理员注册审批、项目创建/编辑/启停、分页、权限和审计完成。
- `TASK-204`：staged 附件、同报销跨版本复用、鉴权下载、失败补偿和 24 小时后台清理完成。
- `TASK-205`：报销 v1/vN+1、提交、撤回、软删除、旧版本只读、并发戳和旧版本审批 409 完成。
- `TASK-206`：申请人项目化报销页面、保存草稿/提交分离、编辑/删除、附件、版本历史、筛选和移动端适配完成。
- `TASK-207`：管理员待审批、待发放、全部报销、组合筛选、项目/人员分组、数据库汇总、审批和发放完成。

### 主要变更文件

- `backend/TravelReimbursement.Api/Program.cs`
- `backend/TravelReimbursement.Api/Services/ClaimWorkflowService.cs`
- `backend/TravelReimbursement.Api/Services/PrivateFileStore.cs`
- `backend/TravelReimbursement.Api/Services/StagedAttachmentCleanupService.cs`
- `frontend/src/api.ts`
- `frontend/src/router.ts`
- `frontend/src/components/AppShell.vue`
- `frontend/src/components/ClaimEditorDialog.vue`
- `frontend/src/components/ClaimDetailDrawer.vue`
- `frontend/src/views/MyClaimsView.vue`
- `frontend/src/views/AdminRegistrationsView.vue`
- `frontend/src/views/AdminProjectsView.vue`
- `frontend/src/views/AdminClaimsView.vue`
- `frontend/src/style.css`

### HTTP 与浏览器验收

- 注册批准/拒绝，项目创建/停用/启用，所有用户选择启用项目通过。
- staged PDF 上传和下载通过。
- v1 草稿修改后生成 v2，v1 作废且旧版本审批返回 409。
- v2 批准后为待发放，确认发放成功，重复发放失败，已批准报销不可删除。
- 管理员自审、项目/人员分组和数据库金额汇总通过。
- 浏览器中 v1 草稿金额为 1,580 元；修改并提交后生成 v2，v1 在历史中只读可见；v2 批准并确认发放。
- 管理员全部报销显示 2 笔，当前版本合计 3,180 元。
- `390x844` 移动端无横向溢出，移动抽屉包含全部管理入口。

### 实现中发现并修复的缺口

- “我的报销”使用启用项目列表会导致历史停用项目无法筛选，新增 `GET /api/projects/mine` 返回当前用户历史关联项目。
- 管理员分组汇总原先无条件排除 `Cancelled`，导致显式筛选作废状态仍为空；已改为仅在未指定状态时默认排除。
- 原技术设计采用新建/编辑/详情独立路由；实际根据当前单页工作流改为 `ClaimEditorDialog` 和 `ClaimDetailDrawer`，已回写技术设计。

## 2026-08-03 14:35 +08:00 - M5 完整验证与 Docker 验收

### 完成任务

- `TASK-208`：构建、测试、权限、并发冲突、真实依赖、浏览器和 Docker 验收完成。

### 验证命令与结果

```powershell
dotnet build TravelReimbursement.slnx --no-restore
dotnet test TravelReimbursement.slnx --no-build --no-restore
Set-Location frontend
npm.cmd run build
```

- 后端构建：0 错误；仅有 NuGet 漏洞源 SSL 不可达产生的 `NU1900` 警告。
- 后端测试：10/10 通过。
- 前端生产构建：通过；仅有 Vite 大包提示。
- API 生产 Dockerfile 构建、启动和健康检查通过。
- Nginx 入口登录及 2 笔/3,180 元汇总通过。
- 固定 `node:24-alpine`、`nginx:1.27-alpine` 从 Docker Hub 拉取时网络超时；使用本机 Nginx 镜像挂载同一 `dist` 和 `nginx.conf` 完成运行验收。该限制不影响代码与配置正确性，但部署环境首次构建仍需能访问 Docker Hub 或预置镜像。
- 本轮临时 Docker API/Web/网络/镜像已删除，未停止或修改其他仓库的容器和进程。

## 2026-08-03 14:50 +08:00 - TASK-209 实际破坏式重建

### 清理目标与清理前计数

- 精确目标：`chuchai_postgres_data`、`chuchai_minio_data`、`D:\Code\chuchai\private-uploads`。
- `travel_reimbursement`：2 个用户、2 笔报销。
- `travel_reimbursement_local`：1 个用户、5 笔报销、10 条附件元数据。
- 本地附件目录：3 个 PDF，共 124,949 字节。
- MinIO：无业务桶和对象。

### 执行结果

- 删除并按原所有者重建 `travel_reimbursement`、`travel_reimbursement_local`。
- 两库均应用唯一迁移 `20260803032725_InitialGovernanceRebuild`。
- 删除本地 3 个 PDF，保留空的 `private-uploads` 目录。
- 主库初始化 `Administrator`、`Applicant` 两个角色和 1 个手机号管理员；本地库保持空基线，由首次启动初始化。
- 最终主库共 20 张表、1 条迁移、1 个用户；项目、报销、附件、注册申请均为 0。
- MinIO 数据目录仅有 `.minio.sys` 内部元数据，无业务桶；本地附件目录为 0 文件。
- 实际空库 API 管理员登录通过，浏览器 `/claims` 显示 0 条报销。

### 不可逆边界

- 本次破坏式重建已经完成，旧用户、报销、审批、附件元数据和 3 个本地 PDF 不提供恢复。
- 清理严格限定在已确认数据库、Compose 数据卷和附件目录，没有递归删除工作区或触碰其他仓库服务。

## 2026-08-03 14:54 +08:00 - TASK-210 交付收口

- `http://127.0.0.1:5173` 返回 200。
- `http://127.0.0.1:55182/health` 返回 200 和 `{"status":"ok"}`。
- 只读复核主库：20 张表、1 条迁移、1 个初始化管理员、2 个业务角色；项目、报销、附件、注册申请仍为 0。
- `D:\Code\chuchai\private-uploads` 仍为 0 文件；MinIO 仍无业务桶。
- REQ-113 的服务端分页已实现，但未实际制造超过 20 条记录验证跨页边界，保留为后续规模化回归项。
- 当前目录不是 Git 仓库，因此没有执行 commit 或 push。

## 2026-08-03 16:40 +08:00 - M6 正式移除 MinIO

### 完成任务

- `TASK-211`：删除后端 MinIO SDK、客户端注册和对象存储实现，将 `IPrivateFileStore` 收敛为 `LocalPrivateFileStore`。
- `TASK-212`：删除 Compose MinIO 服务、依赖、密钥和数据卷声明，增加 `attachments_data` 本地持久卷并完成旧运行资源清理。

### 主要变更

- `backend/TravelReimbursement.Api/TravelReimbursement.Api.csproj` 不再引用 MinIO NuGet 包。
- `backend/TravelReimbursement.Api/Program.cs` 只注册本地私有文件存储，并在应用启动时解析服务触发目录写探针。
- `backend/TravelReimbursement.Api/Services/PrivateFileStore.cs` 支持 `FileStorage:LocalPath`，保存时流式计算 SHA-256，读取和删除统一通过安全路径解析；使用 `Path.GetRelativePath` 拒绝对象键逃逸存储根目录。
- `backend/TravelReimbursement.Api/appsettings.json` 默认将附件落到仓库根目录 `private-uploads`；Compose 将容器内 `/data/private-uploads` 挂载到 `attachments_data`。
- `.env.example` 删除 MinIO 配置；README 增加本地目录、鉴权边界及数据库/附件同批次备份恢复说明。
- 新增 `LocalPrivateFileStoreTests`，覆盖保存、读取、删除和路径逃逸。

### 原故障根因

- `POST /api/attachments/staged` 原先依赖 MinIO 客户端；服务不可达、配置缺失或对象存储初始化失败会在上传阶段抛出未处理异常，由统一异常处理返回 `INTERNAL_ERROR`。
- 改造后该接口不再进行网络对象存储调用，文件直接写入进程可访问的私有目录；目录不可写会在启动阶段明确失败，不再等到用户上传时返回模糊 500。

### 验证命令与结果

```powershell
dotnet restore TravelReimbursement.slnx
dotnet build TravelReimbursement.slnx --no-restore
dotnet test TravelReimbursement.slnx --no-build --no-restore
Set-Location frontend; npm.cmd run build
docker compose config --quiet
```

- 后端还原、构建通过：0 警告、0 错误。
- 后端测试：12/12 通过。
- 前端生产构建通过，仅有 Vite 大包提示。
- Compose 配置校验通过；源码、配置和编译产物扫描不再包含 MinIO 运行依赖。
- 当前 API `http://127.0.0.1:55182/health` 返回 200 和 `{"status":"ok"}`，启动写探针通过，活动目录 `D:\Code\chuchai\private-uploads` 无探针残留。

### 隔离真实附件闭环

- 启动一次性 PostgreSQL tmpfs 容器和独立 API 端口，附件目录使用 `D:\Code\chuchai\.tmp\local-files-e2e-20260803`。
- 通过公开注册接口创建首位管理员，再调用正式登录接口取得会话；未生成或伪造 JWT。
- 上传 77 字节 PDF：`POST /api/attachments/staged` 返回 201；鉴权下载返回 200；下载内容与源文件 SHA-256 一致。
- 本地目录产生 1 个对象，隔离数据库 `AttachmentAssets` 记录为 1；验收完成后临时 API、数据库容器和附件目录均已删除。
- Chrome 已检测到正常管理员登录的 `/claims` 页面，但自动接管该标签连续超时，未执行页面文件选择；该环境限制不影响上述正常认证 HTTP 闭环。

### 旧资源移除

- `chuchai_minio_data` 标签归属 Compose 项目 `chuchai`，挂载目标为 `/data`，且只被 `chuchai-minio-1` 使用。
- 使用本机 BusyBox 以只读方式检查卷内容，只有 `.minio.sys` 配置、临时和回收元数据，没有业务桶或附件对象。
- 已精确删除 `chuchai-minio-1` 和 `chuchai_minio_data`；没有停止或修改 `trusted-data-space` 及其他项目容器。
- 最终磁盘复核发现旧路径 `backend/TravelReimbursement.Api/private-uploads` 遗留 10 个 PDF，共 347,031 字节。当前配置指向仓库根目录 `private-uploads`，主库和本地基线库 `AttachmentAssets` 均为 0，确认旧文件无引用后按既有破坏式重建授权精确删除该旧目录；新的活动附件目录未改动。

### 运行和备份边界

- 开发环境附件目录为 `D:\Code\chuchai\private-uploads`，可用 `FileStorage__LocalPath` 覆盖；目录不通过静态 Web 暴露。
- 容器环境附件卷为 `attachments_data`。备份和恢复必须将 PostgreSQL 与附件目录/卷作为同一批次，不能只恢复其中一侧。
- Windows 沙箱仍可能输出 ASP.NET Core DataProtection 用户目录权限警告；当前认证使用 JWT，健康检查和附件业务接口不受影响。

## 2026-08-03 17:54 +08:00 - M7 严格审批合同与刷新会话恢复

### 完成任务

- `TASK-213`：注册审批 API 破坏式升级、刷新会话恢复、审批队列作废版本隔离。

### 破坏式 API 与数据库变更

- `ReviewRegistrationRequest` 只保留必填 `ConcurrencyToken`，批准和拒绝端点不再读取、保存或返回原因。
- 全局 JSON 请求启用未知字段拒绝，旧客户端继续发送 `comment` 时直接返回 400，不保留静默兼容。
- 增加迁移 `20260803093625_RemoveRegistrationReviewComment`，当前业务数据库已应用；迁移 SQL 只执行 `ALTER TABLE "RegistrationRequests" DROP COLUMN "ReviewComment"`。
- 当前数据库确认存在两条迁移，`RegistrationRequests.ReviewComment` 不存在。

### 刷新会话恢复

- 登录成功后将 token、用户和角色写入当前标签页 `sessionStorage`；应用初始化同步恢复并先设置 API Authorization token。
- JWT 无法解析或已到期时不恢复；任一已登录请求返回 401、用户主动退出时同步清空内存和标签页会话。
- 隔离浏览器正式登录管理员后刷新：管理员名称 1 个、登录按钮 0 个、“我的报销”标题 1 个；退出后和再次刷新后登录按钮均为 1 个。

### 审批队列有效性

- 管理员 `status=Submitted` 的列表和分组汇总增加当前版本 `SupersededAt IS NULL` 约束；审批服务写入前再次拒绝已作废当前版本。
- 管理员从待审批入口打开详情时只显示当前有效版本；“全部报销”和申请人详情仍保留只读历史版本追溯。
- 隔离数据库人为构造“主记录 Submitted、当前版本 Superseded”的异常数据后，待审批列表总数和分组均为 0，直接审批返回 409。

### 验证结果与运行态

- `dotnet build TravelReimbursement.slnx -c Release --no-restore`：0 警告、0 错误。
- `dotnet test TravelReimbursement.slnx -c Release --no-build --no-restore`：12/12 通过。
- `npm.cmd run build`：通过，仅保留 Vite 大包提示。
- 隔离真实 HTTP：旧注册审批合同 400、缺少并发令牌 400、新合同 200、批准后用户登录 200、作废审批队列 0、作废版本审批 409。
- 当前 Release API 已替换旧 Debug 进程并监听 `http://127.0.0.1:55182`，健康检查返回 200；启动时已应用新迁移。切换生成了新的 JWT 签名密钥，因此切换前令牌会失效一次，重新登录后刷新保持会话。
- Vite 开发前端已重新监听 `http://127.0.0.1:5173`，入口返回 200，运行模块已包含 `sessionStorage` 恢复和统一 401 清理代码。
- 两次隔离验收使用的临时 API、前端、数据库、文件目录和运行脚本均已清理；当前业务数据库和活动附件目录未写入测试业务数据。

## 2026-08-03 19:30 +08:00 - M8 附件预览与报销录入体验修正

### 完成任务

- `TASK-214`：附件在线预览、费用默认项、添加费用位置、空类别草稿合同和报销弹窗单滚动改造。

### 后端与合同

- `ExpenseCategory` 在不改变已有枚举顺序的前提下增加末尾值 `Unspecified`，用于表达尚未选择类别的草稿费用；字符串枚举直接存储，无需数据库迁移。
- `ClaimSubmissionValidator` 在提交审核时拒绝任何 `Unspecified` 费用，返回 `category: 每项费用均需选择类别。`；新增对应单元测试。
- 隔离真实 HTTP 创建普通单据草稿时发送 `category: Unspecified` 返回成功；随后提交返回 400 和 `CLAIM_VALIDATION_FAILED`，草稿状态及空类别数据保持一致。

### 前端与交互

- 新增 `AttachmentPreviewDialog.vue`，继续调用鉴权下载接口获取 Blob；JPG/PNG 使用 `<img>`，PDF 使用 `<iframe>`，关闭弹窗或销毁组件时统一 `URL.revokeObjectURL`，并保留独立下载操作。
- 新增和详情界面的附件均增加“预览”入口，私有附件目录仍不通过静态路径公开。
- 差旅行程默认生成“费用 1 / 去程交通”和“费用 2 / 回程交通”；普通单据默认只生成一个空类别“费用 1”；切换类型和手动添加费用均不为新增费用预选类别。
- 空类别保存草稿时映射为 `Unspecified`，编辑回显重新显示为空；提交前前端同时提示选择具体类别。
- “添加费用”按钮移动到费用列表末尾并占用独立整行；报销弹窗改为固定头尾、仅 `.el-dialog__body` 纵向滚动，新增和编辑共用同一规则。

### 验证结果与运行态

- `dotnet build TravelReimbursement.slnx -c Release --no-restore`：0 警告、0 错误。
- `dotnet test TravelReimbursement.slnx -c Release --no-build --no-restore`：13/13 通过。
- `npm.cmd run build`：通过，仅保留既有 Vite 大包提示。
- 隔离浏览器：差旅默认两项往返类别；普通单据和手动新增费用类别为空；费用列表 DOM 顺序为费用 1、费用 2、添加费用。新增弹窗和编辑弹窗都只有正文区域可滚动，弹窗容器、表单内容和页面没有第二条纵向滚动。
- 上传 595 字节有效 PDF 后预览为 1 个 Blob `iframe`；上传 PNG 后预览为 1 个 Blob `img`；两类预览关闭后原 Blob URL 均返回 `TypeError`，确认资源已释放。浏览器控制台 0 错误。
- 一次性 `55183` API、`5174` 前端、`travel_acceptance_20260803_1919` 数据库和 `.tmp/attachment-preview-e2e-20260803-1919` 附件目录已精确停止并删除；当前业务数据库和活动附件目录未写入验收数据。
