# 报销凭证分类与拖拽上传：技术分析

## 分析方式

- 本功能改动集中在一个实体字段、一个上传端点、两个前端组件和一组测试，主 Agent 可以完成等价只读分析；未启用 subagent，也没有并行编辑风险。

## 已验证事实

- `AttachmentAsset` 是文件级元数据，单个附件资产绑定到一个报销，但可随不可变版本继续建立费用关联。
- 草稿合同只传 `attachmentIds`；将用途存在 `AttachmentAsset` 上，不需要破坏 `ExpenseItemDraftRequest`。
- `POST /attachments/staged` 当前是唯一创建附件资产的入口，可在上传时强制给出用途。
- 前后端现有提交规则本质上已经是“统一附件数组至少一份”，新增两个用途后仍等价于 OR 规则。
- 当前 ZIP 导出读取附件资产，不依赖用途；新增字段不会改变 `报销凭证/` 结构。

## 技术判断

- 新增 `AttachmentPurpose { Invoice, PaymentRecord }` 和 `AttachmentAsset.Purpose`。
- 数据库列使用字符串存储，非空，迁移默认值为 `Invoice`，直接落实历史附件定义。
- 上传接口从查询参数接收用途，例如 `POST /attachments/staged?purpose=PaymentRecord`；文件仍在 multipart body 中。
- 详情响应返回 `purpose`；编辑器和详情通过筛选统一数组分组，避免重复维护附件 ID 合同。
- 两个 `el-upload` 使用 `drag` 与 `multiple`；上传仍逐文件调用现有端点。

## 风险控制

- API 必须拒绝未给用途或非法用途，避免新附件分类为空。
- 多文件同时上传使用分用途计数器维护 loading，避免第一个请求结束时错误解除状态。
- 历史迁移不可推断内容，固定 `Invoice` 是已确认的数据口径。
- 不修改导出收集逻辑，避免无关回归。
