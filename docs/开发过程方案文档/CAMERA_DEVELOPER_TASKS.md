# Camera Developer 任务清单

## 📋 基本信息
- **Agent名称**: camera-developer
- **工作目录**: `d:/MyWork/SunEyeVision_Dev-camera`
- **工作分支**: `feature/camera-type-support`
- **目标**: 开发相机管理器（支持IP/USB/GigE相机）

---

## 🎯 核心任务

### 任务1：创建相机设备基类

**文件**: `src/Core/Devices/Device.cs`

**需求**:
- 继承 `ObservableObject`
- 定义相机设备的基本属性
- 提供连接、断开、初始化等基本方法

**实现要点**:
```csharp
public class Device : ObservableObject
{
    // 属性
    public string DeviceId { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public DeviceStatus Status { get; set; }
    public bool IsConnected => Status == DeviceStatus.Connected;

    // 方法
    public virtual Task<bool> Connect();
    public virtual Task<bool> Disconnect();
    public virtual Task<bool> Initialize();
}
```

**提交**:
```bash
cd d:/MyWork/SunEyeVision_Dev-camera
git add src/Core/Devices/Device.cs
git commit -m "feat: 添加 Device 基类"
git push origin feature/camera-type-support
```

---

### 任务2：实现具体相机设备类

#### 2.1 IP相机设备

**文件**: `src/Core/Devices/IpCameraDevice.cs`

**需求**:
- 继承 `Device`
- 支持IP地址和端口配置
- 支持用户名密码认证

---

#### 2.2 USB相机设备

**文件**: `src/Core/Devices/UsbCameraDevice.cs`

**需求**:
- 继承 `Device`
- 支持USB设备ID识别
- 支持自动检测USB相机

---

#### 2.3 GigE相机设备

**文件**: `src/Core/Devices/GigECameraDevice.cs`

**需求**:
- 继承 `Device`
- 支持GigE网络配置
- 支持GigE特性（如多播）

**提交**:
```bash
git add src/Core/Devices/*.cs
git commit -m "feat: 添加IP/USB/GigE相机设备类"
git push origin feature/camera-type-support
```

---

### 任务3：定义相机提供者接口

**文件**: `src/Core/Providers/ICameraProvider.cs`

**需求**:
- 定义相机提供者的统一接口
- 支持设备枚举、连接、断开等操作

**实现要点**:
```csharp
public interface ICameraProvider
{
    string Name { get; }
    DeviceType SupportedType { get; }

    Task<IReadOnlyList<Device>> GetDevicesAsync();
    Task<bool> ConnectAsync(Device device);
    Task<bool> DisconnectAsync(Device device);
    Task<Mat?> GetCameraImageAsync(Device device);
}
```

**提交**:
```bash
git add src/Core/Providers/ICameraProvider.cs
git commit -m "feat: 添加 ICameraProvider 接口"
git push origin feature/camera-type-support
```

---

### 任务4：实现相机提供者

#### 4.1 海康威视提供者

**文件**: `src/Core/Providers/HikvisionProvider.cs`

**需求**:
- 实现 `ICameraProvider`
- 支持海康威视IP/GigE相机
- 封装海康威视SDK

---

#### 4.2 大华提供者

**文件**: `src/Core/Providers/DahuaProvider.cs`

**需求**:
- 实现 `ICameraProvider`
- 支持大华IP/GigE相机
- 封装大华SDK

**提交**:
```bash
git add src/Core/Providers/*.cs
git commit -m "feat: 添加海康威视和大华相机提供者"
git push origin feature/camera-type-support
```

---

### 任务5：创建相机管理器

**文件**: `src/Core/Managers/CameraManager.cs`

**需求**:
- 管理所有相机设备
- 提供设备枚举、连接、断开等操作
- 支持设备状态监控

