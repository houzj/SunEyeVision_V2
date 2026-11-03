# Blob 参数依赖闭包产品职责命名归一批（2026-11-03）

## 基线和整批边界

C07完整正式闭口8a4ddafa7；产品c6e0e5e3a、140场景组与570项载荷独立保存。本批仅把已迁移的产品组织和标识改为SunEyeVision中立职责，不变原控件行为、参数身份、算法、存储格式或工作流合同，不新增控件，不合并不同原件家族。

原目录并非全是证据：1630跟踪路径含243cs/36xaml/859png/1ico/491pending；本批按实际Blob参数、数值、范围、引用、ROI依赖闭包实施，其他默认编译家族及非消费pending继续保完整待办，不能声称1630全部清零。原证据、原厂身份和来源注释保留。

## 确定性映射

以下Controls/Native与Themes/Native按实际对应文件同步。namespace以SunEyeVision.Plugin.Wpf.Native为根，最长前缀先换，不能仓库裸替换来源证据。

|原职责位置或namespace后缀|新位置或namespace后缀|
|---|---|
|Composition/Apps.XmlUI/Apps.XmlUI；Composition.Apps.XmlUI|Composition/Parameters/Core；Composition.Parameters.Core|
|Composition/Apps.XmlUI/Apps.XmlUI.ParamTab；Composition.Apps.XmlUI.ParamTab|Composition/Parameters/Rows；Composition.Parameters.Rows|
|Composition/Apps.XmlUI/Apps.XmlUI.ParamTabControl；Composition.Apps.XmlUI.ParamTabControl|Composition/Parameters/Editors；Composition.Parameters.Editors|
|Composition/Apps.XmlUI/resourcedictionaries/globalresources.xaml|Composition/Parameters/ParameterResources.xaml|
|Composition/Apps.XmlUI/paramtabcontrol/paramtabui/subui/clearbuttontextbox/clearbuttontextboxstyle.xaml|Composition/Parameters/Rows/ClearButtonTextBoxStyle.xaml|
|Numeric/AppsXmlUi；Numeric.Apps.XmlUI.ParamTabControl|Numeric/Coordinates；Numeric.Coordinates|
|Numeric/VMControls/UpDown；Numeric.VM.Controls|Numeric/UpDown；Numeric.UpDown|
|Numeric/VMControls/Editors；Numeric.VM.Controls.PropertyGrid.Editors|Numeric/UpDown/PropertyGrid/Editors；Numeric.UpDown.PropertyGrid.Editors|
|Dependencies/Apps.BaseData；Dependencies.Apps.BaseData|Dependencies/ValueConverters；Dependencies.ValueConverters|
|Dependencies/Apps.XmlParser；Dependencies.Apps.XmlParser.ParamTab|Dependencies/Regions；Dependencies.Regions|
|Dependencies/Apps.UIHelper；Dependencies.Apps.UIHelper.Extern|Dependencies/VisualElements；Dependencies.VisualElements|
|Input/VM.PcUI/HikPcUI；Input.HikPcUI|Input/FieldPrimitives；Input.FieldPrimitives|
|Input/VM.PcUI/HikPcUI.ListView；Input.HikPcUI.ListView|Input/FieldPrimitives/Editors；Input.FieldPrimitives.Editors|
|Input/VM.PcUI/HikPcUI.Commom；Input.HikPcUI.Commom|Input/FieldPrimitives/Shared；Input.FieldPrimitives.Shared|
|Input/VM.PcUI/HikPcUI.Controls；Input.HikPcUI.Controls|Input/FieldPrimitives/Controls；Input.FieldPrimitives.Controls|
|Input/VM.PcUI/HikPcUI.Converts；Input.HikPcUI.Converts|Input/FieldPrimitives/Converters；Input.FieldPrimitives.Converters|
|Input/VM.PcUI/HikPcUI.ImageView；Input.HikPcUI.ImageView|Input/FieldPrimitives/Imaging；Input.FieldPrimitives.Imaging|
|Input/VM.PcUI/HikPcUI.Models；Input.HikPcUI.Models|Input/FieldPrimitives/Models；Input.FieldPrimitives.Models|

资源：Composition/Apps.Style→Composition/Styles；Assets/Native/Composition/Apps.Style/Resources→Composition/Icons；Assets/Native/Composition/Apps.XmlUI→Composition/EditorAssets；Assets/Native/Numeric/Apps.XmlUI→Numeric/Coordinates；Assets/Native/Input/VM.PcUI→Input/FieldPrimitives。Themes/VmFieldStyles.xaml及provenance文件移到FieldStyles对应文件，来源字段仍保原文。HikTextBoxFg→FieldTextBoxForeground，Col_Menubar_VMInfo_*→Col_Menubar_ProductInfo_*，值和加载顺序不变；pack前缀保持程序集名称。

## 完整生产链及必须保留区别

DeclaredFieldsView/DeclaredFieldEditor→Core/Rows/Editors→Boolean/ROI/Coordinates及原资源，Numeric.Arranged→FieldPrimitives.Editors，ReferenceFieldSession/NativeTextSession→VisualElements，NativeRoiSession→Regions.RoiType。同步C#全名、using、XAML x:Class/xmlns、ResourceDictionary.Source、DP图片URI、Generic与Palette资源键、真实消费者及测试定位。

Numeric.Arranged（C05/C06）、Coordinates（ROI坐标）、UpDown（独立spinner）保持三家族；两套颜色选择器、Core/Rows同名转换器不合并。C07已中立Popups/References/Variables/Parameters类型保持。NativeResources.props现有中立Display/Primitives/ImageView/Mvvm资源不在移动集合。

## 唯一写入与验证出口

仅复用现有两候选树，不新增工作树：
- 公共实施线唯一写Plugin.Wpf产品源、主题、资源、当前Catalog路径/哈希及本批迁移映射脚本；不改历史证据。
- 消费者线唯一写src/UI、tools和scripts/verification现有真实测试/runner的受影响类型/路径定位（原validation路径笔误已核正；公共线仅本批专用迁移/结构检查，不双写这些既有测试），基于同一映射同步，不自行改公共控件。
- 主线唯一合同、原方案、任务账本、集中合并和正式Release核验；候选构建必须各自真实部署，不拷贝主仓Release或厂商库。

两线固定产品提交后互接；当前Catalog更新localPath/localSha256、dependencies.path/sha256及current集成路径/类型。originalAssembly/originalType/originalPath/originalSha256/MVID和历史migration-manifest固定快照不改，当前与历史分列。

实施前检查目标碰撞和URI闭包；实施后静态校验无本映射旧产品标识/失效引用、保来源字段不变及移动文件内容对应。标准UI/full Release构建；相称回归复用公共C07 10、B08混合12、ROI真实预览/窗口及图文All44以实际runner范围为准，核真实资源加载/同版身份和封包。纯命名不重复扩大算法/无关导出测试；失败按具体证据整批修正。候选与主仓正式分开，VS/人工另记，产品改名不称新功能完成。

## 实施前闭包核对补充
- Windows资源路径按大小写无关查找实际文件；namespace仍精确映射。源码与URI中的ResourceDictionaries/resources大小写不得造成漏迁。
- 同一UpDown家族的Themes/Native/Numeric/VMControls/controls/numericupdown/themes两份XAML同步到Numeric/UpDown对应路径，消费者O06WindowPresentation实际主题断言同步。仅组织映射，16项原主题组成不变。
- 消费者现有17个verification测试修改已固定e8028cdc0；两份原厂来源路径脚本及旧Boolean别名不存在断言不替换，src/UI/tools未发现本批产品引用。不是17个功能新增或测试通过。
