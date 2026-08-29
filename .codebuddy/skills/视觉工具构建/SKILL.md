---
name: 视觉工具构建
description: 视觉工具构建技能。用于批量生成视觉算法工具插件，以 Blob 分析为基准参考源提炼开发范式，输出 UI 方案 + 算法方案 + 差异点清单。触发场景：创建新算法工具、添加视觉算法工具插件、工具 UI/算法方案设计。
---

# 视觉工具构建技能

> **定位**：算法工具的"批量生成器"。基于现有算法工具提炼**开发范式** → 输出 **UI 方案 + 算法方案 + 差异点清单** → 生成**基座代码** → 标注**独立修改点**。

## 0. 适用范围边界（Step0 必须先判定）

| 适用性 | 工具类型 | 判别标准 | 处理方式 |
|---|---|---|---|
| ✅ **适用** | 算法工具 | 有输入图像 → Halcon 算法 → 结果+可视化+**调试窗口**（`HasDebugWindow=true`） | 本技能批量生成 |
| ❌ 不适用 | 流程控制（Condition） | 无图像、有控制信号（`IControlFlowResult`） | 独立设计、多轮迭代 |
| ❌ 不适用 | 子程序（Subroutine） | 嵌套工作流、动态输出映射 | 独立设计 |
| ❌ 不适用 | 脚本（Script） | 动态编译（`IPrecompilableTool`） | 独立设计 |
| ❌ 不适用 | 图像源（ImageLoad） | `HasDebugWindow=false`，无调试窗口 | 非本技能对象 |

**硬性前提**：`HasDebugWindow=true` 是算法工具的本技能适用前提。无调试窗口的工具一律不适用本技能。

---

## 1. 技能产出物（三大件）

1. **UI 方案**（范式 A + 差异点 + 3-Tab ASCII 图）
2. **算法方案**（范式 C + 差异点 + 引擎契约）
3. **基座代码**（三件套 + RoI + 调试控件）

---

## 2. 引擎契约（三铁律，经代码验证）

```
工具层公开 API 永不暴露具体引擎类型
  ├─ 图像：parameters.ResolvedInputImage (IVisionImage? 通用契约)
  ├─ Run 优先路径：Halcon（GetHalconImage() → HOperatorSet.*）
  ├─ 可切换路径：OpenCvSharp（GetCvImage() → CvOp.*）
  └─ 适配：IVisionImage ← HalImage / CvImage
```

1. **Run 默认走 Halcon**（现状即如此）
2. **公开 API 不出现 `HImage`/`Mat`**，图像统一 `IVisionImage`
3. OpenCV 实现走 `CvToolHelpers.GetCvImage()` + `CvOp.*`，不直接 `Cv2.*`

---

## 3. UI 控件复用铁律

1. 调试控件**必须**继承 `ToolDebugControlBase`（自动获得 Execute/Confirm/ContinuousExecute/ResetParameters 命令、ToolExecutionCompleted 事件、AvailableDataSources、SetParameters/SetDataProvider/SetMainImageControl）
2. 参数输入**一律** SDK 控件：`LabeledControl`/`ConfigSetting`/`RangeInputControl`/`RangeSlider`/`NumericUpDown`/`ToggleSwitch`/`ParamComboBox`/`ParamTextBox`/`ImageSourceSelector`
3. 区域编辑用 `RoiEditorManagerControl` / `MeasurementEditorManagerControl` / `MeasureToolBar` / `ShapeToolBar`
4. 图像显示用 `ImageControl`
5. 工具层只做「装配 + 参数绑定」，**禁止自建参数/区域/图像控件**

---

## 4. 开发范式（含出处，可溯源）

### 范式 A：UI 三件套

#### A1. 3-Tab XAML 布局（基本参数/运行参数/结果显示）

**整体结构 ASCII 图**：

