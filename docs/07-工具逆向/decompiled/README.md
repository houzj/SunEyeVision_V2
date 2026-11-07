# 反编译重建源码（控件库 + 参数面板控件库 + 工具模块控件程序集）

本目录是 VisionMaster 4.4.0 控件相关程序集的反编译重建结果，用于工具逆向时对照原件控件结构；同目录另存**共用组定义**与**语言字典**两份配置证据，工具参数面板还原要用。

分两批入库，来源方式不同，务必区分：

| 批次 | 内容 | 入库方式 |
|---|---|---|
| 第一批（2026-11-05） | 7 个公共控件库：VM.Controls / VMControls.WPF / VMBaseControls / VM.PcUI / Apps.XmlUI / Apps.Style / Apps.ColorFun | 原样继承自 Codex 的 VisionReplica 逆向项目，我方未逐行复验 |
| 第二批（2026-11-05） | `ModuleControls/`（73 个工具模块 `*Control.dll`）+ `XmlParser/`（`Apps.XmlParser` 基础参数控件库） | 我方从本机已安装原件直接反编译，哈希与覆盖验证见各自目录的 `README.md` |

**不是产品代码，不参与构建，不被打包。** 产品源码不得引用本目录任何文件。

## 一、来源与锚点

| 项 | 值 |
|---|---|
| 原始产物 | 第一批：`D:\MyWork\REA逆向测试\analysis\decompiled\`（Codex 的 VisionReplica 逆向项目）；第二批：我方直接反编译 |
| 源程序集目录 | 第一批：`D:\Program Files\VisionMaster4.4.0\Applications\myLibs\`；第二批：`...\Module(sp)\x64\**\*Control.dll`（73 个）与 `...\myLibs\Apps.XmlParser.dll` |
| 反编译工具 | 第二批：`ilspycmd` 11.1.0.9782（`-p` 项目模式） |
| 产品版本 | VisionMaster 4.4.0（V4.4.0 Build20250516@1555331） |
| 入库日期 | 2026-11-05（两批同日） |
| 继承关系 | 第一批原样继承自 Codex，我方未逐行复验；第二批为我方自行反编译，逐程序集 SHA-256 已记录 |

源程序集哈希（换机后用它核对「反编译产物是否对应同一版原件」）：

| 源 DLL | 大小 | SHA-256 |
|---|---|---|
| `VM.Controls.dll` | 762,368 B | `D36462C981F1DB26A377C84A7E0C7DE26F0F14EEF92E41A4ED0ACC24E4ADB222` |
| `VMControls.WPF.dll` | 2,279,936 B | `99FFA401C8784472913AD43813D68A8A5577890C00E149506E3E5664FA61C97D` |
| `VMBaseControls.dll` | 561,664 B | `D3ED7CE6CCA860AC89D671DC0FE4EE239A97063D18BED94D3AD97595D5D52555` |
| `VM.PcUI.dll` | 937,984 B | `87D21004FD3807213247623212F93C5F15A784DA78C4BC2C890A7906C3C92B42` |

## 二、已入库内容

| 本目录 | 源程序集 | .cs 数 | 文件总数 | 体积 |
|---|---|---|---|---|
| `Controls/` | VM.Controls | 289 | 389 | 1.27 MB |
| `WpfControls/` | VMControls.WPF | 215 | 826 | 2.99 MB |
| `BaseControls/` | VMBaseControls | 133 | 222 | 0.79 MB |
| `PcUi/` | VM.PcUI | 162 | 249 | 1.47 MB |
| `XmlUi/` | Apps.XmlUI（参数面板控件库） | 130 | 509 | 1.45 MB |
| `AppsStyle/` | Apps.Style（公共样式与图标库） | 1 | 757 | 1.34 MB |
| `ColorFun/` | Apps.ColorFun（主题色字典） | 3 | 8 | 0.08 MB |
| `ModuleControls/` | 73 个工具模块 `*Control.dll`（逐程序集清单见其 `README.md`） | 822 | 1,189 | 6.03 MB |
| `XmlParser/` | Apps.XmlParser（基础参数控件库） | 365 | 366 | 1.11 MB |
| **合计** | — | **2,120** | **4,515** | **16.53 MB** |

> `ModuleControls/` 与 `XmlParser/` 补上了「工具参数面板还原」最后一环：工具 XML 里出现的
> **173 个控件类型**，仅靠第一批 7 个库命中 0 个，加这两项后达到 172 个，剩余 1 个已查实为
> XML 元素名而非控件类型（详见 `ModuleControls/README.md` 第二节）。

新增两个程序集的来源哈希：

| 源 DLL | 大小 | SHA-256 |
|---|---|---|
| `Apps.XmlUI.dll` | 1,039,872 B | `639D0FB9DFCA81E38DF690E201B73874CE006D1361B689DDC6D5F41F7B0B04FD` |
| `Apps.Style.dll` | 952,832 B | `8980A9964C8C530511AE79FE04CF19534DC14377CDC73FFE03F26921F7D0DB63` |
| `Apps.ColorFun.dll` | 80,384 B | `22C277C4E2D384593265EE0D40830252F8317C60A7EBE782D31AEE426C5D8688` |

另有两类**非反编译**的配置证据，同属工具逆向必需材料：

| 本目录 | 原件路径 | 体积 | SHA-256 | 用途 |
|---|---|---|---|---|
| `Groups/Group.xml` | `D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\Group.xml` | 233,943 B | `9129B82A0F9906DDE591347498A80E2E8C019C0FB959A721ECFD24FBBB5207DC` | 34 个共用参数组定义（图像源组 / ROI 创建组 / 夹具组 / 继承方式组 / 底部命令组……），工具 XML 里的 `GroupLinkItem` 靠它展开 |
| `Lang/zh-cn.xaml` | `D:\Program Files\VisionMaster4.4.0\Applications\Lang\zh-cn.xaml` | 839,881 B | `8DBABDC771FC81174D02C40E37843EF4CCCE97133F41988FF976A5BD556B8B7E` | 主语言字典（9,766 条），工具参数显示键的中文来源 |
| `Lang/gateway-zh-cn.xaml` | `D:\Program Files\VisionMaster4.4.0\Applications\GateWay\Lang\zh-cn.xaml` | 782,105 B | `35FA9A40DBEF24434F3B84EB42B6680895513E25032B8B9240F618FA880330CE` | 通讯/网关参数补充字典（4,821 条新增键） |

`XmlUi/` 是参数面板还原的关键材料：`ParamsTabUserControl.xaml` 是面板骨架（页签 + 内容 + 底部按钮），`ParamCategoryUI.xaml` 是分类分组，`resourcedictionaries/globalresources.xaml` 是分组标题线条与字号，`styles/paramusercontroldatatemplate.xaml` 是每种控件（Boolean / Integer / Enumeration / Bettween / ROISelecter / Expand / ImageViewConfig …）的 DataTemplate，`Apps.XmlUI.ParamTabControl/` 下是数字框、ROI 形状选择、滑块区间等具体控件，附 311 个原厂图标 PNG。

`AppsStyle/` 与 `ColorFun/` 补齐了 XmlUi 引用但不属于 XmlUi 的两块：`AppsStyle/` 是 27 个真实 Style/Template 文件（`buttonstyle/togglebuttonstyle.xaml` 里的各 `*RoiBtnStyle`、`radiobuttonstyle/radiobuttonstyle.xaml` 的 `NormalRadioButton`、`comboboxstyle/`、`textboxstyle/` 等）加 **727 个原厂图标**（`swith_on/off.png` 48×32、`check-box_on/off.png` 24×24、`ROI_*.png` 32×32、`combo_ic_*` 等）；`ColorFun/` 是 4 套主题字典，参数面板用到的 `Col_Theme=#E76900`、`Col_ModuleConfig_List_Foreground=#666666`、`Col_SubForm_Background=#F2F3F5`、`Col_ModuleConfig_ComboBox_BorderBrush=#CCC7C6` 都出自 `colorstyle1.xaml`。

