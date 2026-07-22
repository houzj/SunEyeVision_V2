# 图像预处理工具 UI 设计方案

## 一、团队分工

| 成员 | 工具 | 实现内容 | 项目目录 |
|------|------|---------|---------|
| **member1** | 图像二值化 (ImageThreshold) | 参数/结果/主逻辑/调试UI/数据分析UI | `tools/SunEyeVision.Tool.ImageThreshold/` |
| **member2** | 图像滤波 (ImageFilter) | 参数/结果/主逻辑/调试UI/数据分析UI | `tools/SunEyeVision.Tool.ImageFilter/` |
| **member3** | 形态学处理 (Morphology) | 参数/结果/主逻辑/调试UI/数据分析UI | `tools/SunEyeVision.Tool.Morphology/` |

---

## 二、架构规范（所有工具通用）

### 2.1 命名空间规范

| 工具 | 命名空间 | 根命名空间 |
|------|---------|-----------|
| 图像二值化 | `SunEyeVision.Tool.ImageThreshold` | `SunEyeVision.Tool.ImageThreshold` |
| 图像滤波 | `SunEyeVision.Tool.ImageFilter` | `SunEyeVision.Tool.ImageFilter` |
| 形态学处理 | `SunEyeVision.Tool.Morphology` | `SunEyeVision.Tool.Morphology` |

### 2.2 ComboBox 中文显示规范

**所有 ComboBox 的下拉选项必须使用中文显示**，枚举值到中文文本的映射通过 `IValueConverter` 实现（参考 `ThresholdTypeConverter` 模式）。

转换器位于 `Converters/XXXTypeConverter.cs`，在 XAML 中通过 `Converter={StaticResource XXXTypeConverter}` 绑定。

示例：
```xaml
<ComboBox SelectedItem="{Binding Method.Value, Mode=TwoWay, 
          Converter={StaticResource ThresholdMethodConverter}}">
  <sys:String>固定阈值(Binary)</sys:String>
  <sys:String>反二值化(BinaryInv)</sys:String>
</ComboBox>
```

或者在代码后置中直接绑定中文列表，执行时转换为枚举值。

### 2.3 运行参数控件规范

**运行参数 Tab 中的所有数值参数必须使用 `ConfigSetting` 控件**（位于 `SunEyeVision.Plugin.SDK.UI.Controls`）。

`ConfigSetting` 已内置双模式支持：

| 模式 | 说明 | 显示 |
|------|------|------|
| **常量模式(Constant)** | 用户直接输入/拖拽数值 | 显示 `NumericUpDown` 滑块编辑器 |
| **绑定模式(Binding)** | 选择上游节点的输出作为动态输入 | 显示树形数据源选择器 |

**XAML 绑定示例：**

```xaml
<StackPanel>
  <!-- 运行参数示例：阈值 -->
  <controls:LabeledControl Label="阈值">
    <controls:ConfigSetting
        x:Name="thresholdParam"
        ParameterName="Threshold"
        DataType="{x:Type sys:Int32}"
        Value="{Binding Threshold.Value, Mode=TwoWay}"
        Minimum="0" Maximum="255"
        SmallChange="1" LargeChange="10"
        ShowSlider="True" Width="250"
        AvailableDataSources="{Binding AvailableDataSources, 
            RelativeSource={RelativeSource FindAncestor, 
            AncestorType={x:Type controls:ToolDebugControlBase}}}"/>
  </controls:LabeledControl>
</StackPanel>
```

- `Value` 绑定到 `ParamValue<T>.Value`（常量模式直接改值，绑定模式存绑定配置）
- `AvailableDataSources` 绑定到基类的数据源集合（绑定模式时供用户选择）
- 常量模式：用户拖拽滑块或输入数值后自动写入 `ParamValue<T>`
- 绑定模式：用户选择数据源后，`ConfigSetting` 自动将绑定信息写入 `ParamValue.BindingConfig`

