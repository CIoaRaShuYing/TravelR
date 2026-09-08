# 报销与餐补看板：技术方案

## 后端方案

### 报销看板

- `GET /api/admin/expense-items`
- 参数：`category`、`projectId`、`applicantId`、`expenseFrom`、`expenseTo`、`page`、`pageSize`。
- 返回：费用明细分页数据，以及 `expenseItemCount`、`totalAmount`、`pendingAmountCount`。
- `GET /api/admin/expense-items/group-summary`
- 参数：相同过滤条件和 `groupBy=project|applicant|category`。
- 返回：分组键、显示名称、费用明细数、金额。

### 餐补看板

- 保留 `GET /api/admin/meal-allowances`。
- 新增 `GET /api/admin/meal-allowances/group-summary`，支持 `groupBy=project|applicant`，复用列表过滤条件。

### 权限和数据

- 全部接口继承 `/api/admin` 的 `Administrator` 授权。
- 无写操作、事务、审计写入、数据库字段或迁移。

## 前端方案

- 保留 `/admin/meal-allowances` 路径，将导航、路由标题、页面标题改为“餐补看板”。
- 新增 `/admin/reimbursement-dashboard` 和管理员导航“报销看板”。
- 两个看板并发加载明细与当前划分方式的服务端分组。
- 报销看板按类别分组时，卡片点击设置/取消类别筛选；其他维度延续现有项目/人员行为。
- 复用 `ClaimDetailDrawer`，不增加写操作。

## API 合同

- 费用行包含 `id`、关联报销/版本、项目快照、申请人、类别、金额、币种、费用日期、商户、备注、报销状态、更新时间。
- 分组统一返回 `key`、`label`、`itemCount`、`totalAmount`；类别组的 `key` 和 `label` 由枚举字符串序列化，前端映射中文。

## 错误与边界

- 开始日期晚于结束日期返回 Validation Problem。
- 非法枚举由 Minimal API 参数绑定返回 400。
- 非法 `groupBy` 返回 Validation Problem，不静默降级。
- 空金额按 0 聚合并单独计数。

## 回滚

- 移除新增费用查询类、测试、API、页面、路由、导航及分组扩展即可。
- 餐补路径和数据库均未变化，无数据回滚。

## 验证

- `dotnet test backend/TravelReimbursement.Api.Tests/TravelReimbursement.Api.Tests.csproj --no-restore`
- `npm.cmd run build`
- `git diff --check`
- 完整本地栈可用时执行管理员 API 和桌面/移动浏览器 smoke test。
