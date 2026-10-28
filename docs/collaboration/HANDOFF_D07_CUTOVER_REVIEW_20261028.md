# D07 集中切换审核记录（未应用、尚不具备批准条件）

## 本轮修订（2026-10-28，基线4f8efb06；仍未应用）

- 已只读核对主线09f9739a归档的实际候选构建证据：UI零错6547警告158.62秒、全量零错6584警告70.55秒；ImageSources attempt008=180/0，正式探针PID28596 exit0，共享publication10/0、真实表500→1000→1205一组通过。该产物包含另有ROI未提交候选，逐文件SHA以主线 `evidence/O06_RESULT_CHAIN_UNIFIED_20261028.json` 为准，不称干净09提交产物。Global仍上一整轨283/1，4f的转换器修复未正式验证。
- 本轮移除未连接Presenter里固定橙色枚举所有Region的猜测绘制；明确提示正式图形/文字声明尚未接通，不把资源存在当可见性和样式声明。有同帧证明的选择高亮保留。原完整图形/文字迁移清单保留，场景总预算属于恢复该生产链前的必要合同，不因撤候选而记完成。
- 复审修正独立selection代号和每侧pane代号，显式null清空不再受已退休publication阻挡但仍核代号，逐次清理/Present前后核同pub内新选择；异常退出后合并执行已收到的刷新意图。图像来源额外拒重复scope/node及退休上游admission。均只是未连接源码，尚需真实重入测试和正式编译。
- 审核包现在19个拟议文件、13个完整依赖；测试现固定d592454e十三项候选（原八项、两项拒旧恢复、三项最新真实DP重入），仅以完整源码附录保留，不直接cherry-pick进当前编译。原区域“存在即橙色”错误断言已撤，改无声明不绘制及真实同帧选择，并补三项实际DP重入/失效pub清空/异常后新run刷新，不把旧猜测固化成正确断言。
- 当前patch SHA：`ef21267913a6c8c5a63da03a69a905fa524676f82aae879a5d8ca4c62535a176`。17个拟议C#语法零错、两XAML XML可解析，不替代编译/设计器/运行。approvalReady=false，仍不应用被拒生产切换。

当前用户目标 A01–E07 保持完整。此记录区分正式回归、未连接源码候选和只读切换提案；不把源码同步、语法检查或设计制品记作生产交付。

## 已固定的产品失败修复

- 主线正式 dfb7b053 已报告 UI/full 零错及 EXE 探针 exit0；GlobalBinding attempt005 为284项283过1败，ImageSources attempt007 为180项179过1败，原日志保留。
- 剩余 Global 实际失败发生于发送编辑窗口保存重开：纯字符串 DESIGN 被 GlobalVariableValueConverter 强包成单元素数组。4f8efb06 改为纯值按 JSON 实际标量/一维数组形状对称保存，数值仍规范 long/double；未改变 SDK 词汇、图像或 ROI 合同。旧 SDK “标量必变数组”断言同步新正式合同，原发送 DESIGN/CURRENT 断言保留。
- 剩余 ImageSources 失败是大结果分页分配门限。SourceMatches 仍每次复核方案、流程、当前节点实例和 owner，改用索引循环，去除每页每元素的捕获 Count/Any 委托。7d130460 保原200万元素一维数组及1MiB门限，补分段分配诊断和21项 Repository/真实生产通信回归。4f8efb06 已交主线集中构建，尚未收到本轮运行结果。

## 边界与审核制品

公共 Shell 已由主线唯一写者 f93fe2b8 固定；独立树通过619d63b7合入。InputViewport/OutputViewport只读暴露实际原ImageViewBox，旧四个图像DP及两条图像绑定已删除。该公共合同不是本分支越权改写。

自动审批先拒绝跨公共所有权的一次性改写，公共合同同步后又以“多个核心生产UI/XAML/结果管理器大范围改写且未编译验证”为由拒绝自有消费者切换。两个被拒脚本均未执行，不拆命令绕过。当前只生成 `.patch` 与 `.review.json`；JSON明确 applied=false、approvalReady=false，含基线、每文件原始/拟议SHA和完整源码。未向用户请求批准尚不闭合的制品。

