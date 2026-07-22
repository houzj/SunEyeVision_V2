# SunEyeVision 项目文件结构说明

## 概述

SunEyeVision 采用框架-插件架构，支持多人协作开发。框架层提供核心功能和共享UI组件，插件层实现具体业务逻辑，通过统一的接口进行集成。

---

## 一、总体架构

### 1.1 架构分层

```
SunEyeVision/
├── Framework Layer (框架层)
│   ├── SunEyeVision.Core/           # 核心接口和模型
│   ├── SunEyeVision.DeviceDriver/   # 设备驱动抽象
│   ├── SunEyeVision.Workflow/       # 工作流引擎
│   └── SunEyeVision.UI/             # UI框架和共享组件
│
└── Plugin Layer (插件层)
    └── Plugins/                     # 所有插件（统一管理，不区分内置/自定义）
        ├── ImageProcessing/          # 团队A：图像处理插件
        ├── Workflow/                # 团队B：工作流节点插件
        └── CustomFilters/           # 团队C：自定义滤镜插件
```

### 1.2 核心设计原则

1. **插件平等原则**：所有插件统一管理，不区分内置/自定义
2. **接口驱动原则**：通过接口解耦，不依赖具体实现
3. **UI灵活原则**：支持Auto、Hybrid、Custom三种UI模式
4. **团队隔离原则**：不同团队开发不同插件，互不干扰
5. **共享复用原则**：框架提供共享UI组件，避免重复开发

---

## 二、框架层详细结构

### 2.1 SunEyeVision.Core - 核心接口层

```
SunEyeVision.Core/
├── Interfaces/
│   └── Plugins/                     # 插件接口定义
│       ├── IPlugin.cs               # 所有插件的基础接口（必须实现）
│       ├── IPluginUIProvider.cs     # UI提供者接口（可选）
│       ├── INodePlugin.cs           # 节点插件接口
│       └── IAlgorithmPlugin.cs      # 算法插件接口
├── Models/                          # 核心模型
├── Services/                        # 核心服务
├── Events/                          # 事件总线
└── Enums/                           # 枚举定义
```

**关键接口说明：**

#### IPlugin.cs
- 所有插件必须实现的基接口
- 定义了插件的基本属性（ID、名称、版本等）
- 定义了生命周期方法（Initialize、Start、Stop、Cleanup）

#### IPluginUIProvider.cs
- 可选接口，插件可选择性实现
- 定义了UI提供模式：
  - **Auto**：使用框架通用UI，插件只需提供元数据
  - **Hybrid**：使用框架共享UI组件，插件可自定义部分界面
  - **Custom**：插件完全自定义UI界面

#### INodePlugin.cs
- 继承自IPlugin
- 定义工作流节点的标准行为
- 包含端口定义、参数元数据、执行方法

#### IAlgorithmPlugin.cs
- 继承自IPlugin
- 定义图像处理算法的标准行为
- 包含算法参数、执行方法、验证方法

### 2.2 SunEyeVision.UI - UI框架层

