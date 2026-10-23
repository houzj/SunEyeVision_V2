const fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');const p='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';let s=fs.readFileSync(p,'utf8');
const E=x=>String(x).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const nativeInventory=JSON.parse(fs.readFileSync('artifacts/validation/vm-native-export-inventory.json','utf8'));
const raw=fs.readFileSync('artifacts/validation/vm-p0-ranged-evidence.txt','utf8');
function part(name){const start=raw.indexOf('FUNCTION '+name+' @'),end=raw.indexOf('\nFUNCTION ',start+10);if(start<0)throw Error(name);return raw.slice(start,end<0?undefined:end);}
function excerpt(name,needle,before=3,after=13){const lines=part(name).split(/\r?\n/),i=lines.findIndex(x=>x.includes(needle));if(i<0)throw Error(name+needle);return `<details class="native-p0-proof"><summary>${name} · ${E(needle)}</summary><pre>${E(lines.slice(Math.max(0,i-before),i+after).join('\n'))}</pre></details>`;}
const local=f=>pathToFileURL(path.resolve('artifacts/validation/'+f)).href;
const html=`<article class="vm-deep-chain" id="vm-native-p0"><h3>P0进展：原生入口已进入，调度器仍未追到</h3><p><span class="pill">已核实：导出与入口分发</span><span class="pill pending">未闭合：间接目标、节点调度、停止完成、缓冲寿命、资源销毁</span></p>
<p>目标为安装副本 <code>iMVS-6000PlatformSDK.dll</code>，SHA-256 <code>${nativeInventory.sha256}</code>。本轮先用独立PE字节解析核对导出，再复用既有Ghidra12.1.4进行限定函数静态反编译，未执行目标程序。以下是<strong>独立工具观察，不冒称REA Evidence</strong>；REA整库自动分析曾超时，工具超时不是原厂行为证据。</p>
<h4>原生调用链新增到哪一步</h4><pre>托管 ProcessManager / ModuleManager
 → 对应原生 IMVS_* 导出：校验/查找平台句柄，取得公共管理对象
 → 单次执行：0x180647750 尾跳 0x18066ffa0（日志名 SM_ExecuteOnce_V30）
 → 连续执行：0x180647820 → 0x180670170（命令号0x407）
 → 停止执行：0x180647920 → 0x1806702f0（命令号0x408）
 → 关闭模块：0x180645950 → 0x180663ed0（命令号0x3ec）
 → 公共 0x1806c00d0，字符串标识 CServerManager::SendCommand
 → 对象偏移 +0x2398 的函数指针（实际接收方仍需继续逆向）
 → 真正节点排队、取消/终止、驱动释放：尚未追到</pre>
<p>“服务端”仅沿原厂日志名CServerManager描述；目前没有据此认定一定跨进程或走网络。公共发送层出现函数指针与回复解析，原生DLL导出层并不是已恢复的完整调度器。</p>
<div class="table"><table><thead><tr><th>动作</th><th>新增的具体静态证据</th><th>不能据此认定</th></tr></thead><tbody>
<tr><td>执行/连续</td><td>单次执行导出尾跳SM层；连续SM层使用0x407和格式串%d-%u构造命令，再调用公共发送函数。</td><td>不能由命令返回成功推断所有节点完成，不能推断线程、优先级或依赖调度。</td></tr>
<tr><td>停止</td><td>Stop SM层将流程ID和第三实参按%d-%u-%u编码，命令号0x408，调用SendCommand并处理返回状态；第三实参从托管传3u一路保留。</td><td>3u单位、接收方停止策略、是否等待设备/算法结束、完成回调仍未知，不能称可靠强制取消。</td></tr>
<tr><td>模块关闭</td><td>Close SM层用0x3ec、none和模块ID构造%d-%s-%d命令，再转交SendCommand。</td><td>这只补到删除请求，不证明后台线程、共享设备和全部算法句柄已回收；真正销毁处理器待定位。</td></tr>
<tr><td>图像/结果内存</td><td>ReleaseDynamicMemory导出存在于RVA0x650730，并尾跳0x1806af090。上轮已见托管查询结构显式释放和共享映射解绑。</td><td>尚未认证所有内存类型的深层销毁，也没有取得共享区生产者覆写/保留保证；缓冲寿命P0仍未闭合。</td></tr>
</tbody></table></div>
<h4>导出地址与文件偏移（静态定位，不是运行地址）</h4><div class="table"><table><thead><tr><th>导出</th><th>RVA</th><th>首选VA</th><th>文件偏移</th></tr></thead><tbody>${nativeInventory.exports.map(e=>`<tr><td>${e.name}</td><td>${e.rva}</td><td>${e.preferredVA}</td><td>${e.fileOffset}</td></tr>`).join('')}</tbody></table></div>
<h4>证据质量与限制</h4><p>反编译器对若干参数类型有恢复警告，原始类型名和返回签名不能当原厂源码。Windows x64的.pdata可能把同一函数分为主段与链式展开段：本次输出登记了主段，SendCommand另有延续段，因此<strong>不宣称完整函数体或CFG已认证</strong>。关键尾跳、命令常量与格式串以原始字节/反汇编交叉核对；函数指针接收方仍未解析。</p>
${excerpt('IMVS_ExecuteOnce_V30','18064780e JMP',5,3)}${excerpt('StopDispatch','sprintf_s',2,10)}${excerpt('StopDispatch','1806703b6 MOV R9D',4,20)}${excerpt('ContinuousDispatch','180670225 MOV R9D',3,17)}${excerpt('CloseDispatch','180663f89 MOV R9D',4,18)}${excerpt('ExecuteDispatch','param_1 + 0x2398',3,17)}${excerpt('IMVS_ReleaseDynamicMemory','1806507e7 JMP',5,3)}
<p>可复查产物：<a href="${local('vm-native-export-inventory.json')}">PE导出与字节索引</a> · <a href="${local('vm-native-p0-literals.json')}">原始格式串与日志名</a> · <a href="${local('vm-p0-ranged-evidence.txt')}">限定函数反编译/反汇编</a> · <a href="${local('vm-native-execute-p0.json')}">REA自动分析超时记录</a>。观察与分析代码保存在当前工作区artifacts/validation，不改安装DLL。</p>
<h4>对比较和推荐的具体修正</h4><p>VM已有统一命令入口、句柄查找与反馈路径，便于把托管API与后端分离；代价是实际执行/销毁的合同跨越更多层，诊断必须关联命令与结果回调。我们的直接运行请求路径较短且源码可控，但这不证明性能或取消更好。推荐保留自有运行时为候选，同时建立命令接受、执行中、取消中和最终完成的分层状态；不能把SendCommand成功或取消令牌触发直接映射为“设备已停止”。</p>
<h4>接续P0断点</h4><ol><li>RE-W1：先恢复.pdata链式段，再定位+0x2398指针的装配来源与接收函数；沿0x407/单次执行命令进入真实节点调度器。</li><li>RE-W2：沿0x408接收处理追第三实参语义、停止完成条件和状态回调；SyncWaitor实现仍待取得。</li><li>RE-I1/I2：沿0x1806af090和共享区生产者追内存类型、引用与复用；当前不能决定全局零拷贝策略。</li><li>RE-P1：沿0x3ec接收处理追在途任务、回调和驱动析构；不能把请求提交等同模块销毁完成。</li></ol></article>`;
s=s.replace('<article class="vm-deep-chain" id="vm-scheduler-evidence">',()=>html+'<article class="vm-deep-chain" id="vm-scheduler-evidence">');
s=s.replace('未认证native导出地址、未进入这些原生函数体；不能把托管token当原生地址。','该元数据记录本身未认证native导出或函数体；本轮另用独立PE/Ghidra追到原生入口分发，见新增P0进展，仍不能把托管token当原生地址。');
s=s.replace('本轮继续读取原厂托管反编译方法，并对安装副本做REA托管到原生声明检查，不启动VM或连接设备。','本轮已补入独立原生P0入口追踪，并保留此前托管链与REA声明检查的历史证据；不启动VM业务或连接设备。');
s=s.replace('<h3>仍需继续逆向：任务、证据缺口与结论限制</h3>','<h3>仍需继续逆向：任务、证据缺口与结论限制</h3><p class="note"><strong>最新P0断点：</strong>已进入原生导出和CServerManager命令层；实际调度/停止/销毁在间接接收目标下方，尚未闭合。先前任务的“进入原生入口”已部分完成，但接收方及资源终态仍需继续。<a href="#vm-native-p0">查看新增证据</a>。</p>');
s=s.replace('进入 iMVS-6000PlatformSDK.dll 后的节点排队、依赖/分支、Group、线程和优先级尚未逆向闭合。','已补到CServerManager命令分发和+0x2398指针；接收方下的节点排队、依赖/分支、Group、线程和优先级仍未闭合。');
s=s.replace('原生Close是否停止后台任务、等待回调、断设备、释放算法句柄，失败时残留对象和恢复方式未逆向完。','原生Close已追到0x3ec命令提交；接收方是否停止后台任务、等待回调、断设备、释放算法句柄及失败恢复仍未逆向完。');
s=s.replace('原生调度、缓冲寿命、模块销毁和独立插件装载仍未完成。','已追到原生入口和命令分发，但调度接收方、缓冲寿命、模块销毁和独立插件装载仍未完成。');
fs.writeFileSync(p,s);
const task='docs/collaboration/CURRENT_TASK.md';let t=fs.readFileSync(task,'utf8');t=t.replace('# 当前协作任务',`# 当前协作任务\n\n## 2026-10-19 P0原生入口分发证据与阅读结构完成\n\n- 阅读结构已改为8视图/30节，原锚点保留；正文一章/一主题，全文搜索/打印恢复/前进后退通过。双方主题提供对照跳转。\n- REA整库自动分析330000ms超时；已复用既有Ghidra/JDK并用当前工作区独立脚本-noanalysis做限定原生函数反编译，产物明确不冒称REA Evidence。不执行安装目标，不改产品。8个PE导出核对原始字节，9个入口/直接被调函数记录反编译和反汇编。\n- 新证据：ExecOnce导出尾跳0x18066ffa0；连续/停止/关闭进入CServerManager命令分发（0x407/0x408/0x3ec），SendCommand在0x1806c00d0，继续间接调用对象+0x2398指针。实际接收方和节点调度/停止终态仍未解析；不能因此认定跨进程/网络。ReleaseDynamicMemory导出尾跳0x1806af090，缓冲生产者寿命及真正模块析构仍待追。\n- 限制：Ghidra恢复类型有警告，.pdata存在链式分段（SendCommand主段后有延续），当前输出不认证完整body/CFG，不将伪代码参数/返回值当原厂签名。已更新原HTML新增vm-native-p0和后续断点，保留7项待逆向任务和0整域闭合口径。\n- 分析产物在artifacts/validation，runtime-jdk与runtime-ghidra是指向既有工具的目录联接，清理时只能删除联接本身，不得递归删除目标。独立Ghidra缓存/临时项目定向当前工作区，未改全局配置；不提交推送，无跨AI交接。\n`);fs.writeFileSync(task,t);
console.log('Published bounded native P0 observations and unresolved dispatch boundary.');
