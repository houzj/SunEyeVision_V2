# 海康相机驱动开发方案实施完成

## 实施概述

本次实施完成了海康相机驱动的完整开发方案，包括核心接口、基类实现、海康SDK封装、相机池管理器和ViewModel集成。

## 实施内容

### 1. 核心接口层

#### 1.1 相机服务接口 (ICameraService.cs)
- 位置: `src/DeviceDriver/Cameras/ICameraService.cs`
- 功能: 定义相机驱动的核心契约
- 主要接口:
  - `ConnectAsync()` / `DisconnectAsync()` - 连接管理
  - `StartCaptureAsync()` / `StopCaptureAsync()` - 采集控制
  - `TriggerCaptureAsync()` - 软触发采集
  - `SetTriggerModeAsync()` - 触发模式设置
  - `SetCaptureSettingsAsync()` - 参数配置
  - 事件: `FrameReceived`, `ExceptionOccurred`, `ConnectionStateChanged`

#### 1.2 相机工厂接口 (ICameraFactory.cs)
- 位置: `src/DeviceDriver/Cameras/ICameraFactory.cs`
- 功能: 抽象工厂模式，支持多品牌相机
- 主要方法:
  - `DiscoverDevicesAsync()` - 发现相机设备
  - `CreateCameraService()` - 创建相机服务
  - `IsSupported()` - 验证设备支持

### 2. 模型层

#### 2.1 相机设备信息 (CameraDeviceInfo.cs)
- 位置: `src/DeviceDriver/Models/CameraDeviceInfo.cs`
- 包含:
  - 设备基本信息（ID、名称、制造商、型号、序列号）
  - 网络信息（IP地址、端口、MAC地址）
  - 连接类型（Network、USB、GigE、CameraLink）
  - 固件/驱动版本
  - 自定义属性字典

#### 2.2 相机帧信息 (CameraFrameInfo.cs)
- 位置: `src/DeviceDriver/Models/CameraFrameInfo.cs`
- 包含:
  - 帧元数据（帧编号、时间戳）
  - 图像数据（宽度、高度、像素格式、图像数据）
  - 采集参数（曝光时间、增益、帧率）
  - 自定义属性
  - 实现IDisposable接口

#### 2.3 采集参数 (CameraCaptureSettings.cs)
- 位置: `src/DeviceDriver/Models/CameraCaptureSettings.cs`
- 包含:
  - 基本参数（曝光时间、增益、帧率）
  - 图像尺寸（宽度、高度、偏移）
  - 像素格式
  - 触发设置（硬件/软件触发、触发源、触发激活方式）
  - 自动调节（自动曝光、自动增益、目标值）
  - 伽马校正

#### 2.4 事件定义 (CameraEvents.cs)
- 位置: `src/DeviceDriver/Events/CameraEvents.cs`
- 定义:
  - `CameraFrameReceivedEvent` - 帧接收事件
  - `CameraExceptionEvent` - 异常事件
  - `CameraConnectionEvent` - 连接事件
  - 枚举: `CameraExceptionType`, `CameraConnectionState`

### 3. 基类实现

#### 3.1 相机服务基类 (CameraServiceBase.cs)
- 位置: `src/DeviceDriver/Cameras/CameraServiceBase.cs`
- 功能:
  - 实现通用的相机操作逻辑
  - 统一的事件触发机制
  - 异常处理和日志记录
  - 资源管理（IDisposable）
  - 线程安全（锁机制）
- 抽象方法（由子类实现）:
  - `ConnectCoreAsync()` / `DisconnectCoreAsync()`
  - `StartCaptureCoreAsync()` / `StopCaptureCoreAsync()`
  - `TriggerCaptureCoreAsync()`
  - `SetTriggerModeCoreAsync()`
  - `SetCaptureSettingsCoreAsync()`
  - `GetCaptureSettingsCoreAsync()`
  - `GetPropertiesCoreAsync()`
  - `SetPropertiesCoreAsync()`

### 4. 海康SDK封装

#### 4.1 MVS SDK封装 (MvCamera.cs)
- 位置: `src/DeviceDriver/Cameras/Hikvision/MvCamera.cs`
- 功能:
  - P/Invoke封装海康MVS SDK DLL
  - 常量定义（返回码、设备类型、像素类型）
  - 结构体定义（设备信息、帧信息）
  - API封装（初始化、设备枚举、连接管理、图像采集、参数设置）

#### 4.2 海康相机服务 (HikvisionCameraService.cs)
- 位置: `src/DeviceDriver/Cameras/Hikvision/HikvisionCameraService.cs`
- 功能:
  - 继自`CameraServiceBase`
  - 实现海康SDK的具体操作
  - 支持GigE和USB设备
  - IP地址/序列号匹配
  - 图像回调处理
  - 线程安全的相机操作

#### 4.3 海康相机工厂 (HikvisionCameraFactory.cs)
- 位置: `src/DeviceDriver/Cameras/Hikvision/HikvisionCameraFactory.cs`
- 功能:
  - 实现`ICameraFactory`接口
  - 设备发现和解析
  - 创建海康相机服务实例
  - 设备类型识别

### 5. 相机池管理

#### 5.1 相机池管理器 (CameraPoolManager.cs)
- 位置: `src/DeviceDriver/Cameras/CameraPoolManager.cs`
- 功能:
  - 多工厂管理
  - 相机服务生命周期管理
  - 批量操作（连接、断开、启动采集、停止采集）
  - 事件转发（异常、连接状态变更）
  - 资源清理

### 6. ViewModel集成

