# 项目会议记录：实施日志

## 2026-09-03 需求与分析

- 完成参考 Excel、现有周报、权限、审计、导出、后台服务和部署卷调研。
- 用户确认共享编辑、任意日期、多记录、管理员删除、项目全量导出和每周永久备份。
- 经用户许可启用四个只读 subagent，分别完成后端数据库、前端交互、Excel 和备份设计分析。
- 技术路线确定为三表领域模型、专用 OOXML writer、独立后台备份服务和独立持久卷。

## 2026-09-03 实施完成

- 新增 `MeetingRecord`、`MeetingParticipant`、`MeetingRecordItem` 三表模型，会议记录采用并发令牌和管理员软删除；主表及子表使用匹配查询过滤器，备份通过 `IgnoreQueryFilters` 纳入已删除记录。
- 新增共享列表、项目选项、详情、创建、编辑和项目全量导出接口；所有已登录用户共享读写，管理员独占删除接口。
- 新增专用 OOXML 导出器。同一项目导出一个 `.xlsx`，按会议日期、创建时间、ID 稳定排序，每次会议生成一个 `yyyyMMdd-NN` 工作表；工作表包含日期地点、参会人员和会议内容摘要事项表，并设置 A4 纵向、单页宽度和动态打印区域。
- 新增周备份后台服务。按中国时区周日 02:00 形成备份槽，启动及每 15 分钟检查最近应执行槽位；同一槽位幂等，错过时只补最近一期。新版 ZIP 包含 `manifest.json`、`meeting-records.v2.json` 和 `checksums.sha256`，校验后原子落盘，应用不自动删除成功备份。
- 新增独立配置 `MeetingRecordBackup` 和 Compose 卷 `meeting_record_backups:/data/meeting-record-backups`；README 与 Linux 部署手册已补充异机备份及同批次恢复要求。
- 新增会议记录页面和编辑对话框，支持项目/日期筛选、动态增删和排序人员及事项、并发冲突保留输入并显式重载、管理员删除、项目全量导出及移动端布局。
- 生成 EF Core 迁移 `AddProjectMeetingRecords`，包含软删除一致性检查、外键、子项排序唯一索引和活动记录查询索引。

## 2026-09-03 新版模板修正

- 重新检查用户提供的新版 `会议记录.xlsx`，确认“需求内容”是统一事项表的首列标题，“工作重点”是事项内容示例，不是第二类事项。
- API 将 `Requirements`、`WorkFocuses` 合并为 `Items`；列表统一显示事项数；编辑器只保留一个“会议内容摘要”动态事项区。
- 导出改为一个事项表头和至少 4 行数据；截止日期标题与模板统一为“截止时间”。
- 基本信息和参会人员布局同步新版模板：使用“会议时间/会议地点”，参会人员恢复“序号、姓名、单位、职务、电话”列及对应合并关系。
- 备份结构升级为 `meeting-records.v2.json`，旧 v1 备份继续保留。
- 新增迁移 `UnifyMeetingRecordItems`：按原需求内容、原工作重点、原分类内顺序合并为全局顺序，再删除 `Kind` 字段。

## 验证证据

- `dotnet test TravelReimbursement.slnx --no-restore`：38/38 通过，覆盖统一事项模型、服务归一化、备份和导出结构。
- `npm.cmd run build`：`vue-tsc -b` 与 Vite 生产构建通过；仅保留项目既有的大包体积提示。
- `docker compose config --quiet`：退出码 0；当前账户无权读取用户级 Docker 配置文件的警告不影响 Compose 文件解析。
- EF Core：`UnifyMeetingRecordItems` 已在本地 PostgreSQL 实际应用，确认 `Kind` 列已删除，且 `(MeetingRecordId, SortOrder)` 唯一索引已建立；幂等迁移脚本可正常生成。
- HTTP 联调：在隔离数据库和临时 API 容器完成注册、登录、用户信息、项目、会议记录创建、列表、详情和导出验证；详情返回 2 条 `items`，列表返回 `itemCount=2`，旧分类字段不再返回。
- OfficeCLI：新版样本工作簿结构校验 `no errors found`、问题数 0；单次会议工作表为 13 行 9 列、29 个合并区域，基本信息、参会人员和统一事项表布局与新版参考文件一致。
- 浏览器自动化未在模板修正后重跑：本机 Playwright 模块加载失败；本轮以前端生产构建、组件结构检查和真实 HTTP/Excel 验证覆盖修改范围。
- `git diff --check`：通过。

## 部署边界

- 本地 PostgreSQL 已完成迁移和隔离 HTTP 验证；生产环境迁移、部署及真实周调度备份仍属于部署环境验收。
- “永久存储”表示应用不设置自动过期或删除策略。服务器磁盘、Docker 卷和异机副本仍由运维备份策略负责。
