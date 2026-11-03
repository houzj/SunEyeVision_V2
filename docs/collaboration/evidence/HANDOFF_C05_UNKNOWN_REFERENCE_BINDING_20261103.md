# C05未知引用数值绑定确认链候选补充交接

- 基线提交：`f2e794533d8db8235505ca6b4ccf98d908d82843`；当前分支：`codex/c05-number-model-consumers-20261103`；执行者：`display_neutral_implementation`。
- 目标与边界：定位正式ROI六叶All圆环在常量切换后确认失败，消费公共唯一写者的数值投影及事件清理修正。只在现独立工作区接收固定源码和维护真实生产测试；未改ROI、Workflow、SDK校验、算法、Catalog或原O06。
- 修改与提交：`3f67e2915`仅增加真实`ToolEditorCommit.Walk/Bindings`确认前后诊断；公共`06b0bacf9`接为`5a1700137`。最后按`bce775fcf→cf7f50a73→dbd76045b→df62048ac→31d434cc0`完整接收测试门禁和生命周期修正，最终产品为`d5d3e56b7b31a1269420f96d213f226ab0c50e2e`。

## 已证问题与修正

原`f2e`完整Window轨`attempt-001`前8组通过，第9组实际运行形成2个圆环，但确认失败。诊断使用SDK真实遍历和绑定枚举，确认前0错误/6个dirty；确认后5个已加载但隐藏的`NumericControl.Text`空串向`SliderArrangeFloat.StartValue`转换失败，规则为`ConversionValidationRule`。随后直接`TryValidateEditor`和`TryCapture`仍成功，明确不是六叶身份或Blob捕获失败。原失败日志、实体诊断、源码快照完整保留。

公共修正仅在模型未知、引用态、非dirty且当前owned的数字槽恢复原物理DP对应的outer/inner target绑定，不将物理目标当作已知模型值或常量。进一步沿原owner值变化事件处理原生弹窗强制刷新后的隐藏校验，并对构造失败和Dispose对称退订。整数原物理0低于Minimum10仍保留物理值，不写DP、模型或正式字段；实际converter和文化格式用于目标恢复。真实空常量、坏raw、失效/退役输入不借此清错。

## 本线最终同版自测

- UI004：0错误、6553既有警告，1:53.02；全量full004：0错误、6540既有警告，0:39.45。均从本工作区真实构建，未复制主仓Release、现成程序集或厂商库。
- 公共`C05NumericModel-20261103/attempt-001`：10组；含确切inner空串来源、序列及整数真实原生弹窗、无值/坏raw、旧事件退役。`exit=0`、payload/identity门禁均true，实加载2项SHA匹配。
- 真实`C05NumberModels/attempt-004`：6组；初始化及显式回读、引用与模型分离、raw保留、无值/异常解绑受保护常量输入、重入换根、实际确认取消历史/Copy/保存重开。实加载15项SHA匹配。
- 完整`BlobRoiInheritance/attempt-003`，筛选`Window`：11组全部通过，保留原场景与断言，贯通原失败六叶All多圆环、单常量不广播、恢复All、确认保存重开、逐项圆环和多项常量显式修复。实加载15项SHA匹配。
- 所有原句柄自然终态，无新源前不重复构建。22项源码raw SHA与Git内容对象、构建日志/回执/实际加载身份见同目录`C05_UNKNOWN_REFERENCE_BINDING_CANDIDATE_20261103.json`。

## 版本边界、未做与下一步

`5a1700137`的UI003/full003及Window11/C056是独立中间版本，未移签到最终DLL；`f2e`失败原件不覆盖。最终窄修正没有无差别重跑原B08/Numeric/Param轨，主线按影响决定正式相称回归。C06双端未扩展；本Window不替原厂全域、自然焦点、剪贴板、连续会话或ROI图文。主仓071图文All在最后事件场景watchdog超时，仍由主线另行定位，未宣称44项通过。

人工验收：本批未启动VS或用户桌面App，设计时用户另验。主仓正式Release与人工验收均不借本候选成绩。主线接管正式源码集成、Catalog/O06及同版验证；本工作区产品和测试保持冻结。原`CURRENT_TASK.md`已有脏内容按主线要求保留，未覆盖或提交。
