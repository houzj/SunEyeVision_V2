# 图像源生产源条会话整批候选交接（2026-10-28）

## 基线与写权

- 分支 `codex/image-source-session`，独立 worktree `workflow-timing-footer/SunEyeVision_V2`；从 root 集成点 `9dae388f`创建，并接源条菜单补丁 `45a81f5d`（本分支等价提交 `f9dc9bb2`）。
- 唯一新业务文件为 `src/UI/Services/Images/ImageSourceStripSession.cs`与`.FileImport.cs`。本批经root明确追加授权，仅对既有指定ImageSources目录内会话接口和控件补视口上报及ImportDirectory.Paths；未写87f4/主仓、其他UI/Workflow/主题/项目/Catalog/测试。
- 这是未编译、未部署候选。没有私有Release、DLL复制、算法库副本或运行锁。

## 冻结生产API

`SunEyeVision.UI.Services.Images.ImageSourceStripSession : IImageSourceStripSession, IImageSourceStripViewportSession, IDisposable`

构造为 `(ImageInputSource source, Func<bool> isCurrent, Func<ImageInfo,int,long,Task>? activate=null, Dispatcher? dispatcher=null, Func<Window?>? dialogOwner=null)`；`Source`只读公开。构造/正式操作在owner Dispatcher；跨线程请求与释放转owner线程，不引用MainWindow、工具草稿或视觉控件。

- `Activate`有callback时只转交image/index/expectedVersion，不预先Select；callback拥有正式选图与执行的完整接纳。没有callback时只SelectImage，不调度执行。
- `SelectStart`只SelectImage。模式/Auto走现有正式源属性；删除/清空走源接纳。
- 完整勾选集合校验稳定Id、重复及过期后，只调用root本批新增的`ImageInputSource.SetRunSelection(IEnumerable<string>)`一次，不foreach写IsForRun。该方法不在本分支改写，由root合并时供给。
- 版本/owner/只读前置核对；CancelImport与OpenDirectory按非编辑语义，不因只读被错误拒绝，但仍核会话版本。打开目录只使用对应stableId的实际路径，不临时读当前索引。
- 所有展示恢复、源事件、缩略图及视口变化均不发Activate，不触碰Runtime或历史。

## 导入完整链

1. ImportFiles/ImportDirectory都允许不可变Paths；null才显示系统对话框。显式目录路径、拖入目录复用既有顶层枚举，不实现递归订阅、Linux或RP。
2. 获取dialogOwner之后、modal返回之后、枚举之后、最终接纳前分别校验owner/source/version/取消/只读。选择器返回时来源变化会拒绝，不能落到新来源。
3. `ImageFileImport.DirectoryFilesAsync/ExistingFilesAsync`负责目录枚举和存在/去重；构造ImageInfo在后台完成，不解码全图、不逐条改live集合。
4. 唯一`AddImages(additions, selectFirstAdded:true)`做一次完整接纳。600项也是一个编辑历史条目，不按块接纳；既有文件不重复导入，缺失文件跳过。
5. 自有CTS/代际与外部token相连。新导入、CancelImport和Dispose退役旧代；取消前未接纳则零更改，已接纳后取消不回滚，旧finally不清除新导入状态。
6. IsImporting、Status为真实生产通知，可用于UI与回归观察。通知可能重入取消/切owner/新导入，每个通知后的继续路径核代际，不覆盖新状态。

生产方法保留`internal Task ImportFilePathsAsync(string[] paths)`、`internal Task ImportDirectoryAsync(string directory)`，唯一同名`private Task ImportAsync(Func<CancellationToken,Task<string[]>> enumerate)`进入真实核心`ImportCoreAsync`；不是测试专用钩子。测试可使用公开Request.Paths避免OS对话框，或反射真实枚举边界验证未完成枚举取消。

## 视口、资产和资源所有权

- 新最小非编辑能力接口为`IImageSourceStripViewportSession.UpdateVisibleItems(IReadOnlyList<string> itemIds)`，不改正式源Version、不写配置或历史。
- 原件Source条通过Loaded、容器生成状态、LayoutUpdated和ScrollChanged合并到Background优先级，上报实际已生成且与列表bounds相交的stable IDs，最多128；折叠、卸载和切Session上报空集合。重复同窗口不再通知。
- 生产session防御复制可见ID，只为该窗口保留独立LocalImageAssetCache waiter与冻结BitmapSource；窗口外取消并释放自身引用，源行事件订阅另行管理。既有PreviewBitmapCache的内容版本/并发/缓存预算直接复用。
- 不调用ImageInfo.CancelThumbnail/ReleaseFullImage、不invalidate全局资产缓存、不处置正式source或结果资源，避免一个视图关闭伤害另一个视图。Source/ImageInfo的既有Thumbnail变化会重新投影；跨线程通知调度到owner。
- 原控件过去每次刷新都ScrollIntoView会导致缩略图完成刷新后强拉回当前图。本批只在显示stableId变化时滚入，保留用户横向浏览位置。
- 会话Dispose退订source/所有行事件、取消导入和自有缩略图任务；旧完成回调核owner/成员身份/代际/取消，无资格更新新视图。

## 验证与未验

- 必要静态核：本批两份UI C#、源条/缩略图/会话接口共五份C#由已载入Roslyn 5.0 ParseText检查，syntax Error=0；git diff --check通过。
- 已按真实原入口核查，不以同名代码猜测业务；原基础批45+1/EXE结果属于6ae86393，不是本批成绩。
- **未完成验证**：语义/WPF构建、实际源条realized viewport、600项接纳与通知切源取消、真实工具与主界面调用、深浅主题/DPI、VS设计器、用户及硬件验收。由root合入完整候选后统一主仓正式Release验证；本路不构建私有Release。

## root集成事项

1. 本提交须与root的SetRunSelection一次Commit及工具/主界面消费者同批合入；不保旧ImagePreviewControl壳。
2. editor测试路已收到实际生产session签名、ImportAsync唯一签名、公开viewport接口和ImportDirectory.Paths行为；不加载控件时初始缩略图按设计为空，可明确上报非编辑可见窗口或加载真实Strip进行回归。
3. 明确Dispose归属：每个真实视觉owner持有并释放自身session；正式source与ConfigurationWriter的生命周期仍由root宿主/草稿接纳层管理。
4. 同一组件运行/调试与主界面均消费此真实session，Activate业务行为通过构造callback区分，不再复制第二套取图或配置逻辑。
