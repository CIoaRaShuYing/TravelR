# 报销归档批次：技术设计

## 数据库

- 新表 `ClaimArchiveBatches`：`Id`、`Name`、`NormalizedName`、`SubmittedFrom`、`SubmittedTo`、`CreatedById`、`CreatedAt`、`UpdatedAt`、`ConcurrencyToken`。
- `ReimbursementClaims` 新增可空 `ArchiveBatchId`，外键删除行为 `Restrict`。
- `NormalizedName` 唯一；日期范围使用检查约束；为 `ArchiveBatchId` 和 `SubmittedAt` 建索引。
- 迁移为非破坏性：历史行外键为空。回滚时先备份批次成员，再删除外键、列和批次表。

## 服务

- `ClaimArchiveEligibility`：纯规则函数，判断父报销和当前餐补是否结案并返回阻塞原因。
- `ClaimArchiveService`：预览、原子创建、列表/详情和改名；`MonthlyClaimExportService` 复用既有工作簿/凭证逻辑并按批次精确导出。
- 统一中国日期边界转换，使用 `[from 00:00, to+1 00:00)` UTC 查询 `SubmittedAt`。

## API

- `GET /api/admin/claim-archive-batches`
- `GET /api/admin/claim-archive-batches/{id}`
- `POST /api/admin/claim-archive-batches/preview`
- `POST /api/admin/claim-archive-batches`
- `PUT /api/admin/claim-archive-batches/{id}/name`
- `GET /api/admin/claim-archive-batches/{id}/export.zip`

列表接口增加 `archiveState=all|unarchived|archived` 和 `archiveBatchId`；分组接口增加 `groupBy=archiveBatch`。

## 前端

- `AdminClaimsView.vue`：新建归档弹窗、预览、确认、归档状态/批次筛选、批次划分和状态展示。
- `AdminReimbursementDashboardView.vue`、`AdminMealAllowancesView.vue`：批次筛选与划分。
- 批次列表可改名、查看摘要和重新导出 ZIP。
- `ClaimDetailDrawer.vue` 展示归档批次，但保留原审批/发放状态。

## ZIP

- 批次导出从 `ArchiveBatchId` 精确查询父报销，不使用批次日期重新选择。
- 复用 `XlsxWorkbookWriter`、凭证命名和 `MonthlyClaimArchiveWriter`。
- 文件名由批次名称净化得到；缺失凭证沿用 `EXPORT_ATTACHMENT_UNAVAILABLE` 整体失败。

## 不采用的方案

- 不把 `Archived` 加入 `ClaimStatus`：会覆盖审批状态并污染现有工作队列。
- 不给 `MealAllowance` 增加 `ArchiveBatchId`：会允许父子不一致。
- 不复制附件到物理目录：增加存储一致性、权限和清理成本，无业务收益。
