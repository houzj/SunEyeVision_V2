const fs=require('fs'),path=require('path');
const f='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html',jf='artifacts/validation/comparison-work-packages.json';
let s=fs.readFileSync(f,'utf8');if(s.includes('id="vm-four-line-round6"'))throw Error('already published');
const esc=x=>String(x).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
function link(p,label){if(!fs.existsSync(p))throw Error('missing '+p);return `<a href="file:///D:/MyWork/SunEyeVision_V2/${p}">${esc(label||p)}</a>`;}
function src(p,line,label){return link(p,(label||path.basename(p))+':'+line);}
const tr=(name,l)=>src('artifacts/validation/'+name+'-trace.txt',l,name.replace('vm-',''));
const eng=(l)=>src('src/Workflow/WorkflowExecutionEngine.cs',l,'Engine');
const mgr=(l)=>src('src/UI/Services/Workflow/WorkflowExecutionManager.cs',l,'ExecutionManager');
const tbl=(heads,rows)=>`<div class="table"><table><thead><tr>${heads.map(x=>'<th>'+x+'</th>').join('')}</tr></thead><tbody>${rows.map(r=>'<tr>'+r.map(x=>'<td>'+x+'</td>').join('')+'</tr>').join('')}</tbody></table></div>`;
let h=`<article class="vm-deep-chain" id="vm-four-line-round6"><h3>四线第六轮：四张阶段对照表与工作包验收收敛</h3><p><span class="pill">本轮交付：四表与逐包门槛盘点</span><span class="pill pending">完整验收仍0/20；不再纯粹增加调用深度</span></p><p>本轮复用前五轮静态产物，三个只读分析子智能体核对双方源码，主智能体统一回填。没有新增逆向进程、目标执行、设备测试或产品修改。表中的“局部机制已证”与“整个工作包完整验收”分开；源码链接后的行号是本轮读取定位，不是稳定API。</p><h4>A．调度、停止和执行权阶段对照（C03/C04）</h4>`;
h+=tbl(['阶段','VM实际机制与定位','SunEye实际机制与定位','具体取舍 / 验收边界'],[
['运行计划','10ae80构造拓扑并交dcba0；CBaseTopology连接源/汇节点。'+tr('vm-round2-dispatch',1448),'BuildExecutionPlan生成执行副本、含绑定的依赖图及执行配置。'+eng(789),'两边都有组织边界。VM全部控制类型、自研可变输入深度隔离仍待完整核对，不能只凭“显式计划”判优。'],
['依赖就绪','ScheduleHelper遍历邻接、互斥内减前驱数、零才分派，递归/队列混合。'+tr('vm-round4-graph-cache',552),'按前驱Completed及失败/剪枝状态处理就绪，申请资源租约后派发。'+eng(290),'局部静态机制已证。计数方案需维护重置，状态查询需维护一致性；分支/循环/同资源场景尚未完整验收。'],
['任务消费','共享或按流程ID选队列；锁内移出、锁外执行slot10。'+tr('vm-round5-core',15)+'；'+tr('vm-round5-workers',54),'并发上限、Task.Run/WhenAny；缺配置采用LegacyExclusive。'+eng(302),'VM消费者数/公平性未知；自研依赖资源声明正确性。按ID分队列不等于流程内串行，租约接口不等于无饥饿。'],
['工具执行','节点+128实现slot8或备用路径。'+tr('vm-round3-execute',212),'AlgorithmNode.ExecuteAsync传CancellationToken到执行器。'+src('src/Workflow/AlgorithmNode.cs',66),'均有调度/工具边界。VM具体实现身份、自研原生/驱动是否合作取消仍待证。'],
['停止请求','置90/80并清7c；进入等88路径。'+tr('vm-frame-stop-leaf',32),'WorkflowRunSession.RequestStop取消并唤醒暂停。'+src('src/Workflow/WorkflowRunSession.cs',130),'请求路径已证，请求成功不是任务终止。'],
['停止等待','当前标志0等88最多3000ms，超时E0000300；88早于尾处理，98更晚。'+tr('vm-frame-stop-leaf',41)+'；'+tr('vm-round2-events',391),'StopAndWaitAsync取消后轮询IsAnyRunning，超时阻止切换。'+mgr(64),'双方都有等待门槛，但88和IsAnyRunning=false均不能直接当作全部在途工具/回调排空。'],
['清理与通知','已有等98的删除路径以及manager尾部汇合548；外部通知另行传递。'+tr('vm-round3-execute',1093),'Engine finally释放剩余租约；连续模式await cameraFeed.DisposeAsync，失败警告，移任务/释放执行权再Post停止。'+eng(398)+'；'+mgr(345),'自研剩余任务未在该finally等待是静态审计点，尚非运行故障复现。推荐统一定义执行权、资源和通知各阶段，承担超时与慢驱动处理成本。']]);
h+=`<h4>B．完成事件：生产—传递—接收—确认（C12）</h4>`;
h+=tbl(['事件/状态','生产与传递','已证接收 / 未证确认','可以用于什么决策'],[
['VM退出88','dd330循环退出先Set88，之后才尾部报告/回调。'+tr('vm-round2-events',391),'dd030等待88。'+tr('vm-frame-stop-leaf',45),'确认已到循环退出阶段；不能据此删除全部回调资源。'],
['VM晚完成98','dd330最后Set98。'+tr('vm-round2-events',422),'dcae0无限等待；108c30先检查非运行再等98。'+tr('vm-round3-execute',1),'已有删除等待路径，异常出口和全部删除入口仍待补。'],
['VM多流程尾汇合548','尾回调76070原子减manager540，零Set548；6c640设计数并等待。'+tr('vm-round3-lifetime',857),'已证内部汇合，不代表SDK/UI消费者处理完成。','可区分内部批次尾完成，不能替代外部ACK。'],
['VM状态6 / 完成12','Frame→Proxy，完成12构造0xc47→PM5d51c0→5f6e50/事件0x811→6203a0。'+tr('vm-round5-receiver',244)+'；'+tr('vm-round5-finish-notify',27),'已到PM后续投递边界；最终消费者和确认未知，状态6消息不能全套用0xc47。','消息发送、业务完成和资源排空分别表示。推荐记录代次/投递结果，增加状态维护成本。'],
['自研单次完成/停止','RunSingleAsync先发布事件再finally ReleaseExecution。'+mgr(196),'OnWorkflowExecutionStopped→InvokeOnUI使用SynchronizationContext.Post，无本路径ACK。'+mgr(1373)+'；'+mgr(1428),'事件发布不等于UI已处理；UI忙闲不能作为设备释放凭据。'],
['自研连续停止 / 切方案','连续取流清理后清任务/执行权再Post；切换等登记状态。'+mgr(345)+'；'+mgr(64),'未证明残余任务与所有已排队UI回调排空，设备清理失败可能仅警告。','推荐执行完成、资源排空、投递、消费确认分别立约；普通UI投影可丢旧代次，不能自动要求每个日志ACK。']]);
h+=`<p><strong>推荐修订：</strong>两边都存在多阶段完成和异步通知；自研Task/CancellationToken/Post本身不提供更强排空保证。推荐依据是可审查的所有权与产品扩展约束，必须把不可取消调用、清理失败和迟到事件纳入成本。</p><!-- ROUND6_RESOURCE_TABLE --><!-- ROUND6_PLUGIN_TABLE -->`;
const gaps=[
'双方启动失败回滚及退出服务顺序尚未按统一门槛复核。',
'运行代次、节点/设备身份与迟到回调的全对象所有权表尚未闭合。',
'就绪与消费已证；分支、循环、Group/子流程和跨流程失败传播覆盖不完整。',
'资源声明、共享相机冲突、公平性与取消排队没有完整同场景证据。',
'标量/数组/图像/动态端口的双边解析及失效绑定四场景尚未齐全。',
'五类所有权本轮汇总；原始帧复用与最后读者、异常出口仍缺证。',
'相机/标准通信/特殊SDK三类接入及重连没有统一失败恢复表。',
'标准删除及代表析构已证；在途回调、特殊入口和库所有者仍待证。',
'草稿提交/取消/关窗/切方案与迟到调试结果尚未逐入口验收。',
'显示对象与坐标、ROI、选中切换及回放所有权需专门对齐。',
'双方版本迁移、保存失败/恢复、外部依赖持久化尚未统一复核。',
'完成事件表已交；异常出口、不可取消任务与最终消费者确认仍缺证。',
'关联ID、错误分类、恢复动作和跨层日志追踪尚未形成统一对照。',
'插件依赖发布/缺件诊断/升级回滚的双边安装产物链未完整验收。',
'权限入口、脚本信任/隔离与缓存/卸载的双边边界尚未系统复核。',
'仅有机制建议，尚未按相同输入/环境/范围形成完整指标和实验计划；不评性能胜负。',
'已有变量链；数组/图像浅复制、生产冲突和删除改名的双边矩阵未齐。',
'目录/连接/预览/正式采集的身份、触发写入、断线和切方案未逐项闭合。',
'SDK/控件/模板拆分建议尚缺三类代表工具的真实依赖消费者全表。',
'13项已有正文，逐项正常/忙/错误/关闭状态证据和边界验收尚未齐全。'];
const j=JSON.parse(fs.readFileSync(jf,'utf8'));
h+=`<h4>D．20工作包验收门槛盘点：不靠重复深挖抬高进度</h4><p>以下是对现有材料与原完成条件的缺口盘点，不冒称本轮已系统复核所有领域。7个包保留“链路部分”，其余13个仍待统一复核。完整验收要求原工作包条件有可追溯材料，不能以一段附条件推荐替代。后续优先补覆盖面及具体场景，避免让所有包等待同一条P0原生链。</p>`;
h+=tbl(['工作包','现有分析入口','当前验收 / 尚欠什么'],j.packages.map((p,i)=>[p[0]+' · '+esc(p[2]),`<a href="#${p[8]}">原分析</a> · <a href="#plan-${p[0]}">原验收条件</a>`,(j.round1.packages.includes(p[0])?'链路部分；':'待统一复核；')+gaps[i]]));
h+=`<h4>本轮停止及下一批有限交付</h4><p>本轮四表和门槛盘点完成即停止，不新增原生断点。本轮关闭的是“四张表尚未成形”的交付缺口，不是7类RE任务或新原生断点。已连续两轮工作包完整验收无增长，下一轮触发覆盖审计，停止纯P0下钻。</p><ol><li>A：C02/C05，完成身份/作用域及标量、数组、图像、动态端口四场景输入绑定矩阵。</li><li>B：C10/C19，完成图像显示坐标/ROI及SDK、公共控件、模板真实消费者矩阵。</li><li>C：C07/C18，完成相机、标准通信、特殊SDK接入及预览/正式采集/断线恢复矩阵。</li><li>D：C20，逐项审阅13个UI部件的动作、调用责任、忙/错误/关闭状态及证据，再统一逐包验收。</li></ol><p>下一轮四项矩阵、证据缺口与验收判定回填、离线验证通过即收尾。不得自动增加函数深度；缺证据对应限制结论，保留暂缓项。新原生分析仅在能明确改变哪项决策时另选有限检查点，不以所有DLL源码恢复作为文档交付前提。</p></article>`;
// Supplement tables are prepared after read-only reviewers return.
const supplement=JSON.parse(fs.readFileSync('artifacts/validation/round6-tables.json','utf8'));
h=h.replace('<!-- ROUND6_RESOURCE_TABLE -->',()=>'<h4>C．五类图像对象所有权（C06/C10）</h4>'+tbl(supplement.resources.heads,supplement.resources.rows)+supplement.resources.note);
h=h.replace('<!-- ROUND6_PLUGIN_TABLE -->',()=>'<h4>D．插件创建、删除、析构与库释放（C08）</h4>'+tbl(supplement.plugins.heads,supplement.plugins.rows)+supplement.plugins.note);
s=s.replace('<article class="vm-deep-chain" id="vm-four-line-round5">',()=>h+'<article class="vm-deep-chain" id="vm-four-line-round5">');
const note='<p class="note"><strong>第六轮收敛：</strong>新增双方四张阶段/所有权表和20工作包门槛盘点，自研停止通知也是Post且无本路径ACK；双方排空合同均须补证。<a href="#vm-four-line-round6">阅读可评审对照及下一轮覆盖任务</a>。完整验收仍0/20，不把四表当作四包完成。</p>';
for(const marker of ['<h2>四、全系统优化方案与推荐理由</h2>','<h3>四线第五轮：真实队列消费、完成消息接收、批次加载与相机析构</h3>','<h3>仍需继续逆向：任务、证据缺口与结论限制</h3>'])s=s.replace(marker,m=>m+note);
s=s.replace('<h3>交付进度与每轮停止条件 · 第五轮基线</h3>',m=>'<h3>交付进度与每轮停止条件 · 第六轮</h3>'+note);
s=s.replace('<td>第五轮预定局部检查点</td><td>4/4</td><td>真实消费、Server接收、加载任务实际调用、代表相机析构各补齐一个既有边界</td>','<td>第六轮有限交付</td><td>4/4张表</td><td>调度停止、完成事件、五类图像、插件生命周期；新增原生分析0，完整验收增量0</td>');
s=s.replace('<h4>下一轮固定四项交付，先收敛已有证据</h4>','<h4>第五轮约定的四项交付（第六轮已交；下轮转覆盖审计）</h4>');
fs.writeFileSync(f,s);
j.round6={anchor:'vm-four-line-round6',status:'四表与20包门槛盘点交付；完整验收增量0，触发覆盖审计',deliverables:['调度/停止阶段表','完成事件传递确认表','五类图像所有权表','插件生命周期表及20包门槛盘点'],nativeAnalysesStarted:0,nativeBreakpointsClosed:0,packageAudit:j.packages.map((p,i)=>({id:p[0],status:j.round1.packages.includes(p[0])?'partial':'pending',gap:gaps[i]})),next:['A:C02/C05身份输入绑定四场景','B:C10/C19显示坐标与SDK控件模板消费者','C:C07/C18设备接入与相机采集恢复','D:C20全部13项UI状态/责任/证据'],exit:'四项矩阵+逐包判定+HTML验证即停止，不自动深挖'};
Object.assign(j.progressPolicy,{round:6,roundTargets:4,roundTargetsMet:4,closedExistingCheckpoints:0,wholeTasksClosed:0,documentationDeliverablesMet:4,nativeAnalysesStarted:0,consecutiveRoundsWithoutPackageAcceptance:2,nextMode:'覆盖审计与文档验收，禁止自动原生深挖'});
fs.writeFileSync(jf,JSON.stringify(j,null,2)+'\n');
const cf='artifacts/validation/check-research-reader.cjs';let c=fs.readFileSync(cf,'utf8');c=c.replace("await route('vm-four-line-round5');", "await route('vm-four-line-round6');assert.equal(await page.locator('.vm-deep-chain:visible').count(),1);assert.equal(await page.locator('#vm-four-line-round6 table').count(),5);await page.screenshot({path:'artifacts/validation/research-reader-four-line-round6.png'});await route('vm-four-line-round5');");fs.writeFileSync(cf,c);
console.log('Published four round6 tables and package gate audit.');
