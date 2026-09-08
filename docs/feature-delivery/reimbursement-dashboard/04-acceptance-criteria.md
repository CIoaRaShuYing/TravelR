# 报销与餐补看板：验收标准

## AC-001：餐补看板命名

Requirement: REQ-001

Acceptance criteria: 管理员导航、浏览器路由标题和页面标题均显示“餐补看板”；现有 `/admin/meal-allowances` 可继续直接访问，功能与数据不因改名变化。

Verification method: 前端代码检查、生产构建、管理员浏览器检查。

Test data: Administrator 登录态。

Risk: 误改 URL 会破坏现有书签。

Status: Static verified; browser verification pending

## AC-002：当前版本费用明细

Requirement: REQ-002

Acceptance criteria: 每个当前版本费用明细最多出现一行；历史版本明细、餐补、草稿和已作废报销的明细不出现；待审批、已批准和已驳回状态可见；行字段完整，空日期或金额有明确提示。

Verification method: 查询测试、API 数据核对、浏览器列表检查。

Test data: 含旧版本的报销、普通单据、差旅报销、草稿、待审批、已批准、已驳回、已作废和含餐补的报销。

Risk: 从全部 `ExpenseItems` 查询但不校验 `CurrentVersionId` 会产生重复。

Status: Query tests and build verified; live API verification pending

## AC-003：费用筛选与分页

Requirement: REQ-003

Acceptance criteria: 六个业务费用类别、费用日期、项目和人员可分别及组合筛选；日期边界当天命中；指定日期时空日期不命中；清空恢复全部；分页切换保留条件。

Verification method: 查询边界测试、浏览器筛选与分页验证。

Test data: 六类已提交费用、跨月日期、同项目多人和同人员多项目。

Risk: 错用报销创建时间会导致筛选结果不符合费用业务日期。

Status: Query tests and frontend build verified; browser verification pending

## AC-004：两个看板划分方式

Requirement: REQ-004

Acceptance criteria: 餐补看板可切换按项目/按人员；报销看板可切换按项目/按人员/按类别；分组笔数和金额基于排除草稿、已作废后的完整筛选集；点击卡片设置筛选，再次点击取消；切换维度和分页不会产生客户端当前页统计误差。

Verification method: API 分组测试、浏览器交互验证、人工金额核对。

Test data: 至少两个项目、两名申请人、六个费用类别、每组多条费用和餐补，以及应排除的草稿和已作废数据。

Risk: 复用报销单分组接口会把“报销单数”误当“费用明细数”。

Status: Backend and frontend build verified; live grouping verification pending

## AC-005：汇总口径

Requirement: REQ-005

Acceptance criteria: 报销看板费用明细数、已填写金额和待填写数与排除草稿、已作废后的筛选结果一致；餐补原有汇总不回归；空金额按 0；分页不改变汇总。

Verification method: API 聚合测试、人工算例核对。

Test data: 金额 100、200 和一条空金额，预期 3 条、300 元、1 条待填写。

Risk: 使用报销版本总额会在逐明细口径下重复累计。

Status: Build verified; live aggregate data verification pending

## AC-006：权限、详情与响应式

Requirement: REQ-006

Acceptance criteria: 两个看板仅管理员可访问；报销看板可打开正确关联报销详情；桌面表格和移动卡片可读；空状态、刷新和 API 错误有明确反馈。

Verification method: 权限代码检查、前端构建、管理员/申请人及桌面/移动浏览器 smoke test。

Test data: Administrator、Applicant、有结果和无结果筛选。

Risk: 只隐藏导航不能构成后端权限控制。

Status: Permission wiring and frontend build verified; browser verification pending
