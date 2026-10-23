const fs=require('fs');const file='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';let s=fs.readFileSync(file,'utf8');
const esc=x=>x.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const link=p=>'file:///D:/MyWork/SunEyeVision_V2/'+p;
const proof=(p,a,b,title)=>{const lines=fs.readFileSync(p,'utf8').split(/\r?\n/);return `<details class="vm-proof"><summary>${title} · ${a}–${b}行</summary><p><a href="${link(p)}">完整材料</a>；以下为本次读取时行号，原生材料是独立Ghidra静态反编译/反汇编，不是原厂源码或REA Evidence。</p><pre>${esc(lines.slice(a-1,b).map((x,i)=>(a+i)+' '+x).join('\n'))}</pre></details>`;};
let h=`<article class="vm-deep-chain" id="vm-four-line-round1"><h3>四线第一轮：停止等待、传输映射、模块删除与变量编辑</h3><p><span class="pill">已核实：部分函数实现与真实调用边</span><span class="pill pending">未闭合：调度全图、完成事件生产者、算法析构与全异常寿命</span></p><p>本轮A/B/C只读复核，主智能体D统一审查与写入；不改产品，不执行VM业务、设备或运行探针。覆盖<a href="#plan-C03">C03</a>/<a href="#plan-C04">C04</a>/<a href="#plan-C06">C06</a>/<a href="#plan-C08">C08</a>/<a href="#plan-C12">C12</a>/<a href="#plan-C17">C17</a>/<a href="#plan-C20">C20</a>的部分问题，不能算七个工作包完成。ModuleFrame.dll SHA256：<code>45c57bc46f72fd1f9e0b3a6f9183ff974d14590cfd05ecf4e9fa97a4ff95ae9b</code>，首选基址0x180000000；所有原生VA为静态地址。</p>
<h4>A线：停止已追到实际等待，仍需追事件与外部通知闭合</h4><pre>停止JSON命令3017 / 0xbc9
ModuleProxy分支 @18004d2d1 → 适配器@1800244d0
  读取对象+0x120，尾跳VM_M_StopRunProcedure
ModuleFrame导出@18013c250 → ManagerStop@180043cb0
  单流程：查流程 → ProcedureStop@1800dd030(flow, 0)
  全部：ID10999，遍历流程（跳过10999/100000）调用ProcedureStop
ProcedureStop：检查状态 → 事件置位(+0x90/+0x80)，清+0x7c
  WaitAdapter@1800da1c0(handle+0x88, 3000)
  → WaitForSingleObject；WAIT_TIMEOUT(0x102) → 0xe0000300</pre>
<p><strong>本轮修正：</strong>不能再概括为“VM只有停止请求、没有原生等待证据”。当前调用点第二实参为0，走3000毫秒等待；函数另有非零标志对应INFINITE分支，不能推广到当前调用点。上游托管3u与该常量是否有传递关系仍未证明，不因数值相近认定同一参数。</p><p>状态检查判为非运行时，ProcedureStop调用ReportState(...,3,...)直接返回0；运行路径等待超时明确返回错误。等待封装还会把部分非超时错误转换为GetLastError，调用方仅显式检查0x102：不能把返回0泛化为所有OS异常都已处理。全流程分支未汇总每次ProcedureStop返回值，不能把全流程成功回复解释成每个流程均成功停止。</p><p>ReportState@1800de180写状态、触发回调码6；状态参数为3时减少运行计数，最后一项有回调码0x12。这补实了状态/计数通知路径，但回调码的完整外部合同、+0x88句柄的创建/置位者、原生任务退出和外部回调排空仍未连通。</p><p><strong>具体比较与建议：</strong>VM有显式等待及超时返回，值得吸收；多层状态和全部停止的错误汇总仍需审查。自研Task/取消令牌利于组合，但不可替代原生任务完成合同。推荐单流程与全部停止都返回逐项结果，分别暴露请求接受、终态、回调排空和资源可释放；代价是跨层状态关联和失败恢复，不能仅将Cancel视为完成。</p>`;
h+=proof('artifacts/validation/vm-frame-stop-leaf-trace.txt',27,65,'ProcedureStop：事件、等待与超时');
h+=proof('artifacts/validation/vm-frame-completion-trace.txt',5,39,'等待封装：OS等待及返回处理');
h+=proof('artifacts/validation/vm-frame-completion-trace.txt',388,411,'状态回调与末项计数通知');
h+=`<h4>B线：图像传输映射是复制通道，不能等同算法源缓冲</h4><pre>PlatformManager ReleaseBinaryJson@18068aa20：构建3090 / 0xc12
→ ModuleProxy命令3090@18005671e
→ MappingRelease@180074e00
→ UnmapViewOfFile(当前视图+0x88)，CloseHandle(当前映射+0x58)

WriteBinaryMapping@180074b10
→ 先清旧映射(74e00)
→ CreateFileMappingA → MapViewOfFile
→ 写长度头 → memcpy_s(源字节,映射区+0x10)</pre><p><strong>本轮修正：</strong>已经看到代理侧映射生产、复制与释放实现，不只是释放接口。该传输路径实际复制字节，不能称“VM图像全链零拷贝”。代理释放自己的视图/句柄，不直接证明客户端独立映射同时失效，更不等于算法源图像已释放。原始帧、算法分配、传输映射、查询动态内存和显示缓存必须分开登记。</p><p><strong>具体比较与建议：</strong>传输复制将消费者与源缓冲分开，代价是复制带宽、映射分配及协议头管理；共享映射仍需要客户端持有规则。自研直接持有图像对象少一道传输复制，却承担多消费者引用与替换释放的协调。推荐按对象类别定义借用/保留/复制；下一步追原算法分配与复用、异常清理、并发查询是否共用映射，不能一律零拷贝或深拷贝。</p>`;
h+=proof('artifacts/validation/vm-proxy-buffer-trace.txt',49,85,'映射创建前清理及句柄持有');
h+=proof('artifacts/validation/vm-proxy-buffer-trace.txt',107,125,'Map与实际复制');
h+=proof('artifacts/validation/vm-mapping-release-trace.txt',48,76,'代理视图Unmap与Close');
h+=`<h4>C线：模块删除比移除目录更深，但还未证明算法析构</h4><pre>ModuleProxy命令3006@18004ba8a → DeleteWrapper@1800242c0
  尾跳对象+0x90 → VM_M_DeleteModuleNode
ModuleFrame导出@180139d60
  ManagerCheck@1800441c0(3000) → DeleteNode@18010a910
DeleteNode：查节点所属流程→检查流程状态
  → 清理多处关系/容器 → 通知观察者slot+0x18
  → 后续流程更新；原生算法对象最终析构仍待追</pre><p>ManagerCheck只有在特定管理器标志与+0x558句柄成立时才等待；不能将参数3000解读成“每次删除都会等待节点停止3秒”。DeleteNode在流程状态检查阻止时返回0xe0000300，允许时执行关系清理及通知；这些是实际实现证据，但没有等于驱动断连、算法销毁、共享设备引用归零或DLL卸载。</p><p><strong>具体比较与建议：</strong>VM已经有删除前置约束和关系清理，不宜评价为仅删托管目录。自研可读的实例/租约边界便于检查，但应逐项证明在途执行、编辑会话、结果持有和设备共享。推荐把删除编排成准入检查→停止/排空→解绑→实例释放→目录移除，允许报告部分失败并恢复；代价是事务和重入处理，不能把注销注册表视为热卸载。</p>`;
