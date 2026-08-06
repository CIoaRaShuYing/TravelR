# 用户中心与申请人筛选修复：实施日志

## 2026-08-06：后端用户目录与账号启停

- 完成任务：`TASK-UC-001`、`TASK-UC-002`、`TASK-AP-001`。
- 变更文件：`backend/TravelReimbursement.Api/Program.cs`、`frontend/src/api.ts`。
- 新增接口：
  - `GET /api/admin/users`
  - `GET /api/admin/applicants`
  - `POST /api/admin/users/{id}/enable`
  - `POST /api/admin/users/{id}/disable`
- 实现事实：
  - 用户目录只返回拥有 `Applicant` 或 `Administrator` 角色的正式账户，支持关键字、状态和分页。
  - 申请人目录只返回启用、拥有 `Applicant` 角色且有手机号的正式账户，不依赖报销记录。
  - 用户启停写入 `UserEnabled` / `UserDisabled` 审计；拒绝停用当前账号和最后一个启用管理员。
  - 无数据库字段或迁移变更。

## 2026-08-06：前端用户中心与申请人目录接入

- 完成任务：`TASK-AP-002`。
- 变更文件：
  - `frontend/src/views/AdminUsersView.vue`
  - `frontend/src/views/AdminClaimsView.vue`
  - `frontend/src/components/AppShell.vue`
  - `frontend/src/router.ts`
  - `frontend/src/style.css`
- 实现事实：
  - 桌面和移动端管理员导航新增“用户中心”。
  - 用户中心支持姓名/手机号、启用状态、服务端分页和账号启停。
  - 当前登录管理员的停用按钮在页面禁用，后端同时保留强制保护。
  - 报销管理申请人下拉改为远程申请人目录；按申请人分组仍使用报销聚合接口，目录与汇总解耦。

## 2026-08-06：验证与清理

- 完成任务：`TASK-VERIFY-001`。
- 自动验证：
  - `dotnet build TravelReimbursement.slnx -c Release --no-restore -p:BaseOutputPath=D:\Code\chuchai\.tmp\validation-bin\`：通过，0 警告、0 错误。
  - `dotnet test TravelReimbursement.slnx -c Release --no-build --no-restore -p:BaseOutputPath=D:\Code\chuchai\.tmp\validation-bin\`：13/13 通过。
  - `frontend/npm.cmd run build`：通过；保留现有单包体积大于 500 kB 的 Vite 提示，本次未引入代码拆包范围。
  - `git diff --check`：通过，仅有 Git 未来转换 LF/CRLF 的工作区提示。
- 隔离 PostgreSQL 真实 HTTP：
  - 开放注册返回 `registrationCompleted=true`；审核注册返回 `registrationCompleted=false`，批准后可登录且包含 `Applicant` 角色。
  - 正式用户总数 3；开放注册和批准注册用户均在无报销时进入申请人目录。
  - 普通申请人访问用户管理返回 403；停用后旧 JWT 返回 401；目录排除停用用户，启用后恢复；管理员自停用返回 409。
  - 姓名/手机号关键字、状态过滤和分页参数通过；用户响应未出现 `PasswordHash`、`SecurityStamp`、`ConcurrencyStamp`。
- 浏览器验证：
  - 桌面导航存在“用户中心”，列表显示开放注册用户和管理员；当前管理员停用操作禁用。
  - 报销管理申请人下拉显示无报销的 `Open Applicant · 13900001002`。
  - 390×844 移动端抽屉包含用户中心，用户卡片正常，页面 `scrollWidth=innerWidth=390`，控制台 0 错误。
- 清理：唯一命名的临时 API、Vite 服务、PostgreSQL 数据库 `uc_accept_20260806_001`、同名角色和临时附件目录均已停止或删除；未访问或修改现有业务数据库。