文件总数大于 `.cs` 数，因为同时保留了反编译附带的 XAML、BAML、资源字典与图片；这些是还原控件视觉的必要材料，不删。

## 三、原件目录 → 我方控件族映射

我方控件族位于 `src/Plugin.Wpf/Controls/Native/`，按用途分层。对照规则：**命名按职责替换，结构层级保持一致**。

**本节只是程序集级粗映射，便于检索定位。逐文件精确映射的权威源是：**

- `docs/collaboration/evidence/公共控件中立命名路径对照-20261103.html` —— 逐文件「迁移前路径 → 本方路径」全量对照（正文 624K 字符，基线 `f3ebe4318`）
- `docs/collaboration/evidence/公共控件消费者中立映射-20261103.json` —— 消费者侧映射

**注意一处历史遗留**：`src/Plugin.Wpf/Controls/Native/Input/migration-manifest.json`（1.86 MB，基线 `b87fb685`）记录的是**早期批次策略**——保留原厂命名空间加 `SunEyeVision` 前缀（如 `SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF`），输出多为 `.pending`。当前实际代码已改为中立化命名（实测 `WorkspaceControls/AnnularSectorIn.cs` 的 namespace 为 `SunEyeVision.Plugin.Wpf.Native.Input.WorkspaceControls`）。**做映射时以中立命名对照表为准，不以该 manifest 为准。**

