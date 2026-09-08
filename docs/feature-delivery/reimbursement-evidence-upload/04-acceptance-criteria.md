# 报销凭证分类与拖拽上传：验收标准

## AC-001：两个分类上传区域

Requirement: REQ-001
Acceptance criteria: 每条费用同时显示“发票上传”和“支付记录上传”两个独立区域；两个区域各自展示已上传文件，移除一个区域的文件不影响另一区域。
Verification method: 前端组件检查、生产构建、浏览器新建与编辑报销验证。
Test data: 一份 PDF 发票、一张 PNG 支付截图。
Risk: 窄屏下两个拖拽区拥挤。
Status: Implemented; build verified; browser verification pending

## AC-002：任一类满足最低提交条件

Requirement: REQ-002
Acceptance criteria: 对每条费用，仅有发票可提交；仅有支付记录可提交；两类都有可提交；两类都没有时前端阻止提交，绕过前端直接调用 API 也被后端拒绝。管理员仍可正常驳回只有一种凭证的报销。
Verification method: 后端单元测试、浏览器四组场景验证。
Test data: 发票-only、支付记录-only、两类都有、两类都无。
Risk: 只改前端会被 API 绕过，必须保留服务端校验。
Status: Verified by backend tests; browser/API smoke verification pending

## AC-003：拖拽与点击上传

Requirement: REQ-003
Acceptance criteria: 两个区域都可以点击选择文件，也可以拖入一个或多个受支持文件；文件进入对应类别；超 10MB 或格式/内容不合规时显示明确错误且不加入列表。
Verification method: 前端生产构建、浏览器拖拽/点击/错误场景验证。
Test data: JPG、PNG、PDF、多文件组合、超 10MB 文件、不支持文件。
Risk: 多文件并发上传时 loading 状态和成功提示必须准确。
Status: Implemented; build verified; real drag-and-drop verification pending

## AC-004：分类持久化与历史兼容

Requirement: REQ-004
Acceptance criteria: 保存草稿并重新打开后分类不丢失；提交后申请人和管理员详情能按类别查看；迁移前附件按已确认口径显示为“发票”，并继续满足最低提交条件。
Verification method: API/数据库集成测试、浏览器保存重开与管理员详情验证、迁移 SQL 检查。
Test data: 新分类附件、迁移前附件、包含多个不可变版本的报销。
Risk: 历史附件统一归为发票是用户确认的业务口径，不代表系统执行了内容识别。
Status: Implemented; migration SQL verified; database/browser verification pending

## AC-005：现有能力无回归

Requirement: REQ-005
Acceptance criteria: 分类附件仍可预览、下载并随当前版本导出，且全部位于 ZIP 的 `报销凭证/` 目录；附件鉴权、私有存储、文件限制、版本绑定和清理逻辑不变；后端测试与前端生产构建通过。
Verification method: `dotnet test TravelReimbursement.slnx --no-restore`、`npm.cmd run build`、相关浏览器 smoke test。
Test data: 申请人账号、管理员账号、包含两类附件的报销。
Risk: 运行中的 API 可能锁定默认输出目录，必要时使用独立 `BaseOutputPath`。
Status: Verified locally; deployed acceptance pending
