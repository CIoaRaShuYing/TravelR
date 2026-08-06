# 用户中心与申请人筛选修复：任务清单

## TASK-UC-001

- Milestone: 后端目录与权限
- Linked requirement: REQ-UC-001, REQ-UC-002, REQ-UC-004
- Linked acceptance criteria: 用户中心列表
- Goal: 新增管理员用户列表 API，过滤正式用户并返回安全字段。
- Files likely to change: `backend/TravelReimbursement.Api/Program.cs`
- Implementation steps: 查询用户、角色、关键字和启用状态；服务端分页；批量组装角色。
- Verification method: Release build、接口静态检查。
- Done condition: 管理员可分页读取用户且响应不含认证敏感字段，普通用户受授权保护。
- Status: done

## TASK-UC-002

- Milestone: 后端账号启停
- Linked requirement: REQ-UC-003, REQ-UC-004
- Linked acceptance criteria: 账号启停与权限
- Goal: 新增启用/停用接口、审计和最后管理员保护。
- Files likely to change: `backend/TravelReimbursement.Api/Program.cs`, `frontend/src/api.ts`
- Implementation steps: 校验用户存在、当前账号和最后管理员；更新 `IsActive`；写 `AuditLog`。
- Verification method: Release build、现有测试、错误码静态检查。
- Done condition: 启停接口受管理员保护且不可停用当前账号/最后启用管理员。
- Status: done

## TASK-AP-001

- Milestone: 申请人目录
- Linked requirement: REQ-AP-001 至 REQ-AP-004
- Linked acceptance criteria: 申请人目录完整性
- Goal: 新增启用 Applicant 目录 API。
- Files likely to change: `backend/TravelReimbursement.Api/Program.cs`, `frontend/src/api.ts`
- Implementation steps: 角色过滤、关键字、分页、稳定排序，定义前端类型和调用。
- Verification method: Release build、接口静态检查。
- Done condition: 无报销但已正式注册/审批通过的 Applicant 可被目录查询。
- Status: done

## TASK-AP-002

- Milestone: 前端用户中心与筛选接入
- Linked requirement: REQ-UC-001 至 REQ-AP-005
- Linked acceptance criteria: 用户中心列表、筛选与汇总兼容
- Goal: 完成用户中心页面、导航和申请人下拉接入。
- Files likely to change: `frontend/src/views/AdminUsersView.vue`, `frontend/src/router.ts`, `frontend/src/components/AppShell.vue`, `frontend/src/views/AdminClaimsView.vue`, `frontend/src/style.css`
- Implementation steps: 管理页面、启停确认、远程申请人搜索、路由和移动端入口。
- Verification method: `npm.cmd run build`、浏览器桌面/移动布局检查。
- Done condition: 管理员能进入用户中心并管理账号，申请人下拉能查到无报销正式用户。
- Status: done

## TASK-VERIFY-001

- Milestone: 验证与文档
- Linked requirement: 全部
- Linked acceptance criteria: 全部
- Goal: 运行构建测试并回写实施日志和验收状态。
- Files likely to change: `08-implementation-log.md`, `04-acceptance-criteria.md`, `07-task-list.md`
- Implementation steps: 运行命令、修复编译问题、记录真实环境缺口。
- Verification method: 后端构建/测试、前端构建，必要时真实 HTTP/浏览器。
- Done condition: 静态验证通过，真实验收边界明确记录。
- Status: done
