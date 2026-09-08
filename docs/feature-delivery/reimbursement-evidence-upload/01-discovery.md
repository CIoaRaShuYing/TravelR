# 报销凭证分类与拖拽上传：项目探索

## 架构摘要

- 前端为 Vue 3 + TypeScript + Element Plus，报销编辑集中在 `frontend/src/components/ClaimEditorDialog.vue`。
- 后端为 ASP.NET Core 8 Minimal API + EF Core + PostgreSQL，附件文件写入私有本地存储，元数据写入 `AttachmentAssets`。
- 报销使用稳定主记录与不可变 `ClaimVersion`；费用与附件通过 `ExpenseItemAttachment` 关联。

## 当前上传链路

1. `ClaimEditorDialog.vue:353-368` 为每项费用渲染一个“凭证”上传按钮，没有 `drag` 模式。
2. `ClaimEditorDialog.vue:159-175` 调用 `api.uploadStagedAttachment(file)`，上传成功后加入统一 `item.attachments`。
3. `frontend/src/api.ts:369-372` 向 `POST /attachments/staged` 发送仅含 `file` 的 `FormData`。
4. `Program.cs:344-370` 校验并保存文件，创建 `AttachmentAsset`，当前没有附件业务类型字段。
5. `ClaimEditorDialog.vue:203-211` 保存草稿时只发送统一 `attachmentIds`。
6. `ClaimWorkflowService.cs:458-470,501-520` 校验附件归属后创建 `ExpenseItemAttachment` 关联。

## 当前提交和审核链路

- 前端 `ClaimEditorDialog.vue:215-241` 对每条费用要求 `item.attachments.length > 0`。
- 后端 `ClaimSubmissionValidator.cs:7-18` 再次要求每条费用至少有一个 `Accepted` 附件；不能只改前端而破坏服务端兜底。
- `Program.cs:1046-1084` 的详情响应把附件作为统一数组返回。
- `ClaimDetailDrawer.vue:150-158` 申请人和管理员共用费用详情，仅展示统一附件列表，当前无法区分发票和支付记录。

## 数据库与版本边界

- `AttachmentAsset` 保存物理文件的所有者、对象键、文件名、类型、大小、哈希、扫描和绑定状态。
- `ExpenseItemAttachment` 当前只有 `ExpenseItemId` 和 `AttachmentAssetId`。
- 附件用途描述文件本身，放在 `AttachmentAsset` 可保持现有草稿合同 `attachmentIds` 不变；用户已确认历史行默认归为 `Invoice`。
- 新字段需要 EF Core migration；这是加列式兼容迁移，不需要清理现有数据库或文件。

## 当前文件上传入口清单

- 全前端仅发现 `ClaimEditorDialog.vue` 中一个 `<el-upload>`。
- 因此本轮“上传文件的位置都要做拖拽”实际覆盖拆分后的发票区和支付记录区。

## 导出影响

- `MonthlyClaimExportService` 当前从费用附件关联统一收集文件。
- `MonthlyClaimArchiveWriterTests` 约束 ZIP 中保留 `报销凭证/` 目录和现有重命名规则。
- 若仅新增分类展示而不改变导出目录，现有导出可继续工作；按类别拆目录属于可选扩展。

## 可扩展点

- 为附件资产增加用途枚举：`Invoice`、`PaymentRecord`。
- 上传接口额外接收用途，响应和详情投影返回用途。
- 编辑器把新上传附件放入对应区域；详情按用途分组展示。
- Element Plus `el-upload` 原生支持 `drag`，无需新增依赖。

## 风险与不确定事项

- 历史附件实际内容可能并非发票，但用户已明确决定迁移时统一定义为发票；实施需在迁移中稳定写入该默认值。
- 只在前端维护两个数组会在保存后丢失分类，且管理员详情无法判断上传入口来源。
- 多文件拖拽会并发触发单文件上传，需要避免 loading 状态提前结束和重复附件关联。
- 需要用后端测试覆盖服务端“任一类即可”及历史附件默认归为发票，并用前端构建验证模板与类型。
