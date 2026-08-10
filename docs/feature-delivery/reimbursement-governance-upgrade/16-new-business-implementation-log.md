# 新增业务功能：实施日志

## 2026-08-10

- 当前里程碑：NB-01 用户资料。
- 已完成：需求确认、现状调研、技术方案和任务拆解。
- 下一步：实现领域模型、资料保护服务、API 门禁和资料页面。

### NB-01 至 NB-05 实施完成

- 用户资料：增加个人姓名与银行卡；银行卡使用 ASP.NET Core Data Protection 加密保存；登录响应和后端中间件实施资料硬门禁；管理员用户目录显示完整卡号并记录查看/复制审计；发放前通过专用接口再次读取并核对收款资料。
- 自动餐补：差旅版本自动生成唯一餐补记录，按行程自然日含首尾计算；差旅提交、批准、驳回与餐补状态联动；餐补单独审核并输入每日金额，金额最多两位小数；报销和餐补分别确认发放。
- 管理队列：增加 `workQueue=approval|payout`，服务端按差旅或餐补状态合并筛选，避免分页后前端拼接漏单；列表返回餐补并发令牌。
- 周报：实现用户按项目/周创建、查询、编辑，管理员查询和编辑任意周报；作者、项目、周唯一，管理员编辑不改变作者。
- 月度导出：按项目与 `SubmittedAt` 生成四工作表 XLSX，日期留空使用上月 10 日至本月 10 日；不导出完整银行卡。
- 数据库：生成非破坏性迁移 `AddMealAllowanceWeeklyReportsAndBankProfile`，并在独立 PostgreSQL 容器成功执行全部迁移。

### NB-06 验收完成

- 后端：`dotnet test TravelReimbursement.slnx -c Release --no-restore -p:BaseOutputPath=D:\Code\chuchai\.tmp\validation-bin\`，21/21 通过，0 失败。
- 前端类型检查：`npx.cmd vue-tsc -p tsconfig.app.json --noEmit --incremental false` 通过。
- 前端生产构建：`npx.cmd vite build --configLoader runner --outDir <temp> --emptyOutDir` 通过；仅保留既有大分块提示。
- 静态检查：`git diff --check` 通过，仅有 Git 行尾转换提示。
- 隔离 API：资料未补全访问业务接口返回 409；3 天餐补按 100.00 元/天计算为 300.00 元；100.001 元/天被拒绝；差旅和餐补按顺序分别审核、分别发放；差旅驳回联动餐补驳回且原因一致。
- 周报：重复创建返回 409；管理员编辑后作者保持不变，最后编辑人更新为管理员。
- XLSX：首次验收发现 Npgsql 不接受 `+08:00` 的 `timestamp with time zone` 参数；修复为中国时区边界转 UTC 后复验 HTTP 200，4 个工作表完整，包含餐补金额且不含完整银行卡。
- 浏览器：桌面和 390px 移动端完成申请餐补预览、管理列表、月度导出表单和详情账本验收；修复移动端金额末位换行；无横向溢出，控制台无错误或警告。

### 剩余部署事项

- 现有业务数据库尚未执行迁移；部署前必须备份数据库与 Data Protection 密钥目录，再在目标环境执行迁移和上线验收。
- Data Protection 密钥必须持久化并纳入备份，否则历史银行卡密文无法解密。
