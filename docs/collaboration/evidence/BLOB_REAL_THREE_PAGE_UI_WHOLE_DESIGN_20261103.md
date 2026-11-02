# Blob真实三页原UI状态与受保护预览整批实施（2026-11-03）

基线f5a2266b0，完整导出正式八轨67777已自然exit0（23/33/1/44/7/4/12/24共148），正式回执C17_NATIVE_EXPORT_FORMAL_20261103.json。源代码不因本设计改变。当前回到更早的S1真实UI缺口优先修正，不启动新算法或公共平台。

## 原范围、已证缺口及复用

原完整范围仍为三页96个直接结构项（基本8、运行46、结果42）及8共享组，不将它解释为96个控件或通过率。原XML/default/trigger逐项见BLOB_WHOLE_TOOL_THREE_PAGE_MATRIX_CANDIDATE_20261031.json与BLOB_WHOLE_TOOL_VM_20261031/原厂整工具证据链.md；前者originalTabSha256=34A0CC5423A3D66B92DC272E0F50B013310E5180E801C648005383C3ADDDFDEC、originalGroupSha256=9129B82A0F9906DDE591347498A80E2E8C019C0FB959A721ECFD24FBBB5207DC。按原声明和现有源资源设计；原机所有状态未由静态XML代证。

真实缺口：BlobRunUi五个模式禁选、Auto/两软模式没有正式key；NativeThresholdUiDraftControls只有独立样例、未接真实Blob。高级临时组、NativeJudgmentUiDraftControls原15组及四个缺值显示对象整体禁用。六个字样、控件类存在、独立样例不能称正式三页全部UI完成。

正确成果复用：原控件/主题/图标、正式字段数值/范围/引用、检测继承与本地多形ROI、位置三方式UI、已支持结果图文和文本、真实底栏单次/确认及保护。禁止重迁底栏/窗口、复制业务模型、用旧SDK可视回退或重写正确执行/引用/历史合同。Blob.Wpf只组装接字段，公共UI组合归Plugin.Wpf。

## 一个完整纯UI批

|整组|真实窗口行为|执行与数据边界|
|---|---|---|
|六阈值模式|无二值化：极性；Single：极性+低阈值；Double：低/高而无极性；Auto：极性；FixedSoft：极性+软范围+Softness；RelativeSoft：极性+Softness+四比例。生产文本/顺序/默认/范围依原XML逐项核，不直接沿独立样例硬编码值。|显示选择与正式SegmentationMode分离。Single继续复用正式字段和私有草稿；五未支持模式只预览，不新增算法枚举/端口/保存字段，不用FormalKey=null假选择成功。|
|高级筛选、排序、4/8|全部原开关/范围/使能显隐、原排序13项及方向；已有支持项继续真实字段，未支持项UI-local。|明确映射与未映射项；未支持active预览/未恢复变化阻断接纳。不得保存no-op设置。|
|原15组判定|零目标、普通/Advanced展开、15组使能与范围、非法原文及恢复。逐端核默认/范围/精度，数量下端最小1而上端最小0，不能用同一minimum。|不是本方12条旧判定的别名，不提前改算法/判定模型。UI-local变化不能落正式配置或CurrentValues。|
|基本页完整交叉|原图形/图像区域、检测绘制/四继承、屏蔽绘制/继承、输出mask、位置三方式及外层显隐，保用户屏蔽多形差异。|复用已真实接通的组；缺mask/pose producer不造假候选或像素，现保护不丢。|
|结果图文与四缺值对象|已有正式对象、文本继续实际场景；缺值四项的眼/笔/样式允许仅UI预览，不画假对象。|复用已有HasUiChanges及来源生命周期；预览未恢复不得确认/执行，无正式无效字段。|
|页签、Advanced与底栏|跨页pending、分支留值、回Single恢复、当前预览状态观测、取消关闭与Owner退役同批核。|不以HALCON功能作为UI预览前置，未支持预览不允许执行/提交。连续/Reset仍保护，不引入WPF轮询。|

## 状态、身份、回调与真实出口

只用窗口生命周期内的不可序列化UI预览作用域（可复用现有HasUiChanges/Changed/ValidateUiState并集中协调，不先造通用框架）。它保存未支持分支选择、原文/pending、变化及活动状态，不成为第二套工具配置，不写Runtime模型、正式/私有已支持字段、CurrentValues、Dirty、Undo或文件。

