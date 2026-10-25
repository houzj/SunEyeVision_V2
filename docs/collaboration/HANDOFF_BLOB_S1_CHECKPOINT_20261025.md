# Blob S1 首次源码检查点（非G1通过）

- 基线提交：30d9407872c52ea647174f9ec92ac70c9bd49b87。
- 当前分支：codex/blob-s1-component-metadata；隔离路径 artifacts/worktrees/blob-s1-component-metadata-20261025。
- 当前执行者：Codex父负责人，唯一隔离集成写者；资源子包已停止写入，原分支codex/blob-s1-resource-contracts，中文临时提交06e53750及611759f4，已no-commit集入本隔离树，不合入主树。
- 任务目标：落实已批准Blob S1实际合同与兼容首门；本件只交第一增量，不宣布整门或产品通过。
- 允许修改目录：Plugin.Contracts资源/版本/编辑约束及批准metadata文件；Plugin.Runtime严格binding serializer/批准candidate catalog；Workflow持久化格式政策及其现有入口；定向案例及隔离验证设施。本包不写UI/算法/native，不改正式O06或共享治理文件。
- 已修改文件：资源与空间合同、资源常量拒绝、5.0版本/包说明、严格引用解析和显式旧binding迁移API、中立组件角色/目录候选、Solution/SolutionRepository/CompatibilityAdapter/Snapshot及三个格式/值/实际Settings列表政策、两个定向case入口。精确列表及逐SHA见artifacts/validation/blob-s1/delivery回执。
- 已完成：typed Image布局/stride/Frame/真实Domain及Region leases；neutral pose/boundary；负行列/count拒绝；格式2/读1共享版本政策；V1 scalar/alias只在candidate转换，V2拒绝旧表示；打开/列表已接共享版本门；原JSON写前重复键检查；strict binding未知/重复/坏身份拒绝；实际ParamSettingContainer.Settings列表验证，空/重复名字与缺typed动态绑定提前拒绝；metadata只校验候选整组，不加载生产插件。
- 已执行构建与测试：隔离Contracts/Runtime离线restore成功；Release构建0警告0错。可执行runner实际调用ResourceContractCases.Run及S1ContractMigrationCases.Run，两组PASS；测试源链接实际SDK ParamSetting/Container/BindingType与三个政策文件。设施故意负控exit1，正控exit0。ABI样本为Reflection.Emit独立动态程序集，不是持久PE或生产加载证明。
- 已保留失败：初次Runtime build未restore自身assets而NETSDK1004；离线restore后解决。初次runner build中间目录属性设置太晚MSB3540；移到早期Directory.Build.props后解决。原日志保留，不覆盖成通过；未发生产品GUI/弹窗。
- 人工验收结果：未执行；无用户可试用Blob包，无全量Release、native算子、VS设计时或产品独验结果。
- 已知风险或禁止改动：G1仍开放。旧binding owner映射尚未连通实际document/schema，4版表达式当前安全拒绝，不能只改format升2；完整Repository/Snapshot原盘保护链尚未执行；Workflow/Node/Device AdditionalData未知策略未全量完成；新资源接口的现有SDK/样例Host等消费者尚未适配，未做全量构建。不得把局部source-linked helper测试当完整Workflow/Sdk/发布链通过；生产SDK3.0门与Foundation5.0不同，不允许format迁移放宽旧插件ABI。
- 下一步：经理核收首检查点的源码/运行身份后继续S1真实owner映射和合法旧文档fixture、嵌套未知/字段清单与资源消费者编译适配；G1通过再并行S2事务与S3真实Blob/native/装配，首试优先Blob，不等八工具。预计首试仍60–110有效工作小时的条件区间，当前无新证据支持压缩；许可使用现加密狗直接验证，不另设许可探测阻塞。
- 复现入口：artifacts/validation/blob-s1/Runner/Runner.csproj；先核evaluated所有输出均在本隔离根，再用本地empty-source离线restore，Release --no-restore构建，然后dotnet Runner/bin/Runner.dll。BLOB_S1_NEGATIVE_CONTROL=1时应exit1，清该变量后应exit0。所有日志及manifest在同目录，避免重放其他旧frozen矩阵。
- 交接边界：首包保存点只含自有源和复现设施，无push或主树合入。共享CURRENT_TASK/队列由经理独占写，本方已把真实事实与未验边界交经理同步，不与其并行覆盖。
