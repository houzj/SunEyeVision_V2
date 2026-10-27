# 根流程总览实施交接

- 基线提交：adc727ba
- 当前分支：codex/workflow-overview
- 当前执行者：Codex B（独立 worktree）
- 任务目标：R15/R16 根总览呈现链；正式状态、调度、位置历史与复制由集成宿主接线。
- 允许修改目录：仅总览新控件/呈现合同/专用测试/本交接；共享宿主和 Group 冻结。

## 实现设计（先于实施冻结）

恢复件 WorkflowOverview.cs 负责入口、根集合过滤和140×120卡片，WorkflowOverviewDrag.cs 负责身份/修订门禁、5px吸附、5000逻辑页和 .7 原卡影像；WorkflowInputBoundaries.cs 说明 Ctrl 在拖动入口锁定效果，释放时再核严格 Ctrl，WorkflowStyles.xaml 说明四态资源。新控件只消费不可变根条目和可信统计；无统计显示 --，不能刷新计数。SetEntries 对整批身份/坐标校验后替换，只在跨文档时重置视口；当前文档刷新保留视口、撤销正在进行的手势。

入口由宿主进入总览，喂 DocumentIdentity/revision；点击发 Open/Configure/Run，拖动提交发 Move/Copy（原身份与修订）；宿主重验并走原正式命令、历史、保存与整流程复制。控制层无模型与执行器引用。Unloaded、Escape、失去捕获及身份变化终止手势、清 ghost。总览视口独立保存，打开根流程不修改其画布视口。

## 资源与证据

原恢复件 D:/MyWork/REA逆向测试/src/VisionEditor/WorkflowOverview.cs、WorkflowOverviewDrag.cs、WorkflowInputBoundaries.cs、Styles/WorkflowStyles.xaml。总览专用矢量字典从 Styles/WorkflowDrawings.xaml 按三类四态 DrawingImage 的 StaticResource 依赖闭包原样抽取，新增本地主题橙色常量（独立适配）；展开PNG直接复制 analysis/ui-assets/wpf，同名hover/disable在恢复件也回退normal。配置复用当前宿主已有四态PNG。

- D:\MyWork\REA逆向测试\src\VisionEditor\Styles\WorkflowDrawings.xaml: SHA256 `0f7985bdf43a7277f2ec319c85011298892ff26d9d77008a2f196439ade6158d`

- D:\MyWork\REA逆向测试\analysis\ui-assets\wpf\ic_展开_normal.png: SHA256 `7d7a99560eb53a0ad7856ea16638652b3da1e63319ecedfb91d6c956590009f8`

- D:\MyWork\REA逆向测试\analysis\ui-assets\wpf\ic_展开_click.png: SHA256 `57ee2e94c5b65838bb42c458df7ac9327f3e3d3bd52affea95dc0b2065d89044`

## 已完成与交接合同

- RootWorkflowOverviewEntry 为不可变只读投影；RootId 稳定身份，X/Y成对或同时空，运行次数/耗时为空显示 --。SetEntries 先全批校验再替换，非法批次不破坏现有可视树。当前文档同修订统计刷新保留活动拖动令牌；修订、文档变化或源失去移动权立即取消。
- RootWorkflowOverviewControl.SetEntries(entries, documentIdentity, revision)；Viewport 为 RootOverviewViewport(Scale,PanX,PanY)，Pan 为缩放后 ScrollViewer 的非负 DIP 偏移；范围 Scale .1..4。中键平移、Ctrl滚轮指针中心缩放、双击标题或展开按钮打开。返回总览由宿主保留控件或保存 Viewport，不能复用根画布视口。
- OpenRequested/ConfigureRequested/RunRequested 为 RootOverviewRequestEventArgs(RootId,RunKind)，RunKind Single/ToggleContinuous；连续按钮在 IsContinuousRunning 时显示停止。宿主须再次验证当前可运行/编辑状态；UI 本身不调用执行器。
- MoveCommitted/CopyRequested 为 RootOverviewPositionEventArgs(RootId,X,Y,DocumentIdentity,Revision)。Drop 先退休令牌、清 ghost/捕获，再发唯一提交事件，允许宿主在回调同步重建全部条目。拖动请求使用当前控件私有对象令牌，拒绝其他控件、旧文档、旧修订、无编辑权或视口外；Ctrl 在进入 OLE 时锁定，Copy 释放要求精确 Ctrl，Move 不接受 Ctrl。
- CancelGesture 可由宿主在切根/切工作区前调用；Unloaded、Escape、修订/身份切换和 OLE finally 统一清理。中键失捕只终止平移；OLE QueryContinueDrag 根据私有源令牌取消。控件不持有正式模型、运行图像或 IDisposable 资源。
- 暗亮主题仅容器使用 ThemeWorkspaceBackgroundBrush/ThemeTextSecondaryBrush，140×120卡片客户端/标题及矢量原灰色仍按恢复件固定色；专用矢量字典无外部资源缺口。PNG复用现有自动 Resource glob，无需改csproj；新展开正常/点击两图，恢复件本身缺失悬停/禁用时回退正常。

