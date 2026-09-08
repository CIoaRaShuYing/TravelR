# 餐补看板：实施记录

> 2026-09-08 后续增强：页面名称统一为“餐补看板”，并增加按项目/人员划分；详见 `docs/feature-delivery/reimbursement-dashboard/`。

## 2026-09-08

- 当前里程碑：MAL-001 至 MAL-003。
- 完成任务：新增当前版本餐补查询、组合筛选、服务端汇总、分页、独立管理员页面、导航、路由和详情复用。
- 变更文件：
  - `backend/TravelReimbursement.Api/Program.cs`
  - `backend/TravelReimbursement.Api/Services/MealAllowanceLedgerQuery.cs`
  - `backend/TravelReimbursement.Api.Tests/MealAllowanceLedgerQueryTests.cs`
  - `frontend/src/api.ts`
  - `frontend/src/router.ts`
  - `frontend/src/components/AppShell.vue`
  - `frontend/src/views/AdminMealAllowancesView.vue`
  - `frontend/src/style.css`
- 验证命令与结果：
  - `dotnet test backend/TravelReimbursement.Api.Tests/TravelReimbursement.Api.Tests.csproj --no-restore`：通过，43/43。
  - `npm.cmd run build`：通过，`vue-tsc` 与 Vite 生产构建完成；仅保留既有大包体警告。
  - `git diff --check`：通过。
- 环境情况：首次沙箱内测试触发 NuGet TLS 和 `obj` ACL 错误，前端触发 `node_modules/.tmp` ACL 错误；改用已缓存依赖并在允许的本机执行环境验证后通过。
- 未完成的运行时验收：Docker Desktop 未运行，未执行真实数据库 API、管理员登录和桌面/移动浏览器 smoke test。
- 数据库变更：无。
- 下一步：在完整本地栈启动后按 AC-001 至 AC-005 完成真实数据和浏览器验收。
