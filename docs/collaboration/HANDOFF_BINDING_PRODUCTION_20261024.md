# 生产绑定计划有限交接

- 基线提交：0641c98bfaa4989b675d6e5077d47e09d3477d95。
- 当前执行者：Codex，唯一主树产品写入者；协调负责独立空合同修复包，原固定包不覆盖。
- 任务目标：原批准F02/G02/M03依赖，实际Workflow计划冻结正式声明和expected源准入身份，同一BindingPlan解析并按真实CLR属性受检物化。
- 已修改：Contracts数值物理约束及指纹/引用、Runtime编译/解析/序列化、SDK参数定义与实际物化/原Owner root授权、Workflow计划/图/请求入口、必要测试。未修改程序集载体或另建Owner注册表。
- 运行身份：显式ActivateRoot/CompleteRoot由实际宿主root所有者调用，旧root不能凭延迟Begin重新激活。每逻辑节点同一domain，scope内重试只失效本scope，根运行切换使旧attempt失效。计划逐节点准入并独立冻结expected，不从待验结果反推身份，源/消费RequestId不同。
- 绑定：typed ReferenceExpression权威，序列化保存类型/稳定字段/选择/物理约束/转换；损坏引用不回名称。AtField索引源也入真实依赖图。旧正式数值/字符串/bool声明经同编译器；未知geometry/resource旧专用adapter暂保留，不冒所有类型桥已闭环。新计划每scope重建，无文档缓存，DocumentRevision/SchemaRevision暂0，结构指纹及原scope/attempt形成冻结边界，不冒已实现编辑文档revision服务。
- 数值：语义仍Int64/Float64，物理宽度为声明约束，自动只允许安全方向，窄化受值守卫，double到integer必须显式checked；实际可写属性优先于持久化TargetType；数组先全量校验再提交。
- 构建：owned binding-production-03。全量01真实CS0012失败保留，CircleCircleMeasure WPF assets缺新增Contracts传递依赖；仅本地offline restore该项目及依赖后target0错，全量02真实0错1136警告52.29秒，不改产品csproj或全局依赖规则。
- 开发验证：最新Foundation49通过0失败0blocked，先核halconxl SHA与成熟固定包一致；SDK受影响38通过；最终真实Engine11通过（普通/坏typed、AtField双源、整数实际属性3方向、原5回归），Script准入/生命周期16通过。四处app/UItests/SDKtests/Scripttests SDK SHA同57C611414D7F777CE092C80E576845FAC31BB010B3AAABD98391AAD45C35EE5C。
- 实际程序：同owned Release工具箱探针PID52652自然exit0，4.121秒，程序前后SHA同54AFE658280C094D823797FC7518F935DDF9F56C4F3A2A4A000217641DAF56B4，ToolContract3.0/22工具。这是实际插件元信息探针，不代完整主窗人工/VS。
- 固定包：artifacts/validation/tool-framework-contract/binding-production-03，封存脚本检查后执行；source manifest为构建后实际输入快照，不冒构建前清洁提交。二进制内stamp0641与随后有限源码提交分开记录；源码归属逐文件记录，不纳无关锁和其他人的文档变动为本实现。
- 未完成：此有限包独验；指定Run公共结果/资源reader桥及按需树、资产/EditorScope、代表工具及整方案性能/生命周期、最终统一Release主窗/VS集中人工验收。继续实施，不以交接结束或冒整包PASS。
