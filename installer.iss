; Inno Setup 6 Script for WindowsStats (Windows 10 / 11)
; Produces a modern standalone Windows installer executable

#define MyAppName "WindowsStats"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "mrgoldsmitt"
#define MyAppPublisherURL "https://mrgoldsmitt.work"
#define MyAppURL "https://github.com/mrgoldsmitt/WindowsStats"
#define MyAppExeName "WindowsStats.exe"

[Setup]
AppId={{C84F39E1-692B-4B77-9E41-3F8E12A81B84}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
DefaultGroupName={#MyAppName}
OutputDir=installer_output
OutputBaseFilename=WindowsStats_Setup_v{#MyAppVersion}
SetupIconFile=assets\app.ico
LicenseFile=LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\assets\app.ico

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "WindowsStats beim Start von Windows automatisch im Infobereich ausführen"; GroupDescription: "Autostart:"; Flags: unchecked

[Files]
Source: "bin\WindowsStats.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "assets\app.ico"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "assets\icon.svg"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "assets\icon-192.png"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "assets\icon-512.png"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "assets\apple-touch-icon.png"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; IconFilename: "{app}\assets\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; IconFilename: "{app}\assets\app.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\WindowsStats.exe"
Type: files; Name: "{app}\assets\*"
Type: dirifempty; Name: "{app}\assets"
Type: dirifempty; Name: "{app}"

[Code]
// Close running instance before installation
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM WindowsStats.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM WindowsStats.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
