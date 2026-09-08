# 报销与餐补看板：实施记录

## 2026-09-08

- 当前里程碑：RDB-001 至 RDB-003 已完成静态实现与验证。
- 需求确认：已通过；报销看板排除草稿、已作废，增加按类别划分。
- 分析方式：用户未单独确认启用 subagent，由主 Agent 完成。
- 数据库变更：无。
- 已完成：
  - 新增当前版本费用明细查询，排除草稿、已作废和 `Unspecified`。
  - 新增费用明细列表、汇总、项目/人员/类别分组接口。
  - 新增餐补项目/人员分组接口。
  - 将“餐补详情”统一改名为“餐补看板”。
  - 新增“报销看板”路由、导航、筛选、汇总、分组、桌面表格、移动卡片和报销详情入口。
- 变更文件：
  - `backend/TravelReimbursement.Api/Program.cs`
  - `backend/TravelReimbursement.Api/Services/ExpenseItemDashboardQuery.cs`
  - `backend/TravelReimbursement.Api.Tests/ExpenseItemDashboardQueryTests.cs`
  - `frontend/src/api.ts`
  - `frontend/src/router.ts`
  - `frontend/src/components/AppShell.vue`
  - `frontend/src/views/AdminMealAllowancesView.vue`
  - `frontend/src/views/AdminReimbursementDashboardView.vue`
  - `frontend/src/style.css`
- 验证：
  - 后端测试通过，45/45。
  - `npm.cmd run build` 通过；仅保留既有大包体提示和 Vite 插件耗时提示。
  - `git diff --check` 通过。
- 运行时边界：Docker Desktop 未运行，无法执行真实 PostgreSQL API、管理员登录和桌面/移动浏览器 smoke test。
- 下一步：在完整本地栈可用后执行 AC-001 至 AC-006 的运行时验收。
