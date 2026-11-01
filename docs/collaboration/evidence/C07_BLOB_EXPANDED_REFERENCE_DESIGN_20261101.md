# C07非模态展开订阅窗完整生产链设计（2026-11-01）

基线2f942e6d2201d943fda2e0318171073cc16899f8；唯一候选blob-reference-chain-20261101，分支codex/blob-expanded-reference-chain-20261101。不改主仓/正式Release、不启VM、不扩偏好或其他基础。

## 原件、现有实现及差异

复用B08_REFERENCE_WINDOW_OWNERSHIP_PREFERENCE_DESIGN_20261031.json与B08_ORIGINAL_REFERENCE_PREFERENCE_BOUNDARY_20261031.md：原AlgorithmTabRoot383–440/1558有当前开关、可空SelectedSubscribeParam、IsSubscribeIndex；ButtonSelecter598–606显式展开不写默认；ComprehensiveConfigControlBase.OpenSubWindow170–204/Close208–224管单Root一窗及Loaded延迟。原ParamSubscribeWindow61–146有Owner跟随/双向移动及状态条件，中文240、英文320；原窗/树模板入口见O06_BLOB_EXPANDED_REFERENCE_CHECKPOINT_20261027.json。原跟随只在规定BaseWindow/非SDK分支成立，不把HWND/多宿主互斥外推。本方当前为真实WPF Owner的平台适配，不复制SDK全局服务。

现原件链：ParamSubscribeWindow.xaml两个SubTreeView绑定Root.SourceNodes/IndexNodes、SelectedItem及IsIndex，OnTreeNodeDoubleClick→ParamsEventCommand→ReferenceWindowOwner转发→ReferenceExpansionRequest代次/目标/节点成员资格→同一ReferenceFieldSession→原字段接纳。SubTreeView仅叶容器双击触发；选中本身只是视觉投影。候选树完全来自宿主ReferenceFieldOptions，不在Wpf计算执行资格或读订阅运行值。OwnerWindow.Show为非模态，Follow Top/Left/Height及反向移动；不调用ShowDialog冒充展开窗。

现owner链：真实DocumentEditSession临时Preference→ProductionEditorContext→RuntimeNodeConfigurationControl.ReferenceWindowOwner→ReferenceFieldOptions→NativeNumberCoordinator/NativeRangePresenter/NativeReferencePresenter。每Root一窗、CurrentRequest可空；字段重绑只退役目标，不重建Root；整个Root隐藏/禁用/关闭/文档退役清request、modal和Window订阅。原ParameterBindingOrder/五整数政策与Float严格政策不变，无新合同或第二值owner。

偏好边界：原VmSolution841–881/UserData缓存和SDK读写可达，缓存先更新且底层写失败可能被吞；SaveAs/全新实例重开/Dirty/Undo及完整多宿主互斥未闭合，静态前沿未穷尽。本方仅ConditionalWeakTable<DocumentEditSession,...>会话内临时默认方式，不新增方案字段，不把当前开关/默认偏好混成同一bool，也不因本批运行宣称持久化完成。

## 已覆盖与实际缺口

S4FieldEditing WindowSession已有三消费者、默认true自动空窗、切字段/BindDraft/异常与退役、Owner隐藏/关闭、A→B及同ID重开；O06WindowPresentation原16组有原树/展开/索引/跟随等；BlobReferenceWindow9与BlobRangeProduction各16组有真实接纳/执行/保存，但它们主要用inline session命令，没有直接证明实际非模态窗两棵原树的容器双击最终消费。

本批补此组合缺口，不重复重写owner：新增BlobExpandedReference真实Host专轨，复用已有真实ImageLoad→Blob、原变量模态、私有执行/确认取消/历史/Copy/文件重开执行。所有GUI在自有隔离桌面，不SwitchDesktop，不操作用户窗口。实际容器选中先断言不写，再发原TreeViewItem MouseDoubleClick路由事件，经原EventSetter和原EventTrigger接纳；这是WPF事件链自动化，不称物理鼠标双击。固定索引走原可编辑索引文本，动态索引走实际索引树/变量窗，完整序列化身份与本轮结果对照。

## 同批矩阵与验收出口

1. 原窗/模板/两树/叶及组：实际非模态Owner关系、中文/英文宽度资源、单击不接纳/组不接纳、双击唯一接纳；原来源/索引集合必须与同一字段session引用一致，不克隆候选或伪造业务事件。
2. 低阈值、查找个数、面积左右端切目标：同一Root/物理窗复用，旧request无效，新request有效；Source/Index以及无目标/无选择空窗均不能写错字段。
3. 真实来源、变量、All/固定/AtField/AtVariable，私有调试精确ThresholdMinUsed及范围实际筛选；确认取消/历史/复制/保存重开对象执行保持完整身份；失败不得正式半写。
4. Owner移动/尺寸和反向移动、不改变模型；隐藏/禁用/关闭/字段重绑关闭或空窗按当前合同，事件对称退订，旧回调不关闭或改新窗。
5. 来源控制断边后保值、刷新禁用旧叶、Undo恢复；同ID文件关闭重开产生新DocumentEditSession、偏好不跨会话、新窗有效/旧request与旧树项不能写新旧草稿/正式/历史/文件。
6. 既有异常/同步重入清理测试复用；新增真实消费者缺陷才改对应产品方法并同版重建。没有具体产品缺陷则只提交测试与准确边界，不造空接口。

最终候选必须从本工作区标准UI/全量构建，不复制正式产物；逐次保存source、receipt、loaded-product.tsv和实际截图，核当前候选DLL，不冒充正式同版。自然键鼠/系统焦点/剪贴板、VS/DPI、VM保存偏好与动态来源全域语义另列未验；原完整C07清单保留。

