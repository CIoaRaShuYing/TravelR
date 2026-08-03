# 报销治理升级：验收结果

需求和验收方向已于 2026-08-03 确认。M1 至 M8 已完成，以下状态基于单元测试、隔离 PostgreSQL HTTP 流程、真实浏览器、Docker 运行态及实际数据库破坏式重建结果。

## REQ-101：注册审批

- Acceptance criteria: 管理员可看到待审核、已批准、已拒绝申请；批准和拒绝均只提交必填 `concurrencyToken`，无需填写原因；请求携带旧 `comment` 字段或缺少有效并发令牌时返回 400；响应和数据库模型不再包含注册审批原因；处理后保留状态、处理人和处理时间；普通申请人访问接口返回 403；重复处理返回明确错误。
- Verification method: 临时环境 API 覆盖批准、拒绝、重复处理、筛选和普通申请人权限；浏览器验证管理员审批界面。
- Status: 通过。M7 破坏式合同验收中旧 `comment` 请求和缺少并发令牌的请求均返回 400，仅包含有效 `concurrencyToken` 的新请求返回 200；注册申请响应不再出现 `reviewComment`，迁移已删除数据库列。

## REQ-102 至 REQ-104：项目与归属

- Acceptance criteria: 管理员可创建、修改、停用项目；停用项目不再出现在新建报销下拉框，但历史报销仍显示原项目；新建报销缺少项目或伪造无效/停用项目时服务端拒绝；每笔报销可由项目和申请人反向查询。
- Verification method: 临时环境验证项目创建、停用、启用、停用项目禁止新建报销；申请人使用 `GET /api/projects/available` 选择启用项目，使用 `GET /api/projects/mine` 筛选本人历史报销涉及的停用项目；管理员汇总按项目和申请人查询。
- Status: 通过。

## REQ-105 至 REQ-107：草稿、编辑、删除与版本一致性

- Acceptance criteria: 新建后“保存草稿”只产生 Draft，不进入审核队列；Draft、Submitted、Rejected 在批准前均可编辑；保存编辑后版本号增加、旧版本为 Superseded 且只读；编辑 Submitted 后旧审批项消失，新版本为 Draft；管理员待审批列表与审批详情不展示 Superseded 历史版本；删除未批准报销后为 Cancelled 且不能再审批；Approved 不显示编辑/删除且直接调用接口也被拒绝。
- Verification method: 临时环境验证 v1 草稿、保存 v2、v1 写入 `SupersededAt`、旧版本审批返回 409、已批准报销删除返回 409；浏览器验证 v1 金额 1,580 元，修改并提交后生成 v2，旧版本可在历史中只读查看。
- Status: 通过。M7 隔离验收额外构造“主记录为 Submitted、当前版本已写 SupersededAt”的异常数据，待审批列表和分组汇总均返回 0，直接审批返回 409；审批入口详情抽屉不渲染历史已作废版本。

## REQ-108：独立发放状态

- Acceptance criteria: 未批准报销显示“无需发放”；批准成功后显示“待发放”；普通申请人不能确认发放；管理员确认后显示“已发放”并保存管理员、时间、备注；同一报销不能重复确认；已发放状态没有直接撤销入口，直接调用非公开或伪造接口也不能回退。
- Verification method: 临时环境验证批准、待发放、确认发放、重复发放失败和不可回退；浏览器验证 v2 批准并确认发放。
- Status: 通过。

## REQ-109：管理员全部报销汇总

- Acceptance criteria: 页面能查到所有用户的当前有效报销，包含项目、申请人、金额、审批状态、发放状态和更新时间；项目、用户、状态、日期筛选可组合；可切换按项目或用户分组；分页总数和金额汇总与筛选结果一致；历史版本不重复计数。
- Verification method: 临时数据库/API 对照当前版本聚合结果，验证项目/申请人分组和组合筛选；浏览器验证 2 笔当前版本报销、合计 3,180 元；显式筛选 `Cancelled` 时分组汇总可返回作废记录，默认列表排除作废记录。
- Status: 通过。

## REQ-110：我的报销

- Acceptance criteria: 列表增加项目列和发放状态列；可按项目和审批状态筛选；Draft/Submitted/Rejected 显示符合状态的编辑和删除/撤回操作，Approved 只允许查看；筛选结果只包含当前登录用户。
- Verification method: API 验证申请人数据隔离和项目筛选；浏览器验证项目/状态筛选、编辑、删除/撤回、详情和版本历史操作。
- Status: 通过。

## REQ-111 至 REQ-112：审计与破坏式重建

- Acceptance criteria: 执行前明确显示并核对目标数据库和私有附件桶；清空后不存在原用户、报销、审批、附件元数据和对象文件；新迁移可在空库完整执行；角色种子只包含业务所需角色；首位管理员初始化后可登录；新产生的版本替换、撤回、审批和发放均可查到操作者、时间、前后状态和关联版本。
- Verification method: 清理前核对 `travel_reimbursement`、`travel_reimbursement_local`、`chuchai_postgres_data`、`chuchai_minio_data` 和 `D:\Code\chuchai\private-uploads`；两库删除并重建后应用唯一迁移 `20260803032725_InitialGovernanceRebuild`；主库只保留初始化管理员和 `Administrator`、`Applicant` 两个角色；项目、报销、附件、注册申请为 0；本地附件目录 0 文件，MinIO 无业务桶或对象；管理员登录通过。
- Status: 通过。旧业务数据、活动目录中的 3 个 PDF，以及 M6 复核发现的旧路径 `backend/TravelReimbursement.Api/private-uploads` 中 10 个无引用 PDF 均已按授权永久删除，不提供恢复；删除前已确认主库和本地基线库 `AttachmentAssets` 均为 0，当前活动存储路径不指向旧目录。

