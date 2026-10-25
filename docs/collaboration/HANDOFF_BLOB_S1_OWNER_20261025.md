# Blob S1 真实文档 owner 与图像桥集成检查点

- 基线提交：0799918373bb5f00ac5a00f30591a7e79463a84e；图像桥来源84d88634430a54e6d141e4bb0e02ceef7e4a1a31，六源原样集入；未合并主树、未推送。
- 当前分支：codex/blob-s1-owner-migration；隔离目录artifacts/worktrees/blob-s1-owner-migration-20261025。
- 当前执行者：Codex父负责人为本目录唯一写者；桥接子树已冻结。共享CURRENT_TASK与队列由交付经理维护，本交接事实已同步经理。
- 任务目标：在批准S1内连接真实Repository的旧引用显式映射、当前格式语义校验和图像资源消费者编译接缝；不是完整G1通过。
- 允许修改目录：六项图像桥源、Workflow持久化政策/Repository/结果冻结接缝、隔离验证Host及本交接单。
- 已修改文件：完整文件和SHA见artifacts/validation/blob-s1-owner/delivery/source-manifest.json；不修改Blob算法、UI、生产SDK插件发现门或程序集管理策略。
- 已完成：format1内已知binding4通过实际workflow owner、静态输出声明和批准30项Blob别名表映射到binding5；当前format2也核对owner/schema，禁止旧4和name别名；引用类型/storage、冗余身份、依赖环编译校验；Workflow/Node/Device/Variable结构层未知字段拒绝；顶层Image捕获使用实际准入身份并注册结果资源owner。
- 已执行构建与测试：离线restore；受影响Workflow真实Release构建0错误/360警告，后续owner重编0错误/101警告，最后仅Host增量0警告0错误。真实Repository使用实际Blob静态声明和SDK目录，旧读迁移、当前读、两次原子Save/备份、双workflow同nodeId成功；12负例拒绝且输入bytes/mtime不变。未把注册旧SDK目录当新Runtime producer发现验收。Host源码可供独测了解API，但独测应只引用固定PE、使用自己的输入。
- 保留失败：bridge前BorrowedImage四项接口缺失导致编译失败，集入批准桥后解决；Host首轮构造器/using错误保留；未知Workflow/Node两个负例首轮FAIL，补结构合法字段门后通过。日志独立留存，没有覆盖成通过。
- 人工验收结果：尚无本包用户人工验收；无完整Blob可试包、VS设计时/运行时或统一Release完成结论。人工项待首个完整包集中提交一次，不让用户验证开发中间Host。
- 已知风险或禁止改动：G1开放。参数/变量/Device深嵌套完整清单、Snapshot完整事务链、真实资源Workflow执行链、持久ABI4 PE admission、新组件目录与SDK3/Registry/PluginManager接线、native provider、完整Blob编辑/UI/Release仍待完成。仅支持当前可声明的类型和显式映射，不推断schema转换；未有typed expression的旧动态引用仍安全拒绝，不能宣称所有历史方案已兼容。Sequence<Image>多帧provenance、外部Unwrap并发写图像未闭合。旧079与84源码/PE/历史日志保持冻结。程序集管理等主控，不扩范围。
- 下一步：独测接收本固定commit/PE/入口，运行自有Repository用例；开发在新工作目录继续S1尚未完成的合法字段/持久化和真实资源调用链。经理据独验结果决定G1，随后依原计划进入后续阶段。继续推进到验收，不以发交接单代替完成。

## 实际API与复现范围

Host源码：artifacts/validation/blob-s1-owner/Host/Program.cs；早期输出隔离属性见同目录Directory.Build.props和Host.csproj。其Main仅在专用进程内ToolRegistry.RegisterTool(typeof(BlobAnalysisTool))及PluginLoader.MarkAsLoaded，属于旧SDK真实类型声明目录初始化，不证明生产发现。

真实入口：new SolutionRepository(recoveryDirectory)；LoadCandidate(path)返回Candidate/Diagnostics且不激活UI；Save(candidate,newPath)；GetRecoveryPath(path)取得上次保存备份。解决方案按WorkflowSerializationOptions.Default序列化；节点使用AlgorithmNode(id,name,displayName,new BlobAnalysisTool())并设置ToolType="BlobAnalysis"与BlobAnalysisParameters。合法示例均保留于Host/bin/fixtures；输入含实际ParamSettings.Settings列表，输出来自BlobOutputContract.Describe()，字段和numeric storage由BindingClrAdapter.DescribeOutput得到。不得据此虚构端口。

本包采用本地缓存离线restore（NuGetAudit=false），编译命令：dotnet build artifacts/validation/blob-s1-owner/Host/Host.csproj -c Release --no-restore -p:NuGetAudit=false -p:ReferencePath=<本隔离树>/libs/halcon/net -v:minimal。执行dotnet artifacts/validation/blob-s1-owner/Host/bin/Host.dll；所有fixtures写入本隔离Host/bin/fixtures。本包实际执行已结束，无UI窗口或持续占用。接收方自行复制固定输入后运行自己的PE引用Host，避免修改生产者冻结目录。