正式字段与预览字段不来回复制。进入未支持模式保留既有Single正式草稿/引用/当前值及pending，切回恢复原对象/状态，不能用默认覆盖。未支持预览选中即明确拒绝实际运行/确认；其他未恢复UI-only变化亦拒绝。恢复支持选择及未支持组原状态后才可走现有接纳。只读CaptureAcceptedFields类快照不应把UI-only状态装入Fields；真正TryCapture/执行/确认不得绕过集中门禁。方案主菜单保存既有正式配置的合同不扩改，不因本窗口预览阻断工作流另线；UI-only状态永不被持久化。

切模式/页签/隐藏/卸载要退役原来源树、Popup和延迟回调。先找现有控件的关闭/失效接口并复用；确缺时只补此真实组装所需的UI生命周期接口，不改SDK执行/引用身份合同。隐藏分支可留原文，但旧叶回调不可写当前新字段；重新显示从现有作用域投影。样式确认/取消、旧Owner关闭都同理。

不要添加常驻解释性文字/新流程提示，也不造假数据。必要的拒绝沿现有错误/不可用通道表达，视觉继续原控件、模板、图标、间距、颜色。S1不声称绑定/保存/算法或原机像素同态。

## 唯一写入边界与设计时

复用现有D:/MyWork/line-aiproject/blob-display-neutral-20261103，一个独立分支codex/blob-three-page-ui-preview-20261103，不新增worktree。唯一写者display_neutral_implementation；主线负责合同、Catalog/O06和正式集成；另线只读审查。写者先核完整设计/96项原XML及真实调用链再统一实施，不能只去掉IsEnabled=false。

允许BlobRunUi、BlobDisplayUi、BlobRoiUi、BlobParameterEditor.xaml.cs及必要同目录窗口内预览协调；公共NativeThresholdUiDraftControls、NativeJudgmentUiDraftControls、NativeUiDraftRange/条件/显示组合及确实需要的现有字段控件UI关闭接口；ROI/Assist仅在确证外层显隐或退役缺陷时调。专属三页UI验证/runner及实际设计消费者同步。不改Runtime算法模型、Workflow、输入/发布/保存合同、旧参数迁移或平台架构，不和工作流对话争写。

同一真实视图的运行时及设计时走同一组装链；找清当前设计入口，不能另放独立样例/复制XAML当Blob接通，不复制业务默认。独立渲染只核资源/构造，实际VS用户手动验；不把此前Single设计器截图代签新增分支。

## 完整验收出口

1. 同一真实Blob窗口逐六模式核实际可见/隐藏原字段、标签、默认/范围、软分支/四比例；另核所有高级与15判定及四样式预览。不是只测类/标签或API TrySelectMode返回true。
2. 每端原数据边界/精度、非法原文/跨页/隐藏/重显留值、旧来源/Popup回调拒绝，用户多形屏蔽保留；全部三页原矩阵逐项标支持/预览/缺基础，保全范围。
3. 预览前后正式及私有已支持Fields/Selection/CurrentValues、历史位置和方案字节不变；五模式及未支持active/变化均经真实执行/确认出口拒绝，UI-only不进入保存payload。
4. Single与原支持项仍完成真实引用、私有调试、确认取消、Undo/Redo及保存重开；跨Owner关闭不泄漏，设计消费者同组装。
5. 候选真实restore/UI+全量Release、专属三页UI整批及相称现有B08/运行窗回归。只新变更/失败/未解风险才补测，不以重复流水数量代UI完成。固定实际源码/载荷/额外来源SHA和支持/保护范围；主线正式同版另验，人工另记。

本批S1补齐后重新核S2剩余控件实际链；算法/producer缺基础保护不能假称功能完成，但也不无限扩基础。S3原算法完整对标仍按原目标接续，不能把此UI批当S2/S3全部完成。
实施澄清：未支持组原默认眼开/默认true不应让未交互的新Single窗口失效；按原基线、主动未支持选择与未恢复UI-only变更判门禁。为极性/低阈值等原共享字段从现正式编辑器读不可变setting或显示/pending作为必要初始UI投影是允许的，不是第二套可保存模型；预览变更绝不回灌正式草稿，回Single恢复原正式对象/引用/raw，不能硬回独立样例默认。
