const fs=require('fs');const file='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';let s=fs.readFileSync(file,'utf8');if(s.includes('id="vm-four-line-round2"'))throw Error('already published');
const esc=x=>x.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;'),url=p=>'file:///D:/MyWork/SunEyeVision_V2/'+p;
const proof=(p,a,b,title)=>{const l=fs.readFileSync(p,'utf8').split(/\r?\n/);return `<details class="vm-proof"><summary>${title} · ${a}–${b}行</summary><p><a href="${url(p)}">完整静态材料</a>；独立Ghidra/PE分析，不是原厂源码或REA Evidence；名称为分析标签，原生类型与间接调用仍可能不完整。</p><pre>${esc(l.slice(a-1,b).map((x,i)=>(a+i)+' '+x).join('\n'))}</pre></details>`;};
let h=`<article class="vm-deep-chain" id="vm-four-line-round2"><h3>四线第二轮：循环执行、停止事件时序、源结果复制与析构</h3><p><span class="pill">新证据：事件创建/置位、循环内调用、默认源结果缓存释放</span><span class="pill pending">仍未闭合：实际节点调度策略、外部排空、扩展缓冲与算法/驱动析构</span></p><p>四线只读分析，主智能体统一生成限定静态证据并更新HTML。本轮继续使用已认证ModuleFrame.dll（SHA256 <code>45c57bc46f72fd1f9e0b3a6f9183ff974d14590cfd05ecf4e9fa97a4ff95ae9b</code>，基址0x180000000）；未执行VM、产品、设备或探针。下述事实仅适用于所列分支，不表示全领域完成。</p>
<h4>A．实际调度：进入运行准备和循环内调用，但节点执行者仍需解析</h4><pre>FrameRun → RunDispatch@180128200
→ ProcedureRun@1800dcc80：锁+0x100，检查运行/状态，保存参数
→ PrepareRun@1800e2720：检查+c8/脏标志，必要时10ae80→deba0
→ ResetEvent(+80/+88/+90)
→ 将dd330和流程this封装提交到15aaf0
→ ProcedureWorker@1800dd330工作循环
→ RCX=[流程+c8]，RAX=[RCX]，CALL [RAX+8]</pre><p>SubmitWork@18015aaf0按类别2选管理对象+0x80，其余选+8，进入1800fb9d0；当前不以类别值命名优先级或线程池。循环中的+c8包含间接可调用对象，RTTI还有std::function/绑定CProcedure的候选；未装配闭合前，不能直接叫算法节点虚表。1800580c0只是准入检查，不是节点调度器。</p><p><strong>比较与推荐：</strong>VM已有流程互斥、参数保存、事件复位及工作循环，不能再归为仅转发命令。自研BuildExecutionPlan→绑定依赖图→资源声明→就绪队列可直接审查，但不证明VM没有同类能力。暂保留自有执行层，按绑定依赖、资源冲突、分支循环、子流程、取消排空五组同场景补证；成本是继续承担图与资源规则一致性，而不是先复制未恢复的VM内部图。</p>`;
h+=proof('artifacts/validation/vm-round2-entry-trace.txt',94,120,'运行准备与事件复位');
h+=proof('artifacts/validation/vm-round2-events-trace.txt',240,251,'循环实际间接工作调用');
h+=proof('artifacts/validation/vm-round2-submit-trace.txt',14,29,'提交目标分支，尚未证明消费策略');
h+=`<h4>B．停止完成事件：+88早于终态报告和尾部回调</h4><pre>CompletionInit@1800de620
  +80/+88 = CreateEventW(NULL, TRUE, TRUE, NULL)
  手动复位、初始有信号；每次Run准备成功后Reset
Stop@1800dd030：Set(+90/+80) → 等+88，当前调用3000ms

Worker循环退出后的静态顺序：
  SetEvent(+88)
  清+e9；可选ReportState(1)；清+7c
  ReportState(3)（含状态回调和末项计数通知）
  调+1c8尾回调 → +1d0=1 → SetEvent(+98)</pre><p><strong>新边界：</strong>停止等待+88只覆盖到工作循环退出信号，信号之后仍有状态通知和尾回调。+98是更晚的完成线索，尚未解析其等待者和异常出口，不能直接称完整线程/模块析构完成。管理器+558另由CreateEventW(NULL,FALSE,FALSE,NULL)创建，由管理操作分支置位；它与流程+88作用域不同，不能混作同一种完成。</p><p>ProcedureStop仅将等待返回0x102转为内部0xe0000300，其余返回继续走0；全流程停止没有汇总逐流程返回。FrameStop导出返回寄存器合同仍待核对。因此“返回成功”“收到+88”“尾回调排空”必须区分。此处记录静态错误传播边界，未运行证明可达故障。</p><p><strong>比较与推荐：</strong>VM多阶段事件有利明确唤醒与退出，但调用者要知道等待哪一阶段；跨层错误传播也有成本。自研Cancel/IsAnyRunning同样不能替代在途任务与原生调用排空。推荐把StopRequested、LoopExited、Draining、Stopped/TimedOut/Faulted分别绑定证据，只有拥有者确认任务、回调和资源借用结束后才允许切换/销毁；代价是逐项结果、事件关联和非协作调用治理，不能靠一个布尔状态完成。</p>`;
