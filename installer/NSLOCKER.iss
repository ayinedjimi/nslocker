; Installeur NSLOCKER - Inno Setup 6
; Compilation : ISCC installer\NSLOCKER.iss  (après dotnet publish)
#define AppName "NSLOCKER"
#define AppVersion "0.1.0-alpha"
#define AppPublisher "AYI NEDJIMI CONSULTANTS"
#define AppURL "https://ayinedjimi-consultants.fr"
#define AppExe "NSLOCKER.exe"
#ifndef SourceExe
  #define SourceExe "..\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\publish\NSLOCKER.exe"
#endif

[Setup]
AppId={{6F1B7A52-3C84-4E0D-9B2A-5D7E41C0A9F3}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL=https://github.com/ayinedjimi/nslocker/issues
AppUpdatesURL=https://github.com/ayinedjimi/nslocker/releases
AppCopyright=Copyright (c) 2026 {#AppPublisher}
VersionInfoVersion=0.1.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Installeur {#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\Assets\app.ico
LicenseFile=..\LICENSE
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
OutputDir=..\dist
OutputBaseFilename=NSLOCKER-Setup-{#AppVersion}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runascurrentuser
