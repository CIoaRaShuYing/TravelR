# 用户中心与申请人筛选修复：只读技术分析

## 现有实现与目标差异

| 目标 | 当前实现 | 差异 |
| --- | --- | --- |
| 管理员查看正式用户 | 没有用户列表 API 或页面 | 新增管理员目录 API、Vue 页面、路由和菜单 |
| 管理员启停账号 | `AppUser.IsActive` 已存在，JWT 已检查该字段，但没有管理端点 | 新增启用/停用端点和审计记录 |
| 申请人下拉包含无报销用户 | `AdminClaimsView` 调用 `group-summary?groupBy=applicant` | 新增 `admin/applicants` 目录，分组汇总继续保留原语义 |
| 用户权限 | 管理员组统一由 `/api/admin` 保护 | 新端点复用同一授权组，不新增角色 |

## 后端分析

- `AppUser` 已是 Identity 实体，`IsActive` 可直接复用，无需数据库迁移。
- 角色关系存在 `AspNetUserRoles`，申请人目录必须通过角色名 `Applicant` 过滤，不能仅按 `AppUser` 查询。
- 用户列表需要同时返回角色摘要，采用“先分页用户、再批量读取角色关系”的两次查询，避免逐行调用 `UserManager.GetRolesAsync`。
- 账号启停使用现有 `AuditLog` 追加记录；不删除用户、不写入认证敏感字段。
- 停用最后一个启用管理员会造成系统管理面失效，因此后端拒绝该操作；停用当前登录管理员也拒绝，避免页面误操作后会话立即失效。
- 申请人目录只返回 `IsActive=true` 且拥有 `Applicant` 角色的正式用户，支持姓名/手机号关键字、分页和稳定按姓名/手机号排序。

## 前端分析

- 新增 `AdminUsersView.vue`，复用现有管理页的表格/移动卡片、筛选、分页、确认对话框和错误提示模式。
- `AppShell.vue` 增加“用户中心”管理员菜单，移动端抽屉自动复用同一 `navItems`。
- `router.ts` 增加 `/admin/users`，沿用 `administrator: true` 路由守卫。
- `AdminClaimsView.vue` 的申请人选项从报销分组中解耦；使用 `api.listApplicants` 载入目录。下拉保留远程关键字查询，仍按 ID 传给既有 `/api/admin/claims`。
- 申请人分组账本继续使用 `getClaimGroupSummary`，因此无报销用户会出现在筛选项但不会凭空出现在“有报销分组”账本中。

## 验证边界

- 静态验证：TypeScript、C# 编译、接口响应字段和授权代码。
- 自动验证：现有 .NET 测试集；如无集成测试宿主，则补充可编译的查询/规则测试或记录真实 HTTP 未执行。
- 真实验收：需要 PostgreSQL、运行中的 API 和浏览器，验证开放注册/审批通过用户均可出现在申请人下拉，以及启停后的 JWT 行为。