## 定位与本批唯一产品修复

原中文001先通过真实展开树消费九组，在旧容器身份前置失败；不能据此判产品错误。Targets003分离观测证实旧容器已被WPF解绑：DataContext={DisconnectedItem}、MS.Internal.NamedObject、无ItemsControl关联、非原树后代、Loaded/Visible均false，旧node/DC均不属当前候选。实际RaiseEvent仍经原树事件转发1次，但payload是DisconnectedItem，不是旧业务node；随后无写。另以当前/旧request对缓存旧node的CanExecute验证成员身份门禁，两种证据不混称。003后半段新窗默认基本页未生成订阅按钮，是夹具错误，004通过真实Tab选择修复；002观察事件命名空间编译失败保留。未重设旧容器DataContext，未改成仅调用request替代UI事件。

修复前中文005/英文006 All各12组自然exit0，源码36C7C282...，各15个加载DLL匹配本候选Root；回执见C07_BLOB_EXPANDED_REFERENCE_PRELOCALIZATION_20261101.json。此时观察英文树无项仍为中文；定位DeclaredReferenceTree.cs:20硬编码“无”。现有Themes/Native/Localization/{zh-cn,en-us}.xaml已有None键（无/None），ReferenceFieldSession已使用AppLocalizationService.GetLang，因此本批只将公共Build的清除项标题改用同一正式资源。Clear身份、层级、候选、接纳、持久和API均不变，不新增资源或平台。本批冻结产品SHA C40AC560FAC69A91AC76ADC37738516E4E3515D1EC200DD34E49ADF6A985A673、测试SHA E2A6550EAEC894C619B936B336DEF37655321E66C501954EB94FA9F453899204；最终测试新增两树数据与实际TreeViewItem/TextBlock的本语言None断言。重建与复测结果另列，不能用修复前通过代替。

语言边界：LocalizationResourceDictionary单例首次按CurrentUICulture选语言，Refresh重建树；两语言分别独立进程验证，无语言热切换承诺。业务声明/用户模块名仍可能中文，不将英文资源加载称完整本地化。

FindNum仅验证展开树选择、当前值8及持久完整身份；范围8..8让Count=0只证明面积筛选，不是限数8的独立算法效果。默认true自动空窗、整体禁用、BindDraft及异常重入主要复用既有WindowSession/O06原16组回归，不冒称本新轨全覆盖。新轨Owner正常移动/尺寸/反向移动、隐藏/关闭及同ID新会话实测；最小化/最大化、真实鼠标/系统焦点/键盘、VS/DPI仍未验。固定索引由正式索引文本适配，动态索引由实际展开树/原变量模态；不称物理输入。

## 最终候选交接（2026-11-01）

- 基线2f942e6d2201d943fda2e0318171073cc16899f8，分支codex/blob-expanded-reference-chain-20261101；唯一写入工作区D:/MyWork/line-aiproject/blob-reference-chain-20261101。本批产品只修复DeclaredReferenceTree清除项语言，另新增完整真实Blob展开窗专轨与正式启动入口；不改资格、执行、保存合同或偏好格式。
- 最终冻结后标准UI build002：0错/6537警告；全量build002：0错/6498警告，原句柄3278自然exit0。无复制正式Release；标准HalconSourceRoot读取主仓原始依赖，输出为本工作区候选。
- 最终串行句柄92104自然exit0：中文attempt-007（PID142256）、英文attempt-008（PID41892）各12组、各自exit0；末组均自然完成，未增加预算/重启。两个进程各15个实际加载DLL匹配本候选目录，测试源码E2A6550EAEC894C619B936B336DEF37655321E66C501954EB94FA9F453899204，产品源码C40AC560FAC69A91AC76ADC37738516E4E3515D1EC200DD34E49ADF6A985A673，构建前冻结复核无变化。当前无本批GUI进程。
- 最终回执C07_BLOB_EXPANDED_REFERENCE_CANDIDATE_20261101.json包含两次receipt、实际DLL逐项身份、原始日志及截图SHA；中文实际两树“无”、240宽，英文两树“None”、320宽，已查看实际截图。候选与主仓正式DLL身份分别记录，不能以同源码称正式同版。
- 旧容器真实双击产生DisconnectedItem载荷并被安全隔离；缓存旧业务node的request成员资格另测。不是物理双击，也不是同一条证据；旧容器解绑前置诊断及前轮失败完整保留。
- 本轨验证低阈值、FindNum及面积两端目标切换/真实树接纳，来源和固定/动态索引、原变量模态、快照、精确越界拒绝、断边Undo恢复、确认取消/历史/真实保存重开执行、Owner移动隐藏关闭与同ID新会话旧回调。FindNum仅选择/当前值/持久身份，Count0证明面积筛选，不能证明限数8算法。
- 未完成：原偏好保存重开/Dirty/Undo未证且本方仅会话临时状态；默认真空窗、整体禁用、BindDraft及异常重入主要复用既有WindowSession/O06；OS最小化/最大化、自然键鼠/系统焦点/剪贴板、VS设计时/其他DPI未验。无语言热切换，用户/模块/声明名称不作为静态资源本地化缺陷。本批不称B08、S2/S3整体闭口或VM算法/全域索引语义等价。
- 正式验收出口：集成后在主仓正式Release分别设置C07_CASE=All、C07_CULTURE=zh-CN/en-US，每种独立调用Run-S2ReleaseValidation.ps1 -Stage S4 -Track BlobExpandedReference -ValidationScope Formal -TimeoutSeconds 480。用户人工验收未执行；不把其作为候选集成前置。
