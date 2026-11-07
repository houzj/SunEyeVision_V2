# 根参数Runtime正式消费者整链交接（2026-10-28）

- 基线提交：9615bdbe。
- 当前分支：codex/image-source-root-bindings。
- 当前执行者：source_editor；独立workflow-overview worktree唯一写者。
- 任务目标：让根运行参数代理消费现有Runtime声明、强设置及语义订阅，保完整历史/保存/执行链。
- 允许修改目录：指定根参数目录/适配/实际视图消费者、RootResourceProductionTests及本交接；未改Plugin.Wpf共享合同、Blob业务、结果宿主或项目引用。

## 设计复核和实施

1. RootParameterCatalog先分支RuntimeConfiguration，读取SettingsSchema与Schema对应Presentation及BindingTargets，以稳定Port.Id作代理字段和存储路径；EditModes.None不曝光。仅集合/资源常量缺专用编辑器者明确不可选，正式资源引用可选，不从CLR属性推断。
2. RootResourceProxyBinding增加正式Runtime目标路径。强设置常量通过WithFieldValues生成新的私有草稿配置，引用仍唯一写ParamSettings的序列化OutputReference，不复制强设置常量或运行资源；非设置输入常量按Port冻结并持既有输入设置，禁止声明不允许的模式。Unset移除订阅，设置回到自身原有literal。
3. 复用RootWorkflowConfigurationContext既有Resource适配分支，无需改其事务/历史机制：Read/Write、ValidateReference临时写入后ValidateBatch并finally恢复、Remove恢复opening、Prepare/Acknowledge/Cancel/Undo/Redo保持同一参与者。Runtime的CaptureRestore恢复配置与当前字段设置；Remove仅恢复目标字段的原强设置及订阅，保留同节点其他编辑。
4. RootWorkflowReferencePicker对Runtime不套旧SDK资源完整类型预过滤；保正式候选/成员路径，正确性由实际ParameterBindingService编译链及ValidateBatch判定。原SDK资源分支仍服务尚在产品中的其他工具，不加ImageLoad旧入口。
5. RootWorkflowConfigurationWindow使用同一DeclaredFieldEditor消费Runtime正式Port/Presentation/ChoiceLabels；设置的常量备份来自同一草稿literal，输入资源不造null常量，不保留近似预览UI。无需共享控件新API。

## 原真实资源回归保留并扩展

RootResourceProductionTests保持同一Fact，不Skip、不直写绑定绕根代理：正式PluginLoader→实际ImageLoad/Blob Runtime配置→RootContext.Add/Write两路图像及一路区域订阅→确认前正式设计不变→跨根与错误资源类型候选拒绝且私有草稿恢复→源SN强设置常量编辑、错误类型拒绝与移除代理恢复→TryApply→Undo/Redo→新上下文取消解除订阅不污染正式配置→SolutionSnapshot保存/反序列化→重开RootContext重新解析每个代理及ReferenceSetting→WorkflowRunRequest.sourceInputs绝对地址/SN→真实源像素/Blob区域生产及消费→声明Reader断言数量1/面积16及持久设计仍仅含订阅无运行资源值。

- Blob面积使能字段来自正式XML稳定field:e9845ab7a46e5ee9b42310bef5a11c19；结果数量/面积/区域沿既有BlobSchema.Field(11/12/101)声明；测试不引用Blob CLR新项目，不改算法或范围。
- 生产执行、资源Lease、作用域/TypeRef/selection/storage编译、正式历史与序列化复用现有Workflow能力，未新增公共合同或平行配置层。

## 已执行验证与未验范围

- 五个改动C#通过当前已加载Roslyn纯语法解析，错误0；git diff --check通过。
- 尚未语义编译、正式Release构建、真实资源回归执行、VS设计器、WPF实际布局/主题或用户人工验收。源码贯通候选不等于功能验收完成。
- 主线须在同一正式Release执行UI及全量构建和原资源回归；验证根参数窗口添加/设置选择/输入订阅、错误诊断/取消、移除恢复、undo/redo、保存重开及真实结果。S2_CASE_ROOT仍由原正式回归宿主提供。
- RootWorkflowConfigurationContext未改文件，其既有适配分支被真实Runtime目标复用；不需要扩大历史或共享字段合同。
- CURRENT_TASK由root集成时集中更新，本支仅写专用交接避免覆盖总账。