```
┌──────────────────────────────────────────────────────────────┐
│ ToolDebugControlBase (PreferredDebugWindowSize 900×620)        │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ [基本参数] [运行参数] [结果显示]        ← TabControl     │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│ Tab1 基本参数（无引导，Blob 范式 - 基准参考源）：              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ [输入图像] ImageSourceSelector (SDK)                    │  │
│  │ ┌─ 区域设置 ─────────────────────────────────────────┐ │  │
│  │ │ RoiEditorManagerControl (SDK)                      │ │  │
│  │ │   ├─ 检测区域 InspectionRegions                    │ │  │
│  │ │   └─ 屏蔽区域 MaskRegions                          │ │  │
│  │ └─────────────────────────────────────────────────────┘ │  │
│  │ [位置修正] ToggleSwitch + ParamBinding(矩阵绑定源)       │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│ Tab1 基本参数（有引导，EdgeDetect 范式）：                     │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ MeasurementEditorManagerControl (SDK)                  │  │
│  │   = MeasureToolBar + GuideEditor + MaskRegions         │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│ Tab2 运行参数（SDK 参数控件）：                                │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ LabeledControl + ConfigSetting(数值+Slider)            │  │
│  │               + ParamComboBox(枚举+Converter)          │  │
│  │               + ToggleSwitch(开关) + RangeInputControl │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│ Tab3 结果显示（Blob 范式）：                                   │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ ▾ Expander 结果判断 (IsExpanded=True)                   │  │
│  │   [ToggleSwitch + RangeInputControl]×N 绑定 ResultConfig│  │
│  │   (输出为空/面积/数量/圆度/矩形度/独立Blob判定...)       │  │
│  │ ▾ Expander 图形显示 (IsExpanded=True)                   │  │
│  │   DispItemColorControl×N (检测区域/屏蔽区域/轮廓/质心...)│  │
│  │ ▾ Expander 文本显示 (IsExpanded=True)                   │  │
│  │   DispTextColorControl×N (面积列表...)                  │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

**XAML 骨架**（出处 `BlobAnalysisToolDebugControl.xaml` / `ThresholdToolDebugControl.xaml`）：

```xaml
<controls:ToolDebugControlBase x:Class="SunEyeVision.Tool.{ToolName}.Views.{ToolName}ToolDebugControl"
        xmlns:controls="clr-namespace:SunEyeVision.Plugin.SDK.UI.Controls;assembly=SunEyeVision.Plugin.SDK"
        xmlns:region="clr-namespace:SunEyeVision.Plugin.SDK.UI.Controls.Roi.Views;assembly=SunEyeVision.Plugin.SDK"
        xmlns:results="clr-namespace:SunEyeVision.Plugin.SDK.Execution.Results;assembly=SunEyeVision.Plugin.SDK"
        DataContext="{Binding Parameters, RelativeSource={RelativeSource Self}}">
  <TabControl Margin="5,0,5,0">
    <!-- Tab1 基本参数（Blob 范式：区域管理器常显 + 位置修正） -->
    <TabItem Header="基本参数" IsSelected="True">
      <StackPanel>
        <controls:LabeledControl Label="输入图像">
          <controls:ImageSourceSelector
              PlaceholderText="请选择输入图像源" Width="250"
              AvailableDataSources="{Binding AvailableDataSources, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type controls:ToolDebugControlBase}}}"
              SelectedDataSource="{Binding ImageSource, Mode=TwoWay}"/>
        </controls:LabeledControl>

        <!-- 区域设置（检测区域数量=0 时全图分析，否则自动启用区域限制） -->
        <GroupBox Header="区域设置" Margin="0,8,0,0">
          <region:RoiEditorManagerControl x:Name="regionManager"
              InspectionRegionsSource="{Binding InspectionRegions.Regions}"
              MaskRegionsSource="{Binding MaskRegions.Regions}"/>
        </GroupBox>

        <!-- 位置修正（算法工具标配：ROI 随定位矩阵变换） -->
        <controls:LabeledControl Label="位置修正" Margin="0,8,0,0" HorizontalAlignment="Left">
          <controls:ToggleSwitch
              IsChecked="{Binding IsPositionCorrectionEnabled, Mode=TwoWay}"
              SwitchWidth="48" SwitchHeight="24"/>
        </controls:LabeledControl>
        <controls:LabeledControl Label="修正信息" Margin="0,4,0,0" HorizontalAlignment="Left"
            Visibility="{Binding IsPositionCorrectionEnabled, Converter={StaticResource BoolToVisibilityConverter}}">
          <controls:ParamBinding
              x:Name="positionCorrectionBinding"
              DataType="{x:Type results:AffineMatrix}"
              ParameterName="位置修正" Width="200"
              BindingSource="{Binding PositionCorrectionBindingSource, Mode=TwoWay}"
              BindingDisplayText="{Binding PositionCorrectionBindingDisplayText, Mode=TwoWay}"
              AvailableDataSources="{Binding AvailableDataSources, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type controls:ToolDebugControlBase}}}"/>
        </controls:LabeledControl>
      </StackPanel>
    </TabItem>

    <!-- Tab2 运行参数 -->
    <TabItem Header="运行参数">
      <StackPanel>
        <controls:LabeledControl Label="阈值">
          <controls:ConfigSetting ParameterName="Threshold"
              DataType="{x:Type sys:Int32}"
              Value="{Binding Threshold.Value, Mode=TwoWay}"
              Minimum="0" Maximum="255" ShowSlider="True" Width="250"
              AvailableDataSources="{Binding AvailableDataSources, ...}"/>
        </controls:LabeledControl>
      </StackPanel>
    </TabItem>

    <!-- Tab3 结果显示（Blob 范式：结果判断 + 图形显示 + 文本显示） -->
    <TabItem Header="结果显示">
      <Grid Background="{DynamicResource ThemePanelBackgroundBrush}">
        <Grid.RowDefinitions>
          <RowDefinition Height="*"/>
          <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <ScrollViewer Grid.Row="0" Padding="3">
          <StackPanel>
            <!-- 结果判断（每个判定 = ToggleSwitch + RangeInputControl，绑定 ResultConfig） -->
            <Expander Header="结果判断" IsExpanded="True">
              <StackPanel Margin="16,8">
                <controls:LabeledControl Label="输出为空判断">
                  <controls:ToggleSwitch IsChecked="{Binding ResultConfig.IsEmptyCheckEnabled, Mode=TwoWay}"/>
                </controls:LabeledControl>
                <controls:LabeledControl Label="面积判断">
                  <controls:ToggleSwitch IsChecked="{Binding ResultConfig.IsTotalAreaCheckEnabled, Mode=TwoWay}"/>
                </controls:LabeledControl>
                <controls:LabeledControl Label="面积范围 (px²)"
                    Visibility="{Binding ResultConfig.IsTotalAreaCheckEnabled, Converter={StaticResource BoolToVisibilityConverter}}">
                  <controls:RangeInputControl Unit="px²" Minimum="0" Maximum="10000000"
                      MinValue="{Binding ResultConfig.TotalAreaMin, Mode=TwoWay}"
                      MaxValue="{Binding ResultConfig.TotalAreaMax, Mode=TwoWay}"
                      DecimalPlaces="0" NumericWidth="120"/>
                </controls:LabeledControl>
                <!-- 更多判定项按工具差异扩展：数量/圆度/矩形度/独立Blob... -->
              </StackPanel>
            </Expander>

            <!-- 图形显示（DispItemColorControl 控制各类叠加元素颜色/可见性） -->
            <Expander Header="图形显示" IsExpanded="True">
              <StackPanel Margin="16,8">
                <controls:LabeledControl Label="检测区域">
                  <controls:DispItemColorControl Item="{Binding InspectionRegionStyle, Mode=TwoWay}"/>
                </controls:LabeledControl>
                <controls:LabeledControl Label="屏蔽区域">
                  <controls:DispItemColorControl Item="{Binding MaskRegionStyle, Mode=TwoWay}"/>
                </controls:LabeledControl>
              </StackPanel>
            </Expander>

            <!-- 文本显示（DispTextColorControl 控制文本标注） -->
            <Expander Header="文本显示" IsExpanded="True">
              <StackPanel Margin="16,8">
                <controls:LabeledControl Label="面积列表">
                  <controls:DispTextColorControl Item="{Binding AreaDisplayStyle, Mode=TwoWay}"
                      AvailableDataSources="{Binding AvailableDataSources, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type controls:ToolDebugControlBase}}}"/>
                </controls:LabeledControl>
              </StackPanel>
            </Expander>
          </StackPanel>
        </ScrollViewer>
      </Grid>
    </TabItem>
  </TabControl>