### 3.1 `Controls/`（VM.Controls → PresentationControls）

| 原件命名空间/目录 | 我方目录 |
|---|---|
| `VM.Controls` | `Display/PresentationControls/Presentation.Controls` |
| `VM.Controls.AttachedProperties` | `Display/PresentationControls/Presentation.Controls.AttachedProperties` |
| `VM.Controls.Chromes` | `Display/PresentationControls/Presentation.Controls.Chromes` |
| `VM.Controls.Converters` | `Display/PresentationControls/Presentation.Controls.Converters` |
| `VM.Controls.Core` | `Display/PresentationControls/Presentation.Controls.Core` |
| `VM.Controls.Core.Converters` | `Display/PresentationControls/Presentation.Controls.Core.Converters` |
| `VM.Controls.Core.Input` | `Display/PresentationControls/Presentation.Controls.Core.Input` |
| `VM.Controls.Core.Media` | `Display/PresentationControls/Presentation.Controls.Core.Media` |
| `VM.Controls.Core.Media.Animation` | `Display/PresentationControls/Presentation.Controls.Core.Media.Animation` |
| `VM.Controls.Core.Utilities` | `Display/PresentationControls/Presentation.Controls.Core.Utilities` |
| `VM.Controls.Primitives` | `Display/PresentationControls/Presentation.Controls.Primitives` |
| `VM.Controls.PropertyGrid` | `Display/PresentationControls/Presentation.Controls.PropertyGrid` |
| `VM.Controls.PropertyGrid.Attributes` | `Display/PresentationControls/Presentation.Controls.PropertyGrid.Attributes` |
| `VM.Controls.PropertyGrid.Commands` | `Display/PresentationControls/Presentation.Controls.PropertyGrid.Commands` |
| `VM.Controls.PropertyGrid.Converters` | `Display/PresentationControls/Presentation.Controls.PropertyGrid.Converters` |
| `VM.Controls.PropertyGrid.Definitions` | `Display/PresentationControls/Presentation.Controls.PropertyGrid.Definitions` |
| `VM.Controls.PropertyGrid.Editors` | `Display/PresentationControls/Presentation.Controls.PropertyGrid.Editors` |
| `VM.Controls.Themes` | `Display/PresentationControls/Presentation.Controls.Themes` |
| `VM.Controls.Utility` | `Display/PresentationControls/Presentation.Controls.Utility` |
| `VM.Utility` | `Display/PresentationControls/View.Utility` |
| `VM.Utility.Convert` | `Display/PresentationControls/View.Utility.Convert` |
| `HikVision.Styles` / `styles` / `themes` | 并入 `Display/PresentationControls/PlatformStyles.Styles` 与主题资源 |

### 3.2 `WpfControls/`（VMControls.WPF → WorkspaceControls）

| 原件 | 我方目录 |
|---|---|
| `VMControls.WPF` | `Input/WorkspaceControls/WorkspaceControls` |
| `VMControls.WPF.Base` | `Input/WorkspaceControls/WorkspaceControls.Base` |
| `VMControls.WPF.Base.Astar` | `Input/WorkspaceControls/WorkspaceControls.Base.Astar` |
| `VMControls.WPF.Base.ExternAdorner` | `Input/WorkspaceControls/WorkspaceControls.Base.ExternAdorner` |
| `VMControls.WPF.Base.ExternServer` | `Input/WorkspaceControls/WorkspaceControls.Base.ExternServer` |
| `VMControls.WPF.CalibGuide` | `Input/WorkspaceControls/WorkspaceControls.CalibGuide` |
| `VMControls.WPF.CameraAcq` | `Input/WorkspaceControls/WorkspaceControls.CameraAcq` |
| `VMControls.WPF.DataForUI` | `Input/WorkspaceControls/WorkspaceControls.DataForUI` |
| `VMControls.WPF.GlobalUI` | `Input/WorkspaceControls/WorkspaceControls.GlobalUI` |
| `VMControls.WPF.GlobalUI.GlobalTrigger` | `Input/WorkspaceControls/WorkspaceControls.GlobalUI.GlobalTrigger` |
| `VMControls.WPF.TimeDialog` | `Input/WorkspaceControls/WorkspaceControls.TimeDialog` |

### 3.3 `BaseControls/`（VMBaseControls → ImageViewport.Base）

| 原件 | 我方目录 |
|---|---|
| `VMBaseControls` / `VMBaseControls.Base` | `Display/ImageViewport/ImageViewport.Base` |
| `VMBaseControls.Base.ImageView` | `Display/ImageViewport/ImageViewport.Base.ImageView` |
| `VMBaseControls.Base.ImageView.ROI` | `Display/ImageViewport/ImageViewport.Base.ImageView.ROI` |
| `VMBaseControls.Base.ImageView.Shape` | `Display/ImageViewport/ImageViewport.Base.ImageView.Shape` |

