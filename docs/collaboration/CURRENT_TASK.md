# SunEyeVision_V2 当前任务账本

> 本文件只回答「现在在做什么、卡在哪、下一步做什么」。历史批次已按月切到 `history/CURRENT_TASK_ARCHIVE_<年-月>.md`（只追加、不改写），2026-11-05 之前共 2206 个批次已归档。
> 找文档先看 `docs/00-索引/主题台账.html`；找具体文件用 `docs/00-索引/文档索引.html`。

## 一、当前主线

| 事项 | 状态 | 关键结论 |
| --- | --- | --- |
| 统一开发基线 | 已建立 | `main` 指向合并提交 `b895d697e`（双父：重构分支 `f5e6b8fde` + 旧 main `d44904f38`，164 个旧提交全保留）；回退点 `backup/main-before-sync`；`origin/main` 尚未同步 |
| 数据目录启动门禁 | 实施中（代码未修） | 已定案：数据根写程序目录 `App.ini` 的 `[Data] Root`，不弹「选择软件数据目录」；退出 `data-location.json`、`suneyevision-data.json` marker、`schemaVersion` 协商 |
| main 控件原件回补 | 分批进行 | 按原件映射表回补 55 个控件提交 + 72 个验证夹具提交，不做逐文件人工合并 |
| 工具身份合同 | 已定案 | 发布态才有 `release-payload.json` + `ToolGroups.xml` 强准入；开发态 `dev\Debug\plugins\<Tool>\` 只有 dll/pdb/deps.json |

## 二、已定案、不要再反复讨论

- **版本口径**：方案格式版本 = V5；解决方案文件 `4.solution` 的 `SchemaVersion` = 2（与节点配置格式版本无关）。
- **数据根**：缺省 `%LocalAppData%\SunEyeVision`；配置存在但读不出直接抛异常，不兜底（rule-015）。本机 `Root=D:\SunEyeVisionData`，方案/日志零迁移。
- **Debug 与 Release 数据根不同**：`dev\Debug` 无 `App.ini` 时走缺省根，用 Debug 做人验看不到 `D:\SunEyeVisionData` 的方案，不是迁移失败。
- **构建口径**：唯一解决方案 `SunEyeVision.slnx`（61 工程）；全量 `dotnet build SunEyeVision.slnx -c Release --no-incremental`。

## 三、未验证（不得记作已通过）

统一基线真实程序人工验收、VS 设计器验证、真实相机/硬件、VM 动态差分、主仓推送（当前未 commit / 未 push）。

## 2026-11-06 迁移 REA 历史架构与源码树方案

已从 `D:\MyWork\REA逆向测试` 迁入与架构、源码树、插件边界、配置宿主、调试会话和验收模型直接相关的原文，位置为 `docs/07-工具逆向/历史-REA逆向测试/`。旧项目的 `VisionReplica.slnx` 和 `src/Vision*` 已确认是“独立兼容重建”实施树，不是已证明等同 VM 的最终树；旧 `*.vmx.json`、未闭合 `.sol`/SDK/ABI 和独立实现不直接迁入产品。历史树已回填 `docs/01-方案设计/2026/11/06/最终源码树评审稿-证据分级-20261106.html`，下一步按 VM 真实程序集、模板、NodeData 和生命周期证据逐项目映射。
## 四、最近 12 个批次

下面按倒序排列，最早批次已归档：`history/CURRENT_TASK_ARCHIVE_2026-11.md`、`history/CURRENT_TASK_ARCHIVE_2026-10.md`。

## 2026-11-07 性能与 SDK 状态纠偏（当前有效批次）

本批把“窗口性能”和“旧 SDK 迁移”拆成两个可并行收口的批次。性能问题不再用连续加探针拖延，现有探针已足以确认本方的第一处真实阻塞：Blob 专用组合和字段编辑器在当前页同步创建重型 WPF 视觉树。REA 本地重建项目的源码证据显示其链路是 `AlgorithmTab` 数据树 → 模板注册表 → 统一窗口壳 → 状态刷新，不能把该项目的测试耗时直接当作原厂 VM 数值，但足以确定当前 V2 的命令式专用组装方向与目标链不同。

性能批次的最快落地顺序固定为：先保留现有生命周期和字段合同，把普通字段与 Blob 专用控件统一登记到数据树和模板槽，不在 Tab 切换回调中临时拼装整组视觉树。然后交付 Blob 的一条完整链，首次显示只创建当前页可见模板，复杂 ROI、运行和结果槽按页首次激活创建一次，重复切换只刷新状态，不重建视觉树。最后以现有 `RuntimeEditorProductionProbe` 验收首帧、首次页切换、重复切换、重绑定和释放。该批独立于旧 SDK 删除，不等 SDK 全迁移才开始。

状态纠偏：`src/Plugin.SDK` 目前只剩空目录，旧 SDK 源码已移除，但正式工具项目仍有直接 `ProjectReference` 和命名空间引用，构建日志已出现 `MSB9008`。因此消费者迁移仍未完成，不能把旧 SDK 退场记作完成，也不能把当前破损引用当作迁移结果。性能批次和 SDK 批次共用新的 Contracts/Runtime/Wpf 合同，但分别验收。

旧 SDK 消费者迁移的新增事实：CircleCircleMeasure 等测量工具的 Runtime 仍可识别到新 `Plugin.Editing.Wpf` 中的基础执行类型，但 WPF XAML 仍直接引用旧 `ToolDebugControlBase`、Measure、ROI、ParamBinding 和 ResultPresentation 控件。旧控件源码已在历史提交 `8e76389de` 从 `src/Plugin.SDK` 删除，当前 `Plugin.Wpf` 尚未形成完整的一对一公共控件闭包。因此不能做纯命名空间替换；先迁公共控件/模板闭包，再迁工具 XAML 和生命周期。

本批已落地并验证：`DeclaredFieldEditor` 对高级字段初始延迟创建输入视觉树；`Plugin.Wpf` Release 构建 0 错误；`RuntimeEditorProductionProbe` 使用当前源码运行 ImageLoad 与 BlobAnalysis 实际 Session，Blob 首屏 364ms，首次第 2/3 页 988ms/1877ms，重复切换 38ms/37ms，捕获、回执和关闭释放通过。该结果只证明第一刀没有破坏生产链，不能记作 VM 等效性能完成。

随后接入 `Plugin.Contracts.ParameterPresentationTree`，通用宿主按树构造页和分组。最新探针仍通过完整 Session、回执、重复切换和释放，Blob 首屏 327ms，第 2/3 页首次切换 627ms/1419ms，重复切换 16ms/27ms。性能数据受运行环境影响，仍以生成层重构完成后的同场景回归作为验收，不把本次回归读数当作 VM 等效值。

数据树已修正同名分组的高级/普通状态拆分，避免改变既有布局语义。修正后 Plugin.Wpf Release 构建 0 错误，生产探针继续通过 ImageLoad/BlobAnalysis 的捕获、回执、重复切换和释放；当前一次读数为 Blob 首屏 433ms，第 2/3 页 1316ms/1546ms，重复切换 25ms/44ms。


## 2026-11-05 统一开发基线建立；数据目录门禁退出转入实施

main 已指到合并提交b895d697e（双父f5e6b8fde工作流重构分支codex/workflow-toolbox-dataflow＋d44904f38旧main，164个旧提交历史全保留），内容取重构分支为骨架。回退点backup/main-before-sync=d44904f38，origin/main仍9a0834ef8。判定依据：两线自共同祖先4431cfa02（2026-11-02 19:05）分叉3天，main 164提交/分支325提交；git merge预演2012个冲突，其中1719个为同一批VM原件被两边改成不同名字（1106个改名后内容完全相同）。分支承载框架重构（Workflow 39＋Application 55＋Domain 19＋tests 202提交），main承载控件原件迁移（55个代码提交）＋验证夹具（72提交）。定案：以分支为骨架统一，main控件成果按原件映射表分批回补，不做逐文件人工合并。

环境补齐：libs\halcon\runtime\x64-win64\原为空目录（被gitignore屏蔽），Directory.Build.targets:262强制要求存在；已从SunEyeVision_Dev-camera复制25个native dll（369 MB）。唯一解决方案SunEyeVision.slnx（61工程）统一基线全量构建已核实：dotnet build SunEyeVision.slnx -c Release --no-incremental → 0错误/6150警告/2:36.35，与历史全量口径一致（此前738系增量构建读数，已作废），日志.codebuddy\tmp\build-d1-full.log，产物Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe。工具插件新布局为dll直接落根目录，不再有plugins\<Tool>\子目录。前一批对f03 Release的冻结随统一基线切换结束（该冻结仅针对f03旧产物，不适用于统一基线新产物）。

启动门禁事件（只记录，代码未修）：用户首次启动统一基线被「选择软件数据目录」窗口阻断，报「应用目录定位格式不受支持。」。根因是本机%LOCALAPPDATA%\SunEyeVision\data-location.json与D:\SunEyeVisionData\suneyevision-data.json仍为schemaVersion:1（旧main于2026-11-05 11:08写入），基线要求2。临时处置：两处手改为2（原值备份.codebuddy\tmp\*.bak），解决方案文件4.solution的SchemaVersion字段＝2（与节点配置格式版本无关）可读；v2不再读Config\user-settings.json，主题/窗口布局/KnownSolutions偏好会丢一次。该手改属环境处置，不代表门禁已解决。data-location.json／suneyevision-data.json marker／schemaVersion 协商／dataId 目录身份均为我方自建；门禁整体退出的依据是原厂无「选数据目录」环节。

计划依据与分工：门禁退出是既有待办不是新需求（本文件「App数据目录门禁仍未实施退出」「数据目录旧Locate/选择窗/Remember门禁须整体退出」，重构计划「旧统一数据目录启动门禁纳入退出」）。2026-11-05起：rea-investigator负责VM应用层逆向取证＋工具层材料盘点（只写docs\collaboration\evidence\）；framework-refactor负责框架重构，第一批即数据目录门禁整体退出（只改src/tests）；doc-keeper只维护文档。

VM逆向取证实况：本机确认原厂D:\Program Files\VisionMaster4.4.0（此前探测漏检）。Applications\App.ini仅[DefaultSection] SaveSol=C:\Users\houzhongjie\Desktop；AppData.dat为XML产品标识；程序数据全在安装目录（data/ProjectParam/PublicFile/ColorStyle/Lang/Resources/Tools/platforms/myLibs），用户目录仅%APPDATA%\VisionMaster4.4.0\res；VisionMaster.exe字符串SaveSol命中8次，DataPath/UserData/WorkingDirectory/data-location均0次；.sol方案由用户自存（桌面5个），安装目录内无。初步结论：原厂VM无「选择软件数据目录」环节、无数据目录版本协商。注意D:\MyWork\参考项目\VisionMaster是HeBianGu开源示例，不是原厂VM，不作原件证据。

数据根定案（已定案，实施中，非已完成）：数据根写在程序目录App.ini的[Data] Root，不对用户弹窗（原厂VM 4.4的Applications\App.ini全文只有[DefaultSection]与SaveSol=C:\Users\houzhongjie\Desktop，路径配在程序目录ini）。规则：缺省%LocalAppData%\SunEyeVision；配置缺失用缺省并记Info日志；配置存在但读不出直接抛异常、不兜底（rule-015）；本机配Root=D:\SunEyeVisionData，现有方案/日志零迁移。同时退出：不读data-location.json、不读suneyevision-data.json marker、不做schemaVersion协商、不弹「选择软件数据目录」窗。今天的启动拦截是schemaVersion 1≠2造成的真实拦截，仅手工改现场两个文件应急，代码层未修，不得记述为门禁已修复或启动问题已解决。`[Data] Root` 与数据根概念为我方自建，仅借用「配置放程序目录 ini」这一形式；原厂 App.ini 只有 `[DefaultSection] SaveSol`。

数据根在两个输出目录的实际差异（本机实测）：Release\SunEyeVision_v1.0.0\SunEyeVision\App.ini 存在，内容 [Data]／Root=D:\SunEyeVisionData；dev\Debug\App.ini 不存在，Debug 构建启动走缺省根 %LocalAppData%\SunEyeVision（该目录存在，是更早期历史数据）。因此用 Debug 构建做人工验收时看不到 D:\SunEyeVisionData 里的方案和配置，别误判成数据迁移失败；要让 Debug 指向同一数据根，在 dev\Debug 放一份同内容 App.ini 即可。两目录均被 .gitignore 屏蔽（[Rr]elease/ 与 /dev/），App.ini 属本机部署产物、不入库，换机器需重新放一份。D:\SunEyeVisionData 下现有 Config/Solutions/Logs/.recovery，与 ApplicationDataPaths 子目录约定一致；.recovery 与 suneyevision-data.json 属已退出概念的历史残留，仅磁盘残留、不影响运行。

工具身份合同（发布态与开发态强弱不同）：发布态才有 release-payload.json ＋ ToolGroups.xml 的强准入；开发态 dev\Debug\plugins\<Tool>\ 实测仅 dll/pdb/deps.json，无任何清单，形态接近原厂松散目录，不得据此认为每个工具目录都有清单。

版本口径：当前方案格式版本＝V5（历史 V3→V4→V5，本文件旧段出现的 V3/V4 为当时冻结值）。解决方案文件 4.solution 的 SchemaVersion 字段＝2，与节点配置格式版本无关。

状态分列：已通过＝统一基线Release全量构建0错误、产物已出；临时处置＝本机两处数据目录标记手改schemaVersion（原值备份.codebuddy\tmp\*.bak）；实施中＝数据目录门禁整体退出（数据根已定案：程序目录App.ini的[Data] Root，本机Root=D:\SunEyeVisionData且方案/日志零迁移；退出data-location.json/suneyevision-data.json marker/schemaVersion协商/选择目录窗）、main控件原件分批回补；未验＝统一基线真实程序人工验收、VS设计器、真实相机/硬件、VM动态差分、主仓推送（未commit/push）。

## 2026-11-04 用户明确整条生产链贯通

工具UI、交互、工作流、图像源与Blob作为同一原型联动实施及自测；工具箱/节点连线/参数引用/正式与私有执行/同次图像结果/历史保存重开/取消关闭释放全部贯通后才签本次批完成。原主方案、矩阵及本批HTML已同步；缺基础保护，不以局部测试替代生产链。

## 2026-11-04 独立框架集成候选已接日志，构建在途

根设计固定984da7d45。新独立树D:/MyWork/line-aiproject/platform-framework-unified-20261104，分支codex/framework-unified-20261104，以984为基线；B日志d14/e174/6a三提交分别接成ce0d56e48/576b77e92/7d5d2e1a2，仅CURRENT/原主HTML冲突按根最新保留，B独立设计及最终JSON回执完整保留；产品冲突0。候选locked restore自然0，日志framework-unified-restore-20261104.log；候选全量Release已自然退出0：6216警告/0错误，2:28.76，日志framework-unified-logging-build-20261104.log；session25112已终态。候选Diagnostics16/File6/Footer7通过0fail0skip，日志framework-unified-{diagnostics,file-logging,footer}-20261104.log；尚非完整主窗/两工具联动自测通过（独立候选树，非根）。根Release未变。B正在只读核总监新Blob数值状态/结果显示消费者，根尚未接Blob新判断批；App数据目录门禁仍未实施退出，不能把日志整合说成启动问题已解决。A冻结其它工具扩展准备交接，C共享唯一草稿/动态输入与公共ROI批仍在途。
## 2026-11-04 已核收框架消息；先两工具原型统一基线

已读原版架构新增tool-layer-new-development-20261104及方案管理静态报告（7633D791…6039E6），总监交接81837f48a及产品ce64已接收，尚未统一到根f03。先现有图像源＋Blob打通新Session：正式Schema/Storage/空态、真实窗口、唯一草稿与历史保存、执行和私有调试快照、同次结果与图像区域reader释放。其它工具在途保留停止扩展；原全平台范围保留，Group/新循环暂停。禁止整仓恢复旧SDK/Workflow/主窗；两Blob候选未提交测试及设计修改原地保护。新主窗空态GetNodeSchema、运行publication.Schema、引用RootPort.Storage已核源码，新Int32状态/Float32总面积消费者仍待整批验证。

详细“框架消息核收与原型统一基线实施批-20261104.html”和原主计划/剩余矩阵同步本阶段。数据目录旧Locate/选择窗/Remember门禁须整体退出；B日志三个提交已固定，App路径接缝由唯一负责人集中调整，原厂资源/日志/恢复路径未证不猜。根人工查看Release继续冻结。独立几何探针修正reader.Values后构建0错0warn，实际Session链未运行不计通过；C公共ROI/宿主自测仍在途，不把子项结果签成完整批完成。
## 2026-11-04 当前人工查看Release保持不变

用户询问进度并确认打开路径。已告知可打开本树Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe；当前产品f03e6347f（几何常量整链源码），正式全量Release0错6154warn，载荷1A289D1261066C1E21C3AC6AE40BB3766C986FAD26FC626A8C211388C0B390E1，247条目只读核对0缺失/0字节差异。已明确承诺用户测试期间冻结此Release；不得再次构建/还原/签封覆盖本树产物，替换前协调关闭。源码分析、文档、并行独立树实施继续。几何七类/嵌套/严格反例的修复前3失败与修复后Persistence48通过已保留，但真实Session/历史/执行专项仍待，不把93上批UI232成绩转签f03。93/D5完整上批证据保留，f03只是当前可查看的新构建，不宣称全平台目标已完成。
## 2026-11-04 V5复杂值与变量原件集中集成完成；全平台继续

根产品93b1ff896，已合C完整95d/30cc/bd49/25072/cbf（原ded/463/873/6d8/7c74）与B完整63e354bfa（原48c，Shutdown保最终词汇捕获/实际变量场景）；146/188/93集中修复退出旧Hal装配后实际主窗依赖加载。最终61工程locked restore/全量Release0错3418warn，载荷D5A32CDD3C28CA68F9292092B3905CA1466BD37EF5E03E90726D155AF48BA054，49关键依赖实际字节相同。变量25/Persistence45/SDK280/完整UI232/公共变量6/Authority25均通过0skip；真实Session非空V5/档案2/保存重开/伪光栅零回调拒绝/TCP/无WPF生产RunChain13/变量Main原件命令和绑定单receipt/结果解绑重绑/隐藏两参数窗/完整App启动与全部reader-Session-目录-Dispatcher-OLE关闭均exit0。complex-variable-approved-*20261104日志、TRX和hash JSON为最终证据；先前失败保留，93本批以后无根活跃验证句柄。

关键依赖修复：仅批准清单PE+SHA；递归实际组件引用，已签封owner.deps经CLR resolver选RID胜出，不猜路径；已加载副本亦验实际bytes，组件入口严格来源保持。RunChain去显式HAL/Kernel bootstrap后仍过；A与B日志线已精确接同Authority文件，不重复合其版本。

原平台主HTML/完整剩余矩阵/全局变量方案及逆向计划/复杂值设计已集中统一，新增统一验证HTML与最终真实双主题PNG。原完整范围不缩：目标批量字段绑定/初始化配置执行/流程局部/档案UI/复杂新运行绑定等未实现保护；六测量/公共绘制、日志、其它工具、SDK退出和最终物理树继续，Group/新循环暂停；人工/VS/硬件/原厂动态/主仓未验。本批自测完成不等人工。

并行A平台measure-integration直接分支/引用/历史/保存中立探针自测通过，但绘制/原件整数下拉UI仍在途，尚未根集成；几何常量canonical未支持为真实共享缺口，与持久storedROI不同，暂保护不伪通过。C独立platform-measure-roi-implementation（63前置），独占公共ROI/字段+RuntimeNodeConfigurationControl，同草稿跨schema候选/实际帧/回调退休已定位，四原件与手势仍在途；隐藏未激活desktop直接CaptureMouse=false，不能冒称物理鼠标通过。B独立platform-neutral-logging（146+精确93前置），已批准中立完整日志设计，正在真实启动/关闭验证，根未合。逆向负责人已集中请求SectorCaliper与CircleAnnulus字段转换/内半径/单位及卡尺绑定证据，不顺手运行原SDK/硬件。主仓总监398a/ce2原13排序/C-R/F6/rad已固定，尚未接本方新Session，后续按真实消费者差异集中核收，不整文件回退旧SDK。
## 2026-11-04 正式开发构建工程图完成；复杂值集成核收中

根源码bd15ef061（a931统一入口/94dc重复签封已纠正），唯一SunEyeVision.slnx实核61工程/0缺路径；四脚本复用既有UI AfterBuild签封，退出旧.sln/App/Legacy视图与旧Workflow构建，MSBuild Rebuild不手删。实际build-full.bat Release0错6150warn/exit0，自动载荷66FCDC574EEE1B8A0538C301DB932F432BE930C06E6B66083349A29C1DB04C4A；同版RunChain13不加载WPF/UI/SDK、隐藏两工具窗口/根删除Undo身份退出0。canonical-build-entry-final-full / production-run / hidden-window-20261104.log。原主方案/矩阵/详细设计同步，VS重新打开与Rebuild所有变体未测，不冒签。

六测量完整HTML转义报告与原JSON快照已入根，资源SHA679BECD8C0E42623BB016D0FB06989B066DD886F2427BB9EDF84B3E545006FAA，报告4AE29D84E5B02B8E94BBAFA2943BEE3E79577F210DE645B6EB1730D6280048F9。已通知A角度包装/九值标定/父级触发，仍有原生入口不签静态穷尽。C候选6d8/文档58e独立全量及存储24/Persistence45/SDK280与真实Session/TCP/RunChain/隐藏App通过；根审发现codec接受任意IReadOnlyStoredRaster与受控实现设计矛盾，C补充7c74限定受控类型、25定向通过，实际保存拒绝链仍在验，未合入。B变量视觉原树唯一写；A六工具正式Authority/UI尚在途，显示真实帧消费由根核，不伪帧/塞ROI。全平台范围不缩，Group/新增循环暂停，人工/VS/主仓分列。
## 2026-11-04 根配置草稿所属查询捕获代次整链修复通过

根产品dafcf3c98（前置f7增加draft验证/真实回归；首次PowerShell保留变量使host替换未执行，before测试真实失败已保留）。host.OwnsRoot实际委托draft捕获scope→现Session.ValidateScope；删除→Undo同实体旧draft仍false/写入拒绝，新draft true且正常可编辑。全61工程Release0错6150warn，载荷1F304C6C643EEA28B59FC2EFD5D07E2337D492375C56850E3B3530B701540906：Editing39/39、根UI23/23、真实Main根操作/复制HALCON/保存重开/双主题/关闭排空退出0；root-draft-generation-*20261104和TRX。根无活跃构建测试句柄。原主HTML与矩阵当前作用域待办改为真实修复回执；联合5bf/1EBE证据保原版范围。

C已从192d48807新独立实施树D:/MyWork/line-aiproject/platform-complex-persistence-20261104启动完整复杂值/持久化/Domain/Application/实际UI非视觉和通信消费者，根不并写其文件；C不改本批两个Editing文件。当前其UI定位联编0错，不计整批完成/不可提前封包交付，旧测试须迁至新规范并真实资源验证。A测量整组设计384c58正核几何/绘制模式与当前frame能力；B变量UI只读设计bad313待根核收。18工具/全局变量/最终目标树/SDK完整范围保持，Group/新循环暂停，人工/VS/主仓分列。
## 2026-11-04 Blob完整新会话与中立画布统一集成通过

根产品5bf65949e（前置3a6画布中立命名、7bf根集合UI、3c可信目录、79域修复、b927采集连续）。61工程locked restore通过，Release0错6150warn；同版载荷1EBEECBE2FF6EA5B48B24DD5E1B7C3515AA5CA41199C3C1762F2E56A36539FFA：冷FixedSoft新testhost1、Blob36、UI227、Canvas27、Shell5全过且0skip；真实主窗根操作/复制HALCON/保存重开/双主题/关闭/OLE及连续暂停恢复/取消与同意Stop-await关闭均exit0，生产工厂RunChain13无WPF/UI/SDK和CoreContinuous15组过。blob-canvas-unified-*20261104+TRX为根证据。源图像/Blob字段与共享控件整批已合，不再标仅独立候选。根无活跃构建/测试句柄。

独立Blob最终243复合常量显示→原件Delete→Unset→父状态清理，UI201/201，失败用例400转正；已与完整36算法/资源专项集中入根同版验证。原件冷Result-first无主窗入口列防御未验。Host.OwnsRoot(draft)单独scope代次准确性仍待共享合同补齐，UI已退休窗口不代签该API。原主HTML和完整矩阵当前摘要统一替换，历史折叠保留。

下一并行批：A从5bf新platform-measure-audit-20261104只读设计六测量工具整组；C platform-sdk-complex-values-20261104细化唯一现合同上的V5/档案2/中立持久ROI和8/16位光栅设计，禁止重造纯值框架或弱化运行frame，未改产品；B变量UI整体只读设计bad313完成待核。其余18工具/全局变量/SDK活跃消费者/最终树完整范围不缩，Group/新循环暂停。未启动用户可见程序，未合主仓、未推送；人工/VS/主仓分列。
## 2026-11-04 可信目录与根集合UI完整集中集成通过

根产品7bf6812c3（3c32可信目录+caba中立名，8ee顶栏真实连续状态，c585根集合UI）；61工程全量0错6127warn。正式载荷BA3699FB31EE268C55BB041FACA43DEF2FFD6B384BE1121995A4F42F1DAD01DC：完整UI225/225、真实工具箱/顶栏1/1、Authority/目录/隐藏两工具参数窗25/25；真实Main根菜单/删除Undo旧窗退休/复制HALCON/保存重开/双主题/关闭排空、连续主窗暂停恢复/取消和同意关闭Stop-await/OLE、真实生产工厂RunChain13含无WPF/UI/SDK加载均退出0。准备组件独立载荷0951E30...真实重入/owner卸载过期拒绝退出0。catalog-root-ui-*20261104和TRX为根证据。首次探针路径、旧连续占位断言失败记录保留，不退回功能。根无活跃句柄。

Blob独立集成树c5da（d390正确Record资源读者、408双图+父状态、b5域修复），全量0错6145warn，完整专项句柄75098正在执行；之前D4D9载荷10资源/RunChain10/隐藏两窗过，不签全部。A继续真实pose常量关闭清除专项；B从7bf新独立树核完整画布中立命名；C复杂值设计46b8已给V5/档案V2与权威字段，待根精确审定后整批实施，未改产品。Host.OwnsRoot(draft)捕获代次缺口保留，现UI已同步退休窗口，不能代签API准确性。原主方案和完整矩阵当前摘要统一替换，原阶段证据折叠保留；18工具/SDK活跃消费者/全局变量/最终树完整范围不缩，Group暂停，人工/VS/主仓分列。
## 2026-11-04 域修复后的正式生产工厂无桌面整链通过

根1c前置79域修复全59工程Release0错6124warn已完成。RunChainProbe真实ImageSourceHostInputFactory的本地/模拟相机、正式/连续/私有调试、变量、失败、取消删除、Undo、V4及释放13组通过，并在Run返回且内部using释放后检查实际程序集：未加载WPF/UI/旧SDK。载荷5EB9C02DD823B77DFDA150EB51DBBF7641EE51CD95D82B6D617B53F19E957623；explicit-domain-integrated-{full-build,seal}与explicit-domain-production-{probe-build,runchain}-20261104.log。无根活跃句柄。全量Blob新专项A待最终固定后集成；C可信目录补中立命名，B根集合UI按C增量接缝核收，禁止整文件回退b927采集/连续。原主HTML和矩阵已同步，本次不签全平台/硬件/VS/人工/主仓。
## 2026-11-04 根集合核心与采集/连续主窗集中集成通过；域适配修复待复验

当前产品79f4ae856，前置b927（C完整82→a142与B cd60整批）、根集合676/669。根集合53工程最终Release0错6110warn，同版9372748341F19903AA766BC38B7B87770EC803EF1D5DDE7FD38C8D2D32B8BB6F：Editing39/Graph39、真实复制运行/删除Undo/V4重开通过，日志root-collection-integrated-final-*20261104。首次RootCollectionProbe私有工具副本失败保留，669将四工具只作构建引用，未绕门禁。

b927完整59工程Release0错6124warn；载荷AD5ABC601EB5C949C8EEB74D55332B5189E0A9A579B9B60041F4A8DE8D52867C：真实隐藏Main工具栏/暂停恢复/取消关闭/同意Stop-await/Dispatcher-OLE释放通过、CameraHost实帧参数/模拟生命周期probe通过、UI13/13、RunChain12、Core连续15通过。image-continuous-integrated-*20261104+TRX。locked restore因新项目图变更，重新求解后既有包版本/内容hash变化0（package-audit JSON），只锁图更新。工业未知域硬件前拒绝，未测试真实相机，不签硬件/VS/人工/主仓。

关键新缺陷：A全新物理线程证明HalconResourceBridge.CopyImage先CopyRegion导致GenRegionRuns按上一幅128格式把256全域裁成128；这解释FixedSoft/Relative按worker顺序不同，LUT不改。79f4（Blob树b5fc0d9ed）统一区域创建/仿射的tsp_clip_region保存-false-恢复，聚合恢复错误并释放未交付region。源码已合，尚未根构建/自测；修复前AD5不能签修复后79。A从be3建Blob新回归树，已同版导入b5，补线程域/六模式/8选择/资源/三页真实窗口，产品唯一由根修改。

根仍另写D:/MyWork/line-aiproject/platform-blob-integration-20261104（当前b5；此前be3完整构建/10合同/RunChain10/Core15/隐藏两窗口通过，未合根）；新fef父状态与7cdf四成员双图声明待集中核收，不追未固定版本。B从b927另树platform-root-collection-ui-20261104接根集合UI完整四操作/统一事务/回执/Tab；C独立可信目录e7/d643候选待根核收，并补命名与退役失败测试。根不并改其文件。全平台原HTML/完整矩阵统一本阶段效果、真实失败和剩余；完整18工具/SDK退出/最终目录树不缩，Group/新循环暂停。当前根没有活跃构建/测试句柄，不含子线自有句柄。





### 2026-11-07 当前对齐批次

- 当前审计基线：分支 `main`，HEAD `71da36329d405e29dab12d1b221269d2e5707ac4`；工作区保留 CodeBuddy 未提交改动，不能把工作区状态冒充正式提交。
- 图像源→BlobAnalysis 作为后续标准链：四个 Runtime/Wpf 工程已分别 Release 构建；Runtime 未直接引用旧 `Plugin.SDK`，Wpf 仅依赖自身 Runtime 与 `Plugin.Wpf`。
- 本批已完成静态源码映射和构建验证；当前源码已重新封存 `release-payload.json`，`NeutralReleaseProbe` 已通过无 UI 装载、ImageLoad→BlobAnalysis、reader、保存重开和取消排空。标准命令已固化为 `scripts/verification/Run-NeutralReleaseProbe.ps1`。下一步进入统一 VM 窗口宿主的首开、Tab/节点切换和关闭释放接入。
- 旧 `Plugin.SDK` 删除继续按消费者迁移门推进：先迁移图像源→Blob 完整窗口/执行链，再分批删除已无消费者目录。`n- 外部 SDK 边界已落地到 `PluginDevKit`：`sdk/net9.0` 只发布 Contracts、Runtime、Wpf、Foundation，RuntimePlugin、WpfTool 和 SamplePlugin 已独立构建通过；并在 `_validation/ExternalSdkAcceptance` 脱离主解决方案完成还原和构建。该开发包不再引用旧 `Plugin.SDK`。


