# WindowsStats ⚡

> Ein hochmodernes, schlichtes und extrem ressourcenschonendes Hardware-Monitoring-Tool für Windows im Stil des **BitAxe / AxeOS Dashboards**.
> Lauffähig als native Windows-Tray-Anwendung und gleichzeitig als Web-Dashboard im gesamten lokalen Netzwerk (LAN / WLAN) über Browser oder Smartphone erreichbar.

---

## 🔍 Reverse-Engineering-Analyse (REA & Ghidra / Cecil)

Um die exakte Sensor- und Ausleselogik des auf diesem Rechner installierten Programms **Sidebar Diagnostics** (`C:\Users\s_sch\AppData\Local\SidebarDiagnostics`) zu erfassen, wurden das Reverse-Engineering-Toolkit **REA** (`rea-agents`) sowie statische Bytecode- und Metadaten-Inspektion eingesetzt:

1. **REA Managed Artifact Inspection**:
   - Befehl: `rea inspect-managed-artifact "C:\Users\s_sch\AppData\Local\SidebarDiagnostics\app-3.6.3\SidebarDiagnostics.exe"`
   - **Ergebnis**: Identifikation als .NET Framework 4.7.2 PE32 Assembly mit direkter Abhängigkeit zur High-Performance-Hardwarebibliothek `LibreHardwareMonitorLib.dll` (v0.9.1) und `HidSharp.dll`.
2. **Dekompilierung & Typen-Audit (Cecil / IL)**:
   - `SidebarDiagnostics.Monitoring.OHMMonitor`: Nutzt `LibreHardwareMonitor.Hardware.Computer` zur ring-0- und WMI-gestützten Abfrage von CPU (`Intel Core i5-6500`), RAM (`Generic Memory`), GPU (`Intel HD Graphics 530`) und Mainboard (`Fujitsu D3501-A1`).
   - `SidebarDiagnostics.Monitoring.DriveMonitor`: Kombiniert `System.IO.DriveInfo.GetDrives()` mit den Windows Performance Countern der Kategorie `LogicalDisk` (`Disk Read Bytes/sec`, `Disk Write Bytes/sec`, `% Free Space`).
   - `SidebarDiagnostics.Monitoring.NetworkMonitor`: Ermittelt über `NetworkInterface.GetAllNetworkInterfaces()` und Durchsatz-Deltas die genauen Upload- und Downloadraten der aktiven Adapter (Intel Ethernet Connection I219-V & VMware Adapters).

---

## 🚀 Kernfunktionen & Highlights

- **BitAxe / AxeOS Designästhetik**:
  - Moderner OLED/Charcoal Dark-Mode (`#0a0e17`) mit neon-grünen, cyanfarbenen und violetten Akzenten.
  - Großes SVG-Radial-Gauge für die CPU-Gesamtauslastung.
  - Live-Sparkline-Verlaufschart (HTML5 Canvas) über die letzten 60 Sekunden für CPU- und RAM-Last.
  - Integrierter **QR-Code-Modal**: Ermöglicht das sofortige Öffnen des Dashboards auf dem Smartphone durch einfaches Abscannen des Bildschirms!
- **Komplette Hardware-Unterstützung**:
  - **CPU**: Intel Core i5-6500 (Gesamtauslastung, 4 individuelle Kern-Balken, Taktraten in MHz, Package-Power in Watt, Kerntemperaturen bei Admin-Start).
  - **Arbeitsspeicher**: 24 GB RAM (Physischer RAM: Belegt / Frei / Gesamt + Virtueller Pagefile-Speicher).
  - **Grafikkarte**: Intel HD Graphics 530 (3D-Engine-Last, Video-Decode, Shared Memory).
  - **Speicher (Alle 12 Partitionen!)**: Vollständige Anzeige von `C:`, `D:`, `E:`, `F:`, `G:`, `H:`, `I:`, `J:`, `K:`, `L:`, `N:`, `V:` mit Volume-Label, Kapazität, Füllstandsbalken (mit Farbschwellen) und Echtzeit-Schreib-/Lese-Durchsatz.
  - **Netzwerk**: Live Download- & Upload-Geschwindigkeiten (automatische Skalierung KB/s & MB/s), Adapter-IPs und Gesamtdatenvolumen.
  - **Mainboard**: Fujitsu D3501-A1 & Uptime-Zähler.
