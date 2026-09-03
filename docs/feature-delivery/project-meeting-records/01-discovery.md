# 项目会议记录：现状调研

## 项目架构

- 后端：ASP.NET Core 8 Minimal API、EF Core、PostgreSQL、ASP.NET Core Identity。
- 前端：Vue 3、TypeScript、Element Plus、Vue Router，API 集中在 `frontend/src/api.ts`。
- 角色：`Applicant` 与 `Administrator`；多数业务页面要求已登录且个人资料完整。

## 可复用的周报链路

- 领域模型：`backend/TravelReimbursement.Api/Domain/Entities.cs` 中的 `WeeklyReport` 已包含项目、作者、最后编辑人、时间戳和 `ConcurrencyToken`。
- ORM：`backend/TravelReimbursement.Api/Data/AppDbContext.cs` 为周报配置唯一索引、长度约束、并发令牌和外键。
- API：`backend/TravelReimbursement.Api/Program.cs` 已实现周报查询、新建、编辑、权限校验、周一校验、审计和本人/管理员导出。
- 前端：`frontend/src/views/WeeklyReportsView.vue` 已实现项目筛选、周一日期选择、分页、编辑弹窗、并发更新和导出下载。
- 导出：`WeeklyReportExportService` 复用 `XlsxWorkbookWriter`，但现有生成器只支持无样式二维数据，不能复刻会议记录模板的合并单元格、边框、行高和打印设置。

## 参考 Excel 结构

`会议记录.xlsx` 包含 1 个工作表、13 行、9 列，纵向打印，主要结构如下：

- 标题：`会议记录`。
- 基本信息：`会议时间`、`会议地点`。
- 参会人员：序号、姓名、单位、职务、电话，模板预留 3 行。
- 内容区域标题：`会议内容摘要`。
- 统一事项表：需求内容、状态、截止时间、负责人，模板预留 4 行。
- `工作重点` 位于事项内容列，是示例内容，不是独立事项分类或分区标题。
- 使用 29 个合并区域、粗黑边框、黑体表头、宋体/Times New Roman 内容字体，纸张为 A4 纵向。

## 权限与审计基础

- 现有所有认证接口位于 `secured` 路由组；管理员接口另位于 `admin` 路由组。
- `AuditLog` 可记录操作人、动作、实体、追踪 ID 和上下文。
- 周报编辑通过 `ConcurrencyToken` 返回 409，适合复用到共享编辑场景。

## 定时任务与服务器存储基础

- `Program.cs` 已注册 `StagedAttachmentCleanupService`，项目已有 `BackgroundService` 和 `PeriodicTimer` 的后台任务模式。
- `LocalPrivateFileStore` 已实现配置路径解析、安全目录约束和服务器本地文件写入，但会议备份不应复用附件对象接口或与业务附件混存。
- Docker Compose 当前使用 `attachments_data:/data/private-uploads` 持久卷；会议备份适合增加独立 `meeting_record_backups:/data/meeting-record-backups` 持久卷。
- 当前项目没有定时数据库快照框架、分布式任务锁或外部对象存储；本次只做会议记录领域全量快照，不替代 PostgreSQL 和附件的整库灾备。

## 扩展点

- 新增会议记录主表及参会人员、会议事项子表。
- 在 `secured` 路由组提供全部用户共享的查询、创建、编辑和导出接口。
- 新增独立 Vue 页面、路由和主导航入口。
- 新增支持样式、合并单元格、动态行和多工作表的会议记录导出服务；不应把现有简单 `XlsxWorkbookWriter` 强行扩展成通用 Office 框架。
- 新增会议记录备份服务、独立配置目录和后台调度服务；备份文件使用原子写入并禁止覆盖。

## 风险

- “所有人可修改”意味着任意用户可修改他人创建的记录，必须保留创建人、最后编辑人、审计和并发冲突保护。
- 同项目同日允许多份记录，列表和工作表命名必须使用创建顺序或记录短 ID 区分。
- 多条会议记录导出为多个工作表时，需要处理工作表名称长度、非法字符和重名。
- 动态参会人员和事项数量会改变模板行数，导出必须保持边框、合并区域和打印可读性。
- 单机后台任务在未来横向扩容时会重复执行；当前部署为单 API 实例，可用“当周备份文件已存在”作为幂等保护，横向扩容不在本期范围。
- 永久保留会持续占用磁盘，必须记录备份大小和失败日志，并在部署手册中要求监控剩余空间。
