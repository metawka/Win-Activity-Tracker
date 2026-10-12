; Установщик Windows Activity Tracker (Inno Setup)
#define AppName "Windows Activity Tracker"
#define AppExe "WinActivityTracker.exe"
; Версия берётся прямо из собранного exe - всегда актуальна.
#define AppVer GetVersionNumbersString("staging\WinActivityTracker.exe")
#define AppPublisher "metawka"

[Setup]
AppId={{7C2A9E14-3B6D-4F71-9E2A-WATRACKER01}}
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\WinActivityTracker
DefaultGroupName=Windows Activity Tracker
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=.
OutputBaseFilename=WinActivityTracker-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\WinActivityTracker\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
; Процессы гасим сами в [Code] (счётчик без окна CloseApplications не закрывает).
CloseApplications=no

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "autostart"; Description: "Запускать при входе в Windows"
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Flags: unchecked

[Files]
Source: "staging\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Windows Activity Tracker"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\Windows Activity Tracker"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--install-autostart"; Tasks: autostart; Flags: runhidden runasoriginaluser
Filename: "{app}\WatCounter.exe"; Description: "Начать подсчёт сейчас"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall-autostart"; Flags: runhidden; RunOnceId: "DisableAutostart"

[Code]
procedure KillProcesses;
var rc: Integer;
begin
  Exec('taskkill.exe', '/F /IM WatCounter.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Exec('taskkill.exe', '/F /IM WinActivityTracker.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
end;

// Перед установкой файлов гасим оба процесса, чтобы не были заняты.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  KillProcesses;
  Result := '';
end;

// Перед удалением тоже гасим (счётчик без окна иначе держит файлы).
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    KillProcesses;
end;