- **Extrem Ressourcenschonend (Zero Electron Bloat)**:
  - Kompiliert zu einer **einzelnen nativen Executable (`WindowsStats.exe`, ca. 960 KB)**.
  - Benötigt im Betrieb lediglich **ca. 20 MB RAM** und **unter 0,1 % CPU-Last**!
  - Asynchroner TCP-HTTP-Server mit **Server-Sent Events (SSE)**: Überträgt Updates im 1-Sekunden-Takt direkt über offene Sockets, ohne dass der Browser ständig HTTP-Polling betreiben muss.
  - **Adaptives Polling**: Wenn keine Web-Clients verbunden sind und die App im Tray minimiert ist, senkt das Tool die Polling-Frequenz automatisch, um noch mehr Strom und Rechenzeit zu sparen.
- **Windows Tray-Integration**:
  - Läuft unauffällig im Windows-Infobereich (System Tray).
  - Dynamischer Tooltip mit aktueller CPU- und RAM-Auslastung.
  - Kontextmenü: Web-Dashboard öffnen, LAN-URL kopieren, Autostart umschalten, Admin-Neustart, Beenden.

---

## 📂 Projektstruktur

```
c:\Projekte\WindowsStats\
├── assets\
│   ├── app.ico                   # Multi-Res App- & Installer-Icon
│   ├── icon.svg                  # Skalierbares Vektor-Icon
│   ├── icon-192.png              # PWA Standard Icon
│   ├── icon-512.png              # PWA High-Res Splash Icon
│   └── apple-touch-icon.png      # iOS Safari Home Screen Icon
├── bin\
│   └── WindowsStats.exe          # Fertige Standalone-Executable (Single-File)
├── installer_output\
│   └── WindowsStats_Setup_v1.0.0.exe # Inno Setup Windows Installations-Assistent
├── lib\
│   ├── LibreHardwareMonitorLib.dll # Hardware-Abstraktionsschicht
│   └── HidSharp.dll              # USB/HID-Hardwaretreiber
├── src\
│   ├── Model\
│   │   └── HardwareData.cs       # Datenmodell & schneller JSON-Serializer
│   ├── Monitoring\
│   │   └── HardwareMonitor.cs    # Sensor-Erfassung & Delta-Berechnung
│   ├── Server\
│   │   └── HttpServer.cs         # Asynchroner HTTP-, PWA- & SSE-Webserver
│   ├── UI\
│   │   ├── ControlForm.cs        # Windows Forms Einstellungsfenster
│   │   └── TrayApp.cs            # Tray-Icon & Kontextmenü
│   ├── Web\
│   │   ├── index.html            # BitAxe-Style Web-Dashboard
│   │   ├── manifest.json         # Web App Manifest (PWA)
│   │   └── sw.js                 # Service Worker (PWA Offline-Shell)
│   └── Program.cs                # Entry-Point & Assembly-Resolver
├── build.bat                     # 1-Klick-Build via Windows csc.exe & Inno Setup
├── installer.iss                 # Inno Setup 6 Installationsskript
├── install.ps1                   # Schnelles lokales PowerShell-Setup
├── uninstall.ps1                 # Schnelles lokales Deinstallationsskript
├── LICENSE                       # Creative Commons CC BY-NC 4.0
└── README.md
```

---

## 📱 Progressive Web App (PWA)

