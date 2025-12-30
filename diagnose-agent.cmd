@echo off
setlocal EnableExtensions

set "LOG=C:\Windows\Temp\PrintControl_diagnose.log"
set "SHARE=\\ti-fabiano\SHARE"
set "HOST=192.168.22.5"
set "MSI=%SHARE%\PrintControl.Agent.msi"
set "DOTNET=%SHARE%\dotnet-runtime-8.0.22-win-x64.exe"

> "%LOG%" echo ==== PrintControl Agent diagnostics %DATE% %TIME% ====
>> "%LOG%" echo Computer: %COMPUTERNAME%
>> "%LOG%" echo User: %USERNAME%
>> "%LOG%" echo Share: %SHARE%
>> "%LOG%" echo Host: %HOST%
>> "%LOG%" echo.

>> "%LOG%" echo [OS]
>> "%LOG%" ver
>> "%LOG%" echo.

>> "%LOG%" echo [Identity]
>> "%LOG%" whoami /all
>> "%LOG%" echo.

>> "%LOG%" echo [Network]
>> "%LOG%" ipconfig /all
>> "%LOG%" ping -n 2 %HOST%
>> "%LOG%" powershell -NoProfile -Command "Test-NetConnection -ComputerName %HOST% -Port 5080 | Format-List"
>> "%LOG%" echo.

>> "%LOG%" echo [GPO]
>> "%LOG%" gpresult /r /scope computer
>> "%LOG%" echo.

>> "%LOG%" echo [GPO Startup Scripts]
>> "%LOG%" dir "C:\\Windows\\System32\\GroupPolicy\\Machine\\Scripts\\Startup"
>> "%LOG%" type "C:\\Windows\\System32\\GroupPolicy\\Machine\\Scripts\\scripts.ini"
>> "%LOG%" echo.

>> "%LOG%" echo [Share]
>> "%LOG%" net use
>> "%LOG%" dir "%MSI%"
>> "%LOG%" dir "%DOTNET%"
>> "%LOG%" echo.

>> "%LOG%" echo [Services]
>> "%LOG%" sc.exe query spooler
>> "%LOG%" sc.exe query eventlog
>> "%LOG%" sc.exe query PrintControl.Agent
>> "%LOG%" sc.exe queryex PrintControl.Agent
>> "%LOG%" sc.exe qc PrintControl.Agent
>> "%LOG%" echo.

>> "%LOG%" echo [Agent Files]
>> "%LOG%" dir "C:\\Program Files\\PrintControl\\Agent"
>> "%LOG%" dir "C:\\Program Files (x86)\\PrintControl\\Agent"
>> "%LOG%" dir "C:\\ProgramData\\PrintControl\\Agent"
>> "%LOG%" echo.

>> "%LOG%" echo [Agent Config]
>> "%LOG%" type "C:\\Program Files\\PrintControl\\Agent\\appsettings.json"
>> "%LOG%" type "C:\\Program Files (x86)\\PrintControl\\Agent\\appsettings.json"
>> "%LOG%" echo.

>> "%LOG%" echo [DotNet Runtime]
>> "%LOG%" reg query "HKLM\\SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64\\sharedfx\\Microsoft.NETCore.App" /v Version
>> "%LOG%" reg query "HKLM\\SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64\\sharedfx\\Microsoft.AspNetCore.App" /v Version
>> "%LOG%" echo.

>> "%LOG%" echo [PrintService Log]
>> "%LOG%" wevtutil gl Microsoft-Windows-PrintService/Operational
>> "%LOG%" echo.
>> "%LOG%" wevtutil qe Microsoft-Windows-PrintService/Operational /q:"*[System[(EventID=307)]]" /f:text /c:1
>> "%LOG%" echo.

>> "%LOG%" echo [Service Control Manager Events]
>> "%LOG%" wevtutil qe System /q:"*[System[Provider[@Name='Service Control Manager'] and (EventID=7000 or EventID=7009 or EventID=7023 or EventID=7031 or EventID=7034)]]" /f:text /c:20
>> "%LOG%" echo.

>> "%LOG%" echo [MsiInstaller Events]
>> "%LOG%" wevtutil qe Application /q:"*[System[Provider[@Name='MsiInstaller']]]" /f:text /c:20
>> "%LOG%" echo.

>> "%LOG%" echo [.NET Runtime Events]
>> "%LOG%" wevtutil qe Application /q:"*[System[Provider[@Name='.NET Runtime']]]" /f:text /c:20
>> "%LOG%" echo.

>> "%LOG%" echo [Application Error Events]
>> "%LOG%" wevtutil qe Application /q:"*[System[Provider[@Name='Application Error']]]" /f:text /c:20
>> "%LOG%" echo.

>> "%LOG%" echo [Last MSI Log]
>> "%LOG%" powershell -NoProfile -Command "if (Test-Path 'C:\\Windows\\Temp\\PrintControl_install.log') { Get-Content -Path 'C:\\Windows\\Temp\\PrintControl_install.log' -Tail 200 } else { 'No MSI log found.' }"
>> "%LOG%" echo.
>> "%LOG%" powershell -NoProfile -Command "if (Test-Path 'C:\\Windows\\Temp\\PrintControl_install.log') { Select-String -Path 'C:\\Windows\\Temp\\PrintControl_install.log' -Pattern 'Return value 3|Error 1920|1603' -Context 2,2 }"

>> "%LOG%" echo.
>> "%LOG%" echo ==== End diagnostics ====

echo Diagnostic log saved to %LOG%
exit /b 0
