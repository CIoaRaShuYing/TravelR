# 差旅报销 Web：实现记录
## 2026-08-03：附件类型误判与错误提示修复

- 根因：上传校验把文件大小、浏览器 MIME 和扩展名合并为同一个条件，扩展名比较区分大小写；合法的 `.PDF`、`.JPG`、`.PNG`，以及浏览器以 `application/octet-stream` 上传的真实 PDF 会被统一误报为“仅支持 10MB 以内”。
- 后端变更：保留单文件 10MB 上限；扩展名统一转为小写，并使用 PDF、JPEG、PNG 文件头校验真实内容；附件记录保存规范 MIME；空文件、实际超限、类型不支持或扩展名与内容不一致分别返回明确错误。
- 前端变更：选择文件时立即校验扩展名和 10MB 上限，无效文件不进入待上传状态；行程报销和单据报销页面明确提示单个凭证支持 JPG、PNG、PDF，最大 10MB。
- 安全边界：文件头与扩展名必须一致，仅修改扩展名的伪装文件仍会被拒绝；未放宽附件类型和大小限制。
- 验证：`dotnet build TravelReimbursement.slnx --no-restore` 零警告、零错误；`dotnet test TravelReimbursement.slnx --no-build --no-restore` 8/8 通过，覆盖大写 PDF、通用 MIME、大写 JPG、伪装文件和实际超限；`npm.cmd run build` 通过，仅保留既有大包提示。

## 2026-07-31：首位管理员注册引导与登录切换

- 根因复核：首位管理员角色授予已存在，但 `Closed` 策略在管理员判定前提前阻断，公开设置也未返回“首位管理员待创建”状态，页面无法提前提示。
- 后端变更：公开注册设置新增 `initialAdministratorRegistration`；首位管理员事务优先于三态策略；即时注册响应新增 `registrationCompleted`；手机号校验和重复提示移除遗留邮箱文案。
- 前端变更：登录入口在无管理员时显示“创建首位管理员账户”；注册表单增加琥珀色系统初始化提示，明确管理员、审核人、申请人三种角色；即时注册成功后切回登录表单、带入手机号并清空密码；待审核申请保持等待审核语义。
- 验证：`dotnet build TravelReimbursement.slnx --no-restore` 零警告、零错误；`dotnet test TravelReimbursement.slnx --no-build --no-restore` 4/4 通过；`npm.cmd run build` 通过，仅保留既有大包提示。
- 隔离 E2E：唯一命名临时数据库设置为 `Closed` 后，公开设置仍声明首位管理员注册可用；首位注册和登录成功，三个角色全部存在；临时数据库、临时角色、测试账号和日志随验证结束清理。
- 页面检查：桌面和 390px 移动端无横向溢出，浏览器控制台无错误；当前实际隔离库已存在管理员，因此实际页面正确显示普通注册状态，未改动现有账户。

## 2026-07-31：手机号注册 Identity 校验修复

- 根因：账户注册已改用手机号，但 Identity 仍配置 `RequireUniqueEmail = true`，使空邮箱在 `UserManager.CreateAsync` 中触发 `InvalidEmail`，注册接口返回 `400`。
- 变更：关闭该邮箱唯一性选项；手机号唯一性仍由用户名、`AppUser.PhoneNumber` 唯一索引与待审核申请去重保证。同步删除审批转正流程中不可达的邮箱重复查询。
- 验证：`dotnet build TravelReimbursement.slnx --no-restore` 零警告、零错误；`dotnet test TravelReimbursement.slnx --no-build --no-restore` 4/4 通过；使用 `travel_reimbursement_local` 启动的 API `/health` 返回 `200`，前端 `http://127.0.0.1:5174` 返回 `200`。
- 待人工验收：由用户使用姓名、手机号、密码注册首位管理员账户；为保留该资格，未使用测试账号发送有效注册请求。

## 2026-07-30：工程骨架与首个业务闭环

- 当前里程碑：M1 至 M3 基础实现，待真实容器运行验证。
- 已完成：
  - 创建 ASP.NET Core 8、Vue 3、PostgreSQL、MinIO 的单体工程与 Docker Compose。
  - 实现开放注册、管理员审核注册、不开放注册，且注册 API 在服务端读取当前策略。
  - 实现登录 JWT、`Applicant`/`Reviewer`/`Administrator` 三类角色及管理员初始账号环境变量。
  - 实现行程与普通报销、费用条目、私有附件、提交校验、审核批准/驳回、审计记录。
  - 行程提交校验仅要求去程与回程交通；住宿为零到多项，支持当天往返。
  - 生成 PostgreSQL 初始 EF Core 迁移。
- 变更文件：`backend/`、`frontend/`、`docker-compose.yml`、`.env.example`、`README.md`。
- 已运行验证：
  - `dotnet build TravelReimbursement.slnx --no-restore`：通过，零警告零错误。
  - `npm.cmd run build`：通过；Vite 仅报告产物体积建议。
  - 浏览器本地登录页：桌面和 390px 移动端未出现横向溢出，页面明确显示住宿可选。
  - `.tools/dotnet-ef migrations add InitialCreate ...`：通过。
  - `dotnet test TravelReimbursement.slnx --no-restore`：通过，4/4；覆盖当天往返、去/回程缺项、普通报销、附件必填规则。
- 已知缺口：Docker Desktop 未运行，`docker compose up --build -d` 无法连接 Docker daemon，因此尚未完成真实 PostgreSQL/MinIO 启动、完整 API 状态流转和容器 E2E 验证。
- 下一步：在 Docker Desktop 启动后执行 Compose 启动，验证注册三态、当天往返提交、附件越权下载、审核批准/驳回。

