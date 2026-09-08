# 餐补看板：项目探索

## 项目结构

- 前端采用 Vue 3、Vue Router、Element Plus；路由集中在 `frontend/src/router.ts`，导航集中在 `frontend/src/components/AppShell.vue`。
- 后端采用 ASP.NET Core Minimal API、EF Core 和 PostgreSQL；管理员 API 位于 `Program.cs` 的 `/api/admin` 路由组。
- 当前“报销管理”页面为 `frontend/src/views/AdminClaimsView.vue`，已经实现项目、申请人、状态、日期筛选、分页、分组汇总和详情抽屉。

## 餐补领域事实

- `MealAllowance` 与 `ClaimVersion` 一对一，保存 `DepartureDate`、`ReturnDate`、`Days`、`DailyAmount`、`TotalAmount`、审核状态、发放状态及并发标识。
- 每次报销编辑会创建新 `ClaimVersion`，差旅类型的新版本同步创建新的餐补。
- 报销主记录通过 `CurrentVersionId` 指向当前版本；因此从 `ReimbursementClaims.CurrentVersion.MealAllowance` 查询可以天然排除旧版本重复项。
- 普通单据没有餐补，查询必须只返回 `CurrentVersion.MealAllowance != null` 的记录。

## 当前 API 能力

- `/api/admin/claims` 返回当前版本餐补摘要，但分页单位是报销且包含无餐补的普通单据，不能作为“所有餐补”页面的可靠数据源。
- 现有餐补操作端点位于 `/api/admin/claims/{id}/meal-allowance/*`，审批和发放均以当前报销版本和并发标识保护。
- `ProjectClaimList` 已投影申请人、项目、报销号、当前版本、餐补日期以外的餐补摘要；独立餐补 API 需要补齐行程起止、每日金额和更新时间。

## 当前前端扩展点

- `router.ts` 的管理员路由通过 `meta.administrator` 守卫。
- `AppShell.vue` 的管理员导航按 `isAdministrator && !profileIncomplete` 显示。
- `ClaimDetailDrawer.vue` 可复用为“查看完整报销详情”，无需重复构建审计详情。
- `AdminClaimsView.vue` 的项目和申请人远程筛选、桌面表格和移动卡片布局可作为交互范式，但新页面应保持独立数据和路由。

## 数据库与权限

- 所需字段均已存在，不需要迁移。
- 新 API 应继续放在管理员路由组，后端权限不能只依赖前端路由守卫。
- 查询只读，不改变餐补状态或审计记录。

## 风险与不确定项

- “时间”可以解释为报销创建时间、提交时间、餐补创建时间或行程日期；推荐使用行程日期，并在控件文案中明确。
- 若返回全部历史版本，会使同一报销产生多条餐补并与“当前版本金额”口径冲突；推荐只返回当前版本。
- 若复制审批/发放动作，需要同步维护两个工作台和并发刷新逻辑；本轮推荐只读。
- 当前没有可用 Docker/PostgreSQL 实例，实施后可完成编译和单元测试，但真实 SQL/浏览器验收需要目标环境。
