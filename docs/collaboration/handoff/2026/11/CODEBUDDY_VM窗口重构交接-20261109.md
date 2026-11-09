# CodeBuddy VM 工具窗口重构交接

日期：2026-11-09

## 交接结论

当前主线只保留 ImageSource 与 BlobAnalysis 两个正式工具。旧工具目录仍作为逆向输入保留，旧 Plugin.SDK 已从现行解决方案、现行工具和发布目录退役。

本对话已完成一轮 VM 静态证据对齐、启动壳、工具准入、参数树、Tab 复用、窗口关闭释放和 Platform SDK 主链落地。当前最后一轮改动引入了 WPF 模板资源回归，必须先修复并重新构建 Release，再继续人工验收。

## 已确认并可复用的 VM 证据

- 启动顺序静态证据为快速启动阶段、方案管理器、用户打开方案、工作区加载遮罩、完整工作区。
- 参数窗口静态链为 `AlgorithmTabRoot → ParamsConfigControlBase → ParamsTabUserControl → ParamListView/ParamUserControl → DataTemplate`。
- Platform SDK 和 MVD Algorithm SDK 是两条不同产品线。外部应用 SDK 与工具作者 SDK 不能合并成一个包。
- Blob 参数 XML 的 `visibleWhen`、高级分组、模板字段身份和资源键是当前参数窗口的唯一数据来源。

证据目录主要文件如下：

- `docs/collaboration/evidence/VM启动阶段顺序静态对照-20261109.txt`
- `docs/collaboration/evidence/VM-CodeBuddy对齐审计-20261109.html`
- `docs/collaboration/evidence/VM原厂SDK发布边界静态取证-20261109.txt`
- `docs/07-工具逆向/decompiled/XmlUi/Apps.XmlUI/ParamListView.xaml`
- `docs/07-工具逆向/decompiled/XmlUi/Apps.XmlUI/ParamListView.xaml.cs`
- `docs/07-工具逆向/decompiled/XmlUi/resourcedictionaries/globalresources.xaml`

## 已落地代码

- `src/UI/Shell/Controls/Shell/DeferredWorkspaceHostControl.cs`
- `src/UI/Shell/Controls/Shell/DeferredImageResultHostControl.cs`
- `src/UI/Shell/Controls/Toolbox/DeferredToolboxHostControl.cs`
- `src/Plugin.Runtime/ReleasePayloadAuthority.cs`
- `src/Plugin.Wpf/Fields/DeferredDeclaredFieldVisual.cs`
- `src/Plugin.Wpf/Fields/DeclaredFieldTemplateRegistry.cs`
- `src/Plugin.Wpf/Presenters/Regions/NativeAssistUiDraftControls.cs`
- `src/Plugin.Wpf/Presenters/Display/NativeGraphicStylePresenter.cs`
- `src/Plugin.Wpf/Themes/Native/Composition/Display/GraphicStyle.xaml`
- `src/Plugin.Wpf/Fields/DeclaredFieldsView.cs`
- `src/Platform.Sdk.Contracts/`
- `src/Platform.Sdk/`

## 已通过的最近基线

最近一次已完成封签且通过生产探针的版本，载荷 SHA-256 为：

`B8CF66F0EF786D57AB5D403D4503C5935CB242CEE3297FC86042AFDDF701A202`

对应证据：

`docs/collaboration/evidence/RuntimeEditorProductionProbe-20261109-final-result-prewarm.txt`

该版本的关键结果为：

- Blob ROI 组合约 `356ms`
- Blob 首次第 2/3 页约 `271/257ms`
- Blob 重复切换约 `0/17/29ms`
- ImageSource 与 BlobAnalysis 的 Session、参数捕获、回执和关闭释放通过
- Plugin.SDK 退役门禁通过

## 当前回归点

最近新增的 `DeclaredFieldsView` 改动把 `Advanced` 和 `VisibleWhen` 字段统一改成延迟编辑器物化，目标是消除运行参数页约 2.1 秒的普通字段批量构造。

用户人工启动后出现 WPF 线程异常，日志为：

`无法找到名为“TextLeft”的资源。资源名称区分大小写。`

日志文件：

`D:\SunEyeVisionData\Logs\suneye.log2026-11-09.log`

最近已将 `GraphicStyle.xaml` 中的：

`Template="{StaticResource TextLeft}"`

改为：

`Template="{DynamicResource TextLeft}"`

但这次修改后的全量 Release 构建被用户中断，尚未得到构建通过和人工验证结果。不得把载荷 SHA-256 `0EB2902A77706C82628FF528C5573D42DA19AA1541549742A05E2FF629544FB2` 作为已验收版本。

## CodeBuddy 接手顺序

1. 先检查 `GraphicStyle.xaml` 动态资源修复是否能消除 `TextLeft` 异常。构建命令使用 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -v:q`。
2. 如果仍异常，回退“条件字段延迟物化”这一刀，先恢复最近通过的 `B8CF...` 行为，再用模板宿主资源边界重新设计，不能继续叠加临时资源补丁。
3. 修复后只重新构建 Release 和封签，先交用户手动验证启动、Blob 打开、运行参数页、Tab 切换、切换条件字段、关闭节点。
4. 人工确认稳定后，再继续运行页整页优化。优先改进 `NativeRangePresenter` 等条件成对字段的延迟组合，保持字段状态、引用提交和 Dispose 合同。
5. 每轮必须更新 `docs/collaboration/CURRENT_TASK.md`、`docs/09-现行文档/框架架构总览.html` 和本交接文件，不用新的零散探针替代完整链路验收。

## 当前限制

- VM 同版本动态 UI 线程耗时和真实实例复用边界仍未取得，性能判断只能依据静态模板证据和本方测量。
- Group、真实设备控制、WPF 渲染控件公开合同和 Visual Studio 外部附加验收仍未完成。
- 当前工作区包含大量 CodeBuddy 既有改动和未提交文件，不能按单文件状态推断所有改动归属。交接时以当前工作区和本文件记录为准。
