const { chromium } = require('C:/Users/houzhongjie/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');
(async () => {
 const file=path.resolve('docs/04-工具设计/标定工具/2026/10/17/halcon-9plus3-parameter-guide.html');
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 const errors=[];page.on('pageerror',error=>errors.push(error.message));
 await page.goto(require('url').pathToFileURL(file).href);
 assert.equal(await page.locator('.parameter-section').count(),7);
 const total=await page.locator('.parameter-section tbody tr').count();
 assert.ok(total>=50);
 await page.locator('#search').fill('RepeatToleranceMm');
 assert.equal(await page.locator('.parameter-section tbody tr:visible').count(),1);
 await page.locator('#reset').click();
 await page.locator('#level').selectOption({label:'内部保护'});
 assert.equal(await page.locator('.parameter-section tbody tr:visible').count(),5);
 await page.locator('#reset').click();
 assert.equal(await page.locator('.parameter-section tbody tr:visible').count(),total);
 await page.locator('#search').fill('不存在的测试参数xyz');
 assert.ok(await page.locator('#empty').isVisible());
 await page.locator('#reset').click();
 const links=await page.locator('a[href]').evaluateAll(elements=>elements.map(e=>e.getAttribute('href')));
 for(const link of links){
  if(link.startsWith('#'))assert.equal(await page.locator(link).count(),1);
  else assert.ok(fs.existsSync(path.resolve(path.dirname(file),link)),link);
 }
 await page.screenshot({path:'artifacts/validation/calibration-guide-desktop.png',fullPage:false});
 await page.locator('#nt-runtime').scrollIntoViewIfNeeded();
 await page.screenshot({path:'artifacts/validation/calibration-guide-parameters.png'});
 await page.setViewportSize({width:390,height:844});
 await page.evaluate(()=>window.scrollTo(0,0));
 assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));
 await page.screenshot({path:'artifacts/validation/calibration-guide-mobile.png'});
 assert.deepEqual(errors,[]);
 console.log(JSON.stringify({rows:total,sections:7,links:links.length,search:true,filter:true,reset:true,mobileOverflow:false,pageErrors:errors}));
 await browser.close();
})().catch(error=>{console.error(error);process.exit(1);});
