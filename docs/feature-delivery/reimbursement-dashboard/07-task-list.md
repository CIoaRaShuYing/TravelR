# 报销与餐补看板：任务清单

## RDB-001

Task ID: RDB-001

Milestone: 后端查询与聚合

Linked requirement: REQ-002, REQ-003, REQ-004, REQ-005

Linked acceptance criteria: AC-002, AC-003, AC-004, AC-005

Goal: 提供费用明细列表/分组接口和餐补分组接口。

Files likely to change: `Program.cs`、查询辅助类、后端测试。

Implementation steps: 固化当前版本和状态边界；实现过滤、分页、汇总、三维分组；补查询测试。

Verification method: 后端测试与编译。

Done condition: 服务端合同完成且查询边界测试通过。

Status: Done

Notes: 后端测试 45/45 通过，无数据库迁移；真实 PostgreSQL API 待完整本地栈验证。

## RDB-002

Task ID: RDB-002

Milestone: 两个看板前端

Linked requirement: REQ-001, REQ-002, REQ-003, REQ-004, REQ-006

Linked acceptance criteria: AC-001, AC-003, AC-004, AC-006

Goal: 完成命名、导航、报销看板和两个看板的划分方式。

Files likely to change: `api.ts`、`router.ts`、`AppShell.vue`、两个看板页面、`style.css`。

Implementation steps: 扩展类型和 API；新增页面；接入筛选、汇总、分组、分页、详情与响应式布局。

Verification method: TypeScript 检查和生产构建。

Done condition: 前端构建通过，交互口径与需求一致。

Status: Done

Notes: 前端生产构建通过，不新增依赖；真实登录态浏览器验证待运行环境可用后执行。

## RDB-003

Task ID: RDB-003

Milestone: 验证与文档回写

Linked requirement: REQ-001 至 REQ-006

Linked acceptance criteria: AC-001 至 AC-006

Goal: 完成自动化验证并记录运行时验收边界。

Files likely to change: 验收标准、任务清单、实施记录。

Implementation steps: 运行测试、构建、差异检查；修复根因；回写状态。

Verification method: 命令输出和 Git 状态。

Done condition: 静态验证通过，未完成的真实环境验收明确。

Status: Done

Notes: `git diff --check` 通过；Docker Desktop 未运行，未声称完成真实 API 或浏览器验收。不执行提交，除非用户另行要求。
