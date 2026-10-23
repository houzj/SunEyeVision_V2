const fs=require('fs'),file='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';let s=fs.readFileSync(file,'utf8');if(s.includes('id="vm-four-line-round4"'))throw Error('already published');
const esc=x=>x.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;'),url=p=>'file:///D:/MyWork/SunEyeVision_V2/artifacts/validation/'+p;
function proof(file,a,b,title){const l=fs.readFileSync('artifacts/validation/'+file,'utf8').split(/\r?\n/);return `<details class="vm-proof"><summary>${title} · ${a}–${b}行</summary><p><a href="${url(file)}">完整静态反编译与反汇编</a>；独立Ghidra产物，不是原厂源码或REA Evidence；分析标签和恢复类型需以实际指令核对。</p><pre>${esc(l.slice(a-1,b).map((x,i)=>(a+i)+' '+x).join('\n'))}</pre></details>`;}
let h=`<article class="vm-deep-chain" id="vm-four-line-round4"><h3>四线第四轮：前驱计数调度、事件代理、写入入口与DestroyModule</h3><p><span class="pill">已核实：真实依赖就绪分派与加载器厂商销毁调用</span><span class="pill pending">未闭合：全部控制结构、外部消费者排空、缓存串行化及厂商内部释放</span></p><p>本轮只读四线继续，主智能体统一保存静态证据和修订HTML。ModuleFrame SHA256沿用<code>45c57bc46f72fd1f9e0b3a6f9183ff974d14590cfd05ecf4e9fa97a4ff95ae9b</code>；ModuleProxy为<code>76f09a40184b2b5f99b7173edde23a716d339e3c64955696d6927495efdf68a9</code>，基址均0x180000000。未运行VM业务、设备或探针，不改产品。</p>
<h4>A．实际节点调度已进入前驱计数与混合分派</h4><pre>ProcedureWorker@dd330 → ffaa0
  ffaa0按分支提交绑定任务、按流程提交，或直接进入ffd00
  CWorkFlow::ScheduleHelper@1800ffd00
    当前节点+20 = +24（计数重新初始化）
    b98c0准入 → 节点虚表slot8执行
    01e310取得相邻列表 → 01e400传播结果状态
    后继节点+38互斥内递减+20
    计数归零才分派：类型2递归，其余经ffaa0提交
    保留一项在末尾直接递归执行</pre><p><strong>本轮修正：</strong>VM不只有流程循环和接口调用，已有邻接遍历、前驱计数就绪判定、结果传播及递归/任务提交混合调度的函数实现证据。前轮ffaa0“可能只是指针setter”的候选解释撤回；1e4e0仍只是节点状态设置，不能与真实派发ffd00混淆。单模块100e00也有准入与slot8调用，但不等同完整流程路径。</p><p>节点类型数值2/3/5的全部语义、分支/循环计数重置、线程消费、同资源冲突和栈深边界尚未核实；因此不把计数调度泛化为所有控制结构正确。</p><p><strong>比较与推荐：</strong>两边已有实际依赖调度机制，不能以“VM仅中央命令层”推荐自研。自研显式计划和资源租约更贴合已定开放设备/插件约束，但须维护声明完整性、失败传播和取消排空。VM混合分派有机会减少提交次数，同时需要理解线程/递归边界；没有对等实测不评吞吐优劣。暂保留自研执行层，借鉴明确的就绪条件与汇聚合同，按多前驱、失败、剪枝、循环和共享设备场景验证。</p>`;
h+=proof('vm-round4-graph-cache-trace.txt',605,679,'后继前驱计数递减、归零与实际分派');
h+=proof('vm-round4-graph-cache-trace.txt',240,252,'节点准入和虚表执行');
h+=`<h4>B．外部状态：已追到ModuleProxy真实接收与文本/JSON分派</h4><pre>ModuleFrame VM_M_SetEventCalBack → 回调槽2a5258
实际注册：ModuleProxy@35f60
  绑定表+248传入callback=38050、context=this
  38050检查上下文、重排寄存器 → 38470
事件6：按payload状态1/2/3 → 文本通知slot8
  状态3另构造JSON → slot0
事件12：EVENT_ALL_PROCEDURE_WORK_FINISH
  构造消息0xc47并序列化 → slot0</pre><p>该注册来源是实际调用实参和指针装配，已超出未知回调槽；不是凭函数名字猜接收方。文本/JSON分派两条通道已见，但接收函数本身直调slot0/8不证明最终消费者同步。实际虚方法、注册对象和更外层排队仍须核对；不能把发送状态通知解释为托管/UI已处理完毕。</p><p><strong>比较与推荐：</strong>VM统一原生事件并提供多种通知表示，方便消费者接入；代价是状态经过原生、代理、编码与外部接收多层。“算法尾完成”“通知送出”“消费者处理完”需要不同合同。自研可继续用会话/代次和Task表达，但应把外部通知结果、延迟及丢弃旧代次明示，不能只按UI忙状态认定排空。</p>`;
