; DualSense Bridge – Inno Setup 6 script.
; Built by build\build-installer.ps1, which publishes the app to artifacts\publish and
; downloads the driver installers to installer\redist before calling ISCC.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#define MyAppName "DualSense Bridge"
#define MyAppExeName "DualSenseBridge.exe"
#define RedistDir "redist"

[Setup]
AppId={{6B0E3C9A-5D1F-4C7B-9A43-2E8D7F1B0C55}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=DualSense Bridge
DefaultDirName={autopf}\DualSense Bridge
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=DualSenseBridge-Setup-{#MyAppVersion}
SetupIconFile=..\src\DualSenseBridge\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; The running app holds this mutex; Setup asks the user to close it first.
AppMutex=DualSenseBridge.SingleInstance
CloseApplications=yes
RestartIfNeededByRun=yes

[Languages]
; Croatian is a user-contributed translation (bundled in installer\Languages) written for Inno Setup 6.5+.
#if Ver >= EncodeVer(6,5,0)
Name: "croatian"; MessagesFile: "Languages\Croatian.isl"
#endif
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
#if Ver >= EncodeVer(6,5,0)
croatian.DriversGroup=Driveri:
croatian.InstallViGEm=Instaliraj ViGEmBus (obavezno – emulacija Xbox 360 kontrolera)
croatian.InstallHidHide=Instaliraj HidHide (preporučeno – sprječava dvostruki unos)
croatian.OtherGroup=Dodatno:
croatian.DesktopIcon=Ikona na radnoj površini
croatian.InstallingViGEm=Instalacija ViGEmBus drivera…
croatian.InstallingHidHide=Instalacija HidHide drivera…
croatian.LaunchApp=Pokreni DualSense Bridge
croatian.MissingRedist=Instalacijski program drivera nije uključen u ovaj paket. Preuzmite ga s:
#endif
english.DriversGroup=Drivers:
english.InstallViGEm=Install ViGEmBus (required – Xbox 360 controller emulation)
english.InstallHidHide=Install HidHide (recommended – prevents double input)
english.OtherGroup=Additional:
english.DesktopIcon=Desktop shortcut
english.InstallingViGEm=Installing ViGEmBus driver…
english.InstallingHidHide=Installing HidHide driver…
english.LaunchApp=Launch DualSense Bridge
english.MissingRedist=The driver installer is not bundled with this package. Download it from:

[Tasks]
Name: "vigem"; Description: "{cm:InstallViGEm}"; GroupDescription: "{cm:DriversGroup}"; Check: not IsViGEmInstalled
Name: "hidhide"; Description: "{cm:InstallHidHide}"; GroupDescription: "{cm:DriversGroup}"; Check: not IsHidHideInstalled
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:OtherGroup}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RedistDir}\ViGEmBus_Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall skipifsourcedoesntexist; Tasks: vigem
Source: "{#RedistDir}\HidHide_Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall skipifsourcedoesntexist; Tasks: hidhide
Source: "..\README.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Driver setups run with their own (signed) installer UI.
Filename: "{tmp}\ViGEmBus_Setup.exe"; StatusMsg: "{cm:InstallingViGEm}"; Flags: waituntilterminated; Tasks: vigem; Check: FileExists(ExpandConstant('{tmp}\ViGEmBus_Setup.exe'))
Filename: "{tmp}\HidHide_Setup.exe"; StatusMsg: "{cm:InstallingHidHide}"; Flags: waituntilterminated; Tasks: hidhide; Check: FileExists(ExpandConstant('{tmp}\HidHide_Setup.exe'))
; Allow the app in HidHide right away so it can always see the controller it hides from games.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-hidhide"; Flags: runhidden waituntilterminated runascurrentuser
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Make the controller visible again, remove the HidHide entries and the autostart task.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "DualSenseBridgeCleanup"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\DualSenseBridge"

[Code]
function IsViGEmInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\ViGEmBus');
end;

function IsHidHideInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\HidHide');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('vigem') and not FileExists(ExpandConstant('{tmp}\ViGEmBus_Setup.exe')) then
      MsgBox(CustomMessage('MissingRedist') + #13#10 + 'https://github.com/nefarius/ViGEmBus/releases/latest', mbInformation, MB_OK);
    if WizardIsTaskSelected('hidhide') and not FileExists(ExpandConstant('{tmp}\HidHide_Setup.exe')) then
      MsgBox(CustomMessage('MissingRedist') + #13#10 + 'https://github.com/nefarius/HidHide/releases/latest', mbInformation, MB_OK);
  end;
end;