### 2.3 文件结构模板（每个工具一致）

```
tools/SunEyeVision.Tool.XXX/
├── XXXParameters.cs              # 参数类
├── XXXResults.cs                 # 结果类
├── XXXTool.cs                    # 主工具类
├── XXX.csproj                    # 项目文件
├── Converters/
│   └── XXXTypeConverter.cs       # 枚举转换器（可选）
├── Models/
│   └── XXXResultConfig.cs        # 结果判断配置
└── Views/
    ├── XXXToolDebugControl.xaml       # 调试UI XAML
    ├── XXXToolDebugControl.xaml.cs    # 调试UI代码后置
    ├── XXXDataAnalysisControl.xaml    # 数据分析UI XAML
    └── XXXDataAnalysisControl.xaml.cs # 数据分析UI代码后置
```

### 2.3 csproj 模板

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <OutputType>Library</OutputType>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <RootNamespace>SunEyeVision.Tool.XXX</RootNamespace>
    <AssemblyName>SunEyeVision.Tool.XXX</AssemblyName>
    <IsToolPlugin>true</IsToolPlugin>
    <OutDir>$(MSBuildProjectDirectory)\..\..\dev\Debug\plugins\</OutDir>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Plugin.SDK\SunEyeVision.Plugin.SDK.csproj" />
    <ProjectReference Include="..\..\src\SunEyeVision.CvEngine\SunEyeVision.CvEngine.csproj" />
  </ItemGroup>