## 2026-11-07 工具窗口分层探针收敛（当前批次）

已接手 CodeBuddy 的逐点探针并完成一轮分层定位。`RuntimeEditorProductionProbe` 现输出字段行、页面布局、显隐统计和 Blob 专用组合阶段。当前证据：Blob 基本参数 ROI 组合约 1.16–1.32s，运行参数专用组合约 1.10–1.18s，结果显示专用组合约 1.27–1.65s；通用字段行约0.59–1.16s，显隐约36–93ms，重复切页约20–50ms。根因收敛到工具专用组合首次实例化大量 `DeclaredFieldEditor`/原生呈现控件，不是Tab控件或显隐遍历主导。已回填 `docs/01-方案设计/2026/11/07/工具调试窗口性能取证与架构评审总结-20261107-131704.md`。新增诊断字段位于 `DeclaredFieldsView`、`BlobUiComposition` 和 `RuntimeEditorProductionProbe`，UI Release构建0错误（保留既有警告）。下一步按“数据描述 + 模板槽 + 复杂控件延迟/复用”设计 Blob 专用组合第一批，不直接删除 `DeclaredFieldsView` 或旧 Plugin.SDK。

## 2026-11-07 Blob 延迟组合第一批已落地

Blob 专用 ROI、运行参数、结果显示组合已从当前页同步路径移到 `DispatcherPriority.SystemIdle`。通用字段先显示，组合未完成时提交/确认/调试执行受门禁，异常显式留痕，关闭/重绑定清理待处理页面。探针显示首屏可见约0.29s，第2/3页可见约1.18s/2.03s，重复切页19–33ms，完整提交/释放回归通过。剩余瓶颈为通用 `DeclaredFieldEditor` 行创建，下一批继续做字段编辑器构造与模板复用优化，不删除 `DeclaredFieldsView`。

