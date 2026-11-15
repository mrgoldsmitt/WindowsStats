@echo off
title WindowsStats - Admin-Autostart einrichten
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0enable-admin-autostart.ps1\"' -Verb RunAs"
