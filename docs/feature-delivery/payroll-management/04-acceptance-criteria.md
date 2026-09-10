# 工资发放管理：验收标准

## AC-001 月份与人员快照

Requirement: REQ-001、REQ-002
Acceptance criteria: 同一月份不能创建两个有效工资期间；新月份默认包含当时启用的 `Applicant`；草稿可增删正式用户且同一用户不可重复；锁定后用户停用或改名不改变该月人员和姓名快照。
Verification method: 领域规则单测、EF Core 唯一约束、隔离 PostgreSQL 迁移与 HTTP 测试。
Test data: 启用申请人、停用申请人、管理员兼申请人、同月重复创建、锁定后用户改名。
Risk: 把实时用户目录误当作历史工资表。
Status: implemented / verified by model checks and isolated PostgreSQL HTTP acceptance

## AC-002 工资填写与公式

Requirement: REQ-003、REQ-004
Acceptance criteria: 管理员可在月表中保存多人明细；服务端按确认的公式计算应发、总扣款和实发；负数、超过两位小数、实发小于零及过期并发令牌均返回明确错误且不覆盖已保存数据。
Verification method: 公式/校验单测、并发冲突测试、HTTP 请求响应断言、桌面和移动端表单验证。
Test data: 正常工资、全零工资、小数边界、扣款大于应发、两名管理员并发修改。
Risk: 前后端计算口径漂移。
Status: implemented / verified by calculator tests, batch HTTP validation and stale-token conflict

## AC-003 锁定与逐人发放

Requirement: REQ-005、REQ-006
Acceptance criteria: 资料缺失或金额非法时整月锁定失败并指出人员；锁定后工资字段不可编辑；管理员可逐人确认发放并生成唯一凭证；重复确认返回冲突；部分发放可继续完成剩余人员；全员发放后期间自动变为 `Completed`；任何人已发放后不能退回草稿。
Verification method: 状态机单测、事务与唯一约束测试、隔离 PostgreSQL HTTP 测试。
Test data: 完整资料、缺姓名、缺银行卡、重复确认、部分发放、最后一人发放、并发发放。
Risk: 重复请求造成重复记账，或部分成功被错误显示为整月完成。
Status: implemented / verified by isolated PostgreSQL HTTP lifecycle and unique payout count

## AC-004 按月查询与权限

Requirement: REQ-007、REQ-008、REQ-010
Acceptance criteria: 管理员可切换月份并得到与明细一致的汇总；普通用户只能获得本人已发放明细，访问管理接口为 403，访问他人数据不存在可利用入口；未登录访问为 401；响应和日志不含完整银行卡号。
Verification method: 权限矩阵测试、API 响应敏感字段检查、桌面与 390×844 移动端浏览器验收。
Test data: 两个月份、两名普通用户、一名管理员、Draft/Pending/Paid 混合明细。
Risk: 工资高敏感数据通过列表、导出或审计上下文泄露。
Status: implemented / verified by 401/403 HTTP checks, payload scan and desktop/390×844 browser acceptance

## AC-005 工资表导出

Requirement: REQ-009
Acceptance criteria: 管理员导出指定月份的完整工资表，不受当前分页和筛选页码影响；行数、各项金额、汇总和发放状态与数据库当月快照一致；普通用户不能调用；导出操作写审计。
Verification method: Excel XML/工作簿测试、HTTP Content-Type 与文件名验证、权限测试。
Test data: 空月份、多人月份、部分发放、全部发放、姓名重复。
Risk: 导出实时读取用户资料导致与已发放凭证不一致。
Status: implemented / verified by workbook tests and authenticated HTTP download inspection

## AC-006 现有功能回归

Requirement: REQ-011
Acceptance criteria: 新迁移只新增工资领域对象；现有注册、用户、报销、餐补、归档、周报和会议接口行为不变；后端全量测试、Release 构建、前端类型检查与生产构建通过。
Verification method: `dotnet test`、`dotnet build`、`npm.cmd run build`、`git diff --check`，并执行关键既有 HTTP smoke test。
Test data: 现有自动化测试基线及独立测试数据库。
Risk: Minimal API 与前端集中式文件改动导致非工资功能回归。
Status: implemented / verified by 77/77 backend tests, Release compilation, frontend production build and `git diff --check`
