@echo off
title WindowsStats - Admin-Autostart entfernen
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0disable-admin-autostart.ps1\"' -Verb RunAs"
