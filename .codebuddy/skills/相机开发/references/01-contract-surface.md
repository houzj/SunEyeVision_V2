# 01 契约面清单（实现前必读）

> 所有结论均带 `文件:行号`。**契约层只允许纯数据与服务接口**；带 SDK 语义的东西一律在 `DeviceDriver`。

## 1. Plugin.SDK：跨厂商稳定契约（`src/Plugin.SDK/Devices/Camera/`）

| 类型 | 种类 | 关键成员 |
|---|---|---|
| `CameraDescriptor` | `sealed record`（`CameraDescriptor.cs:33`） | `DeviceId`(:35)、`CameraRef`(:36)、`DisplayName`(:37)、`Manufacturer`(:38)、`Model`(:39)、`SerialNumber`(:40)、`IpAddress`(:41)、`MacAddress`(:42)、`Transport`(:43)、`Protocol`(:44)、`Metadata`(:45) |
| `CameraTransportKind` | enum（`:5`） | Unknown / GigEVision / Usb3Vision / CameraLink / GenTL / DirectShow / MediaFoundation / **Simulation** / File |
| `CameraProtocolKind` | enum（`:18`） | Unknown / GenICam / GigEVision / Usb3Vision / CameraLink / **VendorSdk** / Simulation / File |
| `CameraDeviceRef` | `sealed record`（`CameraDeviceRef.cs:7`） | `Value`、`IsEmpty`(:9)、`ToString()`(:11)。**只存逻辑引用**，不存 IP / 句柄 / 厂商类型(:4-5) |
| `CameraCapabilities` | `sealed record`（`CameraCapabilities.cs:8`） | `SupportsSoftwareTrigger`(:10)、`SupportsHardwareTrigger`(:11)、`SupportsContinuousCapture`(:12)、`SupportsRoi`(:13)、`SupportsExposure`(:14)、`SupportsGain`(:15)、`SupportedFeatures`(:16) |
| `CameraCaptureRequest` | `sealed record`（`CameraCaptureRequest.cs:16`） | `CameraRef`(:18)、`TriggerMode`(:19，默认 Software)、`Timeout`(:20，默认 3s)、`DiagnosticCorrelationId`(:22)、`Parameters`(:23)；`CameraTriggerMode` enum(:6) = Software / Hardware / Continuous |
| `CameraFrame` | `sealed class : IDisposable`（`CameraFrame.cs:11`） | `CameraRef`(:29)、`FrameId`(:30)、`Timestamp`(:31)、`Image`(:32)、`Metadata`(:33)、`IsDisposed`(:34)、`Dispose()`(:36，释放 Image 于 :46) |
| `CameraParameterSnapshot` | `sealed record`（`CameraParameterSnapshot.cs:9`） | `CameraRef`(:11)、`CapturedAt`(:12)、`Values`(:13) |
| `CameraState` | enum（`CameraState.cs:3`） | Offline / Connecting / Connected / StartingCapture / Capturing / StoppingCapture / Reconnecting / Faulted / Disposed |
| `CameraErrorCode` | enum（`CameraErrorCode.cs:3`） | None / CameraNotFound / ConnectionTimeout / ConnectionRejected / DeviceBusy / TriggerTimeout / FrameTimeout / ParameterUnsupported / StreamStopped / DeviceDisconnected / DriverUnavailable / OperationCancelled / Unknown |
| `ICameraCaptureService` | interface（`ICameraCaptureService.cs:9`） | `Task<CameraFrame> CaptureAsync(CameraCaptureRequest, CancellationToken)`(:11) |
| `IExecutionServices` | interface（`IExecutionServices.cs:6`） | `T? Get<T>() where T : class`(:8) |
| `CameraDiagnosticScope` | `sealed class : IDisposable`（`CameraDiagnosticScope.cs:9`） | `AsyncLocal<string?>`(:11)、`static string? CurrentCorrelationId`(:21)、`static CameraDiagnosticScope Begin(string?)`(:23)、`Dispose()`(:25) |

> ⚠️ 常见误解：`ICameraAdapter` / `CameraParameterSchema` / `CameraConnectionOptions` **不在** Plugin.SDK，而在 `DeviceDriver/Cameras/Contracts/`。

## 2. DeviceDriver 驱动契约（`src/DeviceDriver/Cameras/Contracts/`）

### ICameraAdapter（原样签名，`ICameraAdapter.cs:17-58`）

```csharp
public interface ICameraFrameSink                       // :9
{
    ValueTask OnFrameAsync(CameraFrame frame, CancellationToken cancellationToken);   // :14（返回成功后接管所有权）
}

public interface ICameraAdapter : IAsyncDisposable       // :17
{
    string AdapterId { get; }                            // :19
    CameraDescriptor Descriptor { get; }                 // :20
    CameraCapabilities Capabilities { get; }             // :21
    CameraState State { get; }                           // :22

    Task ConnectAsync(CameraConnectionOptions options, CancellationToken cancellationToken);   // :24
    Task DisconnectAsync(CancellationToken cancellationToken);                                 // :28
    Task<CameraFrame> CaptureAsync(CameraCaptureRequest request, CancellationToken cancellationToken);  // :30
    Task StartStreamAsync(CameraCaptureRequest request, ICameraFrameSink sink, CancellationToken cancellationToken);  // :34
    Task StopStreamAsync(CancellationToken cancellationToken);                                 // :39
    Task ExecuteSoftwareTriggerAsync(CancellationToken cancellationToken);                     // :41
    Task<CameraParameterSchema> GetParameterSchemaAsync(CancellationToken cancellationToken);   // :43
    Task<CameraCaptureConfiguration> ReadCaptureConfigurationAsync(CancellationToken cancellationToken);  // :46
    Task<IReadOnlyDictionary<string, object?>> ReadParametersAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken);  // :49
    Task ApplyParametersAsync(IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken);  // :53
    Task ExecuteCommandAsync(string name, CancellationToken cancellationToken);                 // :57
}
```

