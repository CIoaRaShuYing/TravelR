# 报销归档批次：现状调研

## 架构摘要

- 后端：ASP.NET Core 8 Minimal API、EF Core、Npgsql，入口集中在 `backend/TravelReimbursement.Api/Program.cs`。
- 前端：Vue 3、TypeScript、Element Plus，管理页面通过 `frontend/src/api.ts` 调用 API。
- 测试：xUnit；查询规则主要使用可组合 `IQueryable` 辅助类进行单元测试。

## 现有领域边界

- `ReimbursementClaim` 保存审批状态、发放状态和当前版本指针。
- `MealAllowance` 与 `ClaimVersion` 一对一；当前有效餐补通过 `ReimbursementClaim.CurrentVersion.MealAllowance` 访问。
- 报销和餐补分别审批、分别发放；已批准报销不可继续编辑。
- 附件通过当前版本费用明细关联私有存储，月度 ZIP 在文件缺失时整体失败。

## 现有查询面

- `/api/admin/claims`：报销管理，支持项目、人员、审批/发放状态、工作队列和创建时间。
- `/api/admin/expense-items`：报销看板，以当前版本费用明细为根，支持项目、人员、类别和费用日期。
- `/api/admin/meal-allowances`：餐补看板，以父报销当前版本餐补为根，支持项目、人员和行程日期。
- `/api/admin/claims/export.zip`：按项目和父报销 `SubmittedAt` 日期生成 ZIP。

## 扩展点与风险

- 在父报销保存可空批次外键，可用最小模型表达一批多报销且餐补继承父级。
- 不能把 `Archived` 加入 `ClaimStatus` 后覆盖 `Approved/Rejected/Cancelled`，否则会破坏审批审计与现有查询。
- 三个查询面当前日期口径不同；归档筛选必须增加独立 `archiveBatchId`/`archiveState`，不能复用日期参数。
- 批次创建必须在事务中重新验证资格和未归档状态，防止预览后状态变化造成部分归档。
- 当前工作区起点为提交 `083586c`，已包含“已批准餐补进入月度报销汇总”的修复。