## 2026-11-07 通用字段级探针与资源作用域优化

新增 `DeclaredFieldsView.LastFieldBuildMs`，确认 Blob 页面单字段编辑器约25–103ms，页面总耗时由字段累加形成。页面构建增加 `EditorResources.BeginSharedResourceScope()`，运行参数通用字段阶段由约0.78s降至约0.61s，提交/释放回归通过。下一批按字段类型拆解 `DeclaredFieldEditor.Render`，优先数值、范围和结果显示字段。

## 2026-11-07 加速执行口径

按最快路径收敛为一条标准链优先：先完成 BlobAnalysis 的普通字段模板/数值范围/结果显示性能批次，固定窗口、Tab、提交、回调和释放合同，再以该合同批量迁移普通工具，最后批量迁移旧 Plugin.SDK 消费者并封存外部 SDK 验收。未接入的延迟字段实验已清理，不作为现行实现。

## 2026-11-07 性能线时间盒决定

本窗口问题不再阻塞全平台重构。已确认生成层设计差异和主要成本，保留已验证的首屏先显、复杂组合空闲组装、提交/释放门禁和分层探针。未验证收益的高级字段延迟实验已撤回。当前批次以 Blob 标准链行为和生命周期通过为出口，字段模板化作为独立后续批次，随后立即转入普通工具和旧 Plugin.SDK 消费者迁移。

