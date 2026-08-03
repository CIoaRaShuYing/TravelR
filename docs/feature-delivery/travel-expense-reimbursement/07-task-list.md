# 差旅报销 Web：实现任务清单

## M1：工程骨架与身份治理

### TASK-001

- Milestone: M1
- Linked requirement: REQ-001 至 REQ-004、REQ-010
- Linked acceptance criteria: 注册治理
- Goal: 建立 Vue、ASP.NET Core、PostgreSQL、MinIO 的本地开发与 Docker Compose 骨架。
- Files likely to change: 根目录解决方案、`frontend/`、`backend/`、`docker-compose.yml`、`.env.example`、部署文档。
- Implementation steps: 初始化项目；添加健康检查；配置数据库与私有对象桶；建立迁移和本地启动脚本；不写入真实密钥。
- Verification method: 容器启动；Web/API 健康检查；空库迁移成功。
- Done condition: 新开发者只用示例环境变量即可启动完整本地依赖。
- Status: done（PostgreSQL、MinIO 容器已真实运行；API 健康检查和空库迁移在隔离数据库中通过。）

### TASK-002

- Milestone: M1
- Linked requirement: REQ-001 至 REQ-004、REQ-010
- Linked acceptance criteria: 注册治理
- Goal: 实现 Identity、角色、注册模式、注册申请与管理员审核。
- Files likely to change: Identity 实体、认证/管理 Controller、服务层、迁移、登录注册页面、管理设置页面、测试。
- Implementation steps: 实现 `Open`、`ApprovalRequired`、`Closed` 分支；添加管理员创建/授权初始账号机制；写入审计记录；服务端拦截关闭注册。
- Verification method: 按验收文档执行三种注册模式及接口绕过测试。
- Done condition: REQ-001 至 REQ-004 的全部验收通过。
- Status: done（隔离 PostgreSQL 真实 HTTP 已覆盖审核注册、关闭注册和开放注册三种模式。）

### TASK-009

- Milestone: M1
- Linked requirement: REQ-001 至 REQ-004、REQ-015
- Linked acceptance criteria: 注册治理、首位管理员注册引导
- Goal: 补齐首位管理员注册的公开状态、关闭模式初始化例外、醒目页面提示和成功后登录切换。
- Files likely to change: `backend/TravelReimbursement.Api/Program.cs`、`frontend/src/api.ts`、`frontend/src/App.vue`、`frontend/src/style.css`、注册测试与交付文档。
- Implementation steps: 公开首位管理员注册状态；调整注册判断顺序；修正手机号提示；实现页面身份提示条；立即注册成功后带手机号切回登录表单。
- Verification method: 后端构建和测试、前端生产构建、空管理员数据库只读设置验证、浏览器桌面与移动端检查；不使用有效注册请求占用用户的首位管理员账号。
- Done condition: 空管理员状态可见、三态均不阻断首位管理员按钮、后端仍以事务判定首位角色、立即注册响应能驱动登录切换。
- Status: done（后端构建零警告、测试 4/4 通过，前端生产构建通过；临时隔离数据库完成 `Closed` 模式首位管理员真实 HTTP 验证并已清理；实际 `travel_reimbursement_local` 已存在管理员，未删除或修改现有账号。）

## M2：报销领域与私有凭证

### TASK-003

- Milestone: M2
- Linked requirement: REQ-005 至 REQ-008、REQ-010、REQ-012
- Linked acceptance criteria: 报销录入
- Goal: 实现报销单、行程、费用条目、附件和草稿保存。
- Files likely to change: 领域实体、EF Core 映射/迁移、报销 API、对象存储服务、申请人页面、测试。
- Implementation steps: 建立 `Travel`/`General` 类型；保持一张旅行报销单对应一次行程；费用条目和附件一对多；实现私有上传、下载鉴权、文件类型/大小/哈希校验。
- Verification method: 创建旅行与普通草稿；上传多张凭证；无权限用户不能下载附件。
- Done condition: 草稿可可靠保存，附件不公开且可由授权用户读取。
- Status: done（真实 PostgreSQL/MinIO 已验证草稿保存、两张私有凭证上传和对象级下载授权。）

### TASK-004

