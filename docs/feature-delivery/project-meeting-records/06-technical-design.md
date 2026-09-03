# 项目会议记录：技术方案

## 数据模型

### MeetingRecord

- `Id Guid`
- `ProjectId Guid`
- `MeetingDate DateOnly`
- `Location string(200)`
- `CreatedById Guid`
- `LastEditedById Guid`
- `CreatedAt DateTimeOffset`
- `UpdatedAt DateTimeOffset`
- `DeletedById Guid?`
- `DeletedAt DateTimeOffset?`
- `ConcurrencyToken Guid`
- `Participants List<MeetingParticipant>`
- `Items List<MeetingRecordItem>`

索引：活跃记录 `(ProjectId, MeetingDate, CreatedAt)`、活跃记录 `(MeetingDate, CreatedAt)`；不设置日期唯一键。删除字段增加成对为空或成对有值的检查约束。

### MeetingParticipant

- `Id Guid`、`MeetingRecordId Guid`、`SortOrder int`
- `Name string(100)` 必填
- `Organization string(200)?`
- `Title string(100)?`
- `Phone string(50)?`
- 唯一索引 `(MeetingRecordId, SortOrder)`

### MeetingRecordItem

- `Id Guid`、`MeetingRecordId Guid`、`Kind MeetingRecordItemKind`、`SortOrder int`
- `Content string(4000)` 必填
- `Status string(100)?`
- `DueDate DateOnly?`
- `Owner string(100)?`
- 唯一索引 `(MeetingRecordId, Kind, SortOrder)`

## API 合同

- `GET /api/meeting-records?projectId&dateFrom&dateTo&page&pageSize`：共享活跃记录概览。
- `GET /api/meeting-records/{id}`：共享完整详情。
- `POST /api/meeting-records`：所有用户创建，仅允许启用项目。
- `PUT /api/meeting-records/{id}`：所有用户完整替换，携带 `ConcurrencyToken`。
- `DELETE /api/admin/meeting-records/{id}?concurrencyToken=`：管理员软删除。
- `GET /api/meeting-records/export.xlsx?projectId=`：导出项目全部活跃记录。

创建/编辑 DTO 使用 `Participants`、`Requirements`、`WorkFocuses` 三个数组。参会人姓名和事项内容必填；单位、职务、电话、状态、截止日期和负责人可空。项目、日期、地点必填；至少一名参会人且两类事项合计至少一条。每类数组上限 100，防止异常请求。

## 服务边界

- `MeetingRecordService`：验证、创建、详情、更新、删除、DTO 映射和审计。
- `MeetingRecordExportService`：项目验证、全量查询、稳定排序和工作簿生成。
- `MeetingRecordWorkbookWriter`：纯 OOXML 输出，不依赖 EF Core。
- `MeetingRecordBackupService`：一致性读取、序列化、校验和原子提交。
- `MeetingRecordBackupHostedService`：调度、错过补做、重试和日志。

## 并发与事务

- 更新和删除先比较请求令牌，再依靠 EF `ConcurrencyToken` 捕获保存阶段竞态。
- 409 错误统一使用 `MEETING_RECORD_STALE`。
- 更新时物理替换子项，但主记录不物理删除；一次保存提交。
- 备份读取使用 `RepeatableRead`，数据库事务与文件系统不能组成单一事务，因此文件校验成功后才原子改名为最终文件。

## Excel 版式

- 固定 9 列，列宽比例参考模板。
- 标题、会议日期/地点、参会人员、会议内容摘要、需求内容、工作重点均使用独立分区。
- 参会人员、需求、工作重点行数分别至少为 3、1、3，超出时动态扩展。
- 所有表格使用黑色中粗边框；表头黑体粗体居中；正文宋体；内容自动换行。
- 工作簿名：`会议记录_{项目编码}_{yyyyMMddHHmmss}.xlsx`。
- 工作表名：`yyyyMMdd-NN`，过滤非法字符并保证 31 字符以内和大小写不重复。
- 项目无记录返回 `MEETING_RECORD_EXPORT_EMPTY`。

## 备份格式与配置

配置：

```json
"MeetingRecordBackup": {
  "Enabled": true,
  "LocalPath": "../../meeting-record-backups",
  "TimeZoneId": "Asia/Shanghai",
  "RetryIntervalMinutes": 15
}
```

最终文件：`YYYY/meeting-records-full-week-YYYY-MM-DD.zip`，日期为备份周期周一。ZIP 包含：

- `manifest.json`：格式版本、备份 ID、周期、计划时间、捕获时间、数量和触发方式。
- `meeting-records.v1.json`：稳定排序的全部记录、项目快照、用户显示信息、子项和软删除元数据。
- `checksums.sha256`：manifest 和数据文件的 SHA-256。

备份不包含密码、JWT、银行卡、报销或附件数据。成功文件禁止覆盖、禁止应用自动删除。

## 前端设计

- `MeetingRecordsView.vue`：共享列表、筛选、分页、导出弹窗、管理员删除。
- `MeetingRecordEditorDialog.vue`：大型滚动弹窗和动态表单。
- 列表概览显示日期、项目、地点、参会人数、需求数、重点数、创建人和最后编辑。
- 移动端使用现有记录列表样式；重复项在窄屏切换为单列。
- 冲突时保留输入，显示 `el-alert` 和“重新加载最新内容”操作。

## 迁移与部署

- 新增迁移 `AddProjectMeetingRecords`，只创建新表、索引和约束，无历史数据回填。
- `appsettings.json` 增加开发默认备份目录。
- Docker Compose 增加 `/data/meeting-record-backups` 挂载和 `meeting_record_backups` 卷。
- Linux 部署手册增加卷检查、备份、恢复、容量监控和禁止误删说明。

## 验证命令

- `dotnet test backend/TravelReimbursement.Api.Tests/TravelReimbursement.Api.Tests.csproj -c Release --no-restore`
- `npm.cmd run build`（目录 `frontend`）
- `docker compose config --quiet`
- `git diff --check`
- 对生成样本执行 `officecli validate`、`officecli view issues`、`officecli query merge`、`officecli view html`。
