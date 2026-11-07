# AI 协作交接单 — Production EditorContext 首片

- 基线提交：a156cc314cc2981749638c7cbff198097539d994。
- 当前分支：codex/editor-context-production；唯一写入树 C:/Users/houzhongjie/.codex/worktrees/editor-context-production/SunEyeVision_V2。主目录产品、治理、A11 文件未改。
- 当前执行者：Codex 子线；临时提交与精确证据 SHA 见本树 artifacts/validation/editor-context-01/commit-binding.json。
- 任务目标：原 B02/E02/E03 的实际 ProtocolParse 私有草稿首消费者与原 E06/E07 数值 pending 必要守门；不是完整编辑系统验收。

## 修改文件及职责

1. src/Plugin.SDK/UI/Controls/NumericUpDown.cs：公开 TryValidatePendingEdit/TryCommitPendingEdit/DiscardPendingEdit；跟踪真实模板文本，不反射窥私有文本。检查当前 Culture、有限值、整数约束、范围，拒绝静默 clamp；错误失焦不重置文本。
2. src/Plugin.SDK/UI/Controls/ToolEditorCommit.cs：可选 IToolEditorDraftContext 附加到实际控件；FormalNode 用于作用域。先验证所有 numeric 再 flush，生产控件只写私有草稿；完整验证后只走 Context，跳过 legacy live query callback。
3. src/Plugin.SDK/UI/Controls/ToolEditorClose.cs：有草稿 Context 的关闭仅丢弃；不先提交。
4. src/Plugin.SDK/UI/Windows/DefaultDebugWindow.xaml.cs：实际 Confirm 调用 TryConfirm；提交管线先 ValidateParameters 后正式提交；Close 不二次持久化草稿。EditorControl 暴露壳内原控件以便 UI 接线。
5. src/UI/Services/Workflow/WorkflowExecutionManager.cs：Run 的 open-editor 选择限定当前文档、目标工作流及子流程的正式节点实例。
6. src/UI/Shell/Windows/MainWindowViewModel.cs：初始化实际编辑器时注入 draft node、typed draft parameters 和专用 data provider。
7. src/Workflow/NodeConfigurationEditHost.cs：增加不发布/不消费草稿的 Validate，以及正式节点实例 OwnsNode；复用现 OutputBindingChangeService/绑定编译及依赖图。原 Commit→Store.Publish(CAS)→Apply 是唯一正式写入口。
8. src/UI/Services/Editing/EditorDraftAdapter.cs：明确 ProtocolParse scalar CLR 槽通过 BindingClrAdapter/Port/ParameterEditSession 校验。InputText 的 String/Binary 解释仍用已有 BindingInputCatalog/编译器，不造 union；Outputs 用已有输出合同。
9. src/UI/Services/Editing/ProductionEditorContext.cs：拥有 Host 草稿及正式节点/版本、当前会话有效性回调。合法提交后继续编辑另一个私有副本；失败保留草稿。已发布后重绑异常只记 LastRefreshError，不把正式发布记失败；失效 Context 不重新活动、不继续界面通知。
10. src/UI/Shell/Windows/MainWindowProductionEditing.cs：真实 ProtocolParse 初始化工厂、Session/工作流/节点实例门、独占 DSQS callback、提交后设计态结果及全局绑定刷新。
11. tests/SunEyeVision.UI.Tests/ProductionEditorContextTests.cs：13 个新增定向事实测试，使用实际 ProtocolParse/DefaultDebugWindow.TryConfirm、双离屏窗口、SDK NumericUpDown 与 ConfigSetting 模板；另跑受影响原 scalar/scope 两项。
12. 本交接文件。

## 提交及生命周期顺序

WPF 控件 flush→PersistParameterBindings（CurrentNode 为私有 draft）→控件 ValidateParameters→显式 CLR 槽及 Host 私有 candidate 完整验证→Host.Commit→既有 Store.Publish/CAS→既有 Apply。生产分支不走 live query.CommitOutputConfiguration；专用 query callback 也回同一提交管线。Save/Run 的 ownsNode 使用 Context.FormalNode；取消/关闭使 Context 失活并抑制关闭持久化。

Host 保留原正式 node 对象，替换其 parameters/settings；旧参数引用不会被草稿继续编辑。相同 node 的旧草稿、其它 host/document 同 local ID、节点删建、关闭、Session 切换均拒绝。

## 实际构建与测试

全部输出位于 artifacts/validation/editor-context-01：validation-build.props/targets、offline.NuGet.Config、locks、obj、build/app（PackagingRoot）、testbin、fixtures、logs、results。19 项目闭包每项实际 MSBuild TargetPath/OutDir/obj/targets/PackagingRoot/lock 前检通过。项目本地 Contracts/Runtime/Kernel props 先 import 后 owned 覆盖；WPF 临时项目去 suffix 共用父项目 obj。

