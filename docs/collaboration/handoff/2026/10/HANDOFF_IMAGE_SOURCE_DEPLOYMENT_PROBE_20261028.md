# 部署探针与根参数Runtime消费者交接（2026-10-28）

- 基线提交：aabe0ebb；分支：codex/image-source-deployment-probe；执行者：source_editor。
- 允许修改：Run-S3ComponentDeploymentValidation.ps1与本交接；未写产品及共享合同。
- 已完成：用ToolId=BlobAnalysis及Role遍历实际组件声明，每角色恰一；获取匹配声明的componentId定位optional文件；缺Runtime、重复角色、错误版本、孤立Editor、错误类型全部锁同一Blob组件，不依赖声明数组首项，不删除同文件无关声明。保留22工具准入及全部既有整链探针。
- 验证：PowerShell解析0错误；仅提取纯Get-BlobComponent做内存四角色（ImageLoad/Blob双角色、同文件额外声明）精确选择及重复拒绝夹具通过；未运行完整部署脚本，未碰Release；未构建/运行/VS/人工验收。

## 根参数生产链盘点及最小适配设计（只分析，未实施）

|已有位置|已有行为|实际差异|最小整链适配及验收|
|---|---|---|---|
|Workflow/RuntimeToolConfiguration.cs SettingsSchema/ReadFieldValues/WithFieldValues/BindingTargets|强设置冻结、声明字段读写、设置与运行输入共同为可绑定目标|RootParameterCatalog仅从Parameters提供候选，未枚举此正式能力|Catalog先按RuntimeConfiguration分支枚举SettingsSchema与Schema.Inputs；字段ID/StoragePath用稳定Port.Id，展示取各自Presentation；EditModes.None不提供候选，集合/资源常量继续禁止，不推断CLR属性。强设置常量通过WithFieldValues替换草稿RuntimeConfiguration，绝不写ParamSettings副本。|
|UI/Editing/RootWorkflowConfigurationContext.cs|Candidate通过BeginEdit持同节点私有草稿，根与节点同批提交；ValidateReference临时写入后ValidateBatch并恢复；取消/历史/rebase已具备|Read/Write/Remove依赖旧FieldAccessor或RootResourceProxyBinding，旧资源分支强转MeasureGuideParameters|将Runtime目标适配为真实Port及草稿读写：引用统一ParamSettings稳定ID+序列化OutputReference；Unset解除；Settings常量走正式字段；输入仅按EditModes放行。恢复操作同时恢复RuntimeConfiguration与对应ParamSetting，不保运行资源。Remove恢复opening同字段及订阅，保其他候选变化。复用当前参与者与事务，不新造历史层。|
|Workflow/RuntimeInputBindings.cs / WorkflowBindingContext.cs CompileSetting、ValidateTyped|BindingTargets找目标；拒绝禁用Reference的端口，ParameterBindingService编译OutputReference，检查正式类型/作用域/身份|底层已支持Runtime，无需新资源编译器；Root前端未接|Root验证沿现ValidateBatch到此链，不能只比较ResourceKind或绕过编译；保留完整TypeRef/storage/selection/memberpath，不自行限制为旧完整资源规则。需非法类型、跨根、删除来源、schema变化拒绝并零修改。|
|Workflow/AlgorithmNode.Runtime.cs|绑定计划AcquireReader、Capture/ResolveBinding，设置订阅形成仅本轮WithFieldValues，资源leases在finally释放|执行能力已有，根编辑未产生可达配置|Blob两个运行输入使用input.image、input.inspection.region；源用output.image与实际BlobSchema区域端口。不改Blob业务、不扩大公共合同。真实运行前sourceInputs提供解析后的绝对地址及SN；无默认图回退。|
|Workflow/NodeConfigurationEditHost.cs及.RootConfiguration.cs|同批ComposeRoots，Runtime验证/复制/回放与持久签名已含RuntimeConfiguration和ParamSettings；Root配置代理持稳定节点/字段身份|适配器必须避免Parameters上的旧binding状态副作用|复用现批次接纳/冻结/保存，不新增第二份配置。根代理→编辑订阅→接纳→undo/redo→保存重开→重新解析候选→正式执行均验。RootResourceProductionTests保原实际图像和区域订阅全范围，取消与非法引用不得污染正式节点，资源值不得落持久配置。|

结论：缺口为根参数消费者接入既有Runtime声明/事务/编译能力，不能判为底层能力不具备。建议集中改RootParameterCatalog、RootWorkflowConfigurationContext及其Runtime目标适配（旧SDK分支仍供其他实际工具），并同步根参数实际视图对Port/空设置/引用模式消费；不得为此创建旧ImageLoad桥。RootResourceProductionTests必须改真实组件准入与上述根代理生产入口，不Skip、不直接设置ParamSettings绕过根界面上下文。正式Release同源构建与真实资源回归由主线集中执行；原图像/区域资源输出、面积16、保存重开、无持久运行值范围不得缩小。

- 未完成：上述消费者适配及实际用例迁移由root/main协调；本支仅部署脚本修正。
- 已知风险：完整脚本会暂时移动正式文件并修改清单，必须由唯一正式构建窗口执行；本支没有执行。
- 下一步：root集中合并脚本，核根视图消费后按上表完整适配，再运行原根代理回归及部署轨。