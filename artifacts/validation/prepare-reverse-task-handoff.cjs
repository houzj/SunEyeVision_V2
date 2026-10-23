const fs=require('fs'),crypto=require('crypto');
const root='D:/MyWork/SunEyeVision_V2/',dir='artifacts/validation/',checkpoint=JSON.parse(fs.readFileSync(dir+'evidence-checkpoint-review-v1.json','utf8'));
const binary={frame:{path:'D:/Program Files/VisionMaster4.4.0/Applications/ModuleProxy/x64/ModuleFrame.dll',sha256:'45c57bc46f72fd1f9e0b3a6f9183ff974d14590cfd05ecf4e9fa97a4ff95ae9b'},proxy:{path:'D:/Program Files/VisionMaster4.4.0/Applications/ModuleProxy/x64/ModuleProxy.dll',sha256:'76f09a40184b2b5f99b7173edde23a716d339e3c64955696d6927495efdf68a9'},pm:{path:'D:/Program Files/VisionMaster4.4.0/Applications/Server/PlatformManager.dll',sha256:'06d86f51bebfeba357d1b79297e20a3b0a70580a40af7b77c8abdd6c38ca0624'},camera:{path:'D:/Program Files/VisionMaster4.4.0/Applications/Module(sp)/x64/Global/GlobalCameraModule/GlobalCameraModule.dll',sha256:'f61fb1063325793343139c8699aa9eba3db931a33de3869fd856700ca05cecce'},sdk:{path:'D:/Program Files/VisionMaster4.4.0/Applications/PublicFile/x64/iMVS-6000PlatformSDK.dll',sha256:'b62d7e15588ccda6671228aebc11fec3e27aca878f0e03067e44b17d5a54a9a1'}};
function refs(...names){return names.map(n=>{const path=dir+n;if(!fs.existsSync(path))throw Error(path);return path;});}
const tasks=[];
function add(id,priority,title,points,depends,binaries,chain,entry,need,accept,stop,reference){tasks.push({id,priority,title,kind:'static-code',checkpoints:points,dependsOn:depends,binaries,knownChain:chain,entryPoints:entry,requiredMaterials:need,acceptance:accept,stopCondition:stop,existingReferences:reference,status:'待接收/静态取证',returnPathHint:'在逆向项目自身独占证据目录保存，以任务ID命名并回传绝对路径和哈希；不写SunEye产品代码/HTML。'});}
add('VM-CODE-00','P0','基线、版本和地址身份认证（并行前置）',['RE-X1-04','RE-X1-05'],[],['sdk','pm','proxy','frame','camera'],
'既有托管反编译文件 → 原程序集/版本/MVID → 已用native哈希/PE导出 → 本清单函数位置；不要求重做已认证的native哈希。',
['按实际使用程序集列托管文件到原DLL映射；优先VM.Core、VM.PlatformSDKCS、VMControls.WPF和本轮新增涉及程序集。','五个native已用SHA列于公共基线；旧地址统一为首选基址0x180000000下VA/RVA，不是运行时观测。'],
['托管程序集全名/版本/MVID/SHA256、安装绝对路径、反编译来源、恢复工具版本和源文件哈希。','每个后续任务的二进制哈希、架构、首选基址、导出/符号和函数范围；版本不一致须提供地址迁移，不能套用旧RVA。'],
['实际使用的每个托管来源有可核对身份条目；无法绑定的文件逐项标未认证及影响。','每个任务入口都能追到本次载体；native版本相同可复用既有产物，版本不同不得静默覆盖。'],
'该批实际使用程序集身份清单齐备或精确列出缺件即交；不扩展认证所有无关安装文件。',refs('vm-native-export-inventory.json','vm-platformmanager-exports.json','evidence-checkpoint-definitions-v1.json'));
add('VM-CODE-01','P0','控制类型、分支/循环/Group与计数重置',['RE-W1-05'],['VM-CODE-00'],['frame'],
'FlowRun13bda0 → 128200 → dcc80 → dd330 → ffaa0 → ScheduleHelper ffd00 → 节点slot8；01dfd0构邻接，01e310读邻接，01e400改状态。已证普通计数就绪，缺完整控制类型。',
['RVA ffd00、ffaa0、01dfd0、01e310、01e400、e2720；从节点类型2/3/5的赋值/构造追真实类型，而非按数值猜。','10ae80 → 020ae0(CBaseTopology) → dcba0；源b9760、汇b97e0/b9810作为构图/汇合边界。'],
['上述相关完整函数体、直接调用者及类型字段的全部写入/读取；恢复类型到真实节点/Group/循环实例的对应。','分支命中/未命中、循环继续/退出、Group/子流程的构图/进入/结果传播/失败/取消/计数重置表。','对伪代码歧义给对应基本块反汇编，标定类型名是恢复符号还是分析命名。'],
['每种声明覆盖的控制类型能从创建/配置到分派和结束对应；前驱计数初值、递减和再次运行的重置生产者明确。','不以普通节点执行或“类型2递归”代替全部控制结构；缺某类型则保持检查点部分并列断点。'],
'先交一套完整控制类型清单和每类有限关键链；每个未知边列准确函数/字段，不追无关工具算法。',refs('vm-round4-graph-cache-trace.txt','vm-round4-owner-trace.txt','vm-round3-core-trace.txt','vm-round2-dispatch-trace.txt'));
add('VM-CODE-02','P0','线程策略、消费者数量与资源准入',['RE-W1-06'],['VM-CODE-00'],['frame'],
'15aba0按策略→fc9e0按流程ID队列或fb9d0共享队列→fc8e0→fbee0锁外执行任务；fd350→fc270/fc340仍是待认证策略。',
['RVA fc270、fc340、3f450（线程创建/装配候选须认证）；fc9e0、fb720、fc8e0、fbee0。','追manager+120策略、线程计数+4c、按ID记录队列及工作线程对象对应；检查b9c70真实业务，不把b98c0命名为资源准入。'],
['策略参数的定义/读取/更新、池创建及线程入口绑定、每队列消费者关系和扩缩/退出实现。','同模块/相机共享资源的实际准入/重入规则；若调度层不管资源，给转交给哪个实现的直接证据。'],
['能由策略配置确定共享/分队列和消费者数；证明公平性/饥饿/取消策略的代码边界，未证明则单列运行需求。','不得由按ID队列推出串行、由锁外执行推出无限并发，或由引用锁推出内容锁。'],
'策略到消费者映射及资源边界表交付即停；吞吐/公平实效转VM-RUN-01，不在静态任务中无限寻证。',refs('vm-round5-core-trace.txt','vm-round5-workers-trace.txt'));
add('VM-CODE-03','P0','Frame注册对象、最终通知消费者与ACK',['RE-W2-05','RE-X1-05'],['VM-CODE-00'],['frame','proxy','pm'],
'Frame事件12→Proxy 38050/38470→JSON c47→49e10注册+198/ctx+1a0→PM5d2de0→5d51c0→5f6e50(...811)→6203a0；只到投递边界。',
['Proxy480c0内485a5→35f60注册callback38050；追实际this创建与singleton198620关系。','PM6950d0、6203a0的完整实现/调用者和下游队列/订阅；SDK回调另一通道63c2a0不能混为Server。','Proxy49ee0读+1a8/+1b0文本通道另列，不把状态6都套成c47。'],
['实例构造/注册/销毁与指针传递、callback签名/返回值、最终接收方法、队列及线程切换/退订实现。','通知投递与消费完成的区别；如果协议没有ACK，给直接代码边界并解释实际保证，不能发明ACK。'],
['Frame传入context与Proxy注册实例同一性有证；接收、入队/直调和最终消费者边明确。','尾跳/恢复void等歧义用指令确认EAX/返回合同；完整说明迟到/取消/退订及错误传播缺口。'],
'追到一个完整完成通知的最终接收边界或精确不可继续处即交；不展开全部消息种类。',refs('vm-round5-proxy-setters-trace.txt','vm-round5-manager-callbacks-trace.txt','vm-round5-receiver-trace.txt','vm-round5-finish-notify-trace.txt'));
add('VM-CODE-04','P0','停止异常、无回调及不可取消调用的代码合同',['RE-W2-06'],['VM-CODE-00','VM-CODE-03'],['frame'],
'dd030等88；dd330退出先Set88再报告/尾回调最后Set98；尾计数0置548；普通路径已有，缺异常和无回调终态。',
['RVA dd030、dd330、de180、dcae0、6c640、108c30及其异常清理/展开区域。','追88/98/548信号所有写入、Reset/Close及等待错误路径；不是只看成功返回。'],
['完整函数范围及异常处理/展开元数据对应、每个提前返回/throw/失败等待的控制流。','停止请求、禁新任务、算法返回、尾回调/设备排空、可重跑的状态机和信号生产消费表。'],
['无回调/等待失败/线程异常时证明明确失败或终止路径；不可中断原生调用给出保证边界。','不能将Set88、IsRunning=false或一次超时返回作为资源排空；运行实效只列VM-RUN-01。'],
'正常/异常/取消出口表完整或列未恢复EH区域即交，不运行原厂业务来代替缺失代码。',refs('vm-frame-stop-leaf-trace.txt','vm-round2-events-trace.txt','vm-round3-lifetime-trace.txt','vm-round3-execute-trace.txt'));
add('VM-CODE-05','P0','原始帧生产、复用、最后读者与归还',['RE-I1-01'],['VM-CODE-00'],['camera','frame'],
'GlobalCameraModule创建/采集注册→图像回调→源像素→结果/模块发布→最终读者→归还/复用；现有9b00仅清理，不是像素生产证据。',
['从MV_CC_RegisterImageCallBackEx的非null注册实参追真正图像回调；Create1cbe0→8880，handle在this+1f0。','1a4f0已见作VM_M_SetNotifyEventCallback，不能直接当像素回调。','ImageSourceModule仅清点过导出，实际参与链须先认证实例/调用，不能仅凭安装文件名相同接入。'],
['图像回调完整实现、宽高/stride/格式/FrameId/指针字段及分配/复制/借用/池归还调用。','每层拥有者、借用期限、最后读者和驱动复用时点，含停止/丢帧/替换/错误出口。','必要SDK合同版本与相应入口，原始帧、默认缓存、传输映射、查询结果分对象画图。'],
['同一数据指针从分配到发布/保留/释放或归还可跟踪；若复制，给源/目的地址和长度生产者。','不能用CreateFileMapping或b90d0缓存分配替代原始像素源；运行复用/驱动时序转VM-RUN-01。'],
'先一相机模式/像素格式的完整链；缺SDK内部则给公开合同/边界及缺口，不扩所有厂商。',refs('vm-round5-camera-trace.txt','vm-round5-camera-life-trace.txt','vm-round5-camera-dtor-trace.txt','vm-round5-global-camera-exports.json'));
add('VM-CODE-06','P0','缓存读写、加载准入与扩展实现装配',['RE-I1-05','RE-I1-06'],['VM-CODE-00'],['frame'],
'434d0→扩展slot10或默认b9580；43120→扩展slot18或b9390；4bee0→扩展slot30或b90d0；加载经任务绑定，队列锁不覆盖执行。',
['RVA434d0、b9580、43120、b9390、4bee0、b90d0、b8560及实际写入非空module+128的位置。','加载调用47a00/66c60→70ce0→75870/78130/78440；运行节点b8740互斥+138与所有写入口关系。','e8880是DuplicateModuleNode，102f0是删除析构包装；不要沿旧BuildModuleNode/ExtendedNodeCtor标签推装配。'],
['非空+128具体类型、构造赋值及vtable slot10/18/30完整实现；默认+140表的读/写/删调用者。','运行/加载/公开写/删除的线程及共同锁/租约/状态准入；批次完成事件/计数的实际生产者。'],
['内容串行/借用合同来自真实外层或实现层，明确默认与扩展差异；无同一锁也需解释实际准入，不直接登记竞争故障。','未恢复的实现对象/失败恢复不能拿引用计数、shared_ptr或队列锁填补。'],
'先认证一个真实非空扩展实例，默认路径合同和批次信号列清即交；不逐算法全覆盖。',refs('vm-round3-follow-trace.txt','vm-round3-core-trace.txt','vm-round5-load-body-trace.txt','vm-round4-graph-cache-trace.txt','vm-round4-owner-trace.txt'));
add('VM-CODE-07','P1','已有结果类型、显示与历史消费者的所有权清单',['RE-I2-04','RE-I2-06'],['VM-CODE-00'],[],
'ModuleResultManager.SetModuleResult(default Memory_Deep_Copy)→类型转换/SharedMemoryItemVM→ModuleResultView decorator→切换/历史持有→Dispose/Unmap；我方显示缓存另作对照。',
['现有托管源码：ModuleResultManager、ModuResultMemoryTypeEnum、SharedMemoryItemVM、ImageBaseData、ModuleResultView、历史查询消费者。','自研NodeResultManager/ImageConverterPool/CachedExecutionResult仅供只读比较，不修改产品。'],
['按真实类型分支列深/浅/共享、嵌套数组/图像、借用期限、固定GCHandle和底层像素分别的拥有者/释放。','查询返回/当前显示/历史保存的实际调用者与克隆/保留/退订/释放位置；完整相关方法体和分支，不仅类定义。'],
['每个已声明覆盖类型有分配→消费者→释放或借用说明；GCHandle释放不冒充像素归还。','默认深复制实现归ModuleResultManager（不是CModuleResultBase）；历史缺证或未覆盖类型逐项列，不缩小原检查点。'],
'先复用现有托管材料完成类型/消费者表，材料已足不重复反编译；缺类型列明确清单，不无限追UI。',refs('evidence-checkpoint-review-v1.json','remaining-evidence-followup.json'));
add('VM-CODE-08','P0','映射/查询失败与并发释放的代码出口',['RE-I2-05'],['VM-CODE-00','VM-CODE-05','VM-CODE-06'],['proxy','frame'],
'发送3089/0xc11建立/复制映射74b10，释放3090/0xc12→74e00 Unmap/Close；默认缓存b96a0正常析构已证，缺全异常/并发。',
['Proxy74b10、74e00及3089完整源地址/长度生成、失败跳转；Frame434d0/b9580及异常清理。','相同view/handle字段、客户端打开/保留/释放路径及在途查询的同步生产者。'],
['Create/Map/Copy失败每个分配点的回收和返回值，重复/并发释放及查询保留的锁/引用/代次代码。','跨进程映射与算法缓冲各自生命周期，不能用服务端CloseHandle证明客户端已释放。'],
['失败/中途返回和在途释放逐分支可对应；缺运行竞争证据明确转VM-RUN-01，不从单函数无锁猜安全/故障。'],
'限定一类图像查询和其异常出口，静态失败表交付即停；多类型缺口交VM-CODE-07清单。',refs('vm-proxy-buffer-trace.txt','vm-mapping-release-trace.txt','vm-proxy-buffer-cases-trace.txt','vm-round2-finalize-trace.txt'));
add('VM-CODE-09','P0','相机退出事件生产者及驱动最终关闭',['RE-P1-05'],['VM-CODE-00','VM-CODE-05'],['camera'],
'Destroy1cf50→slot0=8cc0→8d90→9b00；9b00注销图像/异常回调，等+4a918/+4a6a0各3000ms，非零日志后仍继续清理。',
['RVA230b0、16400、1abe0及9b00；this+4a918和+4a6a0所有Create/Reset/Set/Wait/Close交叉引用。','图像回调/工作线程的创建和退出，驱动StopGrabbing/CloseDevice/DestroyHandle的实际调用，不按辅助函数名字猜。'],
['两事件对应的线程/在途计数、信号生产者，回调注销API与SDK版本的保证范围。','清理辅助函数完整实现、句柄最后访问及超时继续清理时的所有者状态。'],
['停止采集、线程/回调结束和句柄销毁顺序来自代码或版本明确合同；超时后安全边界不得仅凭等过事件确认。','若只能证明注销/等待，保持部分，列运行验证而非承诺排空。'],
'只追两事件及三个驱动清理入口的闭环或精确边界；不扩所有相机厂商。',refs('vm-round5-camera-life-trace.txt','vm-round5-camera-dtor-trace.txt','vm-round5-camera-vtables.json'));
add('VM-CODE-10','P1','特殊状态/非相机销毁与库最终退役',['RE-P1-06','RE-P2-06'],['VM-CODE-00','VM-CODE-09'],['frame','camera'],
'DeleteNode10a910→观察者slot18→b6fa0（状态门槛）→b6670条件Destroy→记录移除；记录deleter不销毁实例，GenTL卸载不是插件FreeLibrary。',
['RVA10a910、b6fa0、b6670、b6df0、b53b0、b54e0，global2b12f2及info[1]/E0000010状态生产者。','从实际库句柄记录定位FreeLibrary或常驻所有者；列非相机模块代表选择依据，先不假定与相机相同。'],
['状态定义/写入/拒绝删除分支、异常补偿、非相机代表Destroy函数及其资源关闭。','库引用、实例/在途持有者、最终句柄释放/明确常驻策略和包替换边界。'],
['说明每个状态下销毁/跳过/重试和记录处理；常驻也可作为现状结论，但不能称已热卸载。','代表材料不能升级全插件检查点，范围未全覆盖必须保留部分/未核实并列遗漏。'],
'先一个特殊状态及一个非相机代表；完整覆盖清单/未覆盖清单一并交付，不无界逐插件恢复。',refs('vm-round4-owner-trace.txt','vm-round4-release-callers-trace.txt','vm-round4-loader-life-trace.txt','vm-round3-execute-trace.txt','vm-round5-camera-dtor-trace.txt'));
add('VM-CODE-11','P1','非相机发现、依赖失败和注册回滚',['RE-P2-04'],['VM-CODE-00'],['frame'],
'非相机托管目录/ActivatorWrap/宿主→实际加载和模块工厂→注册→缺件/创建失败补偿；相机Global扫描不能代替此链。',
['优先搜索现有托管反编译的目录扫描、ActivatorWrap、配置解析、模块注册；再按真实PInvoke/导出接native b54e0。','提供真实配置/资源引用及目录布局，不用反编译csproj当原厂发布规则。'],
['一个实际非相机宿主的完整发现/依赖/创建/注册及失败解除订阅/移除/销毁方法。','配置XML/依赖清单、缺DLL/类型/契约/架构错误各自如何报告/保留或回滚。'],
['正常和失败都能关联同一注册对象/目录/实例，部分失败对其他模块影响有代码边界。','自研Loader拒绝契约不是VM回滚证据；若全非相机覆盖未齐，保留检查点部分。'],
'先已有托管材料专项，不重复整库分析；给已覆盖与待补宿主列表即交。',refs('vm-round3-execute-trace.txt','remaining-evidence-followup.json'));
const runs=[
{id:'VM-RUN-01',priority:'P0-验证候选',kind:'runtime-not-authorized-by-this-handoff',title:'取消/排空、缓冲复用和并发释放的有限运行验证',checkpoints:['RE-W1-06','RE-W2-05','RE-W2-06','RE-I1-01','RE-I1-05','RE-I2-05','RE-I2-06','RE-P1-05','RE-P1-06'],dependsOn:['VM-CODE-02','VM-CODE-03','VM-CODE-04','VM-CODE-05','VM-CODE-06','VM-CODE-08','VM-CODE-09'],requiredMaterials:['仅经授权的隔离副本/离线输入或模拟设备；版本/配置清单和独立输出目录。','普通、排队、算法中、回调中四取消时点及一个失败/超时；同一FrameId/指针/事件/线程关联，记录88/98/548而非合成一个完成点。'],acceptance:['正常/错误/取消逐边界时序和资源保留证据，不能用强杀后资源归零证明正常释放。','缺SDK/真实设备条件则不执行，保留未验证项；一次成功不推广全类型/全并发。'],stopCondition:'使用C16已定有限协议选择一个代表场景；不得据本清单自动启动原厂或硬件。'},
{id:'VM-RUN-02',priority:'P1-验证候选',kind:'runtime-not-authorized-by-this-handoff',title:'干净部署、错件及升级回滚',checkpoints:['RE-P2-05','RE-P2-06'],dependsOn:['VM-CODE-10','VM-CODE-11'],requiredMaterials:['C14已列固定5项副本验证：基线、缺依赖、契约不符、架构不符、升级失败/回滚。','原包/新包及方案配置哈希、实际加载路径、失败日志、回滚后可重开记录。'],acceptance:['在无开发PATH的隔离环境可区分错误，其他模块不被污染；回滚含方案/配置，不只进程能启动。','只能回传实际执行结果，不把完整计划写成通过。'],stopCondition:'仅待范围/运行授权确认后执行5项，不改原安装目录。'},
{id:'VM-RUN-03',priority:'P1-验证候选',kind:'runtime-not-authorized-by-this-handoff',title:'代表版本托管→native→回调→释放桥接',checkpoints:['RE-X1-06','RE-X1-05','RE-W2-05'],dependsOn:['VM-CODE-00','VM-CODE-03'],requiredMaterials:['一个离线单流程的程序集身份、加载模块/实际基址、调用/接收回调和释放记录。','探针方法/扰动说明、时钟/线程、输入输出与日志的关联ID。'],acceptance:['运行记录与已认证静态载体一致；方法token不当native地址，首选基址不当运行地址。','不能认证某边时明确断点，不将窗口或回调存在视为业务完成ACK。'],stopCondition:'仅另行授权的代表单流程，不默认全系统或真实设备。'}];
const required=['每个任务一个返回包：taskId/检查点ID/载体身份/工具与版本/来源与哈希/完整函数和类型/调用者被调者/字段所有权/正常异常取消链/结论及未恢复边界。','Managed尽量给完整反编译C#与必要CIL/资源；Native给完整函数范围和伪代码、关键间接CALL/返回/异常基本块汇编。源码级可读恢复不冒称原厂源码；分析重命名单列。','若REA提供Evidence保留原始Evidence引用/输出；独立工具产物标清来源，不冒称REA Evidence。每条结论定位到文件/方法行或模块SHA+RVA/VA。','不以函数名、导出名、目录树、截图或产品意图代替函数实现；超时/部分CFG/叶无.pdata/未解析EH需明确保留，不伪造完整实现。','静态默认；本消息不授权真实设备/原厂业务/原生探针/升级安装。运行候选按目标项目既有授权另行核对，不扩大范围。','接收任务先复用已有证据和运行中作业，映射既有主计划而非重复创建四线；只在逆向项目独占证据目录写入，不修改SunEye产品/HTML或tool-plugins在制工作。'];
const all=[...tasks,...runs];const missing=checkpoint.checkpoints.filter(x=>x.status!=='verified'&&!all.some(t=>t.checkpoints.includes(x.id)));if(missing.length)throw Error('unassigned '+missing.map(x=>x.id));
const manifest={schemaVersion:1,title:'VM逆向补充代码任务交接清单',scope:'本聊天仅整理/发送需求，不执行新逆向；接收方按既有授权静态取证，运行验证单列',analysisCommitBaseline:'3a5dc6dd54f372699a450ad7862b52c961cd2797',checkpointVersion:checkpoint.version,checkpointDefinitionSha256:checkpoint.definitionSha256,analysisPercent:100,evidencePercent:57.1,verified:24,partial:11,unverified:7,recipient:{project:'VN逆向-主程序',thread:'框架层逆向',threadId:'01a15280-de08-7361-80d8-92d9b1ddeb25',hostId:'local'},binaries:binary,requiredReturnPackage:required,firstBatch:['VM-CODE-00','VM-CODE-01','VM-CODE-03','VM-CODE-05','VM-CODE-09'],secondBatch:['VM-CODE-02','VM-CODE-04','VM-CODE-06','VM-CODE-08'],thirdBatch:['VM-CODE-07','VM-CODE-10','VM-CODE-11'],tasks:all,acceptancePolicy:'任务交付通过只表示材料符合该任务；原42检查点不改分母，达到原范围才更新状态。任务依赖是取证建议，优先与接收方现行计划合并，不中断在制工作。',summary:{staticTasks:tasks.length,runtimeCandidates:runs.length,unresolvedCheckpoints:18,checkpointsCovered:18,newReverseEngineeringExecuted:false}};
fs.writeFileSync(dir+'vm-reverse-supplement-tasks.json',JSON.stringify(manifest,null,2)+'\n');console.log(JSON.stringify(manifest.summary));
