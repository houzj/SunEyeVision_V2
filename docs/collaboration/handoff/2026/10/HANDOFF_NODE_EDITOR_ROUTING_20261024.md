# AI 协作交接单：UI节点编辑入口与完整候选集成

- 基线提交：bc186941（ImageThreshold后继0b1e9645已父审合）；原父SDK147668已在同主树。
- 当前分支/执行者：main / Codex，唯一共享树产品写者。
- 任务目标：原生产编辑链，NodeOpen的WPF工厂/壳/Owner移UI，使用正式节点保身份和DebugWindowStyle；None不创建UI；Default/Custom同实际编辑初始化/关闭边界。
- 允许/实际文件：Workflow/Nodes的NodeOpenContext/DecisionChain迁到UI/Services/Editing/Nodes，namespace UI.Services.Editing；MainWindowViewModel唯一caller改用正式node，不经丢style的ToWorkflowNode。选择壳也集中该UI服务，ImageLoad宿主factory在None之后才执行。
- 已完成：父已逐读整个NodeOpen实际调用，仅MainVM使用；Workflow执行层不再包含这两个WPF控制类。原控制流导航事件保正式节点；已存在插件Window保Owner；Custom SDK薄转发和Closed不二次persist已在147668。ImageThreshold/ProtocolParse同ProductionEditorContext→Host唯一CAS，其他原代表链按已批准依赖继续，不借搬类冒全部编辑器迁移。
- 测试：新增NodeEditorRoutingTests的UI物理归属、Default/Custom壳、None不调用factory、tool样式、直接Window Owner、Subroutine正式身份。只将ImageThreshold测试Sta方法从private改internal复用相同真实Dispatcher，不改body或重建线程，避免静态WindowChrome跨测试STA。此提交先作为可核源码候选，完整Release/新测试/实际主窗E06E07待本精确整合包运行，未冒PASS。
- 未完成：新整合完整Release、实际程序/所需完整插件、原E06 main173/variable181、E07 not-int拒写+修正重试、真实Run→Save→Reopen独验。原D dd6有限12+6组125只有限组件通过；Threshold开发9+4分轮真实控件通过不替实际主窗。
- 人工结果：尚无本候选人工/VS结论。独测短首段过后才提供有限代表节点试用，最终完整人工仍同一Release集中说明。
- 禁止改动：A11 eeb固定已实收停止写；无载体/ALC策略变更；不自动展开Blob（当前步骤由用户/主控讨论）、模板资产或七形态第三产品面。
- 下一步：从此整合源码独立D归档构建，准确stamp/HostDebugRoot/source-bin闭包，先过设施再新受影响测试/实际主窗短独验；缺陷直接修复新fixed，不停交接。