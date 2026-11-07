# 原绑定/执行依赖：真实 Engine 首收件

- 基线提交：6bfc4a383e89483807501363e5538439f758225b；当前执行者 Codex，唯一产品写者。
- 目标/允许修改：原 FT-A01/A02/F02/G02/G03/K01/K03/M03 依赖，现Contracts/纯Runtime/SDK既有解析入口/Workflow依赖与原测试，不新增批次或载体。
- 已完成：纯net9 Runtime单一BindingPlan编译与解析、SDK委托；旧声明适配复用同compiler和Count规则；同scope无canvas来源进入原执行依赖图；候选从原图排self/真实后继；候选验证Apply/Undo事务核环、默认运行保节点局部失败隔离。
- 实际验证：固定基础43开发及独测PASS属基础层；新UI B2 34/34、Script B2新SDK下11/11、真实WorkflowExecutionEngine入口5/5。Engine实际证明先源后目标/下一run新值、source失败不回旧值、联合cycle准入拒绝/RunnerIdle/设计绑定与关系保留、同名跨scope拒、正式object契约未完整时global消费者不收初999且独立99运行。
- 原始轨迹：UI18/16→29/5→34/0；Engine3/2→5/0（cycle APIThrows期望修正、缺契约fixture改为已注册但类型未完整）；首offline NU1100后owned source mapping修复。原TRX/log不覆盖。
- 收件：artifacts/validation/tool-framework-contract/binding-b2-regression/engine-delivery-receipt.json；source-input-manifest.json是Git tracked/nonignored源码/config资产的精确快照清单，较大source-manifest为额外原始捕获库存；binary-manifest覆盖app和两testbin全部文件。
- 测试设施：原UI/Script csproj，owned validation-build.props保本地intermediate并隔离.Tests输出，owned offline NuGet config仅已有cache。三个精确filter及目录见receipt。Script测试编译期HostDebugRoot指向本专用build/app，冻结后禁止再写；独测若owned testbin仍从此固定host载入插件，必须核该root实际loadpath/前后SHA，不冒全tester-owned、不读共享Release。后继换新PackagingRoot。
- 当前版本限定：本诊断子包SDK ToolContract2.1/Foundation4.0，产品3.0主版本裁定尚未应用，不是最终发布。新Guid RequestId/expected-source准入映射尚未交付，不将旧Sequence基本guard算完整同请求隔离。
- 已知未完成：M03真实窄CLR约束/受检物化及实际字段权威；Conditional同样做source type/FieldId/policy失效；必填非空成员Compatibility和持久化精确minor；产品3.0真实PluginManager版本门/全源工具重编；EditorScope/资产Owner/三工具/正式树消费者/7交互/完整Release和最终集中手验。
- 依赖锁：SDK、Runtime、Verification新增项目闭包是本任务必要变更；原其他dirty locks及B2 restore闭包变化原样记录，不混入本提交/不回滚。所有实际冻结输入以manifest为准，不能宣称worktree clean。
- 下一步：独测立即固定包入口replay/自有实际输入；实施继续原后继，不停交接。程序集/ALC载体仍暂停，A04成本完成不授权生产迁移。
