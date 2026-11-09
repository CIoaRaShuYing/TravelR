# 调研事实

- 技术栈：Vue 3 + Element Plus + TypeScript；ASP.NET Core + EF Core + PostgreSQL。
- 项目内 Everything 检索未发现 AGENTS.md，遵循本次会话提供的规则。工作区初始干净。
- `backend/TravelReimbursement.Api/Domain/Entities.cs`：PayrollEntry 无项目字段；报销通过 CurrentVersion.ProjectId 关联项目；工资有 PayrollMonth，报销有 SubmittedAt，餐补跟随报销版本。
- `backend/TravelReimbursement.Api/Services/PayrollService.cs`：工资金额只能在草稿修改；存在并发令牌、事务与审计。已发工资不能退回草稿。
- `backend/TravelReimbursement.Api/Services/CostDashboardService.cs`：工资仅统计 ReadyForPayout / Completed 的 NetPay；报销和餐补仅统计批准记录；项目范围过滤掉 ProjectId 为空的记录，工资目前全部为空。
- `frontend/src/views/AdminPayrollView.vue`：桌面表格、移动卡片及编辑弹窗共用工资编辑模型。
- `frontend/src/views/AdminCostDashboardView.vue`：项目范围隐藏工资分类，并自动清理互斥筛选，需随本需求更新。
- 原需求 `docs/feature-delivery/total-cost-dashboard/03-requirements.md` 明确不包含工资项目归属和时间筛选，本次需求将覆盖这两项旧边界。
- 验证入口：README.md 的 dotnet build / test 与 frontend npm.cmd run build；现有 CostDashboardQueryTests 包含汇总与 Npgsql 查询翻译验证。
- 风险：未知项目的历史工资不能猜测归属；项目分组不能静默丢失未分配工资；时间必须说明业务口径，不能混用实际发放时间。
