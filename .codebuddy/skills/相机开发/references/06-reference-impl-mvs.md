# 06 MVS 参考实现索引（照抄时的定位表）

> 用法：打开对应文件，按行号跳转，对照 `02/03` 范式文档逐点比对。**不要把整文件读进上下文**，按需定位即可。

## 1. 发现器 `src/DeviceDriver/Cameras/Hikvision/Mvs/MvsCameraDiscoveryProvider.cs`

| 关注点 | 行号 |
|---|---|
| SDK 命名空间条件引入 | `:11-13` |
| 传输位掩码（GigE=0x1 / Usb3=0x4） | `:24-25` |
| `ProviderId = "hikvision-mvs"` | `:27` |
| `DiscoverAsync`（transports 过滤 :35-37、`Task.Run` 后台枚举 :42-43、无 SDK 返回空 :44-46） | `:29-48` |
| `DiscoverWithMvs`（`MV_CC_EnumDevices_NET` :62、`Marshal.PtrToStructure` :72、缓存 `_deviceInfos` :74） | `:50-79` |
| 取缓存设备信息 | `:81` |
| **稳定身份生成** `CreateDescriptor`（GigE :103-113、USB3 :114-123、稳定 ID 规则 :125-127、`CameraRef` :132、`Protocol=GenICam` :140、Metadata :141-145） | `:93-147` |
| 辅助（IP 解析、解码） | `:149`、`:152` |

## 2. 工厂 `src/DeviceDriver/Cameras/Hikvision/Mvs/MvsCameraAdapterFactory.cs`

| 关注点 | 行号 |
|---|---|
| `AdapterId` | `:15` |
| `CanCreate`（`Protocol==GenICam` 且 Manufacturer/前缀/Metadata 命中） | `:17-23` |
| `Create`（有 SDK 取缓存设备信息，缺失抛 `CameraNotFound` :28-31） | `:25-37` |
| 无 SDK → `DriverUnavailable` | `:34-35` |

## 3. 适配器 `src/DeviceDriver/Cameras/Hikvision/Mvs/MvsCameraAdapter.cs`

| 关注点 | 行号 |
|---|---|
| 类声明 / 条件编译入口 | `:21`、`:15`、`:23`、`:43`、`:49` |
| 双信号量（参数 `_gate` / 取帧 `_captureGate`） | `:28-29` |
| 参数描述符缓存 | `:39` |
| 连接 `ConnectAsync`（create :70、open :73、失败销毁 :76-77、`_opened` :79、无 SDK :82） | `:64-85` |
| 断开 `DisconnectAsync`（停抓取→关设备→销毁） | `:87-100` |
| 单帧采集 `CaptureAsync`（scope :104、锁 :107、`MV_CC_GetImageBuffer_NET` :116、`FrameTimeout` :125-137、`Unknown` :140、通道判定 :152-153、构帧 :161、`finally` 释放 :167、无 SDK :177） | `:102-179` |
| 连续采集 `StartStreamAsync`（后台任务 :200、无 SDK :204） | `:181-206` |
| 停流 `StopStreamAsync` → `StopStreamAndWaitAsync` | `:208-223`、`:882-910` |
| **参数 schema** `GetParameterSchemaAsync`（深度上限 32/255、visited 去重 :279、无 SDK 返回空 :308-311） | `:225-312` |
| schema 失败码 | `:245`、`:264`、`:267` |
| 软触发 `ExecuteSoftwareTriggerAsync`（需已启动软触发 :378、`TriggerTimeout` :386、无 SDK :391） | `:373-393` |
| 节点 → `CameraParameterKind` 映射 | `:396-475`、`:400`、`:402`、`:403-412` |
| 读写权限 / 实现态 / 可用态 / RequiresStopped / AffectsSchema | `:414-419`、`:431`、`:437-439`、`:507` |
| 异常节点返回带 `StatusMessage` 的描述符 | `:443-455` |
| `StandardFeature` 跨厂商稳定标识映射表 | `:477-505` |
| 日志 `LogCamera`（含关联 ID） | `:520-525` |
| 读值 `TryReadNode` 分派（bool 走布尔接口 :561-564、枚举项组合 :597 :600-601） | `:527-609` |
| 读参数 `ReadParametersAsync` | `:645-689` |
| 写参数 `ApplyParametersAsync`（先禁自动控制 :701-704、分派写入 :717-725、`Convert.*`+InvariantCulture :719-723、失败 `ParameterUnsupported` :730） | `:691-745` |
| 命令 `ExecuteCommandAsync`（无 SDK :798、不可用 :787/:792） | `:777-800` |
| 无 SDK 时 Dispose 空实现 | `:810-812` |
| 连接类错误码（`ConnectionRejected` 等） | `:826`、`:849` |
| 流循环 `RunStreamAsync`（`FrameTimeout` 容忍继续 :927-933） | `:912-960` |

## 4. 装配 `src/DeviceDriver/Cameras/CameraRuntimeComposition.cs`

| 关注点 | 行号 |
|---|---|
| 类与释放接口 | `:11` |
| 构造内注册（MVS 发现器 / 工厂 / 协调器） | `:13-24`、`:15`、`:16-19`、`:20-23` |
| 对外属性 / 释放 | `:26-28`、`:30` |

## 5. 模拟相机（测试基座，运行时**不装配**）

| 文件 | 关注点 |
|---|---|
| `Simulation/SimulatedCameraOptions.cs:5` | `DeviceId` / 尺寸 / 帧率 / 延迟 / 失败注入（`FailConnection`、`FailCapture`、`DropEveryNthFrame`） |
| `Simulation/SimulatedCameraDiscoveryProvider.cs:10` | `ProviderId="simulation"`(:19)；仅当 transports 含 `Simulation` 才返回设备(:26-29)；`Protocol=Simulation`(:42) |
| `Simulation/SimulatedCameraFactory.cs:17-19` | `CanCreate`：`Protocol==Simulation || Transport==Simulation` |
| `Simulation/SimulatedCameraAdapter.cs:12` | 全量 `ICameraAdapter` 实现（软触发信号量 :18、软触发流 :144-156、合成图像 :392-425） |

事实确认：`CameraRuntimeComposition.cs:15-23` **只注册 MVS**，未注册任何模拟器 ⇒ 接新厂商不应改动 Simulation 目录。

## 6. 对照用：契约与驱动契约

- 契约成员清单：`references/01-contract-surface.md`
- 适配器接口原文：`src/DeviceDriver/Cameras/Contracts/ICameraAdapter.cs:9-58`
- 运行时语义：`src/DeviceDriver/Cameras/Contracts/CameraRuntimeManager.cs:32-163`、`CameraSession.cs:24-40`