#### 6.1 相机管理器ViewModel (CameraManagerViewModel.cs)
- 位置: `src/UI/ViewModels/CameraManagerViewModel.cs`
- 更新内容:
  - 集成`CameraPoolManager`
  - 订阅相机池管理器事件
  - 实现相机发现功能
  - 扩展`CameraDevice`模型（添加`DeviceId`, `SerialNumber`）
  - 异步连接/断开操作
  - 批量操作支持
  - 事件驱动UI更新

### 7. 项目配置更新

#### 7.1 DeviceDriver项目文件
- 更新: `src/DeviceDriver/SunEyeVision.DeviceDriver.csproj`
- 添加: `Plugin.Infrastructure`项目引用

## 目录结构

```
src/DeviceDriver/
├── Cameras/                          # 相机驱动核心
│   ├── ICameraService.cs            # 相机服务接口
│   ├── ICameraFactory.cs            # 相机工厂接口
│   ├── CameraServiceBase.cs         # 相机服务基类
│   ├── CameraPoolManager.cs         # 相机池管理器
│   └── Hikvision/                   # 海康实现
│       ├── MvCamera.cs              # MVS SDK封装
│       ├── HikvisionCameraService.cs # 海康相机服务
│       └── HikvisionCameraFactory.cs # 海康相机工厂
├── Models/                           # 数据模型
│   ├── CameraDeviceInfo.cs          # 相机设备信息
│   ├── CameraFrameInfo.cs           # 相机帧信息
│   └── CameraCaptureSettings.cs     # 采集参数
└── Events/                           # 事件定义
    └── CameraEvents.cs              # 相机事件

src/UI/ViewModels/
└── CameraManagerViewModel.cs         # 相机管理器ViewModel（已更新）
```

## 架构设计原则

### 1. 依赖倒置原则 (DIP)
- UI层依赖抽象（ICameraService），不依赖具体实现
- 设备驱动层实现抽象接口

### 2. 开闭原则 (OCP)
- 通过工厂模式支持新品牌相机，无需修改现有代码
- 基类提供通用实现，子类扩展特定功能

### 3. 单一职责原则 (SRP)
- 各类职责清晰：接口定义契约、基类实现通用逻辑、子类实现特定逻辑、工厂负责创建

### 4. 接口隔离原则 (ISP)
- 相机服务接口设计合理，不包含不必要的方法

### 5. 里氏替换原则 (LSP)
- 所有相机实现都可以替换基类使用

## 使用示例

### 1. 初始化相机池管理器

```csharp
// 创建相机池管理器
var logger = ServiceLocator.GetService<ILogger>();
var cameraPoolManager = new CameraPoolManager(logger);

// 注册海康工厂
var hikvisionFactory = new HikvisionCameraFactory(logger);
cameraPoolManager.RegisterFactory(hikvisionFactory);
```

### 2. 发现相机

```csharp
var cameras = await cameraPoolManager.DiscoverAllCamerasAsync();
foreach (var camera in cameras)
{
    Console.WriteLine($"Found: {camera.DeviceName} ({camera.IpAddress})");
}
```

### 3. 连接相机

```csharp
var cameraService = cameraPoolManager.CreateCameraService(cameraInfo);
bool connected = await cameraService.ConnectAsync();
```

### 4. 开始采集

```csharp
// 订阅帧接收事件
cameraService.FrameReceived += (sender, e) =>
{
    Console.WriteLine($"Frame #{e.FrameInfo.FrameNumber} received");
};

// 开始采集
await cameraService.StartCaptureAsync();
```

### 5. 设置参数

```csharp
var settings = new CameraCaptureSettings
{
    ExposureTime = 10000.0,
    Gain = 5.0,
    FrameRate = 30.0
};
await cameraService.SetCaptureSettingsAsync(settings);
```

## 多品牌扩展指南

要添加新的相机品牌（例如大恒、Basler），只需：

1. 在`src/DeviceDriver/Cameras/`下创建新文件夹（例如`Daheng`）
2. 创建相机服务类，继承`CameraServiceBase`
3. 创建相机工厂类，实现`ICameraFactory`
4. 在应用初始化时注册新工厂
5. 无需修改现有代码！

## 技术特性

### 1. 异步编程
- 所有相机操作都是异步的（Async/Await）
- 避免阻塞UI线程

### 2. 线程安全
- 使用锁机制保护共享资源
- 相机操作互斥访问

### 3. 事件驱动
- 帧接收、异常、连接状态变更通过事件通知
- 支持多订阅者

### 4. 资源管理
- 实现IDisposable接口
- 自动释放非托管资源

### 5. 错误处理
- 统一的异常处理机制
- 详细的日志记录

## 后续工作

### 1. 测试
- 单元测试：测试各个类的功能
- 集成测试：测试相机池管理器
- 端到端测试：测试完整的相机操作流程

### 2. 性能优化
- 优化图像数据传输
- 减少内存拷贝
- 提高帧率

### 3. 功能扩展
- 添加更多相机品牌
- 实现图像缓冲池
- 添加视频录制功能
- 实现相机参数配置持久化

### 4. 文档完善
- API文档
- 用户手册
- 开发者指南

## 总结

本次实施完成了海康相机驱动的完整开发方案，包括：

✅ 核心接口定义（ICameraService, ICameraFactory）
✅ 数据模型定义（CameraDeviceInfo, CameraFrameInfo, CameraCaptureSettings）
✅ 事件系统（CameraFrameReceivedEvent, CameraExceptionEvent, CameraConnectionEvent）
✅ 基类实现（CameraServiceBase）
✅ 海康SDK封装（MvCamera）
✅ 海康相机实现（HikvisionCameraService, HikvisionCameraFactory）
✅ 相机池管理器（CameraPoolManager）
✅ ViewModel集成（CameraManagerViewModel）
✅ 项目配置更新

该架构具有良好的扩展性，可以轻松支持其他品牌的相机，无需修改现有代码，符合SOLID原则。
