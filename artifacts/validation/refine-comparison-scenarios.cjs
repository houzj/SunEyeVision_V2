const fs=require('fs');const p='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';let s=fs.readFileSync(p,'utf8');
const rows=[['control-chain','A生产坐标、B消费时，执行顺序不能依赖Tab排列；自有调度能检查依赖，但排序与运行值提交仍在UI执行管理器，推荐迁移实际业务职责。'],['image-source-chain','预览与正式采图争用相机时，服务化有利共享，代价是触发模式、租约和帧策略需要统一协调。'],['script-chain','新增工具私有库时应共享宿主SDK身份；加载隔间只解决部分依赖问题，不能保证崩溃隔离或热卸载。'],['binding-chain','脚本动态字段变化必须同步参数选取、依赖计划和持久化；统一合同减少漂移，但要维护版本和旧方案诊断。'],['communication-chain','外部执行命令当前经UI Dispatcher读取方案再启动；推荐下沉这段分发以支持无界面调用，同时保留范围校验。'],['image-lifetime-chain','输入帧释放会释放图像，显示缓存不能无约定地保留同一对象；资源键统一只解决外观，不解决寿命。'],['persistence-chain','原子保存已有同目录临时文件与恢复副本；保留保护并审计类型迁移，避免仅改文件组织丢失恢复能力。'],['binding-chain','生产者失败时先移除本轮旧值，消费者得到不可用而不是上轮值；初值与运行值分开有明确收益，仍需处理引用类型所有权。'],['image-source-chain','预览前已连接与预览新建连接有不同关闭分支；服务共享允许前者保持连接，但需明确方案切换和异常释放。']];
let i=0;s=s.replace(/(<section id="system-comparison"[\s\S]*?<tbody>)([\s\S]*?)(<\/tbody>)/,(_,a,b,c)=>a+b.replace(/<tr>([\s\S]*?)<\/tr>/g,(m)=>{const [id,txt]=rows[i++];const at=m.lastIndexOf('</td>');return m.slice(0,at)+`<p><strong>具体取舍：</strong>${txt} <a href="#${id}">调用证据与验证</a></p>`+m.slice(at);})+c);if(i!==9)throw Error('row count');
const card={
'device-ownership':['lifecycle-chain','切方案不能只换显示列表：旧运行、旧连接回调与新设备配置要有转换顺序。独立服务便于跨工具复用，代价是应用级转换一致性由我们维护。'],
'device-extension':['image-source-chain','第二厂商只有同一采图工具无需改业务且能表达触发模式，才算扩展成功；公开工厂本身不等于已支持独立设备包。'],
'device-lifetime':['image-lifetime-chain','最新帧替换时旧帧会释放；采集租约保证互斥不代表所有结果消费者拥有图像，必须另定借用/复制规则。'],
'plugin-backend':['control-chain','无命中分支、嵌套子程序和循环取消决定运行语义；保留自有后端的前提是这些实际场景能通过验证，不因代码已经存在而免审。'],
'plugin-catalog':['binding-chain','工具目录既服务元数据又影响动态输出和依赖计划。推荐一份事实并注入运行消费者，代价是迁移真实调用点和注册变更的失效策略。'],
'plugin-unload':['script-chain','工具PluginLoadContext不可回收，脚本ScriptSandbox有ALC.Unload，两者不能混称支持热卸载；脚本池还会保留缓存，应分别验收。'],
'contract-sdk':['communication-chain','通信分发仍依赖UI项目和Tab，即使拆出纯SDK也不会自动获得无界面执行；必须迁移业务分发消费者，物理拆包后置。'],
'contract-input':['binding-chain','WorkflowRunRequest复制字典但不深拷贝图像/数组。统一请求减少注入歧义，却仍需值所有权与缺输入错误，不能把只读属性当不可变数据。'],
'contract-lifetime':['algorithm-chain','标定正式发布需要候选摘要和配置复查，关编辑窗不能随意代替发布或设备释放；专业副作用必须留在独立提交规则内。'],
'contract-templates':['image-lifetime-chain','调整颜色不重算有利复用，但若显示仍借用已释放输入图像会失效；结果投影需要代次和缓存释放合同，不能只换Tab布局。']};
for(const [id,[ref,txt]] of Object.entries(card))s=s.replace(new RegExp(`(<article class="compare-card" id="${id}">[\\s\\S]*?)(<details class="card-evidence">)`),(_,a,b)=>a+`<div class="scenario-note note"><strong>具体机制与取舍：</strong>${txt} <a href="#${ref}">查看实际调用链</a>。</div>`+b);
s=s.replace('本轮没有“整领域完成”项，也不计算完成百分比。','本轮没有“整领域完成”项，也不计算完成百分比。新增11条调用链作为部分证据增量，详见补漏审查，不自动升级整域状态。');
fs.writeFileSync(p,s);
const cp='artifacts/validation/check-vm-design-plan.cjs';let c=fs.readFileSync(cp,'utf8').replace("assert.equal(await page.locator('.chain-audit').count(),11);","assert.equal(await page.locator('.chain-audit').count(),11);\n  assert.equal(await page.locator('.scenario-note').count(),10);\n  const ids=await page.locator('[id]').evaluateAll(es=>es.map(e=>e.id));assert.equal(ids.length,new Set(ids).size,'duplicate IDs');");
c=c.replace("await page.locator('nav a[href=\"#contracts\"]').click();",`await page.locator('#image-source-chain').evaluate(e=>e.scrollIntoView({block:'start'}));await page.screenshot({path:'artifacts/validation/vm-design-plan-chain-audit.png'});
  await page.locator('nav a[href="#contracts"]').click();`);fs.writeFileSync(cp,c);
const task='docs/collaboration/CURRENT_TASK.md';let t=fs.readFileSync(task,'utf8').replace('# 当前协作任务',`# 当前协作任务\n\n## 2026-10-19 全面补漏与实际调用链复查\n\n- 用户要求全面检查遗漏、实际调用链和具体优缺点/推荐。新增11条专项链：绑定依赖编译、分支/子程序/循环、文件/相机输入、帧/结果所有权、脚本编译与ALC、通信触发、启动/切换/退出、保存复制撤销、算法标定发布、事件日志性能候选、权限语言与副作用。每条有场景、双方收益代价、理由成本、验收及带行号的实际片段。\n- 重要现状修正：跨流程排序、ApplyProducedGlobalValues与通信业务分发仍在UI项目；变量提交已有移除旧值和不改初值保护。标明子程序同步桥/间隔等待、最新帧替换释放、脚本与工具ALC不同，均不冒充已复现缺陷或性能结论。\n- 全部24个tools一级目录分类；原9项系统比较、10卡及13项UI比较分别补具体机制和链路引用。16领域保守状态保持0闭合/10部分/6未系统。基线HEAD=3a5dc6dd54f372699a450ad7862b52c961cd2797；仅文档与离线脚本，保留所有已有产品修改，不运行VM/产品/设备，不提交推送，无跨AI交接。\n`);fs.writeFileSync(task,t);
console.log('Refined 9 system rows, 10 original cards and validation evidence checks.');
