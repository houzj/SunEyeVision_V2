---
name: vision-tool-builder
description: 视觉工具构建技能。用于创建新的视觉检测工具插件。触发场景：创建新工具、添加视觉算法工具、开发工具插件。
---

# 视觉工具构建技能

## ⚠️ 工具输出目录读取

工具编译后验证 `dev/Debug/plugins/<ToolName>/` 目录内容时，**禁止**用 `search_file` / `list_dir`（被 .gitignore 屏蔽，会漏读）。**必须**用 `execute_command` + PowerShell：

```powershell
Get-ChildItem -Path "d:\MyWork\SunEyeVision_V2\dev\Debug\plugins\SunEyeVision.Tool.XXX" -File
```

## 触发场景

- 创建新的视觉检测工具
- 添加图像处理算法工具
- 开发工具插件

## 工具三件套架构

每个工具由三个核心类组成，放在独立项目中：

```
tools/SunEyeVision.Tool.{ToolName}/
├── {ToolName}Tool.cs           # 工具执行逻辑
├── {ToolName}Parameters.cs     # 参数定义
├── {ToolName}Results.cs        # 结果定义
├── Models/                      # 可选：显示配置等
│   ├── {ToolName}DisplayConfig.cs
│   └── {ToolName}ResultConfig.cs
├── Views/                       # 可选：调试控件
│   └── {ToolName}ToolDebugControl.xaml
├── Converters/                  # 可选：值转换器
└── SunEyeVision.Tool.{ToolName}.csproj
```

## 1. 工具类（Tool）

### 基本模板

```csharp
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.{ToolName};

[Tool("{toolId}", "{中文显示名}",
    Description = "{工具描述}",
    Icon = "{emoji图标}",
    Category = "{分类名}")]
[JsonDerivedType(typeof({ToolName}Parameters), "{ToolName}")]
public class {ToolName}Tool : IToolPlugin<{ToolName}Parameters, {ToolName}Results>
{
    public {ToolName}Results Run({ToolName}Parameters parameters)
    {
        // 获取输入图像
        var image = parameters.ResolvedInputImage
            ?? throw new InvalidOperationException("未设置输入图像");

        var result = new {ToolName}Results();

        // === 核心算法逻辑 ===

        result.SetSuccess();
        return result;
    }

    // 可选：创建调试控件（注意：签名带 IToolPlugin tool 参数）
    public FrameworkElement? CreateDebugControl(IToolPlugin tool)
    {
        return new {ToolName}ToolDebugControl(this);
    }

    // 可选：创建数据分析控件（主界面右下角 Tab，无参）
    public FrameworkElement? CreateDataAnalysisControl()
    {
        return new {ToolName}DataAnalysisControl();
    }
}
```

### 关键规则

- **必须**标记 `[Tool]` 特性（id, displayName 为必填）
- **必须**实现 `IToolPlugin<TParams, TResult>` 泛型接口
- **必须**在 Parameters 类上标记 `[JsonDerivedType]`
- 图像输入通过 `parameters.ResolvedInputImage` 获取，不作为 Run() 参数传递
- `CreateDebugControl(IToolPlugin tool)` — **带参数**，双击节点弹出的调参窗口
- `CreateDataAnalysisControl()` — **无参**，右下角数据分析 Tab
- `[Tool]` 的 `HasDebugWindow` 控制是否可双击打开调试窗口
- `[Tool]` 的 `DebugWindowStyle` 控制窗口类型（None/Default/Custom）

## 2. 参数类（Parameters）

### 基本模板

```csharp
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Tool.{ToolName};

[JsonDerivedType(typeof({ToolName}Parameters), "{ToolName}")]
public class {ToolName}Parameters : ToolParameters
{
    private int _exampleParam = 128;

    [ParameterRange(0, 255)]
    public int ExampleParam
    {
        get => _exampleParam;
        set => SetProperty(ref _exampleParam, value, "示例参数");
    }
}
```

### 带区域的参数

需要 RoI 支持的工具继承 `RegionParameters`：

```csharp
[JsonDerivedType(typeof({ToolName}Parameters), "{ToolName}")]
public class {ToolName}Parameters : RegionParameters
{
    private int _threshold = 128;

    public int Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, value, "阈值");
    }

    // InspectionRegions 和 MaskRegions 继承自 RegionParameters
}
```

### 参数绑定支持（ParamValue）

需要支持动态绑定的参数使用 `ParamValue<T>`：

```csharp
private ParamValue<double> _minArea = ParamValue<double>.CreateConstant(100.0, "MinArea");

public ParamValue<double> MinArea
{
    get => _minArea;
    set => SetProperty(ref _minArea, value, "最小面积");
}
```

### 参数定义规则

- 继承 `ToolParameters` 或 `RegionParameters`
- 属性使用 `SetProperty(ref _field, value, "中文显示名")`
- `[ParameterRange(min, max)]` 标记数值范围
- `[JsonIgnore]` 标记不序列化的运行时属性
- `[IgnoreBind]` 标记不允许绑定的属性

## 3. 结果类（Results）

### 基本模板（必须合并基类执行耗时）

