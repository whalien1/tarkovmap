@echo off
REM 便捷封装：直接用 PATH 中的 dotnet 透传参数（不写死 SDK 安装路径）。
dotnet %*
