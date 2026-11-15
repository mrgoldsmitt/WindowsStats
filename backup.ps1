$ErrorActionPreference = "Stop"
$zipPath = "$env:TEMP\WindowsStats_backup_temp.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

$dest = "L:\SynologyDrive\Office\Privat\Anwendungsprojekte\WindowsStats\WindowsStats_backup v.1.0.0.zip"

Write-Host "Erstelle Projekt-Zip-Archiv..."
Compress-Archive -Path "C:\Projekte\WindowsStats\*" -DestinationPath $zipPath -Force

Write-Host "Kopiere Backup nach: $dest"
Copy-Item -Path $zipPath -Destination $dest -Force

Remove-Item $zipPath -Force

$item = Get-Item $dest
Write-Host "Backup erfolgreich abgeschlossen!"
Write-Host "Ziel: $($item.FullName) ($([Math]::Round($item.Length / 1MB, 2)) MB)"