- `patches/D07_RESULT_WORKSPACE_CUTOVER_20261028.patch`：19个拟议消费者文件，含新3个尚未连接的候选、原NodeEditorRouting及S4FullChain验收消费者。
- 同名 `.review.json`：另含13个完整源码/测试依赖，不能隐式依赖主仓尚不存在的pane、decoder、fullscreen或ROI基础文件。
- 上一修订补丁SHA为071f4f4c2b0900c767396ad64d7f55bbb5ce2ff23f0e502dd0b808afc3f82552；修订后以JSON和重新计算SHA为准。17个拟议C#语法0错、2个XML解析过，不是语义编译、WPF外观、VS或人工验收。

## 必须保留的评审清单

原件命令与选择态补核（已按完整源码纠正）：两侧选择集合与选中项独立，但原背景按钮是输出叠层背景交换，须已有背景、目标已勾选、非当前背景，旧背景转叠层；不是普通来源选择。此前由共用模板推导两侧独立背景管理的判断撤回，输入侧富按钮运行效果仍未证。本候选撤错误SelectBackgroundCommand别名，IsBackgroundImage明确false，普通下拉选图保留；受管多图层/至多3叠图/背景交换/透明度等整链未具备，入口禁用，不计迁移完成。左右导航是本层全部源瞬态命令后一次Run，未证native边界不擅用正式配置提交；辅助线必须原CrossConfig双模型，不造toggle。详见HANDOFF_D07_NAVIGATION_CROSSLINE_SOURCE_EVIDENCE_20261028.md及7个托管文件SHA证据。

|链|现有实现/证据|拟议调整或真实依赖|当前状态|
|---|---|---|---|
|唯一原Shell与消费闭包|原Shell两view、原PropertyList/源条/当前结果槽|迁移ImageResultControl/Main/NodeResultManager及所有脚本和设计消费者；不留隐藏SDK主图或Bitmap回退|提案未应用|
|同一结果publication|当前Main拥有树表共享pub|选中节点直接借当前pub；仅真正上游另建pub，plan只Dispose自己拥有的上游pub；删除重复结果刷新|候选已修，未运行|
|来源完整性|正式WorkflowExecutionDependencyGraph含画布与参数订阅|复用GetPredecessors；根端口/成员/明确序列索引以正式类型投影，Record内序列因现有引用语义不足明确禁用|候选已修，未运行|
|切源/重入|每侧pane独立receipt与borrow|入口固定epoch/plan，每个外调后复核；外部导航索引更新不得被旧key覆盖；旧selection先核pub再清自己的层|已修部分，需故障回归继续核|
|全部释放|原pane与全屏各有聚合清理|Presenter构造失败/Dispose、ImageResultControl源session和Shutdown逐项聚合，所有权先摘除再外调，不能异常后跳过pane/plan释放|已修提案，未运行|
|ROI正式生产链|原session与attachment完备；现SDK DeclaredControl的SetRegionInputProvider已有基础和5项候选测试|实际tools没有IConfigurationFieldProvider实现；RuntimeNodeConfigurationControl使用IToolSettingsView，现Options没有图像lease/viewport注入。不能以接口/单测存在称真实工具接通|未贯通，完整清单保留|
|ROI历史/手势|RoiViewportEditScope私有草稿接纳存在|现session私有Undo/Redo明确拒绝，缺稳定正式Runtime ROI接纳基础；不能造第二历史栈。全屏前需由实际编辑scope取消手势并恢复焦点|未完成，不冒称原菜单完整|
|正式结果显示|BlobDisplayDefinition确有Graphics/Texts、可见性和OKNG样式；IToolResultPresentationProvider当前只公开表格ResultProjection|旧DisplayCollector没有Frame/Coordinates，不能贴当前背景身份。此前只读候选的固定橙色Values.Regions已撤除，改明确不可用诊断，仍不能替代正式显示定义。Blob业务/未授权显示插件合同扩展仍冻结，不按类名反射专有元数据|未完成，依赖需明确|
|整场景预算|公共RegionLayer逐项限额/真实RLE可用|宿主还需层数、总BGRA字节和总runs预算，完整准备后发布，任何失败清本结果层并保背景/树表，不截断1205项制造半场景|未完成|
|历史来源|ShowDeclaredRun暂无正式历史UI/存储调用者|候选历史态禁止拼接当前WorkflowContext上游；缺整次运行冻结来源上下文继续明确占位，不造历史数据库|禁止混用候选已修；历史功能未完成|
|真实验收|主线唯一正式Release|完整依赖与消费者闭合后再请求具体审批、应用、规定UI/full、生产回归、深浅/宽窄/DPI、真实VS与人工验收，分项记录|待执行|

