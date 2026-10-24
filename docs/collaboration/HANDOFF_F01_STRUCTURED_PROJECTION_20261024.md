# AI 协作交接单 — FT-F01/F03 结构值只读投影（关联 E01）

- 基线提交：97f1326c42dbfe75fb516731be9df0ec76ed2da7。
- 当前分支：codex/f01-structured-projection。
- 当前执行者：Codex 独立切片实施者。
- 任务目标：仅为冻结 Contracts 的 ConstantSetting/SequenceValue/RecordValue 提供声明驱动只读文本投影。
- 允许修改目录：独占 worktree C:/Users/houzhongjie/.codex/worktrees/f01-structured-projection/SunEyeVision_V2。产品仅 src/Plugin.Wpf/ParameterEditorControl.cs；测试仅 samples/PluginFoundation/Verification/StructuredProjectionCases.cs。
- 已修改文件：上述产品/测试与本交接；CURRENT_TASK 仅更新本独占 worktree、不纳合并提交，避免主目录协作日志冲突。
- 已完成：Record 按 Declaration.Fields/Port.Id 顺序，Sequence 按 index 递归；missing、null、empty 序列/记录/字符串明确区分，数字及索引 InvariantCulture，字符串经 JSON 转义。嵌套 Float64 的冻结旧 double[] 载体同样只读投影。Geometry/Resource 仅声明标签，不访问 payload、不反射任意对象。结构与引用分支 IsReadOnly/Commit.IsEnabled 及两个事件入口均设门；Refresh 不调用 SetText、不写 Committed。原普通 scalar SetText/TryCommit 校验保留。
- 跨模块影响：只读消费冻结纯 Contracts，未修改 Contracts/SDK/UI宿主/Workflow/序列化/构建规则；无主目录新 Guid/Binding API 依赖。
- 未完成：本切片不包含结构写回、完整 E/F 能力、七交互、生产控件替换；Program 最终单点注册由父实施者合入；完整 Release/UI/Visual Studio 真实设计时与集中用户人工验收由集成阶段执行。
- 已执行构建与测试：所有输出/obj 属本 worktree。八项目 dotnet msbuild -p:Configuration=Release -getProperty:TargetPath,OutDir,IntermediateOutputPath,MSBuildProjectExtensionsPath,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,PackagingRoot -getItem:ProjectReference 已实际核对；每个 DirectoryBuildTargetsPath 均为 worktree 内空 targets，无共享部署目标。首 Wpf --no-restore 构建 NETSDK1004（新 worktree 缺 assets）保留原日志，定因后 dotnet restore samples/PluginFoundation/Verification/SunEyeVision.Plugin.Foundation.Verification.csproj --locked-mode --ignore-failed-sources 成功，无 locks 修改。随后 dotnet build samples/PluginFoundation/Verification/SunEyeVision.Plugin.Foundation.Verification.csproj -c Release --no-restore 成功，0 warnings/0 errors，含 Wpf/Contracts/Runtime/Kernels/Samples/Host/HalconSample。
- 真实运行：先 Parser.ParseFile 完整 run-verification.ps1 通过，再核 runtimeconfig net9.0 及文件完整性；本 worktree 自有 dotnet 验证进程 PID32316，2026-10-24T02:28:57.5388273Z，60s 有界，exit0/timedOut=false，47 passed/0 failed/0 blocked。新增4项均由真实 STA WPF 控件 TextChanged/Click/Refresh 验证；含原真实 HALCON 用例通过。运行前后 Verification DLL SHA 均 F9716A9E4C42E4376A5BAD4AC122590AA8B32732AEE25E707F2B294CF6647E50。
- 临时验证输入：Program.cs 在 await StructuredCases(); 前临时加入且仅加入 await StructuredProjectionCases();，验证后已还原，未纳提交。精确补丁与带该行的实际源码哈希见 temporary-program-registration.patch / validation-input-manifest.json。父实施者应合入该唯一调用；单独 cherry-pick 测试文件不会自动执行新增4项。
- Vendor：worktree 现有 halcondotnetxl.dll SHA A1C8C3EF74F37F751E5CFB08A513B68F36A75511D7B4F9FD4E5A532E45652C57 与批准冻结 binding-b2-regression/build/app 一致；仅从该冻结包复制 halconxl.dll、libiomp5md.dll、libcrypto-3-x64.dll、libssl-3-x64.dll、libcurl.dll 到本 worktree Verification 输出，各自复制前后 SHA 相等。不取主目录 live 产品 DLL，不写主目录/旧冻结包。
- 证据目录：本 worktree artifacts/validation/f01-structured-projection/。含八项目 *-evaluation.json、wpf-build.log（首次缺assets）、restore.log、verification-build.log、run-verification.ps1、verification-stdout.log、verification-stderr.log、verification-report.json、runtime-receipt.json、vendor-receipt.json、validation-input-manifest.json、runtime-binary-manifest.json、temporary-program-registration.patch。
- 人工验收结果：未执行用户人工和真实 VS 设计时；不能把 STA 控件运行代替设计时验收。
- 已知风险或禁止改动：仅文本投影，未加入虚拟化或大数据交互；任意对象不递归，不访问图像/模型 payload；不改未知文件，不 push。SetText 未调用以封闭事件门及草稿哨兵/无效草稿状态未变验证，没有修改 sealed session 来插桩。
- 下一步：父实施者 cherry-pick 本切片，合入单注册点，再在统一身份下执行集成/独验/集中人工验收。不要将47项局部开发验证等同整包 E/F/七交互完成。