```
SunEyeVision.UI/
├── Core/                            # UI核心服务
│   └── Services/
│       ├── PluginUIAdapter.cs       # 插件UI适配器（智能选择UI模式）
│       └── PluginManager.cs         # 插件管理器（加载、注册、生命周期管理）
│
├── Shared/                          # 共享UI组件库
│   └── Controls/
│       ├── PropertyGrid/            # 通用属性网格
│       │   ├── GenericPropertyGrid.xaml
│       │   ├── GenericPropertyGrid.xaml.cs
│       │   └── PropertyItemPanel.cs
│       ├── ParameterPanel/          # 通用参数面板
│       │   ├── GenericParameterPanel.xaml
│       │   └── GenericParameterPanel.xaml.cs
│       ├── Visualization/           # 可视化组件
│       │   ├── ImageVisualizationPanel.xaml
│       │   └── ImageVisualizationPanel.xaml.cs
│       └── Common/                  # 通用控件
│           ├── ProgressPanel.xaml
│           ├── ProgressPanel.xaml.cs
│           ├── StatusIndicator.xaml
│           └── StatusIndicator.xaml.cs
│
├── PluginDebug/                     # 插件调试系统
│   ├── IDebugControlProvider.cs     # 调试控制提供者接口
│   ├── DebugControlManager.cs       # 调试控制管理器
│   ├── SharedDebugControl.xaml      # 共享调试控件
│   └── SharedDebugControl.xaml.cs
│
├── Panel/                           # 面板系统
│   ├── PanelExtension.cs            # 面板扩展接口
│   └── PanelManager.cs              # 面板管理器
│
├── Controls/                        # 框架主界面控件
│   ├── WorkflowCanvasControl.xaml   # 工作流画布
│   ├── ToolboxControl.xaml          # 工具箱
│   └── PropertyPanelControl.xaml    # 属性面板
│
├── ViewModels/                      # 视图模型
├── Converters/                      # 值转换器
├── Adapters/                        # 显示适配器
└── Commands/                        # 命令模式实现
```

**核心组件说明：**

#### PluginUIAdapter（插件UI适配器）
- 智能选择UI展示方式
- 根据插件实现的接口自动判断UI模式
  - 插件未实现IPluginUIProvider → Auto模式
  - 插件实现IPluginUIProvider且Mode=Auto → Auto模式
  - 插件实现IPluginUIProvider且Mode=Hybrid → Hybrid模式
  - 插件实现IPluginUIProvider且Mode=Custom → Custom模式

#### PluginManager（插件管理器）
- 负责插件的加载、注册、生命周期管理
- 支持从指定目录批量加载插件
- 读取plugin.json获取插件元数据
- 提供插件查询和管理API

#### 共享UI组件库
- **GenericPropertyGrid**：通用属性网格，支持多种数据类型（int、double、bool、string等）
- **GenericParameterPanel**：通用参数面板，显示运行时参数
- **ImageVisualizationPanel**：图像可视化面板，显示图像处理结果
- **ProgressPanel**：进度面板，显示任务执行进度
- **StatusIndicator**：状态指示器，显示运行状态

#### 插件调试系统
- **IDebugControlProvider**：插件可实现的调试控制接口
- **DebugControlManager**：管理所有插件的调试控制器
- **SharedDebugControl**：共享调试控件，提供通用的调试功能（Start、Stop、Step、Reset）

#### 面板系统
- **IPanelExtension**：插件可实现的接口，用于注册自定义面板
- **PanelManager**：单例模式，管理所有扩展面板

---

## 三、插件层详细结构

### 3.1 插件通用结构

每个插件都遵循以下统一的结构：

```
Plugins/[PluginName]/
├── plugin.json                      # 插件元数据
├── [PluginName].csproj             # 项目文件
├── [PluginName]Plugin.cs           # 插件主类（实现IPlugin）
├── Nodes/                           # 节点实现（如果适用）
├── Algorithms/                      # 算法实现（如果适用）
├── UI/                              # 自定义UI（Custom模式下）
└── Services/                        # 插件内部服务
```

### 3.2 plugin.json 元数据格式

```json
{
  "pluginId": "ImageProcessing",      // 插件唯一标识
  "pluginName": "Image Processing Plugin",
  "version": "1.0.0",
  "description": "Image processing algorithms plugin",
  "author": "Team A",                 // 开发团队
  "dependencies": [],                 // 依赖的其他插件
  "permissions": [],                 // 权限要求
  "minFrameworkVersion": "1.0.0",    // 最低框架版本
  "customData": {                    // 自定义数据
    "category": "Image Processing",
    "supportedFormats": ["jpg", "png", "bmp"]
  }
}
```

### 3.3 插件开发示例

#### 示例1：Auto模式插件（零代码UI）