**实现要点**:
```csharp
public class CameraManager : ObservableObject
{
    private readonly List<ICameraProvider> _providers;
    private readonly ObservableCollection<Device> _devices;

    public IReadOnlyList<Device> Devices => _devices;

    public CameraManager()
    {
        _providers = new List<ICameraProvider>();
        _devices = new ObservableCollection<Device>();
    }

    public void RegisterProvider(ICameraProvider provider)
    {
        _providers.Add(provider);
    }

    public async Task EnumerateDevicesAsync()
    {
        // 枚举所有设备
    }

    public async Task<bool> ConnectAsync(Device device)
    {
        // 连接设备
    }
}
```

**提交**:
```bash
git add src/Core/Managers/CameraManager.cs
git commit -m "feat: 添加 CameraManager"
git push origin feature/camera-type-support
```

---

### 任务6：更新UI层

#### 6.1 更新ViewModel

**文件**: `src/UI/ViewModels/CameraManagerViewModel.cs`

**需求**:
- 继承 `ViewModelBase`
- 绑定相机管理器
- 提供UI命令

---

#### 6.2 更新Dialog

**文件**: `src/UI/Dialogs/CameraManagerDialog.xaml` / `CameraManagerDialog.xaml.cs`

**需求**:
- 显示相机设备列表
- 提供连接/断开按钮
- 显示相机图像预览

**提交**:
```bash
git add src/UI/ViewModels/CameraManagerViewModel.cs
git add src/UI/Dialogs/CameraManagerDialog.*
git commit -m "feat: 添加相机管理器UI"
git push origin feature/camera-type-support
```

---

### 任务7：集成到主窗口

**需求**:
- 在主窗口添加相机管理入口
- 更新菜单或工具栏

**提交**:
```bash
git add src/UI/MainWindow.xaml
git add src/UI/MainWindow.xaml.cs
git commit -m "feat: 集成相机管理器到主窗口"
git push origin feature/camera-type-support
```

---

## 🧪 测试任务

### 任务8：编写单元测试

**文件**: `tests/Core/DeviceTests.cs`

**需求**:
- 测试Device基类功能
- 测试各设备类的实现

**提交**:
```bash
git add tests/Core/DeviceTests.cs
git commit -m "test: 添加设备单元测试"
git push origin feature/camera-type-support
```

---

## 📝 文档任务

### 任务9：更新文档

**需求**:
- 更新 README.md
- 添加相机管理器使用文档
- 添加相机SDK集成文档

**提交**:
```bash
git add README.md
git add docs/camera-management.md
git commit -m "docs: 添加相机管理器文档"
git push origin feature/camera-type-support
```

---

## 🔄 开发流程

### 日常开发
```powershell
# 1. 切换到工作目录
cd d:/MyWork/SunEyeVision_Dev-camera

# 2. 拉取最新代码（如果有）
git pull origin feature/camera-type-support

# 3. 开发功能
# ... 编写代码 ...

# 4. 编译验证
dotnet build

# 5. 提交代码
git add .
git commit -m "feat: 描述你的修改"
git push origin feature/camera-type-support
```

### 同步主分支
```powershell
# 定期同步主分支的最新修改
cd d:/MyWork/SunEyeVision_Dev-camera
git fetch origin main
git rebase origin/main
```

---

## 📊 进度跟踪

- [ ] 任务1：创建 Device 基类
- [ ] 任务2：实现具体相机设备类
- [ ] 任务3：定义 ICameraProvider 接口
- [ ] 任务4：实现相机提供者
- [ ] 任务5：创建 CameraManager
- [ ] 任务6：更新UI层
- [ ] 任务7：集成到主窗口
- [ ] 任务8：编写单元测试
- [ ] 任务9：更新文档

---

## 🚨 注意事项

1. **只在 feature/camera-type-support 分支工作**
2. **不要修改 feature/tool-improvement 分支的代码**
3. **定期同步主分支**
4. **提交前先编译验证**
5. **遵循项目编码规范**
   - 使用 `ObservableObject` 基类
   - 使用项目日志系统
   - 使用 PascalCase 命名

---

## 💬 联系方式

- Team Lead: `team-lead@camera-tool-dev`
- Tool Developer: `tool-developer@camera-tool-dev`
