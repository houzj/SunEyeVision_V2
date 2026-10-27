# 根运行参数分类/关联归属纠正与资源订阅最小设计

- 执行者：并行 B，只读核证；允许写入仅本报告。
- 本产品固定核证版本：`ba889d4a`，用 `git show ba889d4a:path` 读取，未把主集成草稿搬入此工作区。
- 恢复项目：`D:/MyWork/REA逆向测试` 当前本地源码，非新原厂动态观察。
- 结论：**CategoryId/Relations 的可操作入口属于 SubprogramCallWindow 的 Group 运行参数配置，不能据共享模型字段误列为根 R12 缺项。当前阶段不要增加根参数类别、多目标关联，也不要扩大冻结 Group。** 根资源/图像输入的 Reference-only 订阅另有真实已有基础，不能因没有常量编辑器而一概置为缺基础。

## 1. 完整入口与准确文件

原请求所写 `VisionEditor/WorkflowConfiguration.cs` 实际不存在。模型准确位置为 `src/VisionFramework/WorkflowConfiguration.cs`，关联校验在 `src/VisionFramework/WorkflowRunParameters.cs`。

| 作用域 | 入口到生产消费者 | 已确认行为 |
| --- | --- | --- |
| 根流程配置 | `VisionEditor/WorkflowConfigurationHost.cs` CreateWorkflowConfigurationWindow/OpenWorkflowConfiguration → `WorkflowConfigurationWindow.xaml.cs:30` Refresh → `:61` BuildRunParameters → `:99` AddRunParameter | 平铺单目标参数或输入槽，编辑/订阅/移除；不读写CategoryId/Relations，无关联编辑或同步按钮。AddRunParameter从各节点Parameters+Inputs产生候选，并按NodeId/Parameter/IsInput防重复。参数值写SetProcedureNodeParameters；输入订阅写SetProcedureInputSubscription。 |
| Group运行参数 | `SubprogramCallHost.cs` 创建SubprogramCallWindow并绑定Group命令 → `GroupInputRunControls.cs:49` CreateRunConfigurationWindow → `GroupRunConfigurationWindow.cs` → partial `GroupRunCategories.cs` | 类别、重命名/次序、关联选择、数据同步和复杂参数编辑，明确消费GroupBody。 |
| Group多目标写入 | `VisionEditor.Core/GraphEditorDocument.Groups.cs:70–74` SetGroupRunParameterValue/Source/ComplexValue | Targets包含主目标及关联目标；候选全部验证后一次Edit，落入各节点正式参数/订阅，运行层随后消费正常节点值；关联不是输出订阅本身，也不是执行器每轮自动广播逻辑。 |

共享WorkflowConfiguration能存储CategoryId/Relations且通用Validate不强制GroupOwner，不足以证明根配置提供此功能。上述窗口入口和实际回调才确定可用范围。根页传入一个外部带Relations的对象也不会自动获得Group多目标写入行为。

## 2. 冻结Group语义保留记录，不在本阶段实施

- **类别**：WorkflowRunCategory(Id,Name) 是持久化自定义运行参数目录分组；CategoryId指向此Id，可添加/改名/上下移动/选择/展开。删除类别需确认并同时移除该类别运行参数；清空移除类别与代理。新增首个参数时可建立“分组0”。它不等于SDK RuntimeParameterDescriptor.GroupName元数据。
- **关联**：WorkflowRunTarget(NodeId,Parameter,IsInput) 表示同一代理附加的写入目标，不是数据来源；Targets=主目标+Relations。ChooseRelations只从相同ModuleId、相同Parameter名、相同输入/参数域及兼容类型/编辑器能力的节点产生可多选候选，排除主目标，支持“解除关联”。不能仅按都是double而跨工具任意关联。
- **同步**：点击“同步”从主目标当前值复制到全部目标；普通参数拒绝已订阅目标；复杂参数同步全部声明Fields/ROI等并批量验证。选中关联本身只是保存目录，不代表已经把全部参数值立即改成一致。
- **订阅**：SetGroupRunParameterSource把同一个真正来源展开成每个Targets的正式WorkflowSubscription；输入订阅同时清除对应普通数据边后统一验证类型/循环/顺序。普通类型化引用是一个目标的来源关系，多目标关联是目录聚合，两者不可混称。
- **约束**：WorkflowRunParameterLimit读取MaxRunParamsNum，默认32；类别与代理各受该限额。Targets主+附最多128且不重复，不混输入/参数域；输入Kind+Cardinality匹配，参数Kind/Editor.Kind/Fields及ROI能力匹配。GroupOwner非空时还约束代理Label非空且唯一。
- **复制/删除**：WorkflowConfiguration.Snapshot复制Relations数组；RemapNodes→proxy.Remap重写主目标和关联NodeId，类别/代理局部Id保持原件现策略。RemoveNodes删除主目标则整个代理删除，删除附加目标只过滤该Relations；GraphEditorDocument.cs的DuplicateProcedure和DeleteNodes实际调用这些方法。不能将此原件局部Id策略不加分析照搬成产品全局身份。