公共控件闭包门槛已回填 docs/collaboration/evidence/PluginSDK消费者迁移清单-20261107.html。测量工具迁移必须覆盖调试窗口基类、Measure/ROI 槽、ParamBinding/ConfigSetting 通用模板和 ResultPresentation 槽四组闭包，分别完成打开、Tab、提交、回绑定、回调退订和释放验收后，才可清零对应旧 SDK 消费者。

公共闭包实施进展：已将无业务依赖的 `LabeledControl` 迁入 `src/Plugin.Wpf/Controls`，并接入 `Themes/Generic.xaml`。Plugin.Wpf Release 构建通过 0 错误。`ConfigSetting`、`ParamBinding`、Measure/ROI 和 ResultPresentation 仍待按完整依赖闭包迁移。

归类纠正：`LabeledControl` 来源是旧 `Plugin.SDK` 的本方自研代码，不是 VM 逆向原件，也不作为 VM 控件证据。它只是公共标签布局容器的迁移基础。VM 原件控件仍须依据程序集、模板和资源证据逐项迁移。

ImageSource/Blob 状态纠偏：两个工具的正式项目已经没有 `Plugin.SDK` 引用，普通参数和 XAML 外壳使用新的 `Plugin.Wpf`。但 Blob 当前仍使用 `BlobUiComposition`、`BlobRunUi`、`BlobDisplayUi`、`BlobRoiUi` 以及 `Native*UiDraftControls`、`NativeUiDraftRange` 等本方视觉组合适配器。它们只复用部分逆向迁入的底层控件，不是 VM 原件控件本身；其中纯生命周期/状态适配可保留，视觉组合必须迁到 VM 模板和原件控件闭包后才能算最终完成。该遗留正是当前性能和外观差异的主要重构对象。

