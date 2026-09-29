; Cosmic Brew installer (Inno Setup 6)
; Build the Unity player into Builds\Windows first, then run: ISCC.exe Tools\installer\CosmicBrew.iss

#define AppName "Cosmic Brew"
#define AppVersion "1.0.0"
#define AppPublisher "NodeStl"
#define AppExe "CosmicBrew.exe"
#define BuildDir "..\..\Builds\Windows"

[Setup]
AppId={{E71EB89E-B429-4B88-8A0F-6F485A3AD371}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=© 2026 {#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
; No admin rights needed: installs per user (the wizard also offers "all users")
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppPublisher}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
SetupIconFile=CosmicBrew.ico
WizardStyle=modern
WizardImageFile=wizard.bmp,wizard@2x.bmp
WizardSmallImageFile=small.bmp,small@2x.bmp
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=..\..\Builds\installer
OutputBaseFilename=CosmicBrew-Setup-v{#AppVersion}

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Excludes: "*DontShip*,*DoNotShip*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