</controls:ToolDebugControlBase>
```

#### A2. Code-Behind 骨架（出处 `EdgeDetectToolDebugControl.xaml.cs`）

```
[DependencyProperty Parameters]
[INPC ResultText / ResultRMSE / ResultEdgePointCount]   ← 结果展示字段
PreferredDebugWindowSize => 900×620

构造：InitializeComponent + Parameters = new {ToolName}Parameters()

Overrides（五件套）：
  SetParameters: base.SetParameters + 类型不符 throw(rule-015) + Parameters = typed
  ExecuteTool:  new {ToolName}Tool().Run(Parameters) → UpdateResultDisplay
  CanExecuteTool: Parameters != null
  SetMainImageControl: base + 托管子编辑器 + Restore{Guide/Roi}FromParameters
  InitializeAsync: 依赖多项数据的初始化（可选）
```

#### A3. SDK 控件复用清单

| 类别 | SDK 控件 |
|---|---|
| 容器/基类 | `ToolDebugControlBase` |
| 参数输入 | `LabeledControl` / `ConfigSetting` / `RangeInputControl` / `RangeSlider` / `NumericUpDown` / `ToggleSwitch` / `ParamComboBox` / `ParamTextBox` / `ImageSourceSelector` |
| 区域编辑 | `RoiEditorManagerControl` / `MeasurementEditorManagerControl` / `MeasureToolBar` / `ShapeToolBar` / `ToolBarControl` |
| 图像显示 | `ImageControl` |
| RoI 辅助 | `DrawingParameterPanel` / `RoiInfoPanel` / `RoiSubscribePanel` / `NodeSelectorPopup` |

---

### 范式 B：参数类（出处 `BlobAnalysisParameters.cs` / `EdgeDetectParameters.cs`）

**枚举**（工具特有枚举放参数文件顶部，对齐 Halcon 算子参数语义）：

```csharp
public enum {Enum}Type { All, {Option1}, {Option2} }
```

**参数类骨架**：

```csharp
[JsonDerivedType(typeof({ToolName}Parameters), "{ToolName}")]   // 反序列化必需
public class {ToolName}Parameters : RoiParameters               // 算法工具基类（含区域）
{
    private double _param = 20.0;
    public double Param
    {
        get => _param;
        set => SetProperty(ref _param, value, "参数中文名");      // 中文名用于 UI/日志
    }
    // 枚举：set 内不 clamp；数值：set 内 clamp（如 Math.Max(1, value)）
}
```

**差异点**：
- 无区域需求 → 继承 `ToolParameters`（非 `RoiParameters`）
- 有引导几何 → 加 `GeometryType` + 几何字段（`GuideCircle`/`LineStartPoint`/`LineEndPoint`）
- 判定配置 → `ResultConfig` 模型（面积/均值/质心判定）

---

### 范式 C：算法 Run 范式（Halcon 优先，出处 `BlobAnalysisTool.cs` / `ThresholdTool.cs`）

```csharp
[Tool(
    id: "{ToolId}", displayName: "{中文显示名}",
    Description = "{工具描述}", Icon = "{emoji}",
    Category = "{分类名}", Version = "1.0.0", HasDebugWindow = true)]