## 验证

- XML解析两份XAML成功，50个矢量资源及全部Static/DynamicResource闭包无缺项。
- PowerShell已有Roslyn 5.0语法解析三份新增C#均0诊断；起初尝试显式载入SDK Roslyn版本冲突失败，随后明确使用进程已有版本完成，不隐去失败。
- 使用安装的.NET10/WPF引用程序集，仅内存CSharpCompilation（生成字段以局部stub模拟，未输出程序集）核产品两份C#语义，0错误；这是定位检查，不能替代net9正式XAML编译。
- 新RootWorkflowOverviewTests覆盖5px吸附/整卡界限、Ctrl锁定/释放规则，以及STA实际控件组装、原尺寸/缺值统计、非法批次原子拒绝、打开/配置/运行请求、禁用与空根。尚未运行；由主集成在正式Release同版产物运行。
- 未在子worktree构建/部署/复制程序或算法库。未进行VS设计时、物理鼠标、原厂动态或用户人工验收。Group未修改。

## 未完成与集成下一步

主集成负责总览入口及主界面组装、实际完成事件统计、开根/配置/运行门禁、DocumentIdentity/revision校验、正式坐标命令与历史、整流程复制及位置传入、保存重开和根画布/总览视口分离。先完整接线再统一构建和专用/原回归，暗亮实际WPF全窗检查与用户验收后如实记录。仅本呈现分支不能宣称R15/R16完整生产链已完成。

## 总览模式切换资源补充

集成沿 WorkflowTabs.cs:144 → SetWorkflowIcon 核查发现，ic_全局/ic_单个 是36×36原PNG，并非 WorkflowDrawings.xaml 矢量。每组仅normal/disable真实文件，hover/click按恢复件实际代码回退normal。本字典新增八个 `ic_全局normalDrawingImage` 等 ImageSource 类型 BitmapImage 键，统一供集成四态模板引用，键名 DrawingImage 仅沿已约定消费键，不表示资源实际类型是DrawingImage；消费者应按ImageSource使用。新增原PNG四张，未描摹伪造矢量。原文件身份如下：

- `D:\MyWork\REA逆向测试\analysis\ui-assets\wpf\ic_全局_normal.png` SHA256 `bdaf3231b6b7ff2814800d8506aecf528fd1e9e9f0d193e20b9f4713bb388e33`

- `D:\MyWork\REA逆向测试\analysis\ui-assets\wpf\ic_全局_disable.png` SHA256 `4aa836dde13ec5dfb09a094716eabe04191f760b6fc4f03c887199de8e819bc9`

- `D:\MyWork\REA逆向测试\analysis\ui-assets\wpf\ic_单个_normal.png` SHA256 `8649a72d069624159f1b722b04d2dd62fa2d245e50f655ce952cfd97a3870a63`

- `D:\MyWork\REA逆向测试\analysis\ui-assets\wpf\ic_单个_disable.png` SHA256 `cfb927894b9b0db7be7059562c2a4d7026a9414697c0e49d0a31357838915eaf`

静态XML解析成功，新增八个ImageSource键及四个pack资源路径存在；没有子worktree构建，实际WPF加载由主集成同版Release验。
