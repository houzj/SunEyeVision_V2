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