Das Dashboard ist eine vollwertige **Progressive Web App (PWA)**:
- **Smartphone & Tablet (iOS / Android)**:
  - Öffne `http://<LAN-IP>:8420` im mobilen Browser (z. B. Chrome oder Safari).
  - Tippe auf **"Zum Home-Bildschirm hinzufügen"** bzw. **"App installieren"**.
  - Das Dashboard erhält ein eigenes hochauflösendes App-Icon auf dem Smartphone-Homescreen und öffnet sich als eigenständige App im Vollbildmodus (ohne Adressleiste).
- **Service Worker (`sw.js`)**:
  - Offline-Shell-Caching für blitzschnelles Laden auch bei instabiler WLAN-Verbindung.
  - Dynamisches Streaming von Hardware-Snapshots via SSE (`/api/stream`).

---

## 🛠️ Bauen & Starten

### 1. Kompilieren (1 Sekunde)
Da das Tool den systemeigenen 64-Bit C#-Compiler von Windows verwendet, wird **kein** zusätzliches SDK oder Visual Studio benötigt:

```cmd
c:\Projekte\WindowsStats\build.bat
```

Das Ergebnis ist eine einzelne Datei: `bin\WindowsStats.exe` sowie bei installiertem Inno Setup 6 der fertige Setup-Wizard `installer_output\WindowsStats_Setup_v1.0.0.exe`. Alle DLLs, PWA-Assets und Icons sind als Manifest-Ressourcen fest in die Single-File-EXE eingebettet.

### 2. Starten

- **Normaler Start (Tray & Kontrollfenster)**:
  ```cmd
  bin\WindowsStats.exe
  ```
- **Direkt minimiert ins System Tray starten**:
  ```cmd
  bin\WindowsStats.exe --tray
  ```
- **Als reiner Hintergrunddienst / Headless-Modus**:
  ```cmd
  bin\WindowsStats.exe --headless
  ```
- **Benutzerdefinierter Port**:
  ```cmd
  bin\WindowsStats.exe --port 9000
  ```

---

## 📦 Installation auf Windows

### Option A: Grafischer Setup-Assistent (Empfohlen)
Führe einfach die erstellte Installations-Exe aus:
```
installer_output\WindowsStats_Setup_v1.0.0.exe
```
Der Assistent richtet das Programm wahlweise unter Program Files oder im Benutzerverzeichnis ein, erstellt Desktop- und Startmenü-Verknüpfungen, konfiguriert den Autostart und registriert einen sauberen Uninstaller in den Windows-Einstellungen ("Apps & Features").

### Option B: Schnelle PowerShell-Installation
```powershell
powershell -ExecutionPolicy Bypass -File c:\Projekte\WindowsStats\install.ps1
```

---

## 🌐 Netzwerk- & API-Schnittstellen

Das Programm öffnet standardmäßig Port `8420` auf allen Netzwerkschnittstellen (`0.0.0.0`).

- **Web-Dashboard / PWA**: `http://localhost:8420/` bzw. `http://<LAN-IP>:8420/` (z. B. `http://192.168.188.194:8420`)
- **JSON-Snapshot API**: `GET /api/stats`
- **Server-Sent Events (SSE)**: `GET /api/stream`
- **Healthcheck / Ping**: `GET /api/ping`
- **PWA Manifest**: `GET /manifest.json`
- **PWA Service Worker**: `GET /sw.js`

---

## ⚖️ Lizenz

Dieses Projekt ist lizenziert unter der **Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)** Lizenz.

- **Kopieren & Weitergeben**: Erlaubt für private und nicht-kommerzielle Zwecke.
- **Namensnennung (Attribution)**: Der ursprüngliche Autor (`mrgoldsmitt`) muss stets genannt werden.
- **Keine Kommerzialisierung (NonCommercial)**: Jegliche kommerzielle Nutzung oder Weiterveräußerung ist ohne ausdrückliche schriftliche Genehmigung untersagt.

Vollständiger Lizenztext: siehe [LICENSE](LICENSE) oder [Creative Commons CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/legalcode).