</Project>
```

---

## 三、工具1：图像二值化 (ImageThresholdTool)

### 3.1 OpenCV 方法覆盖

| 方法 | OpenCV 函数 | 优先级 | 说明 |
|------|------------|--------|------|
| 固定阈值（Binary/BinaryInv/Trunc/ToZero/ToZeroInv） | `Cv2.Threshold()` | ★★★ 核心 | 5种模式 |
| 大津法（Otsu） | `Cv2.Threshold()` + THRESH_OTSU | ★★★ 核心 | 自动阈值 |
| 三角形法（Triangle） | `Cv2.Threshold()` + THRESH_TRIANGLE | ★★★ 核心 | 自动阈值 |
| 自适应均值（Adaptive Mean） | `Cv2.AdaptiveThreshold()` | ★★ 高频 | 局部阈值 |
| 自适应高斯（Adaptive Gaussian） | `Cv2.AdaptiveThreshold()` | ★★ 高频 | 局部阈值 |
| 范围阈值 | `Cv2.InRange()` | ★★ 高频 | 双阈值区间 |

### 3.2 参数设计

```csharp
public enum ThresholdMethod
{
    Binary,        // 固定二值化
    BinaryInv,     // 反二值化
    Trunc,         // 截断
    ToZero,        // 取零
    ToZeroInv,     // 反取零
    Otsu,          // 大津法
    Triangle,      // 三角形法
    AdaptiveMean,  // 自适应均值
    AdaptiveGaussian, // 自适应高斯
    InRange        // 范围阈值
}
```

**转换器 `ThresholdMethodConverter` 映射表：**

| 枚举值 | 中文显示 |
|--------|---------|
| `Binary` | 固定阈值 |
| `BinaryInv` | 反二值化 |
| `Trunc` | 截断 |
| `ToZero` | 取零 |
| `ToZeroInv` | 反取零 |
| `Otsu` | 大津法(Otsu) |
| `Triangle` | 三角形法(Triangle) |
| `AdaptiveMean` | 自适应均值 |
| `AdaptiveGaussian` | 自适应高斯 |
| `InRange` | 范围阈值 |

**[JsonDerivedType(typeof(ImageThresholdParameters), "ImageThreshold")]**
**ImageThresholdParameters : RegionParameters**（继承自 `RegionParameters` 而非 `ToolParameters`，自带区域管理功能）

**基本参数 Tab：**
| 控件 | 参数属性 | 类型 | 默认值 | 说明 |
|------|---------|------|--------|------|
| ImageSourceSelector | `ImageSource` | `AvailableDataSource?` | null | 输入图像源（继承自 RegionParameters） |
| CheckBox | `IsRegionEnabled` | `ParamValue<bool>` | false | 启用区域限制开关 |
| RegionEditorManagerControl | `InspectionRegions/MaskRegions` | `RegionCollectionParameter` | - | 检测区域 + 屏蔽区域编辑器 |
| ToggleSwitch | `IsPositionCorrectionEnabled` | `ParamValue<bool>` | false | 启用位置修正 |
| ParamBinding | `PositionCorrectionBindingSource/DisplayText` | `string` | - | 修正信息绑定源选择 |

**运行参数 Tab（含方法依赖可见性规则）：**
| 控件 | 参数属性 | 类型 | 默认值 | 范围 | 可见条件 |
|------|---------|------|--------|------|---------|
| ComboBox | `Method` | `ParamValue<ThresholdMethod>` | Binary | - | 始终 |
| ConfigSetting | `Threshold` | `ParamValue<int>` | 128 | [0,255] | 固定阈值模式 |
| ConfigSetting | `MaxValue` | `ParamValue<int>` | 255 | [0,255] | 非InRange模式 |
| CheckBox | `Invert` | `ParamValue<bool>` | false | - | 固定/Otsu/Triangle |
| ConfigSetting | `BlockSize` | `ParamValue<int>` | 11 | [3,31] 奇数 | 自适应模式 |
| ConfigSetting | `C` | `ParamValue<double>` | 2.0 | [0,100] | 自适应模式 |
| ConfigSetting | `MinThreshold` | `ParamValue<int>` | 0 | [0,255] | InRange模式 |
| ConfigSetting | `MaxThreshold` | `ParamValue<int>` | 255 | [0,255] | InRange模式 |

**结果显示 Tab：**
| 控件 | 参数属性 | 类型 | 默认值 | 说明 |
|------|---------|------|--------|------|
| ToggleSwitch | `IsEmptyCheckEnabled` | `bool` | false | 输出为空判断 |
| ToggleSwitch | `IsWhitePixelRatioCheckEnabled` | `bool` | false | 白色像素比例判断 |
| RangeInputControl | `WhitePixelRatioMin/Max` | `double` | 0/100 | 比例范围(%) |
| ToggleSwitch | `IsMeanCheckEnabled` | `bool` | false | 输出均值判断 |
| RangeInputControl | `MeanMin/Max` | `double` | 0/255 | 均值范围 |
| ToggleSwitch | `IsAreaCheckEnabled` | `bool` | false | 输出面积判断 |
| RangeInputControl | `AreaMin/Max` | `double` | 0/1000000 | 面积范围(px²) |

### 3.3 结果类

**ImageThresholdResults : ToolResults**

| 属性 | 类型 | 显示分组 |
|------|------|---------|
| `OutputImage` | `Mat?` | ✅ 主显示图像 |
| `BinaryMask` | `Mat?` | ✅ 可显示图像 |
| `GrayImage` | `Mat?` | ✅ 可显示图像 |
| `ThresholdUsed/MaxValueUsed/MethodUsed` | int/int/ThresholdMethod | ℹ️ 参数回写 |
| `InputSize` | Size | ℹ️ 输入信息 |
| `WhitePixelCount/WhitePixelRatio` | long/double | 📊 统计 |
| `OutputMean/OutputArea` | double/double | 📊 统计 |
| `HistogramData` | double[] | 📊 分析 |
| `IsPass/FailReasons` | bool/List<string> | ✅ 判断 |

### 3.4 算法流程

```
1. 输入图像
2. 灰度转换（彩色时调用 Rgb1ToGray）
3. 根据 Method 选择阈值策略：
   a. 固定阈值 → Cv2.Threshold(input, dst, threshold, maxValue, type)
   b. Otsu/Triangle → type | THRESH_OTSU | THRESH_TRIANGLE
   c. 自适应 → Cv2.AdaptiveThreshold(input, dst, maxValue, method, type, blockSize, C)
   d. 范围阈值 → Cv2.InRange(input, lower, upper)
