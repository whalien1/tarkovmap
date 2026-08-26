@echo off
REM Publish the TarkovMap v1.1.2 client with the checked-in MapData.
REM The output directory is disposable build output under repository dist.
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%dist\TarkovMap-v1.1.2"
set "ZIP=%ROOT%dist\TarkovMap-v1.1.2.zip"

if exist "%OUT%" rmdir /S /Q "%OUT%"

dotnet.exe publish "%ROOT%TarkovMap\TarkovMap.csproj" -c Release -o "%OUT%"
if errorlevel 1 exit /b 1

xcopy /E /I /Y "%ROOT%TarkovMap\Data" "%OUT%\Data" >nul
if errorlevel 1 exit /b 1
copy /Y "%ROOT%README.md" "%OUT%\README.md" >nul
copy /Y "%ROOT%NOTICE.md" "%OUT%\NOTICE.md" >nul
del /Q "%OUT%\*.pdb" 2>nul

powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 exit /b 1

echo.
echo TarkovMap folder: %OUT%
echo TarkovMap ZIP:    %ZIP%
exit /b 0
