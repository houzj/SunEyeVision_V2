# framework 进度

更新时间：2026-11-05 16:00
当前状态：framework lane **无 claimed/pending 卡**。T-0006 已按用户决定作废（自建机制按原厂流程拆除）；T-0007/T-0008 交付完成；T-0001/T-0002/T-0003 已 done。

## 一、本轮新结论（2026-11-05 人工验收后）

- **T-0006 作废（superseded）**：用户定调「数据根这套是自建机制，按 VM 流程拆掉」。已删 `App.xaml.cs` 的启动期「数据目录」提示块、`ApplicationDataPaths.IsRootNewlyCreated` 与专用 `OpenConfiguredRoot`；配置根分支统一走 `Open(...)`。
- **H-0013 取 A 并已落地**：`installer/Release.Common.ps1` 的 `New-ReleasePayload` 不再收录 `App.ini`。理由：配置项不该进二进制完整性清单，否则改一次数据根就得重封包。
- **实测闭环**：只改 `App.ini` 的 `Root`、**不重封包** → 程序 44 秒正常进主界面，新根目录自动创建，无 SHA 报错、无提示框；清单 591 条、`ManifestContainsAppIni=False`。

## 二、已完成（可核验）

- [T-0001] C2 文件延迟删除失败改 Warning（`FileAccessManager.cs:346`）、C4 批量更新失败改 Warning（`EnhancedBatchUpdateManager.cs:196`）、D4b 只读日志文件不阻断启动 + D4 清理失败记 Warning、D3 日志根不再静默落 `%TEMP%`（`NodeDisplayAdapterConfig.cs:243-254`）；`src/` 内 `GetTempPath` 0 命中
- [T-0002] 10 条 App.ini 契约测试（`ApplicationDataPathsConfigurationTests.cs`，独立串行集合、不碰 AppContext、盘符 `DriveInfo` 探测）
- [T-0003] 图像源条可见性改绑 `HasActiveImageSource` + 唯一写入口 `SetActiveImageSourceBinding`；XAML 只改绑定名，`StackPanel` 包裹层保留；回归断言在 `SourceAndVariablesSessionTests.cs`
- [T-0006] **已拆除**：启动期提示块删除；`IsRootNewlyCreated`/`OpenConfiguredRoot` 删除；原 3 条断言替换为 2 条语义用例（配置根会被创建 / `[Other]` 段被跳过）
- [T-0007] `tests/SunEyeVision.RootCanvas.Tests` 7 文件删除；`RootCanvasHostProbe.csproj:3` 两条悬空 `Compile Include` 清除；全仓 csproj/slnx/props/targets 对该目录引用 0
- [T-0008] 启动期读一次文件写入失败计数：`NodeDisplayAdapterConfig.cs:257-258`（`_fileLogWriter` 由死字段改为真正持有）+ `:272-295`（`ReportFileLogWriteFailure()`，队列排空上限 300ms、线程池等待避免 UI 死锁）；调用点 `App.xaml.cs`（`InitializeServices()` + `LogRuntimeIdentity()` 之后）
- [H-0013] 载荷清单排除 `App.ini`（`Release.Common.ps1:289`）

## 三、实测数字

| 项 | 命令 | 结果 |
|---|---|---|
| 全量 Release 构建（最终） | `dotnet build SunEyeVision.slnx -c Release --no-incremental` | **0 错误 / 6144 警告 / 1:11**（`.codebuddy\tmp\build-vmflow.log`），`Build payload sealed: 31DB9D57…` |
| 载荷清单 | 读 `release-payload.json` | 591 条，`ManifestContainsAppIni=False` |
| 改配置不重封包启动 | 只改 `App.ini` → 启动 exe | 44 秒进主界面，新根自动创建，无报错/无提示 |
| 数据根配置测试 | `--filter ApplicationDataPathsConfigurationTests` | **12 通过 / 0 失败** |
| 日志自检反例 | 6 次起停读当日日志 | 无「日志文件写入已失败」 |

## 四、未验项

- T-0008 **正例**（日志目录不可写 → 出现「日志文件写入已失败 …」）：未做，回执给了可照抄步骤。
- 原厂（VM）在数据根缺失时的确切行为：本次按用户口径执行，未另取证据。
- `tests/RootCanvasHostProbe` 本体仍不在 `SunEyeVision.slnx`，未单独构建（去留另议）。
- 未跑全量 `dotnet test`（本轮只跑数据根 12 条 + 日志路由 4 条）。

## 五、越界、夹带与现场恢复

- 本轮为验收临时改过 `Release\…\SunEyeVision\App.ini` 与 `release-payload.json` 的 App.ini 条目（因当时清单仍绑定该文件）。**已全部还原**：`App.ini` 回到 `[Data] Root=D:\SunEyeVisionData`；新规则下该文件不再参与校验，无需重封包。
- 验收期间建的 `D:\SunEyeVisionData_probe`、`_probe2`、`_probe_tmp` 三个测试根**已删除**，正式根 `D:\SunEyeVisionData` 未动。
- 曾误留一个 `dotnet SunEyeVision.UI.dll` 实例占着单实例互斥体，导致后续每次启动被静默 Shutdown（表现为"没看到程序"）；已清理全部残留宿主进程，并核对 `Win32_Process` 命令行确认无残留。
- 未 commit / push。本地证据：`.codebuddy/tmp/*.log`、`*.ps1`（诊断脚本，不在交付面内）。
- 登记未动手：`NodeDisplayAdapterConfig.ResolveProjectRoot()` 为无人调用的私有方法且注释已过期，属遗留清理批次。
