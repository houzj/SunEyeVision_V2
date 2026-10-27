# 根资源仅订阅代理交接

- 基线提交：ba889d4a；设计输入554c1399（已在前分支提交，当前新分支从指定A固定基线建立）。
- 当前分支：codex/root-resource-proxies。
- 执行者：并行B，独立workflow-overview工作区唯一写入者。
- 授权范围：根候选目录/上下文/来源选择器/配置窗口代码后置、内部引用适配helper、专用测试及本交接；未改Workflow模型、SDK公共合同、Group、共享Shell与主文档。

## 整体设计与实现

根页资源输入是来源身份曝光，既不要求常量编辑器，也不存图像/区域值。现有多节点草稿事务承担正式校验/发布/历史/保存；新增内部RootResourceProxyBinding仅在节点候选上改既有持久化引用。

- 候选：RootParameterCatalog不再provider提前return；保留provider可存常量字段，再合并专门ImageSource和IInputSlotContract+RegionInputStorage明确像素区域槽，最后补其余已声明字段并按存储路径防重复。资源元数据稳定FieldId为input:存储路径，Port为Resource(Image/Region)、Reference-only，FieldAccessor明确拒写常量。
- 图像：仅MeasureGuideParameters及BindingInputCatalog明确目录的ImageSource开放；Read使用ImageSourceRef+正式输出FieldId/声明，Write先清运行时ImageSource再写新的ImageSourceRef（来源节点/工作流/输出FieldId/真实属性名），不把AvailableDataSource或图像值当canonical参数。解除清引用、对应ParamSettings与BindingSourceIdentities及待重绑标记。
- 像素区域：仅正式IReadOnlyRegion槽且provider.RegionInputStorage映射开放，通过现有ParamSettings写类型化ReferenceExpression及Scope/FieldId；不触及资源对象或lease。
- 源校验：只正式Data输出，资源种类与端口声明一致且根作用域一致，来源字段失效明确错误；目前专门存储只承接完整顶级声明资源（不把成员选择/集合索引错误降为主输出）。Picker只列目标资源种类，仍经现Host.ValidateBatch检查依赖、类型和图可执行性。
- UI：资源行独立只读来源标签、订阅、解除三个动作，未设置为“尚未选择来源”，失效来源仍能解除/重选；不创建Resource常量、空常量或数值模式按钮。原可存常量字段继续复用DeclaredFieldEditor。
- 临时校验：候选引用写入→正式批校验→finally完整恢复专门图像引用、ParamSettings、来源身份和pending/rebinding字典，不污染其他未提交草稿。来源选择确认后再写真正候选。
- 生命周期：RootContext沿原Register/Prepare/Apply/Acknowledge/Undo/Redo/Dispose；删除代理恢复opening候选对应资源引用和元数据，Cancel仅丢候选。正式发布通过现RootConfiguration+node drafts批事务，旧执行快照不受影响。文件与整流程复制复用现ImageSourceReference/ParamSettings/BindingSourceIdentities重映射消费者，无第二套根资源数据。

## 当前未开放范围

RoiCollection几何集合不是IReadOnlyRegion像素区域，继续原禁用说明；位置修正AffineMatrix/数组需要专门类型/取值适配，当前仅记录禁用范围，不伪装图像或像素Region。Group保持冻结，类别/多目标属于Group的审计纠正554c1399不因此反转。

## 测试与验证状态

新增RootResourceProxyTests：provider标量与图像/区域槽合并；双资源候选批发布、旧运行代际、Undo/Redo、解除身份清理；删除恢复与Cancel不写正式；严格SolutionSnapshot文件、片段复制跨流程重映射；坏资源类型/跨作用域/来源失效拒绝。

本子工作区没有构建/部署程序，没有复制算法库，未运行这些测试。六份C#已用进程已有Roslyn5.0做语法解析0诊断。仅内存语义定位首轮暴露ParameterSetting同名命名空间冲突并已用别名修正；首轮还含误纳native DLL/WindowsBase参考版本环境噪声，修正纯托管与WPF引用后，四份产品非XAML代码0语义错误（最后源校验小修仍须正式构建）。这些定位检查不代替正式net9 XAML编译或实际测试。

## 主集成下一步

集成本提交后按当前正式Release统一完整构建/测试，运行RootResourceProxyTests和现根98项回归。用真实ImageLoad→实际工具ImageSource、正式Region→Blob像素区域验证选择/解除/Apply/Cancel/Undo/Redo/保存重开/整流程复制/源删除/作用域拒绝/真实执行；补暗亮完整配置窗、VS设计时和用户人工验收。当前仅代码链与待跑用例交付，不能称资源完整生产验收已通过。实际WPF运行由主集成保持唯一程序产物。