4. Invert 标志处理
5. 统计计算
6. 结果判断
```

### 3.5 UI XAML 布局

```
┌─ TabControl ────────────────────────────────────┐
│ [基本参数] [运行参数] [结果显示]                   │
├─────────────────────────────────────────────────┤
│ 基本参数                                        │
│   输入图像: [选择图像源...]                     │
│   ☑ 启用区域限制                                │
│    ┌─ 区域设置 ──────────────────────────┐      │
│    │  [检测区域] [屏蔽区域]               │      │
│    │   矩形 圆形 多边形...                │      │
│    └──────────────────────────────────────┘      │
│   位置修正: [ToggleSwitch]                       │
│   修正信息: [选择数据源...]                      │
├─────────────────────────────────────────────────┤
│ 运行参数                                        │
│   阈值方法: [固定阈值 ▼]                      │
│   阈值:       [🔗] [====128====] △▽            │
│   最大值:     [🔗] [====255====] △▽            │
│   ── 自适应阈值 ──                              │
│   块大小:     [🔗] [===11=====] △▽             │
│   C常数:      [🔗] [===2.0====] △▽             │
│   ── 范围阈值 ──                                │
│   最小阈值:   [🔗] [===0======] △▽             │
│   最大阈值:   [🔗] [===255====] △▽             │
│   ☐ 反转结果                                    │
├─────────────────────────────────────────────────┤
│ 结果显示                                        │
│ ┌─ 结果判断 ──────────────────────────────────┐ │
│ │ 输出为空判断                [ToggleSwitch]   │ │
│ │ 白色像素比例                [ToggleSwitch]   │ │
│ │   白色像素比例(%): [0] ──────── [100]       │ │
│ │ 输出均值判断                [ToggleSwitch]   │ │
│ │   输出均值范围:    [0] ──────── [255]       │ │
│ │ 输出面积判断                [ToggleSwitch]   │ │
│ │   输出面积范围:    [0] ────── [1000000]     │ │
│ └──────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

---

## 四、工具2：图像滤波 (ImageFilterTool)

### 4.1 OpenCV 方法覆盖

| 方法 | OpenCV 函数 | 优先级 | 说明 |
|------|------------|--------|------|
| 均值滤波 | `Cv2.Blur()` | ★★★ 核心 | 简单平滑 |
| 高斯滤波 | `Cv2.GaussianBlur()` | ★★★ 核心 | 高斯权重 |
| 中值滤波 | `Cv2.MedianBlur()` | ★★★ 核心 | 去椒盐噪声 |
| 双边滤波 | `Cv2.BilateralFilter()` | ★★ 高频 | 保边去噪 |
| 自定义卷积 | `Cv2.Filter2D()` | ★ 低频 | 任意核 |

### 4.2 参数设计

```csharp
public enum FilterType { Blur, Gaussian, MedianBlur, Bilateral, Filter2D }
public enum BorderType { Reflect, Replicate, Constant, Reflect101, Wrap, Isolated }
```

**转换器 `FilterTypeConverter` 映射表：**

| 枚举值 | 中文显示 |
|--------|---------|
| `Blur` | 均值滤波 |
| `Gaussian` | 高斯滤波 |
| `MedianBlur` | 中值滤波 |
| `Bilateral` | 双边滤波 |
| `Filter2D` | 自定义卷积 |

**转换器 `BorderTypeConverter` 映射表：**

