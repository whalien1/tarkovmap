@echo off
REM Portable TarkovMap client build using repository-relative paths.
setlocal
set "ROOT=%~dp0"
dotnet.exe build "%ROOT%TarkovMap\TarkovMap.csproj" -c Release
if errorlevel 1 exit /b 1
xcopy /E /I /Y "%ROOT%TarkovMap\Data" "%ROOT%TarkovMap\bin\Release\net10.0-windows\Data" >nul
exit /b 0
