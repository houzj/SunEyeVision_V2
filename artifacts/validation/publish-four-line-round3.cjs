const fs=require('fs'),file='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';let s=fs.readFileSync(file,'utf8');if(s.includes('id="vm-four-line-round3"'))throw Error('already published');
const esc=x=>x.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;'),url=p=>'file:///D:/MyWork/SunEyeVision_V2/artifacts/validation/'+p;
function proof(file,a,b,title){const l=fs.readFileSync('artifacts/validation/'+file,'utf8').split(/\r?\n/);return `<details class="vm-proof"><summary>${title} · ${a}–${b}行</summary><p><a href="${url(file)}">完整材料</a>；独立Ghidra静态反编译/反汇编，不是原厂源码或REA Evidence，分析标签和恢复签名可能不准确。</p><pre>${esc(l.slice(a-1,b).map((x,i)=>`${a+i} ${x}`).join('\n'))}</pre></details>`;}
let h=`<article class="vm-deep-chain" id="vm-four-line-round3"><h3>四线第三轮：拓扑汇聚、删除等待、缓存替换与模块工厂</h3><p><span class="pill">新增：真实模块接口调用、+98等待者、默认缓存写入</span><span class="pill pending">仍待证：依赖触发完整策略、外部回调排空与厂商销毁</span></p><p>本轮继续在同一已认证ModuleFrame.dll上做限定静态分析，SHA256 <code>45c57bc46f72fd1f9e0b3a6f9183ff974d14590cfd05ecf4e9fa97a4ff95ae9b</code>，VA均以0x180000000为基址。三条只读子线与主智能体第四线并行，只有主智能体写文档和分析产物；未执行目标、设备或运行探针，不改产品代码。</p>
<h4>A．节点调度：拓扑汇聚与算法执行不是同一个入口</h4><pre>流程+c8共享持有CBaseTopology（15c40/7dc50保持指针及控制块）
  slot8@180020dd0 → b9810(target=this+18)
  → WaitForSingleObject(target+128, INFINITE) → ResetEvent

源节点slot8@b9760 → 1e4e0(this,1)
目标节点slot8@b97e0 → 1e4e0(this,0) → SetEvent(target+128)
实际CModuleNode::slot8@b8740
  节点+138互斥/准入 → 实现对象(node+128)的slot8
  → 返回处理 → 1e4e0(this,0)</pre><p>20dd0固定返回0，不能代表所有节点成功；它已被证实为汇聚等待入口，不是直接遍历算法节点。15c40/7dc50只处理共享指针字段和引用计数，不新建std::function包装器或调整对象地址，前轮候选现已排除。真实节点执行持互斥并可按状态跳过；正常和备用路径均已定位实现接口slot+8，但具体插件对象/算法DLL实现仍未知。</p><p><strong>比较与推荐：</strong>VM已有流程工作循环、共享拓扑、源/汇节点和模块实现分层，不能归为简单串行门面。自研显式依赖图和资源租约便于审查，但不能据此裁定并发能力高低。保留自研计划与完成合同，按前驱失败、分支剪枝、多前驱汇聚及不可取消算法验证双方同场景；采用类似拓扑前须先核对后继触发与错误传播，避免只模仿虚表结构。</p>`;