## 3. 固定A版本对照：不要为错误归属重写根合同

`src/Workflow/RootWorkflowConfiguration.cs` 的RootParameterProxy是一条正式字段曝光，Id/NodeId/ToolType/FieldId/StoragePath/DisplayName完整；`RootWorkflowConfigurationContext` 在正式节点候选上读写并经DocumentEditProposal.ForRootConfiguration/ValidateBatch/CommitBatch批量发布，保留正式类型化订阅/运行前快照、复制重映射和保存链。根窗口当前平铺代理与恢复根入口范围一致。

因此分类/多目标不是当前根R12应补项；不增加模型字段、关联窗口或SDK合同。Group续做须另经用户恢复，先就Group拥有关系、字段域、编辑器能力及资源清理整体设计，不能把上述局部多目标能力当全部Group基础已就绪。当前整体最小设计就是维持根单目标，纠正原主方案/审计清单的误归属，再集中验收真实根生产链。

## 4. 根资源Reference-only已有基础与实际缺口

### 已有基础

- `Plugin.SDK/Execution/Parameters/BindingInputCatalog.cs:19–50` 对 `MeasureGuideParameters.ImageSource` 和 `PositionCorrectionBindingSource` 提供专门目录、AllowedTypes、ReferenceOnly、Active、ReadWorkflowId、ReadFieldId。图像来源不是图像像素常量，持久化于ImageSourceRef；AvailableDataSource是运行时来源描述。固定A并无VisionToolParameters类，不能引用不存在的GetInputSlots作为已迁依据。
- `BindingClrAdapter.DescribeOutput:13–30` 仅接受正式Data输出；显式IVisionImage输出可映射Resource(Image)。`TryDescribe:38–39` 直接支持IReadOnlyImage/IReadOnlyRegion。注意TryDescribe并不直接接收IVisionImage，不能以AvailableDataSource的CLR类型推导图像输入Port。
- `Plugin.Wpf/Fields/DeclaredFieldEditor.cs:89–150` 拒绝Resource常量，但可接受ReferenceSetting、选择来源和清除可选Unset；无须像素常量编辑器。当前未设置Resource初始时仍需要正确引导进入引用模式，不能塞ConstantSetting(null)假装图像值。
- `ReferenceSelectionControl.cs` 基于正式Port/OutputReference，不排除资源；最终调用host validator校验。现根RootWorkflowReferencePicker的output.Kind==Data筛选正确，因为图像也是Data；不得放行ControlSignal。
- `Workflow/BindingRelationCatalog.cs` 把BindingInputCatalog与ParamSettings统一纳入正式关系；ToolExecutor.ResolveRuntimeParameters按执行请求快照解析；无需UI自己读图像或执行算法。

### 本产品当前应优先承接的具体槽

| 槽 | 真实目录/存储 | 最小范围 |
| --- | --- | --- |
| 图像来源 | MeasureGuideParameters.ImageSource + ImageSourceRef，BindingInputCatalog的IVisionImage AllowedTypes | 所有实际部署且继承该基类的普通工具，根页显示来源/订阅/解除；不是IRuntimeParameterContract某个数值字段。以当前已装载节点目录为准。 |
| Blob检测像素区域/屏蔽像素区域 | BlobAnalysisParameters.GetInputSlots:InspectionRegionInput/MaskRegionInput，IReadOnlyRegion；IConfigurationFieldProvider.RegionInputStorage声明inspection/mask映射 | 独立工具现有DeclaredToolConfigurationControl.RegionSources.cs已通过ParamSettings+类型化ReferenceExpression写入，根页应复用同一正式存储；无需集合常量编辑器。 |
| 检测/屏蔽几何集合 | Blob/Threshold/Filter/Morphology的InspectionRois/MaskRois是RoiCollection，内部元素身份/模式/订阅另有协议 | 不能因与像素Region同名就当IReadOnlyRegion整体替换，也不应只把Reference-only控件接上就称完整；按现有声明与几何消费实际边界另核后精确占位。 |
| 位置修正 | BindingInputCatalog.PositionCorrectionBindingSource允许AffineMatrix或数组，并有启用状态 | 不是图像资源；需现有矩阵类型/取值语义匹配才能启用。没有规范Port适配时只占位此槽，不能把图像槽一并延期。 |

