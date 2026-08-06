# 用户中心与申请人筛选修复：现状调研

## 项目结构

- 后端入口：`backend/TravelReimbursement.Api/Program.cs`。
- 领域实体：`backend/TravelReimbursement.Api/Domain/Entities.cs`。
- 数据上下文：`backend/TravelReimbursement.Api/Data/AppDbContext.cs`。
- 前端路由：`frontend/src/router.ts`。
- 前端外壳与菜单：`frontend/src/components/AppShell.vue`。
- 前端 API 封装：`frontend/src/api.ts`。
- 报销管理页：`frontend/src/views/AdminClaimsView.vue`。
- 现有注册审批页：`frontend/src/views/AdminRegistrationsView.vue`。

## 后端事实

1. `AppUser` 继承 ASP.NET Identity 用户，包含 `DisplayName` 和 `IsActive`；手机号唯一索引已存在。
2. 角色种子只有 `Applicant`、`Administrator`。
3. JWT 验证阶段检查用户存在且 `IsActive=true`，停用用户会使后续请求失效。
4. 管理员路由统一挂在 `secured.MapGroup("/admin").RequireAuthorization(Roles = "Administrator")` 下。
5. 现有 `GET /api/admin/claims/group-summary?groupBy=applicant` 按 `ReimbursementClaim` 聚合申请人，只能返回至少有一笔非取消报销的用户。
6. 当前没有“正式用户列表”“用户启停”或“申请人目录”端点。
7. 注册开放模式直接创建 `Applicant`；审核模式批准后创建 `Applicant`。两种路径最终都写入 `AspNetUsers` + `AspNetUserRoles`。

## 前端事实

1. 当前路由只有 `/claims` 和四个 `/admin/*` 页面，没有用户中心。
2. `AppShell.vue` 的管理员菜单没有用户管理入口。
3. `AdminClaimsView.vue` 的申请人选项由 `api.getClaimGroupSummary({ groupBy: 'applicant' })` 提供。
4. 该聚合结果同时被分组账本使用；申请人筛选选项和分组结果目前耦合。
5. 现有页面已具备服务端分页、加载、空态、错误提示、移动端抽屉导航和 Element Plus 表格/选择器模式。

## 数据库与权限边界

- 用户身份数据归 ASP.NET Identity 表；角色关系归 `AspNetUserRoles`。
- 报销通过 `ReimbursementClaim.ApplicantId` 关联正式用户，外键为 Restrict。
- 用户只能由管理员读取完整用户目录和执行启停；普通申请人不能读取其他用户目录。
- 账户停用不删除数据；历史报销仍保留申请人名称和关联 ID，停用仅影响登录和新请求授权。

## 关键风险

- 如果用户中心允许停用当前管理员，可能导致当前会话在下一次 JWT 校验时失效；前端应提示确认，后端仍按现有安全规则执行。
- 申请人目录若直接返回全部 Identity 用户而不校验角色，会把无申请人角色的账号暴露为筛选项；必须通过角色关系过滤。
- 申请人下拉若一次性加载无界用户列表，会在数据增长后退化；应保留服务端分页/关键字查询契约。
- 用户中心响应不能包含 `PasswordHash`、安全戳或其他认证敏感字段。

## 结论

这是一个小范围跨层修复：新增管理员用户目录与账号启停能力，拆出只读申请人目录，并将报销管理下拉从“有报销聚合”改为“正式 Applicant 目录”。无需改动注册审批和报销领域模型。
