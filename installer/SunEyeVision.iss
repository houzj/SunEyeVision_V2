; 依赖：Inno Setup 6.7.3（编译器本体）。官方下载：https://jrsoftware.org/isdl.php
; 说明：第三方安装器体积 10.3MB，**不入库**（见 .gitignore 的 installer/third-party/），
;       本机自行安装 Inno Setup 6.7.3 后即可用本脚本编译生成安装包。
;
; 版本/渠道/显示名/AppId/安装目录/载荷目录/输出目录由本次打包调用的 /D 参数注入。
; releaseVersion 是发布语义版本；AppVersion 是四段 Windows 文件版本。

; All release definitions come from the current script invocation. Never load stale fragments.
#ifndef ReleaseVersion
  #error "Run installer/Pack-Release.ps1 to provide release definitions."
#endif
#ifndef AssociationId
  #error "AssociationId must be provided by Pack-Release.ps1."
#endif

#ifndef AppName
  #define AppName "SunEyeVision"
#endif

; 版本真源由打包脚本注入；默认值仅供本地自测，禁止作为发布版本
#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif

; 渠道：local / alpha / beta / rc / stable
#ifndef Channel
  #define Channel "local"
#endif

; 安装显示名包含完整版本；目录与 AppId 不随版本变化
#ifndef AppDisplayName
  #define AppDisplayName AppName
#endif

; 正常渠道共享升级身份；仅 local 使用独立 AppId
#ifndef AppId
  #define AppId "{{7A2E9C41-3B58-4C7E-9E31-2F6D8B1A5C04}"
#endif

; 安装目录名（autopf 下）：正常发布 SunEyeVision，本地包 SunEyeVision Local
#ifndef DefaultDir
  #define DefaultDir AppName
#endif

#ifndef AppPublisher
  #define AppPublisher "SunEyeVision"
#endif

#ifndef AppExeName
  #define AppExeName "SunEyeVision.UI.exe"
#endif

; 安装包图标来源 = 应用程序自身的图标（与 src/UI/SunEyeVision.UI.csproj 的 ApplicationIcon 内容一致）
#ifndef AppIcon
  #define AppIcon "..\src\UI\Icons\app_logo.ico"
#endif

; Release 载荷目录（= Directory.Build.props 的 Release OutputPath，与产品版本解耦）
#ifndef ReleaseDir
  #define ReleaseDir "..\Release\SunEyeVision_v1.0.0\SunEyeVision"
#endif

#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

; 产物名：正式版不带渠道名，其余带（由打包脚本注入；此处仅为本地双击编译的默认值）
#ifndef OutputBaseName
  #define OutputBaseName "SunEyeVision-Setup-" + AppVersion + "-" + Channel + "-x64"
#endif

