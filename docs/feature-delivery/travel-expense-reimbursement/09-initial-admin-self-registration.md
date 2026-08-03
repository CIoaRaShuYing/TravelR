# 首位管理员自助注册

## 已确认边界

- 当数据库中不存在 `Administrator` 角色用户且未通过 Bootstrap 配置初始化时，第一位完成注册的用户成为首位管理员；该初始化流程优先于 `Open`、`ApprovalRequired`、`Closed` 三态策略。
- 首位管理员同时获得 `Applicant`、`Reviewer` 和 `Administrator` 三个角色。
- 首位管理员创建后，后续注册继续按 `Open`、`ApprovalRequired`、`Closed` 三态策略执行。
- 显式配置 `BootstrapAdmin` 的部署仍可在启动期创建管理员；已有管理员时不会重复创建 Bootstrap 管理员。
- 当前仅重置 `travel_reimbursement_local` 隔离库，不触碰 `travel_reimbursement`。

## 验收标准

1. 空库启动后，首位注册用户可直接登录，登录响应的顶层 `roles` 包含三个角色。
2. 首位用户可访问审核与管理设置接口。
3. 首位管理员创建后，第二位用户在开放注册模式下仅获得 `Applicant` 角色。
4. 已有管理员时，关闭和审核注册模式保持既有接口行为。
5. 公开注册设置在无管理员时返回 `initialAdministratorRegistration: true`，注册页醒目提示即将创建首位管理员；成功后自动切换到登录界面并带入手机号。

## 实施记录

- 状态：已实现并完成隔离环境验证。
- 后端注册接口通过串行化事务检查管理员是否存在，并在空库时原子地创建首位管理员和角色审计记录。
- 启动期 Bootstrap 初始化仅在未存在管理员且显式提供配置时执行。
- 公开注册设置返回 `initialAdministratorRegistration`；注册页据此展示醒目的首位管理员权限提示，即使三态策略为 `Closed` 也保留初始化入口。
- 立即注册成功响应包含 `registrationCompleted: true`；前端自动切换回登录表单、带入手机号并清空密码。待审核申请返回 `registrationCompleted: false`，不伪装为注册成功。
- 已验证后端构建零警告、测试 4/4 通过、前端生产构建通过；唯一命名的临时隔离数据库在 `Closed` 模式下完成首位注册和登录，角色包含 `Applicant`、`Reviewer`、`Administrator`，验证后临时数据库与角色均已删除。
- 当前 `travel_reimbursement_local` 已存在管理员，因此实际页面按规则展示普通注册状态；本轮未删除、降权或修改该现有账号。