```csharp
using SunEyeVision.Core.Interfaces.Plugins;

public class BlurAlgorithmPlugin : IAlgorithmPlugin
{
    public string PluginId => "BlurAlgorithm";
    public string PluginName => "Blur Algorithm";
    public string Version => "1.0.0";
    public string Description => "Gaussian blur algorithm";
    public string Author => "Team A";
    
    public string AlgorithmType => "Blur";
    public string Icon => "blur.png";
    public string Category => "Image Processing";
    
    public void Initialize() { }
    public void Start() { }
    public void Stop() { }
    public void Cleanup() { }
    
    public ParameterMetadata[] GetParameters()
    {
        return new[]
        {
            new ParameterMetadata
            {
                Name = "radius",
                DisplayName = "Radius",
                Type = "double",
                DefaultValue = 5.0,
                MinValue = 1.0,
                MaxValue = 50.0,
                Description = "Blur radius"
            }
        };
    }
    
    public object Execute(object inputImage, Dictionary<string, object> parameters)
    {
        // 执行模糊算法
        return processedImage;
    }
    
    public bool ValidateParameters(Dictionary<string, object> parameters)
    {
        return true;
    }
}
```

**说明**：插件未实现IPluginUIProvider，框架自动使用GenericPropertyGrid显示参数，Auto模式，零代码实现UI。

#### 示例2：Hybrid模式插件（部分自定义UI）

```csharp
using SunEyeVision.Core.Interfaces.Plugins;
using SunEyeVision.UI.Shared.Controls.Common;

public class EdgeDetectionPlugin : IAlgorithmPlugin, IPluginUIProvider
{
    public string PluginId => "EdgeDetection";
    public string PluginName => "Edge Detection";
    // ... 其他IPlugin成员
    
    public UIProviderMode Mode => UIProviderMode.Hybrid;
    
    public object GetCustomControl()
    {
        return null; // 使用框架通用控件
    }
    
    public object GetCustomPanel()
    {
        // 返回自定义面板，使用共享的ProgressPanel
        return new ProgressPanel();
    }
    
    // ... 其他成员
}
```

**说明**：插件使用框架通用控件，但添加了自定义面板（ProgressPanel），Hybrid模式。

#### 示例3：Custom模式插件（完全自定义UI）

```csharp
using SunEyeVision.Core.Interfaces.Plugins;
using SunEyeVision.UI.CustomControls;

public class CustomFilterPlugin : IAlgorithmPlugin, IPluginUIProvider
{
    public string PluginId => "CustomFilter";
    public string PluginName => "Custom Filter";
    // ... 其他IPlugin成员
    
    public UIProviderMode Mode => UIProviderMode.Custom;
    
    public object GetCustomControl()
    {
        // 返回完全自定义的控件
        return new CustomFilterControl();
    }
    
    public object GetCustomPanel()
    {
        // 返回自定义面板
        return new CustomSettingsPanel();
    }
    
    // ... 其他成员
}
```

**说明**：插件完全自定义UI界面，Custom模式，框架仅提供容器和基础服务。

---

## 四、UI模式详解

### 4.1 Auto模式（自动模式）

**特点**：
- 插件无需实现UI相关代码
- 框架根据参数元数据自动生成UI
- 使用GenericPropertyGrid显示和编辑参数

**适用场景**：
- 简单算法插件
- 参数类型和结构简单的插件
- 快速原型开发

**实现步骤**：
1. 实现IPlugin接口
2. 在GetParameters()中定义参数元数据
3. 框架自动生成UI

**示例代码**：
```csharp
public class SimplePlugin : IAlgorithmPlugin
{
    // 实现IPlugin成员
    public ParameterMetadata[] GetParameters()
    {
        return new[]
        {
            new ParameterMetadata { Name = "param1", Type = "int", DefaultValue = 10 },
            new ParameterMetadata { Name = "param2", Type = "string", DefaultValue = "test" }
        };
    }
    
    // 不实现IPluginUIProvider，自动使用Auto模式
}
```

### 4.2 Hybrid模式（混合模式）

**特点**：
- 使用框架共享UI组件作为基础
- 插件可自定义部分界面（如附加面板）
- 兼顾通用性和灵活性

