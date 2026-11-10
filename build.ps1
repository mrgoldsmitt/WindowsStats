$ErrorActionPreference = "Stop"
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  WindowsStats - PowerShell Build & Package Script" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "C# Compiler csc.exe not found at $csc"
}

if (-not (Test-Path "bin")) {
    New-Item -ItemType Directory -Path "bin" | Out-Null
}

Write-Host "[1/2] Compiling WindowsStats.exe (Standalone Single-File)..." -ForegroundColor Yellow

$sources = @(
    "src\Logger.cs",
    "src\Model\HardwareData.cs",
    "src\Monitoring\HardwareMonitor.cs",
    "src\Server\HttpServer.cs",
    "src\UI\ControlForm.cs",
    "src\UI\TrayApp.cs",
    "src\Program.cs"
)

$args = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Management.dll",
    "/reference:lib\LibreHardwareMonitorLib.dll",
    "/reference:lib\HidSharp.dll",
    "/resource:lib\LibreHardwareMonitorLib.dll,LibreHardwareMonitorLib.dll",
    "/resource:lib\HidSharp.dll,HidSharp.dll",
    "/resource:src\Web\index.html,index.html",
    "/resource:src\Web\manifest.json,manifest.json",
    "/resource:src\Web\sw.js,sw.js",
    "/resource:assets\icon.svg,icon.svg",
    "/resource:assets\icon-192.png,icon-192.png",
    "/resource:assets\icon-512.png,icon-512.png",
    "/resource:assets\apple-touch-icon.png,apple-touch-icon.png",
    "/resource:assets\app.ico,app.ico",
    "/win32icon:assets\app.ico",
    "/out:bin\WindowsStats.exe"
) + $sources

& $csc $args

if ($LASTEXITCODE -ne 0) {
    throw "CSC Compilation failed with exit code $LASTEXITCODE"
}

$exe = Get-Item "bin\WindowsStats.exe"
Write-Host "[SUCCESS] Executable built: $($exe.FullName) ($([Math]::Round($exe.Length / 1MB, 2)) MB)" -ForegroundColor Green

$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) {
    Write-Host "[2/2] Compiling Inno Setup Installer..." -ForegroundColor Yellow
    & $iscc /Q "installer.iss"
    if ($LASTEXITCODE -eq 0) {
        $setup = Get-Item "installer_output\WindowsStats_Setup_v1.0.0.exe"
        Write-Host "[SUCCESS] Installer created: $($setup.FullName) ($([Math]::Round($setup.Length / 1MB, 2)) MB)" -ForegroundColor Green
    } else {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
    }
}
