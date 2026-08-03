# 差旅账

差旅报销 Web：支持注册审批、管理员项目管理、项目化报销、不可变版本、私有凭证、审批、发放确认和汇总审计。

## 本地开发

1. 启动 PostgreSQL，或使用 Docker Compose。
2. 在当前进程设置 `ConnectionStrings__DefaultConnection`、`Jwt__Key`、`BootstrapAdmin__PhoneNumber`、`BootstrapAdmin__DisplayName`、`BootstrapAdmin__Password`。
3. 启动 API：`dotnet run --project backend/TravelReimbursement.Api --launch-profile http`。
4. 启动前端：`cd frontend; npm.cmd run dev`。

前端默认访问 `/api`，Vite 开发服务器会代理至本地 API；容器环境由 Nginx 反向代理。

附件只保存在本地私有目录，不再依赖 MinIO。开发环境默认使用仓库根目录下的 `private-uploads`；可通过 `FileStorage__LocalPath` 覆盖。该目录不由 Web 服务器直接公开，上传和下载始终经过 API 鉴权。

## 容器启动

基于 `.env.example` 准备仅用于本地/部署环境的 `.env`，填写所有密码和 JWT 密钥后执行：

```powershell
docker compose up --build -d
```

页面地址为 `http://localhost:8088`，API 为 `http://localhost:8080`。Compose 将附件持久化到 `attachments_data` 卷，API 容器内路径为 `/data/private-uploads`。应用启动时自动执行 `Data/Migrations` 中的迁移，并根据环境变量创建首个管理员账号。首位管理员同时拥有 `Applicant` 和 `Administrator`，系统不再使用独立审核人角色。不要将 `.env` 或任何真实凭据提交到仓库。

Linux 生产服务器部署请使用 [Linux 服务器部署手册](docs/linux-deployment-guide.md)。该手册包含生产端口收敛、HTTPS、10MB 附件代理限制、首次部署、备份恢复、升级和故障处理，不建议直接按本地 Compose 端口配置暴露生产服务。

## 核心业务规则

- 管理员创建并启停项目，所有用户均可选择启用项目；停用项目不能用于新建报销。
- 报销以“项目 - 申请人 - 报销主记录 - 不可变版本”组织。草稿、待审批和驳回状态可保存为新版本，旧版本只读并标记作废。
- 草稿、待审批和驳回报销可以删除/撤回，实际保存为 `Cancelled` 审计状态；已批准报销不可删除。
- 管理员批准后，发放状态进入 `Pending`；确认发放后进入 `Paid`，系统不提供撤销发放接口。
- 申请人列表支持项目和报销状态筛选；管理员列表支持项目、申请人、报销状态、发放状态和日期筛选，并提供按项目或申请人汇总。

## 验证

```powershell
dotnet build TravelReimbursement.slnx --no-restore
dotnet test TravelReimbursement.slnx --no-build --no-restore
Set-Location frontend; npm.cmd run build
```

## 已授权的破坏式重建流程

本次治理升级采用空库基线 `InitialGovernanceRebuild`，不迁移旧用户、旧报销或旧附件。数据库和本地附件目录必须在同一维护窗口处理。

1. 停止 `api` 和 `web`，阻断写入。
2. 通过 `docker compose config` 和部署环境配置，只读展示并人工核对 PostgreSQL 主机、端口、数据库、用户、Compose 项目名，以及 `FileStorage:LocalPath` 的最终绝对路径；日志中不得打印密码或 JWT 密钥。
3. 分别记录数据库业务表行数、`AttachmentAssets` 数量和本地附件文件数量。确认目录属于本项目且不是工作区根目录或共享目录。
4. 只对已核对的数据库和附件目录执行清理，不删除工作区、未知目录、整个 Docker 数据目录或其他 Compose 项目的卷。
5. 应用 `Data/Migrations` 中唯一的新基线迁移，启动 API，确认仅存在 `Applicant`、`Administrator` 两个业务角色，手机号管理员可以登录。
6. 创建首个启用项目，再执行注册审批、附件、草稿、版本冲突、审批、发放和汇总的完整验收。

破坏式清理完成后旧数据不可恢复；如果任一目标无法精确确认，应保持服务停止并中止清理。

## 备份与回滚边界

- 数据库备份应在部署前执行，并写入受控备份目录；例如可通过 `docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"'` 导出。恢复必须指向明确的目标数据库，先停掉 `api` 服务并完成备份校验，不能用未知目标执行清库或覆盖操作。
- 附件保存在开发环境 `private-uploads` 目录或 Compose `attachments_data` 持久卷中，必须与 PostgreSQL 备份同批次保存。恢复时先恢复数据库，再恢复同批次附件目录/卷，最后启动 `api`；不要混用不同时间点的数据。
- 本次空库切换完成后不支持恢复旧业务数据。后续常规发布若需回滚数据，应恢复同批次 PostgreSQL 和附件备份，不能只恢复其中一侧。
- `.env` 仅存放在部署主机，不得提交到仓库、构建产物或日志中。
