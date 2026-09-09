# 报销归档批次：任务清单

| Task ID | Milestone | Linked requirement | Goal | Verification | Status |
|---|---|---|---|---|---|
| CAB-01 | M1 领域与数据库 | REQ-001、REQ-003 | 新增批次模型、父报销外键、映射、迁移和资格规则 | 模型/规则测试、迁移生成 | done |
| CAB-02 | M2 服务与接口 | REQ-002、REQ-007 | 预览、原子创建、列表、详情、改名和审计 | 服务/查询测试、API 构建 | done |
| CAB-03 | M3 批次 ZIP | REQ-006 | 按精确批次成员生成并下载 ZIP | ZIP/工作表测试 | done |
| CAB-04 | M4 查询扩展 | REQ-004、REQ-005 | 三个管理查询面支持归档筛选与分组 | 查询规则测试 | done |
| CAB-05 | M5 前端 | REQ-004、REQ-005、REQ-007 | 归档交互、状态、筛选、划分、改名和导出 | vue-tsc、Vite、浏览器 | done |
| CAB-06 | M6 综合验收 | REQ-008 | 数据库、HTTP、权限、审计和回归验证 | 全量测试、隔离环境 smoke | done |

每个里程碑完成后更新状态和 `08-implementation-log.md`；发现方案偏差时先回写对应文档。
