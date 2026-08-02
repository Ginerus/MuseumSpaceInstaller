; Inno Setup Script для MuseumSpace
; Один exe для x86 и x64

[Setup]
AppName = MuseumSpace
AppVersion = 1.0.0
AppPublisher = MuseumSpace Team
DefaultDirName = { autopf }\MuseumSpace
DefaultGroupName=MuseumSpace
OutputDir=.\Output
OutputBaseFilename=MuseumSpace_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\MuseumSpace.exe
UninstallDisplayName = MuseumSpace

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "bin\Release\net8.0-windows\win-x86\publish\MuseumSpaceInstaller.exe"; \
    DestDir: "{tmp}\MuseumSpaceSetup"; \
    Flags: deleteafterinstall

Source: "Payload\*"; \
    DestDir: "{tmp}\MuseumSpaceSetup\Payload"; \
    Flags: recursesubdirs deleteafterinstall

[Run]
Filename: "{tmp}\MuseumSpaceSetup\MuseumSpaceInstaller.exe"; \
    WorkingDir: "{tmp}\MuseumSpaceSetup"; \
    StatusMsg: "Запуск установщика MuseumSpace..."; \
    Flags: waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{app}"