## REQ-113：可用性与并发提示

- Acceptance criteria: 各管理列表使用服务端分页；空结果、加载中、接口失败均有明确中文提示；版本过期或状态冲突提示用户刷新，不静默覆盖。
- Verification method: 检查服务端分页实现、前端 loading/empty/error 状态和 409 冲突提示；浏览器检查桌面与 `390x844` 移动端，确认无横向溢出且移动菜单包含全部管理入口。
- Status: 通过，但未实际制造超过 20 条数据验证跨页边界；服务端分页、总数计算和分页控件已实现，该项保留为规模化数据回归点。

## REQ-115：角色简化

- Acceptance criteria: 新系统业务权限只展示申请人和管理员；管理员拥有注册审批、项目管理、报销审批、发放确认和全量汇总菜单及接口权限；普通申请人只能处理自己的报销；不存在依赖 `Reviewer` 才能完成的业务流程。
- Verification method: 空库角色种子、JWT 角色、前端菜单和接口权限矩阵检查；临时环境验证管理员可提交并审批自己的报销且审计完整。
- Status: 通过。

## REQ-116：正式移除 MinIO

- Acceptance criteria: 后端项目和发布产物不再依赖 MinIO SDK；配置中不存在 Provider/Endpoint/AccessKey/SecretKey/Bucket；本地目录可配置且启动时验证可写；上传、哈希、鉴权下载、补偿删除和过期清理保持可用；对象键不能逃逸配置根目录；Compose 不再包含 MinIO 服务并为附件目录挂载持久卷。
- Verification method: 依赖和文本扫描、后端构建和单元测试、`docker compose config`、真实 API 本地上传/下载、目录与数据库元数据核对、旧 MinIO 容器和卷精确移除检查。
- Status: 通过。后端构建 0 警告、0 错误，12/12 测试通过，前端构建和 Compose 配置校验通过。隔离空库通过公开注册创建首位管理员，经正式登录后 staged 上传返回 201、鉴权下载返回 200，下载内容与 77 字节测试 PDF 的 SHA-256 一致，本地目录仅生成 1 个对象且数据库写入 1 条附件元数据。旧 `chuchai_minio_data` 只含 `.minio.sys` 系统元数据且仅由 `chuchai-minio-1` 使用，容器和卷均已精确删除。Chrome 中已确认管理员登录页面，但自动接管标签页连续超时，因此浏览器文件选择未作为本项通过依据；隔离真实 HTTP 流程已覆盖正常认证、上传、落盘、元数据和下载闭环。

## REQ-117：刷新保持登录

- Acceptance criteria: 登录后刷新当前页面仍显示原用户和其权限菜单；刷新后的首个鉴权请求携带恢复的 JWT；主动退出、JWT 到期或任一鉴权请求返回 401 后立即回到登录界面并删除会话；关闭标签页后不要求继续保持登录。
- Verification method: 前端生产构建；浏览器登录管理员后刷新并访问管理接口；检查 `sessionStorage` 的创建和退出清理；构造过期会话验证初始化清理。
- Status: 通过。隔离浏览器使用正式登录接口进入管理员工作台，刷新后管理员名称和“我的报销”仍可见、登录按钮为 0；主动退出后登录页立即出现，再次刷新仍保持退出。浏览器验收完成后临时 API、前端、数据库和目录均已删除。

## REQ-118：附件预览、费用默认项与单滚动弹窗

- Acceptance criteria: JPG、PNG、PDF 凭证通过现有鉴权下载接口在线预览，图片使用页面内图片容器，PDF 使用页面内嵌预览，仍可单独下载；关闭预览后释放临时 Blob URL。新增差旅行程默认两项费用且类别分别为去程、回程；切换普通单据后只保留“费用 1”且类别为空；点击“添加费用”后新费用类别为空，按钮始终位于最后一项下方。新增与编辑弹窗均只有正文区域可纵向滚动，弹窗、表单内容和页面本身不形成第二条滚动条。空类别草稿可保存为 `Unspecified`，提交审核返回类别必填错误。
- Verification method: Release 构建与单元测试；隔离 PostgreSQL 真实 HTTP 创建普通单据空类别草稿并尝试提交；隔离浏览器检查差旅/普通默认项、添加费用 DOM 顺序、滚动容器计算样式、PDF/PNG 上传预览、Blob URL 释放和控制台错误。
- Status: 通过。后端 Release 构建 0 警告、0 错误，13/13 测试通过；前端生产构建通过。真实 HTTP 中 `Unspecified` 草稿创建成功，提交返回 400、`CLAIM_VALIDATION_FAILED` 和 `category: 每项费用均需选择类别。`。浏览器中差旅默认两项往返交通，普通单据和手动新增费用均显示“请选择类别”；费用列表子节点顺序为费用 1、费用 2、添加费用。新增弹窗只有 `.el-dialog__body` 可滚动，编辑弹窗同样只有该区域可滚动；PDF 生成 1 个 Blob `iframe`，PNG 生成 1 个 Blob `img`，关闭后两类 Blob URL 均不可再读取；控制台 0 错误。隔离 API、前端、数据库和附件目录已清理，未写入当前业务数据库。
