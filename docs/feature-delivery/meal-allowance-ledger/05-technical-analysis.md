# 餐补看板：技术分析

## 分析方式

- 用户未单独确认启用 subagent，本功能由主 Agent 完成只读分析和实现。
- 分析范围覆盖后端实体与查询、管理员授权组、前端 API、路由、导航、详情抽屉和响应式列表样式。

## 已验证事实

- `MealAllowance` 与 `ClaimVersion` 一对一，历史版本同样保留餐补数据。
- `ReimbursementClaim.CurrentVersion` 是当前有效版本入口；从该导航查询可天然排除旧版本重复。
- 管理员接口统一挂在 `/api/admin` 路由组并要求 `Administrator` 角色。
- 项目和申请人筛选数据可复用 `/api/admin/projects`、`/api/admin/applicants`。
- `ClaimDetailDrawer` 接受 `claimId`，可直接复用完整报销详情。
- 本功能只读，不需要新增实体、字段、索引或数据库迁移。

## 关键技术判断

- 台账查询以 `ReimbursementClaims` 为根，限定 `CurrentVersion.MealAllowance != null`。
- 行程筛选采用闭区间相交：`ReturnDate >= tripFrom` 且 `DepartureDate <= tripTo`。
- 指定日期边界时，不完整行程日期不命中该过滤条件。
- 汇总在分页前由服务端计算；`TotalAmount == null` 计入待核定笔数，金额按 0 处理。
- 项目名称和编码使用当前版本快照，避免项目后续改名改变历史报销语义。

## 风险与验证边界

- LINQ 规则已有单元测试覆盖当前版本、组合筛选、区间相交和不完整日期。
- 当前环境 Docker Desktop 未运行，无法启动依赖数据库的完整本地栈，因此真实 API 和浏览器数据验收仍待运行环境验证。
