const fs=require('fs');
const file='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';
let s=fs.readFileSync(file,'utf8');
if(s.includes('id="vm-receiver-p0"'))throw Error('already inserted');
const esc=x=>x.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const link=(f,t)=>`<a href="file:///D:/MyWork/SunEyeVision_V2/artifacts/validation/${f}">${t}</a>`;
const proof=(f,title,lines)=>`<details class="vm-proof"><summary>${title}</summary><p>${link(f,'完整限定反编译与反汇编')}；独立 Ghidra 静态分析，分析者命名，不是原厂源码或 REA Evidence。</p><pre>${esc(lines)}</pre></details>`;
let html=`<article class="vm-deep-chain" id="vm-receiver-p0"><h3>P0续追：统一命令的实际接收方与下一层断点</h3>
<p><span class="pill">已核实：SDK指针绑定、平台接收、停止虚表、第二层发送</span><span class="pill pending">未闭合：真正节点调度、停止终态、缓冲生产者及算法析构</span></p>
<p>本轮只读安装 DLL，按 PE 导出、链式展开信息、限定反编译、反汇编和 RTTI 交叉核对，没有运行 VM 业务或设备。此处接续上一轮 <a href="#vm-native-p0">+0x2398 断点</a>，不把转发函数当作调度器。所有地址均为本次 DLL 首选基址 0x180000000 下的静态 VA，不是运行时地址；伪代码类型/参数仍可能不完整。</p>
<h4>1．真实调用链：从SDK进入PlatformManager，再转发</h4>
<pre>iMVS-6000PlatformSDK.dll
  SlotInit @1806615b0
    LoadLibraryW：..\\..\\Server\\PlatformManager.dll
    GetProcAddress("PF_M_MsgDeal") → 对象+0x2398
  SendCommand @1806c00d0 → 上述函数指针
PlatformManager.dll
  PF_M_MsgDeal @1806a0250 → ClientMsgDeal @180602df0
    命令1030 / 0x406 → ControlExecuteOnceByProcId @180617c10
    命令1032 / 0x408 → ControlStopExecuteByProcId @180619670
    命令1004 / 0x3ec → 关闭分支 @180606a89（下游尚未全部连通）
  流程对象+0x218接口 → CProcedureMngForSp 虚表
    slot+0：单次执行 @1806a3b00 → SP_Execute @1805e2f90
    slot+0x10：停止 @1806a4120 → FilterStop @1805e3640
  BuildContinueExecuteStop @180670200 → SP_SendCommand @1805de710
    GetProcAddress(handle+0x7d510, "VM_PROXY_RecvServerCommand")
    缓存到对象+0x7d518 → 第二层间接调用</pre>
<p><strong>核对方式：</strong>SDK装配处的宽字符串和 GetProcAddress 实参证明第一接收方；PlatformManager 的 PE 导出证明入口地址。ClientMsgDeal整函数反编译20秒超时，故用有界反汇编核对“命令减1000→跳转表”及上述分支的直接CALL，不宣称恢复了完整CFG。流程虚表由RTTI的子对象偏移0x218、虚表项和构造函数写入交叉确认，属于静态目标恢复，尚无运行时对象验证。${link('vm-p0-command-cases.json','命令表')} · ${link('vm-command-cases.txt','分支反汇编')} · ${link('vm-rtti-vtables.json','RTTI/虚表')} · ${link('vm-procedure-vtable-refs.json','构造函数虚表写入')}。</p>
<h4>2．停止返回与停止完成：现在能确认什么</h4>
<p>ControlStop解析流程身份，查找流程对象，调用+0x218接口的slot+0x10，返回成功时形成OK回复。ProcedureStop先清局部标志，再交给FilterStop组装JSON并转发。该链证明停止请求进入流程层及下一层发送，<strong>尚不证明在途节点结束、工作线程退出、末次结果/回调排空或图像可回收</strong>。</p>
<p>第三参数确实被atoi解析，但本次尚未证明其单位、是否实际参与等待和超时后行为；不能把托管层3u解释成“3秒内停止完成”。发现NotifyExecuteStatus与NotifyClientProcedureStopExecute相关候选函数，尚未连通终态生产者、通知消费者和等待解除。某候选复杂函数出现1800000的等待常量，也不能据此称为全局停止超时。</p>
<h4>3．图像与销毁：继续追到同一转发层</h4>
<div class="table"><table><thead><tr><th>链路</th><th>本轮新增证据</th><th>仍不能下的结论 / 下一断点</th></tr></thead><tbody>
<tr><td>缓冲读取/释放</td><td>SP_GetBinary @1805e2520、SP_GetBinaryLen @1805e2300、SP_ReleaseBinary @1805e2740、SP_ReleaseShared @1805f2430均有JSON构建及公共发送路径。</td><td>释放命令不是生产者析构证据；继续追代理端对应命令、实际分配/回收/覆写及借用指针失效时点。</td></tr>
<tr><td>查询动态内存</td><td>SDK ReleaseMemory @1806af090已按链式分段补读，包含按类型分支释放嵌套内存。</td><td>尚未逐一将枚举与图像/历史查询类型关联；不能把查询内存释放等同共享帧回收。</td></tr>
<tr><td>模块关闭</td><td>SP_CloseOne @1805e53f0对20000..20999有特殊路径，普通路径调用SP_Delete @1805df790；收到成功后继续更新本地关系和容器。</td><td>尚未把0x3ec整个分支连到所有关闭分支；元数据移除不证明算法/驱动析构、在途回调排空或DLL卸载。下一步追代理端Delete与特殊ID分支。</td></tr>
</tbody></table></div>
<h4>4．比较与推荐理由随证据调整</h4>
<p><strong>VM收益：</strong>前端统一命令、平台按流程身份分派、再以JSON请求下层，可以使流程门面与下层实现分离，并复用返回处理。<strong>代价：</strong>至少两级命令转换和间接调用使“成功”所在层级不直观，取消、回调、缓冲释放需要跨层核实；尚无耗时测量，不能据此断言性能差，也不能把本次LoadLibrary分支推广为全系统无IPC。</p>
<p><strong>我们的收益与代价：</strong>源码中的Task、取消令牌和显式帧Dispose便于检查上层合同，但原生算法可中断性、显示保留引用与取消排空仍要由我们实现和验证。VM的新证据削弱了“原厂仅有不透明入口”的旧印象，不能反过来宣称我们天然更安全。</p>
<p><strong>暂定推荐：</strong>保留自有执行后端，先把UI项目中的运行协调收归运行服务；定义请求接受、运行中、停止请求、终态、回调排空和资源可释放的可观察合同，并让缓冲/模块删除绑定到相应终态。理由是这些边界直接保护当前调用方，且不依赖照搬尚未恢复的VM调度器。成本是状态迁移、失败恢复与跨层验证；待代理端调度和完成通知补证后复审，不因证据缺口选择全面重写或全局零拷贝。</p>
<h4>5．证据身份与可复查产物</h4>
<p>PlatformManager.dll：SHA256 <code>06d86f51bebfeba357d1b79297e20a3b0a70580a40af7b77c8abdd6c38ca0624</code>；SDK身份沿用上一轮记录。${link('vm-platformmanager-exports.json','平台导出身份')} · ${link('vm-native-receiver-trace.txt','SDK指针装配及完整链式范围')} · ${link('vm-platformmanager-trace.txt','平台导出入口')} · ${link('vm-job-trace.txt','执行/停止/通知候选')} · ${link('vm-sp-trace.txt','流程、缓冲、关闭与第二发送层')} · ${link('vm-loader-trace.txt','代理句柄初始化调用')}。</p>`;
html+=proof('vm-native-receiver-trace.txt','SDK：实际接收方绑定（据反汇编概括，非源码签名）','SlotInit @1806615b0\nLoadLibraryW(...\\..\\..\\Server\\PlatformManager.dll)\nGetProcAddress(module, "PF_M_MsgDeal")\nstore function pointer at object+0x2398\nSendCommand @1806c00d0 calls the pointer');
html+=proof('vm-job-trace.txt','停止：虚表调用与下层转发（概括）','ControlStop @180619670\nlookup flow by id\ncall [vtable(flow+0x218)+0x10]\nProcedureStop @1806a4120 → FilterStop @1805e3640\nBuildContinueExecuteStop @180670200\nSP_SendCommand @1805de710');
html+='</article>';
s=s.replace('<article class="vm-deep-chain" id="vm-native-p0">',()=>html+'<article class="vm-deep-chain" id="vm-native-p0">');
s=s.replace('P0进展：原生入口已进入，调度器仍未追到','上一轮P0记录：原生入口与首次间接断点');
s=s.replace('<h4>接续P0断点</h4>','<p class="note">以下为上一轮断点；+0x2398已在本轮解析，最新状态见<a href="#vm-receiver-p0">实际接收方续追</a>与下方待逆向清单。</p><h4>上一轮接续P0断点（保留追踪历史）</h4>');
s=s.replace('已进入原生导出和CServerManager命令层；实际调度/停止/销毁在间接接收目标下方，尚未闭合。先前任务的“进入原生入口”已部分完成，但接收方及资源终态仍需继续。<a href="#vm-native-p0">查看新增证据</a>。','已解析SDK+0x2398→PlatformManager!PF_M_MsgDeal，追到流程停止虚表与第二发送层+0x7d518；节点调度、停止终态、缓冲生产者和实际析构仍未闭合。<a href="#vm-receiver-p0">查看最新接收链</a>。');
const updates={
'RE-W1':['SDK→PF_M_MsgDeal→ClientMsgDeal；1030分支与流程接口、SP_Execute及第二发送层已见','真实节点排队、依赖/分支、Group、线程和优先级仍未恢复；代理目标绑定与命令落点继续核对。','先追VM_PROXY_RecvServerCommand及下层执行命令，再定位任务队列和结果生产者；后续受控验证另行安排。'],
'RE-W2':['1032→ControlStop→流程slot+0x10→ProcedureStop→FilterStop→JSON发送；回复OK路径已见','第三参数被解析，但单位/等待语义未证实；停止终态生产、回调排空及SyncWaitor解除链未连通。','追代理端停止处理与通知生产者，区分请求回复、终态和资源排空；不以候选函数中的等待常量推全局超时。'],
'RE-I1':['托管深浅拷贝/映射；原生SP_GetBinary/GetBinaryLen/ReleaseShared转发已见','生产缓冲分配、复用、映射稳定期及借用失效点仍在代理后端。','追代理端缓冲命令到生产者/回收器，关联帧代次与保留引用；后续对照连续执行、切方案与删除。'],
'RE-I2':['SP_ReleaseBinary转发；SDK动态内存释放按类型分支的函数体已补读','类型枚举与各查询对象尚未全部对应；异常/重复释放/保留引用路径尚未覆盖。','关联查询类型、分配与释放成对关系，分别核对查询内存和共享帧，不混成一次Dispose。'],
'RE-P1':['托管删除；SP_CloseOne普通ID→SP_Delete、成功后本地关系清理已见','0x3ec完整链与特殊ID关闭分支仍待连通；在途任务、回调排空及算法/驱动析构未证明。','追代理Delete处理及20000..20999特殊路径，定位实例析构、驱动引用和完成通知。']};
for(const [id,vals] of Object.entries(updates)){const re=new RegExp('(<tr id="'+id+'"[^>]*>)([\\s\\S]*?)(</tr>)');s=s.replace(re,(_,a,b,c)=>{let i=0;return a+b.replace(/<td>[\s\S]*?<\/td>/g,t=>{const n=i++;return n>=1&&n<=3?'<td>'+vals[n-1]+'</td>':t;})+c;});}
s=s.replace('已追到原生入口和命令分发，但调度接收方、缓冲寿命、模块销毁和独立插件装载仍未完成','已补SDK实际接收方、流程虚表和第二层转发，但节点调度、停止终态、缓冲寿命、模块销毁和独立插件装载仍未完成');
fs.writeFileSync(file,s);
console.log('Inserted vm-receiver-p0 and refreshed five P0 tasks.');
