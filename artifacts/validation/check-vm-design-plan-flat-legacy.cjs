const {chromium}=require('C:/Users/houzhongjie/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('fs'),path=require('path'),url=require('url'),assert=require('assert/strict');
(async()=>{
 const file=path.resolve('docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html');
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
  const errors=[],requests=[];page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>requests.push(r.url()));
  await page.goto(url.pathToFileURL(file).href);
  assert.equal(await page.locator('section[data-section]').count(),29);
  assert.equal(await page.locator('section[data-section]').first().getAttribute('id'),'vm-architecture');
  assert.deepEqual(await page.locator('main > section').evaluateAll(es=>es.map(e=>e.id).filter(x=>['vm-architecture','suneye-architecture','system-comparison','architecture-summary'].includes(x))),['vm-architecture','suneye-architecture','system-comparison','architecture-summary']);
  assert.equal(await page.locator('.architecture-domain').count(),18);
  assert.equal(await page.locator('#system-comparison tbody tr').count(),9);
  assert.equal(await page.locator('.file-inventory').count(),2);
  assert.equal(await page.locator('.ui-review').count(),13);
  assert.equal(await page.locator('.chain-audit').count(),11);
  assert.equal(await page.locator('.vm-deep-chain').count(),3);
  assert.equal(await page.locator('.reverse-pending').count(),7);
  assert.ok(await page.locator('.vm-proof').count()>=20);
  assert.ok((await page.locator('#vm-evidence-progress').textContent()).includes('managed-declaration-only'));

  assert.equal(await page.locator('.scenario-note').count(),10);
  const ids=await page.locator('[id]').evaluateAll(es=>es.map(e=>e.id));assert.equal(ids.length,new Set(ids).size,'duplicate IDs');
  assert.ok(await page.locator('.chain-evidence').count()>=30);
  for(const item of await page.locator('.chain-audit').all()){assert.ok((await item.textContent()).includes('验证门槛'));assert.ok((await item.textContent()).includes('选择后的成本'));}

  assert.equal(await page.locator('#coverage-matrix tbody tr').count(),16);
  assert.equal(await page.locator('#coverage-matrix tr[data-coverage-status="partial"]').count(),10);
  assert.equal(await page.locator('#coverage-matrix tr[data-coverage-status="pending"]').count(),6);
  await page.locator('[data-coverage-filter="pending"]').click();
  assert.equal(await page.locator('#coverage-matrix tbody tr:visible').count(),6);
  await page.locator('[data-coverage-filter="partial"]').click();
  assert.equal(await page.locator('#coverage-matrix tbody tr:visible').count(),10);
  await page.locator('[data-coverage-filter="all"]').click();
  assert.equal(await page.locator('.compare-card').count(),10);
  assert.equal(await page.locator('.decision-review').count(),10);
  assert.equal(await page.locator('.decision-review[open]').count(),1);
  for(const review of await page.locator('.decision-review').all()){
   assert.equal(await review.locator('.tradeoff-side').count(),2);
   assert.ok(await review.locator('h4').count()>=7);
   assert.ok((await review.locator('.recommendation').textContent()).length>60);
   assert.ok((await review.locator('.limits').textContent()).length>40);
  }
  assert.equal(await page.locator('#plugin-backend-tradeoffs').evaluate(e=>e.open),true);
  assert.equal(await page.locator('#implementation-details').getAttribute('open'),null);
  assert.equal(await page.locator('section[data-section]:visible').count(),13);
  assert.equal(await page.locator('#dialogs details').count(),10);
  assert.equal(await page.locator('#tabs article').count(),6);
  const links=await page.locator('a[href]').evaluateAll(es=>es.map(e=>e.getAttribute('href')));
  for(const link of links){
   if(link.startsWith('#'))assert.equal(await page.locator(link).count(),1,link);
   else if(link.startsWith('file:'))assert.ok(fs.existsSync(url.fileURLToPath(link)),link);
   else assert.ok(fs.existsSync(path.resolve(path.dirname(file),link)),link);
  }
  await page.locator('#search').fill('ParamBinding');
  assert.ok(await page.locator('section[data-section]:visible').count()<29);
  assert.equal(await page.locator('#implementation-details').evaluate(e=>e.open),true);
  await page.locator('nav a[href="#theme"]').click();
  assert.equal(await page.locator('#search').inputValue(),'');
  assert.equal(await page.locator('section[data-section]:visible').count(),29);
  await page.locator('#search').fill('zzzz-no-results-xyz');assert.ok(await page.locator('#empty').isVisible());
  await page.locator('#reset').click();
  await page.evaluate(()=>window.scrollTo(0,0));
  await page.screenshot({path:'artifacts/validation/vm-design-plan-light.png'});
  await page.locator('#toggle-theme').click();
  assert.equal(await page.locator('html').getAttribute('data-theme'),'dark');
  assert.equal(await page.locator('#toggle-theme').getAttribute('aria-pressed'),'true');
  await page.evaluate(()=>window.scrollTo(0,0));await page.screenshot({path:'artifacts/validation/vm-design-plan-dark.png'});
  await page.locator('nav a[href="#devices"]').click();await page.screenshot({path:'artifacts/validation/vm-design-plan-devices.png'});
  await page.locator('#vm-image-release-evidence').evaluate(e=>e.scrollIntoView({block:'start'}));await page.screenshot({path:'artifacts/validation/vm-design-plan-vm-release-evidence.png'});
  await page.locator('#image-source-chain').evaluate(e=>e.scrollIntoView({block:'start'}));await page.screenshot({path:'artifacts/validation/vm-design-plan-chain-audit.png'});
  await page.locator('nav a[href="#contracts"]').click();await page.screenshot({path:'artifacts/validation/vm-design-plan-sdk.png'});
  await page.locator('nav a[href="#plugin-backend"]').click();
  await page.locator('#plugin-backend-tradeoffs').evaluate(e=>e.scrollIntoView({block:'start'}));
  await page.screenshot({path:'artifacts/validation/vm-design-plan-execution-tradeoffs.png'});
  await page.locator('#plugin-backend-tradeoffs .recommendation').evaluate(e=>e.scrollIntoView({block:'start'}));
  await page.screenshot({path:'artifacts/validation/vm-design-plan-execution-reason.png'});
  await page.evaluate(()=>window.dispatchEvent(new Event('beforeprint')));
  assert.equal(await page.locator('#implementation-details').evaluate(e=>e.open),true);
  assert.equal(await page.locator('.decision-review[open]').count(),10);
  await page.evaluate(()=>window.dispatchEvent(new Event('afterprint')));
  assert.equal(await page.locator('#implementation-details').evaluate(e=>e.open),false);
  assert.equal(await page.locator('.decision-review[open]').count(),1);
  await page.locator('#toggle-theme').click();
  await page.setViewportSize({width:390,height:844});await page.evaluate(()=>window.scrollTo(0,0));
  await page.screenshot({path:'artifacts/validation/vm-design-plan-mobile.png'});
  const overflow=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,items:Array.from(document.querySelectorAll('body *')).filter(e=>!e.closest('.table')&&e.getBoundingClientRect().right>innerWidth+1).slice(0,12).map(e=>({tag:e.tagName,id:e.id,cls:e.className,right:e.getBoundingClientRect().right,text:e.textContent.slice(0,80)}))}));
  if(overflow.scroll>overflow.width)console.log(JSON.stringify(overflow));
  assert.ok(overflow.scroll<=overflow.width);
  await page.screenshot({path:'artifacts/validation/vm-design-plan-mobile.png'});
  assert.ok(requests.every(r=>r.startsWith('file:')),JSON.stringify(requests));
  assert.deepEqual(errors,[]);
  console.log(JSON.stringify({sections:29,coverage:{first:false,areas:16,partial:10,pending:6,filter:true},architectureCards:10,tradeoffReviews:10,executionReviewOpen:true,tabs:6,dialogs:10,links:links.length,search:true,navigationReset:true,appendix:true,printState:true,theme:true,mobileOverflow:false,pageErrors:errors,networkRequests:0}));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
