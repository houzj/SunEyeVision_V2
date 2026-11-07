# 指定运行结果与原资源owner reader桥

- 基线：d62156ff（已合公共纯presentation源6028822d），reader依赖e029f9b8。原J02/J03/L01/F02范围，不新Manager/运行身份owner。
- 实施：AlgorithmNode在实际ToolExecutor返回及状态提交后，WorkflowBindingContext按同一正式声明冻结本轮值一次；下游同BindingPlan消费该冻结值，不重读可变结果。结果异常按本轮失败，无上一轮替代。
- SDK准确入口：DeclaredOutputSnapshot.Schema/SchemaRevision/Identity/Status/Diagnostics、AcquireReader()；DeclaredOutputReader.Values为本reader关闭前借用的InputValues，Dispose只释放原ResultResourceOwner.Retain租约。ToolResults.DeclaredOutput单次CAS发布。普通managed值复制冻结，资源生命周期仍属于原owner。
- 实际运行仓库：现ExecutionResult.DeclaredOutputs只读目录、AcquireDeclaredOutput(完整RunIdentity)。catalog在实际完成事件前捕获，包含未执行节点的同正式schema与准入identity；成功空集合有0元素而未运行无values。错误/取消保留当前已运行结果；读取历史不用IsCurrent，不会从新run替代旧run。关闭ExecutionResult后禁止新取reader；已取reader继续持有原资源，最后一个释放才回收。结果封存后不再写NodeResults。
- 验证：result-bridge-01 owned输出，sdk-build01仅测试fixture缺new()构造编译失败原记录保留，修后sdk-build02/03成功，readers02真实2通过。原owner双reader/关闭run/重复close/最后释放恰一次/错request拒绝/notrun与空集/禁止重复发布均实际SDK入口。ui-build02真实0错1184警告27.40秒；workflow-presentation02真实29通过（Engine12含历史指定run＋原生产绑定回归，已合模型17）。不是最终全量Release或完整资源图像端到端。
- 仍待：本桥当前正式schema来自已支持的CLR纯数据投影；Blob record/完整资源声明与原生实际0/1/N、三投影控件承载/显式field-element映射、代表工具/性能/集中人工VS继续实施，不把本桥局部计整树完成。binding-production-03封存不再写。