Blob 相关模板证据已定位：`docs/07-工具逆向/decompiled/ModuleControls/IMVSBlobFindLabelsModuControl/IMVSBlobFindLabelsModuControl/ThresholdAreaPanel.xaml` 使用 `ItemsControl`、数据模板 `ThresholdAreaItem`、`Integer_Sub` 和 `IntegerBettween_Sub`。该文件属于 `IMVSBlobFindLabelsModu`，与当前 `BlobAnalysis` 的工具身份尚未完成逐项映射，因此目前只能作为相关 VM 模板候选证据，不能直接据此替换 `NativeThresholdUiDraftControls`。下一步先完成工具身份、NodeData 和模板键映射，再实施替换。

## 公开未完成项（必须按此状态汇报）

以下事项均未完成，不能用“已接入新合同”“探针通过”替代：

1. **Blob 不是 VM 控件全量迁移**。`BlobUiComposition`、`BlobRunUi`、`BlobDisplayUi`、`BlobRoiUi` 和多项 `Native*UiDraftControls` 仍是本方视觉组合适配器。
2. **VM Blob UI 映射尚未闭合**。当前工具身份已确认是 `IMVSBlobFindModu`，但其完整程序集、NodeData、模板键、复合控件和释放链仍未逐项映射完成。
3. **旧 SDK 消费者未迁移**。20 个正式工具项目仍有旧项目引用或命名空间引用。旧 SDK 源码已删除，当前存在破损引用和 `MSB9008`，不能记作成功退场。
4. **公共控件闭包未完成**。目前只迁入少量基础控件，例如 `LabeledControl`。`ConfigSetting`、`ParamBinding`、Measure/ROI、ResultPresentation 和统一窗口宿主仍未完成。
5. **VM 等效性能未证明**。当前探针只证明本方生命周期和回归链未被阶段性修改破坏，没有原厂 VM 同场景耗时对照，也不能宣称达到 VM 流畅度。
6. **提交基线未固定**。当前工作区仍混有大量既有 CodeBuddy 未提交改动，本批修改未形成独立可回退提交。

后续每次汇报必须分别列出“VM 已确认”“本方已实施”“本方过渡实现”“未确认/未完成”，不再把阶段性代码或本方探针结果描述为全量重构完成。

## 当前阶段范围收口（用户最新决定）

本阶段只收口 `ImageSource` 和 `BlobAnalysis`，20 个旧 Plugin.SDK 工具消费者暂不处理，不再占用本阶段的实现和验收路径。阶段目标是完成两个工具的 VM 原件映射、参数树/模板宿主、Tab 创建与复用、回调退订、资源释放和外部 SDK 基线。

Blob 原件身份已经确认是 VM `IMVSBlobFindModu`。证据为 `docs/07-工具逆向/decompiled/Managed/BlobFind/IMVSBlobFindModuCs/BlobFindParam.cs`，其 `ThresholdTypeEnum`、`PolarityEnum`、软阈值字段、排序特征和连通性与 `tools/SunEyeVision.BlobAnalysis/Runtime/Resources/BlobParameters.xml` 对应。`IMVSBlobFindLabelsModu` 的控件模板只作相关参考，不作为当前 Blob 的直接 UI 映射。

本阶段不再扩展旧工具迁移清单。只有 ImageSource/Blob 完成最终 VM 控件闭包和完整生产链验收后，才恢复旧工具消费者批次。

Blob 全量性纠偏：VM `BlobFindParam.cs` 当前可见的公开参数包含阈值、极性、轮廓/图像输出、孔洞、面积、角度、矩形宽高、周长、短长轴、圆度、矩形度、质心偏移、轴比、排序、连通性、内接矩形、外部像素以及多组结果限制字段。当前 `BlobSettings.cs`/`BlobParameters.xml` 只覆盖其中一部分，并保留了本方简化的输出和判定模型。因此当前 Blob Runtime 不是 VM `BlobFindParam` 的全量重构。下一批必须先完成 Runtime 参数/NodeData 全量映射，再把已有逆向控件和模板接入 UI，不能重做控件库或只替换命名空间。

