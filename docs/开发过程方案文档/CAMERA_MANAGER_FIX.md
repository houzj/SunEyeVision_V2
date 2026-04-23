# 相机管理器修复说明

## 问题描述
用户之前设计的相机管理器新方案（包含 TabControl、基本信息、参数设置等）已经在代码库中实现，但是打开后显示的还是老界面。

## 根本原因
`CameraDetailPanel` 缺少 **DataContext 绑定**，导致无法获取 ViewModel 的数据，因此无法显示新设计的界面。

## 修复内容

### 1. 创建 CameraDetailViewModel.cs
**文件路径**: `src/UI/ViewModels/CameraDetailViewModel.cs`

**功能**:
- 管理 `CameraDetailPanel` 的数据绑定
- 包含相机详情面板的所有属性和命令
- 提供 `ManufacturerParamsViewModel` 用于动态加载厂商参数视图
- 实现连接、断开、预览等操作命令

**主要属性**:
- `SelectedCamera`: 当前选中的相机
- `IsCameraSelected`: 是否选中相机
- `ManufacturerParamsViewModel`: 厂商参数视图模型

**主要命令**:
- `ConnectCommand`: 连接相机
- `DisconnectCommand`: 断开相机
- `PreviewCommand`: 预览相机
- `SaveParamsCommand`: 保存参数
- `ResetParamsCommand`: 恢复默认参数

### 2. 扩展 CameraDevice 模型
**文件路径**: `src/UI/ViewModels/CameraManagerViewModel.cs`

**新增属性**:
- `Manufacturer`: 制造商（如：海康威视、大华）
- `Model`: 型号（如：DS-2CD2043D-IWD）
- `Description`: 描述
- `Port`: 端口
- `Username`: 用户名
- `Password`: 密码
- `Latency`: 延迟（ms）
- `FrameRate`: 帧率（fps）

**更新示例数据**:
- 为所有示例相机添加完整的详细信息
- 包含制造商、型号、连接信息等

### 3. 更新 CameraManagerViewModel
**文件路径**: `src/UI/ViewModels/CameraManagerViewModel.cs`

**新增属性**:
- `CameraDetailViewModel`: 相机详情视图模型实例

**更新构造函数**:
- 初始化 `CameraDetailViewModel` 实例

**更新 SelectedCamera 属性**:
- 当选中相机变化时，同步更新到 `CameraDetailViewModel`

### 4. 更新 CameraManagerDialog.xaml
**文件路径**: `src/UI/Views/Windows/CameraManagerDialog.xaml`

**修改内容**:
```xml
<!-- 右侧：相机详情 -->
<Border Grid.Column="1" Background="White" BorderBrush="#E0E0E0" BorderThickness="1" CornerRadius="4">
    <camera:CameraDetailPanel DataContext="{Binding CameraDetailViewModel}"/>
</Border>
```

**关键修改**: 为 `CameraDetailPanel` 添加 `DataContext="{Binding CameraDetailViewModel}"` 绑定

### 5. 更新 CameraDetailPanel.xaml
**文件路径**: `src/UI/Views/Camera/CameraDetailPanel.xaml`

**修改内容**:
- 为 `IsCameraSelected` 绑定添加 `FallbackValue=Visible`
- 为延迟和帧率绑定添加 `FallbackValue=-`

### 6. 实现 GenericParamsView.xaml
**文件路径**: `src/UI/Views/Camera/GenericParamsView.xaml`

**完整实现**:
- 曝光参数：曝光模式、曝光时间、增益
- 智能功能：移动侦测、越界检测、区域入侵
- 图像增强：宽动态、宽动态等级
- 编码参数：压缩类型、码率、I帧间隔

### 7. 更新 GenericParamsView.xaml.cs
**文件路径**: `src/UI/Views/Camera/GenericParamsView.xaml.cs`

**修改内容**:
```csharp
public GenericParamsView()
{
    InitializeComponent();
    this.DataContext = new GenericParamsViewModel();
}
```

### 8. 创建 AppResources.xaml
**文件路径**: `src/UI/Themes/AppResources.xaml`

**功能**: 定义全局转换器资源
- `InverseBoolToVisibilityConverter`: 反转布尔值到可见性转换器

## 完整功能说明

### 主界面布局
```
┌─────────────────────────────────────────────────────────────────────────┐
│  相机管理器                                     [_] [□] [×]             │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ 工具栏                                                           │  │
│  │  [➕添加] [➖删除] [🔍刷新] [📤导出] [💾保存]  [⚙️全局设置]      │  │
│  └─────────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────┬───────────────────────────┐  │
│  │  相机列表                           │  相机详情                 │  │
│  │  - DataGrid 显示相机列表            │  - TabControl 详情面板   │  │
│  │  - 批量操作栏                      │  - Tab1: 基本信息       │  │
│  │  - 全部连接/断开/启用/禁用         │  - Tab2: 参数设置       │  │
│  └──────────────────────────────────────┴───────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────┤
│  状态: 就绪 | 已连接: 2 | 已禁用: 1 | 总计: 5                        │
└─────────────────────────────────────────────────────────────────────────┘
```

