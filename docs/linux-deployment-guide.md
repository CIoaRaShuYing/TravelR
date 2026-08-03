# 差旅账 Linux 服务器部署手册

最后更新：2026-08-03

## 1. 适用范围

本文用于将当前差旅报销系统部署到单台 Linux 服务器，推荐方式为：

- Docker Engine + Docker Compose Plugin；
- PostgreSQL、ASP.NET Core API、Vue/Nginx 前端分别运行在容器中；
- PostgreSQL 数据和附件使用两个独立 Docker 命名卷持久化；
- 公网只开放宿主机 Nginx 的 `80/443`，数据库和 API 不直接暴露；
- 数据库与附件必须同批次备份和恢复。

本文以 Ubuntu Server 22.04/24.04/26.04 LTS 为主要示例。其他 Linux 发行版安装好 Docker Engine 和 Compose Plugin 后，项目部署命令相同。

## 2. 系统结构

```text
用户浏览器
   |
   | HTTPS :443
   v
宿主机 Nginx
   |
   | http://127.0.0.1:8088
   v
Web 容器（Nginx + Vue）
   |
   | /api -> http://api:8080
   v
API 容器（ASP.NET Core 8）
   |                         |
   | PostgreSQL             | /data/private-uploads
   v                         v
PostgreSQL 容器          attachments_data 卷
   |
   v
postgres_data 卷
```

当前应用启动时会自动执行 EF Core 数据库迁移，并在数据库中不存在管理员时，根据 `.env` 创建首位管理员。

## 3. 上线前必须确认的事项

### 3.1 不要直接使用开发 Compose 暴露端口

仓库根目录现有 `docker-compose.yml` 会将以下端口发布到宿主机：

- PostgreSQL：`5432`；
- API：`8080`；
- Web：`8088`。

生产环境不应直接公开 PostgreSQL 和 API。本文后续提供完整的 `docker-compose.production.yml`，只把 Web 绑定到 `127.0.0.1:8088`。

### 3.2 前端 Nginx 必须放宽附件上传大小

系统允许单个 JPG、PNG、PDF 凭证最大 10MB。Nginx 默认请求体限制可能导致较大附件返回 `413 Request Entity Too Large`。

部署前必须在项目的 `frontend/nginx.conf` 中设置：

```nginx
client_max_body_size 12m;
```

如果宿主机前面还有一层 Nginx、Caddy、网关或云负载均衡，也必须将该层请求体限制设置为至少 12MB。

### 3.3 JWT 密钥必须长期保持不变

`JWT_KEY` 用于签发和验证登录令牌。只要数据库不变，正常升级和容器重启都必须继续使用原密钥。

更换 `JWT_KEY` 会让所有现有登录会话立即失效，用户需要重新登录。

### 3.4 初始化管理员配置只在首次创建时生效

只要数据库中已经存在管理员，修改 `.env` 中的 `BOOTSTRAP_ADMIN_*` 不会修改现有管理员手机号或密码。

因此首次部署前必须保存好管理员手机号和密码。当前系统没有管理员自助修改或找回密码页面。

### 3.5 数据库和附件必须成对备份

附件元数据保存在 PostgreSQL，文件实体保存在 `chuchai_attachments_data` 卷。只恢复数据库或只恢复附件都会造成数据不一致。

## 4. 服务器准备

### 4.1 建议配置

小规模内部使用可从以下配置起步：

- CPU：2 核；
- 内存：4GB；
- 系统盘：40GB 以上；
- 独立备份空间：至少为数据库和附件实际占用量的 2 倍；
- 操作系统：Ubuntu Server 22.04、24.04 或 26.04 LTS，64 位。

实际容量主要取决于附件数量和保留时间，应持续监控磁盘占用。

### 4.2 安装基础工具

```bash
sudo apt update
sudo apt install -y git curl openssl nginx
```

### 4.3 安装 Docker

生产环境建议通过 Docker 官方 APT 仓库安装 Docker Engine、Buildx 和 Compose Plugin，不建议使用便捷脚本直接部署生产服务器。

安装完成后检查：

```bash
sudo systemctl enable --now docker
sudo docker version
sudo docker compose version
```

