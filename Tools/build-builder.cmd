@echo off
REM Publish the MapData Builder GUI and create a distributable ZIP.
REM All paths are repository-relative and dotnet.exe is resolved from PATH.
setlocal
set "TOOLS=%~dp0"
for %%I in ("%TOOLS%..") do set "ROOT=%%~fI\"
set "OUT=%ROOT%dist\TarkovMap-MapDataBuilder-v1.0.0"
set "ZIP=%ROOT%dist\TarkovMap-MapDataBuilder-v1.0.0.zip"

dotnet.exe publish "%TOOLS%MapPackBuilder.Gui\MapPackBuilder.Gui.csproj" -c Release -o "%OUT%"
if errorlevel 1 exit /b 1

del /Q "%OUT%\*.pdb" 2>nul
del /Q "%OUT%\TarkovMap.exe" 2>nul
del /Q "%OUT%\TarkovMap.deps.json" 2>nul
del /Q "%OUT%\TarkovMap.runtimeconfig.json" 2>nul

powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 exit /b 1

echo.
echo MapData Builder folder: %OUT%
echo MapData Builder ZIP:    %ZIP%
exit /b 0
