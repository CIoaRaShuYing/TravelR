# 项目会议记录：任务清单

| Task ID | Milestone | Linked requirement | Goal | Verification | Status |
|---|---|---|---|---|---|
| MR-01 | 领域与数据库 | REQ-001~REQ-005、REQ-009 | 新增主表、参会人、事项、软删除和并发配置 | 模型测试、迁移检查 | done |
| MR-02 | 后端 API | REQ-001~REQ-005、REQ-007、REQ-009 | 共享列表/详情/创建/编辑及管理员删除 | 服务/验证测试、build | done |
| MR-03 | Excel 导出 | REQ-006 | 专用多工作表模板导出 | OOXML 单测、officecli QA | done |
| MR-04 | 永久备份 | REQ-010 | 周日调度、补做、幂等、ZIP/JSON/checksum、独立目录 | 调度/文件测试 | done |
| MR-05 | 前端页面 | REQ-008 | 列表、动态编辑、冲突、删除、导出和移动端 | vue-tsc、Vite、浏览器 | done |
| MR-06 | 部署与迁移 | REQ-009、REQ-010 | EF 迁移、Compose 卷和 Linux 手册 | migration、compose config | done |
| MR-07 | 综合验收 | 全部 | 后端、前端、Excel、备份和差异检查 | 全量测试与人工抽检 | done |

## 完成条件

- 所有已确认验收标准有代码或文档证据。
- 后端测试、前端构建、Compose 配置和 `git diff --check` 通过。
- 生成的样本工作簿通过 OfficeCLI 结构与视觉检查。
- 未执行真实生产迁移、生产备份或服务器部署时，最终明确列为待部署验收。
