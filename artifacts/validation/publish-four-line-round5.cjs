const fs=require('fs');
const file='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';
let s=fs.readFileSync(file,'utf8');
if(s.includes('id="vm-four-line-round5"'))throw Error('already published');
const esc=x=>x.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const url=p=>'file:///D:/MyWork/SunEyeVision_V2/artifacts/validation/'+p;
function proof(f,a,b,title){const l=fs.readFileSync('artifacts/validation/'+f,'utf8').split(/\r?\n/);return `<details class="vm-proof"><summary>${title} · ${a}–${b}行</summary><p><a href="${url(f)}">完整静态反编译与反汇编</a>。独立Ghidra产物，不是原厂源码或REA Evidence；名称为分析标签，类型、尾跳与间接调用须结合指令核对。</p><pre>${esc(l.slice(a-1,b).map((x,i)=>(a+i)+' '+x).join('\n'))}</pre></details>`;}
let h=`<article class="vm-deep-chain" id="vm-four-line-round5"><h3>四线第五轮：真实队列消费、完成消息接收、批次加载与相机析构</h3><p><span class="pill">已核实：任务消费实现、注册槽与Server接收、代表相机模块最终析构</span><span class="pill pending">仍未闭合：策略并发度、外部排空、缓存互斥和原始帧寿命</span></p><p>四线只读分析，主智能体统一写入。继续基于安装产物的静态反编译和指令证据；没有执行VM、相机、业务或探针。ModuleFrame、ModuleProxy、PlatformManager沿用前轮哈希；本轮新增安装文件 <code>Applications/Module(sp)/x64/Global/GlobalCameraModule/GlobalCameraModule.dll</code>，SHA256 <code>f61fb1063325793343139c8699aa9eba3db931a33de3869fd856700ca05cecce</code>。它是代表模块，不证明本机某个方案实际加载它，更不代表所有插件。</p>
<h4>A．任务终于进入真实消费端，按流程队列不等于流程内串行</h4><pre>提交15aba0：策略字段manager+120 == 2
  → fc9e0(manager+f8, 流程ID, task) → 按流程ID查记录 → 该记录队列
其他策略 → fb9d0(manager+80, task) → 共享队列
消费入口fc8e0 → fbee0（原日志CConcurrentThreadPoolImpl::DoWork）
  无任务时等待同步对象 → 锁队列 → 移出队首/出队
  → 解锁 → 调用任务虚表slot10
  返回0xffff走退出；特定标志下重新放入退出哨兵</pre><p>WorkerEntry在消费前后增减工作线程计数+4c。队列互斥保护取放任务，执行在锁外；已经确认实际生产/消费环，不能再把线程消费列为完全未知。尚未认证每个队列的消费者数、扩缩规则、策略参数含义、取消排队与资源冲突。按流程ID分队列不能直接写成“每流程单线程”；同步适配器未完成IAT身份认证，暂不命名为条件变量。</p><p><strong>比较与推荐：</strong>VM共享队列利于复用工作线程，按流程队列提供隔离的组织手段；具体并发与公平性仍取决于消费者和策略。自研显式执行计划/资源租约更便于开放相机与插件声明冲突，但需要自己解决饥饿、不可取消调用和停止排空。推荐保留这一方向，吸收“策略选队列—锁内出队—锁外执行—显式退出”的审查分层，以同流程分支、跨流程同相机、长任务堵塞和停止重启作同场景验收；当前不比较吞吐高低。</p>`;