**适用场景**：
- 需要显示运行进度的插件
- 需要添加可视化组件的插件
- 需要部分自定义界面但不完全重写的插件

**实现步骤**：
1. 实现IPlugin接口
2. 实现IPluginUIProvider接口，Mode设为Hybrid
3. GetCustomControl()返回null（使用框架通用控件）
4. GetCustomPanel()返回自定义面板（使用共享组件或自定义组件）

**示例代码**：
```csharp
public class HybridPlugin : IAlgorithmPlugin, IPluginUIProvider
{
    public UIProviderMode Mode => UIProviderMode.Hybrid;
    
    public object GetCustomControl() => null; // 使用框架通用控件
    
    public object GetCustomPanel()
    {
        // 使用共享的ProgressPanel和ImageVisualizationPanel
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new ProgressPanel());
        stackPanel.Children.Add(new ImageVisualizationPanel());
        return stackPanel;
    }
}
```

### 4.3 Custom模式（自定义模式）

**特点**：
- 插件完全自定义UI界面
- 框架仅提供容器和基础服务
- 最大灵活性

**适用场景**：
- 复杂交互的插件
- 有特殊UI需求的插件
- 需要完全控制UI表现的插件

**实现步骤**：
1. 实现IPlugin接口
2. 实现IPluginUIProvider接口，Mode设为Custom
3. GetCustomControl()返回自定义的主控件
4. GetCustomPanel()返回自定义的附加面板（可选）

**示例代码**：
```csharp
public class CustomPlugin : IAlgorithmPlugin, IPluginUIProvider
{
    public UIProviderMode Mode => UIProviderMode.Custom;
    
    public object GetCustomControl()
    {
        // 返回完全自定义的主控件
        return new CustomMainControl();
    }
    
    public object GetCustomPanel()
    {
        // 返回自定义的附加面板
        return new CustomSidePanel();
    }
}
```

---

## 五、团队协作开发指南

### 5.1 团队隔离策略

插件层采用团队隔离策略，不同团队开发不同的插件，互不干扰：

```
Plugins/
├── ImageProcessing/      # 团队A负责：图像处理算法插件
├── Workflow/             # 团队B负责：工作流节点插件
└── CustomFilters/        # 团队C负责：自定义滤镜插件
```

**原则**：
- 每个团队有独立的插件目录
- 团队间通过接口通信，不直接依赖
- 框架层统一管理所有插件

### 5.2 开发流程

1. **需求分析**：确定插件功能、UI模式
2. **接口设计**：定义插件需要实现的接口
3. **元数据编写**：创建plugin.json
4. **插件开发**：实现接口，编写业务逻辑
5. **UI开发**：根据模式开发UI（Auto/Hybrid/Custom）
6. **调试测试**：使用插件调试系统进行测试
7. **注册发布**：通过PluginManager注册插件

### 5.3 版本管理

- 每个插件独立版本号
- 插件间依赖在plugin.json中声明
- 框架版本向后兼容，逐步升级

### 5.4 代码规范

- 使用命名空间：`Plugins.[PluginName]`
- 类名包含插件名前缀，避免冲突
- 遵循框架接口规范
- 充分利用共享UI组件

---

## 六、插件调试系统

### 6.1 调试系统架构

```
PluginDebug/
├── IDebugControlProvider.cs     # 调试控制提供者接口
├── DebugControlManager.cs       # 调试控制管理器
├── SharedDebugControl.xaml      # 共享调试控件
└── SharedDebugControl.xaml.cs
```

### 6.2 调试功能

- **Start**：开始调试
- **Stop**：停止调试
- **Step**：单步执行
- **Reset**：重置调试状态

### 6.3 使用示例

```csharp
public class MyPlugin : INodePlugin, IDebugControlProvider
{
    public object GetDebugPanel()
    {
        // 返回自定义调试面板，可使用SharedDebugControl
        return new CustomDebugPanel();
    }
    
    public void StartDebug() { /* 开始调试逻辑 */ }
    public void StopDebug() { /* 停止调试逻辑 */ }
    public void Step() { /* 单步执行逻辑 */ }
    public void Reset() { /* 重置调试逻辑 */ }
}

// 注册调试控制器
DebugControlManager.Instance.RegisterDebugControl(
    "MyPlugin", 
    myPluginInstance
);
```

