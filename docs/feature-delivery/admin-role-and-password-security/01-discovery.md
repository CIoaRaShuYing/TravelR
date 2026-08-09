# 现状调研

- 后端使用 ASP.NET Core Identity，角色表和 `SecurityStamp` 已存在。
- `Program.cs` 已有管理员用户目录和启停接口，但没有角色变更或改密接口。
- JWT 当前有效期 8 小时，仅检查用户存在及启用状态，需增加安全戳校验。
- 前端已有 `AdminUsersView.vue`、`AppShell.vue` 和统一 `api.ts` 请求层。
- 现有审计入口为 `AuditAsync`，安全操作可复用 `AuditLog`。
