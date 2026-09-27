# 05 测试与验收（Step 9 / Step 10）

## 1. 三层测试策略（新增厂商必须补齐）

| 层 | 依赖 | 目的 | 参照实现 |
|---|---|---|---|
| 契约单测 | 无硬件、无 SDK | 锁住 `CameraFrame` 释放、请求默认值、描述符稳定身份 | `tests/SunEyeVision.Plugin.SDK.Tests/CameraContractTests.cs` |
| 运行时语义 | 模拟相机 | 锁住注册/去重/独占会话/释放后可重开 | `tests/SunEyeVision.DeviceDriver.Tests/CameraRuntimeManagerTests.cs` |
| 真机诊断 | 真实设备 | 锁住发现、连接、身份、启动恢复 | `RealMvsConnectionDiagnosticTests.cs`、`RealCameraRestoreDiagnosticTests.cs` |

## 2. 现有范式（照抄，不另造）

| 文件 | 关键手法 |
|---|---|
| `CameraContractTests.cs` | xUnit `[Fact]`；帧只释放一次(:12-26)、触发默认值与有限超时(:29-36)、描述符稳定身份(:39-54)、参数快照作用域(:57-72) |
| `CameraRuntimeManagerTests.cs` | 用**模拟相机**（`using ...Simulation;` :4）验证：发现即注册并可采集(:13-38)、第二个独占会话被拒(:40-56)、会话释放后可重开(:58-72)。**无需真机** |
| `RealMvsConnectionDiagnosticTests.cs` | `[Fact(Timeout = 30000)]` + `[Trait("Category","RealHardware")]`(:25-26)；硬编码期望序列号(:20)；`Assert.Single(matching)`(:43) 与 `DeviceId == "hikvision-mvs:{serial}"`(:45) |
| `RealCameraRestoreDiagnosticTests.cs` | `[Trait("Category","RealHardware")]`(:24)；`ServiceInitializer.InitializeServices / InitializeSolutionManager`(:31-32)；`RestoreConnectionsForCurrentSolutionAsync`(:54)；`finally` 释放运行时(:59) |
| `SolutionLoadRegressionTests.cs` | 超时封装 `RunWithTimeoutAsync`(:195)、**STA 线程范式** `RunOnStaWithTimeoutAsync`(:213，`SetApartmentState(ApartmentState.STA)` :234)；读取方案 `Devices` 用 `JsonDocument` 遍历断言 `Id`(:36-63)，不引入 UI 依赖 |

**真机用例的无设备策略**：靠 `[Trait("Category","RealHardware")]` 过滤，而不是在测试里写跳过逻辑。

## 3. 测试工程配置（新增测试工程时）

`tests/SunEyeVision.DeviceDriver.Tests/SunEyeVision.DeviceDriver.Tests.csproj`：

- 目标框架 `net9.0-windows`(:3)、`PlatformTarget x64`(:4)
- `IsTestProject`(:8)、`Microsoft.NET.Test.Sdk 17.*`(:11)、`xunit 2.*`(:12)
- `ProjectReference` → `SunEyeVision.DeviceDriver`(:23)
- **不直接引用厂商 SDK**：SDK 由被引工程按 `DefineConstants` 条件引入

登记进 `SunEyeVision.slnx` 的格式见 `04-runtime-wiring.md` §4。

## 4. 命令

```powershell
# 构建（当前仓库标准命令）
dotnet build SunEyeVision.slnx --configuration Debug -v:minimal -nologo

# 全量测试
dotnet test SunEyeVision.slnx --configuration Debug -v:minimal -nologo

# 排除真机用例（无设备环境）
dotnet test SunEyeVision.slnx --configuration Debug --filter "Category!=RealHardware"
```

## 5. 12 条验收的可执行做法

| # | 验收项 | 怎么验（可执行） |
|---|---|---|
| 1 | 发现与稳定身份 | 连续两次 `Discovery.DiscoverAsync()`，断言两次 `DeviceId` 集合完全相同 |
| 2 | 重复发现不重复登记 | 对同一 Provider 重复调用发现后 `Runtime.Devices.Count` 不变 |
| 3 | 连接 / 断开 / 重复连接 | `OpenAsync` 成功后再次 `OpenAsync` 同一 ref ⇒ 断言抛明确错误（`DeviceBusy` 或等价） |
| 4 | 单次与连续采集 | `CaptureAsync` 得到帧；`StartStreamAsync` 收 N 帧后 `StopStreamAsync`；断言帧均被释放 |
| 5 | 软件触发与超时 | 断网/遮光等无帧场景下 `ExecuteSoftwareTriggerAsync` ⇒ 断言 `TriggerTimeout` |
| 6 | 参数读写 | `GetParameterSchemaAsync` 取一个可写参数 → `ApplyParametersAsync` → `ReadParametersAsync` 回读一致；对只读参数写入 ⇒ 断言被拒 |
| 7 | 关管理器不影响连接 | 关闭 `CameraManagerDialog` 后断言 `Runtime.TryGetSession(ref, out _)` 仍为 true |
| 8 | 关预览不改变预览前状态 | 未连接设备开预览 → 关闭 → 断言回到未连接；已连接设备同理保持 |
| 9 | 保存方案后重启恢复 | 保存方案 → 重启 → 断言设备被重新发现并绑定（`AutoConnect` 生效） |
| 10 | SDK 缺失 / 离线 / 被占 | 三种情形分别断言错误码 `DriverUnavailable` / `CameraNotFound` 或 `ConnectionTimeout` / `DeviceBusy` |
| 11 | 加载失败仍进主界面 | 让恢复流程失败/超时 ⇒ 断言 10s 内主界面可用（`CameraRestoreTimeout`，`App.xaml.cs:49`） |
| 12 | 测试全绿 | `dotnet test`（真机用例按 Trait 过滤）与构建均无 error |

## 6. 新增厂商时要补的测试（最低清单）

1. `XxxCameraAdapterFactory.CanCreate` 的正/负判定（含与 MVS 工厂**互不误吞**）。
2. 无 SDK 时：工厂 `Create` 抛 `DriverUnavailable`，发现器返回空。
3. 有设备时：发现 → 连接 → 单帧采集 → 连续采集 → 软触发 → 读写一个关键参数 → 断开。
4. 真机诊断用例（`RealHardware` Trait）：断言稳定身份（序列号→DeviceId）与方案重启恢复。
