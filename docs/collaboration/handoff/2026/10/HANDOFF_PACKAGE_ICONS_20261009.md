# 2.0.0.8_alpha 正常发布与图标关联交接

- 基线提交：1173baedc23eb879811f53242282c6072b10101a。
- 当前分支：main，D:\MyWork\SunEyeVision_V2；不改通讯分支。
- 当前执行者：Codex。
- 任务目标：用户批准正常打包，核对桌面/安装包/方案文件图标关联；回答旧 Release 的 .NET 弹窗原因。
- 允许修改目录：installer、Directory.Build.props、依赖锁、version.json、docs/collaboration；生成产物为 artifacts/staging 与 dist。
- 已修改文件：installer/Pack-Release.ps1、Release.Common.ps1、SunEyeVision.iss、Test-Release.ps1、README.md，Directory.Build.props，54 份 packages.lock.json，version.json，协作记录。

## 已完成

- 正常发布自动从 7 递增到 8，失败不递增；安装名称 SunEyeVision 2.0.0.8_alpha，默认目录 {autopf}\SunEyeVision。
- Setup 使用当前载荷中 suneyevision.ico，不再使用旧生成光之眼 ICO。
- 程序、开始菜单/桌面快捷方式、卸载条目使用带冰蓝背景应用图标；快捷方式显式图标路径/索引 0。
- .solution 使用独立文件页 solution.ico，正常安装更新本产品 ProgID/Applications\SunEyeVision.UI.exe 的命令与图标；不改 UserChoice，不抢其他软件的默认关联。ChangesAssociations=yes 刷新 Shell 关联。
- 载荷门禁新增两份 ICO 的存在性和源码哈希校验，清单记录图标路径；发布测试新增 BrandingLogo、单实例及基线整合回归。
- 修复普通还原丢失 win-x64 锁目标：RuntimeIdentifiers 只补充还原目标，普通构建仍框架依赖；54 份锁保留原依赖版本/内容哈希，新增 RID。发布继续 locked-mode，不在流水线内自动升级依赖。
- 保留用户 5 个本地未跟踪探测文件；不执行旧 Build-Icons.ps1、不安装、不写注册表、不清理旧 Release、不提交、不推送。

## 构建与测试

- installer/Test-Release.ps1：90 项检查通过。
- 打包隔离全量 Release：1144 警告，0 错误；测试 238/238 通过。
- 自包含 win-x64 EXE 启动探针退出 0：2.0.0.8、alpha、2026-10-09、Git 基线 1173baed、Dark、背景 #FF1E1E1E。探针不初始化硬件。
- 19 个插件程序集载荷通过；两份 ICO 源码/载荷哈希一致。
- Windows ExtractIconEx 从 Setup 和载荷 UI.exe 提取 48x48 图标，顶部背景均为 A255/R220/G239/B252，确认实际内嵌带背景图标。
- 产物：dist/2.0.0.8_alpha/SunEyeVision-Setup-2.0.0.8_alpha-x64.exe（249360316 字节）、对应 exe.sha256 与 release-manifest-2.0.0.8_alpha.json。
- 安装包 SHA256：A1366218D19E1EFE9E48AC9C3FB1C5F08A048FC1904415DF6938C5612B645FB1，清单和 sidecar 一致。
- 成功 staging：artifacts/staging/20261009-152946-1173baed-d8b21865；第一次旧锁失败目录 20261009-152724-1173baed-e1d79e99 保留诊断。

## 旧 Release 弹窗诊断

- 本机已安装 Microsoft.NETCore.App 与 Microsoft.WindowsDesktop.App 9.0.20。
- 旧 Release 目录 UI.exe 为 2.0.0.7、普通框架依赖 runtimeconfig（frameworks），但同时保留历史自包含 hostfxr/hostpolicy/coreclr。
- COREHOST_TRACE 确认从该目录加载 hostfxr，将该目录用作 dotnet root，在目录中找框架，报 No frameworks were found，退出 -2147450730。启动尚未进入业务代码，不是相机/通讯或本次单实例逻辑错误。
- 诊断子进程设置 DOTNET_DISABLE_GUI_ERRORS，不另弹图形提示，已正常失败退出；环境设置仅在诊断 PowerShell 子进程中临时生效，不修改用户环境变量。日志 artifacts/validation/baseline-sync-20261009/old-release-host-trace.txt。
- 旧目录仅做只读诊断，未获修复授权，不删除或移动其中任何文件；新包使用干净 staging，runtimeconfig 为 includedFrameworks 9.0.20，真实 EXE 探针通过。

## 未完成与下一步

- 安装/升级/卸载以及实际桌面、开始菜单、Explorer 文件关联验收待隔离 VM 或用户目标电脑；清单 installationVerified=false，未声称已安装验收或签名。
- Windows 图标缓存现场显示和 Visual Studio 设计器外观仍需人工确认。安装时需要桌面图标则勾选创建桌面快捷方式。
- 测试使用 dev/Debug 或新安装包，旧混合 Release 目录若需修复另行授权；不要因该目录错误重新下载安装 .NET。
- 发布脚本与 RID 锁补齐属于本次主分支未提交改动。维护者确认后提交并按项目流程同步，不自动改通讯工作区或远端。
