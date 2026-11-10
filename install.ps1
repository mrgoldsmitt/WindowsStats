# ========================================================
#   WindowsStats - Installer & Setup Script
# ========================================================
[CmdletBinding()]
param(
    [switch]$NoStart
)

$ErrorActionPreference = "Stop"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   WindowsStats - Installation" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) { $scriptRoot = Get-Location }
$binExe = Join-Path $scriptRoot "bin\WindowsStats.exe"

# 1. Sicherstellen, dass die Binary kompiliert ist
if (-not (Test-Path $binExe)) {
    Write-Host "[1/6] Kompiliere Anwendung via build.bat..." -ForegroundColor Yellow
    cmd.exe /c (Join-Path $scriptRoot "build.bat")
    if (-not (Test-Path $binExe)) {
        Write-Error "Kompilierung fehlgeschlagen! bin\WindowsStats.exe wurde nicht erstellt."
        return
    }
} else {
    Write-Host "[1/6] Ausführbare Datei bin\WindowsStats.exe gefunden." -ForegroundColor Green
}

# 2. Beende eventuell laufende Instanzen
Write-Host "[2/6] Prüfe laufende Instanzen..." -ForegroundColor Yellow
Get-CimInstance Win32_Process -Filter "Name = 'WindowsStats.exe'" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Beende vorhandenen Prozess ID $($_.ProcessId)..." -ForegroundColor Gray
    Invoke-CimMethod -InputObject $_ -MethodName Terminate | Out-Null
    Start-Sleep -Milliseconds 400
}

# 3. Installationsziel vorbereiten
$installDir = Join-Path $env:LOCALAPPDATA "WindowsStats"
Write-Host "[3/6] Installiere nach: $installDir" -ForegroundColor Yellow
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

$installedExe = Join-Path $installDir "WindowsStats.exe"
Copy-Item -Path $binExe -Destination $installedExe -Force
Write-Host "  WindowsStats.exe erfolgreich kopiert." -ForegroundColor Green

# 4. Verknüpfungen erstellen (Startmenü & Desktop)
Write-Host "[4/6] Erstelle Verknüpfungen..." -ForegroundColor Yellow
$wsh = New-Object -ComObject WScript.Shell

# Desktop-Shortcut
$desktopPath = [Environment]::GetFolderPath("Desktop")
$desktopLnk = Join-Path $desktopPath "WindowsStats.lnk"
$shortcut = $wsh.CreateShortcut($desktopLnk)
$shortcut.TargetPath = $installedExe
$shortcut.Description = "WindowsStats - Hardware Monitor & Web Dashboard"
$shortcut.WorkingDirectory = $installDir
$shortcut.Save()

# Startmenü-Shortcut
$programsPath = [Environment]::GetFolderPath("Programs")
$startMenuLnk = Join-Path $programsPath "WindowsStats.lnk"
$shortcutStart = $wsh.CreateShortcut($startMenuLnk)
$shortcutStart.TargetPath = $installedExe
$shortcutStart.Description = "WindowsStats - Hardware Monitor & Web Dashboard"
$shortcutStart.WorkingDirectory = $installDir
$shortcutStart.Save()
Write-Host "  Startmenü- und Desktop-Verknüpfung erstellt." -ForegroundColor Green

# 5. Windows Autostart einrichten (Registry HKCU)
Write-Host "[5/6] Konfiguriere Autostart..." -ForegroundColor Yellow
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Set-ItemProperty -Path $runKey -Name "WindowsStats" -Value "`"$installedExe`" --tray"
Write-Host "  Autostart in Windows Registry registriert." -ForegroundColor Green

# 6. Windows Firewall-Regel (Port 8420)
Write-Host "[6/6] Prüfe Firewall-Freigabe für Port 8420..." -ForegroundColor Yellow
$fwRuleName = "WindowsStats Web Dashboard"
$ruleExists = netsh advfirewall firewall show rule name="$fwRuleName" 2>&1 | Select-String "WindowsStats"
if (-not $ruleExists) {
    try {
        netsh advfirewall firewall add rule name="$fwRuleName" dir=in action=allow protocol=TCP localport=8420 | Out-Null
        Write-Host "  Firewall-Regel für Port 8420 erfolgreich hinzugefügt." -ForegroundColor Green
    } catch {
        Write-Host "  Hinweis: Firewall-Regel konnte ohne erhöhte Rechte nicht direkt gesetzt werden." -ForegroundColor Gray
        Write-Host "  Sie kann jederzeit im Desktop-Fenster per Klick eingerichtet werden." -ForegroundColor Gray
    }
} else {
    Write-Host "  Firewall-Regel ist bereits aktiv." -ForegroundColor Green
}

# Lokale LAN IP ermitteln (Physischer LAN-Adapter)
$lanIp = "127.0.0.1"
try {
    $ipObj = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { 
        $_.InterfaceAlias -notlike "*Loopback*" -and 
        $_.InterfaceAlias -notlike "*VMware*" -and 
        $_.InterfaceAlias -notlike "*Virtual*" -and 
        $_.IPAddress -notlike "169.254*" 
    } | Select-Object -First 1
    if ($ipObj) { $lanIp = $ipObj.IPAddress }
} catch {}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  Installation erfolgreich abgeschlossen!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  Installationspfad: $installedExe"
Write-Host "  Lokales Dashboard: http://localhost:8420" -ForegroundColor Cyan
Write-Host "  WLAN / LAN Zugriff: http://${lanIp}:8420" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Green

if (-not $NoStart) {
    Write-Host "Starte WindowsStats..." -ForegroundColor Yellow
    Start-Process -FilePath $installedExe
}