| 枚举值 | 中文显示 |
|--------|---------|
| `Reflect` | 镜像反射(Reflect) |
| `Replicate` | 边缘复制(Replicate) |
| `Constant` | 常量填充(Constant) |
| `Reflect101` | 镜像反射101(Reflect101) |
| `Wrap` | 环绕(Wrap) |
| `Isolated` | 独立(Isolated) |

**[JsonDerivedType(typeof(ImageFilterParameters), "ImageFilter")]**
**ImageFilterParameters : RegionParameters**（继承自 `RegionParameters`）

**基本参数 Tab（同图像二值化）：**
| 控件 | 参数属性 | 类型 | 默认值 |
|------|---------|------|--------|
| ImageSourceSelector | `ImageSource` | `AvailableDataSource?` | null |
| CheckBox | `IsRegionEnabled` | `ParamValue<bool>` | false |
| RegionEditorManagerControl | `InspectionRegions/MaskRegions` | `RegionCollectionParameter` | - |
| ToggleSwitch | `IsPositionCorrectionEnabled` | `ParamValue<bool>` | false |
| ParamBinding | `PositionCorrectionBindingSource/DisplayText` | `string` | - |

**运行参数 Tab：**
| 控件 | 参数属性 | 类型 | 默认值 | 范围 | 可见条件 |
|------|---------|------|--------|------|---------|
| ComboBox | `FilterType` | `ParamValue<FilterType>` | Gaussian | - | 始终 |
| ConfigSetting | `KernelWidth` | `ParamValue<int>` | 3 | [1,31] 奇数 | 始终 |
| ConfigSetting | `KernelHeight` | `ParamValue<int>` | 3 | [1,31] 奇数 | 非MedianBlur |
| ConfigSetting | `SigmaX` | `ParamValue<double>` | 0.0 | [0,100] | Gaussian |
| ConfigSetting | `SigmaY` | `ParamValue<double>` | 0.0 | [0,100] | Gaussian |
| ConfigSetting | `SigmaColor` | `ParamValue<double>` | 10 | [0,200] | Bilateral |
| ConfigSetting | `SigmaSpace` | `ParamValue<double>` | 10 | [0,200] | Bilateral |
| ComboBox | `BorderType` | `ParamValue<BorderType>` | Reflect | - | 始终 |

### 4.3 结果类

**ImageFilterResults : ToolResults**

| 属性 | 类型 | 显示分组 |
|------|------|---------|
| `OutputImage` | `Mat?` | ✅ 主显示图像 |
| `GrayImage` | `Mat?` | ℹ️ 中间结果 |
| `FilterType/Kernel/KernelUsed` | 类型回写 | ℹ️ 参数回写 |
| `InputSize/InputChannels` | Size/int | ℹ️ 输入信息 |
| `OutputMean/OutputMin/OutputMax/OutputStd` | double | 📊 统计 |
| `ChannelMeans` | double[] | 📊 分析 |
| `IsPass/FailReasons` | bool/List<string> | ✅ 判断 |

### 4.4 UI XAML 布局