## 2026-07-30：真实依赖环境验证与待修复项

- 已完成：启动 Docker Desktop，并运行本项目 PostgreSQL 与 MinIO 容器；API 健康检查返回 `200`。
- 已修复：
  - PostgreSQL 不支持原 `byte[]` RowVersion 的数据库生成假设，移除该映射并重新生成初始迁移。
  - API 枚举统一使用字符串 JSON 契约，前端可正确接收 `ApprovalRequired`、`Travel` 等值；注册申请状态查询显式解析字符串。
  - 移除受限 Windows 会话中会导致错误日志二次失败的 EventLog provider，仅使用控制台日志。
- 真实 HTTP 证据：审核注册申请创建返回 `202`，申请审批前登录返回 `401`，管理员将模式切换为 `Closed` 后注册接口返回 `400`。
- 未通过：旅行报销 `PUT /api/claims/{id}` 更新费用条目时出现 `DbUpdateConcurrencyException`，尚未完成 MinIO 上传、报销提交和审核批准的真实端到端验证。
- 环境限制：完整 Compose 前端镜像因 Docker Hub 拉取 `node`/`nginx` 匿名令牌超时未完成；本地 Vue 产物构建和浏览器布局检查已通过。
- 下一步：定位并消除旅行报销更新的 EF 实体跟踪/并发更新异常，重跑私有附件、当天往返提交、审核批准与附件越权访问验证。

## 2026-07-30：业务边界澄清

- 已确认：住宿不是差旅行程报销的必填项；系统必须保留当天往返场景。
- 边界落点：无论行程日期是否相同，提交校验仅要求报销说明、去程交通和回程交通；住宿为零到多项。当天往返允许出发日期等于返程日期。
- 已回写文档：`02-business-boundary.md`、`03-requirements.md`、`04-acceptance-criteria.md`、`06-technical-design.md`、`07-task-list.md`。
- 后续实施顺序：先完成 TASK-007 修复草稿更新并回归验证，再执行 TASK-008 真实端到端验收。

## 2026-07-30：TASK-007 报销草稿更新修复与真实验证

- 根因处置：旅行行程和费用条目原先仅通过已跟踪报销单的导航集合挂载，实体状态依赖 EF Core 自动推断；同时替换费用时旧条目仍保留在集合中。
- 变更：新增行程时显式调用 `db.TravelItineraries.Add`；费用更新时先显式删除并清空无附件旧条目，再用 `db.ExpenseItems.AddRange` 写入新条目；同步更新草稿 `TotalAmount`；移除更新路径不需要的申请人导航加载。
- 验证：`dotnet build TravelReimbursement.slnx --no-restore` 通过；`dotnet test TravelReimbursement.slnx --no-restore` 4/4 通过；在独立 PostgreSQL 临时数据库中以真实 HTTP 连续执行两次当天往返、零住宿的 `PUT /api/claims/{id}`，最终回读 2 条交通费用、总额 200，未再出现 `DbUpdateConcurrencyException`。
- 清理：临时数据库、临时角色及测试 API 进程均已删除/停止；未访问现有业务库数据。
- 下一步：执行 TASK-008，完成 MinIO 凭证上传、行程提交、审核与附件对象级授权验证。

## 2026-07-30：TASK-008 真实附件、提交与审核验收

- 发现并修复：JWT Bearer 的 `IFormFile` 上传端点会被 ASP.NET Core 自动加入防伪元数据，而应用未使用 Cookie 会话和防伪令牌；该端点原先返回 500。已仅对该 Bearer 上传端点调用 `DisableAntiforgery()`，对象级授权与状态校验保持不变。
- 发现并修复：提交、批准和驳回时的新审批记录不再仅挂到导航集合，改为显式 `db.ApprovalRecords.Add`，消除 GUID 主键实体状态依赖自动推断的风险。
- 验证环境：临时 MinIO 容器、临时 PostgreSQL 数据库和临时 API 进程；所有临时资源在测试后已删除。
- 真实 HTTP 验证结果：上传 2 张 PDF 至 MinIO；当天往返行程仅含去程/回程、住宿为 0 条仍可提交；未登录附件下载为 401；其他申请人下载为 403；管理员批准成功；回读到 2 条审批记录（提交、批准）。
- 已运行：`dotnet build TravelReimbursement.slnx --no-restore` 与 `dotnet test TravelReimbursement.slnx --no-restore`，均通过，4/4 测试通过。
- 下一步：补齐三态注册当前运行验证、前端构建与交付文档收尾。

## 2026-07-30：三态注册、普通单据与驳回重提闭环

- 三态注册：隔离 PostgreSQL 真实 HTTP 验证通过。默认审核注册为 `202 -> 401 -> 200`（申请、审批前登录、审批后登录）；关闭注册接口返回 `400`；开放注册为 `200 -> 200`（注册、登录）。
- 普通单据：办公用品和聚餐两项费用各上传 PDF 凭证后提交成功，类型为 `General`，总额为 100。
- 驳回重提：费用请求支持可选条目 `id`，带私有附件的既有条目原地更新，只有无附件条目可删除；前端增加已驳回单据的“继续编辑”入口。真实 HTTP 验证了驳回后保留原凭证、金额 50 更新为 60、重新提交成功并回读 3 条审批记录。
- 已运行：`dotnet build TravelReimbursement.slnx --no-restore`、`dotnet test TravelReimbursement.slnx --no-restore`（4/4）及 `frontend/npm.cmd run build` 均通过；前端构建仅保留 Vite 大包提示。
