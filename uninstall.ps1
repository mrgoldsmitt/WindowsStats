# ========================================================
#   WindowsStats - Uninstaller Script
# ========================================================
[CmdletBinding()]
param()

$ErrorActionPreference = "SilentlyContinue"

Write-Host "========================================================" -ForegroundColor Yellow
Write-Host "   WindowsStats - Deinstallation" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Yellow

# 1. Beende laufende Prozesse
Write-Host "[1/5] Beende laufende WindowsStats-Prozesse..." -ForegroundColor Gray
Get-Process WindowsStats | Stop-Process -Force

# 2. Autostart Registry entfernen
Write-Host "[2/5] Entferne Windows Autostart..." -ForegroundColor Gray
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "WindowsStats" -ErrorAction SilentlyContinue

# 3. Verknüpfungen entfernen
Write-Host "[3/5] Lösche Verknüpfungen..." -ForegroundColor Gray
$desktopLnk = Join-Path ([Environment]::GetFolderPath("Desktop")) "WindowsStats.lnk"
if (Test-Path $desktopLnk) { Remove-Item $desktopLnk -Force }

$programsPath = [Environment]::GetFolderPath("Programs")
$startMenuLnk = Join-Path $programsPath "WindowsStats.lnk"
if (Test-Path $startMenuLnk) { Remove-Item $startMenuLnk -Force }

# 4. Firewall-Regel entfernen
Write-Host "[4/5] Entferne Firewall-Regel..." -ForegroundColor Gray
netsh advfirewall firewall delete rule name="WindowsStats Web Dashboard" | Out-Null

# 5. Installationsordner löschen
Write-Host "[5/5] Entferne Programmdateien..." -ForegroundColor Gray
$installDir = Join-Path $env:LOCALAPPDATA "WindowsStats"
if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  WindowsStats wurde vollständig entfernt." -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
