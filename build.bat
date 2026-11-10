@echo off
setlocal
echo ========================================================
echo   WindowsStats - Build Script
echo ========================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    echo [FEHLER] C# Compiler csc.exe nicht gefunden: %CSC%
    exit /b 1
)

if not exist bin mkdir bin

echo [1/2] Kompiliere WindowsStats.exe (Standalone Single-File)...
%CSC% /nologo /target:winexe /platform:x64 ^
    /optimize+ ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Management.dll ^
    /reference:lib\LibreHardwareMonitorLib.dll ^
    /reference:lib\HidSharp.dll ^
    /resource:lib\LibreHardwareMonitorLib.dll,LibreHardwareMonitorLib.dll ^
    /resource:lib\HidSharp.dll,HidSharp.dll ^
    /resource:src\Web\index.html,index.html ^
    /resource:src\Web\manifest.json,manifest.json ^
    /resource:src\Web\sw.js,sw.js ^
    /resource:assets\icon.svg,icon.svg ^
    /resource:assets\icon-192.png,icon-192.png ^
    /resource:assets\icon-512.png,icon-512.png ^
    /resource:assets\apple-touch-icon.png,apple-touch-icon.png ^
    /resource:assets\app.ico,app.ico ^
    /win32icon:assets\app.ico ^
    /out:bin\WindowsStats.exe ^
    src\Model\HardwareData.cs ^
    src\Monitoring\HardwareMonitor.cs ^
    src\Server\HttpServer.cs ^
    src\UI\ControlForm.cs ^
    src\UI\TrayApp.cs ^
    src\Program.cs

if %ERRORLEVEL% EQU 0 (
    echo [2/3] Build erfolgreich abgeschlossen!
    echo Ausgabedatei: bin\WindowsStats.exe
    dir bin\WindowsStats.exe | findstr WindowsStats.exe

    set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if exist %ISCC% (
        echo [3/3] Erstelle Inno Setup Installer...
        %ISCC% /Q installer.iss
        if %ERRORLEVEL% EQU 0 (
            echo Installer erstellt: installer_output\WindowsStats_Setup_v1.0.0.exe
        )
    )
) else (
    echo [FEHLER] Kompilierung fehlgeschlagen!
    exit /b %ERRORLEVEL%
)

endlocal