[JsonDerivedType(typeof({ToolName}Parameters), "{ToolName}")]
public class {ToolName}Tool : IToolPlugin<{ToolName}Parameters, {ToolName}Results>
{
    private readonly ToolAttribute? _attribute;
    public {ToolName}Tool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

    public {ToolName}Results Run({ToolName}Parameters parameters)
    {
        var result = new {ToolName}Results
        {
            ToolName = _attribute?.DisplayName ?? "{ToolName}",
            ToolId = _attribute?.Id ?? "{ToolName}",
            Timestamp = DateTime.Now
        };
        var sw = Stopwatch.StartNew();
        try
        {
            // ✅ 图像获取：Halcon 优先（GetHalconImage 内部兼容 Cv→Hal 转换）
            var hImage = parameters.GetHalconImage()
                ?? throw new InvalidOperationException("无输入图像");
            parameters.LogInfo("开始{ToolName}处理");

            // —— 算法步骤（本工具差异点核心）——
            // Step1: ROI 提取 → HOperatorSet.ReduceDomain
            // Step2: 位置修正（IsPositionCorrectionEnabled 时）→
            //        ToolRegionHelper.ApplyPositionCorrectionHalcon(roi, insp, mask, matrix, w, h)
            // Step3: Halcon 算子序列（HOperatorSet.*）
            // Step4: 结果计算 + 判定

            result.DurationMs = sw.Elapsed.TotalMilliseconds;
            result.SetSuccess(result.DurationMs);
            parameters.LogSuccess("...完成");
        }
        catch (Exception ex)
        {
            result.SetError(ex.Message, ex);        // rule-015 禁止静默
            parameters.LogError($"失败: {ex.Message}");
        }
        return result;
    }

