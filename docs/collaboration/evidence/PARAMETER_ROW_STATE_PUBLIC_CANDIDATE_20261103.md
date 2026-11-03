# 参数行等待选源与真实错误状态候选交接（2026-11-03）

本公共候选完成实施自测，主仓正式验证另记。仅修参数行状态投影，不改控件模板、参数接纳、必填门禁、焦点/选中、工作流或算法；未启动原VM。

## 基线、证据与根因

- 工作区：`D:/MyWork/line-aiproject/blob-reference-chain-20261101`，既有分支`codex/parameter-evidence-public-20261103`；基线`a8f08e4d4`。
- 产品提交及真实构建源：`a92dfe3a61ee36bbd3ab21076f6a5a3ab620e5bb`。测试刷新顺序修正`6f9ae1bf0`；最后配对读错误恢复顺序修正随本交接提交，不重建产品。
- 精确回执：[PARAMETER_ROW_STATE_PUBLIC_CANDIDATE_20261103.json](PARAMETER_ROW_STATE_PUBLIC_CANDIDATE_20261103.json)，包含最终原回执、24份证据文件SHA、原件五源SHA、最终源码SHA及完整载荷文件集合。

复用既有导出源核对了完整状态链：

1. `Apps.XmlUI/resourcedictionaries/globalresources.xaml:22`的ParamItemTemplate仅将MyItemState经ItemCheckConverter绑定到整行BorderBrush；原转换器将GetParamErro映射黄、SetParamErro映射红，其余透明。
2. `Apps.XmlParser.ParamTab/NameValueItem.cs:77/340`保存独立状态且默认Normal；SetParamUnitByZmq:933先Normal，960/966在真实写入异常时转SetParamErro。
3. `ValueBase.cs:27–72`的IsSubA切换不将等待选源直接设为写错误。`paramusercontroldatatemplate.xaml:550–551`的Integer_Sub绑定IsSubA；267–269的3px主题高亮由独立IsSelectedSubscribeParam控制，不等于切绑定或校验失败。

原件路径和字节SHA在JSON的preValidationFreeze.originalSources中固定；只读现有证据，没有启动SDK或增加逆向取证。

本方之前把HasInvalidInput作为WriteError的兜底。刚切绑定仍持有已接纳常量且尚未选源时，MissingReference正确阻止确认，但被错误汇入整行红框。组件基线attempt-002在旧e567真实载荷通过原模式按钮复现`WriteError/#FFFF0000`；三份投影源与a8f基线规范Git内容相同，不靠截图推断。

## 完整修正

NativeParameterRow.ProjectState统一单字段、配对/range和对话组的行状态：显式EditError优先红，SourceError黄，其余真实invalid红；MissingReference等待态不再被兜底当作失败写入。DeclaredFieldEditor和DeclaredFieldsView接同一投影入口。

DeclaredFieldState.HasInvalidInput及正式参数保持不变。等待态仍标记必填未完成、阻止确认；单纯切模式不写参数，合法选源后恢复正常，真正拒绝、坏raw和读取错误仍分别显示原错误框。原独立订阅选中高亮未改。Catalog只同步本次三源的27处当前路径哈希引用，original身份和完整历史/receipt快照相对a8f一致。

改动产品：Adapters/Fields/NativeParameterRows.cs、Fields/DeclaredFieldEditor.cs、Fields/DeclaredFieldsView.cs及Catalog/controls.json。新增组件ParameterRowStateCases.cs与独立runner；没有消费者、主仓或混合CURRENT写入。

## 标准候选构建与17组验证

程序为本树自身`Release/SunEyeVision_v1.0.0/SunEyeVision`，使用真实构建和主仓HalconSourceRoot部署，没有复制现成Release。

|构建|命令|自然终态|
|---|---|---|
|UI001|`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -p:HalconSourceRoot=D:/MyWork/SunEyeVision_V2/libs/halcon/`|exit0；6553警告/0错误；1:35.38|
|full001|`dotnet build SunEyeVision.slnx -c Release --no-restore -p:HalconSourceRoot=D:/MyWork/SunEyeVision_V2/libs/halcon/`|exit0；6555警告/0错误；1:00.16|

|轨道|实际范围|终态与加载|
|---|---|---|
|ParameterRowState attempt-005|整数单字段、浮点单字段、整数配对三组完整状态链，含配对右端|3组exit0，2DLL路径SHA核符|
|C07Reference attempt-006|既有完整公共引用整轨|10组exit0，2DLL路径SHA核符|
|WindowPresentation attempt-013|`O06_WINDOW_CASE_FILTER=原参数宿主`既有四组|4组exit0，4DLL路径SHA核符；filter在finally清除|

新组件经真实模板模式按钮覆盖常量正常→绑定空窗透明且门禁保留→实际焦点/行选中不误红→合法选源→读取黄框→选源拒绝/独立读写错红优先→恢复→无效数字文本红框→读错误恢复及合法提交→配对右端等待/显式错误。没有直接给Border涂色或修改业务门禁来通过测试。

三轨实际测试源SHA与运行快照一致。buildSource为a92；后续仅测试源码变化，未替换DLL。201项payload逐项核符，202个完整文件集合/内容及封包在测试前后不变；SHA256：`EC8DE9E04231B70BFE2056414D721F6F99B43FF6DF67F2CB0FB3393A86CD449C`。源、命令、起止时间、原日志、每个加载路径/SHA及前后清单见JSON和本树`Release/_validation/parameter-row-state-20261103/`。

## 保留失败与边界

- attempt-001为新夹具错误读取不存在的`_states`，修为真实`_fieldStates`；不算产品问题。
- attempt-002是旧载荷真实红框基线复现，不把修正版通过移签旧版。
- attempt-003已通过切换/等待/选源，但夹具误以为来源Refresh成功后仍保留旧可用性错误；改为刷新后重新安装独立读失败，再验证红优先与清红留黄。
- attempt-004两单字段已通过，paired因夹具仍保留独立SourceError而被正确阻挡提交；夹具改为显式恢复该读错误后再提交合法raw。两个后续调整均仅测试，无产品修改或重建；原失败全部保留。

这是公共17组候选，不替代主仓正式、实际VS、自然键鼠/系统焦点及用户人工验收。没有运行FieldEditing整轨；主线已知VerifyReferenceComposition Enter-focus失败以及其后的主窗打开命令不在本批通过声明内。本批不声称修复一般焦点、开窗性能或剩余原件结构。

主线下一步接产品与最终组件夹具，在同一正式Release验证并维护原主方案。剩余证据归档/结构批仍待统一决定，未混入本次实施。