h+=proof('artifacts/validation/vm-round2-events-trace.txt',389,425,'退出信号早于状态报告与尾部回调');
h+=proof('artifacts/validation/vm-round2-events-trace.txt',3215,3233,'完成事件创建属性');
h+=`<h4>C．图像源缓冲：部分路径复制，默认结果缓存已有释放实现</h4><pre>GetBinary@1800434d0分派：
  ##ExportModuleData## → 18005dbe0（导出元数据，不统称像素）
  DynamicInData/OutData → 1800a2f90 / 1800e1bc0 → memcpy
  普通模块 → 有+128接口则CALL slot+10
             否则ModuleBinary@1800b9580

默认ModuleBinary：
  table = module+140
  entry = table+58498 + i*118
  源地址entry+108，长度entry+110
  容量不足→e0000012；足够→memcpy_s到调用方缓冲</pre><p>普通模块查找期间持有模块shared_ptr样控制块，调用结束才递减引用；这证明模块对象在本次调用内保活，不证明借出像素的跨调用寿命。默认实现返回独立复制字节而非借出源地址；Dynamic路径的跳板1801e918e已经按原始字节与导入表认证为MSVCR120.dll!memcpy，不是按参数形状猜测。+128扩展接口尚未解析，不能将默认复制行为推广所有模块。</p><p>默认缓存源释放与读取可对应：b9580的entry+108等于table+585a0+i*118；CleanupNode@1800b96a0遍历同一+58490计数及+585a0指针，调用operator delete[]，再operator delete(module+140)。NodeDeletingDestructor@1800b85d0实际调用该清理器。<strong>已补默认缓存最终释放实现，但仍缺填充/替换写入、并发保护及何时触发析构的完整链。</strong>这些字节不自动等同相机驱动缓冲或厂商算法内部像素。</p><p><strong>比较与推荐：</strong>已认证路径可包含源结果到调用方、再到传输映射的复制，利于隔离源寿命，代价是带宽、分配与延迟；没有实测不判性能输赢。自研直接引用/Clone/显示缓存路径成本不同。推荐按实时预览、历史快照和算法执行分别声明复制或带所有者的借用；先补源写入和并发，再决定减少哪一次复制，不采用全局零拷贝口号。</p>`;
h+=proof('artifacts/validation/vm-round2-entry-trace.txt',429,477,'默认模块结果按名字匹配与复制');
h+=proof('artifacts/validation/vm-round2-finalize-trace.txt',134,192,'默认源结果缓存释放，与读取布局对应');
h+=`<p><a href="${url('artifacts/validation/vm-round2-copy-import.json')}">memcpy跳板字节/IAT认证</a>。ImageSharedDestructor@1800f01e0及ImageMappingDestructor@1800f4480另有Unmap/Close实现，但其持有者/作用域尚未对应所有图像源，不把它们当作全部源像素回收器。</p>
<h4>D．模块最终析构：对象析构可定位，删除触发和厂商析构仍有断点</h4><pre>DeleteNode@18010a910 → EraseNode@180085440
→ 树范围查找/删除辅助18006ee20（继续18006fa20）
  当前还未从这条路径连通实际控制块deleter

独立RTTI定位：CModuleNode虚表229e50，slot0→1800b85d0
  NodeDeletingDestructor：
    重置虚表 → b96a0清结果缓存 → 销毁+138互斥
    → 1de70基类清理 → 条件operator delete对象
  BaseCleanup：清字符串/集合/互斥等</pre><p>RTTI与析构体可证明“存在并实现了模块节点清理”，但不证明删除请求返回时所有持有引用均已归零，也不证明厂商算法/设备驱动析构已执行。树删除后的控制块类型、观察者slot+18、112db0后续及特殊ID分支仍须分别追。不能将一个底层CModuleNode析构器升级成全插件安全卸载证明。</p><p><strong>比较与推荐：</strong>VM实例清理包括结果块、同步对象和基类资源，已比目录移除更明确；但管理引用与算法宿主对象之间的关系还未闭合。自研会话幂等/finally便于审计，也须证明异步任务与结果持有者结束。推荐将删除分为停止/排空、解除关系、释放实例、厂商资源结束、目录移除，并保留失败状态；引入合同和追踪的成本，换取删除结果可验证，不要求无证据支持的热卸载。</p>`;
