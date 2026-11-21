# PixelCount 目标架构

## 目标工程树

```text
tools/
└─ SunEyeVision.PixelCount/
   ├─ SunEyeVision.Tool.PixelCount.csproj
   ├─ PixelCountTool.cs
   ├─ Algorithms/
   │  ├─ PixelCountSession.cs
   │  ├─ PixelCountOperations.cs
   │  └─ PixelCountHalconAdapter.cs
   ├─ Parameters/
   │  ├─ PixelCountSettings.cs
   │  ├─ PixelCountParameterDefinition.cs
   │  ├─ PixelCountFields.cs
   │  ├─ PixelCountRoi.cs
   │  └─ PixelCountRanges.cs
   ├─ Results/
   │  ├─ PixelCountOutput.cs
   │  ├─ PixelCountDisplay.cs
   │  └─ PixelCountResultProjection.cs
   ├─ Editor/
   │  ├─ PixelCountEditorFactory.cs
   │  ├─ Views/
   │  │  ├─ PixelCountParameterEditor.cs
   │  │  └─ PixelCountRangePanel.cs
   │  └─ Design/
   └─ Resources/
      ├─ PixelCountParameters.xml
      ├─ PixelCountAlgorithmTab.xml
      ├─ PixelCountDisplay.xml
      └─ ToolItemInfo.xml
```

旧的 `Runtime/SunEyeVision.PixelCount.Runtime.csproj` 和 `Wpf/SunEyeVision.PixelCount.Wpf.csproj` 不属于目标工程入口。源码迁移完成并通过样板验证后再移除旧项目文件。

## 原厂工程结构分析

原厂工程结构是发布职责结构，不是本方必须复制的项目结构。

| 原厂发布单元 | 原厂职责 | 本方适配原则 |
|---|---|---|
| `IMVSPixelCountModuVA.dll` | 海康算法运行单元 | 不生成伪造同名算法 DLL。由工具算法会话调用 HALCON 适配层 |
| `IMVSPixelCountModuVACs.dll` | 参数、结果、ROI 和宿主桥接 | 归入单项目的工具入口、Parameters、Results 和 Session |
| `IMVSPixelCountModuVAControl.dll` | 原厂专用动态范围控件 | 只在有证据的范围列表处保留 Editor 专属面板 |
| `Algorithm.xml`、`AlgorithmTab.xml` | 参数身份、页签、分组、控件和顺序 | 作为工具 Resources，由公共参数框架消费 |
| `Display.xml` | 结果显示身份和数据类型 | 作为工具 Results/Resources，由公共结果框架消费 |
| `ToolItemInfo.xml` | 工具箱身份 | 作为工具资源并接入产品工具箱目录 |

因此本方的目标是职责等价。算法库可以不同，程序集数量可以不同，项目结构统一为单项目，公共窗口和参数生命周期由平台负责。

## 原厂事实到本方目标映射

| 原厂事实 | 本方目标 | 归属 |
|---|---|---|
| `IMVSPixelCountModuVA.dll` | `Algorithms/PixelCountSession.cs` + `PixelCountHalconAdapter.cs` | 工具算法接缝 |
| `IMVSPixelCountModuVACs.dll` | `PixelCountTool`、`PixelCountSettings`、`PixelCountOutput`、ROI/参数模型 | 工具运行合同 |
| `IMVSPixelCountModuVA.xml` | `PixelCountParameters.xml`、输入输出声明 | 工具资源与声明 |
| `IMVSPixelCountModuVAAlgorithm.xml` | 参数类型、默认值、范围和枚举 | 工具资源与声明 |
| `IMVSPixelCountModuVAAlgorithmTab.xml` | 页签、分组、`GroupLinkItem`、`ROISelecter`、底栏和顺序 | 工具资源，由公共框架消费 |
| `IMVSPixelCountModuVADisplay.xml` | `PixelCountDisplay.xml`、`PixelCountDisplay.cs` | 工具结果声明，由公共结果框架消费 |
| `IMVSPixelCountModuVAControl.dll` | `Editor/Views/PixelCountRangePanel.cs` | 有原厂证据的专属动态控件 |
| `ToolItemInfo.xml` | `Resources/ToolItemInfo.xml` 与工具箱目录映射 | 工具资源 |

## 公共框架接缝

PixelCount 不自行创建普通参数行。工具资源和声明进入公共链：

```text
PixelCountAlgorithmTab.xml
  → AlgorithmTabRoot / Tab / Category / GroupLinkItem
  → Tab.Collection
  → CollectionViewSource(src)
  → ParamListView
  → ParamItemStyle
  → ParamUserControl(DisplayType)
```

`ImageSourceGroup`、`RoiSelectGroup`、`InputMaskImageGroup`、`OutputMaskImageGroup`、`InheritWayGroup`、`AssistGroup` 和 `BottomCommandGroup` 属于公共参数对象链。PixelCount 只声明它们的身份、顺序和字段映射。

`PixelModuRangeConfigList` 是原厂专用动态控件。`PixelCountRangePanel` 只承担动态行增删、范围编辑和设置提交，外层页签、父分组、可见性、生命周期和底栏仍由公共框架负责。

## 当前实现与目标差异

1. 当前 PixelCount 参数声明主要由 `PixelCountFields.cs` 手工构造，缺少与 Blob 相同的参数 XML 入口。
2. 当前输入端口虽存在，但没有完整的 PixelCount `ImageSourceGroup`、`RoiSelectGroup`、掩膜组和 `AssistGroup` 工具资源映射。
3. 当前结果显示声明缺少原厂位置修正显示对象的完整映射，这些行为暂标未确认。
4. 当前动态范围面板已存在，但还没有在单项目目标中与原厂 `PixelModuRangeConfigList` 资源身份统一。
5. 当前 HALCON 执行只有本方测试，没有原厂同输入输出黄金样本，算法等价状态为未确认。

## 目标闭口条件

- 单项目能够同时注册 Runtime 和 Editor。
- PixelCount 参数 XML 能被公共声明加载器消费。
- 输入、ROI、掩膜、AssistGroup、结果判定和底栏都能在公共集合中定位。
- 动态范围列表只保留一个有证据的专用面板接缝。
- Release、静态映射、参数页探针和资源释放通过。
- 原厂与 HALCON 的黄金样本比较通过，或明确登记为算法未确认。
