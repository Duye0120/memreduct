; Build through modern/build-installer.ps1. This script never publishes a release.
#ifndef AppVersion
  #error AppVersion must be provided by build-installer.ps1
#endif

[Setup]
AppId={{5B9D5C89-F01C-4AB6-A593-DA69B3835832}
AppName=Mem Reduct Fluent
AppVersion={#AppVersion}
AppPublisher=Duye0120
AppPublisherURL=https://github.com/Duye0120/memreduct-winui
AppSupportURL=https://github.com/Duye0120/memreduct-winui/issues
AppUpdatesURL=https://github.com/Duye0120/memreduct-winui/releases
DefaultDirName={autopf}\MemReduct Fluent
UsePreviousAppDir=yes
DisableDirPage=no
AlwaysShowDirOnReadyPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#InstallerOutput}
OutputBaseFilename=MemReductFluent-{#AppVersion}-win-x64-setup
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\Assets\AppIcon.ico
UninstallDisplayIcon={app}\MemReduct.Fluent.exe
LicenseFile=..\..\LICENSE
AppMutex=Local\MemReduct.Fluent.Running
SetupMutex=MemReduct.Fluent.Setup
CloseApplications=no
RestartApplications=no
Uninstallable=yes

[Languages]
Name: "zhcn"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Excludes: "settings.json,settings.json.tmp,*.pdb,*.log,Updates\*,HelperPayload\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppSource}\HelperPayload\MemReduct.Helper.exe"; DestDir: "{commonpf64}\MemReduct Helper"; Flags: ignoreversion
Source: "{#AppSource}\HelperPayload\MemReduct.Helper.exe.config"; DestDir: "{commonpf64}\MemReduct Helper"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Mem Reduct Fluent"; Filename: "{app}\MemReduct.Fluent.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\MemReduct.Fluent.exe"; Parameters: "{code:ImportArguments}"; Description: "{cm:LaunchProgram,Mem Reduct Fluent}"; Flags: postinstall nowait skipifsilent runasoriginaluser; Check: not IsAppUpdate

[UninstallDelete]
; Only our verified installer cache. User profiles in LocalAppData are deliberately retained.
Type: files; Name: "{app}\Updates\update.exe"
Type: files; Name: "{app}\Updates\update.exe.partial"
Type: dirifempty; Name: "{app}\Updates"
Type: files; Name: "{commonpf64}\MemReduct Helper\Updates\update.exe"
Type: files; Name: "{commonpf64}\MemReduct Helper\Updates\update.exe.partial"
Type: dirifempty; Name: "{commonpf64}\MemReduct Helper\Updates"
Type: dirifempty; Name: "{commonpf64}\MemReduct Helper"

[CustomMessages]
zhcn.ImportTitle=导入便携版设置（可选）
zhcn.ImportDescription=选择原便携版目录中的 settings.json，留空则跳过。
zhcn.ImportNote=首次打开时复制设置；原文件不删除，已有安装版设置不覆盖。管理员开机启动请在安装后到设置中开启并授权一次。
zhcn.ImportLabel=设置文件：
zhcn.InvalidDirectory=请选择独立的本地程序目录，不能使用磁盘根目录或包含符号链接的路径。
zhcn.MoveInstallation=升级需沿用原安装目录。如需迁移，请先卸载旧版本再选择新目录；个人设置会保留。
zhcn.OlderInstaller=已安装的版本更新，请使用最新安装包。
zhcn.CloseFirst=请先从 Mem Reduct 的托盘菜单退出，再继续安装或卸载。
zhcn.HelperStopFailed=无法停止后台清理助手，请稍后重试安装或卸载。
zhcn.HelperBusy=后台清理助手文件仍被占用，无法替换。请稍后重试安装。
zhcn.RelaunchFailed=未能重新打开程序。请从开始菜单手动打开，现有设置已保留。
zhcn.InvalidSettings=请选择一个不大于 64 KB 的 settings.json 文件，或留空跳过。
en.ImportTitle=Import portable settings (optional)
en.ImportDescription=Select settings.json from your portable app folder, or leave this blank.
en.ImportNote=Settings are copied on first launch. The source and existing installed settings are preserved. Enable administrator startup in the app settings after installation and authorize once.
en.ImportLabel=Settings file:
en.InvalidDirectory=Choose a dedicated local application folder, not a drive root or a path containing symbolic links.
en.MoveInstallation=Upgrade in the existing folder. To move the app, uninstall it first, then select a new folder. Your settings are preserved.
en.OlderInstaller=A newer version is already installed. Use the latest installer.
en.CloseFirst=Exit Mem Reduct from its tray menu before installing or uninstalling.
en.HelperStopFailed=The background cleanup helper could not be stopped. Retry setup or uninstall shortly.
en.HelperBusy=The background cleanup helper file is still in use and cannot be replaced. Retry setup shortly.
en.RelaunchFailed=The app could not be reopened. Open it from the Start menu; your settings are preserved.
en.InvalidSettings=Choose a settings.json file no larger than 64 KB, or leave the field blank.

[Code]
var
  ImportPage: TInputFileWizardPage;
  PreviousDirectory: String;

function GetFileAttributesW(const FileName: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

function ConvertStringSecurityDescriptorToSecurityDescriptorW(Sddl: String; Revision: Cardinal; var Descriptor: NativeUInt; var Size: Cardinal): Boolean;
  external 'ConvertStringSecurityDescriptorToSecurityDescriptorW@advapi32.dll stdcall';
function SetFileSecurityW(FileName: String; Information: Cardinal; Descriptor: NativeUInt): Boolean;
  external 'SetFileSecurityW@advapi32.dll stdcall';
function LocalFree(Memory: NativeUInt): NativeUInt;
  external 'LocalFree@kernel32.dll stdcall';

procedure ProtectHelperPath(Path: String);
var Descriptor: NativeUInt;
    Size: Cardinal;
begin
  if not ConvertStringSecurityDescriptorToSecurityDescriptorW('O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;BU)', 1, Descriptor, Size) then
    RaiseException('Could not create the cleanup helper security descriptor.');
  try
    if not SetFileSecurityW(Path, $80000007, Descriptor) then
      RaiseException('Could not protect the cleanup helper: ' + Path);
  finally
    LocalFree(Descriptor);
  end;
end;

function ConvertStringSidToSidW(StringSid: String; var Sid: NativeUInt): Boolean;
  external 'ConvertStringSidToSidW@advapi32.dll stdcall';
function LookupAccountNameW(SystemName: NativeUInt; AccountName: String; Sid: NativeUInt; var SidSize: Cardinal;
  Domain: NativeUInt; var DomainSize: Cardinal; var Use: Cardinal): Boolean;
  external 'LookupAccountNameW@advapi32.dll stdcall';
function EqualSid(First, Second: NativeUInt): Boolean;
  external 'EqualSid@advapi32.dll stdcall';
function LocalAlloc(Flags: Cardinal; Bytes: NativeUInt): NativeUInt;
  external 'LocalAlloc@kernel32.dll stdcall';

function SameTaskUser(Value, Sid: String): Boolean;
var Expected, Account, Domain: NativeUInt;
    SidSize, DomainSize, Use: Cardinal;
begin
  Result := CompareText(Value, Sid) = 0;
  if Result or (Value = '') then exit;
  Expected := 0;
  Account := 0;
  Domain := 0;
  if not ConvertStringSidToSidW(Sid, Expected) then exit;
  try
    SidSize := 0;
    DomainSize := 0;
    LookupAccountNameW(0, Value, 0, SidSize, 0, DomainSize, Use);
    if (SidSize = 0) or (SidSize > 68) or (DomainSize > 32768) then exit;
    Account := LocalAlloc($40, SidSize);
    Domain := LocalAlloc($40, (DomainSize + 1) * 2);
    if (Account = 0) or (Domain = 0) then exit;
    // Task Scheduler may serialize the same account as a name instead of its SID.
    if not LookupAccountNameW(0, Value, Account, SidSize, Domain, DomainSize, Use) then exit;
    Result := EqualSid(Account, Expected);
  finally
    if Domain <> 0 then LocalFree(Domain);
    if Account <> 0 then LocalFree(Account);
    LocalFree(Expected);
  end;
end;

procedure StopHelperTasks(DeleteTasks: Boolean);
var Service, Folder, Tasks, Task, Definition, Actions, Action: Variant;
    I, Attempt: Integer;
    Sid: String;
    Matches: Boolean;
begin
  Service := CreateOleObject('Schedule.Service');
  Service.Connect;
  Folder := Service.GetFolder('\');
  Tasks := Folder.GetTasks(1);
  for I := Tasks.Count downto 1 do
  begin
    Task := Tasks.Item(I);
    if Pos('MemReduct.Fluent.Helper.S-1-', Task.Name) = 1 then
    begin
      Sid := Copy(Task.Name, Length('MemReduct.Fluent.Helper.') + 1, MaxInt);
      Definition := Task.Definition;
      Actions := Definition.Actions;
      if Actions.Count = 1 then
      begin
        Action := Actions.Item(1);
        Matches := False;
        try
          Matches := (CompareText(Action.Path, ExpandConstant('{commonpf64}\MemReduct Helper\MemReduct.Helper.exe')) = 0)
            and (Action.Arguments = '--serve ' + Sid) and SameTaskUser(Definition.Principal.UserId, Sid);
        except
          Log('Skipping a task without the expected helper action.');
        end;
        if Matches then
        begin
          if Task.State = 4 then
          begin
            try
              Task.Stop(0);
              for Attempt := 1 to 50 do
              begin
                if Task.State <> 4 then break;
                Sleep(100);
              end;
            except
              RaiseException(CustomMessage('HelperStopFailed'));
            end;
            if Task.State = 4 then RaiseException(CustomMessage('HelperStopFailed'));
          end;
          if DeleteTasks then Folder.DeleteTask(Task.Name, 0);
        end;
      end;
    end;
  end;
end;

function HasLink(const Directory: String): Boolean;
var
  Entry: TFindRec;
begin
  Result := False;
  if not DirExists(Directory) then exit;
  if (GetFileAttributesW(Directory) and $400) <> 0 then begin Result := True; exit; end;
  if FindFirst(AddBackslash(Directory) + '*', Entry) then
  begin
    try
      repeat
        if (Entry.Name <> '.') and (Entry.Name <> '..') then
        begin
          if (Entry.Attributes and $400) <> 0 then begin Result := True; exit; end;
          if (Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
            if HasLink(AddBackslash(Directory) + Entry.Name) then begin Result := True; exit; end;
        end;
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
end;

function InitializeSetup: Boolean;
var I: Integer;
begin
  // The updater launches setup before exiting; wait briefly instead of killing the app.
  if ExpandConstant('{param:FROMAPP|0}') = '1' then
    for I := 1 to 150 do
    begin
      if not CheckForMutexes('Local\MemReduct.Fluent.Running') then break;
      Sleep(100);
    end;
  Result := True;
end;

function IsAppUpdate: Boolean;
begin
  Result := ExpandConstant('{param:FROMAPP|0}') = '1';
end;

procedure DeinitializeSetup;
var ResultCode: Integer;
    Directory: String;
begin
  if not IsAppUpdate then exit;
  Directory := ExpandConstant('{commonpf64}\MemReduct Helper');
  // Also restore the old app after cancelling the wizard; the protected helper drops privileges.
  if not Exec(Directory + '\MemReduct.Helper.exe', '--launch-ui', Directory, SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    MsgBox(CustomMessage('RelaunchFailed'), mbError, MB_OK);
end;

procedure InitializeWizard;
begin
  RegQueryStringValue(HKLM64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{5B9D5C89-F01C-4AB6-A593-DA69B3835832}_is1', 'InstallLocation', PreviousDirectory);
  ImportPage := CreateInputFilePage(wpSelectDir, CustomMessage('ImportTitle'), CustomMessage('ImportDescription'), CustomMessage('ImportNote'));
  ImportPage.Add(CustomMessage('ImportLabel'), 'JSON (*.json)|*.json', '.json');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = ImportPage.ID) and (PreviousDirectory <> '');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Size: Integer;
begin
  Result := True;
  if (CurPageID = ImportPage.ID) and (ImportPage.Values[0] <> '') then
  begin
    Result := FileSize(ImportPage.Values[0], Size) and (Size <= 65536) and
      (CompareText(ExtractFileName(ImportPage.Values[0]), 'settings.json') = 0);
    if not Result then MsgBox(CustomMessage('InvalidSettings'), mbError, MB_OK);
  end;
end;

function ImportArguments(Param: String): String;
begin
  Result := '';
  if ImportPage.Values[0] <> '' then Result := '--import-settings "' + ImportPage.Values[0] + '"';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var VersionMS, VersionLS: Cardinal;
    SelectedDirectory, ParentDirectory: String;
begin
  Result := '';
  SelectedDirectory := RemoveBackslashUnlessRoot(ExpandConstant('{app}'));
  if (Pos(Lowercase(AddBackslash(ExpandConstant('{commonpf64}\MemReduct Helper'))), Lowercase(AddBackslash(SelectedDirectory))) = 1)
    or (Pos(Lowercase(AddBackslash(SelectedDirectory)), Lowercase(AddBackslash(ExpandConstant('{commonpf64}\MemReduct Helper')))) = 1) then
  begin Result := CustomMessage('InvalidDirectory'); exit; end;
  if (Length(SelectedDirectory) <= 3) or (Copy(SelectedDirectory, 2, 2) <> ':\') or HasLink(SelectedDirectory) then
  begin Result := CustomMessage('InvalidDirectory'); exit; end;
  ParentDirectory := SelectedDirectory;
  repeat
    if DirExists(ParentDirectory) and ((GetFileAttributesW(ParentDirectory) and $400) <> 0) then
    begin Result := CustomMessage('InvalidDirectory'); exit; end;
    ParentDirectory := ExtractFileDir(ParentDirectory);
  until Length(ParentDirectory) <= 3;
  if (PreviousDirectory <> '') and
    (CompareText(SelectedDirectory, RemoveBackslashUnlessRoot(PreviousDirectory)) <> 0) then
  begin Result := CustomMessage('MoveInstallation'); exit; end;
  if CheckForMutexes('Local\MemReduct.Fluent.Running') then
  begin Result := CustomMessage('CloseFirst'); exit; end;
  if GetVersionNumbers(ExpandConstant('{app}\MemReduct.Fluent.exe'), VersionMS, VersionLS) then
  begin
#if VersionMajor == "0"
    // Migrate only the published builds from before the 0.0.1 version reset.
    if (VersionMS = (1 shl 16)) and
      ((VersionLS = 0) or (VersionLS = (1 shl 16)) or (VersionLS = (2 shl 16))) then exit;
#endif
    if (VersionMS > (({#VersionMajor} shl 16) or {#VersionMinor})) or
      ((VersionMS = (({#VersionMajor} shl 16) or {#VersionMinor})) and (VersionLS > ({#VersionPatch} shl 16))) then
      Result := CustomMessage('OlderInstaller');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Directory, Parent, Executable: String;
    ResultCode, I: Integer;
begin
  Directory := ExpandConstant('{commonpf64}\MemReduct Helper');
  Executable := AddBackslash(Directory) + 'MemReduct.Helper.exe';
  if CurStep = ssInstall then
  begin
    Parent := Directory;
    repeat
      if DirExists(Parent) and ((GetFileAttributesW(Parent) and $400) <> 0) then RaiseException(CustomMessage('InvalidDirectory'));
      Parent := ExtractFileDir(Parent);
    until Length(Parent) <= 3;
    if HasLink(Directory) then RaiseException(CustomMessage('InvalidDirectory'));
    if not ForceDirectories(Directory) then RaiseException('Could not create the protected cleanup helper directory.');
    ProtectHelperPath(Directory);
    StopHelperTasks(False);
    // Unlink old known files before copying: do not overwrite a pre-existing hard link.
    for I := 1 to 50 do
    begin
      if not FileExists(Executable) or DeleteFile(Executable) then break;
      Sleep(100);
    end;
    if FileExists(Executable) then RaiseException(CustomMessage('HelperBusy'));
    if FileExists(Executable + '.config') and not DeleteFile(Executable + '.config') then RaiseException('Could not replace the helper configuration.');
  end;
  if CurStep = ssPostInstall then
  begin
    ProtectHelperPath(Executable);
    ProtectHelperPath(Executable + '.config');
    if not Exec(Executable, '--verify', Directory, SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
      RaiseException('Cleanup helper protection verification failed.');
    if not Exec(Executable, '--migrate-startup', Directory, SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
      MsgBox('The previous startup task could not be migrated. Enable startup in the app settings after installation.', mbInformation, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Service, Folder, Tasks, Task, Definition, Actions, Action: Variant;
    I: Integer;
    Matches: Boolean;
    RunCommand: String;
begin
  if CurUninstallStep <> usUninstall then exit;
  StopHelperTasks(True);
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MemReduct.Fluent', RunCommand) then
    if CompareText(RunCommand, '"' + ExpandConstant('{app}\MemReduct.Fluent.exe') + '" --minimized') = 0 then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MemReduct.Fluent');
  // Delete only this product's tasks before removing files; failures stop the uninstaller.
  Service := CreateOleObject('Schedule.Service');
  Service.Connect;
  Folder := Service.GetFolder('\');
  Tasks := Folder.GetTasks(1);
  for I := Tasks.Count downto 1 do
  begin
    Task := Tasks.Item(I);
    if Pos('MemReduct.Fluent.S-1-', Task.Name) = 1 then
    begin
      Definition := Task.Definition;
      Actions := Definition.Actions;
      if Actions.Count = 1 then
      begin
        Action := Actions.Item(1);
        Matches := False;
        try
          Matches := (CompareText(Action.Path, ExpandConstant('{app}\MemReduct.Fluent.exe')) = 0) and
            (Action.Arguments = '--elevated --minimized');
        except
          // Non-executable actions do not expose Path/Arguments; leave those tasks alone.
          Log('Skipping a task without an executable action.');
        end;
        if Matches then Folder.DeleteTask(Task.Name, 0);
      end;
    end;
  end;
end;
