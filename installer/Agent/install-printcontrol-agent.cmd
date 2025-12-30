@echo off
set "MSI=\\ti-fabiano\SHARE\PrintControl.Agent.msi"
set "DOTNET=\\ti-fabiano\SHARE\dotnet-runtime-8.0.22-win-x64.exe"

REM Enable the PrintService Operational log so EventID 307 is generated.
wevtutil sl Microsoft-Windows-PrintService/Operational /e:true

REM Install .NET 8 runtime if missing.
reg query "HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App" /v Version >nul 2>&1
if errorlevel 1 (
  "%DOTNET%" /install /quiet /norestart
)

msiexec /i "%MSI%" /qn /norestart /l*v "C:\Windows\Temp\PrintControl_install.log"
sc.exe start PrintControl.Agent >nul 2>&1
exit /b 0
