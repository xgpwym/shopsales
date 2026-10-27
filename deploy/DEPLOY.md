# 部署说明（腾讯云 TencentOS 3.3 + MySQL 8.0 + nginx 1.28 + .NET 10）

## 一、服务器环境准备

```bash
# 1. 安装 .NET 10 运行时（TencentOS/EL 系列）
sudo dnf install -y dotnet-sdk-10.0
# 或仅运行时：sudo dnf install -y aspnetcore-runtime-10.0

# 2. 安装 MySQL 8.0 并设为开机自启
sudo dnf install -y mysql-server
sudo systemctl enable --now mysqld

# 3. 安装 nginx
sudo dnf install -y nginx
sudo systemctl enable --now nginx
```

## 二、数据库初始化

```bash
# 建库建表（12 张表）
mysql -uroot -p < database/init.sql

# 导入演示数据（2026-07-01 ~ 2026-10-25，含归属变更版本演示；正式环境可跳过）
mysql -uroot -p ecom_sales < database/seed.sql
```

建议为系统创建专用账号：

```sql
CREATE USER 'ecom'@'localhost' IDENTIFIED BY 'Ecom@123456';
GRANT ALL PRIVILEGES ON ecom_sales.* TO 'ecom'@'localhost';
FLUSH PRIVILEGES;
```

## 三、发布后端

Windows 开发机（VS2026）执行：

```bash
cd backend/EcomSales.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
```

> 若服务器未装共享运行时，可改用自包含发布：`--self-contained true`（产物较大，无需服务器安装 .NET）。

上传到服务器：

```bash
scp -r ./publish/* user@your-server:/opt/ecom-sales/api/
```

修改生产配置 `/opt/ecom-sales/api/appsettings.Production.json` 中的连接串，然后：

```bash
sudo cp deploy/ecom-sales.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now ecom-sales
sudo systemctl status ecom-sales        # 确认 running
curl http://127.0.0.1:5000/api/auth/me  # 返回 401 即服务正常
```

> 首次启动会自动写入默认用户：`admin/Admin@123`（管理员）、`ops01/Ops@123`（录入员）、`viewer/View@123`（只读），**上线后请立即修改密码**。

## 四、发布前端

```bash
cd frontend
npm install
npm run build          # 产出 dist/
scp -r dist/* user@your-server:/var/www/ecom-sales/
```

服务器 nginx：

```bash
sudo mkdir -p /var/www/ecom-sales
sudo cp deploy/nginx-ecom-sales.conf /etc/nginx/conf.d/
sudo nginx -t && sudo systemctl reload nginx
```

浏览器访问 `http://服务器IP` 即可使用。

## 五、目录与端口约定

| 项 | 值 |
|---|---|
| 后端目录 | /opt/ecom-sales/api |
| 截图上传目录 | /opt/ecom-sales/api/uploads（自动创建，请纳入备份） |
| 前端目录 | /var/www/ecom-sales |
| 后端监听 | 127.0.0.1:5000（仅本机，由 nginx 反代对外） |
| 数据库 | ecom_sales，12 张表 |

## 六、日常运维

```bash
sudo systemctl restart ecom-sales   # 重启后端
journalctl -u ecom-sales -f         # 查看后端日志
```

- 数据备份：`mysqldump -uroot -p ecom_sales | gzip > ecom_sales_$(date +%F).sql.gz`
- 附件备份：rsync `/opt/ecom-sales/api/uploads`