## 真实缺口的范围约束

不修改或扩展Group/循环语义、Blob业务和未获授权的显示插件合同。缺基础的入口应明确不可误用并列依赖，原完整清单不删除、不计完成。当前ROI和正式图形/文字接缝不能靠猜测字段、反射专有工具、把任意背景贴Frame、固定默认样式或重建旧SDK视觉补齐。公共原件能力与宿主生产消费者分开核收。

下一批仍按完整功能链集中完善制品和对应真实测试；不因自动审批拒绝把未应用提案当已迁移，也不以新定向测试数量代替整批完成。

本轮候选测试已固定35259200，共8项，仅审查包依赖：实际原ShapeList依赖属性通知内B重入，核B两侧列表及shape实例未被旧A清写；失效publication仍可显式清选择；实际GetOutputContract受控故障前发布新run，最终必须显示新Frame。未将测试提前加入当前编译。内存叠加19拟议源码后，旧GetMainImageControl/GetImageDisplayControl/SetResultRenderLayer/SetImageDisplayControl/MainImageControl在src/UI、验收脚本及UI测试无剩余精确引用；此检索不替代语义编译及完整交互验收。

本轮原39项验收矩阵已逐项按attempt008与O06真实范围核定，原需求单元格不变；历史5780/失败证据保留，内部ShowDeclaredRun不再写成已接真实历史UI。普通下拉选图仍是可实施链，受管图像叠层/背景交换则缺公共owner能力，区分记录。17拟议C#语法及两XML仍需本次重核，未交付。

旧缩放状态整链核对：src/tools/tests/scripts精确引用仅见Main/ImageResult的5个命令自声明/自初始化及ImageScale旧SDK使用；实际TopBar.F11经FullscreenRequested→ImageResultHost.ToggleFullscreen，Shell按钮直接原Enlarge/Shrink/Init。提案删除无消费者的5旧命令及两VM的独立ImageScale，保公共原ImageViewBox自身倍率与流程画布Scale，不新增委托兼容桥或第二缩放源。只改review拟议源码，现生产文件未变；待语义构建/实际缩放全屏验证。

旧来源回写整链修正：输入/输出DP在清图、取消预览和写导航索引前核当前plan的对象引用成员资格（输入另核IsUpstream），拒旧plan项及未知对象；恢复上次接纳的真实项时抑制自身DP回调，保新图像receipt/缩放不重绑。恢复观察者同步选择新的合法项则只执行新意图，代次/plan变化退出；不因稳定Key相同授权旧对象。换plan/Dispose释放所记项引用。仍为未连接候选，需真实旧项回写测试后正式验证。


## 选择重入集中修订检查点

旧来源对象按当前plan对象身份拒绝，恢复后显式null清空重放；刷新期间接纳真实选择但推迟呈现，退出刷新后读取最终DP；输入回调改变输出时以输出意图代号阻止旧默认覆盖；呈现异常独立记录，刷新调度置于finally。审核包已与实际候选同步，patch SHA256 ef21267913a6c8c5a63da03a69a905fa524676f82aae879a5d8ca4c62535a176。19拟议文件、13依赖哈希、C#语法及两XAML解析通过；不是语义编译或运行通过。仍需对应新增真实DP回归与正式整链验证，approvalReady=false，不直接应用。十项候选测试保留，尚未运行。


本次新增十三项测试依赖已只读核原十例保留，补Refresh跨侧选择、Restore显式null、原图像呈现DP异常同时Publish新run；语法通过，未编译运行。原件导航补证及当前产品执行差异已归档，见D07_ORIGINAL_IMAGE_NAVIGATION_20261028.json和原导航交接整链适配设计；未改执行产品链。
