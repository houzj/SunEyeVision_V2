// Static source review. Does not execute tools, devices or reconstructed VM code.
const fs=require('fs'),crypto=require('crypto');
module.exports=function(){
const root='tools/SunEyeVision.Tool.',sdk='src/Plugin.SDK/Models/Communication/';
const rows=[
{id:'Condition',route:'混合整改',difference:'规则语义及输入门禁待对齐',layout:'规则表：输入绑定 → 数值范围/期望文本 → OK/NG → 目标节点；不是图像/ROI工具。',
 chain:'ConditionDebugControl.ExecuteTool → ConditionTool.ExecuteCore → ConditionInputResolver → TryMatchRule → NextNodeIds → 执行器控制信号剪枝',
 parameters:'每条规则有类型、输入、范围或期望文本、判定方向及目标节点。目标候选来自下游节点。',algorithm:'遍历所有规则，可同时命中多个目标；没有命中时无默认else。数值为闭区间，字符串为区分大小写等值；期望文本为空时非null即通过。',transfer:'节点结果解析为规则输入，输出是目标节点ID集合与命中规则名，不是图像。',cache:'本次执行新建结果并收集目标；此函数没有跨轮结果缓存。',release:'本工具不建立原生图像/连接；规则循环未见取消检查。外层异常/取消完成与旧结果清理仍需联查。',
 defect:'绑定数字字符串先命中IConvertible.ToDouble(null)，采用当前文化；常量解析采用InvariantCulture，解释规则不一致。局部绑定解析未检查源结果成功状态，端到端是否由引擎屏蔽仍待核。',
 vmKey:'Logic/IfBranchModule',vm:'原资源声明BranchMatch、CanSubscribe=False等；另一个Logic/IfModule是逻辑检测合同，不能合并为同一迁移对象。原生多命中/无命中实现仍缺。',
 change:'统一失败源门禁、有限数和文化转换；显式展示多命中/无命中规则。保留控制信号接口只基于其职责，不能据此判整体优于VM。',accept:'常量/绑定在不同Culture下同义；失败源、空/多元素数组、NaN/Infinity有明确诊断；多命中/无命中及取消边界可验。',refs:[['ConditionTool.cs','private static ConditionResults ExecuteCore'],['ConditionTool.cs','convertible.ToDouble(null)'],['Models/ConditionInputResolver.cs','ResolveBinding'],['Views/ConditionDebugControl.xaml','Rules']]},
{id:'ProtocolAssemble',route:'原文本模式整改＋按需新增',difference:'VM声明脚本模式；当前仅文本拼接',layout:'组装行列表、输入绑定、分隔符、增删/上下移动；结果由通信分析面板显示。',
 chain:'ProtocolAssembleEditor.ExecuteTool → ProtocolAssembleTool.Run → 冻结PartId/InputText行 → ProtocolBindingInput → Format → Join → UTF8 → PublishOutputs',
 parameters:'每行有稳定PartId和输入表达式；分隔符与行顺序决定输出。空/重复身份拒绝。',algorithm:'string/int/long/有限float/double转文本后拼接，再严格UTF8编码；不是任意对象序列化或直接向设备发送。',transfer:'同时输出稳定字段的文本和BinaryPayload；分析行用于查看过程，不是可绑定输出。',cache:'每次新建行数组、文本和字节载荷，无跨轮算法缓存。BinaryPayload构造与ToArray分别复制字节。',release:'纯托管字符串/字节；每行和发布前检查取消，失败/取消清空业务输出，不建立图像或设备句柄。',
 defect:'当前没有字节字段布局、校验和、脚本模式；未见本函数载荷大小准入。不能标完整覆盖VM组装。',
 vmKey:'Communication/DataAssembleModule',vm:'原声明AssembleType、Separator、PyScriptPath与文本/字节输出；声明证明模式入口，不证明脚本执行隔离和错误路径。',
 change:'先定义最大载荷和拒绝策略；文本/字节布局/脚本分成显式模式。脚本待需求与原实现材料后设计自有隔离，不能直接复用未知代码。',accept:'Unicode和非法代理字符、空项/分隔符、重排行、取消、失败源和载荷上限；文本与UTF8字节一一对应。',refs:[['ProtocolAssembleTool.cs','public CommunicationToolResults Run'],['ProtocolAssembleTool.cs','private static string Format'],['Views/ProtocolAssembleEditor.xaml','PartsGrid']]},
{id:'ProtocolParse',route:'混合优化',difference:'缺脚本模式；字节/字段合同不同',layout:'文本/字节模式 → 输入/编码/分隔符 → 输出字段表 → 所选字段字节范围/字节序/解释；不是ROI界面。',
 chain:'ProtocolParseEditor.ExecuteTool → ProtocolParseTool.Run → 冻结字段 → ProtocolBindingInput → Split或ProtocolValueCodec → PublishOutputs',
 parameters:'稳定字段GUID、名称、类型、起止字节、字节序和数值解释。行顺序在文本模式中决定片段映射，不只是显示排序。',algorithm:'文本保留空片段，不足报错、多出只作分析；字节模式显式HEX/文本，起止含两端，经Codec解码后int转long、float转double。',transfer:'先收集所有字段，再完整发布；输出字段与分析行分离。来源失败被ProtocolBindingInput拒绝。',cache:'字段本轮冻结为数组；字节转换及切片有复制；无图像/原生缓存。',release:'每字段和发布前检查取消；失败/取消ClearOutputs。无设备所有权，纯托管字节不需要native Dispose。',
 defect:'没有原声明的脚本解析模式。重排字段会改变文本映射，必须提示；截断多字节字符、符号和字节序需可视说明。',
 vmKey:'Communication/DataAnalysisModule',vm:'原声明AnalysisType、PyScriptPath、txt/shell/byteOutputParams；原生解析分派、脚本执行及释放缺实现证据。',
 change:'增加字节范围预览和字段变更影响提示；按需求补类型/脚本，不用VM字段名称冒充兼容。现有严格类型仍需逐边界验收。',accept:'不足/多余/空片段、多字节UTF8截断、1–4字节整数符号、四字节序、NaN、字段重排/删除后的绑定诊断。',refs:[['ProtocolParseTool.cs','public CommunicationToolResults Run'],['Views/ProtocolParseEditor.xaml','OutputsGrid']]},
{id:'ReceiveData',route:'混合整改＋队列待新增',difference:'队列接收明确未实现；编辑动作产生设备读取',layout:'来源 → 设备 → 目标点或变量行 → 执行；通信分析展示来源/当前值/时间，不是图像区域。',
 chain:'ReceiveDataEditor.ExecuteToolAsync → ToolExecutor → ReceiveDataTool.ReadAsync → Active ReadAsync / Passive ConsumeAsync / ReadVariables → 类型检查 → PublishOutputs',
 parameters:'设备/目标或变量行和稳定输出字段；只有宿主确认被动源时采用ExternalDataWait，其他路径仍LegacyExclusive。',algorithm:'主动读等待设备；被动读按消费者游标取报文并在回调成功后提交；变量读本轮运行值。其他SourceKind明确抛队列未实现。',transfer:'输出标准化为声明类型，类型变化拒绝；被动报文含Sequence/Generation，不能写成所有节点共享最新一条。',cache:'宿主被动缓存有256报文/4MiB/4096消费者边界；消费游标和连接代次在宿主服务，不由工具创建独立连接。',release:'执行读取传取消令牌并在失败/取消清业务值；工具不Dispose共享连接。编辑选点另行ReadAsync未传取消令牌，关闭窗口不能由此取消。',
 defect:'TargetChanged选择Native类型目标便主动读取设备；编辑操作与执行副作用混合。队列收取未实现；缓存溢出和重载策略需用户可见。',
 vmKey:'Communication/ReadDatasModule',vm:'原声明QueueItemList、GlobalVarList、CommunicationItemList及PLC/Modbus入口；原生消费/溢出/停止语义仍缺，不能据声明判运行完成。',
 change:'类型选择优先用元数据；确需探测时提供明确可取消动作，关闭/换目标取消。禁用队列入口或独立新增；定义消费者重置与溢出恢复。',accept:'双消费者独立消费、取消不推进、断线/配置更换拒旧代次、溢出可诊断、关窗取消探测、变量失败不回退旧初值。',refs:[['ReceiveDataTool.cs','private static async Task<CommunicationToolResults> ReadAsync'],['ReceiveDataTool.cs','队列接收尚未实现'],['Views/ReceiveDataEditor.xaml.cs','private async void TargetChanged'],['Views/ReceiveDataEditor.xaml','SourceKind']]},
{id:'SendData',route:'混合优化',difference:'队列未实现；发送非可回滚事务',layout:'目的地 → 设备/事件/变量 → 发送行与输入绑定 → 协议选项 → 执行；专业结果展示已提交/失败/跳过。',
 chain:'SendDataEditor.ExecuteToolAsync → 参数复制 → SendDataTool.SendAsync → PrepareAsync全部计划 → 逐计划SubmitAsync → 宿主会话复核 → Transport',
 parameters:'行身份、目标、输入和声明类型；Raw/事件分组，变量/普通设备按行计划；IgnoreErrors影响预检/提交失败政策。',algorithm:'先准备后提交；不可撤回已提交单元，不自动重试。代码atomic只控制错误跳过策略，不提供设备事务或回滚。',transfer:'保留Submitted/Failed/Skipped和已提交载荷作诊断；失败/取消清业务输出，但不抹掉已产生副作用的事实。变量发布运行输出而非覆盖设计初值。',cache:'PreparedCommunicationSend单次提交，不是可复用重试缓存；共享连接由宿主保管。',release:'提交链接设备寿命令牌；工具不销毁共享连接。取消停止剩余计划，不保证撤销正在外设执行的动作。',
 defect:'队列目的地未实现；部分成功必须明确显示。编辑模式切换会重建行，删除后配置失败的本地回滚仍待核。Modbus类型支持范围有限，不能宣称所有类型。',
 vmKey:'Communication/SendDatasModule',vm:'原声明事件、设备、DataCenter、IgnoreDuration、IgnoreErroneousDataEnable等；提交顺序/确认/重试的原生实现未知。',
 change:'保留Prepare/Submit职责边界并补部分成功说明；队列/类型按需求补齐，编辑映射走可恢复事务。重试需设备确认或幂等合同，不直接失败重发。',accept:'IgnoreErrors=false时任一预检失败零外发；第2提交失败仍报告第1已提交；取消不续发；旧方案/连接计划拒绝；二次提交拒绝；不支持类型明确报错。',refs:[['SendDataTool.cs','private static async Task<SendDataResults> SendAsync'],['SendDataTool.cs','await plan.SubmitAsync'],['Views/SendDataEditor.xaml','DestinationKind']]}
];
function ref(file,token){const t=fs.readFileSync(file,'utf8'),ls=t.split('\n'),i=ls.findIndex(l=>l.includes(token));if(i<0)throw Error(file+' '+token);return {path:file,line:i+1,token,sha256:crypto.createHash('sha256').update(t).digest('hex'),snippet:ls.slice(i,i+18).join('\n')};}
const vmPath='D:/MyWork/REA逆向测试/docs/architecture-recovery/tool-plugin-map.json',vmText=fs.readFileSync(vmPath,'utf8'),vmData=JSON.parse(vmText);
for(const r of rows){r.sources=r.refs.map(([f,t])=>ref(root+r.id+'/'+f,t));delete r.refs;r.sources.push(ref(sdk+'BinaryPayload.cs','public sealed'),ref('src/Plugin.SDK/Execution/Results/CommunicationToolResults.cs','PublishOutputs'));r.status='自研局部实现已静态核对；VM仅原始资源合同对照；运行/完整寿命未验收';const original=vmData.tools.find(t=>t.key===r.vmKey);if(!original?.declarations)throw Error('VM declarations '+r.vmKey);r.vmEvidence={path:vmPath,sha256:crypto.createHash('sha256').update(vmText).digest('hex'),key:r.vmKey,declarations:original.declarations,policy:'仅提取原资源声明；不使用currentImplementation推定VM原算法'};}
const extra={Condition:[[root+'Condition/Models/ConditionInputResolver.cs','private static object? ResolveConstant'],['src/Plugin.SDK/Execution/Parameters/OutputVariableResolver.cs','public static']],ReceiveData:[[sdk+'CommunicationReceiveConsumption.cs','public const int MaximumBufferedMessages'],[sdk+'CommunicationReceiveConsumption.cs','token.ThrowIfCancellationRequested(); commit(next)']],SendData:[[sdk+'CommunicationSendService.cs','class PreparedCommunicationSend']]};
for(const r of rows)for(const x of extra[r.id]||[])r.sources.push(ref(...x));
const out={reviewed:rows.length,policy:'读取实现体并保留来源哈希；源码审读不是运行验收，VM声明不是原生实现。',tools:rows};fs.writeFileSync('artifacts/validation/communication-tool-review.json',JSON.stringify(out,null,2));return out;
};
