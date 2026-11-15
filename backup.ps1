$ErrorActionPreference = "Stop"
$zipPath = "$env:TEMP\WindowsStats_backup_temp.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

$dest1 = "L:\SynologyDrive\Office\Privat\Anwendungsprojekte\WindowsStats\WindowsStats_backup v.1.0.0.zip"
$dest2 = "L:\SynologyDrive\Office\Privat\Anwendungsprojekte\Anwendungen\WindowsStats_backup v.1.0.0.zip"

Write-Host "Erstelle Projekt-Zip-Archiv..."
# Exclude bin and installer_output if desired, or zip all files
Compress-Archive -Path "C:\Projekte\WindowsStats\*" -DestinationPath $zipPath -Force

Write-Host "Kopiere Backup nach: $dest1"
Copy-Item -Path $zipPath -Destination $dest1 -Force

Write-Host "Kopiere Backup nach: $dest2"
Copy-Item -Path $zipPath -Destination $dest2 -Force

Remove-Item $zipPath -Force

$item1 = Get-Item $dest1
$item2 = Get-Item $dest2
Write-Host "Backup erfolgreich abgeschlossen!"
Write-Host "Ziel 1: $($item1.FullName) ($([Math]::Round($item1.Length / 1MB, 2)) MB)"
Write-Host "Ziel 2: $($item2.FullName) ($([Math]::Round($item2.Length / 1MB, 2)) MB)"
