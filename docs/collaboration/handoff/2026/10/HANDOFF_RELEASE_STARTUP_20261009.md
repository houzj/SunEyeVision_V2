# 普通 Release 直接启动修复交接

- 基线提交：1173baedc23eb879811f53242282c6072b10101a。
- 当前分支：main，D:\MyWork\SunEyeVision_V2。
- 当前执行者：Codex。
- 任务目标：普通 Release 构建输出 EXE 直接启动正常；不重复打包、不递增版本。
- 允许修改目录：Directory.Build.targets、构建回归 fixtures/utilities、installer/README.md、docs/collaboration；只对已验证默认输出目录的三个冲突宿主文件做可恢复移动。
- 已修改文件：Directory.Build.targets；tests/build/SunEyeVision.UI.proj、NonUiRuntimeGuard.proj；utilities/Test-RuntimeOutputGuard.ps1；installer/README.md；协作记录。

## 跨模块影响与实现

- 普通 Release 保持框架依赖，继续依赖本机 .NET 9 Desktop Runtime；安装包的自包含 win-x64 发布保持不变。没有改 UI 业务、工作流、SDK 契约、插件解析策略、版本或输出路径。
- UI Build 后仅在本工作区默认 dev/Debug 或 Release/SunEyeVision_v1.0.0/SunEyeVision 根目录，隔离 hostfxr.dll、hostpolicy.dll、coreclr.dll。三个文件是冲突宿主，其他业务/用户文件不动。
- 使用非递归 Move，备份路径唯一；源路径经过 GetFullPath 并与默认输出根做精确比较。移动失败使构建报错，不静默宣称输出正确。
- SelfContained=true、PackagingRoot 非空、DesignTimeBuild=true、自定义输出目录、非 UI 项目均跳过。工具目录、测试目录和安装包自包含运行时不受本规则修改。
- 真正旧目录三个文件已移动到 artifacts/runtime-host-backups/Release/ac07a333-a911-47dc-bcd1-d50700add77a，逐文件哈希与移动前一致；可恢复，不删除运行时或整个目录。
- 使用独立 fixtures 调用实际 MSBuild Target，不复制一份逻辑假测试。重复执行无新备份，用户配置 sentinel 保留。

## 验证

- dotnet restore SunEyeVision.slnx --locked-mode，成功。
- dotnet build SunEyeVision.slnx -c Release --no-restore：1146 警告，0 错误。
- 原路径 Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe 的真实 --release-probe 退出 0：2.0.0.8、alpha、Dark。没有用 dotnet DLL 绕过 apphost，也不初始化硬件。
- COREHOST_TRACE 确认 apphost 解析系统 hostfxr，dotnet root 为 C:\Program Files\dotnet，框架和 coreclr 为 shared\Microsoft.NETCore.App\9.0.20；WindowsDesktop 同为 9.0.20。
- 证据：artifacts/validation/release-startup-fix-20261009/host-trace.txt、probe.json。
- utilities/Test-RuntimeOutputGuard.ps1：52 项通过，包含 Release/Debug、备份内容、用户文件保留、幂等、自包含/打包/设计时/自定义输出/非 UI 边界。
- installer/Test-Release.ps1：90 项通过。
- 设计时 MSBuild 属性：OutDir/TargetPath 保持原 Release 根，SelfContained=false；可见 VS 设计器人工验收未执行。
- 已发布安装包 2.0.0.8_alpha SHA256 仍为 A1366218D19E1EFE9E48AC9C3FB1C5F08A048FC1904415DF6938C5612B645FB1；未改包、未增号、未安装或改系统关联。

## 重复构建与当前验收状态

- 只读确认用户启动的 PID 41724 是原 Release EXE，无探针参数，窗口“SunEyeVision 解决方案管理器”且响应正常，实际加载系统 hostfxr/coreclr，说明已进入正常 UI。用户尚未明确提交人工验收结果。
- 第二次 UI Release 构建曾被该用户进程锁住 Plugin.SDK.dll，MSB3027/MSB3021；用户确认关闭后，重新执行 dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore，928 警告、0 错误，耗时 37.93 秒。未强制结束用户进程。
- 重复构建后，再次直接执行原 Release EXE 的启动探针，退出 0，版本 2.0.0.8、Dark；证据 artifacts/validation/release-startup-fix-20261009/probe-after-rebuild.json。
- 默认 Release 根没有再次出现三个冲突宿主；实际 Release 备份目录仍只有原来 1 个，没有多余备份。version.json 仍为 8，已发布安装包 SHA256 未变化。
- 构建与真实 apphost 启动验证均已完成；实际正常窗口先前已只读观察到，用户明确人工验收和可见 VS 设计器验证仍待确认。
- 未提交或推送，保留此前打包改动、54 份锁、version.json=8 和 5 个用户未跟踪文件；未修改通讯工作区。
