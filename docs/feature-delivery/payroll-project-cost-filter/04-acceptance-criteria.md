# 验收标准

| Requirement | Acceptance criteria | Verification method / Test data | Risk | Status |
| --- | --- | --- | --- | --- |
| REQ-001 | 项目单选保存回显；空项目不能锁定；非法或停用新项目拒绝；桌面/移动可编辑 | 服务与构建检查、项目 A/B | 归属错误 | automated checks passed |
| REQ-002 | 有效期间均草稿；项目空；无工资发放凭据和工资审计；取消状态及其他领域审计保留；并发令牌刷新 | 隔离 PostgreSQL 迁移测试，含已发、待发、取消 | 重置不可逆 | automated checks passed |
| REQ-003 | 项目中三类费用正确合计，无重复或漏计；草稿工资不统计 | 查询测试及 Npgsql 翻译 | 汇总错误 | automated checks passed |
| REQ-004 | 首尾月包含；跨年、时区边界正确；倒置月份报错；所有视图同口径 | 查询测试、服务校验及前端构建 | 时间混淆 | automated checks passed |

基础验证：dotnet build / test TravelReimbursement.slnx，frontend npm.cmd run build。

项目选择持久化、非法项目、锁定及并发行为已经隔离 PostgreSQL 服务测试验证；页面通过类型检查和生产构建。浏览器交互和移动端视觉验收未执行。
