; 依赖：Inno Setup 6.7.3（编译器本体）。官方下载：https://jrsoftware.org/isdl.php
; 说明：第三方安装器体积 10.3MB，**不入库**（见 .gitignore 的 installer/third-party/），
;       本机自行安装 Inno Setup 6.7.3 后即可用本脚本编译生成安装包。
;
; ★ 版本号单一入口：版本/渠道/显示名/AppId/安装目录/载荷目录/输出目录**一律由打包脚本注入**
;   （见 installer\Pack-Release.ps1）。注入方式：脚本编译前生成 .generated\version.iss，
;   本文件用 #ifexist + #include 引入 —— 不用命令行 /D，规避中文与编码问题。
;   本文件内所有 #define 仅是「直接双击编译」时的本地默认值，不作为版本真源（rule-022）。

#ifexist ".generated\version.iss"
  #include ".generated\version.iss"
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

; 安装向导与程序组显示名（内测版注入 "SunEyeVision（内测版）"）
#ifndef AppDisplayName
  #define AppDisplayName AppName
#endif

; 安装标识：覆盖升级用同一 AppId；与正式版并存时由脚本注入独立 AppId
#ifndef AppId
  #define AppId "{{53C3A889-9EEA-4AAC-A25B-1435DB5D29B5}"
#endif

; 安装目录名（autopf 下）：默认 SunEyeVision；并存模式注入 "SunEyeVision Alpha"
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
AppName={#AppDisplayName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
; 安装包自身的版本资源：不声明则右键属性"版本"为空
; （VersionInfoVersion 必须是四段纯数字，故产品版本采用四段方案，见 rule-022）
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoProductName={#AppDisplayName}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppDisplayName} 安装程序
VersionInfoCopyright=Copyright (C) SunEyeVision
DefaultDirName={autopf}\{#DefaultDir}
DefaultGroupName={#AppDisplayName}
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
Root: HKCU; Subkey: "Software\Classes\.solution"; ValueType: string; ValueName: ""; ValueData: "SunEyeVision.SolutionFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.solution"; ValueType: string; ValueName: "Content Type"; ValueData: "application/vnd.suneyvision.solution"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SunEyeVision.SolutionFile"; ValueType: string; ValueName: ""; ValueData: "SunEyeVision 解决方案文件"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SunEyeVision.SolutionFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\Icons\solution.ico"",0"
Root: HKCU; Subkey: "Software\Classes\SunEyeVision.SolutionFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\SunEyeVision.SolutionFile\shell\edit\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

[Icons]
Name: "{autoprograms}\{#AppDisplayName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppDisplayName}"; Flags: nowait postinstall skipifsilent
