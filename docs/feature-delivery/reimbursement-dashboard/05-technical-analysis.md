# 报销与餐补看板：技术分析

## 分析方式

- 用户未单独确认启用 subagent，由主 Agent 完成等价只读分析和后续实现。
- 已检查实体关系、管理员路由组、现有餐补查询、报销管理分组接口、前端 API、路由、导航、详情抽屉和响应式样式。

## 已验证事实

- `ExpenseItem.ClaimVersion` 可追溯 `ReimbursementClaim.CurrentVersionId`、项目和申请人。
- `ExpenseItem` 没有独立更新时间；关联报销的 `UpdatedAt` 可表示当前版本上下文的最近变化。
- 提交校验会拒绝 `Unspecified`、空金额和空费用日期，但历史或异常数据仍需查询层明确限定正式类别。
- `MealAllowanceLedgerQuery.Apply` 已统一餐补的当前版本、项目、人员和行程时间过滤。
- 现有 `/api/admin/claims/group-summary` 按报销单统计，不能用于费用明细数或餐补数。

## 查询边界

- 报销看板从 `ExpenseItems` 查询，并要求 `ClaimVersionId == ClaimVersion.Claim.CurrentVersionId`。
- 只接受关联报销状态 `Submitted`、`Approved`、`Rejected`。
- 排除 `ExpenseCategory.Unspecified`。
- 项目、人员、类别和费用日期过滤在分页及聚合前统一应用。
- 餐补分组继续复用现有餐补查询边界，包含其当前版本全部餐补状态。

## 前端设计复核

- 保持现有深绿台账视觉系统，不引入新的色板或字体依赖。
- 两个看板共用“汇总带 + 划分方式 + 分组卡片”结构，结构本身表达统计口径。
- 报销看板用费用类别标签和金额作为视觉主轴，避免复制“报销管理”的审批操作密度。
- 桌面端优先横向可扫描表格，窄屏切换为明细卡片；交互名称使用管理员可理解的业务词。

## 风险

- EF Core 的多级导航分组需要由编译和尽可能的查询测试验证。
- 当前本地 Docker 未运行时，真实 PostgreSQL API 和浏览器数据验收可能仍受环境限制。