h+=proof('vm-round3-core-trace.txt',5,22,'拓扑slot8的实际调用');
h+=proof('vm-round3-follow-trace.txt',5,31,'目标事件无限等待及复位');
h+=proof('vm-round3-execute-trace.txt',207,241,'模块执行接口slot8与备用路径');
h+=`<h4>B．停止与移除：+98晚完成事件确有删除路径等待者</h4><pre>Stop：等循环退出+88（仍有后续状态/尾回调）
流程移除@180108c30：查流程、检查非运行
  → TailWait@1800dcae0：WaitForSingleObject(flow+98, INFINITE)
  → 关系/节点移除（118370、085440等）
析构@1800dc730：关闭事件句柄；函数本身不承担上述等待</pre><p><strong>本轮修正：</strong>VM不只区分早退出与晚完成信号，至少该流程移除入口实际等待+98后才继续清理。不能再把“没有晚完成等待”当VM缺点。TailWait是void包装，等待错误未向调用者传播；仍需核对可达异常、其他移除入口和worker异常是否均置位+98，不能推为全平台安全删除。</p><p>回调注册叶导出已认证为<strong>VM_M_SetEventCalBack@180140e80</strong>：保存RDX上下文到2b1180，RCX非零则用调用者回调，否则用默认函数，写槽2a5258并返回0。这与状态6/末项0x12的既有回调槽对应；外部真正传入者及托管/UI排队仍未连通。不能与另一个SetCustomDataCallBack混用。</p><p>尾回调setter@1800e20b0已见对内嵌+1b0函数对象替换，结果存+1c8，先销毁旧对象后安装新对象；它对应worker尾部调用的位置，而不是简单搜索字段就认为没有安装者。具体安装场景与回调工作继续核对。</p><p><strong>比较与推荐：</strong>借鉴VM区分普通停止和可移除时点的合同；自研应将删除等待绑定实际任务/借用释放，而非仅看IsAnyRunning。我们可用可await结果表达成功/超时/失败，但仍须证明原生调用和回调已结束；代价是外部通知关联与失败恢复，不能只换成Task便宣称可靠。</p>`;
h+=proof('vm-round3-execute-trace.txt',1093,1118,'尾完成等待封装与错误传播边界');
h+=proof('vm-round3-execute-trace.txt',1617,1658,'流程移除路径等待后再清理');
h+=proof('vm-round3-lifetime-trace.txt',5,40,'尾回调替换与+1c8存储');
h+=`<p><a href="${url('vm-round3-callback-register.json')}">回调导出、原始字节及寄存器解释</a>。</p>
<h4>C．源缓存寿命：加载、按键替换、读取复制与析构已能对应</h4><pre>CControlDelegate::LoadModuleData@4bee0
  默认 → CModuleNode::SetModuleDataCache@b90d0
  表为空才分配0x61118；复制结构后逐块分配并深拷贝
  失败 → b96a0清理
CControlDelegate::SetBinaryData@43120
  扩展接口优先slot18；否则SetBinaryDataToCache@b9390
  匹配旧键：delete[]旧块、清指针/长度 → 分配新块 → memcpy_s
GetBinary默认路径b9580 → 复制到调用方
节点析构b85d0/3b700 → b96a0释放块与表</pre><p>上述名称有原始日志字面支撑。b90d0表非空时只见警告后继续复制，没有先清旧块；不能改写成“每次安全刷新”，也不能在未核对初始化调用约束前报告实际泄漏。b9390已匹配旧项时先释放再分配，未见容量复用；还应核对分配失败后的有效状态。</p><p>这些加载/写入函数及所读外层函数体未见数据互斥；shared_ptr的LOCK是引用计数，不是缓存内容锁。NodeExecute持锁也不能证明外部缓存写入用同一锁。扩展接口的读取slot10、写入slot18、加载slot30尚未绑定真实对象；默认缓存行为不能覆盖相机帧、算法工作区或扩展实现。</p><p><strong>比较与推荐：</strong>VM深拷贝使加载输入与缓存独立，按键替换的释放责任可审查；成本是分配、复制及并发更新治理。自研应继续区分独立快照与借用租约，优先验证读写串行化、分配失败、停止/删除时消费者排空。尚不支持全局零拷贝，也不支持一律深复制；策略应绑定实际数据类别与持有时间。</p>`;
h+=proof('vm-round3-core-trace.txt',199,250,'默认模块数据加载深拷贝及失败清理');
h+=proof('vm-round3-core-trace.txt',414,442,'按键替换：先释放再重新分配');
h+=`<h4>D．模块析构：已经找到工厂的Create/Destroy配对装配</h4><p>ResolveModule@1800b54e0对已加载库分别GetProcAddress查CreateModule与DestroyModule；分配模块记录，保存库句柄、创建函数、销毁函数与模块名，然后实际调用创建函数并保存实例。GetModuleImpl@1800b6b70访问同一加载管理范围；这补实了厂商销毁函数并非纯命名猜测，但缓存了DestroyModule指针不等于已经执行销毁。</p><p>记录经1800b7be0创建强/弱初值均为1的控制块，虚表229890、slot0指向1800b81f0。本轮继续核对该deleter的具体清理动作与真实销毁调用，不能把记录释放、算法实例销毁和FreeLibrary混成一件事。节点结果缓存析构沿用前轮已证实路径。</p><p><strong>比较与推荐：</strong>工厂Create/Destroy配对便于把插件实现与宿主分离，也要求明确谁最终调用Destroy、持有者何时归零和库何时可卸载。自研相机/工具实例已有显式会话与Dispose合同，但仍须逐厂商适配器核对回调排空及原生销毁。推荐分别记录模块记录、算法实例、设备会话和库句柄的所有者及完成结果；增加生命周期追踪成本，换取可核查删除，而不是将注销当热卸载。</p>`;
h+=proof('vm-round3-execute-trace.txt',2593,2647,'DestroyModule解析、保存及CreateModule调用');
h+=proof('vm-round3-lifetime-trace.txt',1246,1270,'模块记录共享控制块及虚表装配');
h+=`<h4>标签纠正与下一断点</h4><p>静态产物中的CacheRead@43120实际是SetBinaryData写入分派；CacheRefresh@4bee0是LoadModuleData，不宣称每帧刷新；CacheClear@3b700实际是包含默认缓存清理的析构。原始分析输出保留，正文按已核实实现解释，不让临时名称成为事实。</p><ol><li>节点：01e4e0后继通知/派发、01e5c0状态条件、实现对象+128装配及其算法slot8。</li><li>停止：+98其他等待者与异常出口、外层事件回调接收/排空、尾回调安装场景；+1d0可信读取者仍未知。</li><li>缓存：上游加载/写入准入和读写串行化、非空表重复加载、失败状态及扩展接口实际实现。</li><li>析构：模块记录deleter到DestroyModule实际调用、实例共享所有者归零、库卸载与驱动回调结束。</li></ol><p>四条线均为局部证据增量，原16域不登记新增整域闭合；未做现场运行、性能、长稳或真实UI验收。</p></article>`;
s=s.replace('<article class="vm-deep-chain" id="vm-four-line-round2">',()=>h+'<article class="vm-deep-chain" id="vm-four-line-round2">');
s=s.replace('<h3>四线第二轮：循环执行、停止事件时序、源结果复制与析构</h3>',()=>'<h3>四线第二轮：循环执行、停止事件时序、源结果复制与析构</h3><div class="note vm-progress">第三轮已继续到拓扑汇聚/实际算法接口调用、+98移除等待及缓存写入/替换，最新证据见<a href="#vm-four-line-round3">四线第三轮</a>。</div>');
fs.writeFileSync(file,s);console.log('Published four-line round3 base; final leaf review pending.');
