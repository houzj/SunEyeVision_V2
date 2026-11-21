# PixelCount VA 原厂 UI 静态取证

取证目标为 VisionMaster 4.4.0 安装目录中的 `IMVSPixelCountModuVA`，用于 PixelCount 单项目样板迁移。本文只记录静态事实，不把本方当前实现当作原厂行为证据。

## 原厂文件身份

目录为 `C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Measurement\IMVSPixelCountModuVA`。

| 文件 | SHA-256 |
|---|---|
| `IMVSPixelCountModuVA.xml` | `93B4A18E81D8E5E51608A9714DE90B642D6FC3ED07478EEDC10AC34BAB2A2600` |
| `IMVSPixelCountModuVAAlgorithm.xml` | `BF33A761B8EC4ADA77122AB7E663EE715909D0BD9FE3C026EEFB024400A77A56` |
| `IMVSPixelCountModuVAAlgorithmTab.xml` | `DBA1AFB9012CA71FA06335D590C699D08AEF63FD849E6996F55D756216FDB7CD` |
| `IMVSPixelCountModuVAControl.dll` | `1B0E731C72DCC9D3A4E497F616B6BB6F76A0B73366AFA8DC303978BBB92EF0B1` |
| `IMVSPixelCountModuVACs.dll` | `E280E38FA881892ACF35DF4D2B0628882BBDD34439A3D8FD0BC2E59E05480345` |
| `IMVSPixelCountModuVACs.xml` | `0738BDF3DD9B767BEAB8A3FFCA7F1274B8C6D09FE43C9D39D106F19520C9781D` |
| `IMVSPixelCountModuVADisplay.xml` | `078A43AB72A1360BB483C7C031F04CC844387F230357005EA64DC733A091936D` |
| `ToolItemInfo.xml` | `8DB07F178759E3482AB74AA3550DAC7D2913573C9810D3F7A5A65D8ABB3A24F7` |

## 参数页和分组事实

`IMVSPixelCountModuVAAlgorithmTab.xml` 声明三页，顺序为 `Tab_Basic Params`、`Tab_Run Params`、`ResultShow`。

- 基本参数页先挂 `ImageSourceGroup`。
- `Tab_ROI Area` 内按 XML 顺序挂 `OutputMaskImageGroup`、`InputMaskImageGroup`、`RoiSelectGroup`、`ROISelecter(RoiType)`、`InheritWayGroup` 和 `AssistGroup`。
- `RoiType` 是 `ROISelecter`，可选形状为 `Box SectorRing Sector`，不是普通字段行。
- 运行参数页使用专用 `PixelModuRangeConfigList`，它不是普通 `NameValueItem`。
- 结果页包含数量判断、数量范围、比率判断、比率范围和图像显示分组。数量和比率范围由布尔触发器控制可见性。
- 底部同时声明 `BottomExcuteButtonsLeft` 和 `BottomExcuteButtons`，后者通过 `GroupLinkItem(LinkName="BottomCommandGroup")` 接入公共组。

## 专用控件事实

`IMVSPixelCountModuVAControl.dll` 是 .NET Framework 4.6.1 托管程序集，资源中包含 `IMVSPixelCountModuVAControl.g.resources`。静态类型清单包含：

- `Apps.XmlParser.ParamTab.PixelModuRangeConfigList`
- `Apps.XmlParser.ParamTab.PixelModuRangeConfigItem`
- `IMVSPixelCountModuVAControl.UI.RangeConfigListPanel`

`RangeConfigListPanel` 具有 `AddNewItem`、删除和列表刷新相关方法，并通过 WPF 组件连接器接入 XAML。其程序集引用 `Apps.XmlParser`、`VMControls.Interface`、`VM.Controls`、`VMControls.BaseInterface` 和 `Apps.UIHelper`。这证明运行参数范围列表属于原厂专用动态组装控件，不能用普通数值行替代。

## 参数和结果事实

`IMVSPixelCountModuVA.xml` 的输入组合包括 `InputImage`、`InputROI`、`InputROIAnnulus`、`Position Correction Info` 和 `InputMask`。输出组合包括 `ROI`、`ROIAnnulus`、`OutputMask` 和隐藏的输出位置修正组合。

`IMVSPixelCountModuVACs.xml` 明确存在 `PixelCountVAParam`、`PixelCountVARoiManager`、`ThresholdRangeItemParam`、`PixelCountVAResult` 和动态参数集合。数量范围边界为 `0..999999999`，比率范围边界为 `0..1`。

## PixelCount 样板接入约束

PixelCount 单项目可以复用 Blob 已验证的公共链，但必须保留两个原厂边界：

1. 普通输入、ROI、AssistGroup 和结果判断进入公共 `Tab.Collection → CollectionViewSource → ParamListView → ParamUserControl` 链。
2. `PixelModuRangeConfigList` 进入专用动态面板接缝，不能把动态范围列表压平成普通字段。

本证据足以支持页签、分组、ROI 选择器、底栏、数量/比率触发器和范围列表接缝的实现。范围列表的完整运行时增删时序和原厂人工视觉截图仍需单独验证。
