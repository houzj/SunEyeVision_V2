const fs=require('fs'),path=require('path');
const dir='docs/04-工具设计/架构对照/2026/10/21/';
const layout=JSON.parse(fs.readFileSync('artifacts/validation/document-library-layout.json'));
const include=['现状分析/','能力对照/','优化方案/','目标架构/','关系图解/','框架设计/','SDK设计/'];
const skip=new Set(['capability-index.html','optimization-index.html','comparison.html','optimization.html','recommendation-audit.html','framework-design-comparison.html']);
const decode=s=>String(s||'').replace(/&nbsp;|&#160;/gi,' ').replace(/&amp;/gi,'&').replace(/&lt;/gi,'<').replace(/&gt;/gi,'>').replace(/&quot;/gi,'"').replace(/&#39;|&apos;/gi,"'").replace(/&#(\d+);/g,(_,n)=>String.fromCodePoint(Number(n))).replace(/&#x([\da-f]+);/gi,(_,n)=>String.fromCodePoint(parseInt(n,16)));
const plain=s=>decode(String(s||'').replace(/<br\s*\/?\s*>/gi,' ').replace(/<[^>]*>/g,' ')).replace(/[\u200b\u00ad]/g,'').replace(/\s+/g,' ').trim();
function sectionIdAt(html,position){
 const tags=/<(section|article)\b([^>]*)>|<\/(section|article)\s*>/gi,stack=[];let m;
 while((m=tags.exec(html))&&m.index<position){if(m[3]){for(let i=stack.length-1;i>=0;i--)if(stack[i].tag===m[3]){stack.length=i;break;}}else{const id=m[2].match(/\bid="([^"]+)"/);stack.push({tag:m[1],id:id?.[1]||''});}}
 for(let i=stack.length-1;i>=0;i--)if(stack[i].id)return stack[i].id;
 return '';
}
function readPage(flat,relative){
 const canonical=path.join(layout.root,relative);
 if(fs.existsSync(canonical)){const html=fs.readFileSync(canonical,'utf8');if(!/data-(?:managed-library|library)-router/.test(html))return {html,source:'canonical'};}
 const legacy=dir+flat;
 if(fs.existsSync(legacy)){const html=fs.readFileSync(legacy,'utf8');if(!/data-(?:managed-library|library)-router/.test(html))return {html,source:'legacy-flat-fallback'};}
 return {html:'',source:'missing'};
}
function topicFor(relative){return relative.split('/').slice(0,-1).join(' / ').replace(/-/g,' ');}
function recordsFor(flat,relative){
 if(skip.has(flat)||!include.some(folder=>relative.startsWith(folder))||!relative.endsWith('.html'))return [];
 const page=readPage(flat,relative),html=page.html;if(!html)return [];
 const main=html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1]||html.replace(/<head\b[^>]*>[\s\S]*?<\/head>/i,'');
 const title=plain(main.match(/<h1\b[^>]*>([\s\S]*?)<\/h1>/i)?.[1]||html.match(/<title>([\s\S]*?)<\/title>/i)?.[1]||flat.replace(/\.html$/,''));
 let visible=main.replace(/<(script|style|nav|header|footer|details|svg|pre|table)\b[^>]*>[\s\S]*?<\/\1>/gi,' ');
 const headings=[...visible.matchAll(/<h([23])\b([^>]*)>([\s\S]*?)<\/h\1>/gi)];
 const rows=[];
 for(let i=0;i<headings.length;i++){
  const h=headings[i],heading=plain(h[3]);if(!heading)continue;
  const start=h.index+h[0].length,end=i+1<headings.length?headings[i+1].index:visible.length;
  const section=visible.slice(start,end),text=plain(section).replace(/^[·•→\s]+/,'');
  if(text.length<24)continue;
  const ownId=h[2].match(/\bid="([^"]+)"/)?.[1],anchor=ownId||sectionIdAt(visible,h.index);
  const focusAt=[...text.matchAll(/首次补全|程序集引用管理|引用清单|引用范围|最终推荐|自动补全/g)].map(x=>x.index).find(x=>x>220);
  const begin=focusAt===undefined?0:Math.max(0,focusAt-150),excerpt=(begin?'…':'')+text.slice(begin,begin+260)+(begin+260<text.length?'…':'');
  rows.push({heading,excerpt,anchor,weight:(heading.length>2?2:0)+Math.min(text.length,260)/260+( /算法|引用|补全|编译|执行|数据|参数|结果|运行|缓存|输入|输出|资源|取舍|未知/.test(heading)?2:0)});
 }
 rows.sort((a,b)=>b.weight-a.weight);
 const forceHeadings=/^(?:tool-script|optimization-o01)\.html$/.test(flat)?[/VM → 我们 → 目标/,/基础预热与窗口打开/,/自动补全的触发与任务/,/编译触发、缓存与失败/,/程序集引用管理/,/引用清单的事实源/,/引用更新与缓存失效/,/最终推荐/,/用户实际VM观察/]:[];
 const required=rows.filter(x=>x.anchor==='script-reference-three-way'||x.anchor==='script-final-decision'||x.anchor==='script-performance-chains'||forceHeadings.some(re=>re.test(x.heading)));
 const selected=new Set(required);
 const chosen=[...required,...rows.filter(x=>!selected.has(x)).slice(0,flat==='tool-script.html'?10:8)].slice(0,flat==='tool-script.html'?18:14);
 if(!chosen.length){const excerpt=plain(visible).slice(0,240);if(excerpt.length>24)chosen.push({heading:title,excerpt,anchor:'',weight:1});}
 const pagePath=layout.files[flat]||relative;
 return chosen.map(x=>({title,topic:topicFor(relative),pageTitle:title,path:pagePath,heading:x.heading,excerpt:x.excerpt,href:pagePath+(x.anchor?'#'+encodeURIComponent(x.anchor):''),source:page.source}));
}
module.exports=function(){
 const records=[];
 for(const [flat,relative] of Object.entries(layout.files))records.push(...recordsFor(flat,relative));
 const payload='window.DOCUMENT_SEARCH_INDEX='+JSON.stringify(records).replaceAll('<','\\u003c')+';\n';
 const indexFile=dir+'document-search-index.js',runtimeFile=dir+'document-search.js',rootIndex=path.join(layout.root,'公共资源/document-search-index.js'),rootRuntime=path.join(layout.root,'公共资源/document-search.js'),snapshotIndex=layout.sourceSnapshotDirectory+'document-search-index.js',snapshotRuntime=layout.sourceSnapshotDirectory+'document-search.js';
 const runtime=fs.readFileSync('artifacts/validation/document-search-runtime.js');
 const packed=fs.existsSync(dir+'index.html')&&fs.readFileSync(dir+'index.html','utf8').includes('data-managed-library-router');
 for(const file of [...(packed?[]:[indexFile]),rootIndex,snapshotIndex]){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,payload);}
 for(const file of [...(packed?[]:[runtimeFile]),rootRuntime,snapshotRuntime]){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,runtime);}
 const manifest={version:1,records:records.length,pages:new Set(records.map(x=>x.path)).size,bytes:Buffer.byteLength(payload),topics:[...new Set(records.map(x=>x.topic))],source:'Canonical current topic pages; short visible headings/excerpts only. Scripts, styles, navigation, details, tables, history, audits and snapshots excluded.',canonicalPages:records.filter(x=>x.source==='canonical').length,legacyFallbacks:[...new Set(records.filter(x=>x.source!=='canonical').map(x=>x.path))],remoteFetch:false,thirdParty:false};
 fs.writeFileSync('artifacts/validation/document-search-index-manifest.json',JSON.stringify(manifest,null,2)+'\n');
 return {records:records.length,pages:new Set(records.map(x=>x.path)).size,bytes:Buffer.byteLength(payload)};
};
if(require.main===module)console.log(JSON.stringify(module.exports()));
