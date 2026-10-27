@echo off
chcp 65001 >nul
title 电商销售管理系统 - 前端
cd /d %~dp0frontend
if not exist node_modules (
    echo 首次运行，正在安装依赖...
    call npm install --no-audit --no-fund
)
echo 前端启动中，浏览器访问 http://localhost:5173 （Ctrl+C 停止）
call npm run dev
