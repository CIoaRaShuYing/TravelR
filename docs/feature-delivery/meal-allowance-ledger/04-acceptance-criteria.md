# 餐补详情台账：验收标准

## AC-001：路由与权限

Requirement: REQ-001
Acceptance criteria: 管理员导航出现“餐补详情”，点击进入 `/admin/meal-allowances`；普通申请人无法从导航看到且直接访问会被前端守卫拦截；后端 API 同样要求 Administrator。
Verification method: 路由与权限代码检查、前端构建、管理员/申请人浏览器验证。
Test data: 一个 Administrator、一个 Applicant。
Risk: 只做前端隐藏会造成越权。
Status: Static verified; browser verification pending

## AC-002：当前版本全量餐补

Requirement: REQ-002
Acceptance criteria: 每个含餐补的报销最多出现一条当前版本记录；普通单据不出现；全部餐补状态均可见；列表字段完整，待核定金额明确显示。
Verification method: API 查询测试、浏览器列表检查。
Test data: 草稿、待审批、已批准、已驳回、已作废和包含旧版本的差旅报销，以及普通单据。
Risk: 从 `MealAllowances` 直接查询会混入旧版本。
Status: Query tests and build verified; live API verification pending

## AC-003：组合筛选

Requirement: REQ-003
Acceptance criteria: 项目、申请人、行程日期可分别和组合筛选；日期采用区间相交语义；清空筛选恢复全部；分页切换不丢失筛选。
Verification method: API 边界测试、浏览器筛选与分页验证。
Test data: 跨月行程、同项目多人、同人员多项目、筛选边界当天命中的餐补。
Risk: 日期按创建时间实现会与餐补业务日期不一致。
Status: Query tests and frontend build verified; browser verification pending

## AC-004：汇总与金额口径

Requirement: REQ-005
Acceptance criteria: 汇总笔数和待核定笔数覆盖完整筛选结果；已核定总额只累加非空 `TotalAmount`，空值按 0；分页不改变汇总值。
Verification method: API 聚合测试、人工算例核对。
Test data: 两笔已核定 100、200 元和一笔待核定，预期 3 笔、300 元、1 笔待核定。
Risk: 客户端仅对当前页求和会产生错误汇总。
Status: Build verified; live aggregate data verification pending

## AC-005：详情复用与响应式

Requirement: REQ-004
Acceptance criteria: 点击任一餐补可打开关联报销详情；桌面端表格和移动端卡片均可读；页面刷新、空状态和 API 错误有明确反馈。
Verification method: 前端构建、桌面与移动浏览器 smoke test。
Test data: 有餐补和无匹配结果两组筛选。
Risk: 新页面若复制详情逻辑会与报销管理产生行为漂移。
Status: Frontend build verified; browser verification pending
