const fs=require('fs'),path=require('path');
const dir='docs/04-工具设计/架构对照/2026/10/21/';
const layout=JSON.parse(fs.readFileSync('artifacts/validation/document-library-layout.json'));
const include=['现状分析/','能力对照/','优化方案/','目标架构/','关系图解/','框架设计/','SDK设计/'];
const skip=new Set(['capability-index.html','optimization-index.html','comparison.html','optimization.html','recommendation-audit.html','framework-design-comparison.html']);
function writeGenerated(file,value){
 fs.mkdirSync(path.dirname(file),{recursive:true});
 const temporary=file+'.tmp-'+process.pid;
 try{fs.writeFileSync(temporary,value);fs.renameSync(temporary,file);}
 finally{if(fs.existsSync(temporary))fs.unlinkSync(temporary);}
}
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
 if(flat==='index.html'){const page=readPage(flat,relative);if(!page.html.includes('id="delivery-home-overview"'))return [];return [{title:'当前交付总览',topic:'交付进度',pageTitle:'当前交付总览',path:relative,heading:'当前交付总览 · 总进度与完成度',excerpt:'DELIVERY_LEDGER唯一原ID总账：原48项/纳入47/排除1；已验收、待验收两个子态、实施中、未开始、阻塞与待核；五方向、责任时间、关键路径与Release阶段。不以子测试算整项完成率。',href:'交付总览.html#delivery-home-overview',source:page.source}];}
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
 const pinnedAnchors=['script-s3-design','script-s2-design','script-followup-panorama','script-prototype-final-review'];
 const pinned=/^(?:tool-script|optimization-o01)\.html$/.test(flat)?pinnedAnchors.map(anchor=>rows.find(x=>x.anchor===anchor)).filter(Boolean):flat==='optimization-o06.html'?['implementation-live-status','implementation-eta-snapshot','final-unified-manual-validation','m03-rectangle-projection','a04-transfer-cost-review','implementation-tree-projections','tree-consumer-seams','five-directions-overview'].map(anchor=>rows.find(x=>x.anchor===anchor)).filter(Boolean):[];
 for(const row of pinned){const labels={'script-s3-design':'脚本第三批','script-s2-design':'脚本第二批','script-followup-panorama':'脚本后续优化','script-prototype-final-review':'原型最终采用审查','implementation-live-status':'最新实施验收状态','m03-rectangle-projection':'M03显示点子能力','a04-transfer-cost-review':'A04正式成本','implementation-tree-projections':'树层级对照','tree-consumer-seams':'四接缝实际接入','five-directions-overview':'五方向总览'},label=labels[row.anchor];if(label&&!row.heading.includes(label))row.heading=label+' · '+row.heading;}
 const required=[...pinned,...rows.filter(x=>!pinned.includes(x)&&(x.anchor==='script-reference-three-way'||x.anchor==='script-final-decision'||x.anchor==='script-performance-chains'||forceHeadings.some(re=>re.test(x.heading))))];
 const selected=new Set(required);
 const chosen=[...required,...rows.filter(x=>!selected.has(x)).slice(0,flat==='tool-script.html'?10:8)].slice(0,flat==='tool-script.html'?18:14);
 if(!chosen.length){const excerpt=plain(visible).slice(0,240);if(excerpt.length>24)chosen.push({heading:title,excerpt,anchor:'',weight:1});}
 const pagePath=layout.files[flat]||relative;
 const aliases=/^(?:tool-script|optimization-o01)\.html$/.test(flat)?[
  {heading:'原型最终采用审查',anchor:'script-prototype-final-review',excerpt:'四项已列冻结范围有证据；当前整图JSON传输不采用生产默认，产品S3程序集释放未闭环，原10cold×10未验边界保留。'},
  {heading:'整图JSON不采用',anchor:'script-prototype-final-review',excerpt:'版本切换、失败候选、旧worker退出与A04正式成本合并审查；不同原型身份分别保留，shared buffer未测未授权。'},
  {heading:'前三批之外',excerpt:'脚本后续优化全景：按S1、S2、S3已覆盖边界，梳理前三批之外的11个静态候选问题。'},
  {heading:'脚本数据合同',excerpt:'候选A组：全局地址、数组形状、绑定事实源、结果命名空间、执行结果、0与false。静态分析，条件风险与未知项分列。'},
  {heading:'脚本方案依赖',excerpt:'候选B组：同名DLL替换、方案引用搬家、私有依赖清单；干净目录和离线副本验证仍待实施。'}
 ].map(x=>({...x,anchor:x.anchor||'script-followup-panorama'})):flat==='optimization-o06.html'?[
    // O06-BACKEND-REVIEW-SEARCH-ALIASES
  {"heading":"统一合同：图像、ROI与结果语义","anchor":"br-backend-contract-decisions","excerpt":"算法库迁移复审：本轮固定 HALCON，公共输入图像、ROI区域集合、算法请求、结果、显示持久化和所有权统一业务语义；引擎内部实现可以不同。格式、位深、通道顺序、步长和坐标身份明确，不承诺全格式零拷贝。"},
  {"heading":"工程与消费者调整：HALCON / OpenCV 迁移边界","anchor":"br-backend-project-changes","excerpt":"列明 Contracts / Runtime / Plugin.Wpf、Hal/Cv 适配、图像资源桥、公共结果及消费者的工程影响；现工程存在不等于默认复用。本轮固定 HALCON，未来 OpenCV 实现及迁移方案仍待完整闭环裁决。"},
  {"heading":"后端方案比较与最终取舍","anchor":"br-backend-options","excerpt":"后端迁移与适配桥三方案按需求、双方证据、语义一致性、所有权与成本及工程影响比较；复用、调整或删除重做由完整闭环独立裁决，不以现实现或 VM 作为默认答案。本轮固定 HALCON，未来 OpenCV 实现尚未批准。"},
  {"heading":"VM Blob 决策证据与统一闭环范围","anchor":"br-vm-decision-evidence","excerpt":"原 SDK 与模块 XML、头文件、样例和代码生成映射共同约束 Blob 合同：4/8 邻域、可选输出、多段轮廓、Clear 后不可续读、FindNum 两载体差异及 ROI 优化一致性条件。B1–B7 归入同一闭环；原 ProcessAlg 未完整恢复，不据旁置 OpenCvSharp.Blob.dll 归因算法后端。"},
{heading:'旋转矩形投影',anchor:'m03-rectangle-projection',excerpt:'849d源/baseline97f旋转矩形数据到四点显示轮廓的有限子能力已独验，成熟11回放＋自有10组180断言；不关闭整个M03、CLR窄化、发布/GUI或七交互。'},
  {heading:'M03显示点独验',anchor:'m03-rectangle-projection',excerpt:'真实UI internal Project：整宽一次/2、正弧度Y向下、Frame/非别名/overflow核对通过；108托管新包身份单列，不混主树Guid。'},
  {heading:'结果声明消费链',anchor:'tree-consumer-seams',excerpt:'GetPropertyMap静态声明→GetOutputVariables→OutputVariable→当前UI树投影；已设计、实际接入、独验和未确认分列，不把类型来源说成仅由末次运行值推断。'},
  {heading:'动态声明回调',anchor:'tree-consumer-seams',excerpt:'旧源码动态派发缺口已有有限guard；旧固定16PASS1FAIL确认空合同留旧树，新0641成熟1＋自有3正式有限修复通过，仅关闭该空合同缺陷。已有完成lease/current/selected保护保留，不代完整公共树。'},
  {heading:'图像资源桥',anchor:'tree-consumer-seams',excerpt:'现IVisionImage与新IReadOnlyImage/资源种类接入边界尚未封口；保留HALCON/CV转换限制，不新增Region或管理器，不将声明种类当已实现。'},
  {heading:'绑定树层级对照',anchor:'implementation-tree-projections',excerpt:'VM、当前、目标层级并排；区分业务必需层、类型包装层和控件投影，绑定目录与结果仓库分责。无实测点击数承诺。'},
  {heading:'结果树层级',anchor:'implementation-tree-projections',excerpt:'当前ModuleResultNode投影为FlatResultRows/DataGrid并保留缩进与展开；目标与绑定目录树共享Schema，各自消费。'},
  {heading:'图像传输成本',anchor:'a04-transfer-cost-review',excerpt:'A04三图像profile、18进程、540round和270echo，callWall/EndToEnd/echo口径分别保留；当前JSON整图传输不采用生产默认。'},
  {heading:'统一人工VS清单',anchor:'final-unified-manual-validation',excerpt:'五方向前后行为、最终包步骤与预期、自动证据、未验和用户结果统一记录；最终同一Release身份未冻结，旧image/guard/a45诊断身份不能混充。'},
  {heading:'分项时间估计',anchor:'implementation-eta-snapshot',excerpt:'production03于12:11交付；实施12:17与独测12:18条件时间，13:00或新结果先到重估；整包无可靠总时刻。'},
  {heading:'最新实施验收状态',anchor:'implementation-live-status',excerpt:'四层分别看：类型/BindingPlan基础、真实Workflow冻结解析发布、绑定目录与树、结果仓库与结果树。开发通过、独立验收与未闭环逐项分列。'},
  {heading:'看整体进展',excerpt:'五个方向的关系、已实施成果、已有独立验收、仍未闭环和后续改造；状态以本节最新状态表为准。'},
  {heading:'审核方案',excerpt:'查看统一架构推荐、复用/调整/替换取舍及适用边界；方案批准不等于产品验收完成。'},
  {heading:'审核整体方案',excerpt:'查看统一架构推荐、复用/调整/替换取舍及适用边界；方案批准不等于产品验收完成。'},
  {heading:'验证实际效果',excerpt:'按方向查看验收条件、已取得的结果和仍未闭环项，不把局部测试当整包通过。'},
  {heading:'效果验证',excerpt:'按方向查看验收条件、已取得的结果和仍未闭环项，不把局部测试当整包通过。'},
  {heading:'查技术依据',excerpt:'沿工具到结果显示的完整调用链、数据关系图和来源证据检查方案依据。'},
  {heading:'本次优化总览 · 五个方向',excerpt:'插件基础、脚本、图像源面板、中央预览、工具公共框架如何相互支撑，以及各自的实施与验收状态。'},
  {heading:'用日常说法找框架进度',excerpt:'不知道专业名词时，搜索整体进展、方案审核、实际效果、验收状态或技术依据，直达五方向总览。'}
 ].map(x=>({...x,anchor:x.anchor||'five-directions-overview'})):flat==='index.html'?[{heading:'当前交付总览 · 总进度与完成度',anchor:'delivery-home-overview',excerpt:'DELIVERY_LEDGER唯一原ID总账：原48项/纳入47/排除1；已验收、待验收两个子态、实施中、未开始、阻塞和待核分列；五方向、责任时间、关键路径与Release阶段。不以子测试算整项完成率。'}]:[];
 return [...chosen,...aliases].map(x=>({title,topic:topicFor(relative),pageTitle:title,path:pagePath,heading:x.heading,excerpt:x.excerpt,href:pagePath+(x.anchor?'#'+encodeURIComponent(x.anchor):''),source:page.source}));
}
module.exports=function(){
 const records=[];
 for(const [flat,relative] of Object.entries(layout.files))records.push(...recordsFor(flat,relative));
 const payload='window.DOCUMENT_SEARCH_INDEX='+JSON.stringify(records).replaceAll('<','\\u003c')+';\n';
 const indexFile=dir+'document-search-index.js',runtimeFile=dir+'document-search.js',rootIndex=path.join(layout.root,'公共资源/document-search-index.js'),rootRuntime=path.join(layout.root,'公共资源/document-search.js'),snapshotIndex=layout.sourceSnapshotDirectory+'document-search-index.js',snapshotRuntime=layout.sourceSnapshotDirectory+'document-search.js';
 const runtime=fs.readFileSync('artifacts/validation/document-search-runtime.js');
 const packed=fs.existsSync(dir+'index.html')&&fs.readFileSync(dir+'index.html','utf8').includes('data-managed-library-router');
 for(const file of [...(packed?[]:[indexFile]),rootIndex,snapshotIndex])writeGenerated(file,payload);
 for(const file of [...(packed?[]:[runtimeFile]),rootRuntime,snapshotRuntime])writeGenerated(file,runtime);
 const manifest={version:1,records:records.length,pages:new Set(records.map(x=>x.path)).size,bytes:Buffer.byteLength(payload),topics:[...new Set(records.map(x=>x.topic))],source:'Canonical current topic pages; short visible headings/excerpts only. Scripts, styles, navigation, details, tables, history, audits and snapshots excluded.',canonicalPages:records.filter(x=>x.source==='canonical').length,legacyFallbacks:[...new Set(records.filter(x=>x.source!=='canonical').map(x=>x.path))],remoteFetch:false,thirdParty:false};
 fs.writeFileSync('artifacts/validation/document-search-index-manifest.json',JSON.stringify(manifest,null,2)+'\n');
 return {records:records.length,pages:new Set(records.map(x=>x.path)).size,bytes:Buffer.byteLength(payload)};
};
if(require.main===module)console.log(JSON.stringify(module.exports()));
