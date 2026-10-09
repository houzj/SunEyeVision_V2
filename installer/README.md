# 正常发布打包

双击 `Pack-Release.cmd` 即正常发布，或运行：

```powershell
pwsh -NoProfile -File installer/Pack-Release.ps1
```

要求 PowerShell 7、.NET SDK、Inno Setup 6，以及已生成的依赖锁。
签名证书、干净工作区、VM 安装验收均不是正常打包的强制条件；清单如实记录源码状态、签名和安装验收结果。
不自动提交代码、创建标签或安装程序；请在发布完成后提交源码和 version.json，按项目流程管理标签。

version.json 的三段 version 由维护者决定；fileRevision 是上次成功发布序号，正常发布自动 +1。
序号跨 alpha/beta/rc/stable 统一递增；失败不增加序号，成功才原子写回版本及发布日期。
旧 channelRevision 已由 fileRevision 统一取代，成功发布时自动移除。

```text
version.json: version=2.0.0 / channel=alpha / fileRevision=1
                |
只读预检 -> 独占锁 -> 预留 2.0.0.2（尚不写回）
                |
源码文件快照 -> 锁定还原 -> 隔离全量 Release 构建
                |
自动测试 -> 自包含 win-x64 publish -> 版本/默认暗色探针
                |
可选签名 -> ISCC -> 可选安装验收
                |
源码未变化校验 -> 哈希/清单 -> 移动产物
                |
成功：version.json.fileRevision=2，更新发布日期
失败：保留诊断目录，版本序号和旧产物不变
```

产物示例：

```text
dist/2.0.0.2_alpha/
  SunEyeVision-Setup-2.0.0.2_alpha-x64.exe
  SunEyeVision-Setup-2.0.0.2_alpha-x64.exe.sha256
  release-manifest-2.0.0.2_alpha.json
```

程序与 Windows 文件版本都是 2.0.0.2，程序渠道为 alpha，关于界面显示内测版。
NuGet SDK 包单独映射为 2.0.0-alpha.2；CLR 绑定版本与插件契约保持各自兼容规则。
stable 的文件名省略渠道后缀，例如 SunEyeVision-Setup-2.0.0.5-x64.exe。

alpha/beta/rc/stable 的安装向导、快捷方式、卸载条目统一为 `SunEyeVision <完整发布版本>`，例如 `SunEyeVision 2.0.0.4_alpha`；默认目录仍为 `C:\Program Files\SunEyeVision`。
正常渠道共享安装 AppId，保留此前 alpha 安装包的身份以识别升级；渠道不再作为不同产品并存。
`local` 显示名同样含版本，但目录仍为独立的 `SunEyeVision Local`。升级向导不沿用旧 Preview 默认目录，并精确清理旧 Preview/无版本及本安装上一版本的快捷方式；不会自动删除旧目录或用户数据。
此前已安装到 Preview 目录的用户，建议先卸载旧版（保留个人方案/设置）再安装新包，避免旧目录残留；实际迁移仍须安装验收。

再次正常发布自动变成 2.0.0.3_alpha；源码有变化必须发布新版本。
重打当前已发布版本使用 `-Repack`，要求与该版本源码快照一致，不递增序号，输出 dist/repack 下的独立构建目录。
本地临时包使用 `Pack-Local.cmd` 或 `-Local`，不递增发布序号，输出 dist/local。
只检查条件使用 `-CheckOnly`，不构建、不修改版本。

可选参数：

- `-RequireClean`：额外要求源码已提交。
- `-CertificateThumbprint ... -SignToolPath ...`：启用签名并验证签名；不提供时生成正常渠道的未签名安装包。
- `-InstallationTestScript ...`：启用隔离安装验收；内置 Test-Installation.ps1 要求验收 VM 环境变量 SUNEYEVISION_ACCEPTANCE_VM=1。

清单包含产品/文件版本、渠道、日期、SDK 包映射、Git HEAD、未提交文件列表、完整源码文件哈希、测试结果、默认主题及载荷哈希。
源码未提交时 Git SHA 仅标识基线，源码快照才代表本次实际输入，清单不会声称已提交或已签名。
程序自包含 .NET 运行时，HALCON 许可及厂商相机 SDK 仍按现场条件配置。

回归检查：`pwsh -File installer/Test-Release.ps1`。

安装后通过“帮助 → 关于”查看完整发布版本、渠道、发布日期、文件版本、代码基线、实际运行环境和程序目录，可复制诊断信息。
关于页使用动态主题资源，可滚动/调整大小；不提供在线检查更新，不声称已联网核验最新版本，升级采用手动安装新包。

卸载前必须退出所有 SunEyeVision 实例。卸载器先取得维护锁并检查运行标记及旧版本进程，再允许进入卸载。
发现程序仍运行时提示先保存并退出，提供重试/取消；取消软件自己的关闭提示不会放行卸载，取消卸载不会删除程序文件或清理关联。
检查失败也阻止卸载；静默卸载不等待交互，不强制结束进程。维护锁保持到卸载退出，新版本程序在维护期间拒绝启动。
程序运行标记保持到进程结束，包括保存、相机/通讯退出清理；窗口隐藏或取消关闭不会提前释放标记。
保护随新安装包的程序及卸载器生效，已经安装的旧卸载器不会因修改仓库而自动更新；先退出旧程序后安装新包。

2026-10-08 卸载保护集成包：`dist/2.0.0.5_alpha/SunEyeVision-Setup-2.0.0.5_alpha-x64.exe`。
全量 Release 0 错误，定向测试 125/125，脚本检查 82 项通过；实际新 Release 无设备探针验证维护期间拒绝启动、释放后恢复，安装脚本编译和安装包哈希通过。
尚未执行实际安装/卸载：运行中卸载、取消软件关闭、取消卸载及重试完成后的文件/关联状态须在隔离环境验收；不将该版本标记为安装验收已完成。

2026-10-08 最新集成包为 `dist/2.0.0.4_alpha/SunEyeVision-Setup-2.0.0.4_alpha-x64.exe`，安装显示名称为 `SunEyeVision 2.0.0.4_alpha`。
全量 Release 0 错误，定向测试 122/122，发布脚本检查 75 项通过；两种主题实际渲染、程序版本/暗色探针、载荷与安装包哈希通过。
安装和快捷方式升级仍待现场验收；版本计数为 4，下一次正常发布为 2.0.0.5_alpha。

2026-10-08 安装品牌修正版已发布：`dist/2.0.0.3_alpha/SunEyeVision-Setup-2.0.0.3_alpha-x64.exe`。
实际安装包产品名称为 SunEyeVision，版本 2.0.0.3_alpha；清单记录统一品牌和 `{autopf}\SunEyeVision` 默认路径。
全量 Release 0 错误，定向测试 120/120，发布脚本检查 72 项通过；暗色探针、版本、源码快照、SHA256 核对通过。
尚未在本机执行安装/卸载或验证旧目录迁移；计数器为 3，下次正常发布为 2.0.0.4_alpha。

2026-10-08 已完成正常发布验收：自动流水线生成 `dist/2.0.0.2_alpha/` 的三项产物，全量 Release 0 错误、定向测试 120/120、脚本检查 52 项通过。
程序自报产品版本 2.0.0.2、alpha 渠道、当日发布日期及默认 Dark 主题，19 个插件载荷与安装包哈希核验通过。
发布计数器已更新为 2，下次正常发布预检显示 2.0.0.3_alpha；源码快照与原版本重打条件一致。
