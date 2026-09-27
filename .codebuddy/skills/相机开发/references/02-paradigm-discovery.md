# 02 发现器范式（Step 3）

照抄对象：`src/DeviceDriver/Cameras/Hikvision/Mvs/MvsCameraDiscoveryProvider.cs`。
逐点行号索引见 `06-reference-impl-mvs.md`。

## 1. 骨架职责（固定四件事）

1. 声明 `ProviderId`（稳定字符串，如 `"hikvision-mvs"`）。
2. 声明本 Provider 支持的**传输位掩码**，用于 `transports` 过滤。
3. `DiscoverAsync` 内做 SDK 枚举，**转成 `CameraDescriptor`**，绝不向上暴露 SDK 对象。
4. 缓存「DeviceId → SDK 设备信息」，供同厂商工厂/适配器复用，避免二次枚举。

参考：`MvsCameraDiscoveryProvider.cs:27`（ProviderId）、`:24-25`（GigE=0x1 / Usb3=0x4 掩码）、`:29-48`（DiscoverAsync）、`:74`（缓存 `_deviceInfos`）、`:81`（`TryGetDeviceInfo`）。

## 2. 传输过滤语义（必须实现）

```csharp
// transports == null  ⇒ 不过滤，返回全部支持的设备
// transports 非空   ⇒ 仅当本 Provider 支持的传输与请求集合有交集时才枚举
```

参考：`MvsCameraDiscoveryProvider.cs:35-37`。
新增接口类型时，只在这里扩展掩码与分支，**不要新增工厂**。

## 3. 稳定身份生成（本技能最关键的规则）

```
有序列号            →  "<providerId>:<serial>"
无序列号            →  "<providerId>:<transport>:<model>:<ip 或 userName>"
CameraRef           =  new CameraDeviceRef(<上面的稳定 ID>)
Protocol            =  GenICam（GenICam 系）或 VendorSdk（纯厂商协议）
Metadata["sdk"]     =  "<厂商 SDK 名>"
Metadata["adapter"] =  "<providerId>"      // 工厂 CanCreate 的兜底判据
```

参考：`MvsCameraDiscoveryProvider.cs:93-147`（`CreateDescriptor`）、`:125-127`（稳定 ID 规则）、`:132`（CameraRef）、`:140-145`（Protocol 与 Metadata）。

**自检**：同一台相机连续两次发现得到的 `DeviceId` 必须逐字一致；换 USB 口/换 IP 不应改变有序列号设备的身份。

## 4. SDK 调用与线程

- SDK 枚举通常是**同步阻塞**接口 → 用 `Task.Run` 包裹，避免阻塞 UI 线程（`MvsCameraDiscoveryProvider.cs:42-43`）。
- 发现阶段**不创建长期句柄**：只读取设备信息并缓存（设计文档 §4.1）。

## 5. 失败隔离（零兜底，但不得连坐）

| 情形 | 正确行为 |
|---|---|
| 未安装 SDK / 未编译进 SDK | **返回空数组**，不抛异常、不阻断其他发现器（`MvsCameraDiscoveryProvider.cs:44-46`） |
| 单个设备解析失败 | 跳过该设备，其余照常返回 |
| 发现被取消 | 交由协调器语义处理（保留已完成结果，`CameraDiscoveryCoordinator.cs:40-47`），不要在 Provider 内吞掉取消 |

> 注意区分：**发现阶段**缺 SDK 返回空（优雅降级）；**创建适配器阶段**缺 SDK 必须抛 `DriverUnavailable`（见 03 文档）。两者不同是有意设计。

## 6. 条件编译（若使用厂商 SDK）

```xml
<!-- src/DeviceDriver/SunEyeVision.DeviceDriver.csproj:11-13 -->
<MvsSdkRoot Condition="'$(MvsSdkRoot)' == '' and '$(MVS_SDK_ROOT)' != ''">$(MVS_SDK_ROOT)</MvsSdkRoot>
<MvsSdkRoot Condition="'$(MvsSdkRoot)' == '' and Exists('D:\Program Files\MVS\Development')">D:\Program Files\MVS\Development</MvsSdkRoot>
<DefineConstants Condition="Exists('$(MvsSdkRoot)\DotNet\win64\MvCameraControl.Net.dll')">$(DefineConstants);MVS_SDK_AVAILABLE</DefineConstants>
```

- 条件 `Reference`：`:22-27`（`Private=true`，**厂商二进制不入库**）。
- 代码侧用法：文件头 `#if MVS_SDK_AVAILABLE` + `using 厂商命名空间`（`MvsCameraDiscoveryProvider.cs:11-13`），实现体同样包在 `#if` 内，`#else` 分支返回空数组/抛 `DriverUnavailable`。
- 新厂商照此模式新增自己的 `XxxSdkRoot` 与 `XXX_SDK_AVAILABLE`，**不要复用 MVS 的常量**。

## 7. 本步验证判据

1. 无 SDK：`DiscoverAsync` 返回空，其他发现器结果不受影响。
2. 有 SDK：能枚举到设备；同一设备两次发现只出现一条。
3. `transports` 传入不含本 Provider 支持的类型时，**不做无谓枚举**。
4. 生成描述符里 `DeviceId` / `CameraRef` / `Protocol` / `Metadata` 四项符合 §3 规则。
