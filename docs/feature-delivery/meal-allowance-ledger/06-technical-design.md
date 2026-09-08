# 餐补看板：技术方案

## 总体方案

增加一个管理员只读纵切片：独立查询接口、前端类型与调用、新路由、新导航入口、新页面；复用既有项目/申请人目录和报销详情抽屉。

## 后端改动

- 接口：`GET /api/admin/meal-allowances`
- 参数：`projectId`、`applicantId`、`tripFrom`、`tripTo`、`page`、`pageSize`
- 查询：`ReimbursementClaim -> CurrentVersion -> MealAllowance`
- 返回行：餐补、关联报销、当前版本、项目快照、申请人、行程、金额、状态、更新时间。
- 汇总：`mealAllowanceCount`、`determinedAmount`、`pendingAmountCount`。
- 校验：开始日期晚于结束日期时返回 Validation Problem。
- 权限：继承 `/api/admin` 的 `Administrator` 授权策略。

## 前端改动

- 路由：`/admin/meal-allowances`
- 导航：管理员侧“餐补看板”。
- 筛选：项目、远程申请人、行程日期范围。
- 展示：桌面表格、移动卡片、全量汇总、分页、刷新、空状态与错误提示。
- 详情：传递关联 `claimId` 给现有 `ClaimDetailDrawer`。

## 数据库、事务和审计

- 无数据库结构变更，无迁移。
- 全部操作只读，不开启事务、不写审计日志、不改变餐补状态机。

## 回滚方式

移除接口、查询辅助类、前端 API 类型与调用、页面、路由、导航及对应样式和测试即可；无数据回滚。

## 验证命令

- `dotnet test backend/TravelReimbursement.Api.Tests/TravelReimbursement.Api.Tests.csproj --no-restore`
- `npm.cmd run build`（目录：`frontend`）
- `git diff --check`

## 未采用方案

- 不直接查询 `MealAllowances`：会混入已被替代版本的数据。
- 不复用 `/api/admin/claims`：该接口以报销为分页单位并包含无餐补记录，筛选时间也是创建时间。
- 不在新页面复制审批和发放动作：避免写入口、并发控制和审计行为分叉。
