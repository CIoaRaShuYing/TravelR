# 工资发放管理：技术方案

## 数据模型

### `PayrollPeriods`

- `Id`, `PayrollMonth`, `Status`
- `CreatedById`, `CreatedAt`, `UpdatedAt`
- `LockedAt`, `CompletedAt`, `CancelledAt`
- `ConcurrencyToken`
- `PayrollMonth` 必须为月首日；未取消期间按月份部分唯一。

### `PayrollEntries`

- `Id`, `PayrollPeriodId`, `UserId`
- `EmployeeDisplayNameSnapshot`, `EmployeePersonalNameSnapshot`, `BankCardLastFourSnapshot`
- 九项输入金额、`GrossPay`, `TotalDeductions`, `NetPay`
- `Note`, `PayoutStatus`, `CreatedAt`, `UpdatedAt`, `UpdatedById`, `PaidAt`, `ConcurrencyToken`
- `(PayrollPeriodId, UserId)` 唯一；金额非负，三项结果受数据库公式约束。

### `PayrollPayoutRecords`

- `Id`, `PayrollEntryId`, `PayrollMonth`, `Amount`
- `RecipientName`, `BankCardLastFour`, `ConfirmedById`, `ConfirmedAt`, `Note`
- `PayrollEntryId` 唯一；卡尾号必须为四位数字。

## 服务边界

- `PayrollService`：期间查询/创建、候选人、增删人员、批量保存、锁定、退回、取消、逐人发放、本人已发工资查询。
- `PayrollExportService`：按期间固化明细生成 Excel。
- `PayrollCalculator`：唯一金额公式与校验入口，前端计算只作预览。

## API

管理员：

- `GET/POST /api/admin/payroll-periods`
- `GET /api/admin/payroll-periods/{periodId}`
- `GET /api/admin/payroll-periods/{periodId}/candidates`
- `POST /api/admin/payroll-periods/{periodId}/entries`
- `PUT /api/admin/payroll-periods/{periodId}/entries`
- `POST /api/admin/payroll-periods/{periodId}/entries/{entryId}/remove`
- `POST /api/admin/payroll-periods/{periodId}/lock`
- `POST /api/admin/payroll-periods/{periodId}/return-to-draft`
- `POST /api/admin/payroll-periods/{periodId}/cancel`
- `POST /api/admin/payroll-periods/{periodId}/entries/{entryId}/payout/confirm`
- `GET /api/admin/payroll-periods/{periodId}/export.xlsx`

普通用户：

- `GET /api/payrolls/mine`：只返回当前用户 `Paid` 明细。

## 状态与事务

- 创建期间：同事务创建期间、默认人员快照与审计；数据库部分唯一索引兜底并发同月创建。
- 批量保存：校验期间及全部明细令牌、全部金额后统一写入；审计只记录变化字段的前后金额。
- 锁定：校验全部人员姓名、银行卡和金额，保存姓名与卡尾号快照，统一转 `Pending`。
- 退回：仅未有任何 `Paid` 明细时允许，清除锁定收款快照并转回草稿。
- 发放：校验期间/明细令牌、状态、当前银行卡尾号与锁定快照，生成唯一凭证；最后一人发放后期间变 `Completed`。
- 取消：仅草稿允许；历史保留，同月可另建有效期间。

## 前端

- 新增 `/admin/payroll` 工资管理页和 `/payroll` 我的工资页。
- 管理页以月份为工作台，包含汇总、人员管理、九项工资填写、批量保存、锁定/退回/取消、逐人发放与导出。
- 桌面为宽表；390×844 使用人员卡片和分组编辑弹窗。
- 普通用户只能获取和展示本人已发放工资；金额不进入 URL 或持久缓存。

## 安全与缓存

- 管理接口继续使用 `Administrator`，不增加角色。
- 工资 JSON 与下载响应设置 `Cache-Control: no-store, private`。
- 候选人响应只含资料是否齐全，不含银行卡或密文。
- 审计不记录完整卡号、密文、JWT 或完整请求对象。

## 迁移与回滚

- 新增单个加法迁移 `AddPayrollManagement`，不回填、不修改既有表数据。
- 代码回滚时旧 API 可忽略新增表；一旦产生工资数据，不得直接执行会删表的 `Down`，应使用升级前备份或先导出后经明确授权处理。

## 不采用的方案

- 不将工资存成 Excel/JSON 单字段：无法做并发、权限、逐人发放和数据库约束。
- 不把工资字段放进 `AppUser`：账号状态不是劳动关系，且无法表达月度历史。
- 不复用报销 `PayoutRecord`：工资与报销财务边界不同。
- 不新增第三方表格或 Office 依赖：现有栈足够。
