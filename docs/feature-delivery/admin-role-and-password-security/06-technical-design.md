# 技术设计

- 新增 `ChangePasswordRequest`、`ResetPasswordRequest`。
- 新增 `/api/me/password`、管理员角色授予/取消接口和管理员密码重置接口。
- 新增 `AccountSecurityView.vue` 与 `/account/security` 路由。
- `AppShell.vue` 对所有登录用户展示“账号安全”。
- `AdminUsersView.vue` 增加角色操作、超级管理员标识和重置密码弹窗。
- 错误码包含 `PASSWORD_INCORRECT`、`PASSWORD_UNCHANGED`、`USER_INACTIVE_ROLE_CHANGE`、`USER_SELF_ADMIN_REVOKE`、`SUPER_ADMIN_ROLE_REQUIRED`、`USER_SELF_PASSWORD_RESET`。
