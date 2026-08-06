# 用户中心与申请人筛选修复：技术设计

## API 合同

### 管理员用户目录

```text
GET /api/admin/users?isActive=true|false&keyword=&page=&pageSize=
```

响应使用现有 `PagedResult<T>`：

```json
{
  "items": [
    {
      "id": "guid",
      "displayName": "张三",
      "phoneNumber": "13800000000",
      "isActive": true,
      "roles": ["Applicant"]
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

### 管理员账号启停

```text
POST /api/admin/users/{id}/enable
POST /api/admin/users/{id}/disable
```

成功响应返回 `{ id, isActive }`。禁止停用当前登录账号或最后一个启用管理员时返回 `409`，错误码分别为 `USER_SELF_DISABLE`、`LAST_ADMIN_DISABLE`。

### 申请人目录

```text
GET /api/admin/applicants?keyword=&page=&pageSize=
```

响应使用现有 `PagedResult<T>`，条目只包含：`id`、`displayName`、`phoneNumber`。仅返回启用且拥有 `Applicant` 角色的正式账户。

## 后端实现

1. 在现有 `admin` 路由组中加入用户列表、申请人目录和用户启停端点。
2. 用户列表先基于 `db.Users` 做关键字/状态/角色过滤和分页，再批量查询 `UserRoles + Roles` 组装角色列表。
3. 申请人目录以 `db.UserRoles` 与 `db.Roles` 过滤 `Applicant`，再关联 `db.Users` 查询启用账户。
4. 启停操作更新 `AppUser.IsActive`，追加 `UserEnabled` / `UserDisabled` 审计，并保存。
5. `AdminClaimsView.vue` 不再用申请人分组汇总填充下拉；分组汇总接口保持不变。

## 前端实现

- `api.ts` 增加 `AdminUser`、`ApplicantOption` 类型及 `listUsers`、`setUserActive`、`listApplicants` 方法。
- 新增 `AdminUsersView.vue`，支持状态/关键字筛选、分页、启停确认、桌面表格和移动卡片。
- 路由和导航新增 `/admin/users` 与“用户中心”。
- 报销管理申请人下拉使用远程关键字加载 `listApplicants`，默认加载第一页；选中值仍以 `applicantId` 传给原列表/汇总接口。

## 错误处理与兼容性

- 复用 `api.message`，补充用户自停用、最后管理员保护提示。
- 不修改既有注册、登录、注册审批、报销列表 API 合同。
- 不需要 EF Migration，不需要新增配置，不需要新增依赖。

## 回滚策略

- 代码回滚只涉及新增端点、页面、路由和 API 封装；数据库无需回滚。
- 若前端远程下拉异常，可回退到 `listApplicants({ page: 1, pageSize: 100 })` 的静态首批加载，后端目录合同保持不变。

## 验证命令

```powershell
dotnet build TravelReimbursement.slnx -c Release --no-restore -p:BaseOutputPath=D:\Code\chuchai\.tmp\validation-bin\
dotnet test TravelReimbursement.slnx -c Release --no-build --no-restore -p:BaseOutputPath=D:\Code\chuchai\.tmp\validation-bin\
Set-Location frontend
npm.cmd run build
```
