; Throughput Installer Script for Inno Setup
; Created by Hakkan
; Website: https://hakkan.is-a.dev

#define MyAppName "Throughput"
#define MyAppVersion "2.0.0"
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
; Main Application Files (from publish folder)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
; Start Menu Shortcut
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; Desktop Shortcut (if selected)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; Startup Shortcut (if selected)
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
; Run after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

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

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Pin to taskbar if user selected the option
    if WizardIsTaskSelected('taskbarpin') then
    begin
      PinToTaskbar(ExpandConstant('{app}\{#MyAppExeName}'));
    end;
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
