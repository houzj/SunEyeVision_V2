// Evidence review only. No product mutation or runtime probes.
const fs=require('fs'),crypto=require('crypto');
module.exports=function(caps,sources,optimizations){
 const a=require('./audit-and-paginate-entry-report.cjs');
 sources.push(...a.data.sources.filter(s=>s.id.startsWith('R')));
 for(const c of caps){const r=a.data.caps.find(x=>x.id===c.id);if(r.review){c.review=r.review;c.gap=r.gap.replace(/；[^；]*已拆[^；]*/g,'');c.change=r.change;c.sourceIds=r.sourceIds;}if(c.id==='T14'){c.state=r.state;c.current=r.current;}}
 function src(file,token){const t=fs.readFileSync(file,'utf8'),ls=t.split('\n');let i=ls.findIndex(l=>l.includes(token));if(i<0)throw Error(file+token);const id='N'+String(sources.filter(s=>s.id.startsWith('N')).length+1).padStart(3,'0');sources.push({id,path:file,line:i+1,token,sha256:crypto.createHash('sha256').update(t).digest('hex'),snippet:ls.slice(i,i+22).join('\n')});return id;}
 const bindingRefs=[['src/Workflow/WorkflowExecutionEngine.cs','var inputIssues = OutputBindingChangeService.GetExecutionInputIssues'],['src/Workflow/OutputBindingChangeService.cs','public static IReadOnlyDictionary<string, IReadOnlyList<ExecutionInputIssue>> GetExecutionInputIssues'],['src/Plugin.SDK/Execution/Parameters/BindingContractValidator.cs','public static BindingValidation Validate'],['src/Workflow/WorkflowDefinitionService.cs','workflow.DetectCycles()'],['src/Workflow/OutputBindingChangeService.cs','SourceFieldId']].map(x=>src(...x));
 const stopRefs=[['src/Workflow/WorkflowExecutionEngine.cs','foreach (var item in running)'],['src/Workflow/WorkflowRunSession.cs','public void Dispose() => _cancellationSource.Dispose()'],['src/UI/Services/Workflow/WorkflowExecutionManager.cs','ReleaseExecution(workflowId)']].map(x=>src(...x));for(const id of ['M15','X01'])caps.find(c=>c.id===id).sourceIds.push(...stopRefs);
 for(const id of ['M07','T02']){const c=caps.find(c=>c.id===id);c.sourceIds.push(...bindingRefs);c.current='已静态核实执行前循环检查，以及计划编译时绑定作用域、上游关系、类型/字段路径诊断。';c.gap='待核多入口反馈时机、List<>/未声明对象合同、嵌套参数和旧文件字段迁移；不再把局部UI检查误当全系统能力。';c.change='复用现有校验/变更对账，按缺口补合同与反馈；撤回整体绑定模型重做建议。';c.chain=['WorkflowDefinitionValidator.Validate','ToolParameterSnapshot.Create','OutputBindingChangeService.GetExecutionInputIssues','BindingContractValidator.Validate','WorkflowExecutionDependencyGraph.Compile'];}
 const globalRefs=[['src/UI/Features/GlobalVariableManager/GlobalVariableManagerViewModel.cs','_solutionManager.SaveSolution(currentSolution.FilePath)'],['src/Workflow/SolutionManager.cs','bool success = _repository.Save'],['src/UI/Features/GlobalVariableManager/GlobalVariableManagerViewModel.cs','variable.Id = Guid.NewGuid().ToString()'],['src/UI/Services/Workflow/WorkflowExecutionManager.cs','private void ApplyProducedGlobalValues'],['src/Workflow/GlobalVariable.cs','Value = Value,']].map(x=>src(...x));caps.find(c=>c.id==='M18').sourceIds.push(...globalRefs);caps.find(c=>c.id==='M18').current='已补管理器/绑定/本轮快照/生产/消费/显示/文件7段实际链。基础类型序列化为数组，复杂类型另分支；保存与导入有确定整改项。';
 caps.find(c=>c.id==='M18').review={...caps.find(c=>c.id==='M18').review,confirmed:caps.find(c=>c.id==='M18').current};
 caps.find(c=>c.id==='T18').knownGaps=['ShapeMatch主调试载入模板按钮仅日志，局部未实现'];

 const specialistEvidence=[
 ['M03','persistence-review.json',['write','commit','recover']],
 ['M19','camera-architecture-review.json',['capture','stop','buffer']],
 ['M21','platform-review.json',['theme']],
 ['X02','platform-review.json',['controls','resolve','unload','deploy']]
 ];
 for(const [id,name,ids] of specialistEvidence){const c=caps.find(c=>c.id===id),r=JSON.parse(fs.readFileSync('artifacts/validation/'+name,'utf8')),parts=r.stages.filter(x=>ids.includes(x.id));c.sourceIds.push(...parts.map(x=>src(x.source.path,x.source.token)));c.current=parts.map(x=>x.what).join('；');c.review={...c.review,confirmed:c.current};c.gap=parts.map(x=>x.fact||x.what).join('；');c.accept=parts.map(x=>x.accept).join('；');}
 const root='src/UI/Shell/Windows/MainWindowViewModel.cs';
 const additions=[
 ['M24','状态栏、运行耗时与执行快照','StatusBarControl → StatusBarViewModel → IRootCoordinator/WorkflowExecutionSnapshot → ShowExecutionSnapshot','MainWindow底部实际绑定StatusBar；100ms计时刷新，显示相机/运行/流程数并打开执行快照。','IsRunning来自根协调器，仍受M15提前置停止影响；Shutdown退订与全退出调用者、切方案后快照代次待核。','把请求停止、排空、终态显示分开；状态栏只投影同一执行状态源，快照以方案/根流程/运行身份隔离。','慢执行停止期间显示停止中，终态到达才已停止；切流程/切方案不残留旧耗时；关闭计时器和订阅。',[['src/UI/Shell/Controls/Shell/StatusBarViewModel.cs','public class StatusBarViewModel'],[root,'private void RefreshStatusBarExecutionSnapshot()']]],
 ['M25','专业数据分析面板与图像高亮','SelectedNode → UpdateDataAnalysisControl → IToolEditorFactory.CreateDataAnalysisControl → OnResultUpdated → SelectionHighlightChanged','实际为独立于调试窗口的专业控件；无实现时隐藏Tab，切换先退订旧高亮，已有结果立即填充。','创建的临时tool与分析控件释放归属未在该方法闭合；缓存结果是否本轮/当前方案，失败取消后如何清空须追接收方。','定义分析会话Owner和结果身份，先核工厂是否返回共享/独立实例再配对释放；失败清空并拒旧结果，选中行只发稳定图元键。','多次切节点/开关方案无订阅增长，旧轮次不填新面板；失败不展示旧成功；高亮与当前底图同帧。',[[root,'private System.Threading.Tasks.Task UpdateDataAnalysisControl'],['src/UI/Shell/Windows/MainWindow.xaml','Binding SelectedNodeDataAnalysisControl']]],
 ['M26','日志面板、筛选、搜索与诊断级别','VisionLogger → UILogWriter → CircularBufferLogCollection → LogEntriesView → LogPanelControl','日志Tab实际存在；视图模型支持级别、搜索、自动滚动和清理/复制入口，容量集合与批写是独立层。','注释流畅不等压测；50ms节流和有界显示不证明上游队列有界；跨线程积压、持久日志、隐私和停机排空待核。','冻结日志范围与丢弃/背压策略；Run/Node身份关联故障，UI筛选不改变持久记录；逐订阅核Dispose，压测另行执行。','日志洪峰时UI可操作且积压有界，丢弃计数可见；搜索/清空/复制一致，关窗不再接收。',[['src/UI/Shell/Windows/LogPanelViewModel.cs','public class LogPanelViewModel'],['src/UI/Services/Logging/UILogWriter.cs','class UILogWriter'],['src/UI/Shell/Windows/MainWindow.xaml','Header="日志"']]]
 ];
 for(const [id,title,chain,current,gap,change,accept,refs] of additions)caps.push({id,title,group:'main',state:'partial',chain:chain.split(' → '),current,gap,change,accept,sourceIds:refs.map(x=>src(...x)),vm:'以恢复模型主界面对应状态/专业数据/日志入口核范围；模块历史、专业数据页和应用日志是三种数据，不互相代证。',vmDoc:'framework-architecture.html',vmLimit:'此项当前只完成VM入口对账；原版字段、线程、释放和日志保留机制仍需对应实现体，不能引用重建测试当原版事实。',optimization:id==='M24'?'O02':id==='M25'?'O07':'O12',validation:'静态源码复核；未运行产品或进行性能测量'});
 for(const step of require('./review-result-projection.cjs')()){const c=caps.find(c=>c.id===(step.packageId==='O07'?'M25':'M16'));c.sourceIds.push(src(step.source.path,step.source.token));c.gap+=' '+step.fact;c.accept+=' '+step.accept;}
 for(const step of require('./review-shutdown-chain.cjs')()){for(const id of step.caps){const c=caps.find(c=>c.id===id);c.sourceIds.push(src(step.source.path,step.source.token));c.gap+=' '+step.fact;c.accept+=' '+step.accept;}}
 const scriptEvidence=require('./review-script-source.cjs')();const scriptCap=caps.find(c=>c.id==='T15');for(const step of scriptEvidence.steps){scriptCap.sourceIds.push(src(step.source.path,step.source.token));scriptCap.gap+=' '+step.fact;}
 const subReview=require('./review-subroutine-chain.cjs')();for(const id of ['M09','T16']){const c=caps.find(c=>c.id===id);for(const step of subReview.steps){c.sourceIds.push(src(step.source.path,step.source.token));c.gap+=' '+step.fact;c.accept+=' '+step.accept;}}
 for(const step of require('./review-logging-chain.cjs')()){const c=caps.find(c=>c.id==='M26');c.sourceIds.push(src(step.source.path,step.source.token));c.gap+=' '+step.fact;c.accept+=' '+step.accept;}
 for(const step of require('./review-parallel-findings.cjs')()){for(const id of step.caps){const c=caps.find(c=>c.id===id);c.sourceIds.push(src(step.source.path,step.source.token));c.gap+=' '+step.fact;c.accept+=' '+step.accept;}}
 const editItems=require('./build-editing-transactions.cjs')();const editCap=caps.find(c=>c.id==='M08');for(const x of editItems.filter(x=>['delete-undo','history-failure','sequence'].includes(x.id))){editCap.sourceIds.push(src(x.source.path,x.source.token));editCap.gap+=' '+x.current+' '+x.failure;}
 // category; observed weakness / uncertainty; VM mechanism worth evaluating; proposed concrete action / cost.
 const rows=[
 ['M01','暂不能判断','启动恢复超时后继续启动且观察迟到任务；尚未闭合迟到恢复与切方案的身份隔离。','全局入口就绪检查可参考，不能据此证明VM所有失败回滚完备。','先追ObserveLateCameraRestoreAsync及会话Owner；如缺代次拒收，再加启动/方案代次，代价是恢复状态和回滚测试。'],
 ['M02','暂不能判断','已有StopAndWaitAsync切换门槛，不应误称只有窗口或完全没有等待；加载失败回滚的完整链待核。','参考原版保存决策、ResetResource和方案替换分阶段职责。','先核PrepareSolutionTransitionAsync所有返回分支及加载提交点；只对确认缺边补事务，成本为旧模型保留和恢复验证。'],
 ['M03','必须整改','已有版本/缺插件校验、临时替换和上一版本恢复；但文件提交前修改FilePath和通信设置且失败无内存回滚。必须局部整改；格式整体优劣仍暂缓。','参考VmSolution导出并发标记和保存完成回调；普通模式错误传播仍有限制。','以候选副本规范化并写入，成功后提交内存路径/设置；区分文件、索引、通知失败，保留已有临时替换。版本迁移和格式更换另评。'],
 ['M04','暂不能判断','分类/拖入已见，工具可见不证明依赖就绪；创建失败回滚未完整追。','参考ToolItemInfo→CreateModuleByInfo的发现、创建、ready分段。','对节点/参数/实例逐一登记回滚并做缺依赖验收；搜索另列缺口而非用分类冒充。'],
 ['M05','暂不能判断','删除Tab不等于跨流程绑定与运行实例同步退役；完整消费者未齐。','参考流程稳定身份与Group显示目标分离。','补删除引用清单和撤销重挂证据，确认缺口后集中编辑事务；成本为命令历史迁移。'],
 ['M06','暂不能判断','目前只有交互服务入口证据，多选/取消/Undo一致性未逐组合核完。','VM路由对无关节点移动不重算不是普遍更优，不能为仿制照搬。','先补拖动提交与取消消费者及同坐标命中测试，再选命令改造；暂无更换整个交互层依据。'],
 ['M07','暂不能判断','执行前已有WorkflowDefinitionValidator循环检查；执行计划另经OutputBindingChangeService核作用域、画布上游和BindingContractValidator类型。拖线局部检查不能代替全链，也不能反推全系统无验证。','VM原版粘贴的同父级合法上游规则可作合同对照，不证明端口全覆盖。','复核拖线/粘贴/加载等入口与现有验证的反馈一致性；对确认缺边复用已有规则，不新增第二套绑定或环路模型。'],
 ['M08','必须整改','节点连线Undo已有，但删除后的绑定/全局关系/子流程清理在命令外；显示序号释放未随Undo重新占用，失败Pop历史也未补偿。','参考原版复制映射、撤销操作和模块源切换释放分工。','追删除命令保留对象及最终释放；只有确认不安全时重构Owner，代价为历史对象寿命政策。'],
 ['M09','必须整改','已确认终态映射依赖Errors、循环间隔无取消令牌、根展示缓存拍平子调用身份；需局部整改，不推出子图算法共用或整体迁VM。','原版Group身份、运行目标与显示模块集分离值得逐能力对账。','拆控制语义验收表；优先补嵌套、输入、取消的实际边，再决定子程序与Group是否独立建模。'],
 ['M10','暂不能判断','已有小地图/视口入口，跨DPI和缩放命中无联合结果；不能据组件存在评价优劣。','原版路径更新时机可作行为参考，不作为性能优胜证据。','先统一几何与坐标合同并测布局恢复；暂无整体更换控件依据。'],
 ['M11','借鉴VM重做','当前工具栏/F5运行SelectedTab，和VM方案级工具栏单次不是同一业务范围。','原版方案入口与VmProcedureDecorator所属流程入口明确分开。','重做运行入口语义及文案：方案单次与当前流程单次分别绑定显式Scope/ID；可以调用现有服务，不能把换文案算调度完成。'],
 ['M12','借鉴VM重做','Tab执行依赖选中状态切换，目标选择与显示导航耦合，给异步重入增加风险。','原版RunOnceCommandExe运行所属流程、不改变DisplayModuleSet。','重做Tab命令目标传递，直接携带根流程ID，内部画布保持原位；成本是重接命令和状态投影。'],
 ['M13','暂不能判断','全局连续和Tab连续共用设备/状态的仲裁全链未闭合，不能直接推荐引擎。','参考方案、流程连续分入口；原生内部调度和停止完成尚有未知。','先核全局启动/停止真实消费者和失败政策，再定统一执行会话；不凭托管函数名推原生等价。'],
 ['M14','暂不能判断','按流程连续已见，但与全部连续重入的服务准入及设备共享需核。','借鉴所属流程身份寻址，而非切换当前显示流程。','补每流程运行会话键和共享资源冲突规则；根据实际锁与消费者决定迁移，不预判需全引擎重写。'],
 ['M15','必须整改','ExecuteStopWorkflow请求StopContinuousRun后立即置非运行并记录已停止，UI把请求误报为完成。另确认ReadyQueue取消退出只释放剩余租约、未等待全部running Task，外层返回与节点排空可能分离。','VM流程有工作状态回调/等待标志；并未证明全部原生任务可中断。','停止先进入Stopping，真实完成/超时后投影终态；引擎需持有并等待所有在途节点再释放资源；StopAndWaitAsync仅看宿主active不能补偿提前返回。不强杀线程；代价是会话任务集合、终态和超时保留策略联动。'],
 ['M16','暂不能判断','显示源切换、历史、底图和叠加的最后读者未闭合，不能推荐现有共享方式。','原版ModuleResultView.DoSourceChanged释放旧decorator、ClearDataStore再建新源；SDK缓存内部未知。','以源/方案/帧身份重画数据流，确认历史是复制还是保留租约后选方案；两者有内存/吞吐取舍。'],
 ['M17','借鉴VM重做','目前输出树/表投影不能代替字段身份、动态失效、模块历史和大数组分段消费的完整设计。','借鉴原版模块源decorator生命周期及字段IO与专业视图分工，不复制未知SDK。','重做结果投影会话：稳定字段键、执行代次、选中/展开恢复、大数组懒加载；底层契约是否复用需独立核，不泛称保留结果树。'],
 ['M18','必须整改','变量管理器Save忽略SaveSolution布尔返回却报成功；Import直接追加并更换Id，未调用改名已有的重名校验。Value列是设计值，本轮生产值另存；容器复制和Clone不深拷Value。','VM原变量UI/VariableManager有分组、类型与导入导出状态处理；原native事务和最终值所有权仍未知，不能据此整体迁移。','先修保存结果反馈和导入全批预检/冲突/引用映射；明确设计初值与本轮值投影，按值类型补复制/借用合同，复用已有生产者/消费者图，不重造所有变量模型。'],
 ['M19','必须整改','公开单帧CaptureAsync没有取得预览/工作流相同用途租约；停止非取消异常可跳过流任务等待而释放占用。整改这两条路径，不推出全驱动重写。','VM全局配置及相机注册/注销是参考；厂商驱动内部资源政策未知。','统一所有采集入口用途准入，停止抛错仍观察流任务结局；未排空保持故障隔离占用。逐厂商和多相机同步另验，不整包迁移VM。'],
 ['M20','必须整改','管理页、读写和事件入口不证明逐协议可靠性及后台准入。','参考原版全局通信配置与窗口关闭注销；未恢复的协议边不能推断不支持。','先逐协议追服务入口、重试幂等及后台身份，再决定通信会话整改范围；必须验证拒绝时零写入。'],
 ['M21','必须整改','主题切换先改当前状态/删旧字典再加载校验新字典，失败无回滚且同主题重试提前return；仅主题事务需整改，帮助完整性另待核。','只按实际菜单/控件职责对账，不按截图颜色评架构。','新主题先构造校验，再在UI线程替换，成功后更新当前状态；失败保持旧主题可重试。设计器/帮助/日志另按各自入口验收。'],
 ['M22','暂不能判断','已有异步Closing和方案切换等待；全部调试/设备/回调是否进入同一门槛尚未证。','原版参数页Window_Closed有注销/Dispose，不能据此推全应用排空。','画退出Owner表，逐资源注册等待和失败恢复；证实漏注册才整改，不用现有统一门槛名当完成。'],
 ['M23','新增','ExecuteShowSettings是占位；不能因为存在Command就计统一设置功能。','按原版真实设置能力挑产品需要项，不仿造空入口。','新增设置模型、持久化与生效规则后接窗口；需求未定时禁用/隐藏，已有相机专用设置不重造。'],
 ['T01','暂不能判断','统一窗口与上下文创建已见，但专业控件初始化失败和替换退订未全核。','原版专业参数树/专用Control统一接入提供对账范围。','先逐工厂配对创建/初始化/释放与会话身份，暂无统一壳整体优于或劣于VM的证据。'],
 ['T02','暂不能判断','已有字段ID消费者、变更对账、执行前作用域/上游/类型检查；List<>或未声明对象合同会进入PendingValidation。完整旧文件迁移与所有嵌套输入仍待核，现无整体重做依据。','参考原版合法上游/父级作用域及IO更新；不能由VM声明推其动态类型边界已经闭合。','核现有字段ID重映射、旧SourceProperty迁移及List<>/嵌套输入政策；只扩展已确认缺的合同，并复用现有验证器。'],
 ['T03','必须整改','DiscardPendingEdits仅取消控件待提交值，不回滚已live写入模型；不能对用户承诺通用取消回滚。','原版每参数页提交政策未全部恢复，不宣称VM所有编辑都有事务。','先标清即时生效或草稿；要求可取消的参数页加业务快照/明确提交点；设备副作用不可假装回滚，成本是逐工具改接。'],
 ['T04','必须整改','默认ExecuteToolAsync=Task.FromResult(ExecuteTool())，任务返回不等于后台执行，契约不能暗示自动异步。','原版SelfRun走专用模块执行入口，原生线程内部仍未知。','整改执行契约：定义线程、取消、快照和终态；逐工具覆写核线程亲和，不能简单Task.Run包所有设备/native调用。'],
 ['T05','借鉴VM重做','当前成功回调加500ms重触发主要在窗口壳，完整失败/取消/关闭会话边尚未闭合。','原版连续等待模块回调；Stop关闭标志、取消循环并等待循环任务退出。','重做可等待的调试会话调度，窗口投影状态；借鉴等待与解绑职责，不照搬300ms也不冒称VM可中断原生算法。'],
 ['T06','必须整改','停止分支清连续标志并恢复界面，未在该入口等待当前算法；停止下一轮与本轮结束混淆。','原版停止连续任务退出/解绑有实码，当前原生调用中断仍待证。','引入StopRequested/Drained两阶段，界面说明停止范围，运行终态后才允许会话退役；配合T05不可另造循环。'],
 ['T07','暂不能判断','实际有提交/校验/Close闭环；默认ValidateParameters=true不证明每工具校验完备。','原版专业参数页提交策略需逐页恢复，不能据统一按钮宣称完整。','列各工具校验与模型写入点，确认缺校验时补；不因默认true判所有工具都无校验。'],
 ['T08','必须整改','Closed清标志和退订不等于在途执行排空；必须修订关闭完成合同，具体释放缺边逐工具核。','参考原版Window_Closed注销服务、AlgorithmTabRoot.Dispose/DoFinish的配对职责。','关闭请求先阻止新轮次，等待本会话终态再退订/释放；禁止先Dispose在用帧，成本为异步Closing与失败恢复。'],
 ['T09','暂不能判断','ROI控件存在，但全部图元模型提交、撤销与序列化未逐形状闭合。','原版专业ROI输入、显示图元与结果层应分开；Group专参数UI不可由通用ROI代表。','按形状列交互事件→模型→持久化→算法消费者；证据齐后决定共用还是专业实现。'],
 ['T10','暂不能判断','阈值预览修正有覆写，不能推广到全部算法已应用同一矩阵。','只参考原版Fixture/坐标职责；当前对应原生计算链不足。','逐矩阵列来源、坐标空间、代次、奇异处理与算法消费；显示正确不等测量正确。'],
 ['T11','暂不能判断','共享父变换和全屏恢复已有代码；没有跨DPI/重复切换与VM同条件比较。','ImageViewBox比例/命中/质量的分工可作测试基线。','先验同像素点和绑定恢复，再选改造；不能以重写成本推现控件更优秀。'],
 ['T12','必须整改','文字fingerprint依赖对象/数组身份与长度，不能自行保证内容原位修改或DPI变动的缓存有效性。','VM分组DrawingVisual/TextShape只证明另一渲染机制，不证明性能更好。','必须明确缓存失效合同：不可变内容版本或逐项更新通知，键含必要DPI/字体参数；先核消费者，不能把条件性风险写成已复现故障。'],
 ['T13','必须整改','结果传播已有完成事件，但失败/取消/迟到结果必须有身份和终态消费合同，不能凭退订完成认定安全。','参考原版执行序号查询及模块源切换释放，缓存内部仍未知。','按方案/Run/Node/Frame信封验收所有接收方；核现有门槛后补缺者，失败不得沿用旧成功为本轮。'],
 ['T14','新增','基类ResetParameters只写日志；本轮tools扫描未见对应覆写，标准重置语义未实现。','原版重置的逐工具默认值尚需材料，不能凭命令名克隆。','新增默认参数、绑定/ROI是否重置与Undo策略；未实现前不记录成功或暴露可用按钮。'],
 ['T15','暂不能判断','脚本引用/编译/输出链存在；AssemblyLoadContext不是安全隔离。','原版脚本模块范围作参考；当前没有充分材料证明其沙箱/安全政策。','明确可信脚本或进程隔离需求，追动态端口失效与程序集卸载；再选实现，不先推荐保留脚本架构。'],
 ['T16','借鉴VM重做','当前输入/输出/显示编辑页未证明Group控制语义、嵌套作用域和专用运行页等价。','参考原版DisplayModuleSet配置目标、稳定模块身份及合法父级输入。','重做子程序合同编辑与运行消费者闭环，按稳定端口ID迁移/撤销；专参数运行页单独列，非把整个Group原生算法照搬。'],
 ['T17','暂不能判断','SaveCalibration写候选而非正式发布；正式采用消费者及另外九类标定不由N点代表。','标定恢复模型各工具输入/文件/坐标契约分别对账，不以重建通过证明原版等价。','追候选验证→正式发布→算法读取，核失败保护；尚不决定替换求解器或文件合同。'],
 ['T18','暂不能判断','模板加载/清除入口不闭合模型句柄最终析构，其他专业工具范围仍未逐个核。','以VM工具入口逐项列能力，不从一个匹配编辑器推全算法支持。','补创建/编辑/保存/取消/删除的Owner与算法消费；没有这些证据不推荐统一重写或保持现状。'],
 ['X01','暂不能判断','可复用WriteableBitmap缓存与帧租约不能证明历史隔离；最后读者全链不足。','原版清decorator/显示缓存不证明native缓冲所有权，仍需接口实现。','逐类型列Allocate/Lease/Clone/Dispose与保留策略；按实际消费者选复制点，不一律Clone或共享。'],
 ['X02','必须整改','Workflow和Plugin.SDK仍UseWPF，不能当作已完成运行/UI解耦；部署存在不证明兼容/卸载。','参考原版托管SDK、控件与工具层分责，但不把原厂包布局视为唯一正确分层。','整改真实依赖边界，先列消费者再拆纯运行合同/UI扩展，版本与构建部署联动；成本是插件迁移和干净发布验证。'],
 ['X03','新增','当前缺独立产品狗授权服务；用户已确定自有狗，不能用VM授权填空。','仅参考授权准入所在业务层，不采用其狗协议/密钥。','新增厂商适配、许可快照、服务准入与签发/恢复政策；型号未定不能写成已实现。'],
 ['X04','暂不能判断','当前Repository是JSON明文读写；哈希不等加密，狗授权不等文件保密或插件可信。','原.sol密码/原厂狗不自动成为可采用方案。','先冻结保密、完整性、签名与恢复需求；需保密再新增格式和密钥包装，不能无需求就推荐照搬VM。'],
 ['X05','暂不能判断','无双方同条件正确性/尾延迟/长稳结果；观测日志及容量结构不证明稳定更快。','VM商业成熟不能代替本次同场景测量；重建项目测试也不是原版测试。','冻结数据/环境/质量门槛和基准，另行授权运行；缺测不排名，此轮只记录计划。'],
 ['M24','必须整改','状态栏IsRunning来自根协调器，会投影M15请求后立即非运行；耗时快照与状态文案需一致。','原版流程完成回调可作终态来源参考，原版状态栏字段全链尚未核。','接统一执行终态，Stopping期间不显示完成；核Shutdown调用及切方案快照失效，不能另写一套计时状态。'],
 ['M25','暂不能判断','专业分析普通成功才推送，相机合并刷新未直接推送；切节点缓存无成功门禁，与实时入口不一致。工具所有者未闭合，具体旧值呈现待验证。','只完成VM专业数据页范围对账，原版具体字段和关闭路径仍缺实现材料。','统一所有入口的成功/失败/取消有效性；保留旧成功必须标历史。先核工厂返回共享或独立实例再定Dispose；不因独立控件就评优。'],
 ['M26','必须整改','日志聚合器先TryDequeue再查批次容量，超过200条时额外取出一条却不交付；Flush仅固定等10ms。显示5000条上限不代表聚合队列有界。','原版应用日志、模块历史和结果表不同，VM日志数据管线待补。','先修出队条件顺序；Flush使用序号屏障和写入确认，UI/文件通道分开背压；退出排空与Dispose后写入策略明确。性能收益另测，不把可靠性问题推迟到压测。']
 ];
 const large=new Set('M03 M09 M11 M12 M15 M17 T03 T04 T05 T06 T08 T12 T13 T16 X02'.split(' '));
 const small=new Set('M04 M10 T01 T11'.split(' '));
 const counts={};for(const [id,category,defect,borrow,action]of rows){const c=caps.find(c=>c.id===id);if(!c)throw Error(id);c.previousRecommendation={choice:c.choice,reason:c.reason};c.choice=category;c.defect=defect;c.vmBorrow=borrow;c.reason='依据：'+defect+' VM参考边界：'+borrow+' 结论仅针对列明范围，不作整体优胜排名。';c.change=action+' '+c.change;c.difference=c.state==='unimplemented'?'缺失':large.has(id)?'差异大':small.has(id)?'差异小':'待核实';c.differenceBasis=c.difference==='缺失'?'限定本项列明入口/合同尚未实现，不推全产品不存在。':c.difference==='差异大'?'列明的业务范围、状态合同、数据/依赖或文件模型有重要差异；不同不自动等于较差。':c.difference==='差异小'?'仅限已见基础入口/交互职责相近，不代表内部算法、稳定性或生命周期等价。':'VM或自研对应实现/运行证据未齐，暂不分大小。';c.adoption=category==='新增'?'新增':category==='借鉴VM重做'?(['M12','T05'].includes(id)?'VM机制优先':'混合优化'):category==='必须整改'?(id==='T03'||id==='T12'||id==='X02'||['M18','M03','M19','M21','M09','M26','M20','M08'].includes(id)?'自研整改':'混合优化'):'暂缓决策';c.adoptionBasis=c.adoption==='VM机制优先'?'建议采用VM已确认的目标寻址/会话等待职责模型，用自有代码接入；不照搬原厂二进制，不宣称原生中断已等价。':c.adoption==='混合优化'?'参考VM已确认的职责/行为，结合自有数据与服务实现；不复用VM二进制、协议或密钥。':c.adoption==='自研整改'?'针对本项目已见合同/实现修正，尚无采用VM具体实现的充分依据。':c.adoption==='新增'?'当前缺口按本产品需求开发；狗授权使用自有厂商方案。':'等待证据/需求后评审，没有已经选定的最终实现。';c.selectionStatus='方案建议，产品尚未实施/验收；本轮没有直接采用VM逆向代码的项目';c.reviewDecision={category,defect,borrow,action,difference:c.difference,adoption:c.adoption,confidence:category==='暂不能判断'?'选型暂缓；已见局部代码不等全部闭合':'代码事实与设计要求分列；整改/借鉴范围不等产品已验收',sourceIds:c.sourceIds};counts[category]=(counts[category]||0)+1;}
 if(rows.length!==caps.length||new Set(rows.map(r=>r[0])).size!==caps.length)throw Error('decision coverage');
 const script=caps.find(c=>c.id==='T15');script.knownGaps=['显示设置Tab仅TextBlock占位：局部未实现，需新增或隐藏'];script.current+=' 本轮确认显示设置Tab仅占位；已有专用ResetCode不能等同标准参数重置。';script.defect+=' 新确认局部缺失：显示设置仅占位；ExecuteTool中Task.Run后GetAwaiter().GetResult同步等待调用方。';script.sourceIds.push(src('tools/SunEyeVision.Tool.Script/Views/ScriptDebugControl.xaml','显示设置（占位）'),src('tools/SunEyeVision.Tool.Script/Views/ScriptDebugControl.xaml.cs','protected override object ExecuteTool'));
 for(const o of optimizations){const cs=caps.filter(c=>c.optimization===o.id);o.decision='逐项复审取舍：'+cs.map(c=>c.id+' '+c.choice).join('；')+'。不得以任务包统一宣布保留。';}
 fs.writeFileSync('artifacts/validation/entry-recommendation-review.json',JSON.stringify({date:'2026-10-21',reviewed:caps.length,originalReviewed:46,added:3,counts,policy:'完整选型优胜结论尚未建立；原版恢复与独立重建分开；代码缺口、待证调用边和未运行验证分开',items:caps.map(c=>({id:c.id,...c.reviewDecision}))},null,2));
 return counts;
};

