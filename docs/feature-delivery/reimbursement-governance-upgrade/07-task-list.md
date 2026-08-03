# 报销治理升级：实施任务清单

M1 至 M7 已完成。功能已通过隔离 PostgreSQL、真实浏览器和 Docker 运行验收，实际业务数据库与附件已按授权完成破坏式重建，MinIO 已从代码、配置、部署和运行资源中移除。

## M1：新领域模型与安全重建准备

### TASK-201

- Milestone: M1
- Linked requirement: REQ-102 至 REQ-108、REQ-111、REQ-115
- Linked acceptance criteria: 项目归属、版本一致性、发放、角色简化
- Goal: 建立项目、报销主记录、版本、附件资产、审批和发放领域模型。
- Files likely to change: `Domain/Entities.cs`、`Data/AppDbContext.cs`、`Contracts/`、提交验证器及测试。
- Implementation steps: 增加实体/枚举/索引；将行程和费用改挂版本；增加当前版本指针和并发戳；角色只保留申请人/管理员；更新提交校验以允许不完整草稿。
- Verification method: 后端构建；模型和状态机单元测试；迁移模型审阅。
- Done condition: 模型能表达全部状态、版本、项目和发放约束，旧版本不会进入当前汇总。
- Status: done
- Notes: 新领域模型、提交校验、状态机和 10 个单元测试已落地；空库 HTTP 冒烟验证了 v1/v2 作废、旧版本审批 409、批准、发放和不可撤销。

### TASK-202

- Milestone: M1
- Linked requirement: REQ-112、REQ-115
- Linked acceptance criteria: 破坏式重建、角色简化
- Goal: 建立可复现且目标明确的空库基线和一次性重建步骤，但暂不清理实际数据。
- Files likely to change: `Data/Migrations/`、`.config/dotnet-tools.json`、`.env.example`、`docker-compose.yml`、README/运维文档。
- Implementation steps: 固定 `dotnet-ef`；移除旧迁移并生成 `InitialGovernanceRebuild`；将 BootstrapAdmin 改为手机号；移除无用 Compose 本地上传卷；编写只显示并核对精确目标的重建流程。
- Verification method: 唯一命名临时 PostgreSQL 空库执行迁移；临时 MinIO 桶验证；`docker compose config`。
- Done condition: 新代码可从空库启动，只有两个业务角色，手机号管理员可登录；实际业务库尚未清理。
- Status: done
- Notes: 已生成单一 `InitialGovernanceRebuild`，临时库成功创建 19 张业务/Identity 表；BootstrapAdmin 已改为手机号，Compose 移除无用本地上传卷并通过配置校验。实际业务数据留待 TASK-209。

## M2：管理员注册审批与项目管理纵切片

### TASK-203

- Milestone: M2
- Linked requirement: REQ-101 至 REQ-104、REQ-113、REQ-115
- Linked acceptance criteria: 注册审批、项目与归属、分页权限
- Goal: 交付管理员可用的注册审批和项目管理。
- Files likely to change: `Endpoints/AdminEndpoints.cs`、项目/注册 DTO 与服务、`frontend/src/api/`、管理员页面和菜单。
- Implementation steps: 注册申请真正分页；批准/拒绝并发保护；项目新增/编辑/启停；启用项目选项接口；管理员路由和正式对话框；移除 Reviewer UI。
- Verification method: 管理员/申请人双角色 API 测试；注册批准/拒绝；项目唯一、停用、伪造 ID；前端构建和浏览器检查。
- Done condition: 管理员能在页面完成注册审批和项目治理，普通申请人无法访问管理接口。
- Status: done
- Notes: 后端注册审批、项目管理、分页、权限和审计接口通过 HTTP 冒烟；前端管理页面、桌面侧栏和移动抽屉导航已完成浏览器验收。

## M3：附件资产与版本化报销纵切片

### TASK-204

