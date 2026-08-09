# 技术分析

- JWT 增加当前 `SecurityStamp` 声明；`OnTokenValidated` 查询用户并严格比较安全戳。
- 角色变更显式调用 `UpdateSecurityStampAsync`，从而使升降级后的旧令牌即时失效。
- 个人改密使用 `ChangePasswordAsync`。
- 管理员重置使用默认 Token Provider 的 `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync`。
- 复用现有 Identity 表与 `AuditLogs`，无需 EF Core 数据库迁移。