```
┌─ TabControl ────────────────────────────────────┐
│ [基本参数] [运行参数] [结果显示]                   │
├─────────────────────────────────────────────────┤
│ 运行参数                                        │
│   滤波类型: [高斯滤波 ▼]                      │
│   内核宽度:     [🔗] [===3======] △▽           │
│   内核高度:     [🔗] [===3======] △▽           │
│   ── 高斯滤波参数 ──                            │
│   SigmaX:       [🔗] [===0.0====] △▽           │
│   SigmaY:       [🔗] [===0.0====] △▽           │
│   ── 双边滤波参数 ──                            │
│   SigmaColor:   [🔗] [===10=====] △▽           │
│   SigmaSpace:   [🔗] [===10=====] △▽           │
│   边界处理: [镜像反射(Reflect) ▼]             │
├─────────────────────────────────────────────────┤
│ 结果显示                                        │
│ ┌─ 结果判断 ──────────────────────────────────┐ │
│ │ 输出为空判断                [ToggleSwitch]   │ │
│ │ 输出均值判断                [ToggleSwitch]   │ │
│ │   输出均值范围:    [0] ──────── [255]       │ │
│ └──────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

---

## 五、工具3：形态学处理 (MorphologyTool)

### 5.1 OpenCV 方法覆盖

| 方法 | OpenCV 函数 | 优先级 | 说明 |
|------|------------|--------|------|
| 腐蚀 | `Cv2.Erode()` | ★★★ 核心 | 缩小前景 |
| 膨胀 | `Cv2.Dilate()` | ★★★ 核心 | 扩大前景 |
| 开运算 | `Cv2.MorphologyEx()` + MORPH_OPEN | ★★★ 核心 | 去噪点 |
| 闭运算 | `Cv2.MorphologyEx()` + MORPH_CLOSE | ★★★ 核心 | 填孔洞 |
| 梯度 | `Cv2.MorphologyEx()` + MORPH_GRADIENT | ★★ 高频 | 边缘提取 |
| 顶帽 | `Cv2.MorphologyEx()` + MORPH_TOPHAT | ★★ 高频 | 亮细节 |
| 黑帽 | `Cv2.MorphologyEx()` + MORPH_BLACKHAT | ★★ 高频 | 暗细节 |

### 5.2 参数设计

```csharp
public enum MorphOperation { Erode, Dilate, Open, Close, Gradient, TopHat, BlackHat }
public enum MorphShape { Rect = 0, Cross = 1, Ellipse = 2 }
```

**转换器 `MorphOperationConverter` 映射表：**

| 枚举值 | 中文显示 |
|--------|---------|
| `Erode` | 腐蚀 |
| `Dilate` | 膨胀 |
| `Open` | 开运算 |
| `Close` | 闭运算 |
| `Gradient` | 形态学梯度 |
| `TopHat` | 顶帽 |
| `BlackHat` | 黑帽 |

**转换器 `MorphShapeConverter` 映射表：**

| 枚举值 | 中文显示 |
|--------|---------|
| `Rect` | 矩形 |
| `Cross` | 十字形 |
| `Ellipse` | 椭圆形 |

**[JsonDerivedType(typeof(MorphologyParameters), "Morphology")]**
**MorphologyParameters : RegionParameters**（继承自 `RegionParameters` 而非 `ToolParameters`）

**基本参数 Tab（同图像二值化）：**
| 控件 | 参数属性 | 类型 | 默认值 |
|------|---------|------|--------|
| ImageSourceSelector | `ImageSource` | `AvailableDataSource?` | null |
| CheckBox | `IsRegionEnabled` | `ParamValue<bool>` | false |
| RegionEditorManagerControl | `InspectionRegions/MaskRegions` | `RegionCollectionParameter` | - |
| ToggleSwitch | `IsPositionCorrectionEnabled` | `ParamValue<bool>` | false |
| ParamBinding | `PositionCorrectionBindingSource/DisplayText` | `string` | - |

**运行参数 Tab：**
| 控件 | 参数属性 | 类型 | 默认值 | 范围 |
|------|---------|------|--------|------|
| ComboBox | `Operation` | `ParamValue<MorphOperation>` | Open | [枚举] |
| ComboBox | `KernelShape` | `ParamValue<MorphShape>` | Rect | [枚举] |
| ConfigSetting | `KernelWidth` | `ParamValue<int>` | 3 | [1,31] 奇数 |
| ConfigSetting | `KernelHeight` | `ParamValue<int>` | 3 | [1,31] 奇数 |
| ConfigSetting | `Iterations` | `ParamValue<int>` | 1 | [1,20] |
| ComboBox | `BorderType` | `ParamValue<BorderType>` | Reflect | [枚举] |

**结果显示 Tab：** 除通用的输出为空判断外，增加面积判断和 Blob 数量判断。

### 5.3 结果类

**MorphologyResults : ToolResults**

| 属性 | 类型 | 显示分组 |
|------|------|---------|
| `OutputImage` | `Mat?` | ✅ 主显示图像 |
| `GrayImage` | `Mat?` | ℹ️ 中间结果 |
| `Operation/KernelShape/KernelUsed/Iterations` | 类型回写 | ℹ️ 参数回写 |
| `InputSize/InputChannels` | Size/int | ℹ️ 输入信息 |
| `OutputMean/BlobCount/TotalArea/MaxBlobArea` | double/int/double/double | 📊 统计 |
| `ChannelMeans` | double[] | 📊 分析 |
| `IsPass/FailReasons` | bool/List<string> | ✅ 判断 |

### 5.4 UI XAML 布局

```
┌─ TabControl ────────────────────────────────────┐
│ [基本参数] [运行参数] [结果显示]                   │
├─────────────────────────────────────────────────┤
│ 运行参数                                        │
│   操作类型: [开运算 ▼]                         │
│   结构元素形状: [矩形 ▼]                       │
│   内核宽度:     [🔗] [===3======] △▽           │
│   内核高度:     [🔗] [===3======] △▽           │
│   迭代次数:     [🔗] [===1======] △▽           │
│   边界处理: [镜像反射(Reflect) ▼]             │
├─────────────────────────────────────────────────┤
│ 结果显示                                        │
│ ┌─ 结果判断 ──────────────────────────────────┐ │
│ │ 输出为空判断                [ToggleSwitch]   │ │
│ │ 面积判断                    [ToggleSwitch]   │ │
│ │   面积范围(px²):  [0] ──────── [1000000]    │ │
│ │ Blob数量判断                [ToggleSwitch]   │ │
│ │   Blob数量:       [0] ──────── [9999]       │ │
│ └──────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

