const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {pathToFileURL,fileURLToPath}=require('url');
const {chromium}=require('C:/Users/houzhongjie/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const layout=JSON.parse(fs.readFileSync('artifacts/validation/document-library-layout.json'));
const root=path.resolve(layout.root),snapshot=layout.sourceSnapshotDirectory;
const pages=['guide-dataflow.html','guide-execution.html'];
for(const page of pages){
 const file=path.join(root,layout.files[page]),html=fs.readFileSync(file,'utf8'),flat=fs.readFileSync(path.join(snapshot,page),'utf8');
 assert.equal((html.match(/<nav\b/g)||[]).length,1,page+' should have one focused navigation');
 assert(!html.includes('关系图解 · 数据流与结果发布 · 关系图解')&&!html.includes('关系图解 · 执行架构 · 关系图解'),page+' repeats the topic label in its return bar');
 assert(html.includes('relationship-guide .evidence-columns>section:nth-child(1)')&&html.includes('relationship-guide .evidence-columns>section:nth-child(2)')&&html.includes('relationship-guide .evidence-columns>section:nth-child(3)'),page+' should visually distinguish verified source, current implementation, and unknowns');
 assert(!html.includes('page-journey'),page+' repeats the full reading route');
 assert(!html.includes('document-breadcrumb'),page+' repeats breadcrumb navigation');
 for(const label of ['原版已证事实','自研当前实现','仍未知 / 未验证'])assert(html.includes(label),page+' missing evidence category '+label);
 const graph=html.indexOf('data-visual="'+(page==='guide-dataflow.html'?'data-current':'current')+'"'),analysis=html.indexOf('class="evidence-columns"');
 assert(graph>=0&&analysis>graph,page+' should show the current relationship graph before its analysis');
 assert.equal(html,flat,page+' output and canonical snapshot differ');
 for(const m of html.matchAll(/href="([^"]+)"/g)){const href=m[1];if(/^(?:file:|https?:|mailto:|#)/.test(href))continue;const u=new URL(href,pathToFileURL(file));const target=fileURLToPath(new URL(u.pathname,u));assert(fs.existsSync(target),page+' missing local link '+href);}
}
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'}),errors=[],remote=[];
 try{const p=await browser.newPage({viewport:{width:1366,height:900}});p.on('pageerror',e=>errors.push(e.message));p.on('request',r=>{if(/^https?:/.test(r.url()))remote.push(r.url())});
  for(const page of pages){const file=path.join(root,layout.files[page]),url=pathToFileURL(file).href;
   for(const width of [390,768,1366]){await p.setViewportSize({width,height:900});await p.goto(url);assert(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),page+' horizontal overflow at '+width);assert.equal(await p.locator('nav').count(),1);assert(await p.locator('nav.topic-return span').textContent()=== (page==='guide-dataflow.html'?'数据流与结果发布':'执行架构'),page+' return bar should identify this topic once');assert(await p.locator('[data-visual="'+(page==='guide-dataflow.html'?'data-current':'current')+'"]').isVisible());assert(await p.getByRole('heading',{name:'原版已证事实'}).isVisible());const evidenceColors=await p.locator('.evidence-columns>section h3').evaluateAll(es=>es.map(e=>getComputedStyle(e).color));assert.equal(new Set(evidenceColors).size,3,page+' evidence categories should have distinct visual colors');}
   await p.setViewportSize({width:1366,height:900});await p.goto(url);await p.screenshot({path:'artifacts/validation/'+page.replace('.html','-review.png'),fullPage:true});
  }
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);
  const out={pages,widths:[390,768,1366],singleNav:true,currentGraphFirst:true,visuallyDistinguishedEvidence:true,evidenceCategories:['原版已证事实','自研当前实现','仍未知 / 未验证'],horizontalOverflow:false,pageErrors:errors,remoteRequests:remote,productModified:false};
  fs.writeFileSync('artifacts/validation/relationship-guide-focus-validation.json',JSON.stringify(out,null,2));console.log(JSON.stringify(out));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