已有控件成果纠偏：`f62915240` 已完成公共控件中立命名和资源迁移，`5bf65949e` 已集中接入 Blob 参数、双图结果和原件控件会话链，涉及 72 个文件、3106 行变更，并有 Blob Session 回归测试。当前缺口是 IMVSBlobFindModu 的全量数据/模板接线和过渡组合器收口，不是从零建立 Blob 控件库。

## 批处理实施模式（用户最新决定）

ImageSource/Blob 本阶段改为先完成代码级完整实现，再统一构建、生产链回归和 VM 对照测试。中间控件、单页、单回调和局部探针不再单独汇报，不以阶段性读数宣称完成。实施输入统一来自现有 REA 本地源码、VM 程序集/模板/资源证据、当前 `Plugin.Wpf` 逆向迁移成果和 `IMVSBlobFindModu` 参数映射。实现闭合前不再扩展 20 个旧工具消费者。

本阶段逆向输入范围扩展：除 `D:\MyWork\REA逆向测试` 外，必须直接利用 `docs/07-工具逆向/decompiled` 的 VM 托管实现、编译 XAML/BAML、模板资源、程序集清单和控件代码。当前自动盘点显示 VM `IMVSBlobFindModuCs/BlobFindParam.cs` 有 112 个公开字段，V2 `BlobSettings.cs` 只有 35 个属性、`BlobParameters.xml` 只有 28 个参数项。该差异是代码级全量重构缺口，不能只靠现有本地方案或继续加探针解决。

算法证据纳入本批：ImageSource 与 Blob 的 VM 托管算法、参数类、结果类和资源调用链已有逆向材料，本阶段不重新猜测算法语义，不重复做局部探针。实现以已有 VM 代码证据为迁移基线，统一完成 Runtime 参数/执行、WPF 模板/复杂槽、窗口生命周期和释放，之后一次性执行构建与生产回归。

Blob 全量字段映射已生成：`docs/collaboration/evidence/Blob-IMVSBlobFindModu-字段映射-20261107.json`。当前统计为 VM 112 个公开属性、V2 `BlobSettings` 35 个属性、参数树 28 项，只有 3 项同时接入字段和参数树，93 项尚未接入。该 JSON 是本阶段 Runtime/NodeData/UI 批量实现的输入，不再逐字段凭感觉补丁。

## 2026-11-08 代码级批处理第一刀（ImageSource/Blob范围）

Blob Runtime 已新增 `BlobVmFindParameters`，按 `docs/07-工具逆向/decompiled/Managed/BlobFind/IMVSBlobFindModuCs/BlobFindParam.cs` 建立 VM 参数闭包，覆盖阈值、极性、轮廓、面积/几何筛选、排序、连通性、结果限制等公开参数。`BlobSettings.Freeze()` 会把现行宿主字段投影到该闭包，`BlobSession` 从冻结快照读取 VM 参数并执行新增几何筛选和结果限制，避免执行侧读入编辑中间态。当前闭包已进入代码，尚未宣称所有 VM 结果字段、内接矩形、灰度统计和原生动态接口等语义全部等价。

Blob 参数页组装已补上后台逐页预热链。`DeclaredFieldsView` 暴露页身份和一次性页实现入口，`BlobUiComposition` 在当前页完成后按 Dispatcher Background 逐页建立并缓存，Tab 切换不再首次进入时同步拼装整页。该链仍需在统一 Release 产物上与 VM 同场景回归，当前构建受工作区既有 build_temp 并发/锁定影响，不能把本批构建记作通过。

本批生产参数窗回归已保存到 `docs/collaboration/evidence/ImageSource-Blob-VM批处理生产参数窗回归-20261108.txt`。现有正式探针结果为 ImageLoad 绑定约 340ms、首次呈现约 1179ms，BlobAnalysis 绑定约 420ms、首次呈现约 375ms，Blob 第 2/3 页切换约 162/300ms，重复切换约 15/20ms；两个工具的实际 Session、节点、参数控件、提交/回执和关闭释放均通过。该读数是本方生产链回归，不是 VM 同机等效证明。日志仍提示 RuntimeEditorProductionProbe 的历史旧 Plugin.SDK 项目引用缺失，本阶段未处理 20 个旧工具消费者。

VM 参数闭包的独立冒烟结果保存于 `docs/collaboration/evidence/Blob-VM参数映射冒烟-20261108.txt`，104 个 VM 参数属性的类型闭包和代表性阈值/面积映射通过。

2026-11-08 后续批次已把 VM 运行参数接入 `BlobParameters.xml` 和 `BlobParameterDefinition` 的双模型绑定。参数树从 28 个 Parameter 节点扩展到 105 个，Runtime 参数展示字段为 139 个，104 个 VM 运行参数已经进入参数树或通过现行宿主字段映射。`BlobSettings` 仍保留宿主资源接缝，InputImage、ModuRoiManager 以及枚举类型不伪装成普通字段。新的生产回归日志为 `docs/collaboration/evidence/ImageSource-Blob-VM完整参数树生产回归-20261108.txt`，两个工具提交、回执、Tab、释放链通过；Blob 首次绑定约 720ms，后台页构建后第 2/3 页切换约 228/276ms，重复切换约 19/25ms。

当前完整参数树已切断 Blob 的重复命令式运行参数组装。检测到完整 VM 元数据时 `BlobRunUi` 只保留正式字段状态适配，不再创建隐藏的 `ui-only-run.*` 控件。生产回归保存在 `docs/collaboration/evidence/ImageSource-Blob-VM正式参数树无重复组装回归-20261108.txt`，最新读数为 Blob 绑定约 808ms，后台页构建后第 2/3 页切换约 262/337ms，重复切换约 19/29ms，两个工具完整窗口链仍通过。

VM `BlobFindResult` 已新增中立投影 `BlobVmResultProjection`。面积、状态、数量、质心、外接框、角度、周长、长短轴、圆形度、矩形度和二值/Blob图像已形成代码映射，灰度统计、最大内接矩形、Score、ROIAnnulus、ContourInfo、屏蔽区多边形和 OutputMask 保持明确未接入。投影冒烟证据在 `docs/collaboration/evidence/Blob-VM结果投影冒烟-20261108.txt`。

旧 SDK 消费者已补充源级映射 `docs/collaboration/evidence/PluginSDK消费者源级映射-20261108.json`。扫描结果区分 20 个仍有正式 `ProjectReference` 的工具项目和 2 个只有历史命名空间字符串的宿主项目，记录每个项目的源码/XAML 闭包和新合同目标。20 个直接消费者仍冻结，未开始删除旧 SDK。


## 2026-11-08 代码级闭环继续推进

本批按 VM BlobFindParam/BlobFindResult/IMVSBlobFindModuTool 反编译源码和 REA 本地图像源/Blob 实现直接补齐 ImageSource 与 BlobAnalysis 代码。`BlobVmFindParameters`＋105 项 XML 参数树已成为单一冻结入口。BlobImage 改为从筛选后候选集合生成，Auto/RelativeSoftThreshold 按有效检测域统计，`OKWhenNumIsZero` 接入结果判定。`Directory.Build.props` 排除所有嵌套 `build_temp`，修复隔离构建产物被重新编译造成的重复程序集特性。PluginDevKit Runtime/WPF 模板加入发布 DLL 显式引用，指定 SdkPath 后可脱离主仓构建。

最新生产探针通过：ImageLoad 绑定 615ms、首显 1223ms；BlobAnalysis 绑定 843ms、首显 337ms；第2/3页首次切换 227/307ms，重复切换 19/28ms；两工具实际 Session、参数控件、捕获/回执、Tab、关闭释放通过。证据：`docs/collaboration/evidence/ImageSource-Blob-VM正式参数树无重复组装回归-20261108.txt`、`docs/collaboration/evidence/PluginDevKit模板引用回归-20261108.txt`。

边界仍保持：20 个旧工具消费者未迁移，旧 Plugin.SDK 未删除；VM 原厂同场景动态性能未获得；Blob 结果投影中 13 个原件成员无当前资源证据；xUnit 运行被旧 release-payload.json 与本次重建 Application SHA 不一致拒绝，需固定提交后重新封签再回归。当前工作区仍为 CodeBuddy 混合未提交基线，不得据此宣称正式集成完成。

