# 编辑窗口与事务链重叠交接

核对日期：2026-11-03。主仓基线34002fbd5，原窗口产品bb7b461d1。工作流独立候选platform-tool-edit-visual-20261102尚未集成，本记录不替其签署验收。

## 决定与唯一负责人

工作流线负责唯一编辑事务、中立完成事件及窗口宿主链。本线继续公共显示视口、原结果图文及Blob接入；不在同批重复修改窗口/事务共享接缝。最终只保留一套BaseWindow+NativeToolEditorFrame窗口，不保留NativeRuntimeEditorWindow和RuntimeToolEditorWindow双轨。双方继续独立开发，当前不要求停工或立即合并。

主仓文件：src/UI/Services/Editing/Nodes/NativeRuntimeEditorWindow.cs、NodeOpenDecisionChain.cs、RuntimeNodeConfigurationControl.cs、src/UI/Shell/Windows/MainWindowRuntimeEditing.cs。工作流候选窗口同Nodes目录的RuntimeToolEditorWindow.cs；事务移入Plugin.Editing.Wpf的候选实施尚需独立整链核验。

## 已核具体差异与合并条件

- 两方均复用Plugin.Wpf BaseWindow与NativeToolEditorFrame，不能因名称不同保留两套生产窗口。主仓中文420×500/英文680宽、运行页重置显隐、根流程/HostAvailability门禁及Owner应按原证和实际消费者统一。
- 工作流候选RunContinuousAsync当前在WPF窗口内Task.Delay(500)轮询执行。原BottomCommand是模块结果回调及根流程门禁；该定时循环不属于已证等价迁移，也不符合UI不承担调度边界。已要求移除或保护禁用；完整连续会话归非视觉层，完成取消、停止、关闭与根流程回归后才可开放。主仓连续、停止及Reset仍明确未实现保护，不以本次交接宣布完成。
- 中立事件应替换现行借用SDK路由事件的非视觉接缝；必须同时保留成功/失败、DeclaredOutputSnapshot Retain/Dispose、同次图文发布、草稿重绑、旧Owner、关闭及迟到结果保护，不能只换事件类型。
- 唯一DocumentEditSession发布与去旧方向接受；实际合并仍核完整生产消费者、无草稿回退退出、确认取消/历史/保存重开、运行中关闭、资源与错误清理。正确现有能力按行为复用。

原证及设计：BLOB_NATIVE_FOOTER_WHOLE_DESIGN_20261102.md（其候选历史状态以较新正式回执为准）；原Blob XML2723–2731、Group2938–2981、ParamsTabUserControl95–140、原命令模板159–230与BottomCommand.cs。主仓最近同版正式六轨回执BLOB_RECTANGLE_OUTPUT_FORMAL_20261102.json，不代替工作流候选成绩或未来集成验证。

本次已通过send_message_to_thread成功回复工作流01a176b8-278f-77f3-8093-20a80f11403b，获用户“工作流发来的信息先处理一下”授权；未修改对方文件，未启动VM/SDK，未替换任何正式产物。