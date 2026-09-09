# 报销归档批次：技术分析

## 分析方式

用户未要求启用 subagent；本轮由主 Agent 完成只读分析和实施，未创建 subagent。

## 方案判断

- 使用 `ReimbursementClaim.ArchiveBatchId` 表达一对多关系，比给餐补重复存储批次或创建通用标签系统更简单且可强制父子一致。
- 使用独立归档维度，避免修改 `ClaimStatus` 状态机以及审批、发放条件。
- 批次成员以外键固化；`SubmittedFrom/SubmittedTo` 是创建依据和审计元数据，不是后续查询真相。
- 创建逻辑放入 `ClaimArchiveService`，避免继续扩大 `Program.cs` 中的内联事务代码。
- 月度导出与批次导出共享工作簿/附件打包逻辑，但查询入口分别保留“项目+日期”和“精确批次成员”。
- 三个列表查询必须统一使用父报销的 `ArchiveBatchId`；费用明细和餐补不增加归档字段。

## 并发与失败

- `ReimbursementClaim.ConcurrencyToken` 已是并发令牌；归档时更新令牌，使并发状态变化导致 `DbUpdateConcurrencyException`。
- 创建前检查名称、日期、空范围、资格和重复归档，保存时再次由数据库唯一约束和并发令牌兜底。
- 所有成员和批次一次 `SaveChangesAsync`，失败回滚。

## 验证路径

- `dotnet test TravelReimbursement.slnx -c Release --no-restore -p:BaseOutputPath=...`
- `npx.cmd vue-tsc -p tsconfig.app.json --noEmit --incremental false`
- `npm.cmd run build`
- 隔离 PostgreSQL 应用迁移并完成管理员 HTTP 冒烟。
