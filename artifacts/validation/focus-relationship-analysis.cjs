const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const rows={
 dataflow:[
  ['定义与运行值','原版托管输出树可见；不能由树结构推导底层复制方式。','配置、绑定定义与解析后的运行值处于不同阶段。','明确每条输入是复制还是借用；核对修改运行值后编辑配置是否保持原值。'],
  ['结果与展示','VmModule.Outputs / VmIO.Children 是已恢复的展示路径。','结果写入 WorkflowContext；结果树、图像与分析面板分别消费。','以同一节点、同一轮结果核对多个消费者；不能把某个面板刷新当作全部消费者一致。'],
  ['身份与资源','native 缓冲的引用计数及最后读者仍缺直接证据。','示意图只表达主链；不证明深复制、跨作用域身份或最后读者全部闭合。','分别验失败后旧值拒收、跨轮读取与关闭后持有；最新局部结果按固定包状态引用。']
 ],
 execution:[
  ['运行范围','方案运行与所属流程运行是不同入口；运行流程不等于切换显示流程。','UI侧装配请求，执行引擎按依赖就绪派发。','将单流程、全部流程、子程序的范围逐项对照；不能仅因共享按钮或引擎而认定语义相同。'],
  ['失败与完成','托管调用入口已恢复；native 内部调度和完整异常排空仍未知。','ReadyQueue / Tool.Execute / Context 是主链概览；局部固定包验证另列。','分别核对依赖失败、跳过、异常传播和最终完成；局部 Engine 通过不能代表完整新执行合同。'],
  ['停止与退役','取消是否打断 native 调用、所有回调是否最终排空尚未闭合。','停止请求、工具结束、结果持有与UI投影不是同一个观察点。','用在途任务结束、临时资源释放和外部结果归还分别验收；等待被取消不等于实际执行停止。']
 ]
};
function enhance(html,kind){
 html=html.replaceAll('拟议设计；未实现/未运行验收','目标设计；完整合同尚未验收').replaceAll('A01 / A03 / A06；未实现/运行验收','A01 / A03 / A06目标合同；完整调用链尚未验收').replaceAll('文档交付不等产品验收；原49 / 41 / 42口径保持。','文档交付不等产品验收；统计与进度以交付总账为准。');
 if(!html.includes('2026-10-21静态基线'))html=html.replace('</figcaption>','<span class="chip">2026-10-21静态基线；后续有限验收另见记录</span></figcaption>');
 if(!html.includes('C67有限结果桥已独验'))html=html.replace('<section class="evidence-columns"','<p class="diagram-note">后续证据：<a href="optimization-o06.html#implementation-tree-projections">C67有限结果桥已独验</a>；仅覆盖其固定输入、结果配对与资源生命周期，不代表完整三投影、交互或执行链通过。</p><section class="evidence-columns"');
 if(html.includes('id="relationship-analysis"'))return html;
 const heading=kind==='dataflow'?'数据关系：差异、影响与具体改法':'执行关系：差异、影响与具体改法';
 const table='<section id="relationship-analysis"><h2>'+heading+'</h2><div class="table-scroll"><table><thead><tr><th scope="col">分析点</th><th scope="col">原版事实 / 证据边界</th><th scope="col">自研当前链 / 覆盖边界</th><th scope="col">取舍与验收动作</th></tr></thead><tbody>'+rows[kind].map(row=>'<tr>'+row.map((cell,i)=>'<td data-label="'+['分析点','原版事实 / 证据边界','自研当前链 / 覆盖边界','取舍与验收动作'][i]+'">'+cell+'</td>').join('')+'</tr>').join('')+'</tbody></table></div><p>上图沿用 2026-10-21 架构分析基线，只说明主干关系。后续实施与局部独验以 <a href="optimization-o06.html#implementation-live-status">当前实现与独立验收记录</a> 为准；未知项不因文档重排而升级为已证事实。</p></section>';
 const start=html.indexOf('<div class="compare-pair">'),target=html.indexOf('<figure class="architecture-figure"',start),mainEnd=html.lastIndexOf('</main>'),end=mainEnd<0?html.length:mainEnd;
 assert(start>0&&target>start&&end>target,'Missing current/target split');
 return html.slice(0,start)+table+'<details id="relationship-target-design"><summary>展开目标设计关系图与合同（设计参考，不代表整体已实现或验收）</summary>'+html.slice(target,end)+'</details>'+html.slice(end);
}
module.exports=enhance;
if(require.main===module){
 const layout=JSON.parse(fs.readFileSync('artifacts/validation/document-library-layout.json'));
 const root=path.resolve(layout.root),outputs=[];
 for(const kind of ['dataflow','execution']){
  const name='guide-'+kind+'.html',file=path.join(root,layout.files[name]),before=fs.readFileSync(file,'utf8');
  // Enhance already relocated links using the same relative prefix as the page.
  const after=enhance(before,kind).replaceAll('href="optimization-o06.html#implementation-tree-projections"','href="../优化方案/optimization-o06.html#implementation-tree-projections"').replace('href="optimization-o06.html#implementation-live-status"','href="../优化方案/optimization-o06.html#implementation-live-status"');
  for(const m of before.matchAll(/(?<![\w-])id="([^"]+)"/g))assert(after.includes('id="'+m[1]+'"'),'Anchor lost '+m[1]);
  fs.writeFileSync(file,after);fs.writeFileSync(path.join(layout.sourceSnapshotDirectory,name),after);
  outputs.push({file,sha256:crypto.createHash('sha256').update(after).digest('hex'),oldAnchorsPreserved:true});
 }
 const receipt={outputs,singleReturnNavigation:true,diagramFirst:true,evidenceCategories:3,analysisRowsPerPage:3,targetDesignCollapsed:true,productCodeChanged:false,visualReview:false};
 fs.writeFileSync('artifacts/validation/relationship-analysis-receipt.json',JSON.stringify(receipt,null,2));console.log(JSON.stringify(receipt));
}
