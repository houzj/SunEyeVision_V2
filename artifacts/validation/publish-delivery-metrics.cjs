const fs=require('fs');const file='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html',jf='artifacts/validation/comparison-work-packages.json';let s=fs.readFileSync(file,'utf8');if(s.includes('id="analysis-delivery-review"'))throw Error('already published');
const r=JSON.parse(fs.readFileSync('artifacts/validation/delivery-review.json','utf8')),j=JSON.parse(fs.readFileSync(jf,'utf8'));
const tbl=(heads,rows)=>'<div class="table"><table><thead><tr>'+heads.map(x=>'<th>'+x+'</th>').join('')+'</tr></thead><tbody>'+rows.map(x=>'<tr>'+x.map(c=>'<td>'+c+'</td>').join('')+'</tr>').join('')+'</tbody></table></div>';
const refs=anchors=>anchors.map(a=>`<a href="#${a}">${a}</a>`).join(' · ');
// Fix a genuine content attribution contradiction before accepting C17.
let old='已确认变量定义、编辑及部分读取入口。VM 原生变量一致性和整个保存事务尚未知；本轮已追到 RunSolutionAsync：根据生产消费关系排序流程，ApplyProducedGlobalValues 先移除旧值、仅提交成功生产者到 runtimeGlobals，不改方案初值；共享图像释放和全部并发场景仍未闭合。详见补漏调用链。';
if(!s.includes(old))throw Error('C17 VM baseline mismatch');
s=s.replace(old,'已确认VM变量定义、编辑、部分读取及绑定维护入口。VM原生变量一致性、生产者提交时机、失败值策略和整个保存事务仍未知；不能以自研RunSolutionAsync或ApplyProducedGlobalValues补成VM事实。共享图像/数组寿命及全部并发场景仍未闭合。');
old='已确认变量定义、编辑及部分读取入口。VM 原生变量一致性和整个保存事务尚未知；我们的生产者写回时机、跨流程可见性、失败值/上一轮值及图像释放还需贯通，不能因有唯一生产者字段就认定并发问题已解决。';
if(!s.includes(old))throw Error('C17 ours baseline mismatch');
s=s.replace(old,'自研RunSolutionAsync按生产消费依赖选择流程；ApplyProducedGlobalValues先从runtimeGlobals移除生产变量旧值，仅提交本轮成功生产者的有效结果，不改Solution.GlobalVariables初值。该方法的提交时机/失败清旧值已核实；数组/图像浅引用、全部并发冲突和迟到读者仍未知，不能由唯一生产者字段推出并发问题已解决。<a href="file:///D:/MyWork/SunEyeVision_V2/src/UI/Services/Workflow/WorkflowExecutionManager.cs">WorkflowExecutionManager.cs:421、1057</a>。');
// Keep prior rounds as historical records; explicitly supersede their mixed metric.
s=s.replaceAll('总任务完整验收0/20＝0%，本轮+0个百分点','旧混合口径记录：0/20；已由双统计复审替代');
s=s.replaceAll('总完成率0/20＝0%','旧混合口径0/20（不再作为分析交付完成率）');
s=s.replace('总任务完成百分比＝完整验收通过的工作包数÷20×100%。部分核实不折算；当前0/20＝0%。','以下为第七轮历史混合口径，已废止：当时将证据整体验收与分析交付合并为0/20。当前采用<a href="#analysis-delivery-review">分析交付85%、关键证据0/7整项闭合</a>双统计。');
const currentNote='<p class="note"><strong>当前统计已修订：</strong>分析交付17/20＝85%；关键证据整项闭合0/7＝0%（7项均有部分证据）。此前“0/20完整验收”是混合旧口径，不能表示已交分析为零。<a href="#analysis-delivery-review">20包复审、验收门槛和真实剩余内容</a>。</p>';
let h='<article class="vm-deep-chain" id="analysis-delivery-review"><h3>20工作包双统计复审：分析交付与关键证据分别验收</h3>'+currentNote+'<p>本次只复审现有分析、修正文档和计量标准，不新增原生逆向或产品修改。17包通过是对既有交付按新标准重新认证，<strong>不是本轮新增17包内容或17包运行证据</strong>。三位只读分析者分组审阅，主智能体核对并修正C17归属矛盾后统一写入。</p>';
h+='<h4 id="delivery-acceptance-standard">分析交付验收：五项同时满足</h4><ol>'+r.criteria.map(c=>'<li>'+c+'。</li>').join('')+'</ol><p>未知可以作为结论的一部分，前提是定位到具体缺口、说明它会影响哪项比较/推荐，给出适用/暂缓条件和有限验证。缺失事实不得猜测，也不得把“未恢复”写成VM不支持。只有目录或通用优缺点、无法关联消费者/调用责任、遗漏子域或不可执行的计划，仍属于文档未完成。</p><p><strong>分析交付完成度＝五项全部通过的工作包数÷固定20包×100%＝17/20＝85%。</strong>不折算部分通过，不按字数/函数数计量，也不需要等用户人工产品验收。原工作包的场景范围保持：将其作为分析必须讨论的范围；未知写进证据栏，不要求先把所有实现/设备场景跑完才能交付。</p>';
h+='<h4 id="evidence-closure-standard">关键证据闭合度：沿用原7类RE任务分母</h4><p><strong>关键证据闭合度＝达到该RE任务声明范围完整合同的任务数÷固定7项×100%＝0/7＝0%。</strong>7项均有部分静态证据，0整项闭合不等于没有逆向成果。保留这项严格口径，不用分析通过替换证据通过，不另挑容易关闭的小函数抬高百分比。</p><p>闭合要求来源/版本可追溯、实际调用与数据/状态转移对应、失败/取消及创建到释放的关键出口有证据，并且足以支持该项声明的结论。静态机制可在其限定范围认证；涉及驱动时序、运行安全或性能的结论仍需相应运行证据。原16域覆盖表保留为范围/证据历史索引，不再充当分析交付进度。</p>';
h+='<h4>逐包复审：五门槛、交付判定与剩余内容</h4><p>五门槛依次为A双方事实/职责链，B同场景比较，C具体利弊成本，D条件推荐，E未知影响与有限验证。✓已满足；△文档还需补齐。证据未闭合列独立保留，不把该列全清空作为文档通过条件。</p>';
h+=tbl(['工作包 / 阅读位置','A B C D E','分析交付判定及依据','真正还差的文档','独立关键证据缺口'],r.packages.map(p=>[p.id+' · '+j.packages.find(x=>x[0]===p.id)[2]+'<p>'+refs(p.anchors)+'</p>',p.criteriaPassed.map(b=>b?'✓':'△').join(' '),(p.analysisAccepted?'通过（有明确边界）':'未通过')+'<p>'+p.basis+'</p>',p.remaining,p.evidenceGap]));
h+='<h4 id="remaining-analysis-deliverables">剩余分析交付：仅3包，完成后按同一标准复审</h4>'+tbl(['工作包','下一份具体交付','结束/验收条件'],[
['C14 部署','一张缺DLL/契约版本/架构/升级失败四场景对照表，关联现有构建规则、输出目录、诊断与推荐。','双方每格给证据或显式未知、影响、有限干净环境验证；本轮不要求实际部署验证通过。'],
['C15 权限','一张UI可见性—服务授权—脚本/DLL信任—真实设备写入—拒绝审计的操作责任矩阵。','UI/通信/脚本同动作各入口责任与未知标明，权限/范围校验分开，给条件推荐和拒绝验证。'],
['C16 性能计划','一份可执行的同输入/等算法负载计划，含环境、预热、档位、时长/次数、采样指标、故障取消注入及停止条件。','明确采集/显示/传输是否计入、样本/判定与不支持的结论；交计划可通过，无数据继续不评性能胜负。']]);
h+='<p>三包可先基于已有材料补文档，不默认新增逆向。满足五门槛后逐包复审，可使分析交付到20/20；证据仍未闭合时保持其独立限制。100%分析交付也不表示产品已优化、原厂已完整逆向或运行验证全部通过。</p><h4>关键证据7项：已见什么、还缺什么、如何限制推荐</h4>';
h+=tbl(['任务','已核实限定范围','尚未整项闭合','对推荐的影响 / 有限后续'],r.evidenceTasks.map(e=>['<a href="#'+e.id+'">'+e.id+'</a> · '+e.name,e.verified+'<p>'+refs([e.anchor])+'</p>',e.gap,e.impact+'<p>'+e.next+'</p>']));
h+='<p><strong>每次结束固定报告：</strong>分析交付X/20及百分比、关键证据Y/7及百分比、与上次同口径相比的增量、本轮新增/修订、剩余文档和下一轮停止条件。本次为口径切换基线，不把0%→85%描述为本轮完成85%的工作。</p></article>';
s=s.replace('<article class="vm-deep-chain" id="vm-four-line-round7">',()=>h+'<article class="vm-deep-chain" id="vm-four-line-round7">');
for(const marker of ['<h2>四、全系统优化方案与推荐理由</h2>','<h3>仍需继续逆向：任务、证据缺口与结论限制</h3>','<h2>完整对比计划：驱动HTML分析与方案落地</h2>','<h3>四线第七轮：覆盖审计四组矩阵与逐项验收</h3>'])s=s.replace(marker,m=>m+currentNote);
const progress='<article id="comparison-progress"><h3>当前进度：分析交付85%，关键证据0/7整项闭合</h3>'+tbl(['指标','计算与结果','解释'],[['分析交付完成度','17/20＝85%','当前总任务的文档交付进度；剩C14/C15/C16'],['关键证据闭合度','0/7＝0%；7项均部分核实','原7类RE完整合同；已证子链不丢失、不等全类闭合'],['本次复审','20/20包已逐项审阅','17通过、3文档待补；C17归属/旧状态已修正'],['产品/运行验收','未在本次执行','不改产品、不运行VM/设备、不构建发布']])+'<p>分析交付按五门槛认证：双方事实/文件调用责任、同场景比较、具体利弊成本、条件推荐、未知影响和有限验证。明确写出未知可形成合格分析，不等待全库逆向。关键证据保持原严格范围独立计数。</p><p>旧“0/20”混合了两种验收，已废止；本次为新口径基线，不声称本轮新完成85%。<a href="#analysis-delivery-review">20包逐项依据</a> · <a href="#remaining-analysis-deliverables">剩余3包交付和退出条件</a> · <a href="#evidence-closure-standard">证据闭合标准</a>。以后每次报告两项百分比及同口径增量。</p></article>';
s=s.replace(/<article id="comparison-progress">[\s\S]*?<\/article>/,()=>progress);
for(const p of r.packages){
 const status=p.analysisAccepted?'分析交付通过（证据限制独立保留）':'分析交付待补（见复审具体缺口）';
 const re=new RegExp('(<tr><td><a href="#plan-'+p.id+'">[\\s\\S]*?</a><p>)[^<]*(</p>)');s=s.replace(re,(_,a,b)=>a+status+b);
 const ar=new RegExp('(<article class="comparison-package" id="plan-'+p.id+'">[\\s\\S]*?<p>)[\\s\\S]*?(</p>)');
 s=s.replace(ar,(_,a,b)=>a+'<span class="pill">'+j.packages.find(x=>x[0]===p.id)[1]+'</span> '+status+'；<a href="#analysis-delivery-review">双统计验收依据</a>。'+b);
}
// Refresh the seven preserved RE row anchors; no duplicate tasks or stale next-call instructions.
for(const e of r.evidenceTasks){const re=new RegExp('<tr id="'+e.id+'" class="reverse-pending">[\\s\\S]*?</tr>');s=s.replace(re,()=>'<tr id="'+e.id+'" class="reverse-pending"><td><strong>'+e.id+'</strong><p>'+e.name+'</p><span class="pill pending">部分核实，未整项闭合</span></td><td>'+e.verified+'<p>'+refs([e.anchor])+'</p></td><td>'+e.gap+'</td><td>'+e.next+'</td><td>'+e.impact+'</td></tr>');}
fs.writeFileSync(file,s);
j.legacyProgressPolicy=j.progressPolicy;
j.progressPolicy={schemaVersion:2,primaryMetric:'analysisDelivery',analysisAccepted:17,analysisTotal:20,analysisCompletionPercent:85,evidenceClosed:0,evidenceTotal:7,evidenceClosurePercent:0,packageAccepted:17,packagePartial:3,packagePending:0,totalCompletionPercent:85,totalCompletionFormula:'五项文档交付门槛全部通过的包数/20*100；证据缺口独立限制推荐',baselineChange:true,deltaSinceSameMetricBaseline:null,note:'当前用户授权的新双统计取代旧混合口径；不是本轮新增17包证据；关键证据维持原7类RE分母',next:['C14四类部署失败对照','C15操作授权责任矩阵','C16对等性能实验计划'],exit:'三份文档补齐逐包复审+HTML验证即结束，不自动原生深挖'};
j.deliveryReview={source:'delivery-review.json',anchor:'analysis-delivery-review',analysis:r.analysis,evidence:r.evidence,packages:r.packages,evidenceTasks:r.evidenceTasks};fs.writeFileSync(jf,JSON.stringify(j,null,2)+'\n');
const cf='artifacts/validation/check-research-reader.cjs';let c=fs.readFileSync(cf,'utf8');c=c.replace("await route('comparison-progress');", "await route('analysis-delivery-review');assert.equal(await page.locator('#analysis-delivery-review table').count(),3);await page.screenshot({path:'artifacts/validation/research-reader-delivery-review.png'});await route('comparison-progress');");fs.writeFileSync(cf,c);
const rc='artifacts/validation/check-round7-coverage.cjs';c=fs.readFileSync(rc,'utf8').replace('totalCompletionPercent:0,coveragePercent:65','totalCompletionPercent:j.progressPolicy.analysisCompletionPercent,evidenceClosurePercent:j.progressPolicy.evidenceClosurePercent,legacyRound7CoveragePercent:65');fs.writeFileSync(rc,c);
console.log('Published dual metrics: analysis85%, key evidence0/7; C17 corrected, 3 true document gaps.');
