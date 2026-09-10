# 工资发放管理：任务清单

## M1 领域模型、迁移与服务

Task ID: PAY-001
Linked requirement: REQ-001 至 REQ-008、REQ-011
Goal: 新增三表模型、公式、状态机、并发、审计与发放服务。
Verification method: 模型/公式/状态规则测试，迁移生成与审查。
Done condition: 后端编译且新增测试通过。
Status: done

## M2 API、权限与导出

Task ID: PAY-002
Linked requirement: REQ-006 至 REQ-011
Goal: 接入管理员和本人接口、`no-store`、独立候选投影与工资表 Excel。
Verification method: API 合同检查、导出测试、401/403 与敏感字段验证。
Done condition: 接口与导出满足验收口径。
Status: done

## M3 前端工资工作台

Task ID: PAY-003
Linked requirement: REQ-001 至 REQ-010
Goal: 实现管理员月表、移动端编辑、逐人发放、我的工资和导航。
Verification method: `vue-tsc`、Vite build、桌面与 390×844 浏览器验收。
Done condition: 完成月度填写、锁定、发放和本人查看闭环。
Status: done

## M4 综合验证与文档回写

Task ID: PAY-004
Linked requirement: 全部
Goal: 完整回归、隔离 PostgreSQL、真实 HTTP、浏览器和迁移验证，并回写验收状态。
Verification method: 后端全量测试/Release build、前端 build、`git diff --check`、HTTP/浏览器证据。
Done condition: 可在当前可用环境验证的标准全部通过，外部/生产门槛明确记录。
Status: done
