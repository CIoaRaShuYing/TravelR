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
- 凭证通过 `ClaimVersion -> ExpenseItem -> ExpenseItemAttachment -> AttachmentAsset` 关联，物理文件由 `IPrivateFileStore.OpenReadAsync` 读取；ZIP 必须使用当前有效版本查询结果中的附件链接，不能仅按 `BoundClaimId` 拉取，否则会混入旧版本凭证。
- 凭证金额来自其所属 `ExpenseItem.Amount`，姓名来自申请人的 `PersonalName`；由于资料硬门禁已要求个人姓名完整，正式提交记录正常情况下均可生成姓名文件名，但服务端仍需提供安全回退值。

## 风险

- Data Protection 密钥必须在部署时持久化，否则应用重建后无法解密银行卡；使用现有文件系统密钥环并在部署文档标明备份要求。
- XLSX 生成必须用测试解压并检查工作簿关系、工作表 XML 和敏感字段。
- ZIP 可能包含多份最大 10MB 的凭证，不应把所有附件先复制为独立 `byte[]`；实现采用逐项读取私有存储并写入压缩流，且在缺失实体文件时整体失败。
- ZIP 条目名需要移除路径分隔符、控制字符和 Windows 非法字符，并处理同名序号，防止目录穿越和条目覆盖。
- 当前 `Program.cs` 较大；新增业务服务以独立文件承载，路由仍按现有风格接入，避免无关重构。
- 不启用 subagent；由主 Agent 完成只读分析和实现，避免多编辑者冲突。

## 2026-08-17 周报导出扩展分析

- `WeeklyReport` 已包含导出所需作者、项目、周一日期、三项正文、最后编辑人和时间字段，不需要数据库迁移。
- 普通用户与管理员已有独立列表查询：`GET /api/weekly-reports` 仅查询本人，`GET /api/admin/weekly-reports` 可按用户查询全部；导出应复用相同权限和筛选边界，不接受分页参数。
- 现有 `XlsxWorkbookWriter` 可生成单工作表 XLSX，前端 `download()` 已处理鉴权、错误和响应文件名，因此不新增第三方包。
- 管理员无法新建周报的根因是前端按钮被 `v-if="!isAdministrator"` 隐藏；后端 `POST /api/weekly-reports` 已允许所有认证用户并将当前用户写入 `AuthorId`。
- 管理员筛选需要保留停用项目历史，但新建只能选择启用项目；前端编辑器应使用独立的可选项目集合，编辑既有停用项目周报时仍保留原项目。
- Element Plus `disabled-date` 可在日期和日期区间组件中屏蔽非周一日期；现有前端保存校验和后端 `ValidateWeeklyReport` 继续作为绕过日历时的校验兜底。
- 本扩展规模有限，不启用 subagent，由主 Agent 完成分析、实现和验证。
