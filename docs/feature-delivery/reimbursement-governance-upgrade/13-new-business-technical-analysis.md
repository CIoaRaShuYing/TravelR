# 新增业务功能：技术分析

## 现有扩展点

- 后端集中于 `Program.cs` Minimal API，领域实体位于 `Domain/Entities.cs`，报销状态机位于 `Services/ClaimWorkflowService.cs`。
- `ReimbursementClaim` 已有不可变 `ClaimVersion`、审批、报销发放与并发令牌，可在版本下增加自动餐补子记录，并保持差旅与餐补独立状态。
- `AppUser` 可扩展个人姓名与加密银行卡字段；ASP.NET Core Data Protection 已包含在共享框架中，无需新增包。
- 前端使用 Vue Router、Element Plus 和集中 `api.ts`，可增加资料页、周报页、管理员餐补操作和导出按钮。
- 项目没有 XLSX 依赖；使用 BCL `ZipArchive`、`XmlWriter` 输出 Open Packaging Convention 工作簿，避免引入重大依赖。

## 关键边界

- 银行卡数据库只保存 Data Protection 密文；完整卡号仅在本人资料接口和管理员用户接口解密返回，审计不记录原文。
- 资料门禁不改变 JWT 身份本身：认证后由 API 中间件限制业务路由，只放行本人资料、修改密码与退出相关访问。
- 餐补随差旅版本创建；差旅审核和餐补审核分别写记录，差旅驳回事务性联动餐补驳回。
- 周报独立建表，唯一键为用户、项目、周一日期；管理员编辑不改变作者。
- 导出按 `SubmittedAt` 和当前有效版本查询，项目必选、日期默认上月 10 日至本月 10 日含首尾。

## 风险

- Data Protection 密钥必须在部署时持久化，否则应用重建后无法解密银行卡；使用现有文件系统密钥环并在部署文档标明备份要求。
- XLSX 生成必须用测试解压并检查工作簿关系、工作表 XML 和敏感字段。
- 当前 `Program.cs` 较大；新增业务服务以独立文件承载，路由仍按现有风格接入，避免无关重构。
- 不启用 subagent；由主 Agent 完成只读分析和实现，避免多编辑者冲突。
