# 04 运行时装配与接线点（Step 6 / Step 8）

## 1. 唯一装配点：`CameraRuntimeComposition`（必改）

`src/DeviceDriver/Cameras/CameraRuntimeComposition.cs`：

```csharp
public sealed class CameraRuntimeComposition : IAsyncDisposable          // :11
{
    public CameraRuntimeComposition()                                    // :13
    {
        MvsDiscovery = new MvsCameraDiscoveryProvider();                 // :15
        Runtime = new CameraRuntimeManager(new ICameraAdapterFactory[]   // :16-19
        {
            new MvsCameraAdapterFactory(MvsDiscovery),
        });
        Discovery = new CameraDiscoveryCoordinator(new ICameraDiscoveryProvider[]  // :20-23
        {
            MvsDiscovery,
        });
    }

    public CameraRuntimeManager Runtime { get; }                          // :26
    public CameraDiscoveryCoordinator Discovery { get; }                  // :27
    public MvsCameraDiscoveryProvider MvsDiscovery { get; }               // :28
    public ValueTask DisposeAsync() => Runtime.DisposeAsync();            // :30
}
```

新增厂商时：
1. 在构造里 new 出该厂商的发现器；
2. 加进 `Runtime` 的工厂数组；
3. 加进 `Discovery` 的发现器数组；
4. 按需暴露只读属性（便于诊断测试取用）；
5. `DisposeAsync` 一般无需改（运行时统一释放全部会话）。

> 这是**唯一**允许登记厂商发现器/工厂的位置。UI、Workflow、ServiceInitializer 都不得出现厂商分支。

## 2. 宿主装配与生命周期（**不需要改**）

| 位置 | 事实 | 是否需改 |
|---|---|---|
| `src/UI/Features/WorkflowUI/Canvas/Adapters/NodeDisplayAdapterConfig.cs`（文件名与内容不符，内含 `ServiceInitializer`） | `ServiceInitializer`(:67)、`_cameraRuntime`(:71)、`CameraRuntime => _cameraRuntime ??= new CameraRuntimeComposition()`(:97-98)、`DisposeCameraRuntimeAsync()`(:100-106) | ❌ 不改 |
| `src/UI/App.xaml.cs` | 恢复超时常量 `CameraRestoreTimeout = 10s`(:49)；启动恢复调用(:244、:342)；实现 `RestoreCameraConnectionsAsync`(:553-585)；退出释放(:737) | ❌ 不改 |
| `src/Workflow/SolutionRepository.cs` | 设备列表持久化：`persistedDevices`(:125)、反序列化前 `ReadPersistedDevices`(:146)、`RemoveDevicesProperty(json)`(:153)；实现 `RemoveDevicesProperty`(:177-221，跳过 `Devices` 于 :183)、`ReadPersistedDevices`(:224-273) | ❌ 不改 |
| `src/Workflow/WorkflowJsonConverter.cs` | 只处理 Workflow 的 Nodes/Connections(:70-103、:183-206)，**不涉及设备** | ❌ 不改 |
| `src/UI/Features/Camera/CameraManagerViewModel.cs` | 运行时来源 `ServiceInitializer.CameraRuntime`(:327)；重发现(:460-465)；持久化设备 ↔ 描述符匹配 `FindMatchingDescriptor`(:538-554)；回写身份并 `PersistCameraList()`(:486-509) | ❌ 不改 |

**匹配规则（新厂商必须满足，否则方案重启后无法自动绑定）**：`FindMatchingDescriptor`(:538-554) 依次按

1. `DeviceId` 相等；
2. `SerialNumber + Manufacturer + Transport` 相等；
3. `MacAddress + Transport` 相等。

⇒ 只要发现器产出的描述符具备上述任一稳定组合，持久化设备就能重新绑定，UI 零改动。

## 3. 方案持久化与启动恢复链路（Step 8 要验证的既有行为）

```
加载方案
  → SolutionRepository.ReadPersistedDevices(root, …)       // :146、:224-273
  → 移除 Devices 属性后再反序列化 Solution                 // RemoveDevicesProperty :153、:177-221
  → App.RestoreCameraConnectionsAsync(...)                 // App.xaml.cs:553-585
  → CameraManagerViewModel.RestoreConnectionsForCurrentSolutionAsync
       · 重新发现真实设备
       · 按 §2 匹配规则重新绑定（回写 DeviceId/Serial/Ip/Transport）
       · 按 IsEnabled / AutoConnect 恢复连接
  → 连接成功 / 失败 / 超时 10s（CameraRestoreTimeout :49）后继续显示主界面
```

**持久化只保存身份与用户配置**（`DeviceId`、序列号、MAC、IP、厂商、型号、传输方式、用户名称、`IsEnabled`、`AutoConnect`），**不保存** SDK 句柄或连接对象；连接状态属于运行时。

## 4. 测试工程登记（若新测试工程）

`SunEyeVision.slnx` 采用 `Folder + Project` 结构，示例（`:120-125`）：

```xml
<Folder Name="/Tests/SunEyeVision.UI.Tests/">
  <Project Path="tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj" />
</Folder>
```

新工程用同样的 `/Tests/<工程名>/` 形式追加；**顶层必须是 `<Solution>`**（另有一份 `SunEyeVision.App.slnx` 同格式）。

## 5. 必改 vs 不需改（汇总）

| 必改 | 不需改（厂商无关层） |
|---|---|
| 新增 `src/DeviceDriver/Cameras/<Vendor>/**` 三件套（+ 参数映射） | `src/Plugin.SDK/Devices/Camera/*`（除非新增 transport/protocol/error code） |
| `CameraRuntimeComposition.cs:13-23` 登记发现器与工厂 | `src/DeviceDriver/Cameras/Contracts/*`（除非新增能力位/契约成员） |
| 使用厂商 SDK 时：`SunEyeVision.DeviceDriver.csproj:10-27`（探测 + 条件 Reference + DefineConstants） | `src/DeviceDriver/Cameras/Simulation/*`（**仅测试用**，运行时不装：`CameraRuntimeComposition` 未注册它） |
| 新测试工程登记进 `SunEyeVision.slnx`（`:120-125` 格式） | `ServiceInitializer`、`App.xaml.cs`、`SolutionRepository.cs`、`WorkflowJsonConverter.cs`、`CameraManagerViewModel.cs` |
