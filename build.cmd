set ProgramData=C:\ProgramData
"C:\Program Files\dotnet\dotnet.exe" build "D:\tarkov map\TarkovMap\TarkovMap.csproj" -c Release --no-restore
if errorlevel 1 exit /b 1
xcopy /E /I /Y "D:\tarkov map\TarkovMap\Data" "D:\tarkov map\TarkovMap\bin\Release\net10.0-windows\Data" >nul
