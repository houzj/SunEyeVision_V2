# 启动专题：独立测试复核与首批改造提案

状态：待用户确认，禁止据此修改产品。编号START-CHANGE-01。2026-10-22。

## 实际证据

独立测试START-TEST-01报告：artifacts/independent-testing/START-TEST-01/REPORT.md，SHA256 c109620b6ab023ed9ed332bcff3de8cac40aa9c92c3a8b3f313d58cd0af06932。主控复读报告、verification及baseline，并重新核17源SHA一致。
用例审核10/10；只读基线检查3/3；产品运行通过0、失败0、未执行10。已复现产品缺陷0，不等于产品无缺陷。
Debug UI为旧产物；Release虽有历史全量构建和22工具探针通过，现有材料未绑定当前全部dirty源码及依赖，不能据此断言产物不匹配，也不能断言已完全匹配。正常启动无已验证隔离数据根/IPC/设备替身，故测试者未运行产品、未改用户配置。

## 风险与优先级

| 优先级 | 来源 | 静态事实及风险，非已复现故障 |
|---|---|---|
| P0 | R01/ST03 | 自动与选择方案路径Show和相机等待顺序不同；缺统一WorkspaceReady，不能拿窗口出现当可运行 |
| P0 | R02/ST02 | void加载与MarkAsLoaded无法表达完整能力；已有单插件隔离/契约检查保留，需另验证方案所需工具 |
| P0 | R03/ST04 | 10秒仅等待预算；迟到任务仍在，临时VM Dispose为空操作；旧代次准入与最终Owner待验证 |
| P0设计前置 | ST01、SA04 | 必需阶段无统一失败终态/局部回滚；_servicesReady为false会跳过OnExit部分清理，具体泄漏未复现 |
| P0验证前置 | 基线/SA01—10 | 无安全隔离及完整构建输入绑定，无法独立运行验证 |
| P1 | R04/ST08 | Loaded视图异常以插件错误提示，需准确分类；尚未运行复现 |
| P1 | R05/ST07/ST01 | 早期writer窗口和同步OnExit等待存在风险，未证明必丢日志/必死锁 |
| P1后续 | ST05/ST06 | UI同步扫描/预热、静态装配和重构造影响响应与可测性；无性能实测不作耗时承诺 |

## 第一批只做三个增量

不实施全部16文件/全部8问题；不改工具算法、工作流调度、全SDK、许可或相机驱动。

### S1 可测试的启动上下文与可信基线

- 改App.xaml.cs的装配调用；新增src/UI/Services/Startup/StartupContext.cs、src/UI/Composition/ApplicationCompositionRoot.cs（拟议路径）。
- 显式注入数据位置服务、插件根、实例身份和设备恢复接口；复用ApplicationDataLocationService已有locator构造参数，不再默认静态捕获用户位置。正式启动仍使用默认生产策略。
- 专属测试宿主注入独立目录、IPC身份和内存设备替身；不提供可绕过许可/维护保护的通用生产开关，不用篡改LOCALAPPDATA实现隔离。真实设备适配器不得被测试宿主注册；文件关联不写用户注册表。
- 产品构建由开发唯一写者统一完成；记录源树/dirty输入、构建命令、工具链、DLL/依赖/插件及输出SHA，测试者验证绑定后运行。原数据/方案/配置前后哈希不变。
- 验收：隔离宿主只访问指定样本目录与独立IPC、不注册真实设备；生产默认不改变；17源之外也有明确受测产物manifest。SA02/07的可执行前提满足，不能因此标其业务断言通过。

### S2 显式阶段、终态和两路径一致的就绪门

- 新增StartupContracts.cs、AppStartupCoordinator.cs、StartupResourceScope.cs、WorkspaceOpenCoordinator.cs；改App.xaml.cs、NodeDisplayAdapterConfig.cs中的ServiceInitializer、MainWindow.xaml.cs和MainWindowViewModel.cs。
- 冻结必需阶段：进程维护/单实例准入、数据位置、基础服务和Catalog就绪、方案验证/绑定、必要视图附着。选择方案是可等待用户的阶段，设备恢复是独立能力，不用固定100%概括。
- 分开ShellVisible、WorkspaceReady与设备状态；失败Fatal/取消Cancelled不进Ready。自动/选择两路径共用OpenWorkspaceAsync；运行与外部打开入口执行状态准入，不只覆盖鼠标按钮。
- 已创建资源入scope，失败/取消反序幂等清理。保留进程锁到真实退出；async协调器在Dispatcher退出前完成，OnExit不新增依赖Dispatcher的同步等待。
- 恢复任务有generation及Owner，等待超时进入明确Degraded/Connecting；取消不成功则保留跟踪到终结并拒绝旧代次发布。复用通信ReadyAsync/已有取消机制，不重复新造运行时。
- 验收：SA01/04/05/06/09/10按子场景登记；逐必需阶段创建前后注错、重复退出、延迟恢复屏障、关闭/换代次，证明无提前Ready、无旧代次提交、Owner只释放一次。若不能证明底层回调归属，阻断该子场景验收，不宣称已闭合。

### S3 结构化插件加载与方案能力门

- 改PluginManager.cs、PluginLoader.cs；新增PluginCatalogLoadResult.cs；接SolutionRepository.cs/WorkspaceOpenCoordinator能力校验。
- 返回扫描、成功注册、拒绝、缺件和原因，不以bool替代Catalog能力。保留共享契约类型身份、非collectible进程生命周期和逐插件隔离，不做热卸载。
- 先建立参数类型Catalog再反序列化；缺工具方案的运行必须阻断并给精确诊断，不能静默替换/丢节点。是否允许保真降级编辑须有模型支持，未实现前不承诺可编辑。
- 验收：SA03健康+坏DLL+不兼容+缺必需工具四类组合，健康项仍注册、失败逐项可见、方案运行被正确门禁，源码/磁盘原方案不变。

## 分工、确认和退出

主控唯一产品写者负责S1—S3实现及构建；独立测试只写专属测试产物、复核manifest并执行SA子场景；文档角色在交付后增量同步，逆向角色当前不新派任务。现有VM局部启动证据已足够支持本批设计取舍，不等待整项目恢复。
开发自测通过不等独立验收通过。十项SA仍维持原ID，每个多子场景单独计数；SA08冷暖性能先采原始值和响应基线，本批不承诺性能提升百分比，也不伪造阈值。VS设计器/用户人工验收独立列未执行。
批准前不动产品。批准后的S1先验隔离，再S2/S3，失败则停止推进后续增量，保留精确问题。用户确认范围不等验收已通过。

当前：本次结果复核与改造提案交付完成；产品实施0，SA运行验收0/10；全产品无冻结总分母，不计算总完成百分比。

## SOL-01后续核对更正

方案管理源码已核：SolutionRepository.Load内ValidateTools已拒绝缺ToolType插件，LoadSolutionOnly已支持直接文件路径，不能把S3理解为从零补这两项。后续仅改善结构化诊断、统一全入口和会话完成合同；现有保护保留。启动实施顺序需先确认SOLUTION_MANAGEMENT_REVIEW_20261022.json中候选/目录/会话/提交边界，本提案未获得产品实施授权。