### 其余驱动契约

| 类型 | 成员 |
|---|---|
| `ICameraAdapterFactory`（`ICameraAdapterFactory.cs:5`） | `AdapterId`(:7)、`bool CanCreate(CameraDescriptor)`(:8)、`ICameraAdapter Create(CameraDescriptor)`(:9) |
| `ICameraDiscoveryProvider`（`ICameraDiscoveryProvider.cs:8`） | `ProviderId`(:10)、`Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(CancellationToken, IReadOnlySet<CameraTransportKind>? transports = null)`(:12) |
| `ICameraParameterProvider`（`ICameraParameterProvider.cs:7`） | `GetSchemaAsync`(:9)、`ReadValuesAsync`(:12)、`ApplyValuesAsync`(:16)。**当前仅有定义、无实现无调用**（备用契约，不要误当主路径） |
| `CameraRuntimeManager`（`CameraRuntimeManager.cs:15`，`sealed class : ICameraCaptureService, IAsyncDisposable`） | `Devices`(:27)、`TryGetSession(CameraDeviceRef, out CameraSession)`(:29)、`RegisterDevice(CameraDescriptor)`(:32)、`RegisterDevices`(:44)、`DiscoverAsync(IEnumerable<ICameraDiscoveryProvider>, CT)`(:53)、`OpenAsync(CameraDeviceRef, CameraAccessMode, CameraConnectionOptions?, CT)`(:70)、`CaptureAsync`(:128)、`CloseAsync`(:144)、`DisposeAsync`(:163) |
| `CameraSession`（`CameraSession.cs:7`，`sealed class : IAsyncDisposable`） | `internal` ctor(:9，带 onDisposed 回调)、`CameraRef`(:24)、`AccessMode`(:25)、`Adapter`(:26)、`DisposeAsync`(:28，`Interlocked.Exchange` 保证**只释放一次**(:30)） |
| `CameraDiscoveryCoordinator`（`CameraDiscoveryCoordinator.cs:14`） | ctor(:18)、`DiscoverAsync(CT)`(:24)、`DiscoverAsync(IReadOnlySet<CameraTransportKind>?, CT)`(:28) |
| `CameraAdapterException`（`CameraAdapterException.cs:6`，`sealed class : Exception`） | ctor `(CameraErrorCode, string)`(:8)、`ErrorCode`(:11) |
| `CameraParameterKind`（`CameraParameterSchema.cs:6`） | Boolean / Integer / Double / String / Enumeration / Command / Rectangle / Custom |
| `CameraEnumValue`（`:18`） | `Value`、`DisplayName` |
| `CameraParameterDescriptor`（`:20`） | `Name`(:22)、`DisplayName`(:23)、`SourceOrder`(:25)、`StandardFeature`(:27)、`Category`(:28)、`Kind`(:29)、`Value`(:30)、`Minimum`(:31)、`Maximum`(:32)、`Increment`(:33)、`EnumValues`(:34)、`IsReadOnly`(:36)、`IsAvailable`(:37)、`IsReadable`(:38)、`IsWritable`(:39)、`IsImplemented`(:40)、`RequiresStopped`(:41)、`AffectsSchema`(:42)、`Visibility`(:43)、`StatusMessage`(:44)、`Description`(:45) |
| `CameraParameterSchema`（`:48`） | `AdapterId`(:50)、`SchemaVersion`(:51)、`Parameters`(:52) |
| `CameraCaptureConfiguration`（`:57`） | `StreamMode`(:59)、`AcquisitionModeName`(:60)、`TriggerSourceName`(:61)、`IsSingleFrame`(:62)、`IsValid`(:63)、`StatusMessage`(:64) |
| `CameraAccessMode`（`CameraAccessMode.cs:3`） | Exclusive / SharedRead / Preview / Diagnostic |
| `CameraConnectionOptions`（`CameraConnectionOptions.cs:5`） | `Timeout`(:7，默认 5s)、`ApplyParameterSnapshotOnConnect`(:8，默认 true) |

## 3. 必须沿用的运行时语义（写死在实现约定里）

| 语义 | 事实依据 |
|---|---|
| 注册必须校验 `DeviceId` 与 `CameraRef` 非空，并以 `CameraRef.Value` 作会话键 | `CameraRuntimeManager.cs:32`、`:36-41` |
| 发现按 `DeviceId` 去重，且**单个发现器失败不阻断其他** | `CameraRuntimeManager.cs:62-64`；`CameraDiscoveryCoordinator.cs:49-53`、`:69-73` |
| 打开会话：先 `Connect` 成功再登记；失败必须 Dispose 回滚 | `CameraRuntimeManager.cs:113-125` |
| 同一 ref 已被占用 → 明确抛错（不允许第二会话） | `CameraRuntimeManager.cs:85` |
| 会话释放只发生一次，并在释放后从运行时注销 | `CameraSession.cs:30-40` |
| 取消发现时**保留已完成结果** | `CameraDiscoveryCoordinator.cs:40-47` |