UI Release 构建已完成，0 错误，构建封签已重建。随后 BlobAnalysis xUnit 工程编译完成，但 21 项仍在 ProductRuntimeTestHost 初始化阶段因 `SunEyeVision.Execution.dll` 与 release-payload.json SHA 不一致被拒绝，未进入算法断言；需要单独隔离测试输出或在同一固定提交后重新封签，不能把该结果记为算法失败。

UI Release 封签后再次运行 RuntimeEditorProductionProbe：ImageLoad 绑定 757ms、首显 1324ms；BlobAnalysis 绑定 784ms、首显 320ms；第2/3页首次切换 254/268ms，重复切换 19/28ms；两工具完整 Session、参数控件、回执、关闭释放 PASS。证据已追加到 `docs/collaboration/evidence/ImageSource-Blob-VM正式参数树无重复组装回归-20261108.txt`。

验证收口：BlobAnalysis xUnit 在正式 Release 重新封签后，以 `BuildProjectReferences=false` 编译测试、`--no-build` 执行，37/37 通过。PluginDevKit `build-sdk.ps1 -Configuration Release` 与 `validate-components.ps1` 均通过，SDK DLL 已从当前 Release 重建复制。证据：`docs/collaboration/evidence/Blob-VM参数映射冒烟-20261108.txt`、`docs/collaboration/evidence/PluginDevKit模板引用回归-20261108.txt`。

最终封签后生产探针再次通过：ImageLoad 绑定 801ms、首显 1596ms；BlobAnalysis 绑定 813ms、首显 341ms；第2/3页首次切换 212/265ms，重复切换 18/26ms。两工具完整链 PASS。

本批源码与证据已固定提交 `033fc093d`（中文提交标题）。该提交只包含 ImageSource/BlobAnalysis 重构、参数页生成层、PluginDevKit 模板/SDK、测试和本批证据；其他 CodeBuddy 未归类改动仍留在工作树，未混入提交。

最终固定基线：当前分支 `feature/param-window-datatree`，本批提交链为 `033fc093d`（源码/测试/证据）、`53c5084dc`（任务账本）、`cb3d63ffc`（VM字段与消费者映射）。完整 BlobAnalysis 回归 37/37 通过，最终生产探针和 PluginDevKit SDK/组件验证均通过。当前未提交内容仅为其他文档预览和 CodeBuddy 未归类改动，不属于本批源码基线。


PluginDevKit 外部工程隔离验收已通过：复制 SDK DLL/XML、模板及开发包自带 Directory.Build.props/targets 到 artifacts/validation/ExternalSdkAcceptance-20261108，Runtime/WPF 模板 Release 构建 0 错误，Runtime/Editor ToolComponent 身份校验 PASS。证据：docs/collaboration/evidence/PluginDevKit外部工程隔离验收-20261108.txt。仍未完成 Visual Studio 启动或附加正式产品宿主人工验收。


新增 scripts/verification/Test-PluginSdkRetirementGate.ps1，ReportOnly 列出 20 个直接消费者，正常模式按预期阻断旧 SDK 删除。ImageSource/BlobAnalysis 无旧 SDK 消费。证据：docs/collaboration/evidence/PluginSDK退役门禁-20261108.txt。

## 2026-11-08 CodeBuddy 方案对齐与首批收口

CodeBuddy 的“数据树 + 模板 + 页面复用”方向已作为候选重构方案采用，但其原始评估中的静态性能结论、VM ≤80/100ms 预算和“零构建”表述均不作为已确认事实。`docs/01-方案设计/2026/11/07/参数窗对齐VM数据树模板化优化方案-20261107-153655.html` 已补充评审结论，区分历史基线、实施回归和未确认 VM 动态机制。

本批以已有 VM BlobFind 参数/结果/工具逆向代码和 REA 本地图像源/Blob 实现为输入，已完成 ImageSource 与 BlobAnalysis 的代码级完整链。当前固定生产回归读数为 ImageLoad 绑定约 801ms、首显约 1596ms；BlobAnalysis 绑定约 813ms、首显约 341ms；第 2/3 页首次切换约 212/265ms，重复切换约 18/26ms。BlobAnalysis xUnit 37/37 通过，两个工具 Session、参数控件、捕获/回执、Tab、关闭释放均通过。该结果证明本方重复组装瓶颈已被页面缓存和 VM 参数树入口显著收敛，不证明已达到 VM 原厂同机性能。

当前执行计划固定为：

1. 继续以 ImageSource→BlobAnalysis 作为标准链，补齐 VM 同场景动态窗口证据和时间线对照，禁止把本方实现推定成 VM 机制。
2. 只在两工具范围内收口未映射的 VM 结果成员和资源接缝，保留已确认的图像资源、节点身份、执行、保存和释放宿主边界。
3. 20 个旧 Plugin.SDK 消费者保持冻结。`scripts/verification/Test-PluginSdkRetirementGate.ps1` 已建立删除门禁，只有消费者迁移并通过完整链路回归后才允许删除历史引用。
4. PluginDevKit 的发布 DLL、模板和干净目录构建已通过，外部 VS 启动/附加正式宿主仍列为未验收项。
5. 工作区中 `docs/08-工具预览/ui`、脚本和其它 CodeBuddy 未归类改动不属于本批固定基线，必须单独审查，不与已提交源码混合。

当前固定源码基线为分支 `feature/param-window-datatree`，提交链 `033fc093d`、`53c5084dc`、`cb3d63ffc`、`1229f21de`、`27752cd20`。交接或继续实施前以该提交链和本节为准。

## 2026-11-08 Blob 结果投影灰度成员收口

基于 VM `BlobFindResult` 结果声明和现有 HALCON Blob 区域，已把 `MinGrayValue`、`MaxGrayValue`、`GrayContrast` 接入 `BlobMeasurement`、`BlobVmResultProjection` 和生产执行链。投影只在每个保留 Blob 都有真实值时发布数组，否则继续保留 `Unavailable`，没有用默认值填充原件结果。证据为 `docs/collaboration/evidence/Blob-VM结果投影-灰度统计-20261108.txt`。

验证结果：BlobAnalysis xUnit 38/38 通过；UI Release 构建 0 错误并重新封签；RuntimeEditorProductionProbe 通过，ImageLoad 绑定 721ms、首显 1301ms，BlobAnalysis 绑定 830ms、首显 337ms，第 2/3 页首次切换 231/279ms，重复切换 17/26ms。当前 Blob 结果投影仍有 10 个 VM 成员待接入，具体为最大内接矩形、Score、ROI、ROIAnnulus、ContourInfo、BlindPolygon 三项、OutputMask 和 ModuStatus。该批不扩展未被原件证据闭合的语义。本轮代码、测试、证据和架构回填固定于提交 `e571842cd`。

## 2026-11-08 清理未使用自研标签控件

已确认 `src/Plugin.Wpf/Controls/LabeledControl.cs` 仅是自研标签容器，ImageSource、BlobAnalysis 和 `DeclaredFieldEditor` 没有引用它。实际参数编辑器使用 `Plugin.Wpf.Native.Composition.ParameterPresentation` 下的逆向控件。已删除该类型，主题资源中的对应样式同步移除。

验证：`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore` 0 错误并重新封签；`RuntimeEditorProductionProbe` 通过，ImageLoad 绑定 797ms、首显 1216ms，BlobAnalysis 绑定 780ms、首显 315ms，第 2/3 页首次切换 204/254ms，重复切换 15/24ms。两个工具完整 Session、参数控件、捕获/回执、关闭释放 PASS。

## 2026-11-08 清理生产探针旧 SDK 锁文件

`tests/RuntimeEditorProductionProbe` 的项目引用已经是新合同，但旧 `packages.lock.json` 仍残留 `SunEyeVision.Plugin.SDK`。本批重新评估锁文件，改为 `SunEyeVision.Plugin.Editing.Wpf`，探针 Release 构建 0 错误，`MSB9008` 旧 SDK 项目缺失警告消失。20 个旧工具消费者仍按退役门禁冻结，本次只清理探针工程的过期锁文件。证据已追加到 `docs/collaboration/evidence/PluginSDK退役门禁-20261108.txt`。

