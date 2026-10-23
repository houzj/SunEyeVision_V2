const fs=require('fs'),path=require('path');
const p='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';let s=fs.readFileSync(p,'utf8');
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
for(const id of ['vm-architecture','suneye-architecture']){
 const re=new RegExp(`(<section id="${id}" data-section>)([\\s\\S]*?)(</section>)`);
 s=s.replace(re,(_,a,b,c)=>{
  const cut=b.indexOf('<h3>全系统分域说明');const inv=b.indexOf('<details class="file-inventory">');
  const h=b.match(/^[\s\S]*?<h2>[\s\S]*?<\/h2>/)[0];
  const old=b.slice(h.length,cut),domains=b.slice(cut,inv),inventory=b.slice(inv);
  const overview=id==='vm-architecture'?`<p class="lead">从宿主、方案与流程对象，到平台执行入口、参数框架和公共控件，分别梳理七个领域。下图表示已见职责关系，不声称原厂完整工程依赖已经恢复。</p><pre>VisionMasterMain / VMControls.WPF：界面、设备管理与配置入口
  ↔ VMControls.Data：工具箱等界面数据
  → VM.Core：VmSolution / VmProcedure / VmModule
      → VM.PlatformSDKCS：ProcessManager / ModuleManager → 平台 API
参数编辑：ModuleParam → Apps.XmlParser（参数树）→ Apps.XmlUI（参数窗口）
公共界面：VM.Controls / VMBaseControls；主题：Apps.ColorFun
设备：GlobalCameraManager → 设备模块程序集 → VmSolution 关联</pre>`:`<p class="lead">以真实项目引用和执行入口说明全系统，分别解释运行、装载、编辑和公共资源的职责。箭头表示已观察的引用或调用，不代表当前依赖已符合目标架构。</p><pre>UI → Workflow / DeviceDriver / Plugin.Infrastructure / Plugin.SDK
UI → CommonControl / HalAdapter / CvAdapter
Workflow → Core / Plugin.Infrastructure / Plugin.SDK（当前仍 UseWPF）
DeviceDriver → Core / Plugin.Infrastructure
Plugin.Infrastructure → Core / Plugin.SDK
Core → Plugin.SDK
CommonControl → Plugin.SDK
HalAdapter → Plugin.SDK；CvAdapter → Plugin.SDK / CvEngine
工具插件 → SDK 合同、所需算法后端与可选编辑支持</pre>`;
  return a+h+overview+domains+`<details><summary>设备链路与目录示例（此前的局部说明，保留备查）</summary>`+old+'</details>'+inventory+c;
 });
}
const vmRoot='D:/MyWork/REA逆向测试/analysis/decompiled';
const groups=fs.readdirSync(vmRoot,{withFileTypes:true}).filter(e=>e.isDirectory()).map(e=>e.name);
s=s.replace('<h3>全系统分域说明：7 个领域</h3>','<h3>全系统分域说明：7 个领域</h3><p>已有反编译目录总览（分析分组，不是原厂源码项目清单）：</p><pre>'+esc(groups.join('\n'))+'</pre>');
// Include a machine-derived reference table, rather than inferring dependencies from project names.
const projects=fs.readdirSync('src',{withFileTypes:true}).filter(e=>e.isDirectory()).flatMap(e=>fs.readdirSync('src/'+e.name).filter(f=>f.endsWith('.csproj')).map(f=>'src/'+e.name+'/'+f));
const rows=projects.map(f=>{const t=fs.readFileSync(f,'utf8'),refs=[...t.matchAll(/<ProjectReference\s+Include="([^"]+)"/g)].map(m=>m[1]);return `<tr><td><a href="${require('url').pathToFileURL(path.resolve(f)).href}">${esc(f)}</a></td><td>${refs.map(esc).join('<br>')||'未声明项目引用（仍可能有包和程序集引用）'}</td><td>${/<UseWPF>true<\/UseWPF>/.test(t)?'是':'未显式启用'}</td></tr>`;}).join('');
const table=`<h3>当前项目引用清单（由 csproj 提取）</h3><div class="table"><table><thead><tr><th>项目</th><th>直接 ProjectReference</th><th>UseWPF</th></tr></thead><tbody>${rows}</tbody></table></div><p>只列直接项目引用，包、条件属性、传递依赖和原生依赖不包含在本表。真实构建依赖还需结合 Directory.Build.props / targets；这是观察清单，不是推荐依赖图。</p>`;
s=s.replace(/(<section id="suneye-architecture" data-section>[\s\S]*?)(<details class="file-inventory">)/,(_,a,b)=>a+table+b);
fs.writeFileSync(p,s);
const task='docs/collaboration/CURRENT_TASK.md';let t=fs.readFileSync(task,'utf8');t=t.replace('# 当前协作任务',`# 当前协作任务\n\n## 2026-10-19 全系统架构与文件结构分域梳理\n\n- 按用户进一步要求扩展原 HTML：双方各按工作流/方案、设备通信、工具插件、SDK、UI、公共控件、保存部署七域独立说明职责、调用关系、输入输出、文件与边界；先双方说明，再七项比较及原10卡，最后全系统目标、四阶段优化、推荐理由与改变条件。原设备局部说明折叠保留。\n- 复核 VmProcedure 的 ProcessManager 调用、ModuleManager 单模块执行、参数树及窗口、主题配置入口；核对本项目就绪队列实际入口、加载上下文、项目引用与构建输出。新增自动提取的全部 src 项目直接引用及 UseWPF 清单、限定范围文件索引。分析文件清单不冒充原厂工程；文件列入索引不等于逐个语义审计。\n- 全系统说明覆盖不等于全逆向闭合，原16项证据矩阵仍保留0闭合/10部分/6未系统；未运行原厂、产品、设备或设计器，仅文档和离线检查；不改产品与原有未提交代码，不提交推送，无跨AI交接。\n`);fs.writeFileSync(task,t);
console.log('Reordered system-first explanations and extracted '+projects.length+' project references.');
