const fs=require('fs'),path=require('path'),crypto=require('crypto');
module.exports=function(){
const base='src/Plugin.SDK/';
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(x=>x.isDirectory()?(x.name==='obj'||x.name==='bin'?[]:walk(dir+'/'+x.name)):[dir+'/'+x.name]);}
const projects=[
['Contracts','src/Contracts/SunEyeVision.Contracts.csproj','新建 / net9.0','公共值、schema、资源与后端最小接口',[]],
['Tool.SDK','src/Tool.SDK/SunEyeVision.Tool.SDK.csproj','新建 / net9.0','工具接口、声明与作者数据模型',['Contracts']],
['Runtime.SDK','src/Runtime.SDK/SunEyeVision.Runtime.SDK.csproj','新建 / net9.0-windows x64；UseWPF=false','客户门面；不静态引用执行实现',['Contracts']],
['Controls.Wpf','src/Controls.Wpf/SunEyeVision.Controls.Wpf.csproj','新建 / WPF','可选Viewer/Editor；后续可再拆包',['Contracts','Tool.SDK','Runtime.SDK']],
['Runtime.Engine','src/Runtime.Engine/SunEyeVision.Runtime.Engine.csproj','新建 / 内部装配','实现后端Factory、调度适配与Owner',['Contracts','Tool.SDK','Workflow','Plugin.Infrastructure','DeviceDriver']],
['Workflow','src/Workflow/SunEyeVision.Workflow.csproj','复用整改；最终去WPF','保留执行内核，不替换为SDK门面',['Contracts','Tool.SDK']],
['Plugin.Infrastructure','src/Plugin.Infrastructure/SunEyeVision.Plugin.Infrastructure.csproj','复用整改','插件发现/注册/解析；不公开给客户',['Contracts','Tool.SDK']],
['DeviceDriver','src/DeviceDriver/SunEyeVision.DeviceDriver.csproj','复用；UI传递依赖另审','相机/通信实现',['Contracts']],
['Plugin.SDK','src/Plugin.SDK/SunEyeVision.Plugin.SDK.csproj','保留旧WPF兼容发行','旧ABI、BAML、资源URI；不供新纯API链引用',[]]
];
const overrides={
'Core/ToolExecutor.cs':['Runtime.Engine','Execution/ToolExecutor.cs','拆分整改','把参数准备/同步异步执行编排留内部，公共接口只返回运行合同；旧入口由兼容适配委托，不能让工具反调宿主。'],
'Core/ToolRunner.cs':['Runtime.Engine','Execution/ToolRunner.cs','拆分整改','统一开发测试宿主与正式执行入口；旧静态调用保留适配，不能复制一套取消语义。'],
'Core/ToolHelpers.cs':['Runtime.Engine','Execution/ToolHelpers.cs','成员级拆分','纯作者助手另移Tool.SDK；执行、服务定位部分不进公共合同。'],
'Core/IToolEditorFactory.cs':['Controls.Wpf','Editors/IToolEditorFactory.cs','新旧并存适配','Window/FrameworkElement签名仅留WPF编辑器；旧工厂ABI不直接改。'],
'Core/DebugWindowStyle.cs':['Controls.Wpf','Editors/DebugWindowStyle.cs','迁移并保留旧资源入口','仅编辑器使用，不进入算法运行接口。'],
'Execution/Results/ToolResults.cs':['Tool.SDK','Results/ToolResults.cs','成员级拆分整改','强类型输出与反射输出声明保留；显示投影转Controls.Wpf适配，公开ResultSnapshot另建Contracts。using System.Windows本身不能证明每个成员依赖WPF；移动前逐签名审核。'],
'Execution/Parameters/ParamSetting.cs':['Tool.SDK','Parameters/ParamSetting.cs','保留身份并适配','已有SourceFieldId/SourceWorkflowId必须保留；外部schema不暴露可变ParamSetting。'],
'Execution/Parameters/ToolParameters.cs':['Tool.SDK','Parameters/ToolParameters.cs','保留基础＋隔离运行附属状态','保留Generic/$type识别；只序列化设计态，NodeResults等运行附属数据留内部上下文。'],
'Execution/Parameters/ExecutionContext.cs':['Runtime.Engine','Parameters/ExecutionContext.cs','拆分重写边界','内部可变上下文不作为应用SDK；对外只读InputReader和冻结快照。'],
'Vision/Contracts/IVisionImage.cs':['Contracts','Vision/IVisionImage.cs','保留旧接口＋新增lease','旧Clone/Dispose不改成假Retain；新ImageLease用Owner适配，禁止同一像素双重释放。'],
'Vision/Hosting/IVisionImageDisplayBridge.cs':['Controls.Wpf','Imaging/IVisionImageDisplayBridge.cs','UI适配分离','显示转换服务不由纯合同或工具定位到WPF。'],
'Vision/Hosting/VisionBackendExtractorLocator.cs':['Runtime.Engine','Vision/VisionBackendExtractorLocator.cs','重写为注入','静态定位改宿主服务实例；旧静态入口仅兼容，首期仍限制单宿主。'],
'Vision/Hosting/VisionImage.cs':['Runtime.Engine','Vision/VisionImage.cs','拆分工厂与实现','Contracts只保留图像接口/格式；工厂和引擎提取不放客户合同。'],
'Vision/Hosting/IVisionImageFactory.cs':['Contracts','Vision/IVisionImageFactory.cs','接口签名复审后迁移','实现进Engine，若成员暴露backend类型改DTO/适配；不是已证明可直接转发。'],
'Models/Geometry/HalconArcAdapter.cs':['Runtime.Engine','Vision/HalconArcAdapter.cs','后端适配迁移','Geometry目录不等于公共纯几何合同；引擎适配留内部，防止Contracts传递依赖Halcon。'],
'AssemblyInfo.cs':['Plugin.SDK','AssemblyInfo.cs','兼容原位保留','旧ThemeInfo不得删除；新Controls.Wpf新增自己的ThemeInfo。'],
'Themes/Generic.xaml':['Plugin.SDK','Themes/Generic.xaml','兼容原位保留','原SDK;component/UI/Themes/DefaultTheme.xaml仍可解析；新资源独立，不靠TypeForward修复pack URI。']
};
const internalNames=/^(ParameterResolver|RuntimeParameterSnapshotResolver|ToolExecutionPreparation|OutputVariableResolver|NodeResultsParameterContext|DataSourceQueryService|BindingInputCatalog|BindingContractValidator|GeometryExtractor|RoiComposeResolver|SubroutineDebugServiceProvider|RuntimeVariableReceiveService|CommunicationSendService|CommunicationReceiveService|FileLogWriter|VisionLogger|PluginLogger)$/;
const uiNames=/^(TreeNodeData|ArrayTreeFormatter|AvailableDataSource|ParentNodeInfo|ToolDebugResultSnapshot|SubroutineDisplayItem)$/;
const inventory=walk(base.slice(0,-1)).filter(p=>/\.(cs|xaml)$/.test(p)).sort().map(p=>{const rel=p.slice(base.length),text=fs.readFileSync(p,'utf8'),name=path.basename(p).replace(/\.xaml\.cs$|\.cs$|\.xaml$/g,''),types=[...text.matchAll(/\b(?:class|interface|struct|enum|record)\s+(\w+)/g)].map(m=>m[1]);let r=overrides[rel];if(!r){let target='Tool.SDK',action='按职责迁移，成员依赖待实施复核',why='工具作者模型/声明保留；不等于已验证ABI兼容。';if(/^(UI\/|ViewModels\/|Commands\/)/.test(rel)||uiNames.test(name)){target='Controls.Wpf';why='WPF界面/交互/展示层；纯运行链不加载。';}else if(internalNames.test(name)){target='Runtime.Engine';action='实现迁移＋服务注入整改';why='解析/服务/执行实现不进公共SDK，接口留合同侧。';}else if(/^(Vision\/Contracts\/|Devices\/|Models\/Geometry\/|Models\/Imaging\/)/.test(rel)||/^Logging\/(I|Log)/.test(rel)){target='Contracts';why='公共数据/能力接口候选；跨平台与可变对象成员须逐签名审核，不能凭目录直接移走。';}r=[target,rel,action,why];}
return{source:p,types:[...new Set(types)],target:'src/'+r[0]+'/'+r[1],project:r[0],action:r[2],reason:r[3],review:overrides[rel]?'关键文件定向复核；实施前仍需ABI测试':'全量清单归类；尚未逐成员核兼容',wpfMentions:/\bSystem\.Windows\b|PresentationFramework|BitmapSource|FrameworkElement/.test(text),sha256:crypto.createHash('sha256').update(text).digest('hex')};});
const rewrites=[
['Runtime.SDK/RuntimeHost.cs、ProjectSession.cs、FlowHandle.cs、RunHandle.cs','新增门面','现有WorkflowExecutionEngine/WorkflowRunSession/WorkflowRunScope仍是执行基础；新门面负责接纳、修订、Stop/Close可观察终态，不能简单public内部类。'],
['Contracts/Runtime/IRuntimeBackendFactory.cs、IRuntimeProjectBackend.cs','新增最小后端协议','Runtime.SDK → Contracts；Runtime.Engine实现协议，由发行composition注入；不出现SDK↔Engine循环。'],
['Contracts/Data/FlowSchema.cs、FieldSchema.cs、ResultAddress.cs、ResultQuality.cs','新增稳定外部合同','复用已有SourceFieldId与子Scope.RunId映射；不另造重复运行身份；不暴露反射属性路径为长期客户API。'],
['Runtime.Engine/Results/ResultStore.cs、ResourceOwner.cs；Runtime.SDK/ResultLease.cs、ImageLease.cs','新增Owner与lease适配','保留旧IVisionImage.Clone/Dispose；Completion不等待未来Acquire；关项目未排空保留ClosePending/Quarantined。'],
['Runtime.Engine/Plugins/LegacyToolAdapter.cs、LegacyResultAdapter.cs','新增兼容适配','旧插件继续旧参数/结果类型；新合同转换明确copy/borrow/retain，不用强转让两份程序集同名类型混用。'],
['Runtime.Engine/Persistence/ProjectMigrationService.cs、LegacyDiscriminatorMap.cs','新增版本迁移','读取旧格式→候选模型→判别符/字段/资源映射→验证→新路径原子保存；原文件不覆盖。'],
['Plugin.Infrastructure/Managers/Plugin/PluginAssemblyPolicy.cs、PluginLoadContext.cs','保留基础＋整改版本策略','当前宿主同名共享/非collectible需保留事实；增加合同版本/RID诊断、运行与Editor分开清单，清字典不声称卸载。'],
['Core/Services/Serialization/ParameterTypeRegistry.cs → Runtime.Engine/Persistence/ParameterTypeCatalog.cs','重写静态注册责任','保留旧注册入口适配，先单宿主；按目录构建冻结类型表后反序列化，注册失败不返回半方案。']
];
const chains=[
['插件加载','PluginLoader → PluginLoadContext / PluginAssemblyPolicy → ToolRegistry → ParameterTypeRegistry → 创建工具','新Engine装配Catalog及LegacyToolAdapter；旧合同唯一宿主身份；新工具只依赖Tool.SDK；Editor另清单按ToolId关联。'],
['参数和方案','WorkflowSerializationOptions → ParameterTypeRegistry.SerializationOptions → WorkflowJsonConverter → WorkflowNodeBase反序列化 / OnDeserialized','ProjectMigrationService先识别版本和全部插件，冻结Catalog后读取；SourceFieldId保持；未知$type保留诊断/拒绝，不丢字段硬加载。'],
['执行与发布','ToolExecutor → ToolExecutionPreparation.Apply → 参数解析 → 工具 → ToolResults.GetOutputValues / GetDisplayImages','Engine执行→ResultStore接管候选→发布ResultAddress；Runtime.SDK读取lease，Controls.Wpf只投影。错误候选先释放，不能默取上轮成功结果。'],
['编辑器与主题','IToolEditorFactory → Window/FrameworkElement → SDK ThemeInfo → Themes/Generic.xaml → SDK;component/UI/Themes/DefaultTheme.xaml','旧链保持兼容发行；新Controls.Wpf自有资源URI和主题声明。BAML程序集解析桥保留到旧编辑器退出支持。']
];
const stages=[
['E0 冻结基线','先不移动任何产品文件','登记工具ID、公开类型全名/程序集、$type、资源URI、现有方案样本与输出；建立旧版本可启动发行备份。','目录清单不是ABI验收；缺样本则标未通过。'],
['E1 增量新包与门面','Contracts → Tool.SDK → Runtime.SDK；Engine适配旧Workflow','先用ImageLoad/阈值做新Console路径；旧插件和旧SDK保持；单宿主/单项目能力明确。','与旧UI同输入同输出，正常/失败/停止/释放；不宣称无WPF运行时。'],
['E2 双合同插件共存','LegacyToolAdapter → 新运行插件 → 可选Editor','保持ToolId不变；同工具两版本不得同时注册；全名签名兼容才允许TypeForward，其他走显式适配。','旧二进制不重编可加载并运行；新工具不依赖旧SDK/UI，旧BAML可打开。'],
['E3 方案双读与单独写新','旧Catalog/判别符映射 → 候选验证 → 新版本另存','先读旧，再测新格式往返；不把VM .sol当自研旧方案兼容范围；删除/重命名字段要显式迁移规则。','Script / NPointCalibration / Subroutine样本往返；SourceFieldId、绑定、坐标、文件资源不丢；失败原文件字节不变。'],
['E4 内部移出与UI分离','Executor/Resolver/日志实现进Engine；Controls迁移','分批改调用者；Workflow脱WPF与Core传递依赖同时审计，CommonControl和图像显示现有控件先做适配，不整库盲迁。','新纯API发行传递闭包无旧SDK/WPF/Editor；旧兼容发行单独验。'],
['E5 切默认与退兼容','新格式/新SDK成为默认；旧支持按期限结束','保留双读迁移工具；回滚用原方案备份＋旧完整发行，新格式不能承诺旧版本直接读。','所有兼容矩阵通过才切默认；未验证资源/第三方签名的插件继续旧发行，不能直接删兼容层。']
];
const evidencePaths=['src/Plugin.Infrastructure/Managers/Plugin/PluginLoadContext.cs','src/Plugin.Infrastructure/Managers/Plugin/PluginAssemblyPolicy.cs','src/Core/Services/Serialization/ParameterTypeRegistry.cs','src/Workflow/WorkflowSerializationOptions.cs','src/Workflow/WorkflowJsonConverter.cs','src/Workflow/SunEyeVision.Workflow.csproj','Directory.Build.targets'];
const evidence=evidencePaths.map(p=>({path:p,sha256:crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex')}));
const data={projects,inventory,rewrites,chains,stages,evidence,productModified:false,plannedOnly:true};fs.writeFileSync('artifacts/validation/sdk-engineering-migration.json',JSON.stringify(data,null,2));
return{render({E,table}){const row=(headers,rows)=>table(headers,rows);const graph='<div class="sdk-dependency-graph">'+projects.map(([n,p,s,r,deps])=>'<article class="sdk-dependency-node"><b>'+E(n)+'</b><small>'+E(s)+'</small><code>'+E(p)+'</code><p>'+E(r)+'</p><span>编译依赖 → '+E(deps.join('、')||'无新增项目引用；旧兼容暂原位')+'</span></article>').join('')+'</div>';
return[
['sdk-engineering','工程与目标依赖','<h2>工程落位：新公共边界，复用执行实现</h2><p class="notice">所有新工程、路径及迁移均为待实施设计。本轮未创建工程或搬动产品文件。箭头表示编译依赖，后端注入是运行装配，不是反向ProjectReference。</p>'+graph+'<pre>客户 → Runtime.SDK → Contracts ← Runtime.Engine\n算法插件 → Tool.SDK → Contracts\n可选WPF编辑器 → Controls.Wpf → Runtime.SDK / Tool.SDK / Contracts\nRuntime.Engine → Workflow / Plugin.Infrastructure / DeviceDriver\n旧插件 → 原Plugin.SDK（单列兼容发行；新纯API链不引用）</pre><p>图中Workflow等下游最终只依赖公共边界，不反向引用Engine。现有Workflow还引用Core/Infrastructure/旧SDK且UseWPF，不能把目标图当现状。UI是最外层装配者。Engine默认后端通过发行清单/Factory注入Runtime.SDK；Tool测试宿主独立开发工具引用Runtime.SDK，不加进Tool.SDK依赖。</p><h3>跨工程实际链怎么迁</h3>'+row(['场景','当前调用链（静态定位）','目标责任'],chains)+'<h3>必须新建或重写的文件</h3>'+row(['目标文件（均待实施）','判定','具体责任及保留基础'],rewrites)],
['sdk-file-map','逐文件迁移清单','<h2>旧SDK逐文件、逐类型候选落点</h2><p>共'+inventory.length+'个当前C# / XAML文件，排除bin/obj。类名由声明扫描提取，可能含嵌套类或注释示例，权威以源码为准；不是全量成员级ABI审核。关键混合文件单列拆分，其余按职责归类，实施前仍逐成员核引用。目标路径是候选设计，不是已存在文件。</p>'+Object.entries(Object.groupBy(inventory,x=>x.project)).map(([project,items])=>'<details><summary>'+E(project)+' · '+items.length+'文件（点击逐项查看）</summary>'+row(['当前文件 / 声明','目标文件（拟议）','怎么处理','理由与复核边界'],items.map(x=>[x.source+'\n'+x.types.join(', '),x.target,x.action,x.reason+' '+x.review+(x.wpfMentions?'；检测到WPF相关词，须审核实际成员。':'')]))+'</details>').join('')+'<p>未扫描的第三方插件二进制、历史发行签名和客户方案样本不因本清单而已兼容。迁移文件不默认改namespace；新外部DTO才用新命名空间。保留全名也不自动保持程序集身份，转发仍需逐类型核对。文件SHA与目标表保存sdk-engineering-migration.json，变化后重新核对。</p>'],
['sdk-compatibility','旧插件与方案迁移','<h2>先双轨兼容，再切默认；每阶段可回滚</h2>'+row(['阶段顺序','实施范围','兼容策略','验收门禁（本轮未执行）'],stages)+'<h3>兼容不能靠一句TypeForward</h3>'+row(['对象','兼容办法','禁止误判'],[['旧插件ABI','旧Plugin.SDK保持程序集身份/签名；新边界用显式Legacy适配','全名/结构/签名变更不能万能转发；版本与签名基线尚须收集。'],['自研旧方案','保留$type别名、ToolId与SourceFieldId，先冻结注册表再读候选','不能未知类型默变Generic或覆盖原文件；VM方案格式不在承诺内。'],['旧XAML/BAML/pack URI','保留旧SDK资源路径及默认上下文桥，新控件另用新URI','TypeForward不迁移资源，移ThemeInfo会让旧样式静默失效。'],['部署/native依赖','调整Directory.Build.targets发布规则并保留插件.deps.json，按RID验包','只移动csproj不等于可发布；不以清缓存/字典当插件卸载。'],['数据/图像Owner','旧Clone/Dispose由适配器明确接收/复制策略，结果lease另实现','不将新旧类型强转或重复释放；未排空不得关闭底层资源。']])+'<h3>本轮证据入口（SHA登记；实施验收尚未运行）</h3><ul>'+evidence.map(x=>'<li><a href="file:///'+E(path.resolve(x.path).replaceAll('\\','/'))+'">'+E(x.path)+'</a> · '+E(x.sha256.slice(0,16))+'</li>').join('')+'</ul><p>验收顺序：旧二进制＋旧方案 → 新工具＋旧方案 → 旧新工具混合方案 → 迁移后新方案 → 纯API发行 → WPF嵌入发行。任何一级失败保留原发行和原文件，不能继续删除兼容层。以上均是后续实施门禁，不是本轮已运行结论。</p>']
];}};
};