---

## 六、公共控件映射

| 控件名 | 用途 | 命名空间 |
|--------|------|---------|
| `ImageSourceSelector` | 输入图像选择器 | `SunEyeVision.Plugin.SDK.UI.Controls` |
| `ConfigSetting` | 数值参数控件（支持常量模式+绑定模式） | `SunEyeVision.Plugin.SDK.UI.Controls` |
| `LabeledControl` | 带标签参数容器 | `SunEyeVision.Plugin.SDK.UI.Controls` |
| `ToggleSwitch` | 开关切换 | `SunEyeVision.Plugin.SDK.UI.Controls` |
| `RangeInputControl` | 范围输入(min/max) | `SunEyeVision.Plugin.SDK.UI.Controls` |
| `ToolDebugControlBase` | 调试控件基类 | `SunEyeVision.Plugin.SDK.UI.Controls` |
| `ToolCommands` | 标准命令(Execute/Confirm) | `SunEyeVision.Plugin.SDK.Commands` |

---

## 七、实施步骤

### 7.1 准备工作

1. 为每个工具创建 csproj 项目文件（复制 Threshold 模板）
2. 为每个工具创建目录结构
3. 在解决方案文件（.slnx）中注册三个新项目

### 7.2 开发顺序（每个工具独立）

```
Step 1: 枚举定义 + 结果判断配置类
Step 2: 参数类（继承 RegionParameters + [JsonDerivedType]）
Step 3: 结果类（继承 ToolResults + GetDisplayImages/GetPropertyMap）
Step 4: 工具主类（[Tool] + IToolPlugin<TParams, TResult> + Run()）
Step 5: 调试控件 XAML（三Tab布局：基本参数/运行参数/结果显示）+ 代码后置
Step 6: 数据分析控件 XAML + 代码后置
Step 7: 枚举转换器（如需）
Step 8: 编译验证
```

### 7.3 依赖关系

- 三个工具互相独立，无依赖关系
- 均依赖 `Plugin.SDK` 和 `CvEngine`
- 可**并行开发**