如果部署用户需要不带 `sudo` 执行 Docker，可按 Docker 官方 Linux 安装后配置加入 `docker` 组。注意：`docker` 组权限接近 root 权限，只应授予受信任的运维账号。

## 5. 上传项目代码

推荐部署目录：`/opt/chuchai`。

使用 Git：

```bash
sudo mkdir -p /opt/chuchai
sudo chown -R "$USER":"$USER" /opt/chuchai
git clone <你的仓库地址> /opt/chuchai
cd /opt/chuchai
```

如果没有 Git 仓库，也可以将项目压缩包上传后解压到 `/opt/chuchai`。必须确认以下文件存在：

```bash
test -f /opt/chuchai/docker-compose.yml
test -f /opt/chuchai/backend/TravelReimbursement.Api/Dockerfile
test -f /opt/chuchai/frontend/Dockerfile
test -f /opt/chuchai/frontend/nginx.conf
```

记录本次部署代码版本：

```bash
cd /opt/chuchai
git rev-parse HEAD 2>/dev/null || true
```

## 6. 配置容器内 Nginx

将 `frontend/nginx.conf` 调整为以下内容：

```nginx
server {
  listen 80;
  server_name _;

  root /usr/share/nginx/html;
  index index.html;

  client_max_body_size 12m;

  location = /health {
    proxy_pass http://api:8080/health;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_connect_timeout 10s;
    proxy_read_timeout 30s;
  }

  location /api/ {
    proxy_pass http://api:8080/api/;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_connect_timeout 10s;
    proxy_read_timeout 120s;
    proxy_send_timeout 120s;
  }

  location / {
    try_files $uri $uri/ /index.html;
  }
}
```

其中 `/api/` 继续代理到 API 容器，附件不会由 Nginx 静态公开，上传、预览和下载仍经过 API 鉴权。

## 7. 准备生产环境变量

进入项目目录并复制模板：

```bash
cd /opt/chuchai
umask 077
cp .env.example .env
```

生成随机值：

```bash
openssl rand -base64 36
openssl rand -base64 48
printf '%sA9\n' "$(openssl rand -base64 24 | tr -d '\n')"
```

分别用于 PostgreSQL 密码、JWT 密钥和初始管理员密码。然后编辑 `.env`：

```bash
nano .env
```

示例：

```dotenv
POSTGRES_DB=travel_reimbursement
POSTGRES_USER=travel_app
POSTGRES_PASSWORD=<长随机数据库密码>

JWT_KEY=<固定且至少32字符的随机密钥>

BOOTSTRAP_ADMIN_PHONE_NUMBER=13800000000
BOOTSTRAP_ADMIN_DISPLAY_NAME=系统管理员
BOOTSTRAP_ADMIN_PASSWORD=<初始管理员强密码>
```

要求：

- 手机号必须是有效的 11 位中国大陆手机号；
- 管理员密码至少 8 位并包含数字，生产环境建议使用 16 位以上随机密码；
- `.env` 必须使用 UTF-8；
- 不要将 `.env` 提交到 Git、上传到制品仓库或输出到日志；
- 不要在服务已运行后随意重新生成 `JWT_KEY`；
- PostgreSQL 数据卷已经初始化后，只修改 `.env` 的 `POSTGRES_PASSWORD` 不等于自动修改数据库内部账号密码。

限制文件权限：

```bash
chmod 600 /opt/chuchai/.env
```

## 8. 创建生产 Compose 文件

在 `/opt/chuchai/docker-compose.production.yml` 创建以下内容：

