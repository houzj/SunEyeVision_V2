# 原件结果树和结果工作区组合交接

- 基线提交：988a34f5。
- 当前分支：codex/image-result-native。
- 当前执行者：source_native；独立 workflow-timing-footer worktree 唯一写者。
- 任务目标：按原 ModuleResultView 完整视觉入口迁入结果树、ImageUnion 与底部组合；生产 context、执行、资源所有权由 root 接线。
- 允许修改目录：Plugin.Wpf 的 Results/PropertyList、Results/ResultShell 专用 Controls/Themes/Assets/Design 子目录及本交接单；未写主 worktree、共享 C13/C14、项目、Catalog、ROI、C17。

## 原件与递归闭包

原件为 REA analysis/decompiled/VMControls.WPF/VMControls.WPF/ModuleResultView.xaml 的 imageUnion、resultGrid、localImageGrid 和 logUnion；模板来自 moduleviewstyle.xaml 及 Apps.Style 按 StaticResource 递归跟随。PropertyList 迁入原三列表头、40 高树行、200 名称列、15 层缩进、外 RecScrollViewer、ErrorInfo、变量链接及解绑图标、路径与复制。ResultShell 迁入原 ImageListComboBox、单双图、CrossLine、1:1、全屏、左右导航、原 ZoomIn/Out 图片模板、FrequentTab、ModuleViewTabControl、DisplayHide、滚动模板；26 个键控资源递归闭包。原图字节直接复制，来源路径、源文件和图标 SHA256 在两个专用 Assets/provenance.json 中保留；深浅色来自 colorstyle1/2，公共缺省来自 colorstyle.xaml，未用任意近似颜色填补。

原 selector 中 VM 全局对象、语言扩展及业务命令改为标准 WPF 绑定；原 resultTree 的厂商转换器改为宿主明确视觉投影字段。原 TreeViewItem.HasItems 决定展开按钮改为 IsGroup，因此异步尚未加载的组仍能展开，不伪造业务子项。原图像控件直接复用现 ImageViewBox，无复制图像、ROI 编辑会话或插件 SDK 视觉回退。

## 冻结生产接缝

`PropertyListView` namespace `SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList`：

- ItemsSource IEnumerable，SelectedItem object 双向，ErrorInfo、SelectedItemPath string，IsReadOnly bool（默认 true）。
- 行绑定 DisplayName、DisplayValue、Level、Children、IsGroup、IsExpanded（双向）、CanBindVariable、BindingDisplayName（无绑定时 null）、BindingUnavailableReason。Level 仅布局，不能当业务身份。
- SelectedCommand、BindVariableCommand、UnbindVariableCommand 参数均实际行对象；CopyPathCommand 参数是 SelectedItemPath 字符串。
- SelectedCommand 仅左键/导航键引起的实际选中执行；程序恢复选择、容器延迟生成恢复不执行业务命令。外部 SelectedItem 按引用恢复已生成容器，未生成容器 Loaded 后恢复。SelectedItemChanged 事件参数仍是实际行对象，不代表执行意图。
- 变量命令不存在、只读或 CanBindVariable=false 时禁用；路径复制无命令禁用。控件不操作 Clipboard/变量服务/context。

`ResultWorkspaceShell` namespace `SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell`：

- InputItems/OutputItems IEnumerable；SelectedInputItem/SelectedOutputItem object 双向；InputImage/OutputImage IImageData（现公共库 RenderInterface）。输入输出独立，不镜像。
- selector 项绑定 DisplayName、IsCheck、CanOverlay、IsBackgroundImage。IsCheck 单向；SelectOverlayCommand/SelectBackgroundCommand 参数实际项，宿主接纳后刷新，控件不直接写正式选择集合。
- IsDoubleImage、IsInfoExpanded、SelectedResultTabIndex 双向；双图开关控制 0/1* 输入列。展开底栏 235、收起 40，可拖分隔条。
- SourceStripContent、PropertyListContent、CurrentResultContent 为 object 组合槽；源条外层 MaxHeight110，内部复用已有 125/40/-40 原条，不重复造条。
- SelectOverlayCommand/SelectBackgroundCommand/PreviousImageCommand/NextImageCommand/CrossLinesCommand/FullscreenCommand 均 ICommand。导航和 CrossLines 无参数；Fullscreen 参数为实际 ImageViewBox，同一 view 的全屏移动/恢复 finally 由宿主实现。不存在命令时禁用。
- 本地缩放只调用原 ImageViewBox.Enlarge/Shrink/Init，保原 ZoomOut=Enlarge、ZoomIn=Shrink 语义；双图隐藏放大缩小。控件不调用工作流/ServiceLocator，不接管 reader/frame/image 租约。

## 主题与独立设计

两控件 Resources 默认合各自 Dark.xaml 和 Styles。实例主题切换应在该控件 Resources.MergedDictionaries 后追加对应 Light.xaml/Dark.xaml（后字典优先）；App 级资源不能覆盖本地兜底。键前缀分别 Results.PropertyList 与 Results.Shell。

独立真实视图：Design/Results/PropertyList/PropertyListDesignView.xaml、Design/Results/ResultShell/ResultWorkspaceDesignView.xaml。前者使用视觉示例行覆盖三列层级、只读、错误；后者使用真实双 ImageViewBox/selector/PropertyListView/空 ImageSourceStrip/底部组合，Gray8 样例仅视觉像素，不复制 Runtime 默认或业务结果模型。未创建另一套预览 UI。

## 未做及禁用占位

- 历史缺正式存储、查询、删除与导出服务：历史结果 Tab 明确禁用，说明依赖；未用最新快照假装历史。
- HelpContent 缺失显示“未提供工具帮助”；未桥接旧模块 XML/帮助全局服务。
- 物理坐标、点云和缺 frame 变换的叠图无本批基础，未搬旧全局业务；selector 原叠图视觉保留但宿主不提供命令时禁用。
- 当前结果表放 CurrentResultContent，由主线 C14 StandardTable/10列/增量等既有链承担，本批不重写。
- root 必须接新正式 context 的 owner/publication/epoch、reader 层级与资源帧适配、真正全屏及源导航执行；模板迁入不等于生产闭环。

## 验证记录与下一步

已执行：8 个生产 XAML XML 解析、2 个独立设计 XAML XML 解析；作用域 StaticResource/DynamicResource 和 component URI 闭包核对零缺失；4 个 C# Roslyn 5 纯语法零错误。未语义编译、未正式 Release 构建、未 VS 实际设计器、未运行、未人工验收。未执行私有 Release、未锁 DLL。

下一步由 root 同批整合真实消费者/主题适配，主线唯一正式 Release 集成构建；核三列表头及深层展开、只读/变量禁用、错误/空值、单双图独立 selector、缩放复位、全屏同 view 恢复、源条展开折叠、导航执行与取消、底栏折叠/拖拽、深浅主题、VS 设计及用户人工验收。不得把本源码候选记为上述全部通过。
