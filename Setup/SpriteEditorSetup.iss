; Sprite Editor Pro - Installer Script
; Inno Setup 6.x required
; Download from: https://jrsoftware.org/isdl.php

#define MyAppName "Sprite Editor Pro"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Company Name"
#define MyAppURL "https://spriteeditorpro.com"
#define MyAppExeName "SpriteEditor.exe"
#define MyAppId "{{A1B2C3D4-E5F6-G7H8-I9J0-K1L2M3N4O5P6}"

[Setup]
; Basic Setup
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/updates
AppCopyright=Copyright (C) 2025 {#MyAppPublisher}

; Install Directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output
OutputDir=..\bin\Setup
OutputBaseFilename=SpriteEditorProSetup-v{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes

; Requirements
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; UI
WizardStyle=modern
WizardImageFile=installer-image.bmp
WizardSmallImageFile=installer-icon.bmp
SetupIconFile=..\Resources\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline

; License & Info
LicenseFile=..\LICENSE.txt
InfoBeforeFile=..\Setup\BeforeInstall.txt
InfoAfterFile=..\Setup\AfterInstall.txt

; Uninstall
UninstallDisplayName={#MyAppName}
UninstallFilesDir={app}\uninst
CreateUninstallRegKey=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main Application
Source: "..\bin\Release\net8.0-windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\net8.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\net8.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion

; Runtime Files
Source: "..\bin\Release\net8.0-windows\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

; Resources
Source: "..\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs

; Documentation
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Registry]
; File Associations (optional - for .rig.json files)
Root: HKA; Subkey: "Software\Classes\.rig"; ValueType: string; ValueName: ""; ValueData: "SpriteEditorRig"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\SpriteEditorRig"; ValueType: string; ValueName: ""; ValueData: "Sprite Editor Rig File"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SpriteEditorRig\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\SpriteEditorRig\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Application Settings
Root: HKA; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"

[Run]
; Launch app after install (optional)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Cleanup user data (optional - with confirmation)
Filename: "{cmd}"; Parameters: "/c rd /s /q ""{localappdata}\SpriteEditorPro"""; Flags: runhidden; RunOnceId: "CleanupUserData"

[Code]
// Check if .NET 8.0 Desktop Runtime is installed
function IsDotNet8Installed: Boolean;
var
  Success: Boolean;
  ResultCode: Integer;
begin
  // Check if dotnet command exists and .NET 8 is available
  Success := Exec('cmd.exe', '/c dotnet --list-runtimes | findstr "Microsoft.WindowsDesktop.App 8"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := Success and (ResultCode = 0);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  
  // Check for .NET 8.0 Desktop Runtime
  if not IsDotNet8Installed then
  begin
    if MsgBox('.NET 8.0 Desktop Runtime is required but not installed.' + #13#10 + 
              'Would you like to download it now?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOW, ewNoWait, ResultCode);
      Result := False;
    end
    else
      Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create application data folder
    CreateDir(ExpandConstant('{localappdata}\SpriteEditorPro'));
    CreateDir(ExpandConstant('{localappdata}\SpriteEditorPro\Logs'));
    CreateDir(ExpandConstant('{localappdata}\SpriteEditorPro\Projects'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Response: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Response := MsgBox('Do you want to remove all user data and settings?' + #13#10 + 
                       'This includes logs, projects, and configuration files.', 
                       mbConfirmation, MB_YESNO or MB_DEFBUTTON2);
    if Response = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\SpriteEditorPro'), True, True, True);
    end;
  end;
end;

