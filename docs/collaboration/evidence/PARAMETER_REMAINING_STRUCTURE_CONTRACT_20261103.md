# 剩余公共产品职责结构与原件证据收尾（2026-11-03）

## 已固定基线

上一参数实际闭包已在主仓e956a21fa正式实施自测闭口，正式产品cdbc03b0c、91场景组、570项载荷及完整文件集合不变。两既有候选分别固定e567/4e的独立11/80组回执，不能转签主仓。本批不再迁移新控件、不重做C05/C06/C07或ROI图文链，只收尾产品结构。

## 完整盘点及依据

当前Plugin.Wpf产品仍有91个默认编译cs、7份旧目录XAML、553个Input/VMControls.WPF资产及521个未实现pending。此前491是旧路径关键词子集，不能冒称全数。另有新中立目录下9张文件名仍带VM的图片（7个旧品牌图标、2个Input光标副本），本批一并处理。

主线已沿主仓e956及工作流87f4固定637cbf230的src/tools/scripts只读搜索：84个Input.VMControls.WPF/Release类、ExportMVDCode没有家族外当前消费者；7份XAML中FieldPrimitives.PopupButton实际引用旧VM.PcUI/controls/commomdictionary，必须保留并迁名。Generic仅合并Combo/FieldStyles；旧Input主题仅族内或StyleHelper加载，没有外部实例入口。原资产根及9个旧图片名无现行源引用；Display/ImageView正式鼠标资源及NativeResources.props不动。退出的是本阶段没有消费者的原件，不以改名扩迁原工作流或导出native依赖，也不保旧类型兼容入口。

ColorResourceBinding、Utility、IImageViewBoxWAddition和ExternalCall共6cs均有真实公共消费者，不能删除、合并或改算法；保持完整原行为与资源响应。本方工作流UI自有路径计算不是本批迁移对象，另一开发线独立继续。

## 实际依赖映射（唯一产品变更）

N=SunEyeVision.Plugin.Wpf.Native。

|原目录 / namespace|目标目录 / namespace|
|---|---|
|Controls/Native/Dependencies/VM.Utility；N.Dependencies.VM.Utility|Controls/Native/Dependencies/PlatformServices；N.Dependencies.PlatformServices|
|Controls/Native/Dependencies/VM.ExternalCall；N.Dependencies.HikExternalCall.Common|Controls/Native/Dependencies/Interop；N.Dependencies.Interop|
|Controls/Native/Dependencies/VMControls.Interface；N.Dependencies.VMControls.Interface|Controls/Native/Display/Contracts；N.Display.Contracts|
|Controls/Native/Display/AppsColorFun/Apps.ColorFun；N.Display.Apps.ColorFun|Controls/Native/Display/Theming；N.Display.Theming|
|Themes/Native/Input/VM.PcUI/controls/commomdictionary.xaml|Themes/Native/Input/FieldPrimitives/FieldCommon.xaml|

同步全部C# using/完整和相对别名、XAML xmlns、资源Source及实际消费者；类名/接口成员、Marshal.Copy/内存处理/委托、ColorResourceBinding逻辑、字典键/值与顺序不变。PlatformServices.DelegateCommand与Mvvm.Commands等已有不同实现不合并。目标碰撞先检查。

## 原件保全及产品退出（不是功能实现）

- 84个Input旧框架cs + ExportMVDCode.cs =85cs，及其6个未消费XAML：原字节迁到Catalog/SourceSnapshots/Unused/{原Plugin.Wpf相对路径}.txt。不得以.cs或.xaml后缀继续默认编译，不增加Compile/Page/Resource例外回退。ExportMVDCode的DLL/MarshalType ABI只留原始证据，不改名造一个新native产品入口。
- 553个旧Input资产及9个未消费原品牌/重复光标图：原字节迁到同一Unused证据区，退出Themes/Assets打包通配；保留原来源/字节，不把原标志当SunEyeVision商标。实际Display光标资源不动。
- 全部521个.pending原字节迁到Catalog/SourceSnapshots/Unimplemented/{原相对路径}，保持未实现。无显式Compile/Page/Resource或.pending消费；原有3条指向其未编译XAML预期路径的活动URI不能补造功能：UserTempConverter、两个Overlay转换器仍是未接原件路径，保持明确保护/记录，不加入运行时证据查找或资源回退。其余14预存URI列表保留具体原行及影响。
- 索引逐文件记录原位置、新位置、源基线、SHA256、分类、退出/未实现理由及替换条件；内容逐字节核对。仅删除已核空目录，不碰未知文件，不删除原件。
- 当前Catalog的localPath/localSha256/dependencies及sourceKind同步证据位置或新产品位置，明确哪些不参与产品编译。original*/assembly/MVID及整块历史migration/receipt/report保持不变，不刷新历史SHA假装新状态。原28目录/17组/B08 24项保留。

## 两线写入及自测

仍用两个既有候选树，不新增Worktree；从本合同固定提交各新分支：
- 公共线唯一Plugin.Wpf源/资源/证据快照及当前Catalog、本批专用scripts/analysis迁移/检查；
- 消费线唯一UI/tools和现有scripts/verification的实际消费定位，不改公共源；没有消费就明确无改动，不造空提交。
- 主线唯一合同、原方案/账本、集中集成与正式Release。

整批碰撞/字节/索引检查通过；产品默认Compile/Page/Resource中证据快照为0；实际6cs+1字典完整映射，产品有效代码/路径不剩原身份，原注释及Catalog证据不误改；证明85cs/6XAML/562图片退出打包而未丢原件。源码规范反映射仅组织差异；不为缺资源扩控件功能。

固定完整产品批后互接，双方真实标准UI/full候选构建。公共C07 10+限定Aero2/箭头1，消费者B08混合12+完整ROI预览24+图文All44；主仓同版91及实际加载/载荷/证据不部署核验，失败保留且按具体全链修复。候选不复制主仓Release/厂商库冒充构建。VS与统一人工另记。当前尚未实施本批，不以此提前宣称S2/S3完成。

本批后不再以迁移未用原件作Blob前置：同版核收已支持Blob整链与明确保护依赖后，进入S3按原完整证据实施HALCON算法；不能把保护占位称功能已实现。
