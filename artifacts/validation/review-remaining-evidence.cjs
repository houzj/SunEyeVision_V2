const fs=require('fs'),crypto=require('crypto');const root='artifacts/validation/',r=JSON.parse(fs.readFileSync(root+'evidence-checkpoint-review-v1.json','utf8'));
const specs={
'RE-W1-05':['现有ffd00只有类型数值与递归/提交分支，没有每个控制类型进入/退出、循环重置及异常合同。','类型2不能直接命名全部控制语义；普通节点就绪不能代证Group/循环。','有限静态补证','仅选一种生产需要的控制结构，取得类型定义、构图和重置路径再复审。'],
'RE-W1-06':['fd350调用fc270/fc340，尚未认证两函数参数及各队列消费者数；锁外消费不提供资源公平性证明。','工作线程计数存在，不等于每流程单线程或无饥饿。','静态与运行结合','先确认配置参数/线程装配；同相机争用和慢任务公平性需限定运行用例。'],
'RE-W2-05':['5f6e50成功格式化后进入6203a0，源材料未继续给出最终SDK/UI消费确认。','slot注册字段相同不自动证明Frame注册对象全程同一实例，也不证明ACK。','静态与运行结合','只追最终接收/队列合同与代次，必要时再用单次通知验证，不展开所有消息。'],
'RE-W2-06':['dd330普通尾路径及88/98先后已见，没有无回调、异常或不可取消节点的完整结束证据。','正常返回或一次等待超时不保证任务/回调排空。','限定运行验证','以离线/协作替身覆盖无回调和异常，分别记录禁新任务/结束/资源排空；本轮未执行。'],
'RE-I1-01':['相机清理体展示注销/等待/关句柄，未展示源像素缓冲分配、复用与归还。','传输映射和默认缓存是另两类对象，不能补作原始帧所有权。','静态与运行结合','需要具体相机帧回调/驱动缓冲合同和最后读者；先选一个像素格式/模式。'],
'RE-I1-05':['公开写/加载复用分派，worker执行在队列锁外；没有覆盖运行读/删除的统一内容准入。','引用计数保活对象，不保护内容；未见局部锁也不能断言必有竞争。','静态与运行结合','先明确写/读/删共同所有者和允许时机，再选一项并发替换失败用例。'],
'RE-I1-06':['构造清零+128、扩展slot10/18/30分派已见，真实非空实现类型和数据保留期限仍缺。','默认表+140的复制/析构不能推广到+128。','有限静态补证','只需一个实际启用扩展模块的装配/类型/读写合同，不能拿旧错误标签BuildModuleNode代证。'],
'RE-I2-04':['ModuleResultManager有多类型分支、浅复制及共享映射，SharedMemoryItemVM有DisposeRes；全嵌套/历史类型未逐一配对。','默认Memory_Deep_Copy是入口默认值，不等每种结果都独立持像素或全异常释放。','已有源码专项','可只读现有结果类型清单，逐类型列分配/借用/释放；未覆盖类型继续部分，不新开反编译。'],
'RE-I2-05':['74e00正常Unmap/Close可核对，材料没有中途失败、重复/并发释放和查询在途的完整合同。','单对象正常清理配对不认证全并发安全，也不证明已发生故障。','静态与运行结合','先界定一类查询失败出口/在途所有权，再用副本/替身验证；不运行原厂或设备。'],
'RE-I2-06':['换源Dispose、WriteableBitmap按尺寸复用和直接返回均已证；没有所有视图/历史读者的独立快照合同。','持BitmapSource引用不等快照；有Clone方法不证明调用处使用。','已有源码与运行','按一个历史保留消费者追引用/克隆和UI线程，再做切换/保留验证。'],
'RE-P1-05':['9b00两个等待各3000ms，非零日志后继续；两个事件生产者与注销API的在途保障仍未齐。','“注销过、等待过”不能替代“回调已退出”。','静态与运行结合','限定追两事件生产/重置及SDK合同，超时场景另需有限验证。'],
'RE-P1-06':['b6670受记录/状态条件约束，现有一个代表相机析构不能覆盖其他模块/异常删除。','标准路径达到Destroy边界不等任何删除都立即安全释放。','有限静态补证','仅选一个与产品相关的特殊状态/非相机代表，不自动恢复所有模块。'],
'RE-P2-04':['通用工厂和相机目录加载有证，非相机所有宿主/注册失败补偿未覆盖。','我方Loader拒绝不兼容与VM加载回滚不是同一条链。','已有源码专项','先从现有托管材料选择一个非相机加载宿主，形成失败补偿表；材料不足再单列。'],
'RE-P2-05':['C14已是完整5项待执行计划，但没有干净环境、错件或升级回滚实际结果。','文档100%不把未执行计划计作已核实。','限定运行验证','只能在隔离副本执行C14固定5项；本轮不运行、不修改原安装。'],
'RE-P2-06':['8d90的MV_CC_UnloadGenTLLibrary只针对条件GenTL库；完整插件FreeLibrary/引用退役未证。','常驻ALC现状与记录移除均不能证明完整热卸载。','静态与运行结合','有热更新需求才界定一个包的退役条件/回收观测，不把全系统热卸载设成文档前置。'],
'RE-X1-04':['关键native文件SHA已对应，托管源材料没有全量逐程序集安装版本/MVID认证清单。','文件存在/路径同名不等版本同一，旧反编译输出不能冒称本轮全部重做。','元数据认证','只对影响推荐的托管程序集做版本/哈希/MVID对应清单；不按现有native哈希替代。'],
'RE-X1-05':['已有尾跳/EAX/虚表/绑定调用核对，恢复签名、全部间接对象实例和异常出口仍有未知。','撤回NodeAdmission/ExtendedNodeCtor等错误分析命名后，不能以名字重新推业务。','有限静态补证','优先认证一个会改变结论的签名或间接对象边，不扩展所有函数。'],
'RE-X1-06':['所有已用native产物均注明no target execution，只有静态桥接，未认证端到端运行。','静态首选地址不是运行时观测，不将绑定或哈希当运行验证。','限定运行验证','若另行授权，选离线单流程记录版本、调用/回调/释放闭环；不碰真实设备。']};
const rows=r.checkpoints.filter(x=>x.status!=='verified').map(x=>{if(!specs[x.id])throw Error('missing '+x.id);const [finding,insufficient,needed,next]=specs[x.id];for(const q of x.references){const a=fs.readFileSync(q.path,'utf8').split(/\r?\n/);if(!a[q.line-1].includes(q.locator))throw Error('moved source '+x.id);}return {id:x.id,title:x.title,criterion:x.criterion,before:x.status,after:x.status,finding,insufficient,needed,next,references:x.references};});
if(rows.length!==18)throw Error('scope');const definitions=fs.readFileSync(root+'evidence-checkpoint-definitions-v1.json','utf8');if(crypto.createHash('sha256').update(definitions).digest('hex')!==r.definitionSha256)throw Error('definition changed');
const audit={id:'existing-evidence-followup-1',scope:'复核固定v1中11项部分/7项未核实，仅用既有材料，不继续逆向',definitionVersion:r.version,definitionSha256:r.definitionSha256,reviewed:18,statusUpgrades:0,statusDowngrades:0,verifiedDelta:0,analysisPercent:100,evidencePercent:57.1,newReverseEngineering:false,runtimeValidationExecuted:false,rows};
fs.writeFileSync(root+'remaining-evidence-followup.json',JSON.stringify(audit,null,2)+'\n');console.log(JSON.stringify({reviewed:18,upgrades:0,verified:24,partial:11,unverified:7,percent:57.1,definitionUnchanged:true}));
