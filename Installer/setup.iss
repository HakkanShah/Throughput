; Throughput Installer Script for Inno Setup
; Created by Hakkan
; Website: https://hakkan.is-a.dev

#define MyAppName "Throughput"
#define MyAppVersion "3.1.2"
#define MyAppPublisher "Hakkan"
#define MyAppURL "https://hakkan.is-a.dev"
#define MyAppExeName "Throughput.exe"

[Setup]
; Application Info
AppId={{8A0CDEF1-B234-5678-90AB-CDEF12345678}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation Settings
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=..\publish\installer
OutputBaseFilename=Throughput-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Visual
WizardSmallImageFile=
WizardImageFile=

; Uninstall
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Desktop Shortcut Option
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
; Taskbar Pin Option
Name: "taskbarpin"; Description: "Pin to Taskbar"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
; Start on Windows Startup
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Startup Options:"; Flags: unchecked

[Files]
; Main application files - the ReadyToRun folder build (publish-app.ps1), NOT the
; single-file portable exe. The single-file bundle has to extract ~16MB of native
; libraries on first run after every update (~4.6s) and JITs everything, which
; also doubles committed memory. The folder build starts in ~1s and uses less RAM.
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
; Start Menu Shortcut
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; Desktop Shortcut (if selected)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; NOTE: startup is handled by a logon scheduled task (see [Run]), not a Startup
; folder shortcut. Startup-folder items are launched last - after every HKLM/HKCU
; Run entry - and are subject to Explorer's ~10s startup delay, which made the
; widget appear noticeably later than other autostart apps.

[InstallDelete]
; Remove the legacy Startup-folder shortcut from pre-3.2 installs so upgraded
; users don't end up launching the app twice.
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"

[Run]
; NOTE: the logon task is registered from [Code] (CurStepChanged) rather than here,
; so that silent auto-updates can migrate users who previously used the Startup
; folder - in /SILENT mode Inno falls back to default task selections.
; Run after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; NOTE: no [UninstallRun] for the logon task - schtasks.exe requires elevation and
; this installer runs as a standard user. Removal is done via the Task Scheduler
; COM API in CurUninstallStepChanged below.

[Code]
// Pin to Taskbar functionality - simplified approach
procedure PinToTaskbar(const FileName: string);
var
  Shell: Variant;
  Folder: Variant;
  FolderItem: Variant;
  VerbName: string;
  i, VerbCount: Integer;
begin
  try
    Shell := CreateOleObject('Shell.Application');
    Folder := Shell.NameSpace(ExtractFileDir(FileName));
    if VarIsNull(Folder) then Exit;
    
    FolderItem := Folder.ParseName(ExtractFileName(FileName));
    if VarIsNull(FolderItem) then Exit;
    
    VerbCount := FolderItem.Verbs.Count;
    for i := 0 to VerbCount - 1 do
    begin
      VerbName := FolderItem.Verbs.Item(i).Name;
      // Look for "Pin to taskbar" in various languages
      if (Pos('taskbar', Lowercase(VerbName)) > 0) then
      begin
        FolderItem.Verbs.Item(i).DoIt;
        Break;
      end;
    end;
  except
    // Silent fail - taskbar pinning is optional and may not work on all Windows versions
  end;
end;

const
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';
  TaskName = '{#MyAppName} Startup';

var
  LegacyStartupShortcutExisted: Boolean;

// Registers an "at log on" scheduled task via the Task Scheduler COM API.
// schtasks.exe is NOT used: it requires elevation, and this installer runs as a
// standard user (PrivilegesRequired=lowest). The COM API works unelevated.
// A logon task starts the widget right at sign-in, unlike a Startup-folder
// shortcut, which Explorer launches last and only after its ~10s startup delay.
// "DOMAIN\user" for the account running Setup. Required below - see TryCreateLogonTask.
function CurrentUserId(): string;
var
  Domain: string;
begin
  Domain := GetEnv('USERDOMAIN');
  if Domain = '' then
    Domain := GetEnv('COMPUTERNAME');
  Result := Domain + '\' + GetEnv('USERNAME');
end;

function TryCreateLogonTask(): Boolean;
var
  Service, RootFolder, TaskDef, Trigger, Action, RegInfo, Settings, Principal: Variant;
  UserId: string;
begin
  Result := False;
  UserId := CurrentUserId();
  try
    Service := CreateOleObject('Schedule.Service');
    Service.Connect();
    RootFolder := Service.GetFolder('\');

    TaskDef := Service.NewTask(0);

    RegInfo := TaskDef.RegistrationInfo;
    RegInfo.Description := 'Starts {#MyAppName} when you sign in.';
    RegInfo.Author := '{#MyAppPublisher}';

    Principal := TaskDef.Principal;
    Principal.UserId := UserId;
    Principal.LogonType := 3;   // TASK_LOGON_INTERACTIVE_TOKEN
    Principal.RunLevel := 0;    // TASK_RUNLEVEL_LUA - never elevate

    Settings := TaskDef.Settings;
    Settings.Enabled := True;
    Settings.StartWhenAvailable := True;
    Settings.Hidden := False;
    Settings.DisallowStartIfOnBatteries := False;
    Settings.StopIfGoingOnBatteries := False;
    Settings.ExecutionTimeLimit := 'PT0S';   // no time limit
    Settings.MultipleInstances := 2;         // TASK_INSTANCES_IGNORE_NEW

    Trigger := TaskDef.Triggers.Create(9);   // TASK_TRIGGER_LOGON
    Trigger.Enabled := True;
    // Scoping the trigger to this user is REQUIRED: without a UserId the trigger
    // applies to every user, which the API only allows for administrators, and
    // registration fails with E_ACCESSDENIED for a normal (unelevated) install.
    Trigger.UserId := UserId;

    Action := TaskDef.Actions.Create(0);     // TASK_ACTION_EXEC
    Action.Path := ExpandConstant('{app}\{#MyAppExeName}');
    Action.WorkingDirectory := ExpandConstant('{app}');

    // 6 = TASK_CREATE_OR_UPDATE, 3 = TASK_LOGON_INTERACTIVE_TOKEN
    RootFolder.RegisterTaskDefinition(TaskName, TaskDef, 6, '', '', 3);
    Result := True;
  except
    Result := False;
  end;
end;

// Fallback when the COM API is unavailable: a plain HKCU Run entry. Slower than
// a logon task (it still waits behind Explorer's startup delay) but reliable.
procedure CreateRunKeyFallback();
begin
  RegWriteStringValue(HKEY_CURRENT_USER, RunKeyPath, '{#MyAppName}',
    '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"');
end;

// Registers autostart, preferring the logon task and falling back to the Run key.
procedure EnableAutoStart();
begin
  if TryCreateLogonTask() then
  begin
    // Task won - drop any Run entry a previous install left, or we'd launch twice.
    RegDeleteValue(HKEY_CURRENT_USER, RunKeyPath, '{#MyAppName}');
  end
  else
  begin
    CreateRunKeyFallback();
  end;
end;

// Removes both autostart mechanisms.
procedure DisableAutoStart();
var
  Service, RootFolder: Variant;
begin
  try
    Service := CreateOleObject('Schedule.Service');
    Service.Connect();
    RootFolder := Service.GetFolder('\');
    RootFolder.DeleteTask(TaskName, 0);
  except
    // No task registered (or COM unavailable) - nothing to remove.
  end;
  RegDeleteValue(HKEY_CURRENT_USER, RunKeyPath, '{#MyAppName}');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Record this before [InstallDelete] removes the legacy shortcut, so an
  // upgrade can carry the user's existing "start with Windows" choice over.
  if CurStep = ssInstall then
  begin
    LegacyStartupShortcutExisted :=
      FileExists(ExpandConstant('{userstartup}\{#MyAppName}.lnk'));
  end;

  if CurStep = ssPostInstall then
  begin
    // Pin to taskbar if user selected the option
    if WizardIsTaskSelected('taskbarpin') then
    begin
      PinToTaskbar(ExpandConstant('{app}\{#MyAppExeName}'));
    end;

    // Register autostart when the user asked for it, or when migrating an
    // existing Startup-folder user (silent updates use default task selections).
    if WizardIsTaskSelected('startupicon') or LegacyStartupShortcutExisted then
    begin
      EnableAutoStart();
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DisableAutoStart();
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