- Milestone: M3
- Linked requirement: REQ-105 至 REQ-107、REQ-111
- Linked acceptance criteria: 草稿、版本一致性、附件审计
- Goal: 将附件改造成可跨同一报销版本复用的不可变资产。
- Files likely to change: `Services/PrivateFileStore.cs`、`AttachmentAssetService.cs`、附件 Endpoint、实体映射和测试。
- Implementation steps: 增加 staged 上传、报销绑定、关联表、DeleteAsync 补偿、24 小时临时清理、下载授权。
- Verification method: 上传成功/失败补偿、跨用户/跨报销复用拒绝、旧版本附件可读、临时对象清理测试。
- Done condition: 版本修改不会复制或丢失实际附件，无主对象有明确清理路径。
- Status: done
- Notes: staged 上传、下载、绑定复用、写库失败补偿和 `StagedAttachmentCleanupService` 已落地；默认清理超过 24 小时的未绑定附件。

### TASK-205

- Milestone: M3
- Linked requirement: REQ-103 至 REQ-107、REQ-110、REQ-111
- Linked acceptance criteria: 项目必选、草稿保存、编辑删除、版本冲突
- Goal: 实现申请人报销版本服务和 API。
- Files likely to change: `ClaimVersionService.cs`、`ClaimWorkflowService.cs`、`Endpoints/ClaimEndpoints.cs`、DTO、验证器和测试。
- Implementation steps: 新建 v1；保存 vN+1；Submitted 编辑回 Draft；提交；Cancel 软删除；版本详情；项目和附件校验；409 错误码；审计。
- Verification method: Draft/Submitted/Rejected/Approved/Cancelled 状态矩阵；两个并发保存；管理员看 v1、用户保存 v2 后旧审批冲突。
- Done condition: 批准前可编辑/撤回，每次保存保留旧版本，任何旧版本都不能被审批。
- Status: done
- Notes: 后端新建、保存新版本、提交、撤回、审批冲突和发放状态机通过临时环境冒烟；v1/v2 版本替换、旧版本审批 409 和状态矩阵已验收。

### TASK-206

- Milestone: M3
- Linked requirement: REQ-103 至 REQ-107、REQ-110、REQ-113
- Linked acceptance criteria: 我的报销和草稿交互
- Goal: 交付申请人可用的项目化、版本化报销页面。
- Files likely to change: `frontend/package.json`、路由、申请人页面、编辑器、附件组件、状态标签、API DTO 和样式。
- Implementation steps: 引入 Vue Router；拆分 App.vue；项目必选；保存草稿/提交分离；各状态编辑/删除按钮；版本冲突提示；项目/状态筛选和分页；详情/版本时间线；移动端适配。
- Verification method: 前端构建；不完整草稿；附件保留/替换；Submitted 编辑警告；409；1440/1024/768/390px 浏览器检查。
- Done condition: 用户能完整完成新建、保存、提交、编辑、撤回、筛选和查看版本历史。
- Status: done
- Notes: `ClaimEditorDialog`、`ClaimDetailDrawer`、项目/状态筛选、附件、版本时间线和移动端适配已完成；新建/编辑/详情采用对话框和抽屉，不额外增加页面路由。

## M4：管理员报销审批、发放与汇总

### TASK-207

- Milestone: M4
- Linked requirement: REQ-107 至 REQ-109、REQ-111、REQ-113、REQ-115
- Linked acceptance criteria: 版本审批、发放、全部报销汇总
- Goal: 交付统一管理员报销管理 API 和页面。
- Files likely to change: `ClaimWorkflowService.cs`、`Endpoints/AdminEndpoints.cs`、管理员报销页面、筛选/分组/详情组件。
- Implementation steps: 待审批/待发放/全部三个标签；版本化批准/驳回；不可逆发放；组合筛选；项目/用户分组摘要；总笔数/总金额；允许管理员自审并审计。
- Verification method: v1/v2 竞态；批准/驳回；重复发放与回退失败；组合筛选；数据库聚合对账；普通用户 403。
- Done condition: 管理员能看到并管理所有用户、项目、审批和发放状态，汇总只计算当前版本。
- Status: done
- Notes: `AdminClaimsView` 已交付待审批、待发放、全部报销、组合筛选、数据库汇总、项目/人员分组、版本化审批和不可逆发放；显式筛选 `Cancelled` 的分组逻辑已修复。

## M5：安全、破坏式切换与完整验收

### TASK-208

