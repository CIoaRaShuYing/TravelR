# 新增业务功能：技术方案

## 数据模型

- `AppUser`：新增 `PersonalName`、`BankCardProtected`。
- `MealAllowance`：一对一关联 `ClaimVersion`，保存天数、日标准、总额、审核状态、发放状态、审核信息和并发令牌。
- `MealAllowanceApprovalRecord`、`MealAllowancePayoutRecord`：保存第二次审核与餐补发放历史。
- `WeeklyReport`：关联作者和项目，保存 `WeekStart`、本周完成、下周计划、问题、最后编辑人和并发令牌；唯一索引 `(AuthorId, ProjectId, WeekStart)`。

## 状态机

```text
差旅草稿 -> 餐补 Draft
差旅提交 -> 餐补 PendingTravelReview
差旅驳回 -> 餐补 Rejected（联动）
差旅批准 -> 餐补 PendingReview
餐补批准 -> 餐补 Approved + Payout Pending
餐补驳回 -> 餐补 Rejected
餐补确认发放 -> Payout Paid
```

## API

- `GET/PUT /api/me/profile`：读取、保存个人姓名和银行卡。
- 认证后的资料门禁：资料不完整时除 `/api/me`、`/api/me/profile`、`/api/me/password` 外返回 `PROFILE_INCOMPLETE`。
- `POST /api/admin/claims/{id}/meal-allowance/approve|reject`：第二次审核。
- `POST /api/admin/claims/{id}/meal-allowance/payout/confirm`：餐补发放。
- `GET /api/admin/claims?workQueue=approval|payout`：在服务端合并差旅与餐补的待审批/待发放条件，保证分页结果不漏单。
- `GET /api/admin/users/{id}/payment-profile`：发放前读取姓名与完整银行卡，并记录审计。
- `GET/POST/PUT /api/weekly-reports`：本人周报查询、创建、编辑。
- `GET /api/admin/weekly-reports`：管理员查询；管理员编辑复用 `PUT /api/weekly-reports/{id}`，端点内按作者或管理员角色授权。
- `GET /api/admin/claims/export.zip`：项目必选，`submittedFrom/submittedTo` 可选，返回 ZIP；根目录包含 XLSX 和 `报销凭证/`，凭证从当前有效版本的费用明细附件逐项读取。
- `GET /api/admin/claims/export.xlsx`：保留旧路径作为 ZIP 别名，返回内容、`Content-Type` 和文件名均与 ZIP 接口一致，确保旧前端或缓存页面也不会继续下载单独 Excel。

## 前端

- 新增“个人资料”页和硬门禁路由守卫；登录响应携带 `profileIncomplete`。
- 用户中心直接显示完整卡号并提供复制图标按钮。
- 差旅编辑器显示自动餐补天数，不提供申请开关。
- 报销管理增加餐补待审核/待发放状态与两次操作。
- 新增统一周报页；普通用户维护本人，管理员可切换查看和编辑所有用户。
- 报销管理增加项目、提交日期区间和 ZIP 导出，默认上月 10 日至本月 10 日；下载提示明确压缩包包含 Excel 与报销凭证。
- Element Plus 全局使用 `zh-cn` locale，日期选择器的月份、星期和操作按钮统一显示中文。

## 实施补充

- 月度导出先按 `Asia/Shanghai`/中国标准时间构造自然日边界，再转换为 UTC `DateTimeOffset` 传给 Npgsql，兼容 PostgreSQL `timestamp with time zone`。
- `MonthlyClaimExportService` 继续负责查询和 Excel 生成，并增加 ZIP 组装：条目命名使用 `PersonalName_ExpenseItem.Amount`、原扩展名和同名序号；空数据显式创建 `报销凭证/` 目录条目。
- “报销汇总”在明细行后追加固定 12 列总计行，报销金额列写入当前查询结果的 `CurrentVersion.TotalAmount` 合计，餐补不重复计入。
- ZIP 构建写入临时文件后再作为响应流返回，只有全部凭证成功写入后才产生成功响应；响应结束后清理临时文件，避免大附件集合长期占用托管堆内存。
- 完整银行卡只出现在管理员用户目录和发放资料专用响应；列表、日志、异常和 XLSX 均不包含完整卡号。

## 迁移与回滚

- 新增字段均允许空值，兼容现有用户；登录后由业务门禁要求补全。
- 新增业务表和索引，不删除旧表、不迁移旧报销金额，属于非破坏性迁移。
- 回滚通过迁移 `Down` 删除新增表和用户扩展列；回滚前应确认新增业务数据已备份。

## 验证

- `dotnet build TravelReimbursement.slnx --no-restore`
- `dotnet test TravelReimbursement.slnx --no-build --no-restore`
- `Set-Location frontend; npm.cmd run build`
- PostgreSQL API smoke test、XLSX 解压校验、桌面与移动端浏览器验收。