```yaml
name: chuchai

services:
  postgres:
    image: postgres:16-alpine
    restart: unless-stopped
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-travel_reimbursement}
      POSTGRES_USER: ${POSTGRES_USER:-travel_app}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD in .env}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $$POSTGRES_USER -d $$POSTGRES_DB"]
      interval: 5s
      timeout: 3s
      retries: 20
    networks:
      - internal

  api:
    build:
      context: ./backend/TravelReimbursement.Api
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=${POSTGRES_DB:-travel_reimbursement};Username=${POSTGRES_USER:-travel_app};Password=${POSTGRES_PASSWORD}
      Jwt__Issuer: TravelReimbursement
      Jwt__Audience: TravelReimbursement.Web
      Jwt__Key: ${JWT_KEY:?Set JWT_KEY in .env}
      BootstrapAdmin__PhoneNumber: ${BOOTSTRAP_ADMIN_PHONE_NUMBER:?Set BOOTSTRAP_ADMIN_PHONE_NUMBER in .env}
      BootstrapAdmin__DisplayName: ${BOOTSTRAP_ADMIN_DISPLAY_NAME:-系统管理员}
      BootstrapAdmin__Password: ${BOOTSTRAP_ADMIN_PASSWORD:?Set BOOTSTRAP_ADMIN_PASSWORD in .env}
      FileStorage__LocalPath: /data/private-uploads
      FileStorage__StagedRetentionHours: 24
    volumes:
      - attachments_data:/data/private-uploads
    expose:
      - "8080"
    networks:
      - internal

  web:
    build:
      context: ./frontend
    restart: unless-stopped
    depends_on:
      - api
    ports:
      - "127.0.0.1:8088:80"
    networks:
      - internal

networks:
  internal:
    name: chuchai_internal
    internal: true

volumes:
  postgres_data:
    name: chuchai_postgres_data
  attachments_data:
    name: chuchai_attachments_data
```

这个生产文件具备以下特征：

- PostgreSQL 不发布宿主机端口；
- API 只在 Docker 内部网络暴露；
- Web 只监听宿主机回环地址 `127.0.0.1:8088`；
- 外部用户必须经过宿主机 Nginx；
- 三个服务均启用自动重启；
- 数据卷名称固定，便于备份、恢复和核对。

如果暂时没有域名和宿主机反向代理，需要直接通过 `服务器IP:8088` 访问，可将 Web 端口临时改为：

```yaml
ports:
  - "8088:80"
```

此方式会公开 8088 端口，只适合受控内网或临时验收，不建议作为长期公网部署方式。

## 9. 检查并首次启动

定义后续命令使用的 Compose 参数：

```bash
cd /opt/chuchai
compose_cmd='sudo docker compose --env-file .env -f docker-compose.production.yml'
```

先进行只读配置校验。不要执行会把完整配置和密钥打印到终端的 `docker compose config`：

```bash
$compose_cmd config --quiet
```

构建镜像：

```bash
$compose_cmd build --pull api web
```

启动服务：

```bash
$compose_cmd up -d
```

检查状态：

```bash
$compose_cmd ps
$compose_cmd logs --tail=200 postgres
$compose_cmd logs --tail=200 api
$compose_cmd logs --tail=100 web
```

正常启动时，API 日志应包含：

- 数据库迁移成功或数据库已是最新版本；
- `Now listening on: http://[::]:8080` 或等价监听信息；
- 不出现“本地附件目录不可写”；
- 不出现数据库认证失败。

检查 Docker 卷：

```bash
sudo docker volume inspect chuchai_postgres_data
sudo docker volume inspect chuchai_attachments_data
```

禁止使用：

```bash
docker compose down -v
docker volume prune
```

这些命令可能永久删除数据库卷或附件卷。

## 10. 配置宿主机 Nginx 和 HTTPS

### 10.1 HTTP 反向代理