- Milestone: M2
- Linked requirement: REQ-005 至 REQ-008
- Linked acceptance criteria: 报销录入
- Goal: 实现提交前后端校验，支持当天往返与始终可选的住宿。
- Files likely to change: 提交领域服务、验证器、提交 API、旅行编辑页面、单元/集成测试。
- Implementation steps: 校验旅行说明、去程交通、回程交通、金额、合格附件；不校验住宿是否存在；允许出发日期等于返程日期；前端展示精确缺项，后端作为最终裁决。
- Verification method: 执行六组测试数据，覆盖缺去程、缺回程、无住宿、含住宿、办公用品、聚餐。
- Done condition: REQ-005 至 REQ-008 的验收通过。
- Status: done（当天往返零住宿、住宿可选和普通单据报销均已通过真实 API 验收。）

### TASK-007

- Milestone: M2
- Linked requirement: REQ-005 至 REQ-008、REQ-010
- Linked acceptance criteria: 报销录入
- Goal: 修复旅行报销草稿更新的 EF Core 并发异常，使行程基础信息与无附件费用条目可稳定保存。
- Files likely to change: `backend/TravelReimbursement.Api/Program.cs`、相关 API/集成测试、实施记录。
- Implementation steps: 先记录并定位实际发生零行更新的实体；移除报销编辑路径中不必要的申请人跟踪；将行程新增和费用条目替换改为显式实体状态操作，并以事务保存；保留“已有附件的费用条目不得整体替换”的数据保护规则。
- Verification method: 以新建旅行草稿连续更新两次，分别覆盖当天往返无住宿与含住宿行程；确认两次 `PUT /api/claims/{id}` 均成功且数据正确回读。
- Done condition: 不再出现 `DbUpdateConcurrencyException`，且不破坏附件归属、总额计算和申请人权限。
- Status: done（已改为显式新增行程/费用条目、显式删除旧费用条目并更新草稿总额；真实 PostgreSQL 连续两次当天往返零住宿更新及回读通过）

### TASK-008

- Milestone: M2-M3
- Linked requirement: REQ-005 至 REQ-012
- Linked acceptance criteria: 报销录入、审核和审计
- Goal: 完成真实依赖环境下的端到端验收，重点覆盖当天往返、附件私有性和审核状态流转。
- Files likely to change: API 集成测试或验收脚本、实施记录、README 验收说明。
- Implementation steps: 上传合格的去程和回程凭证；提交当天往返无住宿行程；管理员审核通过；验证无令牌下载为 401、其他申请人下载为 403；补测含住宿行程与普通单据报销。
- Verification method: PostgreSQL、MinIO 与 API 实例均运行时的真实 HTTP 验收。
- Done condition: REQ-005 至 REQ-012 的关键验收路径均具有可复现证据。
- Status: done（隔离 MinIO 与 PostgreSQL 的真实 HTTP 验收通过：两张 PDF 上传、当天往返零住宿提交、管理员批准、未登录下载 401、其他申请人下载 403、提交和批准两条审批记录均已回读）

## M3：审核、审计与交付验证

### TASK-005

- Milestone: M3
- Linked requirement: REQ-009 至 REQ-011
- Linked acceptance criteria: 审核和审计
- Goal: 实现审核工作台、批准/驳回、重提和管理员查询。
- Files likely to change: 审批服务/API、审批记录/审计实体、审核与管理页面、测试。
- Implementation steps: 按状态机做条件更新；驳回意见必填；追加审批记录；实现报销、注册申请筛选与分页。
- Verification method: 申请人、审核人、无权限用户的状态机和权限测试。
- Done condition: REQ-009 至 REQ-011 的验收通过。
- Status: done（真实 API 已覆盖提交、批准、审批记录回读及附件对象级授权）

### TASK-006

- Milestone: M3
- Linked requirement: REQ-001 至 REQ-012
- Linked acceptance criteria: 全部
- Goal: 完成安全加固、自动化测试、文档和可部署验收包。
- Files likely to change: 测试项目、CI 配置、Docker/Nginx 配置、README、运行手册、OpenAPI 文档。
- Implementation steps: 加入安全响应头、上传限制、日志脱敏、错误统一处理；完成单元/集成/E2E 测试；编写备份与恢复说明。
- Verification method: CI 构建、测试套件、Docker Compose 冒烟、手工验收清单。
- Done condition: 全部 Must 验收通过，部署与回滚步骤可复现。
- Status: done（后端构建与测试、前端生产构建均通过；README 已补充本地运行、验证、备份与回滚边界。生产部署仍需由持有真实环境变量的操作者执行。）
