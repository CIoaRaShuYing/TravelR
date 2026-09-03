# 项目会议记录：实施日志

## 2026-09-03 需求与分析

- 完成参考 Excel、现有周报、权限、审计、导出、后台服务和部署卷调研。
- 用户确认共享编辑、任意日期、多记录、管理员删除、项目全量导出和每周永久备份。
- 经用户许可启用四个只读 subagent，分别完成后端数据库、前端交互、Excel 和备份设计分析。
- 技术路线确定为三表领域模型、专用 OOXML writer、独立后台备份服务和独立持久卷。

## 2026-09-03 实施完成

- 新增 `MeetingRecord`、`MeetingParticipant`、`MeetingRecordItem` 三表模型，会议记录采用并发令牌和管理员软删除；主表及子表使用匹配查询过滤器，备份通过 `IgnoreQueryFilters` 纳入已删除记录。
- 新增共享列表、项目选项、详情、创建、编辑和项目全量导出接口；所有已登录用户共享读写，管理员独占删除接口。
- 新增专用 OOXML 导出器。同一项目导出一个 `.xlsx`，按会议日期、创建时间、ID 稳定排序，每次会议生成一个 `yyyyMMdd-NN` 工作表；工作表包含日期地点、参会人员、需求内容和工作重点，并设置 A4 纵向、单页宽度和动态打印区域。
- 新增周备份后台服务。按中国时区周日 02:00 形成备份槽，启动及每 15 分钟检查最近应执行槽位；同一槽位幂等，错过时只补最近一期。ZIP 包含 `manifest.json`、`meeting-records.v1.json` 和 `checksums.sha256`，校验后原子落盘，应用不自动删除成功备份。
- 新增独立配置 `MeetingRecordBackup` 和 Compose 卷 `meeting_record_backups:/data/meeting-record-backups`；README 与 Linux 部署手册已补充异机备份及同批次恢复要求。
- 新增会议记录页面和编辑对话框，支持项目/日期筛选、动态增删和排序人员及事项、并发冲突保留输入并显式重载、管理员删除、项目全量导出及移动端布局。
- 生成 EF Core 迁移 `AddProjectMeetingRecords`，包含软删除一致性检查、外键、子项排序唯一索引和活动记录查询索引。

## 验证证据

- `dotnet test TravelReimbursement.slnx --no-restore`：37/37 通过。
- `npm.cmd run build`：`vue-tsc -b` 与 Vite 生产构建通过；仅保留项目既有的大包体积提示。
- `docker compose config --quiet`：退出码 0；当前账户无权读取用户级 Docker 配置文件的警告不影响 Compose 文件解析。
- OfficeCLI：样本工作簿 3 个工作表、同日序号稳定、结构校验 `no errors found`；HTML 预览确认表头、边框、合并、换行和列宽正常。
- 浏览器：桌面和 `390x844` 移动端检查通过；列表、管理员操作、动态编辑表单无重叠，详情字段完整回填。
- `git diff --check`：通过。

## 部署边界

- 本地未启动真实 PostgreSQL，因此未实际应用迁移、调用真实 API 或生成真实计划任务备份；这些属于部署环境验收。
- “永久存储”表示应用不设置自动过期或删除策略。服务器磁盘、Docker 卷和异机副本仍由运维备份策略负责。
