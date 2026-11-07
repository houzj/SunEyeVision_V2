# 全工具绑定失效隔离修复交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm；执行者Codex，单写入，保留并存修改，无提交推送。
- 任务目标：失效绑定只拦截当前节点及依赖下游，上游和无关分支继续；所有工具明确显示失效项，不仅协议组装。
- 允许修改目录：Workflow执行／依赖／校验、Plugin.SDK公共绑定与调试控件、UI运行管理器、必要的工具输入描述符及本地文档。不改算法、标定和设备。
- 已修改文件：WorkflowDefinitionService、OutputBindingChangeService、WorkflowExecutionDependencyGraph、WorkflowExecutionEngine、SolutionExecutionDependencyGraph；SDK ToolExecutor、IBindingInputProvider、BindingInputCatalog、ParamBindingBase、ParamBinding、ToolEditorCommit、ToolDebugControlBase；新增BindingIssueAdorner.cs、ToolDebugControlBase.BindingIssues.cs；UI WorkflowExecutionManager；ProtocolAssemble/ProtocolParse/SendData参数输入描述符。
- 已完成：结构校验与普通输入校验分离。冻结计划保留节点输入错误及有效画布依赖，失效绑定不构造错误依赖。就绪时问题节点发布失败和GenericToolResults诊断，不调用算法或通讯动作；失败下游向多层传递Skipped，无关分支继续。总体失败状态保留。模式未启用的输入、已删除动态行按活动输入规则排除；真实循环、重复节点和坏画布连接仍为结构失败。
- 统一显示：所有ToolDebugControlBase窗口加入可滚动错误摘要，列出输入名称／稳定键／原因；共享ParamBindingBase错误边框／Tooltip，空ParamBinding用Adorner绘制水印，编辑时让出输入位置。水印从未写入TextBox.Text、BindingDisplayText或BindingSource。状态不作为WPF Validation错误，避免编辑提交重新整轮拦截。独立AdornerDecorator保证插件窗口有绘制层，主题使用已有ErrorBrush，不改主题资源。旧Tooltip注入清理，错误消除后恢复原提示。
- 动态行：描述符新增Owner/MemberPath初始化属性，不改变位置构造参数或原构造签名；组装、发送、协议解析、脚本／子程序输入和内部输出／显示行按模型对象及成员定位，多个空值不互相匹配。图像、ROI、专用选择器等即使没有共享控件，也显示统一输入摘要，不静默空白。
- 运行全部：解决方案图保留合法依赖，失效全局来源不再阻断整批；前序流程部分失败不直接跳过整个后继流程。成功生产者仍可发布本轮值，失败／跳过／失效或输出解析失败的值从本轮表移除。缺值消费者在节点执行时报错并阻断依赖下游，其他分支继续。未写设计期全局值，不允许用历史输出兜底。
- 兼容边界：保留绑定来源、稳定身份、类型与上游限制，不自动按同名回绑。旧EnsureExecutable、三参数图编译及原定义校验重载仍在。调试执行保留SDK失效输入检查，错误增加名称／稳定键。计划快照和设计模型分离，未实现运行中热替换配置。本次不修复具体来源身份为何被删除／改变。
- 已执行构建与测试：首轮全量Release1278警告0错误（91.95秒），后续全量1263警告0错误（63.22秒），独立UI1066警告0错误（14.15秒）；补齐定义校验分离后全量1159警告0错误（71.97秒）。最终全量1093警告0错误（64.66秒），独立UI1066警告0错误（15.48秒）。SDK/Workflow/UI及ProtocolAssemble/ProtocolParse/SendData主DLL发布哈希与Release编译中间DLL一致；diff检查通过，仅换行提示，标定无diff。点线插件目录SDK/HalAdapter副本按现有构建规则刷新，不改加载／发布规则。遵前序用户要求未新增／运行测试，未启动程序／设计器或操作设备。
- 发布补充：点线插件Release单独构建0警告0错误（4.75秒），SDK/HalAdapter依赖副本最终哈希与宿主一致。
- 人工验收结果：暂无。需在原方案确认上游成功、问题节点失败、多层下游跳过、无关分支成功，确认动态行重排／重新绑定／错误消除后提示恢复及打开编辑器不改变参数；跨流程需确认成功分支全局值可用而失败分支不沿用旧值。
- 已知风险或禁止改动：构建不等于运行验收；错误摘要加入调试内容顶部会占少量高度，最大100像素可滚动，实际布局待人工确认。不得关闭校验、覆盖旧绑定或改动标定。工作流仍汇总失败，不承诺连续运行忽略既有停止策略。
- 下一步：原方案人工验收；Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe已更新。不把历史身份溯源或运行验收写成已完成。
