const fs=require('fs'),path=require('path'),assert=require('assert/strict'),vm=require('vm');
const {pathToFileURL,fileURLToPath}=require('url');
const {chromium}=require('C:/Users/houzhongjie/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const layout=JSON.parse(fs.readFileSync('artifacts/validation/document-library-layout.json'));
const root=path.resolve(layout.root),home=path.join(root,layout.files['index.html']);
const index=path.join(root,layout.files['document-search-index.js']),runtime=path.join(root,layout.files['document-search.js']);
const manifest=JSON.parse(fs.readFileSync('artifacts/validation/document-search-index-manifest.json'));
const context={window:{}};vm.runInNewContext(fs.readFileSync(index,'utf8'),context);
const records=context.window.DOCUMENT_SEARCH_INDEX;
assert.equal(records.length,manifest.records);assert.equal(manifest.pages,134);assert(manifest.bytes<600000);assert.equal(manifest.legacyFallbacks.length,0);
for(const item of records)assert.equal(item.source,'canonical');
assert(records.some(x=>x.path.endsWith('tool-script.html')&&x.href.endsWith('#script-reference-three-way')));
assert(records.some(x=>x.path.endsWith('tool-script.html')&&x.href.endsWith('#script-final-decision')));
assert(fs.readFileSync(layout.sourceSnapshotDirectory+'index.html','utf8')===fs.readFileSync(home,'utf8'));
assert(fs.readFileSync(layout.sourceSnapshotDirectory+'document-search-index.js','utf8')===fs.readFileSync(index,'utf8'));
assert(fs.readFileSync(layout.sourceSnapshotDirectory+'document-search.js','utf8')===fs.readFileSync(runtime,'utf8'));
const generator=fs.readFileSync('artifacts/validation/build-visual-document-library.cjs','utf8');
const orchestrator=fs.readFileSync('artifacts/validation/build-architecture-doc-site.cjs','utf8');
const packaging=fs.readFileSync('artifacts/validation/document-library-layout.cjs','utf8');
assert(generator.includes('document-search-index.js')&&generator.includes('document-search.js'));
assert(orchestrator.includes("build-document-search-index.cjs")&&orchestrator.includes("sync-document-search-home.cjs"));
assert(packaging.includes("'document-search-index.js','document-search.js'"));
assert(!fs.readFileSync('artifacts/validation/document-search-runtime.js','utf8').includes('fetch('));
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'}),errors=[],remote=[];
 try{const page=await browser.newPage({viewport:{width:1366,height:900}});page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))remote.push(r.url())});
  await page.goto(pathToFileURL(home).href);
  for(const width of [390,768,1366]){await page.setViewportSize({width,height:900});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'home overflow at '+width);}
  const input=page.locator('#document-search-input'),results=page.locator('#document-search-results a');
  await input.fill('引用管理');let links=await results.evaluateAll(es=>es.map(a=>({text:a.textContent,href:a.href})));assert(links.some(x=>x.href.includes('tool-script.html#script-reference-three-way')));assert(links.some(x=>x.href.includes('optimization-o01.html#script-reference-three-way')));
  await input.fill('最终推荐');links=await results.evaluateAll(es=>es.map(a=>a.href));assert(links.some(h=>h.includes('tool-script.html#script-final-decision')));
  await input.fill('首次补全');const visibleText=await page.locator('#document-search-results').innerText();assert(visibleText.includes('首次补全'));links=await results.evaluateAll(es=>es.map(a=>a.href));assert(links.some(h=>h.includes('tool-script.html')));
   await input.fill('脚本第二批');links=await results.evaluateAll(es=>es.map(a=>a.href));assert(links.some(h=>h.includes('tool-script.html#script-s2-design'))&&links.some(h=>h.includes('optimization-o01.html#script-s2-design')));
  for(const q of ['脚本后续优化','前三批之外','脚本数据合同','脚本方案依赖']){await input.fill(q);links=await results.evaluateAll(es=>es.map(a=>a.href));assert(links.some(h=>h.includes('tool-script.html#script-followup-panorama'))&&links.some(h=>h.includes('optimization-o01.html#script-followup-panorama')),'follow-up search missed a canonical page for '+q);}
   await input.fill('动态输入输出');const dynamicText=await page.locator('#document-search-results').innerText();assert(dynamicText.includes('动态输入输出'));links=await results.evaluateAll(es=>es.map(a=>a.href));assert(links.some(h=>h.includes('#script-s2-design')));
  for(const q of ['脚本第三批','停止','资源释放']){await input.fill(q);links=await results.evaluateAll(es=>es.map(a=>a.href));const tool=links.some(h=>h.includes('tool-script.html#script-s3-design')),o01=links.some(h=>h.includes('optimization-o01.html#script-s3-design'));if(q==='停止')assert(tool||o01,'S3 search missing broad stop entry');else assert(tool&&o01,'S3 exact search missing one of two entries '+q);}
  await input.fill('脚本 编译');const multi=await page.locator('#document-search-results').innerText();assert(multi.includes('脚本')&&multi.includes('编译'));
  await input.fill('不存在的测试词987654');assert.match(await page.locator('#document-search-status').innerText(),/没有找到/);assert.equal(await results.count(),0);
  await page.keyboard.press('Control+k');assert.equal(await page.evaluate(()=>document.activeElement.id),'document-search-input');await input.fill('脚本');await input.press('ArrowDown');assert.equal(await page.evaluate(()=>document.activeElement.tagName),'A');await page.locator('#document-search-clear').click();assert.equal(await input.inputValue(),'');assert.equal(await results.count(),0);
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);
   const output={records:manifest.records,pages:manifest.pages,indexBytes:manifest.bytes,queries:['引用管理','最终推荐','首次补全','脚本第二批','动态输入输出','脚本第三批','停止','资源释放','脚本 编译','无结果'],keyboard:true,clear:true,widths:[390,768,1366],horizontalOverflow:false,remoteRequests:remote,pageErrors:errors,canonicalSourceOnly:true,offline:true,thirdParty:false};
  fs.writeFileSync('artifacts/validation/document-search-validation.json',JSON.stringify(output,null,2)+'\n');console.log(JSON.stringify(output));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
