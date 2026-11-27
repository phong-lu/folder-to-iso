#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef Publisher
  #define Publisher "FolderToISO contributors"
#endif

[Setup]
AppId={{CB03843A-EB76-4E71-A66D-4A083E680563}
AppName=FolderToISO
AppVersion={#AppVersion}
AppVerName=FolderToISO {#AppVersion}
AppPublisher={#Publisher}
SetupIconFile=..\..\FolderToISO\FolderToISO.ico
DefaultDirName={autopf}\FolderToISO
DefaultGroupName=FolderToISO
UninstallDisplayIcon={app}\FolderToISO.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=admin
OutputDir={#OutputDir}
OutputBaseFilename=FolderToISO_{#AppVersion}_x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
WizardImageFile=..\..\FolderToISO\FolderToISO.png
WizardSmallImageFile=..\..\FolderToISO\FolderToISO.png
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes

[Messages]
WelcomeLabel1=Welcome to FolderToISO {#AppVersion}
WelcomeLabel2=Create mountable ISO images from your folders.%n%nFolderToISO preserves nested directories and empty folders, supports files larger than 4 GiB, and lets you choose an output name and volume label.%n%nSetup will install FolderToISO and its required runtimes. You can also choose a desktop shortcut and File Explorer folder context-menu integration.%n%nClick Next to continue.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "contextmenu"; Description: "Add 'Create ISO with FolderToISO' to the File Explorer folder context menu"; GroupDescription: "File Explorer integration:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RedistPath}"; DestName: "vc_redist.x64.exe"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\FolderToISO"; Filename: "{app}\FolderToISO.exe"
Name: "{autodesktop}\FolderToISO"; Filename: "{app}\FolderToISO.exe"; Tasks: desktopicon

[Registry]
; Remove this app's verb when deselected during an upgrade.
Root: HKLM64; Subkey: "Software\Classes\Directory\shell\FolderToISO"; ValueType: none; Flags: deletekey; Tasks: not contextmenu
Root: HKLM64; Subkey: "Software\Classes\Directory\shell\FolderToISO"; ValueType: string; ValueData: "Create ISO with FolderToISO"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKLM64; Subkey: "Software\Classes\Directory\shell\FolderToISO"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\FolderToISO.exe"",0"; Tasks: contextmenu
Root: HKLM64; Subkey: "Software\Classes\Directory\shell\FolderToISO"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"; Tasks: contextmenu
Root: HKLM64; Subkey: "Software\Classes\Directory\shell\FolderToISO\command"; ValueType: string; ValueData: """{app}\FolderToISO.exe"" --source ""%1"""; Tasks: contextmenu

[Run]
Filename: "{app}\FolderToISO.exe"; Description: "Launch FolderToISO"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
procedure InitializeWizard();
begin
  // Keep the square logo proportional within the welcome and completion panels.
  WizardForm.WizardBitmapImage.SetBounds(ScaleX(18), ScaleY(100), ScaleX(128), ScaleY(128));
  WizardForm.WizardBitmapImage2.SetBounds(ScaleX(18), ScaleY(100), ScaleX(128), ScaleY(128));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  ExtractTemporaryFile('vc_redist.x64.exe');
  if not Exec(ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := 'Could not start the Microsoft Visual C++ runtime installer.'
  else if ExitCode = 3010 then
    NeedsRestart := True
  else if (ExitCode <> 0) and (ExitCode <> 1638) then
    Result := Format('The Microsoft Visual C++ runtime installer failed (exit %d).', [ExitCode]);
end;
