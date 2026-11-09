# 技术方案

- PayrollEntries 增加 nullable uuid ProjectId，Restrict 外键、索引；实体 Project 导航。EF 生成迁移与快照。
- SavePayrollEntryRequest 末尾新增可空 ProjectId；PayrollEntryRow 返回 ProjectId/ProjectName/ProjectCode；草稿事务、审计及并发沿用。
- 锁定前校验所有项目存在且启用；之后仍禁止编辑。
- 一次性迁移：删除 PayrollPayoutRecords；工资领域审计以 PayrollPeriod、PayrollEntry、PayrollPayoutRecord EntityType 精确清除；非 Cancelled 期间退回 Draft；清除发放、锁定/完成时间及收款快照、刷新令牌。金额保留。取消期间不激活。
- Down 仅回滚新增外键、索引、字段，不能恢复已清除的发放与审计；恢复数据须使用迁移前备份。开发验证仅使用隔离测试库，不运行真实业务数据库迁移。
- GET /api/admin/cost-dashboard 增加 fromMonth/toMonth 可空 DateOnly，必须为月首且起始不晚于截止。工资 PayrollMonth 按包含边界过滤；报销与餐补按中国时区 SubmittedAt 半开区间过滤。
- 看板保留 payrollExcluded 返回字段但固定 false，移除前端类别与项目互斥；所有汇总及明细共享查询。
- 工资页用现有 listAvailableProjects 接口获取启用项目，已选择但停用项目通过明细返回名称回显。项目纳入脏数据检测。
- 不引入新依赖，不修改权限及部署架构；不实现历史归属专用接口。
- 验证：后端构建及测试，前端类型检查和构建，隔离数据库迁移与业务 smoke test。
