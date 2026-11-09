# 实施记录

2026-11-09：需求已确认。用户授权重置历史工资发放及删除工资审计。开始数据模型、草稿项目和锁定校验；未操作真实数据库。取消期间保持取消以遵守月份唯一约束。

## 已完成变更

- Entities、AppDbContext、Requests、PayrollService：项目外键、项目返回、草稿保存与审计、锁定必选项目。
- `20261109085942_AddPayrollProject` 及 Designer / ModelSnapshot：项目字段增量迁移与一次性历史重置。
- CostDashboardService、Program：真实工资项目归属，起止月份过滤及参数校验，项目范围不再排除工资。
- api.ts、AdminPayrollView.vue、AdminCostDashboardView.vue、style.css：桌面单选列、移动端编辑、脏数据检测、月份选择与组合筛选。
- CostDashboardQueryTests：项目工资纳入汇总、中国时区月边界、月份参数校验、联合查询 SQL 翻译。

## 验证结果

- `dotnet build TravelReimbursement.slnx --no-restore -m:1`：通过，0 警告、0 错误。
- `dotnet test TravelReimbursement.slnx --no-restore --no-build -m:1`：通过，89 项。
- frontend `npm.cmd run build`：类型检查及构建通过；存在 bundle 大小和插件耗时提示。
- `dotnet ef migrations has-pending-model-changes --no-build --project backend/TravelReimbursement.Api`：通过，无遗漏模型变更。
- 独立 Docker PostgreSQL 18.6，127.0.0.1:55449/payroll_project_test：18 项集成检查通过。先用新模型构造历史记录，再降到原工资迁移、应用本次迁移，验证历史已发/待发重置、同月取消期间保留、凭据与工资审计删除且其他领域审计保留、金额不变、项目留空、并发令牌更新。
- 继续验证：草稿不入看板、空项目锁定拒绝、非法/停用项目保存拒绝、保存回显、过期令牌拒绝、重新锁定、项目月份汇总、区间外排除、重新登记发放、禁止重复发放、重复启动不重复重置。
- `git diff --check`：通过。
- 沙箱内 vstest 本机通信超时，隔离数据库连接遇 SocketException 10013；沙箱外重跑以上相同验证通过。首次 solution 并行构建无具体编译错误，采用 `-m:1` 后通过。
- 临时验证代码 `.tmp/payroll-project-check` 已由 .gitignore 排除；不暂存或提交。
- 未执行：真实业务库迁移、浏览器交互与移动端视觉验收、部署、Git 提交。

## 部署与回滚

应用原本在启动时执行迁移；部署本版本首次启动会执行一次历史工资重置。执行后需要管理员重新分配项目、锁定、登记发放。此前草稿期间不计入费用。
本次明确授权清除发放及工资审计，Down 仅移除新增字段和外键，不恢复这些历史记录。已取消期间不激活，避免同月有效期间唯一约束冲突。未来新操作仍按原规则产生审计。
