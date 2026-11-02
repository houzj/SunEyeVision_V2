# 真实三页纯UI批两线最小合同（2026-11-03）

主设计BLOB_REAL_THREE_PAGE_UI_WHOLE_DESIGN_20261103.md持续有效。用户持续多线授权下，两条实际开发线同批并行，不另起业务/平台目标；取证和只读评审不算开发线。

|唯一写者|独立既有工作区/分支|完整职责|
|---|---|---|
|display_neutral_implementation|blob-display-neutral-20261103，codex/blob-three-page-ui-preview-20261103，基线d87a5577b|Blob.Wpf真实三页/窗口私有BlobUiPreviewScope/设计与运行同owner；三页整体专轨、O06现有验证与runner消费者。不得写Plugin.Wpf公共源。|
|ui_preview_components|blob-reference-chain-20261101，codex/blob-ui-preview-components-20261103，基线d87a5577b+41835d16d WIP接续|Plugin.Wpf阈值/判定/范围/条件/缺值显示预览组合及实际必要字段UI退役接口；独立公共组合专轨。不得写Blob.Wpf、现有O06验证/主runner、Catalog/O06/主任务。|
|主线|D:/MyWork/SunEyeVision_V2|统一合同/证据、原方案、Catalog和集中集成正式验收，不和开发线并发改产品。|

原Blob线两份公共WIP（Presenters/Numeric/NativeUiDraftRange.cs、Presenters/Parameters/NativeJudgmentUiDraftControls.cs）已精确接收并逐字节核对，固定41835d16d。它是未构建未验的交接，不计完成；Blob线收到确认后仅恢复这两文件至自己的HEAD，正式消费公共线后续已固定提交，不各写一套。

## 公共API冻结

- `NativeThresholdUiDraftControls(bool externalSingleFields=false)`。六key严格原XML：BinaryImage、SingleThreshold、DoubleThreshold、AutoThreshold、FixedSoftThreshold、RelativeSoftThreshold；`SelectedMode`、`IsSingleMode`、`TrySelectMode(string)`、`SingleVisibilitySource`（真实FrameworkElement.Visibility用于现行FieldVisibilitySources）；`Changed`、`HasUiChanges`、`HasPendingEdits`、`HasInvalidInput`、`RetireInteractions()`、`Dispose()`。
- `ConfigureSingleProjection(Func<(ParameterSetting Polarity, ParameterSetting Low, string? LowPendingRaw)> read)`。Blob把真实Binary/BinaryInv只读映为bright/dark，不回写；只在首次或预览恢复基线且未改时seed，seed更新UI基线，不把原Single pending永久误标为新增预览修改。Single隐藏本组内重复极性/low，保留外部真实字段对象；五模式预览不碰正式草稿。共有预览字段按原留值。
- `NativeJudgmentUiDraftControls`：Changed、HasUiChanges、HasPendingEdits、HasInvalidInput、RetireInteractions()/Dispose()；数量low minimum1、高端minimum0，所有端默认/增量/精度逐XML，不拿本方12算法规则映射15原组。
- `NativeUiDraftRange`保留现真实用途，新增每端必要min/increment/精度配置；Changed、HasChanges、HasPendingEdits、HasInvalidInput、RetireInteractions()/Dispose()。消费者同步到此明确名称，不另造同义兼容接口。
- `NativeUiConditionalFields`及`NativeDisplayUiDraftRows`复用现Changed/HasUiChanges；如缺pending或关闭接口只补本UI生命周期所需；无值对象不给假图，默认true不是自动有修改。
- `RetireInteractions()`只关原来源树/Popup/样式辅助窗并拒绝旧延迟回调，保raw/pending/已接受UI状态、允许后续正常重开。先复用现IParameterVisibility/原字段关闭方法；确缺只补当前组件方法，不新建SDK/Workflow合同或全局服务。Disposed/hidden旧事件不能写新owner；清理尝试全部资源并记录异常。

Blob线负责上下文门禁与真实Single对象可见性/既有字段绑定，公共线不引用Blob.Runtime或SDK宿主。普通导航/Advanced展开不算业务变化；真正UI-only变化/未支持选择由Blob集中拒绝接纳。未支持基线不误封未交互Single。初始UI投影不复制业务模型，不重置真实字段或反灌预览。

构造和控件名不作为闭口。公共线先给最小可消费源码提交（必须实现语义、不交空壳冒充完成），Blob线在其工作区接收后继续集中构建；两线各自真实restore/build/test。共享线不碰主runner，可写独立公共控件检查脚本/C#专轨及自身验证记录。Blob线负责现验证消费者六key更新，不保短key兼容别名。最终完整UI批按同一真实窗96+8矩阵与门禁/生命周期核收，候选不是主仓正式，实际VS/人工另记。