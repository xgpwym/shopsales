@echo off
chcp 65001 >nul
title 电商销售管理系统 - 后端 API
cd /d %~dp0backend\EcomSales.Api
echo 后端启动中，接口地址 http://localhost:5000 （Ctrl+C 停止）
dotnet run
