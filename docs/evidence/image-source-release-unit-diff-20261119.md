# ImageSource 原厂发布单元与自研结构差异

## 证据基线

- 原厂版本：VisionMaster 4.4.0 Build20250516
- 原厂目录：`C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Collection\ImageSourceModule`
- 静态证据：原厂 DLL、XML、AlgorithmTab、Display、ToolItemInfo 和 Cs 文档
- 当前仓库：`D:\MyWork\SunEyeVision_V2\tools\SunEyeVision.ImageSource`

## 原厂事实

原厂 ImageSource 发布单元包含以下文件：

```text
ImageSourceModule/
├─ ImageSourceModule.dll
├─ ImageSourceModuleControl.dll
├─ ImageSourceModuleCs.dll
├─ ImageSourceModule.xml
├─ ImageSourceModuleAlgorithm.xml
├─ ImageSourceModuleAlgorithmTab.xml
├─ ImageSourceModuleCs.xml
├─ ImageSourceModuleDisplay.xml
├─ ToolItemInfo.xml
├─ ImageSourceModule_NormalLogo.png
├─ ImageSourceModule_StateLogo.png
└─ ImageSourceModuleImageEnable.png
```

`ImageSourceModuleAlgorithmTab.xml` 声明一个 `Tab_Basic Params` 基本页。静态顺序为：

```text
ImageSourceType
CurrentImagePath
PixelFormat
SolSaveImageData
ShowImageName
CameraID
ExposureTimeInput
GainInput
InitialSN
PathCache
OutMono8
StitchEnable
StitchStartHeight
StitchHeight
ClearTrigger
ClearTriggerCondition
AutoPlay
AutoStop
TriggerStringEnable
TriggerString
TriggerFilter
IsSubscribeFolderMode
SubscribeFolderPath
RefreshImage
TriggerProcessRun
```

原厂 `ImageSourceModuleDisplay.xml` 声明 `OutputImage`、`OutputImageMono8` 和两类 `Data Record` 显示对象。`ToolItemInfo.xml` 声明工具箱名称、显示类型、帮助 GUID 和优先级。

REA 对 `ImageSourceModuleControl.dll` 的静态托管元数据检查确认：程序集版本为 `1.0.0.0`，架构为 x86，包含 10 个类型、78 个方法和 1 个嵌入资源。已识别 `ImageSourceModuleControl.ImageListPanel`、`ImageSourceModuleControl.LocalImageListView`、`Apps.XmlParser.ParamTab.LocalImageList`、`GeneralParamListVisibleOperation` 和 `GeneralParamListHiddenOperation`。这些类型证明原厂存在独立的图像列表控件和参数页可见性操作，但尚未证明其完整运行行为。

REA 用户字符串进一步确认原厂嵌入了两个 WPF 组件资源：`/ImageSourceModuleControl;component/imagelistpanel.xaml` 和 `/ImageSourceModuleControl;component/localimagelistview.xaml`。同时出现 `IsShowLocalImageList`、`IsExpanded`、`IsSelectMode`、`运行全部` 和 `运行选中` 等控件状态与运行选择文本。该证据支持“图像列表是原厂专属 Control 资源”的结论，不支持继续用普通自研文本或通用字段行替代。

对原厂 `ImageSourceModuleControl.g.resources` 做只读资源枚举，实际包含 3 个 BAML 条目：

```text
localimagelistview.baml                  5407 bytes  1D8FD72744160AA8BC5FDE50FA12996EBC938D3C4FEC763C0F9F2B871C41C9DB
datatemplate/paramusercontroldatatemplate.baml 2769 bytes  D79E29C4D0D8372764B9ED7F4E19C1819FFCB4164ED16EDA1F3C23F78FBA845F
imagelistpanel.baml                     13302 bytes A30B2027677A616D8FF41C6461904EBC5EBC7D9DFC31C0DE97B1E63C8F4CEFAD
```

这确认原厂 Control 同时拥有图像列表视图、图像列表面板和参数用户控件模板三类编译资源。BAML 内部模板语义尚未反编译完成，当前只把资源身份和摘要作为静态证据。

REA 对原厂 Control 的方法签名进一步确认了专属职责边界。`ImageListPanel` 提供构造、`ChangeShadowDataSource` 和 WPF 组件连接入口。`LocalImageListView` 提供 `IsExpanded`、`IsSelectMode`、`Root`、`ImageModes`、`AutoPlay`、`Interval`、`CurrentImagePath`、选择变化、模式变化、清空参数视图和模板应用入口。`Apps.XmlParser.ParamTab.LocalImageList` 提供 `IsShowLocalImageList` 和 `ImageAlgorithmTabRoot` 装饰源连接。这些是原厂 Control 的静态接口事实，当前自研 `ImageSourceParameterEditor` 没有等价的专属 Control 接口。

已建立 `ImageSourceFieldMapping.json`，将原厂基本页 25 项逐项映射到当前字段身份。`CurrentImagePath` 被标记为组合映射，原厂图像列表选择语义、刷新/触发事件时序和 BAML 可见性布局仍保持未确认。

## 当前自研事实

当前项目仍是两个项目和一套自研 WPF 视觉入口：

```text
tools/SunEyeVision.ImageSource/
├─ Runtime/
│  ├─ ImageSourceTool.cs
│  ├─ ImageSourceFields.cs
│  ├─ ImageSourceSettings.cs
│  ├─ ImageSourceSession.cs
│  ├─ ImageSourceOutput.cs
│  └─ SunEyeVision.ImageSource.Runtime.csproj
└─ Wpf/
   ├─ ImageSourceEditorFactory.cs
   ├─ Design/
   ├─ Views/ImageSourceParameterEditor.xaml
   └─ SunEyeVision.ImageSource.Wpf.csproj
```

