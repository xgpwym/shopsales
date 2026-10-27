# 电商销售管理系统

电商销售 / 补单 / 推广费一体化管理后台。技术栈：**MySQL 8.0 + ASP.NET Core 10 Web API + Vue 3 + Element Plus**。

## 功能总览

| 模块 | 说明 |
|---|---|
| 数据看板 | 今日/本月净销售额卡片、近 30 天趋势、推广费平台占比、店铺排行 |
| 销售录入 | 日期默认昨天、选负责人动态加载店铺、逐店填销售额/退款、金额非负校验、同日重复保存即覆盖；右侧最近 30 条面板点击快速定位修改 |
| 补单录入 | 订单号/下单人/金额/是否返款/原因/备注，订单截图支持 Ctrl+V 粘贴与多张上传，勾选返款留痕（时间+操作人） |
| 推广费录入 | 店铺 × 推广类型 × 日期，同维度重复保存即覆盖 |
| 数据明细 | 三表统一列表：日期区间 + 平台/店铺/负责人/主体/下单人/状态组合筛选，行内编辑删除，Excel 导入导出 |
| 统计报表 | 多维汇总（负责人/主体/店铺/平台 × 销售额/退款/净额/补单/推广费，含占比与环比）、月度矩阵（年 × 12 月）、补单返款汇总（下单人 × 公司主体的已返/未返） |
| 店铺管理 | 平台/名称/负责人/主体/启用停用；负责人与公司主体**独立版本化**（各带生效日期），历史数据不受变更影响 |
| 基础数据 | 负责人、公司主体、下单人、字典（平台/补单原因/推广类型）维护 |
| 用户管理 | 管理员/录入员/只读三种角色 |

**核心设计**：业务三表录入时冗余保存"当时的负责人与公司主体"快照；报表统计只读快照字段，店铺归属变更（版本表 + 生效日期）永不影响历史数据。

## 目录结构

```
ecom-sales-system/
├── database/          # init.sql（建库建表）+ seed.sql（演示数据）
├── backend/           # ASP.NET Core 10 Web API（Dapper + MySqlConnector + MiniExcel + JWT）
│   └── EcomSales.Api/
├── frontend/          # Vue3 + Vite + Element Plus + ECharts（11 个页面）
└── deploy/            # nginx.conf / systemd unit / DEPLOY.md 部署说明
```

## 本地开发运行（Windows 11 + VS2026）

### 1. 数据库

> **无需手工建表**：后端首次启动时会自动创建 `ecom_sales` 数据库及全部 12 张表，并写入默认账号。
> 只需确保 `appsettings.json` 中连接串的用户名/密码指向本机 MySQL（默认 `root/root`，请按实际修改）。

可选：导入演示数据（10 店铺 / 4 平台 / 约 3 个月销售、补单、推广费数据，含归属变更版本演示）：

```bash
mysql -uroot -p ecom_sales < database/seed.sql
```

### 2. 后端（端口 5000）

VS2026 打开 `backend/EcomSales.Api/EcomSales.Api.csproj` 直接 F5；或命令行：

```bash
cd backend/EcomSales.Api
dotnet run
```

首次启动自动写入默认账号：`admin/Admin@123`（管理员）、`ops01/Ops@123`（录入员）、`viewer/View@123`（只读）。

### 3. 前端（端口 5173，API 自动代理到 5000）

```bash
cd frontend
npm install
npm run dev
```

浏览器打开 http://localhost:5173 。

## 生产部署（腾讯云 TencentOS 3.3）

见 [deploy/DEPLOY.md](deploy/DEPLOY.md)（nginx 1.28 反代 + systemd 托管 + MySQL 8.0）。

## 关键接口约定

- 认证：JWT Bearer（`Authorization: Bearer <token>`），登录 `POST /api/auth/login`
- 业务：`/api/sales`、`/api/supplements`、`/api/promotions`（列表/新增/编辑/删除）
- 店铺归属：`/api/shops/{id}/owner-versions`、`/api/shops/{id}/company-versions`（版本化变更）
- 报表：`/api/report/summary`（占比+环比）、`/api/report/monthly`（月度矩阵）、`/api/report/refund-summary`（返款汇总）
- 导入导出：`/api/import/template/{kind}`、`/api/import/{kind}`、`/api/export/{kind}`、`/api/report/export`
- 附件：`POST /api/supplements/{id}/attachments`（多张图）、`GET /api/files/{id}`
