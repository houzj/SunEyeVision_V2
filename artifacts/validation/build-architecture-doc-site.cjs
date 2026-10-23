const fs=require('fs'),path=require('path'),crypto=require('crypto');
const dir='docs/04-工具设计/架构对照/2026/10/21/';
const E=s=>String(s??'').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function sections(html,cls){
 const out=[],re=/<\/?section\b[^>]*>/g;let match,depth=0,start=-1;
 while((match=re.exec(html))){if(match[0].startsWith('</')){depth--;if(start>=0&&depth===0){out.push(html.slice(start,re.lastIndex));start=-1;}}
 else {if(start<0&&new RegExp('class="[^"]*\\b'+cls+'\\b').test(match[0])){start=match.index;depth=0;}if(start>=0)depth++;}}
 return out;
}
module.exports=function(){
 require('./document-library-layout.cjs').restore();
 require('./build-joint-optimization-plan.cjs')();
 const handoffPath='docs/collaboration/ARCH_TARGET_VM_REVIEW_20261022.json',d=JSON.parse(fs.readFileSync(handoffPath)),contracts=require('./architecture-target-contracts.cjs');
 const legacyPath=dir+'framework-design-comparison.html';let full=fs.readFileSync(legacyPath,'utf8');
 if(full.includes('data-legacy-router'))full=fs.readFileSync(dir+'framework-print.html','utf8');
 const framework=JSON.parse(fs.readFileSync('artifacts/validation/framework-design-comparison.json'));
 const sdk=JSON.parse(fs.readFileSync('artifacts/validation/sdk-development-design.json'));
 const official=JSON.parse(fs.readFileSync('artifacts/validation/framework-official-sdk-reference.json'));
 const checks=[...d.inputs,...d.sourceCheck.map(s=>({path:s.path,sha256:s.expected})),...d.ownSources];
 const drift=checks.map(s=>({...s,currentSha256:fs.existsSync(s.path)?hash(s.path):null})).filter(s=>s.currentSha256!==s.sha256.toLowerCase());
 const pages=[],anchors={},chunks=sections(full,'framework-view');
 if(chunks.length!==9)throw Error('expected 9 framework views, got '+chunks.length);
 for(const chunk of chunks){const id=chunk.match(/^<section[^>]*id="([^"]+)"/)[1];
  if(id==='sdk'){
   const panels=sections(chunk,'sdk-panel');if(panels.length!==12)throw Error('expected 12 SDK panels');
   for(const panel of panels){const pid=panel.match(/^<section[^>]*id="([^"]+)"/)[1];const title=panel.match(/<h[23][^>]*>(.*?)<\/h[23]>/s)?.[1].replace(/<[^>]+>/g,'')||pid;
    pages.push({file:'framework-'+pid+'.html',id:pid,title,body:panel,kind:'sdk'});}
   anchors.sdk='framework-sdk-overview.html';
  }else pages.push({file:'framework-'+id+'.html',id,title:framework.views.find(v=>v.id===id).title,body:chunk,kind:'framework'});
 }
 for(const p of pages)for(const m of p.body.matchAll(/\bid="([^"]+)"/g))anchors[m[1]]=p.file;
 const style=fs.readFileSync('artifacts/validation/architecture-doc-site.css','utf8');
 fs.writeFileSync(dir+'architecture-doc-site.css',style);
 const sharedNav='<nav aria-label="文档导航"><a href="index.html">文档索引</a><a href="architecture-target.html">最新目标决策</a><a href="framework-overview.html">框架对比</a><a href="framework-sdk-overview.html">SDK设计</a><a href="joint-optimization-plan.html">联合方案</a><a href="architecture-evidence.html">本批证据</a></nav>';
 function page(file,title,body){
  const V=require('./doc-visual-components.cjs');
  if(/^architecture-a\d\d\.html$/.test(file)){const id=file.match(/a(\d\d)/)[1],x=d.decisions.find(x=>x.id==='A'+id),c=contracts.find(x=>x.id==='A'+id);const tail=body.slice(body.indexOf('<h2>拟议接口与数据字段</h2>'));body='<article id="decision-'+x.id+'"><p class="status">proposed-not-implemented · 全部接口为拟议合同，未编译/运行</p>'+V.decisionVisual(x,c)+tail;}
  if(/^framework-/.test(file))body=V.decorate(file,body);
  if(file==='architecture-target.html')body=V.metrics([[12,'目标决策','不是产品完成数'],[24,'验收计划','运行0项'],[53,'选定来源SHA','局部范围']])+V.diagram('execution')+body.replace(/<ol>[\s\S]*?<\/ol>/,'<div class="visual-grid">'+d.decisions.map(x=>'<a href="architecture-'+x.id.toLowerCase()+'.html#decision-'+x.id+'"><span class="section-label">'+x.id+' · 拟议</span><strong>'+E(x.topic)+'</strong><small>'+E(x.route)+'</small></a>').join('')+'</div>');
  if(file==='architecture-acceptance.html')body=V.flow(['准备样本与故障注入','执行指定操作','观察结果/Owner','保存判定证据'],'如何使用验收计划','24项均未运行')+body;
  if(file==='architecture-evidence.html')body=V.metrics([[2,'模型身份','原批次登记'],[28,'原版证据文件','局部边界'],[8,'自研关键源码','包含dirty快照'],[drift.length,'来源漂移','待语义复核']])+V.diagram('evidence')+'<aside>本次只升级文档组织与表达，沿用原交接中的局部事实和历史文件SHA；当前来源变化不自动更新结论。</aside>'+drift.map(s=>'<details><summary>待复核漂移：'+E(s.path)+'</summary><p>原登记 <code>'+s.sha256+'</code></p><p>本次观察 <code>'+E(s.currentSha256||'missing')+'</code></p><p>未进行新语义复核，不升级证据状态。</p></details>').join('')+body;
  fs.writeFileSync(dir+file,'<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>'+E(title)+'</title><link rel="stylesheet" href="architecture-doc-site.css"></head><body>'+sharedNav+'<main><h1>'+E(title)+'</h1>'+body+'</main><footer>文档设计交付 ≠ 产品实现或运行验收；原42检查点、49能力口径保持。</footer><script src="architecture-doc-site.js"></script></body></html>');}
 const rewrite=body=>body.replace(/\s+hidden(?=[\s>])/g,'').replace(/<script\b[^>]*>[\s\S]*?<\/script>/g,'').replace(/href="#([^"]+)"/g,(m,id)=>anchors[id]?'href="'+anchors[id]+'#'+id+'"':m);
 const latest='<aside>历史分析基线为2026-10-21；2026-10-22目标取舍以 <a href="architecture-target.html">12项最新决策</a> 为准。旧建议保留作追溯；新证据仅覆盖所选边界，不能外推完整VM实现。</aside>';
 for(const p of pages)page(p.file,p.title,latest+(p.id==='sdk-overview'?'<p id="sdk">SDK专题入口</p>':'')+'<div class="topic-links">'+pages.filter(x=>x.kind===p.kind).map(x=>'<a '+(x===p?'aria-current="page" ':'')+'href="'+x.file+'">'+E(x.title)+'</a>').join('')+'</div>'+rewrite(p.body));
 // Retain the complete prior document only for optional archival printing and legacy regression.
 fs.writeFileSync(dir+'framework-print.html',full);
 const routeJs='(()=>{const map='+JSON.stringify(anchors)+';let id;try{id=decodeURIComponent(location.hash.slice(1))}catch{id=""}if(map[id])location.replace(map[id]+"#"+encodeURIComponent(id));else if(!id)location.replace("framework-overview.html");else document.getElementById("unknown-anchor").hidden=false;})();';
 fs.writeFileSync(legacyPath,'<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>框架文档兼容入口</title><link rel="stylesheet" href="architecture-doc-site.css"></head><body data-legacy-router>'+sharedNav+'<main><h1>框架文档已按专题拆分</h1><p id="unknown-anchor" hidden>未识别旧锚点，请使用目录查找；没有跳转到无关结论。</p><ul>'+pages.map(p=>'<li><a href="'+p.file+'">'+E(p.title)+'</a></li>').join('')+'</ul><p><a href="framework-print.html">历史合订打印版</a></p></main><script>'+routeJs+'</script></body></html>');
 const decisionNav=d.decisions.map(x=>'<li><a href="architecture-'+x.id.toLowerCase()+'.html#decision-'+x.id+'">'+x.id+' '+E(x.topic)+'</a> — '+E(x.route)+'</li>').join('');
 page('architecture-target.html','目标架构：与VM逐项取舍','<aside>本批 ARCH-TARGET-20261022-01：12项拟议设计，24项验收均 planned-not-run；实现执行数0。目标质量优先，迁移成本另章。未知不解释为VM不支持。</aside><ol>'+decisionNav+'</ol><h2>贯穿关系</h2><p>UI / SDK / 触发 → A01准入 → A04快照 + A02计划 → A05绑定 → 工具执行 → A06结果发布 → UI与业务消费；A07图像Owner和A08关闭贯穿全链，A03停止控制在途，A09/A10定义包边界，A11持久化，A12能力准入。</p><p><a href="framework-implementation.html">实施迁移</a> · <a href="framework-sdk-engineering.html">工程设计</a> · <a href="architecture-acceptance.html">24项验收</a></p>');
 const officialSources=[...official.docs,...sdk.sources.filter(s=>s.id.startsWith('sdk-doc-'))];
 const uniqueOfficial=[...new Map(officialSources.map(s=>[s.path,s])).values()];
 for(const x of d.decisions){const c=contracts.find(c=>c.id===x.id);if(!c)throw Error('missing contract '+x.id);
  const evidence=x.vmEvidence.map((v,i)=>'<details id="evidence-'+x.id+'-'+i+'"><summary>'+E(v.id)+' · '+E(v.classification)+' · '+E(v.member)+'</summary><p>'+E(v.behavior)+'</p><ul>'+v.limits.map(l=>'<li>未知/限制：'+E(l)+'</li>').join('')+'</ul>'+v.refs.map(r=>{const absolute=path.isAbsolute(r.path)?r.path:'D:/MyWork/REA逆向测试/'+r.path;return '<p><a href="'+require('url').pathToFileURL(absolute).href+'">'+E(r.path)+':'+r.start+'–'+r.end+'</a> · '+E(r.evidenceType)+' · 原二进制身份：'+E(r.binaryIdentity)+'</p><p>SHA256 <code>'+E(r.sha256)+'</code></p><pre>'+E(r.excerpt)+'</pre>';}).join('')+'</details>').join('');
  page('architecture-'+x.id.toLowerCase()+'.html',x.id+' '+x.topic,'<article id="decision-'+x.id+'"><p class="status">proposed-not-implemented · 全部接口为拟议合同，未编译/运行</p><h2>VM已证与未知</h2><p>'+E(x.vm)+'</p><h2>自研现状与不足</h2><p>'+E(c.ours)+'</p><h2>目标选择</h2><p><b>'+E(x.route)+'</b></p><p>'+E(x.target)+'</p><h2>收益、代价和替代方案</h2><p>'+E(x.tradeoff)+'</p><p>'+E(c.alternative)+'</p><h2>拟议接口与数据字段</h2><pre>'+E(c.api)+'</pre>'+[['状态与生效时点',c.state],['Owner与释放责任',c.owner],['失败、取消与幂等',c.failure],['版本与兼容',c.version]].map(([title,text])=>'<h3>'+title+'</h3><p>'+E(text)+'</p>').join('')+'<h2>验收：尚未执行</h2>'+x.acceptance.map(t=>'<section id="'+t.id+'"><h3>'+t.id+' · planned-not-run</h3><p>'+E(t.text)+'</p><p>执行方法与判定：<a href="architecture-acceptance.html#'+t.id+'">查看用例</a></p></section>').join('')+'<h2>原版证据与局部边界</h2><p>source-confirmed限交接中选定成员；不是整库复核。独立恢复代码 independent-implementation 不作为原版事实。<a href="architecture-evidence.html">本批来源SHA</a></p>'+evidence+(['A09','A10'].includes(x.id)?'<h2>官方SDK依据</h2><p>复用15个官方页面来源；指南V4.4.2、安装目录4.4.0与反编译样本版本分列；旧控件文档.NET Framework及.NET Core限制不直接转化为自研net9承诺。</p>'+uniqueOfficial.map((s,i)=>'<p><a href="architecture-evidence.html#official-'+i+'">'+E(s.id||s.title||s.path)+'</a></p>').join(''):'')+'</article><p><a href="architecture-target.html">返回12项决策目录</a></p>');
 }
 const scenarios=require('./architecture-target-test-plans.cjs');
 page('architecture-acceptance.html','24项实现验收计划（未运行）','<aside>以下是未来产品测试计划。本次离线HTML验证不执行这些用例，不升级原42检查点。</aside>'+d.decisions.flatMap(x=>x.acceptance.map((t,i)=>{const p=scenarios[x.id][i];return '<section id="'+t.id+'"><h2>'+t.id+' '+E(t.text)+'</h2><p>状态：planned-not-run</p><dl><dt>准备/操作</dt><dd>'+E(p[0])+'</dd><dt>判定与需要保存的证据</dt><dd>'+E(p[1])+'</dd></dl><a href="architecture-'+x.id.toLowerCase()+'.html#decision-'+x.id+'">对应合同</a></section>';})).join(''));
 page('architecture-evidence.html','本批证据与同步边界','<p>代码HEAD：3a5dc6dd54f372699a450ad7862b52c961cd2797；存在产品dirty，不是稳定验收基线。以下文件SHA为本批读取时身份，收到不等于采用或运行验证。</p><h2>2模型 / 28原证据 / 8自研关键源</h2>'+checks.map((s,i)=>'<details id="source-'+i+'"><summary>'+E(s.path)+'</summary><p>SHA256 <code>'+E(s.sha256)+'</code></p></details>').join('')+'<h2>15官方SDK页面与版本边界</h2>'+uniqueOfficial.map((s,i)=>'<details id="official-'+i+'"><summary>'+E(s.id||s.path)+'</summary><p>'+E(s.path)+'</p><p>SHA256 <code>'+hash(s.path)+'</code></p></details>').join('')+'<h2>明确未知</h2><p>原生调度公平/抢占、完整在途回调排空、原生图像最终释放、工具窗通用Run上游与多项目隔离均不能由本批局部材料自动闭合。DI各池/映射/借用边界分开；DR01查询Release finally不等所有权统一；DR02不证明幂等；DR03三仓不等lease；DR04/05为独立实现。</p><p>原42检查点及49能力未改。旧总入口参考模型SHA漂移仍单列待复核，不因本批两份JSON模型核对而自动更新旧261来源基线。</p>');
 const existing=[...new Set([...JSON.parse(fs.readFileSync('artifacts/validation/entry-pages-manifest.json')).pages,'data-architecture.html','recommendation-audit.html','joint-optimization-plan.html','internal-secure-export.html'])].filter(f=>fs.existsSync(dir+f));
 for(const f of existing.filter(f=>!['joint-optimization-plan.html','internal-secure-export.html'].includes(f))){let html=fs.readFileSync(dir+f,'utf8').replace(/<aside data-doc-index>[\s\S]*?<\/aside>/g,'');html=html.replace(/(<main\b[^>]*>)/,'$1<aside data-doc-index><a href="index.html">文档索引</a> · <a href="architecture-target.html">2026-10-22最新目标取舍</a>（拟议、未实施；本页历史分析状态不自动升级）</aside>');fs.writeFileSync(dir+f,html);}
 page('index.html','SunEyeVision 架构文档索引','<aside>最新设计：2026-10-22 · 12项拟议取舍 / 24项未运行验收。产品代码未由文档任务修改；历史42证据与49能力保持独立口径。</aside><h2>从这里阅读</h2><ol><li><a href="architecture-target.html">最新目标架构与12项VM对照</a></li><li><a href="architecture-acceptance.html">合同对应的24项验收计划</a></li><li><a href="architecture-evidence.html">证据与同步边界</a></li><li><a href="joint-optimization-plan.html">联合优化批次路线</a></li></ol><h2>框架与SDK独立专题</h2><ul>'+pages.map(p=>'<li><a href="'+p.file+'">'+E(p.title)+'</a></li>').join('')+'</ul><h2>现有分析与工具入口</h2><p>以下保留既有内容与锚点；仍较大的能力/优化/审计页面后续按明确需求分批拆分，避免本批扩成全站重写。</p><ul>'+existing.map(f=>'<li><a href="'+E(f)+'">'+E(f)+'</a></li>').join('')+'</ul><p><a href="framework-print.html">历史框架合订打印版</a>（归档，不作为最新目标）</p>');
 const syncPath='docs/collaboration/DOC_SYNC_STATUS.json';
 if(fs.existsSync(syncPath)){const sync=JSON.parse(fs.readFileSync(syncPath));const progress='<section id="maintenance-goals"><h2>可见维护目标与同步水位</h2><p>'+E(sync.objective)+'</p><p>当前批次：'+E(sync.batchId)+' · '+E(sync.currentMilestoneStatus)+'</p><ul>'+sync.milestones.map(x=>'<li>'+E(x.status)+' · '+E(x.title)+'：'+E(x.evidence)+'</li>').join('')+'</ul><p>后续触发：明确产品SHA/dirty快照或新增证据送达 → 核实影响 → 更新受影响页与索引 → 离线验证 → 更新水位。没有新交付时不轮询。</p><p>同步状态分列：材料已收 / 局部核实 / 文档已同步；产品实现、运行验证及人工验收不因文档完成而升级。</p></section>';const index=dir+'index.html';fs.writeFileSync(index,fs.readFileSync(index,'utf8').replace('<h2>从这里阅读</h2>',progress+'<h2>从这里阅读</h2>'));}
 const manifest={batchId:d.batchId,anchorMap:anchors,frameworkPages:pages.map(({body,...p})=>p),decisionPages:d.decisions.map(x=>({id:x.id,file:'architecture-'+x.id.toLowerCase()+'.html'})),acceptanceCount:24,acceptanceExecuted:0,sourceChecks:checks,sourceDrift:drift,baselinePolicy:'Historical handoff facts retained; layout-only generation does not adopt changed external sources',officialSources:uniqueOfficial.map(s=>({path:s.path,sha256:hash(s.path)})),handoffSha256:hash(handoffPath),checkpointUpgrades:0,productModified:false};
 fs.writeFileSync('artifacts/validation/architecture-doc-site-manifest.json',JSON.stringify(manifest,null,2));
 fs.copyFileSync('artifacts/validation/architecture-doc-site-reader.js',dir+'architecture-doc-site.js');
 require('./build-visual-document-library.cjs')();
 require('./document-library-layout.cjs').pack();
 if(fs.existsSync('artifacts/validation/tool-core-algorithm-review.json'))require('./build-tool-core-algorithm-review.cjs')();
 if(fs.existsSync('docs/collaboration/STARTUP_REVIEW_20261022.json'))require('./build-startup-document.cjs')();
 if(fs.existsSync('docs/collaboration/SOLUTION_MANAGEMENT_REVIEW_20261022.json'))require('./build-solution-document.cjs')();
 if(fs.existsSync('artifacts/validation/startup-implementation-delivery-checks.json'))require('./build-startup-implementation-document.cjs')();
 if(fs.existsSync('artifacts/startup-implementation/start-page-ui-fix-delivery.json'))require('./build-start-ui-fix-document.cjs')();
 if(fs.existsSync('artifacts/startup-implementation/start-page-simplify-delivery.json'))require('./build-start-simplify-document.cjs')();
 if(fs.existsSync('artifacts/startup-implementation/legacy-manager-cleanup-plan.json'))require('./build-start-cleanup-document.cjs')();
 if(fs.existsSync('artifacts/startup-implementation/static-capability-cards-release.log'))require('./build-start-static-document.cjs')();
 if(fs.existsSync('artifacts/startup-implementation/compact-splash-release.log'))require('./build-compact-splash-document.cjs')();
 if(fs.existsSync('artifacts/ui-prototypes/solution-lifecycle/index.html'))require('./build-solution-flow-entry.cjs')();
 if(fs.existsSync('artifacts/startup-implementation/solution-session-delivery.json'))require('./build-solution-session-document.cjs')();
 if(fs.existsSync('artifacts/ui-prototypes/edit-consistency/index.html'))require('./build-edit-consistency-entry.cjs')();
 if(fs.existsSync('artifacts/startup-implementation/unified-editor-save-delivery.json'))require('./build-editor-save-document.cjs')();
 if(fs.existsSync('artifacts/ui-prototypes/save-roundtrip/index.html'))require('./build-save-roundtrip-entry.cjs')();
 if(fs.existsSync('artifacts/ui-prototypes/optimization-roadmap/index.html'))require('./build-optimization-roadmap-entry.cjs')();
 if(fs.existsSync('artifacts/ui-prototypes/save-roundtrip/vm-comparison.json'))require('./build-vm-persist14-document.cjs')();
 if(fs.existsSync('artifacts/ui-prototypes/save-roundtrip/b1-execution.json'))require('./build-b1-execution-entry.cjs')();
 if(fs.existsSync('artifacts/validation/plugin-foundation-01/delivery.json'))require('./build-plugin-foundation-document.cjs')();
 if(fs.existsSync('artifacts/ui-prototypes/architecture-health/startup-preparation.json'))require('./build-startup-consumer-merge.cjs')();
 if(fs.existsSync('docs/collaboration/SCRIPT_PERFORMANCE_BATCH_20261023.json'))require('./build-script-performance-document.cjs')();
 if(fs.existsSync('artifacts/performance/script-baseline-20261023/component-baseline.json'))require('./build-script-component-document.cjs')();
  require('./build-script-final-decision.cjs')();
  require('./build-script-three-way.cjs')();
  if(fs.existsSync('docs/collaboration/SCRIPT_S2_DESIGN_20261023.json'))require('./build-script-s2-design.cjs')();
  if(fs.existsSync('docs/collaboration/SCRIPT_S3_DESIGN_20261023.json'))require('./build-script-s3-design.cjs')();
  if(fs.existsSync('docs/collaboration/SCRIPT_POST_S3_REVIEW_20261023.json'))require('./build-script-post-s3-panorama.cjs')();
 require('./build-document-search-index.cjs')();
 require('./sync-document-search-home.cjs')();
  require('./normalize-document-home.cjs')();
 require('./normalize-document-hierarchy.cjs')();
 return {frameworkPages:pages.length,decisions:12,acceptance:24,official:uniqueOfficial.length,anchors:Object.keys(anchors).length};
};
if(require.main===module)console.log(JSON.stringify(module.exports()));