    public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        => new {ToolName}ToolDebugControl("{ToolName}", tool, null);

    public FrameworkElement? CreateDataAnalysisControl()
        => new {ToolName}DataAnalysisControl();
}
```

**差异点**：算法步骤序列（Step1-3）、`GetHalconImage` 后的处理、参数如何驱动算子。

---

### 范式 D：结果类（出处 `BlobAnalysisResults.cs`）

```csharp
[JsonDerivedType(typeof({ToolName}Results), "{ToolName}Results")]
public class {ToolName}Results : ToolResults
{
    /// <summary>输出图像（框架自动识别）</summary>
    public IVisionImage? OutputImage { get; set; }

    /// <summary>属性映射（UI 显示用）</summary>
    public override List<PropertyMapEntry> GetPropertyMap()
    {
        return
        [
            // ⚠️ 必须合并基类，获得"执行耗时/执行信息"分组
            .. base.GetPropertyMap(),
            new("ExampleValue", "示例值", "结果"),
        ];
    }

    /// <summary>可视化元素（图像叠加）</summary>
    public override IEnumerable<VisualElement> GetVisualElements()
    {
        // yield return VisualElement.Circle(...);  // 几何/ROI 叠加
        yield break;
    }

    /// <summary>输出变量（数据变量面板 SSOT）</summary>
    public override IReadOnlyList<OutputVariableDefinition> GetOutputVariables() { ... }
}
```

---

### 范式 E：RoI 契约（算法工具标配）

```
InspectionRegions（检测区）→ 限定算法作用范围
MaskRegions（屏蔽区）→ 排除区域
GetRoisByType(RoiType.Inspection/Mask) + ToolRegionHelper.BuildHalconROI
GetVisualElements 输出区域可视化
```

---

### 范式 F：数据分析控件（可选，复杂工具，出处 `BlobDataAnalysisControl.cs`）

实现 `IToolControlDataAnalysis`，订阅 `OnResultUpdated` 刷新统计图表/列表。用于主界面右下角数据分析 Tab。

---

## 5. 差异点清单模板（每个工具必填）

| 差异维度 | 范式默认 | 本工具差异 | 处理方式 |
|---|---|---|---|
| 输入几何 | 无引导 | 需引导(Line/Circle/Arc) | 组装 `MeasurementEditorManagerControl` |
| 参数基类 | `RoiParameters` | 无区域需求 | 改继承 `ToolParameters` |
| 算法步骤 | 固定管线 | 自定义 Halcon 序列 | 独立实现 Run 核心 |
| 结果项 | 统计+判定 | 工具特有结果 | 扩展 Results |
| 可视化 | `GetVisualElements` | 几何/ROI 叠加 | 自定义绘制 |
| 判定配置 | `ResultConfig` | 工具特有判定 | 扩展 Model 子类 |
| 区域启用开关 | 无开关（Blob，区域常显） | 需"启用区域限制"开关 | 加 `IsRegionEnabled` + CheckBox（参考 Threshold） |
| 位置修正 | 标配（Blob：ToggleSwitch + ParamBinding 矩阵） | 无需位置修正 | 移除 Tab1 位置修正段（参考 Threshold） |
| 结果展示 | 三 Expander（结果判断/图形显示/文本显示） | 展示项按工具扩展 | 结果判断加判定项、图形显示加 `DispItemColorControl`、文本显示加 `DispTextColorControl` |

---

## 6. 工作流

```mermaid
flowchart TD
    A[新算法工具需求] --> B[Step0 边界判定 HasDebugWindow?]
    B -->|算法工具| C[Step1 参考范式选择<br>无引导→Blob/Threshold / 有引导→EdgeDetect]
    B -->|非算法工具| X[拒绝使用技能→独立设计]
    C --> D[Step2 输出UI方案<br>3Tab ASCII + Tab1区域组件 + SDK控件 + 参数项]
    D --> E[Step3 输出算法方案<br>Halcon步骤序列 + 引擎契约 + 差异点]
    E --> F[Step4 生成基座代码<br>三件套 + RoI + 调试控件]
    F --> G[Step5 输出差异点清单<br>标注独立修改位置]