- Milestone: M5
- Linked requirement: REQ-101 至 REQ-115
- Linked acceptance criteria: 全部已确认验收标准
- Goal: 补齐并发、安全、权限、错误处理和真实依赖测试。
- Files likely to change: 测试项目、JWT 验证、错误映射、前端页面状态、验证脚本和文档。
- Implementation steps: 用户存活校验；统一 409 错误码；注册/项目/版本/附件/发放测试；真实 PostgreSQL 并发测试；页面 loading/empty/error；开发和 Docker 代理验证。
- Verification method: `dotnet build`、`dotnet test`、`npm run build`、临时 PostgreSQL/MinIO HTTP E2E。
- Done condition: Must 验收在临时环境全部通过，才允许触碰实际业务数据库和附件。
- Status: done
- Notes: 后端构建 0 错误，测试 10/10 通过，前端生产构建通过；临时 PostgreSQL/MinIO 完整 HTTP 流程、真实浏览器和 API 生产 Dockerfile 运行验收通过。固定 `node:24-alpine`、`nginx:1.27-alpine` 拉取因 Docker Hub 网络超时未完成，但使用本机 Nginx 镜像挂载同一 `dist` 与 `nginx.conf` 完成等价运行验收。

### TASK-209

- Milestone: M5
- Linked requirement: REQ-112
- Linked acceptance criteria: 破坏式重建
- Goal: 在精确核对目标后执行用户已授权的数据库和附件清空，并部署新基线。
- Files likely to change: 不以生产代码修改为目标；更新实施记录和验收证据。
- Implementation steps: 停止写入；显示脱敏目标；核对数据库/Compose 卷/桶/本地目录；记录计数；清空数据库和对应附件；应用新迁移；初始化手机号管理员；确认旧业务数据和对象为零。
- Verification method: 清理前后计数、空库迁移、管理员登录、附件桶检查、完整 HTTP/browser E2E。
- Done condition: 新系统从空数据启动并通过全部关键路径；明确报告旧数据不可恢复。
- Status: done
- Notes: 已精确核对并重建 `travel_reimbursement`、`travel_reimbursement_local`，清空 `D:\Code\chuchai\private-uploads`。最终主库 20 张表、1 条迁移、1 个初始化管理员、2 个业务角色，项目/报销/附件/注册申请均为 0；MinIO 无业务桶和对象。

### TASK-210

- Milestone: M5
- Linked requirement: REQ-101 至 REQ-115
- Linked acceptance criteria: 全部
- Goal: 完成交付记录和运行手册。
- Files likely to change: `08-implementation-log.md`、README、API/运维说明。
- Implementation steps: 记录每个里程碑文件、命令、结果和缺口；补充项目/审批/发放操作说明与不可逆边界。
- Verification method: 文档命令复跑、链接检查、验收状态逐条核对。
- Done condition: 实现证据和未完成环境验收均有明确记录。
- Status: done
- Notes: README、技术设计、验收结果、任务清单和实施日志已同步。分页服务端实现已验证，但未实际制造超过 20 条数据验证跨页边界；固定前端基础镜像拉取受 Docker Hub 网络超时阻断，均已记录为非阻断回归项。

## M6：本地附件存储收敛

### TASK-211

- Milestone: M6
- Linked requirement: REQ-116
- Linked acceptance criteria: 本地私有目录、启动可写性、路径安全、附件闭环
- Goal: 删除后端 MinIO 依赖和实现，将附件存储收敛为可配置本地目录。
- Files likely to change: `TravelReimbursement.Api.csproj`、`Program.cs`、`Services/PrivateFileStore.cs`、`appsettings.json`、测试项目。
- Implementation steps: 移除 SDK/DI/实现；增加 `LocalPath`；启动写探针；安全解析对象键；增加保存、读取、删除和路径逃逸测试。
- Verification method: 依赖扫描、`dotnet build`、`dotnet test`、真实上传/下载。
- Done condition: 发布产物不含 MinIO，目录不可写时启动失败，本地附件完整闭环通过。
- Status: done
- Notes: 已删除 MinIO SDK、客户端注册和实现，只保留 `LocalPrivateFileStore`；增加启动写探针和对象键路径逃逸保护。后端构建 0 警告、0 错误，12/12 测试通过。隔离空库经公开注册和正式登录完成 staged 上传 201、鉴权下载 200、SHA-256 一致及数据库元数据核对。

