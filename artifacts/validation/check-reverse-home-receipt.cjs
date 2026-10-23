const fs = require('fs');
const path = require('path');
const {pathToFileURL, fileURLToPath} = require('url');
const {chromium} = require('C:/Users/houzhongjie/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async () => {
  const target = 'D:/MyWork/REA逆向测试/docs/架构逆向与实现对照文档/index.html';
  const browser = await chromium.launch({channel:'msedge', headless:true});
  const page = await browser.newPage();
  const errors = [], layouts = [], links = [];
  page.on('pageerror', e => errors.push(e.message));
  for (const width of [1366, 1440, 390]) {
    await page.setViewportSize({width, height:768});
    await page.goto(pathToFileURL(target).href);
    layouts.push({width, overflow:await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)});
    if (width === 1366) await page.screenshot({path:'artifacts/validation/reverse-home-reviewed.png'});
  }
  for (const href of [...new Set(await page.locator('a[href]').evaluateAll(xs => xs.map(x => x.href)))]) {
    const u = new URL(href);
    if (u.protocol !== 'file:') continue;
    const file = fileURLToPath(u);
    const exists = fs.existsSync(file);
    const anchor = !u.hash || exists && new RegExp('id=["\x27]' + decodeURIComponent(u.hash.slice(1)).replace(/[.*+?^${}()|[\]\\]/g,'\\$&') + '["\x27]').test(fs.readFileSync(file,'utf8'));
    links.push({href, exists, anchor});
  }
  await page.goto(pathToFileURL(target).href+'#main');
  const main = await page.locator('#main').count();
  const navigation = await page.locator('nav').evaluateAll(xs => xs.map(x => ({label:x.getAttribute('aria-label'), text:x.innerText})));
  await browser.close();
  const report = {target, layouts, links, mainAnchor:main===1, navigation, errors, productModified:false, scope:'主控只读文档验收，非产品或性能测试'};
  report.passed = main===1 && !errors.length && layouts.every(x=>!x.overflow) && links.every(x=>x.exists && x.anchor);
  fs.writeFileSync('artifacts/validation/reverse-home-receipt.json', JSON.stringify(report,null,2));
  console.log(JSON.stringify({passed:report.passed,layouts,links:links.length,errors}));
})();