```

---

## 7. 参考实现（真实路径）

**基准参考源**：

```
tools/SunEyeVision.BlobAnalysis/          ← 主参考（标准算法工具完整范例）
  ├─ BlobAnalysisTool.cs                  （Halcon 全流程 + 分步计时 + 延迟计算）
  ├─ BlobAnalysisParameters.cs            （RoiParameters 完整参数模型 24KB）
  ├─ BlobAnalysisResults.cs               （结果 + 可视化 + 输出变量）
  ├─ Models/BlobAnalysisResultConfig.cs   （结果判定配置）
  ├─ Views/BlobAnalysisToolDebugControl.xaml(+.cs)  （3-Tab UI 范式 A）
  └─ Views/BlobDataAnalysisControl.xaml(+.cs)       （数据分析范式 F）
```

**辅助参考**：

```
tools/SunEyeVision.Tool.Threshold/      （标准无引导，UI 精简范例）
tools/SunEyeVision.Tool.EdgeDetect/     （引导式 Metrology，引导几何 UI 范例）
tools/SunEyeVision.Tool.ImageFilter/    （图像滤波）
tools/SunEyeVision.Tool.Morphology/     （形态学）
tools/SunEyeVision.Tool.ShapeMatch/     （复杂匹配，含数据分析控件）
```

> ⚠️ **路径注意**：Blob 分析目录为 `tools/SunEyeVision.BlobAnalysis/`（**无 `Tool.` 前缀**），csproj 名为 `SunEyeVision.Tool.BlobAnalysis.csproj`。**禁止**参考 `ImageLoad`（`HasDebugWindow=false`，无调试窗口，非本技能对象）。

---

## 8. 代码锚点（真实符号名）

### 图像/引擎契约

| 符号 | 位置 | 用途 |
|---|---|---|
| `parameters.ResolvedInputImage` | `src/Plugin.SDK/Execution/Parameters/ToolParameters.cs` | 通用图像契约（`IVisionImage?`） |
| `GetHalconImage()` | `src/Plugin.SDK/VisionAdapters/HalconHelpers.cs` | Run 优先取图（Halcon） |
| `GetCvImage()` | `src/Plugin.SDK/VisionAdapters/CvToolHelpers.cs` | 可切换取图（OpenCV） |
| `IVisionImage` | `src/Plugin.SDK/VisionAdapters/IVisionImage.cs` | 图像通用接口 |

### 工具三件套核心

| 符号 | 位置 | 用途 |
|---|---|---|
| `IToolPlugin<TParams, TResult>` | `src/Plugin.SDK/Core/IToolPlugin.cs` | 工具开发者入口 |
| `ToolAttribute` | `src/Plugin.SDK/Core/ToolAttribute.cs` | 工具元数据特性 |
| `ToolParameters` | `src/Plugin.SDK/Execution/Parameters/ToolParameters.cs` | 参数基类 |
| `RoiParameters` | `src/Plugin.SDK/Execution/Parameters/RoiParameters.cs` | 算法工具参数基类（含区域） |
| `ToolResults` | `src/Plugin.SDK/Execution/Results/ToolResults.cs` | 结果基类 |

### UI 控件（工具开发者用）

| 符号 | 位置 | 用途 |
|---|---|---|
| `ToolDebugControlBase` | `src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs` | 调试控件基类 |
| `ImageControl` | `src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml.cs` | 图像显示 |
| `RoiEditorManagerControl` | `src/Plugin.SDK/UI/Controls/Roi/Views/RoiEditorManagerControl.xaml.cs` | 检测区+屏蔽区管理 |
| `MeasurementEditorManagerControl` | `src/Plugin.SDK/UI/Controls/Measure/Views/MeasurementEditorManagerControl.xaml.cs` | 测量引导+屏蔽区管理 |
| `ConfigSetting` | `src/Plugin.SDK/UI/Controls/ConfigSetting.cs` | 数值参数配置 |

---

## 9. 约束

- 序列化必须 `System.Text.Json`，禁止 Newtonsoft.Json
- 必须 `[JsonPolymorphic]` + `[JsonDerivedType]` 多态
- JSON 使用 PascalCase
- 命名遵循 PascalCase，禁止缩写
- 结果类 `GetPropertyMap()` 必须 `.. base.GetPropertyMap()` 合并基类，禁止重复声明 ExecutionTimeMs
- 工具层公开 API **永不暴露** `HImage`/`Mat`，图像统一 `IVisionImage`
- Run 默认走 Halcon（`GetHalconImage()`），公开 API 不出现引擎类型
- UI 必须复用 SDK 控件，禁止自建参数/区域/图像控件