### TASK-212

- Milestone: M6
- Linked requirement: REQ-116
- Linked acceptance criteria: 轻量部署、持久卷、备份恢复、旧资源移除
- Goal: 将开发和容器部署统一为本地持久附件目录，并安全移除旧 MinIO 运行资源。
- Files likely to change: `docker-compose.yml`、`.env.example`、README、交付文档。
- Implementation steps: 删除 MinIO 服务和密钥；挂载 `attachments_data`；更新本地开发和备份说明；核对旧对象为空后移除精确容器与卷。
- Verification method: `docker compose config`、配置扫描、运行态健康检查、Docker 资源检查。
- Done condition: 项目不再启动或依赖 MinIO，数据库与本地目录的备份边界明确，旧 MinIO 资源已移除。
- Status: done
- Notes: Compose、`.env.example` 和 README 已移除 MinIO 服务、密钥及部署说明，API 改挂 `attachments_data:/data/private-uploads`，数据库与附件要求同批次备份恢复。旧卷只含 `.minio.sys` 元数据且仅由目标容器使用；`chuchai-minio-1` 和 `chuchai_minio_data` 已精确删除，其他项目容器未修改。

## M7：严格审批合同与会话恢复

### TASK-213

- Milestone: M7
- Linked requirement: REQ-101、REQ-107、REQ-117
- Linked acceptance criteria: 注册审批破坏式合同、待审批版本有效性、刷新保持登录
- Goal: 删除注册审批原因合同和数据库字段，恢复刷新后的标签页会话，并让审批工作台只展示当前有效版本。
- Files likely to change: `Requests.cs`、`Program.cs`、`Entities.cs`、EF 迁移、`api.ts`、`session.ts`、注册审批页、管理员报销页和详情抽屉。
- Implementation steps: 严格拒绝未知 JSON 字段；批准/拒绝只接受并发令牌；删除 `ReviewComment` 列；使用 `sessionStorage` 恢复会话并统一清理；服务端过滤作废版本；审批详情隐藏历史作废版本。
- Verification method: 迁移审查、后端构建和测试、前端生产构建、真实 API 旧/新合同对照、浏览器登录刷新、审批列表与数据库版本对账。
- Done condition: 旧注册审批请求被拒绝，刷新不再要求重新登录，管理员待审批入口不出现已作废记录。
- Status: done
- Notes: 新迁移只删除 `RegistrationRequests.ReviewComment`；旧注册审批合同和空并发令牌返回 400，新合同返回 200。隔离异常数据验证作废当前版本不进入审批列表且不可审批；浏览器验证登录后刷新保持管理员会话，退出后刷新仍为登录页。当前 Release API 已在 55182 健康运行。

## M8：附件预览与报销录入体验修正

### TASK-214

- Milestone: M8
- Linked requirement: REQ-118
- Linked acceptance criteria: 附件在线预览、差旅/普通费用默认项、添加费用位置、单滚动弹窗、空类别草稿合同
- Goal: 让用户在新增和编辑报销时直接预览凭证，并修正费用默认值、添加入口位置和弹窗滚动体验。
- Files likely to change: `Entities.cs`、`ClaimSubmissionValidator.cs`、验证测试、`api.ts`、`ClaimEditorDialog.vue`、`ClaimDetailDrawer.vue`、附件预览组件和全局样式。
- Implementation steps: 增加草稿态 `Unspecified`；提交时拒绝空类别；使用鉴权 Blob 下载实现图片/PDF 预览和资源释放；按报销类型生成默认费用；将添加按钮移至列表末尾；固定弹窗头尾并只滚动正文。
- Verification method: Release 构建和测试、前端生产构建、隔离真实 HTTP、隔离浏览器 DOM/样式/上传预览检查。
- Done condition: 四项用户反馈在新增和编辑流程中可重复验证，且不开放私有附件目录、不引入数据库迁移或第二滚动区域。
- Status: done
- Notes: 后端 13/13 测试通过，前端构建通过；隔离 HTTP 和浏览器完成空类别草稿、默认费用、按钮顺序、单滚动、PDF/PNG 预览及 Blob 释放验证，隔离资源已全部清理。
