; 依赖：Inno Setup 6.7.3（编译器本体）。官方下载：https://jrsoftware.org/isdl.php
; 说明：第三方安装器体积 10.3MB，**不入库**（见 .gitignore 的 installer/third-party/），
;       本机自行安装 Inno Setup 6.7.3 后即可用本脚本编译生成安装包。
#define AppName "SunEyeVision"
#define AppVersion "1.0.0"
#define AppPublisher "SunEyeVision"
#define AppExeName "SunEyeVision.UI.exe"
#define ReleaseDir "..\Release\SunEyeVision_v1.0.0\SunEyeVision"

[Setup]
AppId={{53C3A889-9EEA-4AAC-A25B-1435DB5D29B5}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=SunEyeVision-Setup-{#AppVersion}-x64
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayName={#AppName}

[Files]
Source: "{#ReleaseDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent
