; Build through modern/build-installer.ps1. This script never publishes a release.
#ifndef AppVersion
  #error AppVersion must be provided by build-installer.ps1
#endif

[Setup]
AppId={{5B9D5C89-F01C-4AB6-A593-DA69B3835832}
AppName=Mem Reduct Fluent
AppVersion={#AppVersion}
AppPublisher=Duye0120
AppPublisherURL=https://github.com/Duye0120/memreduct
AppSupportURL=https://github.com/Duye0120/memreduct/issues
AppUpdatesURL=https://github.com/Duye0120/memreduct/releases
DefaultDirName={autopf}\MemReduct Fluent
UsePreviousAppDir=no
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#InstallerOutput}
OutputBaseFilename=MemReductFluent-{#AppVersion}-win-x64-setup
Compression=lzma2
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
Source: "{#AppSource}\*"; DestDir: "{app}"; Excludes: "settings.json,settings.json.tmp,*.pdb,*.log,Updates\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Mem Reduct Fluent"; Filename: "{app}\MemReduct.Fluent.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\MemReduct.Fluent.exe"; Parameters: "{code:ImportArguments}"; Description: "{cm:LaunchProgram,Mem Reduct Fluent}"; Flags: postinstall nowait skipifsilent runasoriginaluser

[UninstallDelete]
; Only our verified installer cache. User profiles in LocalAppData are deliberately retained.
Type: files; Name: "{app}\Updates\update.exe"
Type: files; Name: "{app}\Updates\update.exe.partial"
Type: dirifempty; Name: "{app}\Updates"

[CustomMessages]
zhcn.ImportTitle=导入便携版设置（可选）
zhcn.ImportDescription=选择原便携版目录中的 settings.json，留空则跳过。
zhcn.ImportNote=首次打开时复制设置；原文件不删除，已有安装版设置不覆盖。管理员开机启动请在安装后到设置中开启并授权一次。
zhcn.ImportLabel=设置文件：
zhcn.FixedDirectory=安装目录必须为 Program Files 下的 MemReduct Fluent，不能包含符号链接。
zhcn.OlderInstaller=已安装的版本更新，请使用最新安装包。
zhcn.CloseFirst=请先从 Mem Reduct 的托盘菜单退出，再继续安装或卸载。
zhcn.InvalidSettings=请选择一个不大于 64 KB 的 settings.json 文件，或留空跳过。
en.ImportTitle=Import portable settings (optional)
en.ImportDescription=Select settings.json from your portable app folder, or leave this blank.
en.ImportNote=Settings are copied on first launch. The source and existing installed settings are preserved. Enable administrator startup in the app settings after installation and authorize once.
en.ImportLabel=Settings file:
en.FixedDirectory=Install to MemReduct Fluent under Program Files, without symbolic links.
en.OlderInstaller=A newer version is already installed. Use the latest installer.
en.CloseFirst=Exit Mem Reduct from its tray menu before installing or uninstalling.
en.InvalidSettings=Choose a settings.json file no larger than 64 KB, or leave the field blank.

[Code]
var
  ImportPage: TInputFileWizardPage;

function GetFileAttributesW(const FileName: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

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

procedure InitializeWizard;
begin
  ImportPage := CreateInputFilePage(wpSelectDir, CustomMessage('ImportTitle'), CustomMessage('ImportDescription'), CustomMessage('ImportNote'));
  ImportPage.Add(CustomMessage('ImportLabel'), 'JSON (*.json)|*.json', '.json');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = ImportPage.ID) and FileExists(ExpandConstant('{autopf}\MemReduct Fluent\MemReduct.Fluent.exe'));
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
begin
  Result := '';
  if (CompareText(RemoveBackslashUnlessRoot(ExpandConstant('{app}')), ExpandConstant('{autopf}\MemReduct Fluent')) <> 0) or
    ((GetFileAttributesW(ExpandConstant('{autopf}')) and $400) <> 0) or HasLink(ExpandConstant('{app}')) then
  begin Result := CustomMessage('FixedDirectory'); exit; end;
  if CheckForMutexes('Local\MemReduct.Fluent.Running') then
  begin Result := CustomMessage('CloseFirst'); exit; end;
  if GetVersionNumbers(ExpandConstant('{app}\MemReduct.Fluent.exe'), VersionMS, VersionLS) then
    if (VersionMS > (({#VersionMajor} shl 16) or {#VersionMinor})) or
      ((VersionMS = (({#VersionMajor} shl 16) or {#VersionMinor})) and (VersionLS > ({#VersionPatch} shl 16))) then
      Result := CustomMessage('OlderInstaller');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Service, Folder, Tasks, Task, Definition, Actions, Action: Variant;
    I: Integer;
    Matches: Boolean;
begin
  if CurUninstallStep <> usUninstall then exit;
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
