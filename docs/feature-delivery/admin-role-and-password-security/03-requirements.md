# 功能需求

- `POST /api/admin/users/{id}/administrator/grant`：授予管理员角色。
- `POST /api/admin/users/{id}/administrator/revoke`：取消管理员角色。
- `PUT /api/me/password`：用户验证原密码后修改本人密码。
- `PUT /api/admin/users/{id}/password`：管理员重置其他用户密码。
- 角色变化、个人改密、管理员重置密码均写入审计。
- `13730614340` 的管理员角色不可取消；停用用户不可升级。
