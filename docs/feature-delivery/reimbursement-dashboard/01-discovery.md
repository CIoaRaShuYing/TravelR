# 报销与餐补看板：项目探索

## 项目结构

- 后端：ASP.NET Core Minimal API、EF Core、Npgsql，主要接口集中在 `backend/TravelReimbursement.Api/Program.cs`。
- 前端：Vue 3、TypeScript、Element Plus，路由、API、页面与全局样式分别位于 `router.ts`、`api.ts`、`views/` 和 `style.css`。
- 权限：`/api/admin` 路由组要求 `Administrator`；前端路由通过 `meta.administrator` 守卫。

## 数据模型事实

- `ReimbursementClaim.CurrentVersionId` 指向当前版本。
- `ClaimVersion.ExpenseItems` 是费用明细集合。
- `ExpenseItem` 字段包括 `Category`、`Amount`、`Currency`、`ExpenseDate`、`Merchant`、`Note`。
- `ExpenseCategory` 包括去程交通、回程交通、住宿、办公用品、餐费、其他和内部兜底值 `Unspecified`。
- `ClaimVersion.MealAllowance` 是独立餐补对象，不属于费用明细集合。
- 当前改动不需要实体或数据库迁移。

## 可复用能力

- `GET /api/admin/projects`：项目筛选选项。
- `GET /api/admin/applicants`：人员远程筛选选项。
- `ClaimDetailDrawer.vue`：通过 `claimId` 查看关联报销及版本信息。
- `AdminClaimsView.vue`：已有按项目/人员切换、分组卡片点击筛选的交互。
- `AdminMealAllowancesView.vue`：已有看板筛选、汇总、桌面表格和移动卡片骨架。

## 关键风险

- 直接查询全部 `ExpenseItems` 会混入旧版本明细。
- 把餐补并入 `ExpenseItems` 会与独立餐补看板重复统计。
- 客户端按当前页分组会导致计数和金额错误，必须服务端聚合。
- 同一项目的版本快照名称可能不同；分组应按稳定 `ProjectId` 和当前项目名称，列表仍可展示版本快照。
- 草稿明细可能缺少类别、金额或费用日期；用户已明确草稿不纳入报销看板统计，因此查询入口必须先排除草稿和已作废报销。

## 界面方向草案

- 主题：管理员费用台账；单一任务是定位一条费用或餐补并理解其归属。
- 色彩沿用现有 Spruce `#183a34`、Teal `#1b6b5a`、Amber `#d99614`、Canvas `#eef2ef` 和 Surface `#ffffff`。
- 字体沿用中文正文 `Noto Sans SC`/`Microsoft YaHei`，金额与编号使用现有等宽辅助字体。
- 标志性交互是“可切换的分组台账带”：按项目或人员展示完整筛选集的笔数和金额，点击即收窄筛选。
- 保持桌面表格和移动卡片双布局，不引入与现有后台不一致的装饰或动画。

## 需要确认的业务口径

- 是否保留餐补 URL，仅改可见名称。
- 草稿和已作废是否同时从列表、汇总和分组排除。
- 报销看板时间是否按费用日期。
- 餐补看板按项目/人员，报销看板按项目/人员/类别。
