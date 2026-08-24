@echo off
REM TarkovMap 构建脚本：在任意克隆目录用标准 dotnet 构建（相对路径，无机器专属路径依赖）。
REM 等价于：dotnet restore + dotnet build -c Release + 把 Data 拷进运行目录。
setlocal
set "ROOT=%~dp0"
dotnet build "%ROOT%TarkovMap\TarkovMap.csproj" -c Release
if errorlevel 1 exit /b 1
xcopy /E /I /Y "%ROOT%TarkovMap\Data" "%ROOT%TarkovMap\bin\Release\net10.0-windows\Data" >nul
exit /b 0
