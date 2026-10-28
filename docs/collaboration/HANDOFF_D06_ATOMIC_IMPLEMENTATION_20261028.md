# D06 完整类型生产者实现交接

- 基线提交：c2aeebb2（本线合入1d1e229c）。
- 当前分支：codex/workflow-toolbox-dataflow；root集成。两独立子工作树候选均停止写入，作为证据保留。
- 任务目标：以唯一OutputReference合同贯通声明端点、正式UI事务和源scope内生产赋值；Group冻结。
- 允许修改目录：Workflow、UI及专用ResultPresentationModel；本批集中同步SDK非视觉作用域代理/ToolExecutor和ReceiveData消费者，不扩共享Contracts/主题/DeclaredPropertyList。
- 已修改：具体源码与回归见本批Git diff；UI/Manager完整入口分别见HANDOFF_D06_RESULT_BINDING_UI_20261028.md和HANDOFF_D06_PRODUCTION_MANAGER_20261028.md。此两文件保留独立候选阶段事实，集成状态以本文件为准。
- 已形成源码链：ProducerBinding严格序列化ReferenceExpression（Scope/Node/Port/根Type/Storage/MemberFieldPath/All），名称仅显示；clone/copy/history/signature/依赖/通信同批改用新身份。无旧ResultName或Guid/name fallback。
- UI：声明成员及All元数据不借资源，sample[n]不冒充声明端点；真实选择器筛正式存储类型，modal返回复核session/publication/row/endpoint；绑定/解除一次CommitBatch。历史接纳后只刷新同publication元数据，不取latest或多借lease。
- 生产：同Host.Capture版本workflow/global定义及真实owner/资源基目录；live未绑定纯值另作开始快照。源完成后、下游dispatch前，使用真实scope/admission和独立_expected，借该源DeclaredOutput reader编译/解析，物化纯值后释放reader；失败移除本轮值，取消不接纳新值，不读设计初值。batch由Manager最后引用退出后Dispose。
- 通信：单模块统一Engine同root直接节点；SDK RuntimeVariableReceiveService仅消费者scope转发，ReceiveData委托正式运行变量服务，不再从nodeResults另解字段。源字段映射只在正式声明中解析，产值只走同一类型绑定服务。
- 明确未做：通信现行只接单项String/Integer/Double/Boolean，数组读取明确NotSupported、不静默展平；数组全局仍可供工作流参数使用。资源/几何/组合全局存储及租约基础未接入；Group内部单模块拒绝搬到错误scope。D07视口、真实VS/硬件/人工与目标其他未验项保留。
- 已执行：43个修改/新增C#纯语法0错；git diff --check通过。没有本批语义编译或运行成绩，没有私有发布或DLL复制；不能套上一批173/Host通过结果。
- 正式验证：主线合固定候选后规定UI/full Release；先EXE身份probe，再Run-S2ReleaseValidation.ps1 -Track GlobalBinding（15组，含真实引擎/Manager/选择器/历史保存/通信/错误身份/取消及纯值拒绝）及ImageSources（执行准备链受影响）。相关失败只按实际日志定位、集中修，不反复重跑无关已绿轨。
- 人工验收：尚未；正式自动范围通过后再启动同一正式产物。独立子分支/worktree当前不删除；固定成果进入集成基线且验收后再归档，避免丢失未合差异。

## 首次正式回归后的集中修复交接

- 原24615965已合主线e258a862；固定5780c062 UI002/full001均零错，EXE身份probe0。首次GlobalBinding237过20败/257，ImageSources174过2败；权威记录为主仓Release/_validation/S4-20261025/GlobalBinding/attempt-001及对应ImageSources，不覆盖失败日志。
- 夹具集中修复：256b5de0（4类正式类型/字段身份/Host草稿冲突），8ad550f8（真实WPF dispatcher、typed端点断言及当前batch服务）。本次手动租约fixture改登记真实流程，不改生产准入。
- SDK拒绝结果只用显式ToolSideEffectReceipt保已发送纯值事实，新Cancelled保同request失效身份；源资源和IDisposable必清，捕获/清理失败返回新Failed诊断。非发送业务状态、声明成功输出、Tag/显示/ROI不复制。SendData唯一提供者，取消列表只返回副本。8项sync/async×取消/退休/捕获失败/清理失败已挂GlobalBinding；只读审查未见阻断，尚未运行。
- SendData正式事务整链由独立写者集中完成：同私有nodeDraft和变量草稿、typed Configure/Prepare、联合提案/历史/保存/关闭取消；SendDataProductionEditorTests三项真实窗口回归挂同轨。多变量提案冲突仍明确拒绝；不偷改正式对象后Synchronize兜底。
- 下一主线收齐固定候选后统一UI/full、身份探针、GlobalBinding与ImageSources；本线不构建、不写任何私有Release/DLL。语法或源码提交不计正式通过，不提前启动交付；真实VS及人工仍待验。
