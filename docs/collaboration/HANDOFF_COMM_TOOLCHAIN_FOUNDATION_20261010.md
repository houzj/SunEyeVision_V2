# AI 协作交接单

## 开发期规则修订

用户明确未正式发布，不需要旧契约兼容。RequiredToolContractMinor过渡选择已移除，宿主及所有插件统一2.1；版本检查改为主次版本完全一致，旧2.0产物须按当前基线重建。以下首批实现中的旧兼容说明为历史，不能作为后续开发要求；产品版本仍未改。部署验证以修订后的全量构建为准。

本次修订76项专项通过；全量Release0错误905警告，标准UI Release0错误847警告；19个实际工具DLL元数据核验均2.1。实际Release无硬件探针退出0，产品2.0.0.8、契约2.1、Dark，证据artifacts/validation/comm-sdk-contract-20261010。SDK全套两项前述失败未处理，本轮未重跑全套。未改标定、未连用户设备、未更新Debug或其他工作区。

- 基线提交：a0f92a8732f31a2ffe20e10fcc6feb7c792a093f
- 当前分支：feature/comm
- 当前执行者：Codex，本轮唯一写入者
- 任务目标：按通信工具链计划实施P0载荷与结果基础，以及P1共用分析基础；不冒充完成四工具。
- 允许修改目录：SDK、Workflow变量转换器、UI变量显示、相关测试和文档；构建元数据最小兼容调整已评估。
- 已修改文件：新增BinaryPayload、CommunicationOutputField、CommunicationToolResults、CommunicationDataAnalysisControl及对应SDK/UI测试；更新SdkTypeVocabulary、ToolContract、GlobalVariableValueConverter、GlobalVariableManagerDialog.xaml.cs、Directory.Build.targets、类型词汇测试、P0测试命名和协作／计划／共同约定。
- 已完成：不可变整段字节载荷，单字节不解包成数值，旧Byte仍删除；稳定字段键与显示名分离；原子发布、失败或未完成不暴露业务值、数组快照隔离；模块结果契约与只读分析共用同一次结果；变量Base64保存重载、HEX只读显示。SDK能力2.1兼容旧2.0插件，新能力插件需RequiredToolContractMinor=1。产品版本未改，未同步其他工作区。
- 未完成：P0全部接口原型与执行服务注入、P1四插件注册和配置编辑器、P2算法、P3/P4收发、P5队列和P6全链路。数据分析共用控件已实现，但尚无四工具工厂接线；不能在运行程序中宣称已有完整工具。变量绑定宿主全链和设计时可见UI人工验收仍待进行。
- 已执行构建与测试：SDK/通信/类型/兼容专项76项通过，包含STA分析控件；通讯／OPC UA／相机及字节显示串行回归206项通过。最终全量Release0错误1005警告，标准UI Release0错误947警告。实际Release EXE内建无硬件探针退出0、产品2.0.0.8、契约2.1、Dark，证据artifacts/validation/comm-toolchain-p0-20261010/release-probe.json。git diff --check通过。警告数量随增量编译范围变化，不表示清理。
- 全套SDK回归：473项，471通过、2失败，不宣称全套通过。失败为RuntimeParameterSnapshotTests.ToolExecutor_CanAttachRuntimeSnapshotToRequest（测试期望Int32，而当前快照规范不提供该类型），DesignTimeInitializationContractTests.EveryToolDebugXaml_DeclaresDesignOnlyThemeDictionary（NPointVerificationDebugControl.xaml缺字典）。相关实现和标定控件本轮未修改；未在独立干净基线重跑，不把未验证的基线归因当作已证明事实。未绕过或删除这两项测试。
- 人工验收结果：未连接用户实际设备；仅运行无硬件启动探针和自动控件测试，不能替代完整应用业务、明暗主题、可见VS设计器或高DPI人工验收。未更新dev/Debug，没有要求关闭或结束用户进程。
- 已知风险或禁止改动：共享SDK2.1需要主项目后续有范围同步，不自动修改主工作区；旧宿主不能加载要求2.1的新插件，后续通信工程要显式声明；保存为Binary的方案旧SDK不能识别，不能迁移为HEX字符串；队列和读取时机继续按已确认范围推进。保留此前所有未提交方案，不提交或推送，不创建无意义临时提交。
- 下一步：完成执行期通信服务契约和注入，然后建立四个真实插件及配置UI／输出声明，再做纯协议解析与组装。共享标定相关失败需协同确认处理，不在通信分支擅改标定工具。
