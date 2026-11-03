# Blob参数公共控件职责命名归一候选交接（2026-11-03）

本公共候选完成实施自测；主仓正式集成、实际VS设计器及用户人工验收分别记录。本交接只新增证据，不修改产品、不重建、不刷新历史正式回执。

## 基线、职责与来源

- 基线：`8962f6bdbc74e88637fecd3d8bd01677849134b1`。
- 分支：`codex/parameter-neutral-public-20261103`；执行者：Codex公共实施线。
- 工作区：`D:/MyWork/line-aiproject/blob-reference-chain-20261101`。
- 公共实施：`a4300d81e`；消费者测试：`e8028cdc0`，本树互接提交`7377aa46f`；完整修正版产品与构建源：`e567eed25fda33327eebc0b3366e35d34157c4da`。
- 本线产品写入范围为Plugin.Wpf及本批专用analysis脚本；消费者既有测试由消费线同步。混合CURRENT和原未知证据保留，未纳入本交接提交。
- 实施合同：[BLOB_PARAMETER_NEUTRAL_STRUCTURE_CONTRACT_20261103.md](BLOB_PARAMETER_NEUTRAL_STRUCTURE_CONTRACT_20261103.md)。
- 精确回执：[PARAMETER_NEUTRAL_PUBLIC_CANDIDATE_20261103.json](PARAMETER_NEUTRAL_PUBLIC_CANDIDATE_20261103.json)，内嵌最终候选原回执、202文件集合、6条实际加载记录、22份证据/来源文件SHA；JSON SHA256：`C016EAA5E53AD6D715BF560BF6328DADFA190E14F5F7A2B68E4761341E0ED17B`。

## 实施结果

整批迁移参数宿主、行与编辑器、ROI坐标数值、UpDown、字段基础控件及对应主题/资源，公共C#/XAML/URI/资源键与当前Catalog同步。Numeric.Arranged、Coordinates、UpDown及两颜色选择器分别保留；不变控件行为、参数身份、算法、存储及工作流合同，不新增功能或旧入口。

原身份和完整历史/receipt快照相对基线保持不变；仅entries/current对象更新当前路径/哈希。静态核对260个XAML、563处pack URI、945个当前Catalog文件SHA，无本批新断链。四个FieldPrimitives厂商前缀产品类型同批中立；UpDown两份真实主题随家族迁移。完整迁移及逐文件对应保存在候选目录`structure-map.json`，最终静态检查为`structure-check-final.json`；其路径与SHA均已固定到JSON证据。

## 真实构建与候选验证

程序根为本树自身`Release/SunEyeVision_v1.0.0/SunEyeVision`，真实标准部署使用主仓HalconSourceRoot；没有复制现成Release或为凑文件数补库。

|构建|命令|终态|
|---|---|---|
|UI002|`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -p:HalconSourceRoot=D:/MyWork/SunEyeVision_V2/libs/halcon/`|exit0；6553警告/0错误；2:20.02|
|full001|`dotnet build SunEyeVision.slnx -c Release --no-restore -p:HalconSourceRoot=D:/MyWork/SunEyeVision_V2/libs/halcon/`|exit0；6541警告/0错误；1:49.27|

两构建输入均为e567；命令、起止时间及原日志SHA见JSON。候选目录：`Release/_validation/parameter-neutral-public-20261103/`。

|整轨|入口与范围|终态及实际加载|
|---|---|---|
|公共引用|`Run-C07ReferenceValidation.ps1`，attempt-005|10组；自然exit0；Contracts/Wpf两DLL路径SHA一致|
|窗口资源|`Run-S2ReleaseValidation.ps1 -Track WindowPresentation -Stage S4 -ValidationScope Candidate -TimeoutSeconds 900`，attempt-012|限定1组“中立显示资源精确Aero2依赖与原箭头入口”；自然exit0；Wpf/UI/SDK/Contracts四DLL路径SHA一致|

窗口轨仅设置上述`O06_WINDOW_CASE_FILTER`，运行后finally清除。没有把筛选1组称作窗口整轨全部通过；消费者B08/ROI/图文结果由消费线单独交接，不拼入本公共11组。

公共Wpf实际加载SHA：`8F69427203002EAC6618553E0405165B27A9580E98E0504EE3DE13FFDB85B206`。两轨实际路径、每DLL SHA、测试源SHA、原receipt与日志SHA均在JSON逐项保留。

候选封包实际为201项entries，完整文件202个（包含release-payload.json），不是主仓历史570项。201项逐一核符，完整文件集合及内容在测试前后不变；封包sourceCommit始终e567，SHA256：`B8CB52984AA0529612D71524D05F86470BEE89848324186BE7F2FC101ABEDE19`。

## 失败、预存限制与未做范围

首UI001输入为7377，2706警告/5错误，39.53秒自然exit1：NativeRoiSession:109、117–120仍用相对`Native.Dependencies.Apps.XmlParser.ParamTab`。e567集中补齐相对Native映射、对应生产引用和静态门禁。UI001的命令、时间、日志与SHA完整保留，未继续在失败版本跑测试，也不将修正后的成功移签首版本。

14条预存URI逐行固定于JSON：UserTempConverter:22及两个原HikCover转换器:18的三条pending资源预期查找；StyleHelper:19的一条动态URI；Combo.xaml:694、699、703、707、711、716、779、783、786、789的十条缺图片引用。保原路径、迁后对应和分类，不伪造资源或宣称从未可能调用。

另三项预存当前Catalog缺失路径为`Controls/Native/Input/VMControls.WPF/VMControls.WPF/`下CrossConfig.xaml.pending、CrossConfig.xaml.cs.pending、CrossData.cs.pending；它们与上述三条pending URI不是同一组。基线中即无对应文件，本批保留原缺失事实，不计新断链。

尚余默认编译91cs/7XAML、默认打包553资产和实际521份pending，不因本批候选通过称全产品树中立。原491是限定关键词子集。实际VS、自然键鼠/系统焦点及用户统一人工未执行；未启动原VM，也未扩算法、Group或原厂业务服务。

## 后续决策与交接出口

本批源码与候选已可供主线集中集成、同版正式验证；主线维护原主方案与任务账本。当前正式91组未在本公共候选回执中预签。

后续完整结构批只读评估已交主线：84个未见家族外直接消费者的Input VMControls/Release源、7份相关XAML及无实际消费者的ExportMVDCode，可优先评估原字节退至Catalog证据，避免为改名保留未消费工作流/导出依赖；导出ABI字符串仅作原身份保留，不新接PInvoke。六份实际ColorResourceBinding、Utility三源、Interface、ExternalCall沿真实消费者直接职责迁名并保持行为。

归档活动`.cs/.xaml`必须加`.source`等非编译后缀或明确排除，不能在Catalog内原扩展名移动后被SDK默认重新编译。pending可按原相对路径原字节迁至Catalog证据区，建立旧路径/新位置/SHA/未接入状态索引；不删除、不中立重写快照字节、不恢复为产品Page或运行时回退。553资产的动态消费和三条活动pending资源预期URI须逐项审清，再由主线决定整族归档或职责迁名；下一产品批尚未实施。