创建 `/etc/nginx/sites-available/chuchai.conf`：

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name expense.example.com;

    client_max_body_size 12m;

    location / {
        proxy_pass http://127.0.0.1:8088;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_connect_timeout 10s;
        proxy_read_timeout 120s;
        proxy_send_timeout 120s;
    }
}
```

替换 `expense.example.com` 为真实域名，然后启用：

```bash
sudo ln -s /etc/nginx/sites-available/chuchai.conf /etc/nginx/sites-enabled/chuchai.conf
sudo nginx -t
sudo systemctl reload nginx
```

如果同名链接已存在，不要重复创建，直接检查配置并重载。

### 10.2 HTTPS

生产公网环境必须配置有效 HTTPS 证书。可以使用组织已有证书、云证书服务或 Certbot。

证书配置完成后，建议：

- `80` 端口只用于跳转到 HTTPS；
- `443` 反向代理到 `127.0.0.1:8088`；
- 外层 Nginx 继续保留 `client_max_body_size 12m`；
- 定期验证证书自动续期。

### 10.3 防火墙和安全组

公网安全组只应开放：

- `22/tcp`：SSH，最好限制为运维来源 IP；
- `80/tcp`：HTTP/证书签发和 HTTPS 跳转；
- `443/tcp`：HTTPS。

不应开放：

- `5432/tcp`；
- `8080/tcp`；
- `8088/tcp`，除非明确采用直连方式。

Docker 发布的容器端口可能绕过部分 UFW/firewalld 规则，因此生产 Compose 必须从源头停止发布 PostgreSQL和 API 端口，不能只依赖 UFW 拦截。

## 11. 上线验收

### 11.1 命令行检查

在服务器执行：

```bash
curl -fsS http://127.0.0.1:8088/health
curl -fsS http://127.0.0.1:8088/api/registration-settings
curl -I http://127.0.0.1:8088/
```

预期结果：

- `/health` 返回 `200` 和 `{"status":"ok"}`；
- `/api/registration-settings` 返回注册策略 JSON；
- `/` 返回前端页面。

如果已经配置域名和 HTTPS：

```bash
curl -fsS https://expense.example.com/health
curl -I https://expense.example.com/
```

### 11.2 端口检查

```bash
sudo ss -lntp | grep -E ':(80|443|5432|8080|8088)\b' || true
```

推荐状态：

- `80/443` 由宿主机 Nginx 对外监听；
- `8088` 只监听 `127.0.0.1`；
- 宿主机不监听 `5432` 和 `8080`。

### 11.3 浏览器业务验收

使用 `.env` 中的管理员手机号和密码登录，然后按顺序检查：

1. 管理员可以登录，刷新页面后仍保持登录；
2. 创建一个启用项目；
3. 注册一个普通用户并由管理员批准；
4. 普通用户能选择管理员创建的项目；
5. 新增普通报销时只出现空类别“费用 1”；
6. 新增差旅行程时默认出现去程和回程两项；
7. 上传 JPG、PNG、PDF，确认能够在线预览和下载；
8. 保存草稿、编辑并保存，确认产生新版本；
9. 提交报销，管理员批准并确认发放；
10. 管理员全部报销汇总和项目/用户筛选正常。

再执行一次非破坏性重启：

```bash
$compose_cmd restart api web
```

重启后重新检查健康接口和页面。只要 `.env` 中 `JWT_KEY` 没有变化，已有未过期登录令牌仍可继续使用。

## 12. 日常运维命令

查看服务状态：

```bash
cd /opt/chuchai
compose_cmd='sudo docker compose --env-file .env -f docker-compose.production.yml'
$compose_cmd ps
```

查看实时日志：

```bash
$compose_cmd logs -f --tail=200 api
$compose_cmd logs -f --tail=200 web
$compose_cmd logs -f --tail=200 postgres
```

查看资源占用：

```bash
sudo docker stats
sudo docker system df
df -h
```

重启单个服务：

```bash
$compose_cmd restart api
$compose_cmd restart web
```

停止应用但保留数据卷：

```bash
$compose_cmd stop web api postgres
```

重新启动：

```bash
$compose_cmd up -d
```

## 13. 备份

### 13.1 备份原则

每次备份至少包含：

- PostgreSQL 逻辑备份；
- `chuchai_attachments_data` 卷；
- 当前部署代码版本号；
- 安全保存的 `.env` 或等价密钥记录。

为了保证数据库和附件处于同一时间点，推荐在维护窗口暂停 Web 和 API 写入后执行备份。

### 13.2 创建备份目录

```bash
backup_dir="/srv/backups/chuchai/$(date +%Y%m%d-%H%M%S)"
sudo install -d -m 0700 -o "$USER" -g "$USER" "$backup_dir"
```

### 13.3 暂停写入

```bash
cd /opt/chuchai
compose_cmd='sudo docker compose --env-file .env -f docker-compose.production.yml'
$compose_cmd stop web api
```

### 13.4 备份 PostgreSQL

```bash
$compose_cmd exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$backup_dir/postgres.dump"
```

### 13.5 备份附件卷

```bash
sudo docker run --rm \
  --mount type=volume,src=chuchai_attachments_data,dst=/data,readonly \
  --mount type=bind,src="$backup_dir",dst=/backup \
  alpine:3.20 \
  sh -c 'cd /data && tar czf /backup/attachments.tar.gz .'
