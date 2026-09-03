# 项目会议记录：验收标准

## AC-001 共享可见与共享编辑

- Requirement: REQ-001、REQ-003
- Acceptance criteria: 普通用户和管理员看到相同会议记录集合；用户 A 创建后用户 B 可编辑；创建人仍为 A，最后编辑人为 B。
- Verification method: 两个不同用户的 API 权限矩阵测试和浏览器检查。
- Status: confirmed

## AC-002 多记录与任意日期

- Requirement: REQ-002、REQ-004
- Acceptance criteria: 新建只能选择启用项目；日期可选择任意有效日期；同项目同周及同日均可创建多条记录，不发生唯一键冲突。
- Verification method: API 集成测试和日期控件浏览器检查。
- Status: confirmed

## AC-003 动态结构化内容

- Requirement: REQ-005
- Acceptance criteria: 表单可增加、删除和调整参会人员及统一会议事项；保存后再次打开时字段和顺序保持一致；必填和长度校验返回明确错误。
- Verification method: API 数据往返测试和前端交互测试。
- Test data: 5 名参会人员、6 条会议事项。
- Status: confirmed

## AC-004 并发保护与审计

- Requirement: REQ-003、REQ-007、REQ-009
- Acceptance criteria: 旧并发令牌编辑或删除返回 409；创建、编辑、删除和导出均写审计，审计不包含会议正文或电话。
- Verification method: 后端集成测试和审计表断言。
- Status: confirmed

## AC-005 管理员软删除

- Requirement: REQ-009
- Acceptance criteria: 管理员可删除任意未删除会议记录；普通用户调用删除接口返回 403；删除后列表和项目导出不再出现，但数据库和备份中仍保留删除元数据及子项。
- Verification method: 权限矩阵、数据库查询和导出断言。
- Status: confirmed

## AC-006 按项目多表导出

- Requirement: REQ-006
- Acceptance criteria: 选择一个项目后，一份 Excel 包含该项目全部未删除会议记录，不受页面日期筛选和分页影响；每条记录一个工作表，并按日期从旧到新稳定排序。
- Verification method: XLSX 结构、单元格、工作表顺序和记录数量测试。
- Status: confirmed

## AC-007 模板与打印样式

- Requirement: REQ-006
- Acceptance criteria: 导出包含参考文件中的标题、会议时间/地点、参会人员和会议内容摘要统一事项表；事项列为需求内容、状态、截止时间、负责人；使用粗边框和合并区域；A4 纵向打印；动态行完整且长文本自动换行。
- Verification method: `officecli validate`、`officecli view issues`、HTML 预览和 Excel/WPS 人工抽检。
- Status: confirmed

## AC-008 每周全量备份

- Requirement: REQ-010
- Acceptance criteria: 可控时钟到达周日 02:00 时生成一个全量 ZIP；同周重复检查不覆盖或重复生成；停机错过后恢复可补做；快照包含未删除和已删除记录及全部子项。
- Verification method: 后台服务测试、临时目录文件断言、JSON 数据往返和 manifest/checksum 校验。
- Status: confirmed

## AC-009 永久存储与失败恢复

- Requirement: REQ-010
- Acceptance criteria: 应用没有自动删除历史备份的逻辑；新一周生成新文件且旧文件保持不变；写入失败不留下可被识别为成功的文件；重启后能读取已有周标识并避免重复。
- Verification method: 两周模拟、重启模拟、失败注入、Docker Compose 配置检查和部署文档核对。
- Risk: 永久保留持续占用服务器磁盘，需由运维监控容量并备份持久卷。
- Status: confirmed

## AC-010 页面可用性

- Requirement: REQ-008
- Acceptance criteria: 导航可进入会议记录页；桌面端显示表格，移动端显示可读列表；新建、编辑、筛选、翻页、管理员删除和导出均有加载、成功和错误反馈。
- Verification method: 前端类型检查、生产构建、桌面和移动端浏览器验收。
- Status: confirmed
