# ====================================================================
# WindowsStats - Dauerhaften Administrator-Autostart einrichten
# ====================================================================
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Fordere Administratorrechte an..."
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Clear-Host
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  WindowsStats - Dauerhaften Admin-Autostart einrichten" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

$exePath = "$env:LOCALAPPDATA\WindowsStats\WindowsStats.exe"
if (-not (Test-Path $exePath)) {
    $exePath = "C:\Projekte\WindowsStats\bin\WindowsStats.exe"
}

Write-Host "Gefundene WindowsStats.exe: $exePath" -ForegroundColor Gray

# 1. Unprivilegierten Autostart aus der Registry entfernen
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "WindowsStats" -ErrorAction SilentlyContinue
Write-Host "[1/3] Standard-Autostart (ohne Admin-Rechte) in der Registry entfernt." -ForegroundColor Green

# 2. Geplante Aufgabe mit hoechsten Rechten (Admin) registrieren
$taskName = "WindowsStats_Autostart_Admin"
$action = New-ScheduledTaskAction -Execute $exePath -Argument "--tray"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "[2/3] Windows-Aufgabe '$taskName' mit hoechsten Rechten registriert." -ForegroundColor Green

# 3. Aktuellen Prozess beenden und neu als Admin ueber die Aufgabe starten
Write-Host "[3/3] Starte WindowsStats mit vollen Administratorrechten..." -ForegroundColor Green
Stop-Process -Name WindowsStats -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Start-ScheduledTask -TaskName $taskName

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  FERTIG! WindowsStats laeuft jetzt dauerhaft als Admin." -ForegroundColor Cyan
Write-Host "  - Keine UAC-Meldungen bei kuenftigen Windows-Starts." -ForegroundColor Gray
Write-Host "  - Voller Zugriff auf CPU-Temperaturen & Ring-0 Sensoren." -ForegroundColor Gray
Write-Host "  Dashboard: http://localhost:8420" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Druecke eine beliebige Taste zum Beenden..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