### 基本信息标签页
```
┌─ 基本信息 ───────────────────────────────┐
│  ┌─ 基本信息 ───────────────────────┐   │
│  │ 名称: [相机1]                    │   │
│  │ 制造商: [海康威视]               │   │
│  │ 型号: [DS-2CD2043D-IWD]         │   │
│  │ 描述: [室内监控相机]             │   │
│  └──────────────────────────────────┘   │
│  ┌─ 连接信息 ───────────────────────┐   │
│  │ IP: [192.168.1.100]             │   │
│  │ 端口: [8000]                     │   │
│  │ 用户名: [admin]                  │   │
│  │ 密码: [********]                 │   │
│  └──────────────────────────────────┘   │
│  ┌─ 连接状态 ───────────────────────┐   │
│  │ 状态: ✅ 已连接                 │   │
│  │ 延迟: 23 ms                     │   │
│  │ 帧率: 30 fps                    │   │
│  │ [🔗连接] [⏸断开] [👁️预览]     │   │
│  └──────────────────────────────────┘   │
└───────────────────────────────────────────┘
```

### 参数设置标签页
```
┌─ 参数设置 ───────────────────────────────┐
│  ┌─ 曝光参数 ───────────────────────┐   │
│  │ 曝光模式: [自动▼]                 │   │
│  │ 曝光时间: [10000 us]              │   │
│  │ 增益: [50 dB]                    │   │
│  └──────────────────────────────────┘   │
│  ┌─ 智能功能 ───────────────────────┐   │
│  │ ☑ 启用移动侦测                   │   │
│  │ ☐ 启用越界检测                   │   │
│  │ ☐ 启用区域入侵                   │   │
│  └──────────────────────────────────┘   │
│  ┌─ 图像增强 ───────────────────────┐   │
│  │ 宽动态: [启用▼]                  │   │
│  │ 宽动态等级: [50]                 │   │
│  └──────────────────────────────────┘   │
│  ┌─ 编码参数 ───────────────────────┐   │
│  │ 压缩类型: [H.264▼]               │   │
│  │ 码率: [4096 Kbps]               │   │
│  │ I帧间隔: [50]                   │   │
│  └──────────────────────────────────┘   │
│  [💾保存参数] [↺恢复默认]            │
└───────────────────────────────────────────┘
```

## 使用说明

1. **打开相机管理器**
   - 点击主窗口工具栏的"📷 相机"按钮
   - 或通过菜单"工具 → 相机管理"

2. **查看相机详情**
   - 在左侧相机列表中点击任意相机
   - 右侧会显示该相机的详细信息
   - 可在"基本信息"和"参数设置"两个标签页之间切换

3. **操作相机**
   - 点击"🔗连接"按钮连接相机
   - 点击"⏸断开"按钮断开相机
   - 点击"👁️预览"按钮预览相机画面（待实现）

4. **批量操作**
   - 勾选多个相机
   - 点击底部的批量操作按钮（全部连接、全部断开等）

## 技术实现

### MVVM 模式
- **Model**: `CameraDevice` - 相机数据模型
- **ViewModel**: 
  - `CameraManagerViewModel` - 主窗口视图模型
  - `CameraDetailViewModel` - 详情面板视图模型
  - `GenericParamsViewModel` - 通用参数视图模型
- **View**:
  - `CameraManagerDialog.xaml` - 主窗口
  - `CameraDetailPanel.xaml` - 详情面板
  - `GenericParamsView.xaml` - 参数设置视图

### 数据绑定
- 使用 WPF 数据绑定连接 ViewModel 和 View
- `CameraDetailPanel` 通过 DataContext 绑定到 `CameraDetailViewModel`
- `SelectedCamera` 在 `CameraManagerViewModel` 和 `CameraDetailViewModel` 之间同步

### 命令模式
- 使用 `RelayCommand` 实现命令模式
- 所有按钮操作都通过命令绑定实现
- 支持 CanExecute 验证

## 下一步工作

1. 实现海康威视和大华厂商专用参数视图
2. 实现相机预览功能
3. 实现添加/编辑相机功能
4. 实现配置导入/导出功能
5. 实现全局设置功能
6. 集成真实的相机 SDK

## 修复验证

- ✅ 编译通过，无错误
- ✅ DataContext 绑定正确
- ✅ 数据模型完整
- ✅ 示例数据加载
- ✅ TabControl 显示正常
- ✅ 基本信息 Tab 显示完整
- ✅ 参数设置 Tab 显示完整