h+=proof('vm-round5-core-trace.txt',15,48,'策略分流到按流程队列或共享队列');
h+=proof('vm-round5-workers-trace.txt',54,116,'真实等待、锁内出队、锁外执行和退出哨兵');
h+=`<h4>B．全流程完成消息已追到PlatformManager接收体</h4><pre>Frame事件0x12 → Proxy生成JSON消息0xc47
  → Proxy注册槽+198（context +1a0）
  → PM注册Server回调5d2de0 / manager上下文
  → 检查上下文并重排参数 → 尾跳5d51c0
  → 6622c0解析消息 → switch 0xc47
  → 5f6e50(..., 0x811, payload)</pre><p>Proxy setter将Server callback/context写入singleton的+198/+1a0，SDK setter写+1a8/+1b0，与前轮49e10/49ee0实际读槽一致。PM的Server注册固定传5d2de0，SDK注册则原样透传上层函数/上下文，两条通道不能混为同一接收者。接收体5d51c0已见解析后直接分派，不据此推断其下游同步。反编译器可能把尾跳接收体并入adapter伪代码，以实际尾跳指令与独立接收函数为准。</p><p><strong>尚未证明：</strong>Frame注册所用对象与singleton完整实例传递、SDK/UI最终接收与处理完成、异常出口和旧代次通知处理。前轮的循环退出88、尾部98、管理器汇合548，以及本轮消息0xc47/事件0x811，都是不同阶段，不能互相替代。</p><p><strong>比较与推荐：</strong>VM原生事件经统一消息转换接入宿主，便于复用通知，但多层编码与回调会扩大完成语义的审查范围。推荐自研保留代次标识并分开记录执行退出、资源排空、通知投递和消费者确认；代价是额外状态与迟到事件处理，而不是仅以Task完成或UI空闲代替所有阶段。</p>`;
h+=proof('vm-round5-proxy-setters-trace.txt',15,29,'Server注册槽与上下文保存');
h+=proof('vm-round5-manager-callbacks-trace.txt',33,48,'PM固定注册实际Server接收函数');
h+=proof('vm-round5-receiver-trace.txt',244,307,'真实接收体消息解析及0xc47分派');
h+=`<!-- ROUND5_FINISH_DETAIL --><h4>C．缓存加载属于批量任务，等待批次不等于全部读写互斥</h4><pre>AsyncLoadSolutionFromFile@47a00 / ImportModuleDataFromSharedMem@66c60
  → 70ce0捕获LoadModuleData@4bee0及模块共享引用
  → 包装函数对象 → fb9d0(全局加载队列2b2a10)
  → worker锁外调用slot10 → 75870调整this → 78130展开参数 → 78440
调用端：等待本批事件INFINITE，并检查另一事件/逐项汇总结果</pre><p>两条上游都已读到提交与等待实现，不能继续描述为简单同步直调。共享引用保活捕获的模块对象；它不证明像素内容不变，也不提供内容互斥。队列锁不覆盖执行任务，因此同一队列不能证明单消费者或与运行/公开SetBinayData互斥。重复加载、配置更新、扩展接口slot10/18/30仍须分别确认允许时机和所有权。</p><p><strong>比较与推荐：</strong>VM集中批次提交和汇总适合方案加载，成本是跨层任务所有权、计数、错误与无限等待需要闭合。自研可借鉴批次完成屏障，同时独立定义缓存读写准入和原始帧租约；不能以引用计数锁或队列锁代替内容一致性。若选择深拷贝可减少借用耦合但增加带宽；若借用须证明源复用、回调退出和最后读者释放，当前原始相机帧的分配/复用/释放仍未闭合。</p>`;
h+=proof('vm-round5-core-trace.txt',1486,1497,'方案加载任务包装并提交实际队列');
h+=proof('vm-round5-core-trace.txt',1595,1618,'调用者等待批次与后续检查');
h+=proof('vm-round5-load-task-trace.txt',15,28,'捕获模块引用与绑定参数展开');
h+=`<!-- ROUND5_LOAD_DETAIL --><h4>D．代表相机模块：DestroyModule已连到清理体和最终析构</h4><pre>工厂记录DestroyModule → GlobalCameraModule!DestroyModule@1cf50
  → NotifyDecGlobalCameraNum → 实例虚表slot0(this, 1)
  实际CCameraModule表30c88 slot0=8cc0
  → 8d90析构体 → 9b00清理体
      将图像/异常回调注册为null
      等待+4a918、+4a6a0各3000ms
      调用230b0、16400、1abe0等清理辅助函数
      关闭句柄、释放+53198内存、清理关联模块缓存
  → 按条件卸载GenTL库，清理成员容器/同步对象/内存
  → 基类析构 → deleting wrapper条件删除实例</pre><p>CreateModule@1cbe0确实构造同一主虚表对象，并在初始化成功后递增相机数量、注册通知回调；销毁通过对应表进入实例析构，已超过仅见插件销毁入口的证据。9b00对两个等待结果非零记录日志后继续后续清理，不能把“等待过”写成“保证回调全部退出”。注销厂商回调本身也未证明SDK对在途回调的保障。</p><p><strong>尚未证明：</strong>两个事件由谁置位、对应线程还是在途计数；230b0/16400/1abe0内部是否StopGrabbing/CloseDevice/DestroyHandle以及失败行为；原始图像缓冲的分配/复用/最后读者；全部销毁异常分支。MV_CC_UnloadGenTLLibrary是条件GenTL库清理，不等于插件DLL的FreeLibrary。ImageSourceModule本轮仅清点导出，不能算已经复核其析构。</p><p><strong>比较与推荐：</strong>VM代表模块确有注销、等待、成员和基类析构的分层清理，不能说它只删目录或缺少释放设计。其成本是超时后仍继续清理的安全合同依赖事件生产者及SDK行为。自研推荐把停止采集、拒绝新借用、等待在途帧/回调、销毁驱动、移除目录分阶段，超时结果显式可观察；代价是慢设备可能阻塞关闭，需定义隔离或延迟回收策略。不能仅凭IDisposable认定自研更安全。</p>`;