5 个浮动包按原 repo lock resolved 值固定到 owned targets；仅 owned 锁更新。离线还原及随后 strict restore 均成功，原解析包版本变化 0。vendor 来自主树 libs 的只读复制，原/owned SHA 绑定；不读主树 live 输出 DLL。

- build01：真实 1 ERROR/795 warnings，56.66 秒。EditorDraftAdapter 的 ParameterSetting 与 UI 同名 namespace 冲突；原日志保留。改完整 Contracts 类型名后再编译。
- build02：真实 0 ERROR/1007 warnings，39.37 秒。editor01：PID37344，自然退出 0，13 PASS/0 FAIL/0 skip，3 秒。
- build03：真实 0 ERROR/775 warnings，16.38 秒。editor02：PID47336，自然退出 0，14 PASS/0 FAIL/0 skip，1 秒。
- build04 最终：真实 0 ERROR/775 warnings，26.80 秒。editor03：PID55348，自然退出 0，15 PASS/0 FAIL/0 skip，2 秒。新增13+原受影响2。
- 首设施比较器把 C:/ 与 C:\ 斜杠差异误报 escaped；未启动 build/test，规范化路径后全部19前检通过，未盲重跑产品。
- PEReader 实读实际 UI.Tests AssemblyMetadata.HostDebugRoot 为本 owned build/app。SDK/Contracts/Workflow/HALCON app/test 关键输入 SHA 一致。最终源输入清单 source-input-build04.json、最终二进制输入 editor03-binary-input-manifest.json、实际命令 editor03-runtime-receipt.json、TRX results/editor03/editor03.trx。
- exact filter：FullyQualifiedName~SunEyeVision.UI.Tests.ProductionEditorContextTests|FullyQualifiedName=SunEyeVision.UI.Tests.ToolEditorCommitTests.Pending_text_is_committed_via_binding_but_invalid_text_is_retained|FullyQualifiedName=SunEyeVision.UI.Tests.ToolEditorCommitTests.Open_editor_scope_is_respected_and_invalid_editor_prevents_other_pending_publications。

## 原保存失败与验收边界

已实读 original-edit-save-evidence-boundary-20261024.json，SHA256 4AB1C02F5C9C7CCD175BA3D47C2D1D799845BDD6047EF863267E7DADC6E1C9F4。原 EDIT-SAVE-11/uia 9项7 PASS/2 FAIL 保留：E06 实际 Threshold pending181/173遗漏；E07 not-int 未阻主保存，文件仍重写（必须看 mtime）。本片真实模板控制验证 pending flush/invalid 保留，并有 gate fixture 断言旧文件 SHA/mtime 都不变；该 fixture 不是实际主窗 Save 或 UIA 端到端复测，不能覆盖原两 FAIL。

实际主保存与变量窗口 SaveRequested 共 SaveCurrentDocumentWithPrompt→TryValidateSolutionEditors→SolutionApplication.Save callback→TryCommitSolutionEditors→SaveSolutionData→写盘；不绕过失败门。原两个完整 UI 入口须由新集成 fixed 独测按受影响项复测，不重旧7或组件25。

## 尚未完成与已知边界

- 首消费者仅 ProtocolParse。其余默认/专业/无 UI 编辑器未挂 production Context，legacy fallback 不能当作 B02 完成。
- NodeOpenDecisionChain 仍在 Workflow 创建实际 WPF 控件；真实 UI 责任迁移是下一明确片。
- 未运行完整 MainWindow 构造/实际菜单保存/设备/手工 GUI/性能/Visual Studio；离屏 WPF 仅在自有定向测试进程内。完整 Release 与最终同一版本集中人工/VS 由父线集成后执行。
- Host 基线不会悄悄吸收外部正式 topology/binding 变化；已有其它 legacy 编辑器并行写正式配置会导致保草稿拒绝，需要重新建立编辑基线。未承诺所有旧编辑器/变量窗口的跨参与者原子保存。
- LastRefreshError 表示正式发布已接受后的视图错误；Context 重绑失败会失活，需关闭重开。未承诺任意插件回调副作用回滚。
- 无公共 AssetRef、Contracts、runtime、loader、serializer、ToolRegistry/PluginManager 改动；无新 catalog/manager/平行 Apply。

## 下一步

父线审查并合入本临时提交，在新 owned 集成 Release 中复测 E06/E07 及必要关联；随后继续其余编辑器/None 与 NodeOpen UI 责任迁移。原固定包与旧失败日志不覆写；本树 CURRENT 仅本地交接记录，不合主树。
