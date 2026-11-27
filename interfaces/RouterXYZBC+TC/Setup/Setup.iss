; Customer installer built from the application's local Release publish.
#ifndef AppName
  #error AppName must be supplied by BuildSetup.ps1.
#endif
#ifndef AppVersion
  #error AppVersion must be supplied by BuildSetup.ps1.
#endif
#ifndef PayloadDir
  #error PayloadDir must be supplied by BuildSetup.ps1.
#endif

[Setup]
AppId=Radonix-{#AppName}-Customer
AppName=Radonix {#AppName}
AppVersion={#AppVersion}
AppPublisher=Radonix
DefaultDirName={autopf}\Radonix\Customer\{#AppName}
DefaultGroupName=Radonix\{#AppName} (Customer)
DisableProgramGroupPage=yes
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile=..\{#AppName}\RadCAM.ico
UninstallDisplayIcon={app}\{#AppName}.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\NT-ESI\*.xml"; DestDir: "{app}\NT-ESI"; Flags: ignoreversion
Source: "..\Reference\*"; DestDir: "{app}\User Manual\{#AppName}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{commonappdata}\Radonix\{#AppName}"; Permissions: users-modify

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppName}.exe"; WorkingDir: "{app}"
Name: "{group}\Operator Manual"; Filename: "{app}\User Manual\{#AppName}\index.html"
