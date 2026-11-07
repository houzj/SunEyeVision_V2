# Blob S1 增量实际回归交接（不代表G1）

- 基线提交：dd380367620eec0d92bd540e8111e1a7289fba37；主树未合入；本提交只保存验证Host及本交接单，不改变产品源码。
- 当前分支：codex/blob-s1-schema-regression；当前执行者Codex/root；隔离目录artifacts/worktrees/blob-s1-schema-regression-20261025。
- 任务目标：继续S1有效增量验证，不重放已通过8/12/19矩阵；共享治理事实交经理维护。
- 允许修改目录：本隔离验证Host、日志/清单和交接；程序集管理不在范围。
- 已修改文件：artifacts/validation/blob-s1-regression/Host下Program.cs、WorkflowCases.cs、Host.csproj、Directory.Build.props；产品PE来自冻结dd380的Host/bin，通过Reference而非ProjectReference，不重建产品。
- 已完成：八个新增动态索引/转换/未知工具用例通过；七个深层结构/变量/Device用例通过（Blob参数未知、判断项未知、变量wrapper/producer未知拒绝，规范变量和Device声明Settings字典合法）；30alias逐条对原30d940基线BlobOutputContract稳定ID核对，通过。
- 已执行构建与测试：先确认输出/obj归本隔离根，离线restore成功，固定20依赖DLL逐SHA一致；Host最后构建3项依赖冲突警告/0错误。真实native工作流：32×32单4×4白块→HALCON Blob→实际Workflow FreezeResult/Normalize/RegisterOutputResource/Publish→两Declared reader及独立pixel/Domain租约；1blob、area16、每行像素正确、Frame producer/port及Domain同capture；run/reader依次释放后剩余租约保持，最终pixel已Dispose拒读，caller输入保持。workflow-native-01.log自然exit0。Domain释放后的拒读本用例未断言，不能由Dispose调用本身推断已验。
- 保留失败：初Host漏System.IO；固定PE引用漏System.Text.Json10依赖导致CS1705；后补实际固定依赖；Workflow fixture ILogger签名及类名冲突两编译错误后修。均为验证设施编译错误，原日志保留，无产品修复迎合测试。
- 人工验收结果：本件不需用户操作，未交完整Blob/UI包；完整包后统一集中一次人工验收项。
- 已知风险或禁止改动：G1仍开放；本链是旧SDK真实Blob运行，非新Runtime producer/discovery、非物理无WPF闭包。异常/取消资源清理、减域Workflow、下游/scene帧lineage、完整Snapshot事务、Editing和中立展示投影描述/完整字段映射仍待。不得修改079/84/dd380冻结源或PE。本回归Host及当前日志/fixtures冻结，后续增量使用新目录。
- 下一步：独测使用自己的Host/输入，只引用dd380固定PE，复核新增Repository和实际资源Workflow；开发继续S1 Editing/Projection声明与完整字段清单，不提前搬入S3生产Registry/发布拆分。报告真实阶段剩余，不以本交接替代完成。

入口：dotnet <本目录>/artifacts/validation/blob-s1-regression/Host/bin/Host.dll（八新增Repository）；加deep运行七新增深层用例；加workflow运行实际native工作流。workflow运行需要仅当前进程PATH前置冻结dd380的libs/halcon/runtime/x64-win64；不改系统PATH，不做许可探测。独测不得在本冻结目录重跑或修改fixture；所有文件身份见同目录delivery/checkpoint.json及manifest。