### 3.4 `PcUi/`（VM.PcUI / HikPcUI → Interaction）

| 原件 | 我方目录 |
|---|---|
| `HikPcUI` | `Input/Interaction.Pc/Interaction` |
| `HikPcUI.Commom` / `HikPcUI.Common` | `Input/Interaction.Pc/Interaction.Common`（原厂拼写 Commom 与 Common 并存，两处都保留） |
| `HikPcUI.Controls` | `Input/Interaction.Pc/Interaction.Controls` |
| `HikPcUI.Converts` | `Input/Interaction.Pc/Interaction.Converts` |
| `HikPcUI.ImageView` | `Input/Interaction.Pc/Interaction.ImageView` |
| `HikPcUI.ListView` | `Input/Interaction.Pc/Interaction.ListView` |
| `HikPcUI.Models` | `Input/Interaction.Pc/Interaction.Models` |
| `HikPcUI.Utility` | `Input/Interaction.Pc/Interaction.Utility` |
| `Apps.PcUI.Common` | 目标目录 `Input/Apps.PcUI/Apps.PcUI.Controls` 当前为空，尚未落地；实际归属按中立命名对照表确认 |

## 四、尚未入库的程序集（按需补）

| 源程序集 | .cs 数 | 我方对应 |
|---|---|---|
| `VMControls.WPF.Release` | 53 | `Input/WorkspaceRelease/` |
| `Apps.PcUI.ImageSourceAudit` | 21 | 图像源相关，做图像源样板时优先补 |
| `VM.Core` | 114 | `SolutionCore`，做迁移链时补 |
| `CompatibleTool.Audit` | 35 | `SolutionMigration`，做迁移链时补 |
| `iMVS-6000PlatformSDKCS.PublicAudit` | 751 | `PlatformSdk`，做 native/ABI 环节时补 |

> 原表中的 `ImageSourceModuleControl`（7 个 .cs）已在第二批入库，见 `ModuleControls/ImageSourceModuleControl/`。

补录时沿用同一套命名规则与哈希锚点记录。

## 五、怎么检索

**按控件名找**：原件控件类型名（如 `HikCircleArc`、`HikROIBase`）在本目录内是原始类名，直接全文搜。

**按我方实现对照**：先在 `src/Plugin.Wpf/Controls/Native/` 找到中立化后的文件，再把命名反向替换（`WorkspaceControls` → `VMControls.WPF`、`Interaction` → `HikPcUI`、`Presentation.Controls` → `VM.Controls`）后在本目录搜。

**按功能找**：`Controls/` 是通用控件与 PropertyGrid；`WpfControls/` 是工作区与相机采集；`BaseControls/` 是图像视口与 ROI 形状；`PcUi/` 是图像查看、列表与交互。

**按工具找（做某个工具的配置界面时先走这条）**：

1. 确定工具 XML 里该参数的 `LocalName`（例：`ThresholdAreaList`）。
2. 在 `ModuleControls/*/datatemplate/paramusercontroldatatemplate.xaml` 里搜 `x:Key="该 LocalName"`，得到实际承载的 Panel 类名与绑定（例：`ThresholdAreaPanel`）。
3. 在该工具目录下打开同名 `*Panel.xaml` / `*Panel.xaml.cs`，读界面、取值与回写链路。
4. 若是通用类型（`Boolean` / `Integer` / `FloatBettween` / `Enumeration` / `ROISelecter` / `ButtonSelecter` / `Expand` / `GroupLinkItem` / `ImageViewConfig` 等），类实现在 `XmlParser/`，视觉模板在 `XmlUi/styles/paramusercontroldatatemplate.xaml`。
5. 分组参数（`GroupLinkItem` 展开的内容）来自 `Groups/Group.xml`，显示文字来自 `Lang/zh-cn.xaml`。

## 六、边界

- 本目录内容是**商业程序集的反编译重建产物**，仅用于本项目内部对照与自研实现
- 不对外分发、不随产品发布、不进入安装包
- 产品代码不得 `using` 本目录的任何命名空间，也不得引用其类型
- 保留原厂命名（含 `HikVision`、`HikPcUI`、`VM.*`）是为了溯源比对；产品侧一律按 SunEyeVision 职责命名，两侧不混用
- 反编译重建源码不是原始 C#，不能当作原厂实现的逐字事实；结论以「可确认范围」为准

Codex 原项目 `ROADMAP.md` 写明这些材料不随 Git 上传；本库按项目所有者决定纳入版本管理以便换机迁移，此差异在此记录。
