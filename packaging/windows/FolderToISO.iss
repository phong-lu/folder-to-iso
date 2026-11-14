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
DefaultDirName={autopf}\FolderToISO
DefaultGroupName=FolderToISO
UninstallDisplayIcon={app}\FolderToISO.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=admin
OutputDir={#OutputDir}
OutputBaseFilename=foldertoiso_{#AppVersion}_x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes

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