## 2026-11-08 CodeBuddy 窗口入口冲突纠偏

审查未提交 CodeBuddy 改动时发现 `MainWindowRuntimeEditing` 被改成同步完成 `BindDraft` 后才 `Show` 窗口。该顺序会把约 780ms 的参数组装放到窗口首次可见之前，与 VM 流畅打开目标和现行“先显示窗口壳、再按生命周期装配”合同冲突。已恢复该入口到固定基线，保留探针代码作为定位材料，不把该候选实现纳入正式重构。

恢复后 UI Release 构建 0 错误并重新封签。未提交的 `RuntimeNodeConfigurationControl` 探针、`RuntimeEditorProductionProbe` 输出扩展、工具预览文档和脚本改动仍保持独立，未混入正式基线。

当前最新固定提交链追加为 `e571842cd`（灰度结果投影）、`5f13e011d`（移除未使用自研标签控件）、`1d78d7ea5`（清理生产探针旧 SDK 锁文件）、`682a782dc`（纠偏同步显示入口）和 `c6f5e7d2b`（固定窗口分段探针）。

## 2026-11-08 PluginDevKit 正式宿主启动检查

已用当前 Release 重新准备 `PluginDevKit/runtime`，确认 `release-payload.json` 与 `ToolGroups.xml` 存在。启动 `SunEyeVision.UI.exe` 后进程保持运行 8 秒，随后安全结束。该结果补齐了外部 SDK 的正式宿主进程启动检查。Visual Studio 启动或附加调试仍未验收，不能与本次进程启动混称完成。

## 2026-11-08 正式载荷插件发现与生命周期探针

将 `AdmissionLifecycleProbe` 入口程序集放入 `PluginDevKit/runtime` 正式载荷根目录运行后完整通过。验证了 BlobAnalysis 与 ImageLoad 的运行组件发现、Schema 唯一性、保存重开、运行、调试准入、跨代回调、结果 reader 保留、旧代拒绝和释放排空。直接从探针自身输出目录启动会因 Foundation 先加载自有副本触发 SHA mismatch，已确认是测试布局问题，正式 runtime 根目录运行通过。Visual Studio 启动或附加调试仍单独列为未验收。

## 2026-11-08 外部 SDK VS 附加准备

当前机器没有 `devenv.exe` 或 `vswhere.exe`，无法执行真实 Visual Studio 附加。已增强 `PluginDevKit/scripts/launch-debug.ps1`，支持 `-Detach` 和会话回执，分离启动测试通过，回执包含 PID、宿主路径、运行根目录和附加范围。README 已补充使用方式。该脚本为外部 VS 附加提供可复现入口，不能代替 VS 实际附加验收。

## 2026-11-08 VM Blob 原件结果桥接补证

REA 已确认 `IMVSBlobFindModuCs.dll` 只负责 `BlobFindResult` 容器初始化和 `Run → serverSDKManager → SyncSelfRun` 转发，真实结果值由 native `IMVSBlobFindModu.dll` 产生。当前 Windows 环境未配置 Ghidra/IDA，Hopper 不支持该主机，native 函数级追踪暂无法继续。10 个剩余结果成员继续保持 `Unavailable`，没有按名称近似实现。证据：`docs/collaboration/evidence/Blob-VM原件结果桥接证据-20261108.txt`。

## 2026-11-08 两工具代码级重构收口与窗口状态机校正（当前有效批次）

本批按现有 VM 逆向程序集、参数 XML、资源身份、控件模板和 REA 本地源码证据，直接收口 ImageSource 与 BlobAnalysis 的 Runtime/WPF 算法与生产链，不再以增加探针作为主要工作。20 个旧 Plugin.SDK 消费者继续冻结，旧 SDK 删除门禁不改变。

已验证：

- `tests/SunEyeVision.ImageSource.Runtime.Tests` 顺序运行 31/31 通过。此前 6 个失败是未设置 `S2_CASE_ROOT`，补齐验证根后通过。
- `tests/SunEyeVision.Tool.BlobAnalysis.Tests` 顺序运行 38/38 通过。此前 22 个失败是并行构建同时写正式 Release 目录造成的 `release-payload` SHA 竞争，不是源码失败。
- `tests/RunChainProbe` 使用最新正式 Release 载荷参数运行，ImageLoad→Blob、私有调试、连续轮次、取消、删除/撤销、保存重开、模拟相机和释放 13 项通过，并确认 Runtime 进程未加载 WPF、UI 或旧 SDK。
- `tests/RuntimeEditorProductionProbe` 通过两个实际节点的参数绑定、首帧、Tab 复用、捕获/回执、换节点和关闭释放。最新读数为 ImageLoad 绑定 273ms、首帧 1005ms，BlobAnalysis 绑定 855ms、首帧 351ms，第 2/3 页首次切换 298/318ms，重复页切换 0/15/26ms。
- 全量 `dotnet build SunEyeVision.slnx -c Release --no-restore` 通过，0 错误。构建必须串行执行，不能让多个测试项目同时写正式 Release 目录。

实现调整：

- `BlobUiComposition` 的当前页组装调度从 `SystemIdle` 提升到 `ApplicationIdle`，确保宿主正常空闲泵返回前当前页已就绪。其他页面仍由后台队列一次性构建，重复切换复用已有页面树。
- 旧 UI 回归中仍有 6 个测试按“窗口显示后立即可调试”同步假设断言，现行窗口宿主实际有“组装中→就绪→可调试”状态。生产探针已覆盖该状态机，旧测试需要按新状态合同重写，不能为通过测试回退到窗口显示前同步构造。

仍未完成：

- VM 原厂 native 结果桥接只有托管 `SyncSelfRun` 入口证据，10 个结果成员不能猜测填充。
- VM 原厂同场景 UI 线程耗时、首次页装配和跨节点运行时释放顺序仍未动态确认。本方读数不能宣称 VM 等效。
- 20 个旧 Plugin.SDK 工具消费者未迁移，不能删除旧 SDK 或关闭迁移门禁。
- VS 外部工程附加正式宿主、真实硬件和人工验收仍未完成。

本批阶段提交前保持未提交的 CodeBuddy 工具预览 HTML、生成脚本和迁移清单变更隔离，不混入上述正式源码基线。


## 2026-11-08 REA Blob 原厂证据迁移补证

在 D:\MyWork\REA逆向测试\docs\joint-audit 发现并迁入此前被标为缺失的 8 份 Blob 原厂证据，索引与 SHA-256 见 docs/collaboration/evidence/Blob-原厂证据迁移清单-20261108.md。补证明确了 VM 参数窗口的 AlgorithmTabRoot 组装链、参数恢复与关闭入口、Blob 参数合同、4/8 连通参数入口、形状量化校验、BinaryImage/BlobImage 独立输出声明，以及 Result List 的 Angle/BlobAngle 映射。

补证没有证明填孔和阈值边界、4/8 连通的对角行为、形状最终比较公式、双图像像素与 Domain、长短轴半长/全长或动态原机差分。上述项继续保持未确认，不把 HALCON 结果冒充 VM 等价。下一步可依据已迁移的组装和参数证据继续代码落地，不再把“证据文件未迁入”作为阻塞理由。

## 2026-11-08 窗口就绪状态与引用清空语义收口

窗口宿主新增内部组装就绪任务。生产主窗仍先显示窗口，再后台绑定参数页，绑定失败会以失败任务和窗口状态报告，不回退到显示前同步构造。UI 测试等待真实就绪任务后再断言命令和参数视图。

Blob 引用输入修正 DeleteTextBox 的空文本清理路径。WPF 绑定在目标与源都为空时可能跳过 setter，现由逆向引用控件的 Delete 键直接调用同一 ReferenceFieldSession 清理路径，正式字段会提交 UnsetSetting，位置修正视图随之退订和隐藏。

验证结果：窗口、调试、Blob 三页、连续运行和释放相关 UI 回归 16/16 通过。ImageSource Runtime 31/31、BlobAnalysis Runtime 38/38、RuntimeEditorProductionProbe 和生产 ImageLoad→Blob 链继续通过。
