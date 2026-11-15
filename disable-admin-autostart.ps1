# ====================================================================
# WindowsStats - Admin-Autostart entfernen (Zuruecksetzen)
# ====================================================================
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Clear-Host
Write-Host "Entferne WindowsStats Admin-Aufgabe..." -ForegroundColor Cyan
Unregister-ScheduledTask -TaskName "WindowsStats_Autostart_Admin" -Confirm:$false -ErrorAction SilentlyContinue

$exePath = "$env:LOCALAPPDATA\WindowsStats\WindowsStats.exe"
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "WindowsStats" -Value "`"$exePath`" --tray" -Force
Write-Host "Standard-Autostart in der Registry wiederhergestellt." -ForegroundColor Green
Write-Host "Fertig!"
Start-Sleep -Seconds 2