```

### 13.6 保存版本和校验信息

```bash
git rev-parse HEAD > "$backup_dir/git-commit.txt" 2>/dev/null || true
(cd "$backup_dir" && sha256sum postgres.dump attachments.tar.gz > SHA256SUMS)
ls -lh "$backup_dir"
```

`.env` 包含数据库密码、JWT 密钥和管理员初始化密码，不建议直接复制到普通备份目录。应保存在受控密码库、加密备份或独立密钥管理系统中。

### 13.7 恢复服务

```bash
$compose_cmd up -d api web
curl -fsS http://127.0.0.1:8088/health
```

将备份复制到其他服务器或对象存储，并设置合理的保留策略。只保存在本机磁盘上的备份不能防范服务器磁盘损坏。

## 14. 恢复

恢复会覆盖目标数据库和附件卷，属于破坏性操作。执行前必须确认：

- 当前目录是 `/opt/chuchai`；
- Compose 项目是 `chuchai`；
- 目标数据库名与 `.env` 一致；
- 目标附件卷是 `chuchai_attachments_data`；
- `postgres.dump` 与 `attachments.tar.gz` 来自同一批次；
- 已为当前状态额外创建一份备份。

### 14.1 停止写入

```bash
cd /opt/chuchai
compose_cmd='sudo docker compose --env-file .env -f docker-compose.production.yml'
$compose_cmd stop web api
```

### 14.2 校验备份

```bash
backup_dir="/srv/backups/chuchai/20260803-120000"
(cd "$backup_dir" && sha256sum -c SHA256SUMS)
```

将示例时间目录替换为实际要恢复的备份目录。

### 14.3 重建并恢复数据库

```bash
$compose_cmd exec -T postgres sh -c 'dropdb -U "$POSTGRES_USER" --if-exists --force "$POSTGRES_DB"'
$compose_cmd exec -T postgres sh -c 'createdb -U "$POSTGRES_USER" -O "$POSTGRES_USER" "$POSTGRES_DB"'

$compose_cmd exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --clean --if-exists' \
  < "$backup_dir/postgres.dump"
```

如果不熟悉 shell 引号，建议先在测试服务器演练恢复，不要直接在生产环境尝试修改命令。

### 14.4 恢复附件卷

先只读核对目标卷：

```bash
sudo docker volume inspect chuchai_attachments_data
```

确认无误后清空并恢复该精确卷：

```bash
sudo docker run --rm \
  --mount type=volume,src=chuchai_attachments_data,dst=/data \
  --mount type=bind,src="$backup_dir",dst=/backup,readonly \
  alpine:3.20 \
  sh -c 'find /data -mindepth 1 -maxdepth 1 -exec rm -rf -- {} + && tar xzf /backup/attachments.tar.gz -C /data'
```

不要把 `/var/lib/docker`、项目目录或未知宿主机目录作为清理目标。

### 14.5 启动和验收

```bash
$compose_cmd up -d api web
$compose_cmd logs --tail=200 api
curl -fsS http://127.0.0.1:8088/health
curl -fsS http://127.0.0.1:8088/api/registration-settings
```

然后使用浏览器检查登录、历史报销和附件预览。若数据库记录存在但附件预览返回 404，说明数据库和附件未使用同一批次恢复。

## 15. 版本升级

### 15.1 升级前

1. 阅读版本变更说明；
2. 完成数据库和附件同批次备份；
3. 记录当前 Git commit 和镜像 ID；
4. 预留维护窗口。

```bash
cd /opt/chuchai
git rev-parse HEAD
sudo docker images --digests
```

### 15.2 获取新版本并构建

```bash
cd /opt/chuchai
git fetch --all --tags
git checkout <已确认的版本标签或提交>

