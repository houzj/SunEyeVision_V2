const fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');
const p='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html',base='D:/MyWork/REA逆向测试/analysis/decompiled/';let s=fs.readFileSync(p,'utf8');
const E=x=>String(x).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
function cite(file,needle,before=2,after=10){const f=base+file,l=fs.readFileSync(f,'utf8').split(/\r?\n/),i=l.findIndex(x=>x.includes(needle));if(i<0)throw Error(file+': '+needle);return `<details class="vm-proof"><summary>${E(path.basename(file))} · ${i+1}行 · ${E(needle)}</summary><p><a href="${pathToFileURL(f).href}">${E(file)}</a>（当前反编译材料，静态观察）</p><pre>${E(l.slice(Math.max(0,i-before),i+after).map((v,n)=>`${Math.max(0,i-before)+n+1} ${v}`).join('\n'))}</pre></details>`;}
const sdk='VM.PlatformSDKCS.Audit/VM.PlatformSDKCS/',core='VM.Core/VM.Core/',wpf='VMControls.WPF/VMControls.WPF/';
const tasks=[
['RE-W1','P0','原生工作流调度','ProcessManager → IMVS_ExecuteOnce_V30 / ContinousRunStart / StopExecute','进入 iMVS-6000PlatformSDK.dll 后的节点排队、依赖/分支、Group、线程和优先级尚未逆向闭合。','对三个入口追到原生任务队列/回调产生点；对分支、失败和并发用同一小方案作受控时序验证。','不能声称我们的调度更灵活/更稳，也不能确认原厂所有并发与取消语义。'],
['RE-W2','P0','等待、停止与异常退出','同步等待标志由状态回调置位，P2P带超时参数，StopExecute传3u','SyncWaitor实现未在当前材料中恢复；3u的单位/精确语义、无回调和停止失败如何解除等待未核实。','取得SyncWaitor所在原厂程序集并反编译；核对原生停止入参及状态回调顺序，验证无响应/异常/重入。','不能以有Stop接口宣称可靠取消；自有异步也不能自动胜出。'],
['RE-I1','P0','共享内存寿命与帧代次','深/浅拷贝路径、Open/Map、Unmap/Close已见','原生生产者何时覆写/回收共享区、映射期间是否保证稳定、执行次数是否在所有消费点校验未核实。','沿VMGetMemDataPtr和生产者缓冲管理追踪；验证保留浅拷贝后连续执行、切方案、删除模块与深拷贝对照。','不能推荐无条件零拷贝；独立持有结果需要寿命证据。'],
['RE-I2','P0','缓存与结果资源全链','历史查询与Release配对、显示源变化释放decorator已见','图像/轮廓/3D及异常路径是否全部配对释放，UI清空后原生缓存何时退还未闭合。','逐类追Query/PreProData/Release及历史清理；测切节点/清历史/方案切换前后的映射、句柄与原生内存。','不能把Clear列表或ImageBaseData.Dispose当全部资源已确定释放。'],
['RE-P1','P0','模块关闭后的原生资源','VmModule.Dispose → 解绑/移出目录 → IMVS_CloseOneModule','原生Close是否停止后台任务、等待回调、断设备、释放算法句柄，失败时残留对象和恢复方式未逆向完。','沿CloseOneModule原生入口追模块实例销毁与驱动/算法析构；验证运行中删除、共享设备与失败再建。','只能说对象删除链已明确，不能说完整安全卸载已证明。'],
['RE-P2','P1','插件发现、依赖与程序集卸载','相机Cs/Control及依赖装载已见；模块删除不等于DLL卸载','非相机工具的完整发现/注册、缺依赖回滚、升级及全程序集卸载策略尚不完整。','核对模块目录/ActivatorWrap/所有装载宿主；追缓存与注册表解除，查原生库引用，再测替换包和重启升级。','不因没有卸载证据判定VM不支持；我们默认重启升级仍是自身策略。'],
['RE-X1','P1','跨链桥接与运行验证','已有托管代码与安装DLL边界元数据','保存的全部反编译材料与每个安装程序集的版本/哈希对应尚未逐一认证；native导出和函数实现尚未逐项对照。','逐程序集记录哈希/MVID和版本；认证PInvoke对应native导出，再按实际地址追函数，不把metadata token当native地址。','当前方法片段为已有静态材料证据；不能冒称全部来自本次重新反编译。']
];
const work=`<article class="vm-deep-chain" id="vm-scheduler-evidence"><h3>1．工作流调度：补齐请求、等待、回调与停止边界</h3>
<p><span class="pill">已核实：托管控制链</span> <span class="pill pending">仍需逆向：原生排队/线程/分支与等待异常</span></p>
<pre>VmProcedure.Run
 → ProcessManager.SyncExecuteOnce
 → 校验流程ID并清 mProcessRunFlag
 → ServerSDKAPI.IMVS_ExecuteOnce_V30 → NativeMethods → 原生平台（内部待逆向）
 → 可选 SyncWaitor.PrecisionWait 等待
CVmObjectBase 状态事件 → ProcessManager.GetWorkStatusCallBackInfo
 → nWorkStatus == 0 时写入正常状态并置完成标志 → 转发 OnWorkStatusCallBack
连续执行：ContinousRunStart → IMVS_ContinousRunStart_V30
停止：RefreshUIStopExecute 在Task中调用 StopExecute → IMVS_StopExecute_V30(..., 3u)
单模块：SyncSelfRun → 按ID段清标志 → SelfExecuteOneModule → 检查状态 → 等待对应标志</pre>
<p>同步接口不是“原生函数返回就等于流程结束”：包装先提交请求，再等待事件置位。普通流程等待未见显式超时参数，P2P等待另传 nTimeout；SyncWaitor实现未获得，不能判断其轮询、消息泵、超时和异常退出策略。StopExecute的3u仅按实参记录，不推断单位。</p>
<p><strong>具体取舍：</strong>VM集中执行身份和回调，有利统一流程/模块状态；代价是消费者依赖平台回调及等待合同。我们以Task与取消令牌表达运行，更便于组合，但原生算法调用仍可能不可中断。推荐统一“接受、执行、完成、取消中、终止失败”状态，保留自有后端为暂定路线；不能根据语言或异步签名判断哪边取消更可靠。是否更换后端，待RE-W1/W2验证。</p>
${cite(core+'VmProcedure.cs','public override void Run()',2,16)}${cite(sdk+'ProcessManager.cs','public void SyncExecuteOnce(',1,24)}${cite(sdk+'ProcessManager.cs','private void GetWorkStatusCallBackInfo',1,24)}${cite(sdk+'ProcessManager.cs','public void RefreshUIStopExecute',1,29)}${cite(sdk+'ModuleManager.cs','public void SyncSelfRun',1,17)}${cite(sdk+'ModuleManager.cs','if (1 == pstModuleStatuInfo[i].bIsSelfRun)',1,13)}
<p><strong>待继续逆向：</strong><a href="#RE-W1">RE-W1</a>、<a href="#RE-W2">RE-W2</a>。已补请求/回调链，未完成整个调度器逆向。</p></article>`;
const image=`<article class="vm-deep-chain" id="vm-image-release-evidence"><h3>2．图像资源：区分四类所有权和释放动作</h3><p><span class="pill">已核实：深浅拷贝、映射及查询释放入口</span> <span class="pill pending">仍需逆向：原生缓冲寿命与全异常路径</span></p>
<div class="table"><table><thead><tr><th>资源与路径</th><th>已见行为</th><th>不可外推的结论</th></tr></thead><tbody>
<tr><td>结果深/浅拷贝</td><td>VmModule.ModuResultMemType默认Memory_Deep_Copy；SetModuleResult据模式选择。图像浅拷贝创建SharedMemoryItemVM，深拷贝经LargeObject/GetData(false)取得byte[]。</td><td>不能由默认值断言所有宿主和显示路径都深拷贝，实例可以设置模式。</td></tr>
<tr><td>共享内存映射</td><td>GetDataPtr先DisposeRes，再调用VMGetMemDataPtr；DisposeRes执行UnmapViewOfFile与CloseHandle，析构也调用它。对象保存期望/当前执行次数。</td><td>映射解除不等于原生生产缓冲已销毁；字段存在也不证明所有读取都校验代次。</td></tr>
<tr><td>图像包装与固定句柄</td><td>ImageData从指针读取时可Marshal.Copy；ImageBaseData.Dispose与析构中可见GCHandle.Free。其Dispose本体未显式调用sharedMemObj.DisposeRes。</td><td>不能把ImageBaseData.Dispose解释为所有映射立即确定释放；也不能仅凭此断言泄漏，映射有自己的后续释放链。</td></tr>
<tr><td>历史查询结构</td><td>QueryModuleHistoryResult把原生返回指针交给调用方；ReleaseModuleHistoryResult调用ReleaseDynamicMemory。聚合结果类Release遍历指针，VmModule刷新结果finally里调用Release。</td><td>查询结构释放、图像byte[]、共享映射、平台历史缓存是不同资源。</td></tr>
<tr><td>显示订阅与历史列表</td><td>ModuleResultView.DoSourceChanged释放旧decorator并ClearDataStore；decorator.Dispose先DeInit解绑多个事件再清历史列表。手动清历史另调用ClearModuleHistoryResult。</td><td>清UI列表不等于证明平台图像缓冲全部释放；切方案实际触发哪些显示重绑仍需验证。</td></tr>
</tbody></table></div>
<p><strong>具体取舍与推荐：</strong>VM已见“可选择复制或共享、显式查询释放、显示解绑”三类机制，不能再概括为完全未知。复制有利保留稳定结果但增加内存带宽和分配；浅拷贝减少复制，却依赖生产者缓冲寿命。我们的帧Dispose直接释放Image，同样需要跨工具/显示持有规则。推荐按使用者声明借用/保留/复制，默认由场景和测量决定，而不是一律零拷贝或一律深拷贝；最终合同暂待RE-I1/I2。</p>
${cite(core+'VmModule.cs','public ModuResultMemoryTypeEnum ModuResultMemType',1,4)}${cite(sdk+'ModuleResultManager.cs','if (bIsShallowCopy)',2,20)}${cite(sdk+'SharedMemoryItemVM.cs','public IntPtr GetDataPtr()',1,10)}${cite(sdk+'SharedMemoryItemVM.cs','public bool DisposeRes()',1,27)}${cite(sdk+'ImageBaseData.cs','public void Dispose()',1,10)}${cite(sdk+'ModuleManager.cs','public int ReleaseModuleHistoryResult',1,16)}${cite(sdk+'ImvsSdkDefine.cs','public class IMVS_QUERY_MODULE_RES_INFO_LIST_Class',1,26)}${cite(core+'VmModule.cs','iMVS_QUERY_MODULE_RES_INFO_LIST_Class?.Release();',4,4)}${cite(wpf+'ModuleResultView.xaml.cs','private void DoSourceChanged',1,20)}${cite('VMControls.WPF/VMControls.WPF.DataForUI/ModuleDisplayDecorator.cs','public void Dispose()',1,29)}
<p><strong>待继续逆向：</strong><a href="#RE-I1">RE-I1</a>、<a href="#RE-I2">RE-I2</a>；不据局部Dispose判断长期资源行为。</p></article>`;
const plugin=`<article class="vm-deep-chain" id="vm-plugin-lifetime-evidence"><h3>3．插件生命周期：模块创建、删除与DLL卸载分别核对</h3><p><span class="pill">已核实：模块实例管理与托管删除顺序</span> <span class="pill pending">仍需逆向：原生销毁和全插件装载/卸载</span></p>
<pre>相机模块：目录发现 → Cs / Control / 依赖装载 → 反射构造 → VmSolution关联
一般VmModule创建路径：SyncAddOneModule → IMVS_AddOneModule → 返回模块信息 → Init
单模块执行：SelfRun / SyncSelfRun（不同于创建）
VmModule.Dispose → InnerDispose
 → 防重入状态 / 清变量绑定关系 / 必要时导出撤销数据
 → 组合模块删除内部模块 / 移除前驱连线 / 删除参数绑定
 → CommonDispose：取消模块订阅、释放Inputs/Outputs
 → 移出父模块集合和VmSolution.AllModules
 → CloseModuleInterface → ModuleManager.CloseOneModule → IMVS_CloseOneModule
 → 标记disposed / 登记撤销及通知</pre>
<p>现在可以明确：模块删除不只是清字典，还涉及依赖、订阅、IO和原生关闭。InnerDispose捕获异常后复位IsOnDispose并转换异常；这不足以证明前面每一步都会回滚。原生关闭内部是否等待任务和回调、释放驱动及算法资源仍待追。上述也未显示程序集卸载机制，既不能称已证明热卸载，也不能反向断言VM不支持。</p>
<p><strong>具体取舍与推荐：</strong>VM集中模块身份使删除时可以处理绑定和撤销，代价是删除跨多个状态域，原生关闭失败时的一致性需要验证。我们的插件装载隔间能处理依赖与类型身份，但也必须具备实例停止、订阅解除、IO/设备释放和目录注销的顺序。推荐借鉴明确删除合同与注销前检查，默认重启升级仍是我们当前发布策略；是否需要热替换，要等RE-P1/P2及产品停机要求，不按“有Dispose”判断。</p>
${cite(sdk+'ModuleManager.cs','public ImvsSdkDefine.IMVS_MODULE_INFO SyncAddOneModule',1,18)}${cite(core+'VmModule.cs','Init(ServerSDKManager.serverSDKManager.mModuleManager.SyncAddOneModule',3,5)}${cite(core+'VmModule.cs','protected void CommonDispose()',1,23)}${cite(core+'VmModule.cs','private void InnerDispose()',1,23)}${cite(core+'VmModule.cs','CommonDispose();',3,24)}${cite(sdk+'ModuleManager.cs','public void CloseOneModule',1,13)}
<p><strong>待继续逆向：</strong><a href="#RE-P1">RE-P1</a>、<a href="#RE-P2">RE-P2</a>；仅闭合部分托管对象链，不登记整域完成。</p></article>`;
const evidencePath=path.resolve('artifacts/validation/vm-managed-boundaries-20261019.json');
const panel=`<section id="vm-evidence-progress" data-section><h2>VM关键链路：本轮已核实与继续逆向清单</h2>
<div class="note warning"><strong>逆向尚在进行，证据不足必须显式保留。</strong>“未恢复/未核实”不表示VM没有能力；不以证据缺口支持我们更优。以下三域均为部分核实，推荐为有条件的暂定建议。观察到API与释放函数，不等于原生内部、全部异常路径和长稳行为已完成验证。</div>
<p>本轮继续读取原厂托管反编译方法，并对安装副本做REA托管到原生声明检查，不启动VM或连接设备。已有反编译材料与所有原始程序集尚未逐一认证，见RE-X1。</p>
<p><strong>本次新增二进制边界证据：</strong>安装文件 <code>D:/Program Files/VisionMaster4.4.0/Applications/PublicFile/x64/VM.PlatformSDKCS.dll</code>，SHA-256 <code>34c0c90b00cb75a701685b96670b6490f61376f62eacc0ab29d240157db401c1</code>，MVID <code>e7b3a3b0-7083-486a-8ca2-97cecec02ea2</code>。REA Evidence <code>ev_3d164ee62a60810882e6c7885ea294d96fd5dc0b2cef93878d1aab1c2a296d5f</code>，元数据覆盖完整仅指本次metadata表读取；不表示功能逆向完成。</p>
<p>声明将 ExecuteOnce、ContinousRunStart、StopExecute、AddOneModule、CloseOneModule、ReleaseDynamicMemory 等导入到 <code>.\\iMVS-6000PlatformSDK.dll</code>。这是 <strong>managed-declaration-only</strong>，未认证native导出地址、未进入这些原生函数体；不能把托管token当原生地址。目录为x64也不用于推断CLI宿主位数。<a href="${pathToFileURL(evidencePath).href}">查看完整REA静态声明证据</a>。</p>
<div class="overview-links"><a href="#vm-scheduler-evidence">工作流调度与停止</a><a href="#vm-image-release-evidence">图像资源释放</a><a href="#vm-plugin-lifetime-evidence">模块与插件生命周期</a><a href="#vm-reverse-backlog">仍需继续逆向</a></div>
${work}${image}${plugin}
<article id="vm-reverse-backlog"><h3>仍需继续逆向：任务、证据缺口与结论限制</h3><p>P0表示直接影响当前架构决策，不表示已经授权现场动作。本清单是静态逆向与后续验证计划；本轮未执行所列运行实验。</p><div class="table"><table><thead><tr><th>任务 / 优先级</th><th>已核实终点</th><th>缺少什么证据</th><th>下一步逆向 / 验证</th><th>对比较与推荐的限制</th></tr></thead><tbody>${tasks.map(([id,pr,n,known,gap,next,limit])=>`<tr id="${id}" class="reverse-pending"><td><strong>${id} · ${pr}</strong><p>${n}</p><span class="pill pending">需要继续逆向</span></td><td>${known}</td><td>${gap}</td><td>${next}</td><td>${limit}</td></tr>`).join('')}</tbody></table></div></article>
<p>本轮增量：同步请求/回调、深浅结果与部分释放、实例删除顺序已补实；原生调度、缓冲寿命、模块销毁和独立插件装载仍未完成。原16领域保持0整域闭合，不据这批方法片段提升为全架构完成。</p></section>`;
s=s.replace('<section id="chain-audit" data-section>',()=>panel+'\n<section id="chain-audit" data-section>');
s=s.replace('<a href="#chain-audit">补漏 · 11条实际调用链</a>','<a href="#vm-evidence-progress">VM新增证据与待逆向清单</a><a href="#chain-audit">补漏 · 11条实际调用链</a>');
// Attach precise progress and pending-work status at the original comparison sites.
const updates={
'vm-workflow':['vm-scheduler-evidence','本轮已核实同步请求→原生声明→回调置位→等待返回及连续/停止入口；原生调度、SyncWaitor和停止异常仍需RE-W1/W2。不能再把整条包装层都说未知，也不能说调度器已恢复。'],
'vm-plugin':['vm-plugin-lifetime-evidence','本轮已核实模块创建与InnerDispose中的依赖清理、取消订阅、IO释放、移出目录及原生关闭顺序；原生销毁/程序集卸载仍需RE-P1/P2。'],
'vm-persistence':['vm-plugin-lifetime-evidence','模块删除会在需要时导出撤销数据并登记删除/导入操作，但异常中途的完整回滚仍未证实。'],
'control-chain':['vm-scheduler-evidence','本轮补齐ProcessManager的同步提交/回调等待；If/Group的原生依赖、分支和任务队列仍需RE-W1继续逆向。'],
'image-lifetime-chain':['vm-image-release-evidence','本轮确认默认深拷贝、可选共享路径、共享映射析构释放及历史查询显式释放；原生生产缓冲复用和全异常释放仍需RE-I1/I2。'],
'plugin-backend':['vm-scheduler-evidence','推荐保持暂定：VM有明确的同步回调等待与单模块状态链，不能用“原生不可知”推定劣势。自有后端需同条件验证取消、分支、结果和长稳。'],
'plugin-unload':['vm-plugin-lifetime-evidence','模块删除的解绑/释放/原生关闭已见，DLL卸载未核实。我们重启升级是自身策略，不是对VM能力的判定。'],
'contract-input':['vm-image-release-evidence','VM提供可选深浅结果模式，提醒我们按消费者明确复制与借用；单有参数字典快照不能保证图像稳定。'],
'contract-lifetime':['vm-plugin-lifetime-evidence','参考VM实例删除顺序设计停止/解绑/释放合同，但仍需验证原生关闭失败及在途回调；不将Dispose视为热卸载。'],
'ui-results':['vm-image-release-evidence','切模块源会Dispose旧decorator、清数据并解绑事件；手动历史清除另走SDK入口。不能把UI清空与原生缓冲释放合并解释。']
};
for(const [id,[ref,txt]] of Object.entries(updates)){const re=new RegExp(`(<article[^>]*id="${id}"[\\s\\S]*?)(</article>)`);if(!re.test(s))throw Error('missing '+id);s=s.replace(re,(_,a,b)=>a+`<div class="note vm-progress"><strong>本轮核实进度：</strong>${txt} <a href="#${ref}">新增证据及继续逆向任务</a>。</div>`+b);}
// Amend the original claims without replacing unknowns by broad positive claims.
s=s.replace('平台结果转换、原生指针借用及显示缓存的完整所有权仍未知。','本轮已确认深浅结果转换、共享映射和历史查询释放的部分链路；原生缓冲寿命及所有显示缓存的完整所有权仍待RE-I1/I2。');
s=s.replace('<h2>四、全系统优化方案与推荐理由</h2>','<h2>四、全系统优化方案与推荐理由</h2><div class="note warning"><strong>推荐状态：暂定，待关键逆向补证。</strong>新增VM证据显示其具有回调等待、可选深浅结果、查询释放及模块删除合同。建议吸收这些明确边界；我们的运行时、图像复制策略与插件升级选择仍要在RE-W1/W2、RE-I1/I2、RE-P1/P2补证后复审。证据不足不是VM缺功能，更不是我们更优的理由。</div>');
fs.writeFileSync(p,s);
const cp='artifacts/validation/check-vm-design-plan.cjs';let c=fs.readFileSync(cp,'utf8').replaceAll('count(),28','count(),29').replaceAll('count()<28','count()<29').replace("locator('section[data-section]:visible').count(),12","locator('section[data-section]:visible').count(),13").replace('sections:28','sections:29');c=c.replace("assert.equal(await page.locator('.chain-audit').count(),11);","assert.equal(await page.locator('.chain-audit').count(),11);\n  assert.equal(await page.locator('.vm-deep-chain').count(),3);\n  assert.equal(await page.locator('.reverse-pending').count(),7);\n  assert.ok(await page.locator('.vm-proof').count()>=20);\n  assert.ok((await page.locator('#vm-evidence-progress').textContent()).includes('managed-declaration-only'));\n");c=c.replace("await page.locator('#image-source-chain').evaluate", "await page.locator('#vm-image-release-evidence').evaluate(e=>e.scrollIntoView({block:'start'}));await page.screenshot({path:'artifacts/validation/vm-design-plan-vm-release-evidence.png'});\n  await page.locator('#image-source-chain').evaluate");fs.writeFileSync(cp,c);
console.log('Added 3 verified managed chain expansions, 7 explicit reverse tasks; recommendations remain conditional.');
