# 报销凭证分类与拖拽上传：技术方案

## 方案目标

用最小的跨层改动持久化附件用途，提供两个清晰、响应式的拖拽区，并维持现有报销版本、附件鉴权和导出合同。

## 数据库与领域模型

- 表：`AttachmentAssets`
- 新列：`Purpose varchar(32) NOT NULL DEFAULT 'Invoice'`
- 枚举：`AttachmentPurpose.Invoice`、`AttachmentPurpose.PaymentRecord`
- 迁移：EF Core 前向加列；现有行自动得到 `Invoice`。
- 回滚：删除 `Purpose` 列；不删除附件文件或其他数据。
- 破坏性：否。

## API 合同

- `POST /api/attachments/staged?purpose=Invoice|PaymentRecord`
- multipart 文件字段仍为 `file`。
- 上传响应和报销详情附件对象增加 `purpose`。
- 草稿/版本请求继续使用 `attachmentIds`，无破坏性修改。

## 前端模型与交互

- `Attachment` 增加 `purpose`，新增 `AttachmentPurpose` 联合类型。
- `ClaimEditorDialog` 保留一个附件数组，以 `purpose` 筛选到两个区域。
- 每个区域支持点击、拖拽和多文件；沿用预览、下载、移除功能。
- 桌面端两个区域并排，移动端单列。
- 视觉方向沿用现有冷绿色账务界面：发票区使用低饱和青绿色，支付记录区使用低饱和琥珀色；差异只编码业务分类，不新增装饰性动效或字体依赖。
- 标志性元素是两个并列的“凭证投递格”，标题、状态和文件列表都在各自边界内，避免用户把文件拖错类别。

## 服务与权限边界

- 上传前继续执行现有扩展名、MIME、文件头和 10MB 校验。
- 权限、私有文件存储、暂存绑定和清理保持不变。
- 提交校验仍要求每条费用至少一个 `Accepted` 附件，等价覆盖两个用途的 OR 规则。

## 错误处理

- 非法或缺失用途由模型绑定返回 400。
- 文件校验和上传错误继续通过现有 API 错误消息展示。
- 多文件中单个失败不移除已成功文件，用户可针对失败文件重试。

## 导出与回滚

- 月度 ZIP 收集逻辑不改，所有附件继续写入 `报销凭证/`。
- 应用回滚前若数据库列仍存在，旧代码可忽略该列；数据库回滚可在确认无需保留分类后删除列。

## 验证命令

- `dotnet test TravelReimbursement.slnx --no-restore`
- `npm.cmd run build`（`frontend`）
- `git diff --check`
- 条件允许时执行浏览器新建、编辑、详情和拖拽 smoke test。

## 不采用的方案

- 不只在前端拆数组：保存后会丢失分类。
- 不把用途放在 `ExpenseItemAttachment`：用途是文件本身属性，放关联表会增加请求合同和复制版本复杂度。
- 不修改 ZIP 目录：用户明确要求维持现状。
- 不新增上传依赖：Element Plus 已提供拖拽能力。
