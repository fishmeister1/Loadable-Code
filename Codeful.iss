; Codeful Installer Script for Inno Setup
; This script creates a professional Windows installer for Codeful

[Setup]
; Basic application information
AppName=Code
AppVersion=1.0.0
AppPublisher=Loadable
AppPublisherURL=https://github.com/fishmeister1/Loadable-Code
AppSupportURL=https://github.com/fishmeister1/Loadable-Code
AppUpdatesURL=https://github.com/fishmeister1/Loadable-Code
DefaultDirName={autopf}\Loadable
DisableProgramGroupPage=yes
; License file (optional - uncomment if you have a license file)
; LicenseFile=LICENSE.txt
; README file (optional - uncomment if you have a readme)
; InfoBeforeFile=README.txt
OutputBaseFilename=Loadable-Code-Setup
OutputDir=installer
SetupIconFile=Resources\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; Minimum Windows version
MinVersion=6.1sp1

; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Privileges
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminLoggedOn

[Files]
; Main application files
Source: "bin\Release\net10.0-windows\win-x64\publish\Code.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net10.0-windows\win-x64\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net10.0-windows\win-x64\publish\Code.pdb"; DestDir: "{app}"; Flags: ignoreversion

; Include .env file if it exists (for API key)
Source: ".env"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Include diagnostic script for troubleshooting
Source: "diagnostic.bat"; DestDir: "{app}"; Flags: ignoreversion

; Resources (if you want to include them separately)
; Source: "Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Code"; Filename: "{app}\Code.exe"
Name: "{autodesktop}\Code"; Filename: "{app}\Code.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Code"; Filename: "{app}\Code.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\Code.exe"; Description: "{cm:LaunchProgram,Code}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Custom code can be added here if needed for special installation logic