# 报销凭证分类与拖拽上传：任务清单

## TASK-001

Task ID: TASK-001
Milestone: M1 后端分类与兼容迁移
Linked requirement: REQ-001, REQ-002, REQ-004, REQ-005
Linked acceptance criteria: AC-002, AC-004, AC-005
Goal: 持久化附件用途并保持历史附件、草稿合同和导出兼容。
Files likely to change: `Domain/Entities.cs`、`Data/AppDbContext.cs`、`Program.cs`、EF migration、后端测试。
Implementation steps: 新增枚举和字段；上传接收用途；详情返回用途；生成迁移；补测试。
Verification method: 后端测试、迁移代码检查。
Done condition: 历史默认 Invoice，新上传用途可往返，提交最低校验和 ZIP 不回归。
Status: Done
Notes: 无破坏性数据清理。

## TASK-002

Task ID: TASK-002
Milestone: M2 前端双拖拽区与详情展示
Linked requirement: REQ-001, REQ-002, REQ-003, REQ-004
Linked acceptance criteria: AC-001, AC-002, AC-003, AC-004
Goal: 完成两个响应式上传区和分类详情。
Files likely to change: `frontend/src/api.ts`、`ClaimEditorDialog.vue`、`ClaimDetailDrawer.vue`、`style.css`。
Implementation steps: 扩展类型/API；按用途上传和筛选；实现 drag/multiple；添加分类样式；详情分组。
Verification method: `npm.cmd run build`、浏览器 smoke test。
Done condition: 两区可点击/拖拽，多文件状态正确，保存重开及详情分类正确。
Status: Done
Notes: 不新增依赖。

## TASK-003

Task ID: TASK-003
Milestone: M3 整体验证与文档回写
Linked requirement: REQ-001 至 REQ-005
Linked acceptance criteria: AC-001 至 AC-005
Goal: 完成构建、测试、差异检查和验收状态回写。
Files likely to change: `04-acceptance-criteria.md`、`07-task-list.md`、`08-implementation-log.md`。
Implementation steps: 运行验证；修复根因；记录未覆盖的真实浏览器/部署项。
Verification method: 后端测试、前端构建、`git diff --check`、可用时浏览器验证。
Done condition: 静态和自动化验证通过，未验证项如实列出。
Status: Done
Notes: 不执行 git commit。
