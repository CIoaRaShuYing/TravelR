# 餐补看板：任务清单

## MAL-001

Task ID: MAL-001
Milestone: 后端只读查询
Linked requirement: REQ-002, REQ-003, REQ-005
Linked acceptance criteria: AC-002, AC-003, AC-004
Goal: 提供只返回当前版本餐补的分页、筛选和汇总接口。
Files likely to change: `Program.cs`、`MealAllowanceLedgerQuery.cs`、查询测试。
Implementation steps: 增加查询规则、DTO、管理员接口和边界测试。
Verification method: 后端测试、编译。
Done condition: 查询当前版本、组合筛选、汇总和分页均落地。
Status: Done
Notes: 后端测试 43/43 通过；真实数据库 API 尚待运行环境验证。

## MAL-002

Task ID: MAL-002
Milestone: 管理员页面接入
Linked requirement: REQ-001, REQ-004
Linked acceptance criteria: AC-001, AC-005
Goal: 增加独立路由、导航和响应式只读页面。
Files likely to change: `api.ts`、`router.ts`、`AppShell.vue`、`AdminMealAllowancesView.vue`、`style.css`。
Implementation steps: 补齐 API 合同、页面筛选/汇总/列表/详情、管理员路由和导航。
Verification method: Vue 类型检查和生产构建。
Done condition: 前端构建通过，桌面与移动展示代码完整。
Status: Done
Notes: 生产构建通过；真实登录态浏览器验收待本地栈可用后执行。

## MAL-003

Task ID: MAL-003
Milestone: 验证与交接
Linked requirement: REQ-001 至 REQ-005
Linked acceptance criteria: AC-001 至 AC-005
Goal: 完成静态验证并明确运行时验收边界。
Files likely to change: `04-acceptance-criteria.md`、`08-implementation-log.md`。
Implementation steps: 运行测试、构建和 diff 检查，记录环境限制。
Verification method: 验证命令输出与 Git 状态检查。
Done condition: 静态验证全部通过，未验证项明确。
Status: Done
Notes: Docker Desktop 未运行，因此未伪造真实 API/浏览器验收结论。