### 最小整体接线设计（不改代码，供主集成实施决策）

1. 根候选目录从现provider字段、IRuntimeParameterContract、IInputSlotContract及BindingInputCatalog明确专门槽合并，按稳定域+字段/存储身份去重；provider存在不能提前return而漏掉其RegionInputStorage及ImageSource。资源槽只允许Reference/Unset，不按常量字段的CanWrite/Scalar条件误拒绝。
2. 把根候选区分“参数值Accessor”和“专门输入引用Accessor”，都指向同一节点候选快照：图像写ImageSourceRef/来源身份，BlobRegion写正式ParamSettings；不要把图像对象存入RootConfiguration、图像字段CLR常量、上一轮结果或UI缓存。
3. Read返回ReferenceSetting或UnsetSetting，Write只接受Reference/Unset并维护正式Scope/FieldId/Storage；清除同时移除成对存储，不留陈旧BindingSourceIdentities。所有写入仍由当前RootContext的候选/批校验/原子发布链处理。
4. 来源树复用当前RootWorkflowReferencePicker+BindingClrAdapter.DescribeOutput的Resource(Data)声明，依赖和作用域沿正式Query。引用类型不匹配、循环、跨作用域、来源删除或重载冲突由正式Host校验拒绝；资源指针和lease不进入配置候选值。
5. 统一验证：真实ImageLoad输出→普通工具图像槽，以及正式Region输出→Blob像素区域；选择/解除/Cancel/Apply/Undo/Redo/保存重开/整流程复制身份/来源删除/跨域拒绝/真实执行。使用同版正式Release，不以只有内存类型测试代替生产链；未支持的几何集合和矩阵适配逐槽标依赖。

## 5. 验证限制及交付

本批只读双方源码，未构建、未运行WPF/原厂动态，未修改产品或Group。A的98用例通过是主集成已报告状态，本报告没有重复执行或据此宣称资源槽已迁。分类关联归属已通过完整窗口入口/回调/生产修改链确认，停止扩查其他VM功能。主文档/总账由主集成集中纠正，不能将本报告当独立功能完成。

## 恢复件文件身份

- `VisionFramework/WorkflowConfiguration.cs` SHA256 `ad59db622c095ac22c3550c3b3ca4b6491ac31d68108a83e0caa9c56928e7b1f`

- `VisionFramework/WorkflowRunParameters.cs` SHA256 `75820316ee555fa4555d137d0d647d1fbedc068e60dee4ab2b78f469f8c30c37`

- `VisionFramework/WorkflowRunParameterLimit.cs` SHA256 `c569f5b5cff61c6c1148fdf03f798b221b60da97727034ad47e153247dbe7c76`

- `VisionEditor/WorkflowConfigurationHost.cs` SHA256 `6acc3803692f05e9013bd87e4a5ffcedbb011cc501efb3530202e2d1f8b1b52f`

- `VisionEditor/WorkflowConfigurationWindow.xaml.cs` SHA256 `c4550293546a4b0d691a3fe133a16e1e916b479b8b0e243321d804f43c878a50`

- `VisionEditor/GroupRunCategories.cs` SHA256 `a15b69a9549445fa65fb86cd2a24f9ba7346ece88b96827a57b0072c960365ca`

- `VisionEditor/GroupInputRunControls.cs` SHA256 `bfa3ff8a61b0a0d52df815edaa1f97cccdbcb4bdd054297d32a9ecbea7380c6e`

- `VisionEditor/GroupRunConfigurationWindow.cs` SHA256 `c151c447b2819258539c2d47532baaff2aad8d272cd8427730cf5b0f452c6bf2`

- `VisionEditor/SubprogramCallHost.cs` SHA256 `b6c36afc73814a8bb1a9a86abbdf129405a1ff3bf394ed42297c0e6ed8a9a387`

- `VisionEditor.Core/GraphEditorDocument.Groups.cs` SHA256 `48de9ee3c2e861e71e1e27499403341f252212acc132a2cd93db37bb3bfec536`
