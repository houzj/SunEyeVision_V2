# AI 协作交接单：ImageThreshold 最小 production draft

- 基线提交：dd6d0cc90a0cc944e813466a589b4387cb31c79f（前置c795）；分支codex/editor-context-production；执行者Codex子线；仅原独占C Editor工作树写产品。
- 任务目标：原编辑→运行→保存重开链的ImageThreshold最小生产草稿接入；本片不是完整B02/默认专业None/NodeOpen或原47母项通过。
- 允许/实际修改：UI/Services/Editing/EditorDraftAdapter.cs、ProductionEditorContext.cs、UI/Shell/Windows/MainWindowProductionEditing.cs；新增tests/SunEyeVision.UI.Tests/ImageThresholdProductionEditorTests.cs与本交接。父独占的NodeOpen/MainVM其余/Contracts/runtime/loader/算法/AssetRef未改。

## 行为与边界

factory明确准入ProtocolParse与ImageThreshold；Default/Custom/直接ToolDebugControlBase抽取实际control挂同Context。ImageThreshold按明确7个CLR标量槽复用ParameterEditSession，Method检查实际既有ThresholdMethod enum合法值；既有ResultPresentation.ValidateContract处理空/重复key，既有参数Validate处理数值/跨字段/结果判断；完整绑定/图仍现Host.Validate，唯一正式Host.Commit→Store.Publish/CAS。无新manager/Apply/union/asset。

已核RoiParameters/MeasureGuideParameters不override Validate；ToolParameters基类只返回空配置校验；ImageThreshold override不读运行图像。因此保存尚无运行图像的合法配置不被借运行readiness拒绝。ROI集合、Roi、RoiShape、Polygon2d.Vertices、ResultPresentation/判断项由现Host快照深草稿；新测试实际修改顶点、成员、集合和结果判断，发布前/重bind后/取消均核不别名。

父SDK额外编译源必须同时具备：147668bb3b34bfc2def9a16566c46efc7971e4b0中的src/Plugin.SDK/UI/Windows/CustomDebugWindow.cs，实际SHA256 32992F862D5B433F13752243C1218316EA16EBEA5D641C4514503A8B48D3F08B。仅git restore精确父文件，未自行改内容，也不纳子commit；子commit单独archive不是实际编译源闭包。父主树已含该依赖，合入时不得退回旧SDK。

## 实际验证（开发证据，非fixed）

owned根：C:/Users/houzhongjie/.codex/worktrees/editor-context-production/SunEyeVision_V2/artifacts/validation/editor-threshold-production-03。

- props/targets先XML parse，20项目逐一MSBuild实际TargetPath/OutDir/obj/PackagingRoot/HostDebugRoot全部本owned；输入清单project-path-preflight.json。离线NuGet clear源+cache mapping *，owned锁force-evaluate后strict restore两次自然0，repo锁未改。
- 设施targets只给UI.Tests追加ImageThreshold ProjectReference（ReferenceOutputAssembly=false，测试无静态插件依赖/不改csproj）；实际fixture按本测试AssemblyMetadata.HostDebugRoot精确加载plugins/SunEyeVision.Tool.ImageThreshold，走真实ToolRegistry和IToolEditorFactory，不改产品loader。
- vendor6件只复制既有本树已封开发输入，逐SHA见vendor-input-manifest；未拼主线A11/live DLL；原fixed根不写。
- build01 1错误1199警告66.65s：fixture误引UI.Tests未Compile的TestShared namespace；改按真实HostDebugRoot加载。build02 0错1192警告36.15s，产品/SDK完整受影响闭包成功。后续build03/04/05仅修测试设施，分别0错775警告19.22/19.37/54.41s。build06 --no-dependencies仅测试编译0错8警告4.66s（产品自build02未变）。原日志全部保留，不冒首遍PASS。
- threshold01 PID12276自然1：13FAIL，fixture手工参数discriminator与真实ToolRegistry重复权威冲突；移除手工注册，只真实ToolRegistry。
- threshold02 PID51036自然1：1PASS/12FAIL，每case新STA与产品静态WindowChrome线程亲和冲突。只改fixture为唯一真实STA+Dispatcher.Run，13入口、窗口/control/Chrome创建、Bind、Confirm、close/Dispose全Marshal同线程；无每case Application、不改产品Chrome。
- threshold03 PID44828自然1：9PASS/4FAIL。9为enum/oddBlock/C/InRange/ResultRange/null/key、ROI/Polygon深草稿及staleSession；4个pending因fixture未选实际运行参数Tab，FindName取得非当前视觉树模板。
- threshold04 PID59824自然1：只4失败，夹具假定Content=TabControl；已静态确认基类Loaded InstallBindingIssueSummary实际包Grid/AdornerDecorator，改为实际VisualTree按运行参数Header定位Tab。
- threshold05 PID61764自然0：只补测原4项，4PASS/0FAIL/0skip/3s。真实选中Tab→布局→同Dispatcher.ApplicationIdle队列→布局；断言control已Loaded、numeric可见、同PresentationSource及祖先链到实际control后，验证181/173经ConfigSetting Int32进入草稿/正式仅发布一次、not-int保文本/旧formal且Confirm拒绝、Custom转发与关闭不重复发布。

分轮覆盖为此前9 PASS + 最终4 PASS，共13个行为已取得通过证据；不宣称最终测试文件单轮13 PASS。旧9没有无关重跑；旧7/25/12/15均未跑。所有raw TRX与runtime receipt在results/threshold01..05及对应根文件；精确filter在每次runtime receipt及run-targeted-tests.ps1。

PEReader实际metadata见actual-test-assembly-metadata.json：HostDebugRoot为本owned C app；InfoVersion反映实际HEAD dd6d开发WIP，不冒本后继提交stamp。实际编译源按source-input-final.json磁盘SHA及normalizedGitBlob绑定，SDK父文件单独列出。没有本子commit精确archive的新D包；父将合含147的主树后全22同源新fixed。

## 未完成/验收owner

父负责真实MainVM私有factory/DocumentManager生命周期、同源全22完整app与独测原E06/E07（主173/变量181/not-int两入口，SHA+mtime）、实际图像Run→Save→Reopen。此组件夹具手动组Host/Context，调用真实factory/窗口initializer及生产TrackProductionEditor，但未构造完整MainVM/实际保存菜单，也未执行算法/借图像。staleSession是现isCurrent guard负控，不冒真实整工作区切文档操作。

Professional/None及NodeOpen迁UI由父另接；完整主窗、Release/VS设计时/集中人工、性能仍OPEN。旧原EDIT-SAVE-11的7PASS/2FAIL保持原身份，不能用此分轮组件PASS抹掉。未知plans漂移不改/不提交，CURRENT仅本树更新不合主树。父SDK文件diff不纳子提交。