h+=proof('artifacts/validation/vm-round2-resources-trace.txt',5,38,'CModuleNode删除析构实际调用链');
h+=`<p><a href="${url('artifacts/validation/vm-round2-node-vtables.json')}">RTTI/虚表静态索引</a> · <a href="${url('artifacts/validation/vm-round2-finalize-trace.txt')}">容器/基类与清理全材料</a> · <a href="${url('artifacts/validation/vm-round2-resources-trace.txt')}">节点/映射析构材料</a>。</p><h4>下一批必须补的断点</h4><ol><li>实际调度：deba0装配+c8、10ae80构图、间接调用绑定的实际成员函数和节点派发；fb9d0消费端、分支/循环/资源冲突规则。</li><li>停止：+98等待者、+1d0读取者、+1c8安装者、外部code6/12接收排空、异常出口通知与对外返回合同。</li><li>源缓冲：b90d0/b9390等字段写入、+128接口真实实现、每帧覆写/锁及停止/删除期间读者保护。</li><li>析构：6fa20之后控制块deleter及引用归零、观察者接收方、厂商算法销毁函数与设备回调结束。</li></ol><p>本轮仍是局部链路推进，原16域不增加整域完成数；没有运行性能/长稳、设备或UI交互验证。</p></article>`;
s=s.replace('<article class="vm-deep-chain" id="vm-four-line-round1">',()=>h+'<article class="vm-deep-chain" id="vm-four-line-round1">');
const latest='<div class="note vm-progress">第二轮已补工作循环与停止事件时序、默认源结果复制/释放及节点析构体；最新事实和断点见<a href="#vm-four-line-round2">四线第二轮</a>，以下为第一轮记录。</div>';
s=s.replace('<h3>四线第一轮：停止等待、传输映射、模块删除与变量编辑</h3>',()=>'<h3>四线第一轮：停止等待、传输映射、模块删除与变量编辑</h3>'+latest);
s=s.replace('第一轮已补安装配置下ModuleProxy绑定、ModuleFrame实际停止等待和传输映射释放；节点派发、完成事件/回调排空、算法源缓冲和最终析构仍未闭合。','第二轮已补工作循环、+88与+98事件先后、默认结果复制和缓存/节点析构实现；节点派发规则、外部回调排空、源复用保护与厂商析构仍未闭合。');
s=s.replace('四线最新证据</a>','四线第一轮</a> · <a href="#vm-four-line-round2">四线最新证据</a>');
s=s.replace('内部ProcedureStop事件置位与3000ms等待、状态3及计数通知已见','+88循环退出信号早于状态3/尾回调/+98；事件创建与复位/置位已见，当前Stop等+88');
s=s.replace('+88/+558句柄生产者、code6/12到托管等待、外部排空及DLL返回合同未闭合；全部停止未汇总逐项错误。','+88与Manager+558生产者已见；+98等待者、外部code6/12接收排空及DLL返回合同未闭合，全部停止未汇总逐项错误。');
s=s.replace('dcc80之后实际节点派发、分支循环、线程入口与停止事件消费尚未恢复。','dcc80→dd330循环→+c8间接工作调用已见；+c8实际装配与节点派发、分支/循环/资源策略仍未恢复。');
s=s.replace('算法源图像、借用指针寿命及各key读取分支，客户端独立映射持有/并发尚未闭合。','默认源缓存b9580复制与b96a0释放已对应；源写入/复用锁、+128扩展接口及相机/算法内部缓冲仍未闭合。');
s=s.replace('085440容器值deleter、观察者虚表+18、112db0及特殊ID分支仍未证算法/驱动最终析构。','085440→6ee20→6fa20之后deleter/引用归零仍缺；独立已定位b85d0节点析构与缓存释放，厂商算法/驱动析构仍未证实。');
s=s.replace('证据索引、工作包状态', '证据索引、工作包状态');fs.writeFileSync(file,s);console.log('Published four-line round 2.');