---

## 七、面板扩展系统

### 7.1 面板扩展架构

```
Panel/
├── IPanelExtension.cs     # 面板扩展接口
└── PanelManager.cs       # 面板管理器（单例）
```

### 7.2 面板扩展示例

```csharp
public class MyPanelExtension : IPanelExtension
{
    public string PanelId => "MyPluginPanel";
    public string PanelName => "My Plugin Panel";
    public string Icon => "icon.png";
    
    public FrameworkElement GetPanelContent()
    {
        // 返回面板内容
        return new MyPluginPanelControl();
    }
}

// 注册面板
PanelManager.Instance.RegisterPanel(new MyPanelExtension());
```

---

## 八、文件组织最佳实践

### 8.1 命名规范

- **项目命名**：`[PluginName].csproj`
- **插件类命名**：`[PluginName]Plugin`
- **节点类命名**：`[NodeName]Node`
- **算法类命名**：`[AlgorithmName]Algorithm`
- **UI类命名**：`[Purpose]Control` 或 `[Purpose]Panel`

### 8.2 目录结构

每个插件建议采用以下目录结构：

```
Plugins/[PluginName]/
├── [PluginName].csproj
├── plugin.json
├── Core/                    # 核心逻辑
│   ├── [PluginName]Plugin.cs
│   └── ...
├── Nodes/                   # 节点实现（可选）
├── Algorithms/              # 算法实现（可选）
├── UI/                      # 自定义UI（Custom模式，可选）
│   ├── Controls/
│   └── Panels/
├── Services/                # 内部服务（可选）
├── Models/                  # 数据模型（可选）
└── Resources/               # 资源文件（可选）
    ├── Images/
    └── Styles/
```

### 8.3 资源管理

- 图标、图片等资源放在`Resources/`目录
- 样式文件放在`Resources/Styles/`目录
- 资源引用路径使用相对路径

---

## 九、总结

### 9.1 架构优势

1. **高度解耦**：框架与插件通过接口通信，互不依赖
2. **灵活扩展**：支持多种UI模式，满足不同需求
3. **团队协作**：插件隔离，多人开发互不干扰
4. **代码复用**：共享UI组件，减少重复开发
5. **易于维护**：清晰的分层结构，职责明确

### 9.2 开发建议

1. 优先使用Auto模式，减少UI开发成本
2. 充分利用共享UI组件，避免重复造轮子
3. 遵循接口规范，确保兼容性
4. 编写完善的plugin.json元数据
5. 利用调试系统进行充分测试
6. 注重代码注释和文档

### 9.3 未来扩展

- 支持插件热插拔
- 添加插件市场
- 提供插件模板
- 完善插件管理工具

---

## 附录

### A. 参考文档

- IPlugin接口文档
- IPluginUIProvider接口文档
- INodePlugin接口文档
- IAlgorithmPlugin接口文档
- 共享UI组件API文档
- 插件调试系统文档
- 面板扩展系统文档

### B. 示例代码

- Auto模式插件示例
- Hybrid模式插件示例
- Custom模式插件示例
- 节点插件示例
- 算法插件示例

### C. 常见问题

Q1: 如何选择UI模式？
A1: 根据插件复杂度和UI需求选择。简单插件用Auto，中等复杂用Hybrid，高复杂用Custom。

Q2: 插件间如何通信？
A2: 通过框架的事件总线或服务层进行通信，避免直接依赖。

Q3: 如何处理插件版本兼容？
A3: 在plugin.json中声明依赖和最低框架版本，框架会自动检查兼容性。

Q4: 自定义UI如何使用共享组件？
A4: 在自定义UI中引用共享命名空间，直接实例化共享组件。

---

**文档版本**：1.0.0
**最后更新**：2026-01-28
**维护者**：SunEyeVision Team
