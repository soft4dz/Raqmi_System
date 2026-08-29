; RaqmiSystemDesktop.iss
; Inno Setup script for the Raqmi System WPF desktop client (src/RaqmiSystem.Desktop).
;
; This installer only copies the self-contained published application to the target
; machine and creates shortcuts. It intentionally does NOT configure the API URL.
;
; The desktop client resolves its API base URL at runtime, in this order (see
; src/RaqmiSystem.Desktop/DesktopSettings.cs):
;   1. The RAQMI_DESKTOP_API_URL environment variable.
;   2. The per-user settings file %APPDATA%\RaqmiSystem\desktop-settings.json
;      (a JSON object with an "ApiBaseUrl" property).
;   3. A hard-coded fallback default (http://localhost:5180).
;
; Configure the API URL AFTER installation, using one of the two mechanisms above.
; Keeping that out of the installer keeps this first pilot version simple: every
; hotel workstation gets the identical package, and pointing it at the right server
; is a one-time, per-machine step done separately (manually, or by IT tooling).
;
; Prerequisite: run the self-contained publish before compiling this script:
;   dotnet publish src/RaqmiSystem.Desktop/RaqmiSystem.Desktop.csproj -c Release -r win-x64 ^
;     --self-contained true -p:PublishSingleFile=false -o publish/desktop
;
; Compile with:
;   iscc deploy/installer/RaqmiSystemDesktop.iss
;
; Requires Inno Setup 6.3 or newer: the ArchitecturesAllowed/ArchitecturesInstallIn64BitMode
; "x64compatible" keyword used below does not exist in Inno Setup 6.0-6.2.

#define MyAppName "Raqmi System"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Raqmi System"
#define MyAppExeName "RaqmiSystem.Desktop.exe"

[Setup]
AppId={{8F4A6C2E-2B7D-4E6A-9C1F-3D5E8A9B6C41}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\RaqmiSystem
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=RaqmiSystem-Setup
SetupIconFile=..\..\assets\brand\raqmi-system\icons\RaqmiSystem.ico
LicenseFile=..\..\assets\LICENSE-EULA.txt
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\..\publish\desktop\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