h+=proof('vm-round5-camera-trace.txt',320,349,'代表模块DestroyModule及虚表调用');
h+=proof('vm-round5-camera-life-trace.txt',361,385,'虚表删除析构wrapper调用实际析构');
h+=proof('vm-round5-camera-dtor-trace.txt',20,43,'析构首先调用清理体及条件GenTL卸载');
h+=proof('vm-round5-camera-life-trace.txt',479,556,'注销厂商回调与两次有限等待');
h+=`<h4>标签纠正与下一轮准确断点</h4><p>撤回b98c0的“节点准入”业务命名：实际逐项处理列表并调用b9c70，不能据它证明资源锁或取消准入。撤回102f0的ExtendedNodeCtor命名：实际为删除析构包装，未证明node+128实现装配。1e310是邻接列表复制，1e400是加锁更新状态/向量；它们本身不提交任务。分析产物保留原标签供审计，本文按实际函数体修正结论。</p><ol><li><strong>调度：</strong>fc270/fc340策略、3f450线程创建与实际队列消费者数量；b9c70参数/业务处理，类型/循环及资源冲突仍未闭合。</li><li><strong>停止：</strong>0xc47到0x811后续消费者、Frame注册对象实例传递、异常回调与取消路径，直到消费确认和资源排空。</li><li><strong>缓存：</strong>加载任务信号生产者/异常出口、加载池线程数，公开写入与执行准入，非空+128装配；原始帧寿命仍需单独追。</li><li><strong>销毁：</strong>相机清理辅助函数230b0/16400/1abe0、两事件生产者和在途回调，缓冲释放配对及插件库所有者；其余代表工具分组复核。</li></ol><p>本轮扩大的是具体函数链的证据，不是全领域验收：原16域仍0整域闭合/10部分核实/6未系统核实，20工作包与13项UI设计比较继续按原计划推进。没有运行性能、长稳、真实相机或交互验收结论。</p></article>`;
h=h.replace('<!-- ROUND5_FINISH_DETAIL -->',()=>'<p>5f6e50已进一步读到：调用6950d0解析payload，成功后用事件号0x811与解析结果格式化字符串，再交给6203a0；失败分支记录日志并返回错误码。这里没有证明消费者确认或排空，下一准确边界为6950d0/6203a0。汇编保留EAX错误返回，不能按恢复的void类型推断无错误合同。</p>'+proof('vm-round5-finish-notify-trace.txt',27,59,'完成消息处理与下一投递边界'));
h=h.replace('<!-- ROUND5_LOAD_DETAIL -->',()=>'<p>78440已读取到CALL[绑定对象首字段]，与70ce0保存的4bee0对应，worker到实际LoadModuleData调用已连通；随后为共享引用清理。该函数没有SetEvent，批次完成信号生产者及异常汇总仍未闭合，不能因为调用者在等就假定生产者已核实。</p>'+proof('vm-round5-load-body-trace.txt',19,48,'绑定函数实际间接调用及引用清理'));
s=s.replace('<article class="vm-deep-chain" id="vm-four-line-round4">',()=>h+'<article class="vm-deep-chain" id="vm-four-line-round4">');
const note='<div class="note vm-progress"><strong>第五轮证据更新：</strong>已补真实队列消费、Server完成消息接收、批量加载与代表相机模块析构。按流程队列不保证串行、注销和超时等待不保证排空、队列锁不保证缓存互斥；推荐仍为按产品目标暂定。<a href="#vm-four-line-round5">本轮调用链、具体取舍与待补断点</a>。</div>';
for(const id of ['vm-architecture','architecture','vm-evidence-progress']){
  const re=new RegExp('(<section[^>]*id="'+id+'"[^>]*>[\\s\\S]*?<h2>[^<]*</h2>)');
  s=s.replace(re,m=>m+note);
}
s=s.replace('<h2>四、全系统优化方案与推荐理由</h2>',m=>m+note);
s=s.replace('<article id="vm-reverse-backlog"><h3>仍需继续逆向：任务、证据缺口与结论限制</h3>',m=>m+note+'<p><strong>当前待办口径：</strong>7类RE任务仍0整类验收。RE-W1已补真实线程消费；RE-W2已补Server接收及0x811格式化投递；RE-I1/I2已补批次加载实际调用和相机清理体，但原始帧寿命未闭合；RE-P1/P2已补代表模块最终析构，库卸载与全插件覆盖未知。原表中未闭合项继续保留，以第五轮断点和下一轮有限目标为准。</p>');
s=s.replace('<h3>四线第四轮：前驱计数调度、事件代理、写入入口与DestroyModule</h3>',m=>m+'<p class="note">第五轮已更新线程消费、消息接收、批次加载与厂商析构，且纠正b98c0“准入”命名；以下保留为历史阶段记录。<a href="#vm-four-line-round5">阅读最新证据</a>。</p>');
// Keep historical output names, correct the factual business label in prose.
s=s.replaceAll('b98c0准入 →','b98c0逐项处理（业务待核实） →').replaceAll('b98c0准入后slot8执行','b98c0逐项处理后slot8执行');
fs.writeFileSync(file,s);
const jf='artifacts/validation/comparison-work-packages.json',j=JSON.parse(fs.readFileSync(jf,'utf8'));
j.round5={status:'四线静态增量，0整域闭合；线程消费与代表相机析构已补实',anchor:'vm-four-line-round5',packages:['C03','C04','C06','C08','C12'],next:['fc270/fc340策略、消费者数与b9c70业务','完成消息0x811下游消费者及回调排空','加载任务信号/异常、缓存写入与运行准入、扩展装配','相机230b0/16400/1abe0、两事件生产者、原始帧及库释放']};
j.progressPolicy={domainTotal:16,domainAccepted:0,domainPartial:10,domainUnsystematic:6,packageTotal:20,packageAccepted:0,packagePartial:j.round1.packages.length,packagePending:20-j.round1.packages.length,reverseTaskTotal:7,reverseTaskAccepted:0,round:5,roundTargets:4,roundTargetsMet:4,closedExistingCheckpoints:4,wholeTasksClosed:0,note:'局部断点完成不计整工作包完成；新发现辅助函数不自动升级为必须完成任务。每轮固定目标，报告验收增量；连续两轮整工作包无增量则转覆盖复核/结论收敛。'};
fs.writeFileSync(jf,JSON.stringify(j,null,2)+'\n');
console.log('Published round5; detail placeholders to fill before validation.');
