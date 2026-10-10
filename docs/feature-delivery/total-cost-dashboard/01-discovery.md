# 总费用看板：现状调研

- 前端 Vue 3、TypeScript、Element Plus；路由和导航分别位于 `frontend/src/router.ts`、`frontend/src/components/AppShell.vue`。
- 后端 ASP.NET Core 8、EF Core、PostgreSQL；`Program.cs` 中 `/api/admin` 统一要求 Administrator。
- `PayrollEntry` 有 UserId、NetPay、PayoutStatus，期间有 Draft/ReadyForPayout/Completed/Cancelled，无项目关系。
- `ReimbursementClaim` 关联申请人及当前 `ClaimVersion`；当前版本有 ProjectId 和 TotalAmount。TotalAmount 仅汇总 ExpenseItems，不含餐补。
- 餐补属于报销版本，独立保存审批状态、发放状态和 TotalAmount。只取当前版本，避免历史版本重复。
- 现有报销看板包含待审批与驳回记录，不能直接把它的汇总当作本看板费用总额。
- 归档不改变已发放金额；本看板不限制归档状态。
- 来源：`Domain/Entities.cs`、`Services/PayrollService.cs`、`Services/ClaimWorkflowService.cs`、`Services/ExpenseItemDashboardQuery.cs`、`views/AdminReimbursementDashboardView.vue`。
- 验证命令见 README：dotnet build/test、frontend 下 npm.cmd run build。
