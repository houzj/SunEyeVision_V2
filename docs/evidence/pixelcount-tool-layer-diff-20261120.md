# PixelCount 工具层 UI 组装差异

本记录只比较工具层。公共 `Tab.Collection → CollectionViewSource → ParamListView → ParamUserControl` 链按 Blob 样板视为已打通，本轮不修改公共框架。

## 原厂工具层事实

来源为 `IMVSPixelCountModuVAAlgorithmTab.xml`、`IMVSPixelCountModuVA.xml` 和 `IMVSPixelCountModuVAControl.dll`。

| 原厂对象 | 工具层职责 | 本方当前状态 |
|---|---|---|
| `ImageSourceGroup` | 图像来源声明 | 只有 `PixelCountTool.Schema` 的 `input.image` 端口，缺少 PixelCount 自有 XML 分组声明 |
| `RoiSelectGroup`、`ROISelecter(RoiType)` | ROI 选择器和形状选项 | 只有通用 `input.roi` 端口，没有 PixelCount 专属 `ROISelecter` 声明 |
| `InputMaskImageGroup`、`OutputMaskImageGroup` | 输入/输出掩膜组 | Runtime 有 `input.mask` 和 `output.mask`，页签分组映射没有工具 XML 来源 |
| `InheritWayGroup` | ROI 继承方式 | 当前没有 PixelCount 工具层继承声明 |
| `AssistGroup` | 位置修正参数组 | 当前没有 PixelCount 工具层 AssistGroup 声明 |
| `PixelModuRangeConfigList` | 运行参数动态范围列表 | 已有 `PixelCountRangePanel`，但 Runtime 以单个文本字段 `range.thresholds` 承载动态行 |
| `CountLimitEnable`、`RangCountLimit` | 数量判定和数量范围 | 已由 `PixelCountFields` 声明，页签和触发条件有映射 |
| `RatioLimitEnable`、`RangRatioLimit` | 比率判定和比率范围 | 已由 `PixelCountFields` 声明，页签和触发条件有映射 |
| `CheckZone`、`FixtureInitPoint`、`FixtureRunPoint`、`ResutlText` | 结果图像显示对象 | 当前只声明 ROI、区间和文本显示，位置修正显示对象仍未实现 |
| `BottomCommandGroup`、`Button_ResetAlgoParams` | 底栏命令 | 当前由公共底栏接缝承载，PixelCount 没有工具层命令声明 |

## 静态专用控件事实

原厂 `IMVSPixelCountModuVAControl.dll` 含托管类型 `IMVSPixelCountModuVAControl.UI.RangeConfigListPanel`，以及 `Apps.XmlParser.ParamTab.PixelModuRangeConfigList` 和 `PixelModuRangeConfigItem`。这说明运行参数动态范围列表是工具专用动态控件接缝，可以由工具 Editor 提供，但其普通字段仍必须进入公共参数对象集合。

## 样板调整边界

PixelCount 样板先只改工具层，不改公共框架。

1. 增加 PixelCount 参数声明资源，明确基本参数、运行参数、结果显示三页及原厂分组身份。
2. 把 `input.image`、`input.roi`、`input.mask` 的 page/section 映射写入工具声明，交给现有输入组装器进入公共集合。
3. 保留 `PixelModuRangeConfigList` 专用面板作为唯一工具专属动态控件，并把其锚点注册到运行参数页的正式字段身份。
4. 数量/比率判定继续使用公共布尔触发器和范围模板。
5. 对原厂已有但本方没有证据支持的扇环 ROI、位置修正输出显示对象和底栏复位命令登记为未确认，不自行补视觉。

PixelCount 样板只有在上述工具层映射、静态探针和 Release 编译通过后，才复制到其他九个工具。
