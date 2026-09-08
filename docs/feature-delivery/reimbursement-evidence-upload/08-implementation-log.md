# 报销凭证分类与拖拽上传：实施日志

## 2026-09-08

- 当前里程碑：M1 后端分类与兼容迁移。
- 完成任务：需求确认；技术分析；技术方案；任务拆解。
- 变更文件：`00-intake.md` 至 `08-implementation-log.md`。
- 验证命令：`git diff --check`（需求确认前文档通过）。
- 验证结果：生产代码尚未修改；需求边界已确认。
- 发现的缺口：无。
- 已回写文档：历史附件改为发票；ZIP 保持 `报销凭证/`。
- 下一步：实现 `AttachmentPurpose`、迁移和上传/详情 API。

### M1 完成记录

- 完成任务：新增 `AttachmentPurpose`；`AttachmentAssets.Purpose` 字符串映射；上传查询参数和响应；详情用途投影；历史数据默认 `Invoice` 的 EF migration；两类凭证均满足提交规则的测试。
- 变更文件：`Entities.cs`、`AppDbContext.cs`、`Program.cs`、`ClaimSubmissionValidatorTests.cs`、`20260908023855_AddAttachmentPurpose*`、模型快照。
- 验证命令：`dotnet test TravelReimbursement.slnx -c Release --no-restore -p:BaseOutputPath=D:\Code\chuchai\.tmp\evidence-upload-m1\`。
- 验证结果：41/41 通过。
- 环境说明：沙箱内因 `obj`/`.tmp` 写权限失败，经授权后在同一仓库隔离输出目录通过；不是代码错误。
- 下一里程碑：M2 前端双拖拽区与详情展示。

### M2 完成记录

- 完成任务：前端附件类型和上传查询参数；每条费用的发票/支付记录双拖拽区；多文件上传计数状态；分类文件列表；详情页分类展示；桌面双列与移动单列布局。
- 变更文件：`frontend/src/api.ts`、`ClaimEditorDialog.vue`、`ClaimDetailDrawer.vue`、`style.css`。
- 验证命令：在 `frontend` 运行 `npm.cmd run build`。
- 验证结果：`vue-tsc -b` 与 Vite production build 通过；1625 modules transformed。
- 环境说明：首次沙箱运行因 `node_modules/.tmp/*.tsbuildinfo` 写权限返回 `TS5033/EPERM`，经授权后同一命令通过。
- 设计复核：两个区域的颜色差异只表达凭证类别；未引入字体、动效或无业务意义装饰；移动端降为单列。
- 下一里程碑：M3 全量测试、差异检查与验收回写。

### M3 完成记录

- 完成任务：全量后端测试；前端生产构建；迁移 SQL 和模型差异检查；源码差异检查；需求与验收状态回写。
- 验证命令与结果：
  - `dotnet test TravelReimbursement.slnx -c Release --no-restore -p:BaseOutputPath=D:\Code\chuchai\.tmp\evidence-upload-final2\`：最终复跑 41/41 通过。
  - `npm.cmd run build`：`vue-tsc -b` 和 Vite build 通过，1625 modules transformed。
  - `dotnet ef migrations script 20260903125204_UnifyMeetingRecordItems 20260908023855_AddAttachmentPurpose ...`：生成 `ALTER TABLE ... DEFAULT 'Invoice'`。
  - `dotnet ef migrations has-pending-model-changes ... --no-build`：`No changes have been made to the model since the last migration.`
  - `git diff --check`：通过，仅有仓库既有 LF/CRLF 提示。
- 失败与修正：首次迁移脚本使用 `--no-build` 读取了生成迁移前的旧 DLL，返回 `The migration '20260908023855_AddAttachmentPurpose' was not found.`；重新构建后脚本生成成功。
- 未执行：未连接真实数据库应用迁移；未使用真实申请人/管理员账号完成浏览器点击、拖拽、保存重开和详情验收；未验证部署环境。
- 当前状态：实现和本地自动化验证完成，等待目标环境迁移与浏览器验收。
- 清理：已删除本次创建的 `evidence-upload-m1`、`evidence-upload-final`、`evidence-upload-final2` 隔离构建目录。