h+=proof('artifacts/validation/vm-frame-p0-trace.txt',350,365,'删除入口：管理检查后进入节点删除');
h+=proof('artifacts/validation/vm-frame-core-trace.txt',344,378,'管理检查：条件等待，不是所有删除统一等待');
h+=`<h4>D线：全局变量编辑、持久化和运行快照是三个合同</h4><p><strong>自研实际链：</strong>GlobalVariableManagerViewModel.LoadGroups把当前方案变量对象直接加入分组；新增/删除直接改CurrentSolution.GlobalVariables；Save压紧Index并调用SaveSolution；窗口Close_Click只调用Close。已有代码不构成独立草稿事务，“关闭”不能标为“撤销本次变量编辑”。参数快照则由ToolParameterSnapshot通过项目JSON契约复制，不能据此推断变量值也深复制。</p><p>RunSolutionAsync按生产消费依赖运行流程，将无生产绑定的初值写入runtimeGlobals字典，值仍是原对象引用；生产结果提交先Remove旧值，生产失败不保留旧轮次值。方案初值与运行生产值分开已有明确保护；集合/图像内部可变性仍需单独审查。</p><p>ToolDebugResultSnapshot.Capture两次比较上游结果对象身份，捕获期间对象被替换会拒绝调试，避免部分混用；它保留ToolResults引用，没有克隆其字段，也未保证捕获返回后对象内容不变。不能把这个已有保护忽略掉，也不能称为所有数据的不可变快照。</p><p><strong>VM对照边界：</strong>已有变量管理/绑定读写证据不能证明其编辑器也采用草稿或即时提交。本轮新增主要补实自研侧，不据此给双边编辑一致性定输赢。下一步要分别追VM编辑值写入、应用/关闭与持久化入口，再核对运行值快照/提交。</p><p><strong>具体建议：</strong>先向用户明确“即时编辑内存，保存写盘”或改为显式草稿/应用，二者需要选定合同；运行变量保留现有生产失败清旧值保护，按类型明确复制/借用。草稿提高取消可预期性，但增加绑定重映射、校验和提交冲突处理成本；不能只改按钮名字。SDK调试快照继续保留身份一致性检查，后续补对象内容与图像寿命合同。</p>`;
h+=proof('src/UI/Features/GlobalVariableManager/GlobalVariableManagerViewModel.cs',193,207,'变量分组直接引用方案对象');
h+=proof('src/UI/Features/GlobalVariableManager/GlobalVariableManagerViewModel.cs',712,739,'保存负责方案持久化与Index压紧');
h+=proof('src/UI/Services/Workflow/WorkflowExecutionManager.cs',1060,1086,'生产值先清旧值再按成功结果提交');
h+=proof('src/Plugin.SDK/Execution/Parameters/ToolDebugResultSnapshot.cs',41,60,'调试捕获：拒绝结果替换，保留对象引用');
h+=`<h4>本轮证据入口与下一批断点</h4><p><a href="${link('artifacts/validation/vm-frame-exports.json')}">ModuleFrame身份/导出</a> · <a href="${link('artifacts/validation/vm-proxy-leaves-trace.txt')}">代理尾跳适配器</a> · <a href="${link('artifacts/validation/vm-release-build-trace.txt')}">3017/3090构建常量</a> · <a href="${link('artifacts/validation/vm-proxy-buffer-cases-trace.txt')}">代理缓冲分支</a> · <a href="${link('artifacts/validation/vm-frame-run-next-trace.txt')}">运行/算法内存下层候选</a>。</p><ol><li>A：+0x88/+0x558句柄创建及信号源、运行调度实际被调用者、状态3与托管等待解除全链；单流程与全部停止错误聚合。</li><li>B：算法源缓冲分配/复用和显示保留；映射重建、并发与失败路径，以及客户端映射持有期。</li><li>C：删除后实例引用归零、虚表析构目标、驱动/算法释放与特殊ID分支；配置绑定和其他装载方式分别审查。</li><li>D：VM变量编辑到保存的真实合同；自研集合/图像值的可变性与调试捕获后的寿命；扩展其他UI子项。</li></ol><p>上述七个工作包仅更新为“链路部分核实”；16域仍按原保守口径，性能、长稳和真实UI交互未在本轮验证。</p></article>`;
h=h.replace('<h4>B线：',()=>`<p class="note warning"><strong>返回合同仍有断点：</strong>FrameStop导出反编译恢复成void，调用管理器后还有日志和清理；代理读取EAX不独立证明内部错误可靠传过DLL边界，需按原始声明和返回寄存器汇编再核对。上述超时错误是内部函数观察。</p><p><strong>自研对应审计点：</strong>WorkflowExecutionEngine就绪队列正常按WhenAny回收任务，但finally对剩余running仅释放Lease，没有在该块await剩余任务。取消/异常提前退出时，不能仅据UI停止状态认定工具、原生调用及回调已结束。这是定位到实现的条件风险，未运行复现；后续追外层是否还有排空。</p>${proof('src/Workflow/WorkflowExecutionEngine.cs',394,402,'就绪队列退出：剩余租约释放') }<p><strong>运行调度下一落点：</strong>FrameRun→180128200→1800dcc80。1800580c0只做管理器状态准入检查，不是节点调度器；依赖图和节点派发仍须从1800dcc80继续。</p><h4>B线：`);
h=h.replace('<h4>C线：',()=>`<p><strong>显示策略需分开：</strong>自研NodeResultManager既有ImageConverterPool路径，也有CvVisionImageAdapter的像素复制路径。前者以尺寸/格式缓存WriteableBitmap并原对象返回，若多个视图或历史记录保留同一缓存对象，后写可能改变旧显示；尚未追完消费者，属于条件风险，不能称已复现。后者Marshal.Copy到新byte[]再创建BitmapSource，复制与分配成本不同。推荐实时预览与历史快照明确分流，先核对调用者/线程/持有期再改缓存。</p>${proof('src/UI/Services/Performance/ImageConverterPool.cs',43,64,'显示池：按尺寸/格式复用对象')}<h4>C线：`);
h=h.replace('<h4>D线：',()=>`<p><strong>接收方状态升级：</strong>安装ServerConfiguration.ini第44行的ProxyPath为../ModuleProxy/x64/ModuleProxy。ReadConfig取ProxyPath写管理器+0x7d438；LoaderA先读取配置，再将该字段传给ProxyLoader，去.exe后追加.dll并LoadLibraryW，句柄写+0x7d510。配置与代码结合，现可称“本安装配置下的静态绑定”，不再只有同名导出候选；尚无运行时加载观察，也不能据此断言全平台没有IPC。</p>${proof('artifacts/validation/vm-config-stop-trace.txt',423,436,'读取ProxyPath配置键')}${proof('artifacts/validation/vm-loader-trace.txt',150,155,'把配置路径传给代理加载器')}<p>ModuleProxy的ModuleFrame路径构造、Delete槽+0x90及Stop槽+0x120均有字面与指针写入证据；装载帮助函数18008e2b0本体尚未复核，不直接标成LoadLibrary。所谓ProxyUnload@18063c000只见VM_PROXY_DeInit调用，本函数未见FreeLibrary，应称反初始化，不能登记DLL卸载。</p><p><strong>相机对照：</strong>自研CloseAsync在同相机gate下移出会话目录，尝试DisconnectAsync，finally释放CameraSession；Session用Interlocked防重入，再await Adapter.DisposeAsync并finally回调。相机管理窗口Dispose为空，连接属于方案/应用运行时，关窗不等于关设备。适配器是否排空采集回调仍待逐厂商验证，不能以DisposeAsync签名替代。</p>${proof('src/DeviceDriver/Cameras/Contracts/CameraSession.cs',28,41,'相机会话：幂等释放与目录回调')}<h4>D线：`);
if(s.includes('id="vm-four-line-round1"'))throw Error('already published');s=s.replace('<article class="vm-deep-chain" id="vm-receiver-p0">',()=>h+'<article class="vm-deep-chain" id="vm-receiver-p0">');
for(const id of ['C03','C04','C06','C08','C12','C17','C20']){const start=s.indexOf(`id="plan-${id}"`),end=s.indexOf('</article>',start);if(start<0)throw Error(id);let b=s.slice(start,end);b=b.replace('状态：已有材料待按计划复核。',`状态：第一轮链路部分核实，见<a href="#vm-four-line-round1">四线证据</a>。`);s=s.slice(0,start)+b+s.slice(end);const re=new RegExp('(<tr><td><a href="#plan-'+id+'">[\\s\\S]*?<p>)待按统一模板复核(</p>)');s=s.replace(re,'$1链路部分核实 · 第一轮$2');}
s=s.replace('当前20工作包均以此作为保守起点；','计划建立时20工作包以此作为保守起点；后续以各包最新状态为准；');
s=s.replace('中断前本轮新增的ModuleFrame、配置读取、停止及映射静态产物尚未全部回填复核，不在此登记P0完成；之后按上述证据规范与历史章节一起更新。','ModuleFrame停止、映射与删除的部分静态产物已在<a href="#vm-four-line-round1">四线第一轮</a>复核回填；未登记P0完成，事件生产者、算法缓冲寿命与析构仍待后续补证。');
const note='<div class="note vm-progress">第一轮新增证据：停止实际等待、传输映射复制/释放、删除前置与自研变量编辑合同已补，最新范围和限制见<a href="#vm-four-line-round1">四线第一轮</a>；本段保留上一轮追踪记录。</div>';
s=s.replace('<h3>P0续追：统一命令的实际接收方与下一层断点</h3>',()=>'<h3>P0续追：统一命令的实际接收方与下一层断点</h3>'+note);
for(const id of ['ui-globals','suneye-globals']){const start=s.indexOf('id="'+id+'"'),end=s.indexOf('</article>',start);s=s.slice(0,end)+`<p class="note">首轮合同修正：当前自研变量管理直接编辑方案内存对象，保存负责持久化，关闭不等于回滚；运行参数快照、变量字典与调试结果捕获也不是同一种复制。<a href="#vm-four-line-round1">具体调用与取舍</a>。</p>`+s.slice(end);}
fs.writeFileSync(file,s);console.log('Published reviewed four-line round 1.');