[Setup]
AppId={#AppId}
; Runtime marker lives until process exit, including save/cleanup and cancelled close prompts.
AppMutex=Global\SunEyeVision.Running
AppName={#AppDisplayName}
AppVersion={#ReleaseVersion}
AppPublisher={#AppPublisher}
; 安装包自身的版本资源：不声明则右键属性"版本"为空
; （VersionInfoVersion 必须是四段纯数字，故产品版本采用四段方案，见 rule-022）
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoProductTextVersion={#ReleaseVersion}
VersionInfoProductName={#AppDisplayName}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppDisplayName} 安装程序
VersionInfoCopyright=Copyright (C) SunEyeVision
DefaultDirName={autopf}\{#DefaultDir}
DefaultGroupName={#AppDisplayName}
; Do not reuse the obsolete Preview directory/group as the new default.
UsePreviousAppDir=no
UsePreviousGroup=no
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseName}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayName={#AppDisplayName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile={#AppIcon}

[Files]
Source: "{#ReleaseDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; 文件关联（仅当前用户 HKCU，无需管理员）：使 .solution 在资源管理器中显示应用图标，且双击可打开。
; 与程序内 FileAssociationService 写入同一 ProgID（SunEyeVision.SolutionFile）；安装后即刻生效，
; 不再依赖"程序首次运行"（旧行为导致注册表指向失效路径后永不更新）。
#if Channel != "local"
Root: HKCU; Subkey: "Software\Classes\.solution"; ValueType: string; ValueName: ""; ValueData: "{#AssociationId}"; Check: ShouldRegisterDefaultAssociation
Root: HKLM; Subkey: "Software\SunEyeVision"; ValueType: string; ValueName: "InstallLocation"; ValueData: "{app}"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Applications\SunEyeVision.UI.exe\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\Icons\solution.ico"",0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\SunEyeVision.UI.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Flags: uninsdeletekey
#else
Root: HKLM; Subkey: "Software\SunEyeVision\Local"; ValueType: string; ValueName: "InstallLocation"; ValueData: "{app}"; Flags: uninsdeletevalue
#endif
Root: HKCU; Subkey: "Software\Classes\.solution\OpenWithProgids"; ValueType: string; ValueName: "{#AssociationId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\{#AssociationId}"; ValueType: string; ValueName: ""; ValueData: "{#AppDisplayName} 解决方案文件"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\{#AssociationId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\Icons\solution.ico"",0"
Root: HKCU; Subkey: "Software\Classes\{#AssociationId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\{#AssociationId}\shell\edit\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

[InstallDelete]
Type: files; Name: "{autoprograms}\{code:GetPreviousShortcutName}.lnk"
Type: files; Name: "{autodesktop}\{code:GetPreviousShortcutName}.lnk"
#if Channel != "local"
Type: files; Name: "{autoprograms}\SunEyeVision Preview.lnk"
Type: files; Name: "{autodesktop}\SunEyeVision Preview.lnk"
#endif

[Icons]
Name: "{autoprograms}\{#AppDisplayName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppDisplayName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  MaintenanceHandle: LongWord;
  MaintenanceOwned: Boolean;

function CreateMaintenanceMutex(Attributes: LongWord; InitialOwner: Boolean; Name: String): LongWord;
  external 'CreateMutexW@kernel32.dll stdcall';
function WaitForMaintenanceMutex(Handle, Milliseconds: LongWord): LongWord;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function ReleaseMaintenanceMutex(Handle: LongWord): Boolean;
  external 'ReleaseMutex@kernel32.dll stdcall';
function CloseMaintenanceHandle(Handle: LongWord): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

procedure ReleaseMaintenanceGate;
begin
  if MaintenanceHandle <> 0 then
  begin
    if MaintenanceOwned then ReleaseMaintenanceMutex(MaintenanceHandle);
    CloseMaintenanceHandle(MaintenanceHandle);
    MaintenanceHandle := 0;
    MaintenanceOwned := False;
  end;
end;

{ 0 = stopped, 1 = running, 2 = cannot verify. Covers legacy versions without AppMutex. }
function ApplicationRunningState: Integer;
var
  Locator, Services, Processes: Variant;
begin
  if CheckForMutexes('Global\SunEyeVision.Running') then
  begin
    Result := 1;
    Exit;
  end;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Services := Locator.ConnectServer('', 'root\CIMV2');
    Processes := Services.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name = ''SunEyeVision.UI.exe''');
    if Processes.Count > 0 then Result := 1 else Result := 0;
  except
    Log('Cannot verify SunEyeVision process state: ' + GetExceptionMessage);
    Result := 2;
  end;
end;

function RequireApplicationStopped: Boolean;
var
  State, Answer: Integer;
begin
  Result := False;
  State := ApplicationRunningState;
  while State <> 0 do
  begin
    if UninstallSilent then
    begin
      Log('Uninstall blocked: SunEyeVision is running or process state cannot be verified.');
      Exit;
    end;
    if State = 1 then
      Answer := SuppressibleMsgBox(
        'SunEyeVision 仍在运行。请先保存工作并退出所有 SunEyeVision 窗口，再点击“重试”。' + #13#10 +
        '如果取消软件的退出提示，程序仍在运行，不能卸载。点击“取消”退出卸载，不会删除程序文件。',
        mbInformation, MB_RETRYCANCEL, IDCANCEL)
    else
      Answer := SuppressibleMsgBox(
        '无法确认 SunEyeVision 是否已退出，已阻止卸载。请检查系统进程查询服务后重试，或取消卸载。',
        mbError, MB_RETRYCANCEL, IDCANCEL);
    if Answer <> IDRETRY then Exit;
    State := ApplicationRunningState;
  end;
  Result := True;
end;

function InitializeUninstall: Boolean;
var
  WaitResult: LongWord;
begin
  Result := False;
  MaintenanceHandle := CreateMaintenanceMutex(0, False, 'Global\SunEyeVision.Maintenance');
  if MaintenanceHandle <> 0 then
  begin
    WaitResult := WaitForMaintenanceMutex(MaintenanceHandle, 0);
    MaintenanceOwned := (WaitResult = 0) or (WaitResult = $80);
  end;
  if not MaintenanceOwned then
  begin
    SuppressibleMsgBox('无法取得卸载保护锁。请等待其他维护操作或程序启动完成后重试。',
      mbError, MB_OK, IDOK);
    ReleaseMaintenanceGate;
    Exit;
  end;
  Result := RequireApplicationStopped;
  if not Result then ReleaseMaintenanceGate;
end;

procedure DeinitializeUninstall;
begin
  ReleaseMaintenanceGate;
end;

function ShouldRegisterDefaultAssociation: Boolean;
var
  Existing: String;
begin
  if not RegQueryStringValue(HKCU, 'Software\Classes\.solution', '', Existing) then
    RegQueryStringValue(HKCR, '.solution', '', Existing);
  Result := (Existing = '') or (Pos('SunEyeVision.', Existing) = 1) or
    (Existing = 'Applications\SunEyeVision.UI.exe');
end;

function GetPreviousShortcutName(Param: String): String;
begin
#if Channel == "local"
  Result := GetPreviousData('ShortcutName', 'SunEyeVision Local');
#else
  Result := GetPreviousData('ShortcutName', 'SunEyeVision');
#endif
  { Only allow a single SunEyeVision shortcut filename, never a directory/path. }
  if (Pos('SunEyeVision', Result) <> 1) or (Pos('\', Result) > 0) or
     (Pos('/', Result) > 0) or (Pos(':', Result) > 0) or
     (Pos('*', Result) > 0) or (Pos('?', Result) > 0) then
    Result := '{#AppDisplayName}';
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'ShortcutName', '{#AppDisplayName}');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  CurrentProgId: String;
begin
  if CurUninstallStep = usAppMutexCheck then
    if not RequireApplicationStopped then
      RaiseException('SunEyeVision 未退出，卸载已取消。');
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Classes\.solution', '', CurrentProgId) then
      if CurrentProgId = '{#AssociationId}' then
        RegDeleteValue(HKCU, 'Software\Classes\.solution', '');
end;
