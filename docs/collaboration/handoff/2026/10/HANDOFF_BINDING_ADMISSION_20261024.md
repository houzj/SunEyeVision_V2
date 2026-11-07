# AI 协作交接单：Guid准入、执行副本与Script监视租约有限诊断

- 基线提交：71d1dcc0（此前F01 c96ea153源eb308；M03 71d1dcc0源849d0523）。
- 当前分支：main；当前执行者：Codex唯一主树写入者。
- 任务目标：原FT-G03/F02/F03/L范围，统一原Owner.Begin Guid投影、同请求唯一执行、请求独占配置与输入释放；正式生产BindingPlan接线继续。
- 允许修改目录：本提交SDK/Contracts/Runtime/Workflow限定文件、Script消费者及对应测试和当前任务记录。
- 已完成：Guid RequestId保原Owner/root/scope/localNode；expected源身份与待验结果分开冻结；别名共享callback gate；Claim CAS后才安装request资源释放及CreateExecutionSnapshot，重复caller不Dispose首个lease；Script保原RuntimeOwner/PreparedCompilation/Admission/IsDebugMode，direct委托一次body；监视输入返回ScriptResults readonly字典，现DebugControl读取结果，上游与global租约随结果Resources转移。
- 已执行构建与测试：binding-attempt-followup-01/foundation-build02.log 4.11s零警告零错误，真实47PASS/0FAIL/0BLOCK；sdk-test-build09 4.69s零错误31警告，admission04.trx 23PASS/0FAIL/0SKIP；script-test-build08 7.93s零错误20警告，script-admission02.trx 16PASS/0FAIL/0SKIP。Script HostDebugRoot已PEReader前置实核新专用app及DLL/native存在/SHA；Foundation native副本SHA核同后才run。
- 历史：sdk最初restore资产缺Contracts引用、fixture using非IDisposable、Script接口getter/缺System.IO构建失败保留；admission01 10/12、admission03 20/21、Script首14/16均保留。OCE旧测试合同更新为仅本请求已取消且fault同token才Cancelled；getter-only判定fixture显式Populate是序列化声明，未修改全局JSON策略或冒称通用get-only复制支持。
- 固定目录：artifacts/validation/tool-framework-contract/binding-attempt-followup-01；此包仍SDK ToolContract2.1/Foundation4.0诊断；提交后source/bin静态封存绑定，构建时HEAD71+dirt，不伪称DLL stamp为后提交。接收receipt与source/binary manifests另在该固定目录。
- 必要lock：本次SDK.Tests local restore为新增Contracts/Runtime传递闭包更新其lock；alpha8项目版本采用当前版本源事实，未改version.json；其他此前既有大批lock漂移不混暂存。
- 人工验收结果：尚未进行；统一最终Release集中列一次，不能以此定向结果替代真实VS设计时/交互验收。
- 未完成：生产Workflow准入expected与同BindingPlan.Resolve/checked物化、M03物理窄类型与实际属性权威、树时效门及正式投影、资源/资产/代表工具及统一Release性能；此包不宣称它们完成。
- 下一步：封存后直送协调/独测实际有限验证，同时合入协调独立B01/B03人类有限授权3.0片并重新编译下一同源闭包。旧97f/本2.1固定目录不再写；程序集载体不在本批准范围，原S3 ALC未闭环，不扩改ALC或新carrier。

最终封存前补证：静态交叉核发现SubroutineParameters.JsonIgnore的工作流调用委托会在中央JSON副本丢失，已用其CreateExecutionSnapshot显式携带这一现有调用服务（不泛复制computed/服务/Owner）。新增实际ToolExecutor异步委托调用控；最终sdk-final-build02零错364警告8.73s、admission-final01为24/24；script-final-build02零错35警告9.22s、script-admission-final01为16/16。三个SDK副本前置SHA同BA4CB4E276F4BC3FAC9DB5A516999C3A5548897DC6C45F958FBDDA1D5B9C4E27；此前E3EA/D449差异只记preliminary，不作为固定交付。Foundation47仍使用其自有完整纯Foundation闭包；不合并不同验证组的依赖。后继提交记录额外两必要源改动，实际最后构建HEAD560330de+dirt，不冒后继commit内stamp。
