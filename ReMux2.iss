[Setup]
AppName=ReMux2
AppVersion=2.0
AppPublisher=ReMux2 Team
AppPublisherURL=https://github.com/yourusername/ReMux2
AppSupportURL=https://github.com/yourusername/ReMux2/issues
AppUpdatesURL=https://github.com/yourusername/ReMux2/releases
DefaultDirName={autopf}\ReMux2
DefaultGroupName=ReMux2
AllowNoIcons=yes
LicenseFile=
InfoBeforeFile=
InfoAfterFile=
OutputDir=dist-installer
OutputBaseFilename=ReMux2Installer
SetupIconFile=icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ReMux2"; Filename: "{app}\ReMux2.exe"
Name: "{group}\{cm:UninstallProgram,ReMux2}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ReMux2"; Filename: "{app}\ReMux2.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ReMux2.exe"; Description: "{cm:LaunchProgram,ReMux2}"; Flags: nowait postinstall skipifsilent