```csharp
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.{ToolName};

[JsonDerivedType(typeof({ToolName}Results), "{ToolName}Results")]
public class {ToolName}Results : ToolResults
{
    /// <summary>输出图像</summary>
    public Mat? OutputImage { get; set; }

    /// <summary>检测结果</summary>
    public double ExampleValue { get; set; }

    /// <summary>属性映射（UI显示用）</summary>
    public override List<PropertyMapEntry> GetPropertyMap()
    {
        return
        [
            // ⚠️ 必须合并基类，获得"执行耗时/执行信息"分组（rule: 统一执行耗时契约）
            .. base.GetPropertyMap(),
            new("OutputImage", "输出图像", null),
            new("ExampleValue", "示例值", "结果"),
        ];
    }

    /// <summary>可视化元素（在图像上叠加显示）</summary>
    public override IEnumerable<VisualElement> GetVisualElements()
    {
        // yield return VisualElement.Circle(...);
        // yield return VisualElement.Point(...);
        // yield return VisualElement.Rectangle(...);
        yield break;
    }
}
```

### 结果定义规则

- 继承 `ToolResults`
- `OutputImage` 属性名固定，框架自动识别
- `GetPropertyMap()` **必须用 `.. base.GetPropertyMap()` 合并基类**（统一暴露"执行耗时"），禁止重新声明 ExecutionTimeMs
- `GetVisualElements()` 返回图像叠加可视化
- `ResultItem` 工厂方法：`Numeric()`, `Text()`, `Boolean()`, `Point()`, `Circle()`
- `SetSuccess()` / `SetError()` 管理执行状态

## 4. 调试控件（可选）

### 模板

```csharp
public partial class {ToolName}ToolDebugControl : ToolDebugControlBase
{
    public {ToolName}ToolDebugControl(IToolPlugin tool) : base(tool)
    {
        InitializeComponent();
    }
}
```

### 调试控件接口

- `ToolDebugControlBase` 提供基类功能（继承后实现 ExecuteTool/CanExecuteTool）
- `IToolControlInitializer` 支持延迟初始化
- `IToolControlDataAnalysis` 标记数据分析控件

## 5. 项目文件

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <RootNamespace>SunEyeVision.Tool.{ToolName}</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Plugin.SDK\SunEyeVision.Plugin.SDK.csproj" />
  </ItemGroup>
</Project>
```

## 6. 注册与加载

工具通过 `[Tool]` 特性自动注册，无需手动配置：

1. `PluginManager.LoadPlugins()` 扫描 DLL
2. 发现 `[Tool]` 标记的类 → `ToolRegistry.RegisterTool()`
3. 自动提取参数类型 → `ParameterTypeRegistry` 注册多态序列化
4. 工具箱自动显示

## 参考实现

| 工具 | 路径 | 特点 |
|------|------|------|
| ImageLoad | `tools/SunEyeVision.Tool.ImageLoad/` | 图像源节点，HasDebugWindow=false |
| Threshold | `tools/SunEyeVision.Tool.Threshold/` | 标准工具，带RegionParameters和调试控件 |
| BlobAnalysis | `tools/SunEyeVision.BlobAnalysis/` | 复杂工具，带数据分析和显示配置 |

## 代码锚点

### Plugin SDK 核心

| 类 | 路径 | 行号 | 用途 |
|---|---|---|---|
| `IToolPlugin` | `src/Plugin.SDK/Core/IToolPlugin.cs` | L74 | 非泛型接口（框架层入口） |
| `IToolPlugin<TParams, TResult>` | 同上 | L165 | 泛型接口（工具开发者入口） |
| `ToolAttribute` | `src/Plugin.SDK/Core/ToolAttribute.cs` | L32 | 工具元数据特性 |
| `ToolParameters` | `src/Plugin.SDK/Execution/Parameters/ToolParameters.cs` | L54 | 参数基类 |
| `ToolResults` | `src/Plugin.SDK/Execution/Results/ToolResults.cs` | L265 | 结果基类 |
| `ToolExecutor` | `src/Plugin.SDK/Core/ToolExecutor.cs` | L41 | 执行器（统一执行入口） |
| `ObservableObject` | `src/Plugin.SDK/Models/ObservableObject.cs` | L192 | 属性通知基类 |

### Plugin SDK 控件（工具开发者用）

| 类 | 路径 | 工具开发者视角 |
|---|---|---|
| `ToolDebugControlBase` | `src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs` | 创建调试控件时继承（实现 ExecuteTool/CanExecuteTool） |
| `ImageControl` | `src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml.cs` | 在调试控件中显示图像 |
| `ParamBinding` | `src/Plugin.SDK/UI/Controls/ParamBinding.cs` | 参数绑定交互 |
| `ConfigSetting` | `src/Plugin.SDK/UI/Controls/ConfigSetting.cs` | 参数配置 UI |
| `RoiEditorControl` | `src/Plugin.SDK/UI/Controls/Roi/Views/RoiEditorControl.xaml.cs` | RoI 交互（Region→RoI 迁移后） |
| `DefaultDebugWindow` | `src/Plugin.SDK/UI/Windows/DefaultDebugWindow.xaml` | 调试窗口宿主 |

## 约束

- 序列化必须使用 `System.Text.Json`，禁止 Newtonsoft.Json
- 必须使用 `[JsonPolymorphic]` + `[JsonDerivedType]` 支持多态
- JSON 使用 PascalCase 命名
- 命名遵循 PascalCase 规范，禁止缩写
- 结果类 `GetPropertyMap()` 必须 `.. base.GetPropertyMap()` 合并基类，禁止重复声明 ExecutionTimeMs