h+=proof('vm-round4-proxy-events-trace.txt',327,341,'回调注册的真实函数地址和上下文');
h+=proof('vm-round4-proxy-events-trace.txt',1916,1951,'全部流程完成事件构造并转发');
h+=`<h4>C．默认缓存：写入已追到三个上层入口，串行合同尚待证明</h4><pre>VM_M_SetBinayData@13aad0（原厂拼写） → SetBinaryData@43120
CModuleParamBindingHandler::SetModuleParam@bc8a0 → 43120
CControlDelegate::UpdateCurrentConfigOfSolution@69210 → 43120
  → 扩展slot18优先，或默认b9390替换缓存

LoadModuleData@4bee0由70ce0包装成绑定对象
  同时捕获模块shared_ptr → 提交/消费链尚待追</pre><p>这些已读包装/调用函数没有显示停止准入、等待当前运行或节点+138数据锁；LOCK引用计数也不是内容锁。不能因此断言没有更上层串行规则，更不能直接登记读写竞争故障。70ce0保留模块所有权，只证明被捕获对象寿命，不证明任务串行执行。扩展读取slot10、写slot18、加载slot30真实实现仍未知。</p><p>节点构造b8560明确清零+128扩展指针与+140缓存、初始化+138互斥。e8880原始日志为CReentrantObjMgr::DuplicateModuleNode，仅复制节点身份/名称/标志，不装配非空+128。静态标签BuildModuleNode不作为扩展类型证据。</p><p><strong>比较与推荐：</strong>VM将原生导出、参数绑定、配置更新复用到同一写入分派，减少重复逻辑；多入口也增加调用约束和同步审查成本。建议自研将缓存读取、替换、停止、删除纳入统一所有权与线程合同；在证明串行化前不直接照搬先释放再分配策略，也不凭缺局部锁评判VM不安全。</p>`;
h+=proof('vm-round4-graph-cache-trace.txt',3210,3229,'原生二进制写导出直接进入分派');
h+=proof('vm-round4-graph-cache-trace.txt',3593,3628,'加载回调捕获与模块共享引用');
h+=`<h4>D．厂商销毁：已找到真实DestroyModule调用，而非记录deleter推断</h4><pre>工厂@b54e0：GetProcAddress(CreateModule/DestroyModule)
  记录+8=创建的实例，记录+18=已解析DestroyModule
加载器释放@1800b6670：持管理器+38互斥，查模块ID
  有模块记录且状态字段满足条件时：
    CALL [记录+18](模块信息, 记录+8实例)
  → 释放信息块 → 从加载管理容器移除
  → 更新关联索引
上层另有逐项批量释放b6df0及带管理状态门槛的b6fa0</pre><p><strong>本轮修正：</strong>b81f0记录deleter确实不销毁厂商实例，但现在已找到另一条显式加载器销毁路径，不能再概括为“DestroyModule只解析未调用”。此处调用的指针与工厂保存位置对应，证明到达插件销毁边界；状态字段不满足时跳过该调用，具体状态语义仍待核对。虚表和直接调用索引另指向CModuleLoader的slot+18包装候选，需要与DeleteNode观察者实际注册连通。</p><p>函数体中销毁后释放信息并移除管理记录；没有据此证明FreeLibrary、厂商函数内部逐资源释放、驱动回调排空或所有删除入口均走此路径。加载器互斥覆盖厂商Destroy调用，耗时会影响共享该锁的操作，尚未测量，也不以此推断已发生死锁。</p><p><strong>比较与推荐：</strong>VM已有Create/Destroy明确配对，实例销毁与记录释放分工可审查；成本是删除状态、持有者、销毁函数行为和库生命周期跨多层。自研应保留显式实例/会话所有权，同时记录原生销毁完成与失败，释放借用后再移除目录；不能将IDisposable签名或注册注销当作安全卸载证明。无证据要求全系统热卸载。</p>`;
h+=proof('vm-round4-owner-trace.txt',169,207,'已保存的DestroyModule指针实际调用及后续移除');
h+=proof('vm-round4-release-callers-trace.txt',49,59,'批量释放实际调用单实例释放');
h+=`<p><a href="${url('vm-round4-delete-observer.json')}">CModuleLoader虚表/调用候选</a> · <a href="${url('vm-round4-loader-vtable-writes.json')}">虚表装配引用</a>。扫描可能跨表列出候选，只有与真实类型、构造及调用上下文一致的项才可认证；不将相邻表的slot64候选作为有效虚方法。</p><h4>仍需补齐的准确断点</h4><ol><li>调度：邻接构建01dfd0/读取01e310、传播01e400、线程消费15aba0/fb9d0、类型/循环/资源冲突与实现对象+128。</li><li>回调：代理slot0/8真实方法及更外层注册/队列，消费者已处理与通知已送出的区别，异常出口和取消通知。</li><li>缓存：70ce0绑定对象实际提交链（47a00/66c60等候选），上层准入/内容锁、重复加载与失败恢复，扩展非空+128装配。</li><li>销毁：CModuleLoader观察者注册→删除通知→b6fa0/b6670、状态字段语义与销毁异常处理、厂商Destroy内部及库释放。</li></ol><p>本轮纠正关键旧断点并补实机制，仍不登记整个调度、图像或插件领域完成；运行性能、长稳、真实设备与UI交互均未验证。</p></article>`;
s=s.replace('<article class="vm-deep-chain" id="vm-four-line-round3">',()=>h+'<article class="vm-deep-chain" id="vm-four-line-round3">');
s=s.replace('<h3>四线第三轮：拓扑汇聚、删除等待、缓存替换与模块工厂</h3>',()=>'<h3>四线第三轮：拓扑汇聚、删除等待、缓存替换与模块工厂</h3><div class="note vm-progress">第四轮已补实际依赖计数分派、代理事件接收及DestroyModule实际调用，最新进展见<a href="#vm-four-line-round4">四线第四轮</a>。以下为第三轮历史证据。</div>');
fs.writeFileSync(file,s);console.log('Published round4 base.');
