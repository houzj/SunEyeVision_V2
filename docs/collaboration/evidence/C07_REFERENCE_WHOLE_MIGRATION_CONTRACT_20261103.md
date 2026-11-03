# C07/B08 完整引用控件迁移批

基线：主仓 C06 已正式实施自测闭口，b8c96fc46；正式产物787e/570项另保。依据 B08_SOURCE_CAPTION_ORIGINAL_20261103.md、C07_REFERENCE_ORIGINAL_SUPPLEMENT_20261103.md（原文 SHA C8C59305AEB7611882229FD38ADA71B2AAAF21FC21B4BD56AE65AFBA4017D454）及当前完整生产链。补证原文相对链接按逆向工作区 docs/joint-audit 解析。本批不启动原机，不重做已有引用/树/事务/身份，不迁移原全局业务服务。

## 整体迁移及复用

1. 普通来源按原 VmIO.ModulePathName，以“模块编号 名称.所选 IO 本地化名称”显示；普通数值宽度等使用叶自身名称，不重复拼图像组路径。仅改变显示投影，NodeId/Scope/PortId/MemberFieldPath/Type/Storage/Selection 保留结构化身份和既有验证，不能按新标签解析来源。已有声明树层级与资格复用，不凭字符串压平树或虚造未声明来源。
2. 原普通 ButtonSelecter 首次选源追加 `[]`，无 label 与 `[` 之间空格，scalar 也不省略。固定/模块/变量索引使用既有真实语义。切源保已有索引后缀，不能因新源为scalar静默改成ALL；若当前宿主形状/基数不接纳则明确拒绝并保原子状态。原 SDK 单值[0]样本不推广为[1]成功；当前基础不支持的形状不扩写SDK，列受保护未实现，不假造绑定成功。无效 raw 不丢、来源名不可编辑成另一身份。
3. 原 ValueBase.SubData（Integer/Float/Boolean）和 String.SubData 初始 ReadOnlyA=true，模板/FocusedCommand尊重该条件；当前 BlobRunUi 强制 EditableIndex=true 是确定差异，本批去除。按调用家族区分组合 IO 的可编辑索引，不全局禁用组合能力，不按来源数量猜只读。原UI不提供的数值索引键入/焦点弹窗，不再以本方多出的入口作原厂交互验收；已有固定/动态索引的正式模型及执行链继续通过实际非视觉参数命令验证，并明确与界面入口区分，不保旧错误交互作兼容。
4. IMAGE 输入下拉按原可订阅、可见 IMAGE 组合父节点命名，省略数据叶和方括号；宿主仍以已声明 resource 成员路径取得实际图像，这是必要非视觉适配，不能改掉正式payload路径或混入宽度叶。列表刷新/更换使用现有完整快照和原绑定恢复，不在程序刷新中发用户提案；原 EnumerationG 清内部临时值再绑定回读，与正式身份/图像lease分开。
5. 原来源更名与编辑中刷新完整时序未证，不猜“总清空/总保留”。复用现有稳定身份、代次和 raw 保护：更名只刷新显示，不按caption重绑；pending前缀失配应显式拒绝且保raw/原正式值，取消或重新选源后可恢复。此为既有宿主并发保护，不称VM已证行为。原 SetModuleParamBindOperation失败会无trigger清展示，当前host事务/草稿拒绝须保原正式身份和错误状态，不为复制原全局副作用清正式引用；差异在最终回执列明。
6. 双Popup、来源/索引树、展开/变量模态、owner与请求代次、确认取消/撤销/复制/保存重开/实际执行/关闭清理沿已有链复用；修改涉及这些链的完整入口及退出，不按按钮交付。C05/C06的模型/回读/组事务、资源所有权不另造第二套。

## 文件边界与两线

- 公共线（唯一写Plugin.Wpf）：ReferenceFieldSession、DeclaredFieldEditor.ImageSource及确需的既有引用投影/原件接缝；新的C07完整公共测试与runner。必要API变更先报主线，不动SDK/Workflow，也不复制业务全局服务。
- 消费线（唯一写宿主/Blob及真实测试）：DeclaredReferenceCandidateBuilder、RuntimeNodeConfigurationControl的格式投影、BlobRunUi去错误强制可编辑；必要真实消费者测试及现runner。先按上述固定语义准备，不双写公共源。
- 主线：合同、原证归档、Catalog/原O06/全清单与主仓集成正式验收。使用既有两工作树，新分支从本合同固定提交；不创建新worktree。固定可审核源批后各线集中build/test，候选不冒充主仓。

## 一组验收与闭口

同一真实控件覆盖：普通标量/序列/变量的原对应文字、组合固定/动态索引及来源切换的后缀，普通数值原只读门禁；图像父组合下拉和实际resource身份；真实原生WPF来源/索引事件、清空/取消、组拒绝与raw；候选更名/同名新ID/类型或成员变化、刷新与代次；真实窗口未失焦确认、Undo/Redo、复制/文件重开及同次执行值；关闭/重绑迟到请求不写新owner。测试按真实层区分UI准入、非视觉参数命令与执行，不借反射改状态伪造可用UI，不弱化原断言换取通过。

复用既有B08/来源资格/展开与变量/混合窗/ROI与图文消费者证据，按新改动运行相称完整批，不做九字段×全部事件的盲目组合扩测或重复无关导出测试。主仓同一正式Release标准UI/full、实际加载与封包一致后记本控件实施闭口。实际VS/自然键鼠/统一人工另记。原A00–A23、Catalog28/17组及未做范围全部保留：Group暂停、未知producer/动态输出、偏好持久化、连续会话及算法等缺基础须明确保护和替换位置，不能宣称功能已实现，也不为它们提前扩写其他基础。
## 已核必要接缝（本批统一裁决）

- 原 GetFrontFilterTree 按 ComType 提供资格，ReadOnly 仅约束文本/焦点编辑。来源与变量的既有声明/selection资格不能因 EditableIndex=false 删除序列；本批将这两个职责解耦，不能为了恢复数值只读而隐藏原合法来源。
- IMAGE 父组合标签由宿主既有 imageSources 回调按声明父级投影并保正式资源 MemberPath，公共继续消费所给 Label，不猜中文或“数据”后缀。
- 现有 DeclaredSettingsView.TrySetFields 已是非视觉参数命令，但 paired 提案一律拒绝。允许最窄内部接入：单个paired ReferenceSetting/VariableSetting交原 NativeRangePresenter.TryAcceptEndpoint，完整经过原 BeginRange/SubmitRange、host校验/原子接纳/模型版本/退役；不加public/SDK/Workflow签名，不直接SubmitCore绕过range。多端混合/常量仍原范围完整路径或明确拒绝，不能半写。路由须在同Dispatcher、CurrentGroup/非重入与字段快照核对后交原范围取得唯一事务，不能在_submitting已占用后再BeginRange，也不能删除重入保护。
- 保留不支持形状的明确拒绝：旧[固定/动态]切scalar不得静默变ALL；类型投影/变量构造的异常须在现有提案错误边界转拒绝，不可成为WPF未处理异常，不伪装绑定成功。