当前参数声明主要集中在 `ImageSourceFields.cs`，页面通过 `DeclaredSettingsView` 组装，WPF 入口由 `ImageSourceEditorFactory` 提供。当前没有与原厂文件一一对应的 `Algorithm.xml`、`AlgorithmTab.xml`、`Display.xml` 和 `ToolItemInfo.xml` 发布资产。

## 差异结论

| 区域 | 原厂证据 | 当前实现 | 状态 | 处理门禁 |
|---|---|---|---|---|
| 发布边界 | 单一 ImageSourceModule 发布目录 | Runtime 与 Wpf 两个项目 | 已确认差异 | 先建立发布资产映射 |
| 参数默认值 | Algorithm.xml 的 ParamItem | ImageSourceSettings 与 ImageSourceFields | 已确认差异 | 逐字段映射后迁移 |
| 页签和顺序 | AlgorithmTab.xml 单一基本页和固定顺序 | C# 字段 Layout 动态组装 | 已确认差异 | 原厂顺序测试闭合后实施 |
| 条件显示 | XML Triggers/Setters | FieldCondition 与自研条件 | 已确认差异 | 只迁移有证据的触发关系 |
| 结果显示 | Display.xml 的 image/datarecord 对象 | ImageSourceOutput.Display 与 ToolSchema | 部分映射 | 需要结果视图静态和运行证据 |
| 工具箱元数据 | ToolItemInfo.xml | 当前项目注册元数据 | 未完成映射 | 生成发布清单并验收 |
| 专属控件 | ImageSourceModuleControl.dll，含 ImageListPanel、LocalImageListView 和参数可见性操作 | ImageSourceEditorFactory 与 WPF 页面 | 静态已确认，运行未确认 | 不得用自研页面冒充原厂 Control |

## 结论

当前 ImageSource 尚未完成工程结构同构。本文件只记录证据和差异，不授权直接删除 Runtime/Wpf 或重写参数链。下一步先补齐发布单元映射和原厂控件入口证据，再进入代码迁移。

## 2026-11-19 审定增量

原厂 ImageSourceModuleAlgorithmTab.xml 的单一 Tab 内实际有三个 Category，顺序和字段归属已由 XML 直接读取并写入 ImageSourceFieldMapping.json：

    Tab_Basic Params
      ImageSourceType, CurrentImagePath, PixelFormat, SolSaveImageData,
      ShowImageName, CameraID, ExposureTimeInput, GainInput, InitialSN,
      PathCache, OutMono8, StitchEnable, StitchStartHeight, StitchHeight,
      ClearTrigger, ClearTriggerCondition

    Tab_Trigger
      AutoPlay, AutoStop, TriggerStringEnable, TriggerString, TriggerFilter

    Tab_Subscribe_File
      IsSubscribeFolderMode, SubscribeFolderPath, RefreshImage, TriggerProcessRun

ImageSourceModuleAlgorithm.xml 另外声明 Interval、ExecuteType、SetCameraParamWaitTime。这些参数没有进入 AlgorithmTab.xml 当前页面，已记录为 Algorithm-only 运行参数，不能擅自加入可见参数页。

当前代码已将有原厂 Tab 顺序证据的自研 FieldPresentation.Order 调整为对应原厂索引，并移除原厂 Tab 未声明的自研 CameraTimeout 参数视觉行。CameraTimeout 仍保留在运行时设置模型中，待找到同版本原厂 Control 或运行证据后再决定是否存在视觉入口。相机专属 EnumerationCameraForImage、ButtonSelecter 和图像列表选择仍保持证据缺口状态，没有用普通文本行冒充。

新增静态门禁 scripts/verification/Verify-ImageSourceMappingStatic.ps1，当前结果为 pass=true，检查了 Tab 项数、映射项数、原厂顺序、三个 Category 顺序、当前自研字段声明覆盖和 Category 精确内容。输出为 .testdata/image-source-mapping-static-gate-20261119.json。

## 2026-11-19 目标结构迁移增量

本轮先做工程结构与发布单元迁移，不以局部测试代替原厂差异分析。ImageSource 和 Blob 均建立 `Resources/OriginalReleaseUnit` 证据锁定目录，并在 Runtime 工程中以 `CopyToOutputDirectory=Never` 链接，防止把原厂文件冒充可执行自研程序集。Blob 原厂清单为 `BlobReleaseUnitManifest.json`，ImageSource 原厂清单为 `ImageSourceReleaseUnitManifest.json`。

当前已确认的代码级结构差异：

- 原厂 `ToolItemInfo.xml` 的 ImageSource 显示类型为 `IMAGE`，自研工具图标键已从 `image-load` 改为原厂对应的 `image`。
- 原厂 `ShowImageName` 属于 AlgorithmTab 的 `Tab_Basic Params` 第 5 项，已纳入 ImageSource 主 SettingsSchema、持久化模型和基本页顺序。独立 DisplaySchema 已清空，避免同一参数在发布注册时重复。
- 原厂 Tab 外的 `Interval`、`ExecuteType`、`SetCameraParamWaitTime` 保持 Algorithm-only 证据状态，没有进入 UI。
- 原厂相机枚举、图像列表选择和 ButtonSelecter 仍未有足够运行证据，继续保留为专属 Control 迁移缺口。

新增结构门禁 `scripts/verification/Verify-ToolReleaseUnitStructure.ps1`，ImageSource/Blob 共 12 项检查全部通过，输出 `.testdata/tool-release-unit-structure-gate-20261119.json`。