compose_cmd='sudo docker compose --env-file .env -f docker-compose.production.yml'
$compose_cmd config --quiet
$compose_cmd build --pull api web
$compose_cmd up -d
```

API 启动时会自动执行新增数据库迁移。

### 15.3 升级后

```bash
$compose_cmd ps
$compose_cmd logs --tail=200 api
curl -fsS http://127.0.0.1:8088/health
curl -fsS http://127.0.0.1:8088/api/registration-settings
```

再执行浏览器业务验收，至少覆盖登录、项目列表、我的报销、附件预览和管理员报销列表。

### 15.4 回滚边界

- 仅前端显示问题且 API/数据库未变更时，可以切回旧代码并重建 Web；
- 如果新版本执行了数据库迁移，不应直接让旧 API 长期连接新数据库；
- 数据回滚必须恢复升级前同一批次的 PostgreSQL 和附件备份；
- 不要只恢复数据库或只恢复附件。

## 16. 常见故障

### 16.1 页面显示 502

检查：

```bash
$compose_cmd ps
$compose_cmd logs --tail=200 api
$compose_cmd logs --tail=100 web
curl -fsS http://127.0.0.1:8088/health
```

常见原因：

- API 容器启动失败；
- PostgreSQL 尚未健康；
- 数据库用户名或密码不一致；
- 数据库迁移失败；
- API 无法写入附件卷；
- Web 容器无法解析 Docker 内部服务名 `api`。

### 16.2 上传大于约 1MB 的附件返回 413

确认以下两层都设置了 `client_max_body_size 12m`：

- `frontend/nginx.conf`；
- 宿主机 Nginx 或其他外部网关。

修改后必须重建 Web 镜像：

```bash
$compose_cmd build web
$compose_cmd up -d --no-deps web
```

### 16.3 API 提示附件目录不可写

```bash
sudo docker volume inspect chuchai_attachments_data
$compose_cmd logs --tail=200 api
df -h
```

检查附件卷是否挂载到 `/data/private-uploads`、磁盘是否已满。不要通过给整个 Docker 数据目录设置 `777` 解决权限问题。

### 16.4 数据库认证失败

检查 `.env`、容器状态和 API 日志：

```bash
$compose_cmd ps
$compose_cmd logs --tail=200 postgres
$compose_cmd logs --tail=200 api
```

如果 PostgreSQL 卷已经初始化，修改 `.env` 的 `POSTGRES_PASSWORD` 不会自动修改卷内已有数据库用户密码。应恢复原密码，或在明确维护窗口通过 PostgreSQL 管理命令同步修改数据库角色密码。

### 16.5 重启后所有用户需要重新登录

比较部署前后的 `JWT_KEY` 是否变化。不要在日志或命令输出中打印完整密钥。

### 16.6 上传返回 500

根据响应中的 `traceId` 查询 API 日志：

```bash
$compose_cmd logs --since=30m api | grep '<traceId>'
```

重点检查：

- 附件卷不可写或磁盘已满；
- PostgreSQL 连接失败；
- 文件实际类型与扩展名不一致；
- 单文件超过 10MB；
- 旧客户端仍发送不兼容字段。

### 16.7 PDF 或图片无法预览

先确认同一附件能否下载。预览复用鉴权下载接口：

- 下载也失败：检查 API 权限、数据库附件记录和附件卷文件；
- 下载成功但预览失败：检查浏览器控制台、文件 MIME 和文件内容是否真实有效；
- 反向代理不得把 `/api/attachments/.../download` 改写为静态文件路径。

## 17. 安全检查清单

上线前逐项确认：

- [ ] `.env` 权限为 `600`，未提交 Git；
- [ ] JWT 密钥和数据库密码均为随机强值；
- [ ] PostgreSQL `5432` 未发布到宿主机；
- [ ] API `8080` 未发布到宿主机；
- [ ] Web `8088` 只监听 `127.0.0.1`；
- [ ] 公网只开放必要的 `22/80/443`；
- [ ] 已启用 HTTPS；
- [ ] 两层 Nginx 的上传限制均至少为 12MB；
- [ ] `chuchai_postgres_data` 和 `chuchai_attachments_data` 均存在；
- [ ] 已完成数据库和附件同批次备份；
- [ ] 已在其他位置保存备份副本；
- [ ] 已完成管理员登录、项目、报销、附件预览、审批和发放验收；
- [ ] 运维人员知道不能执行 `docker compose down -v` 和 `docker volume prune`。

## 18. 官方参考

- Docker Engine Ubuntu 安装：<https://docs.docker.com/engine/install/ubuntu/>
- Docker Compose 生产部署：<https://docs.docker.com/compose/how-tos/production/>
- Docker 卷备份与恢复：<https://docs.docker.com/engine/storage/volumes/#back-up-restore-or-migrate-data-volumes>
- PostgreSQL 16 `dropdb`：<https://www.postgresql.org/docs/16/app-dropdb.html>
