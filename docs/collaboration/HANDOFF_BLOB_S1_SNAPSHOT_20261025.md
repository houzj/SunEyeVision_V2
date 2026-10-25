# Blob S1 Snapshot 定向修复交接

- 基线提交：`57a33874ba1adb11ebfba3cc9c7c4641afbd20f2`。
- 当前分支：`codex/blob-s1-snapshot-fix`。
- 当前执行者：Codex Snapshot 子线，独立 worktree 唯一产品写者。
- 任务目标：修复 Snapshot 保存前缺少真实 binding owner/schema 验证，以及目标替换失败后中央备份已被覆盖的问题；仅 S1，不推进 S2/S3。
- 允许修改目录：本 worktree 的 SolutionSnapshot、SolutionRepository、必要候选/IO helper、隔离 Host 和本交接单。共享 CURRENT_TASK 及治理账本由 root/经理维护，本子线不写。
- 已修改文件：`src/Workflow/Persistence/SolutionSnapshot.cs`、`src/Workflow/SolutionRepository.cs`、`src/Workflow/Persistence/SolutionFileCommit.cs`；定向入口 `artifacts/validation/blob-s1-snapshot/Host/{Host.csproj,Directory.Build.props,Program.cs}`。
- 已完成：读和保存共用 ReadFrozenCandidate；从同一次捕获 JSON 恢复特殊 Devices、反序列化、调用现有真实 owner 迁移/声明编译；WriteSnapshot 也执行该门并写最终规范化 JSON，不重读源 document。
- IO 行为：同 target 进程内串行；target replace 使用同目录实际 oldTarget；中央备份独立同卷 staging/replace，跨卷只复制。中央提交失败补偿 target，并核对字节/长度/mtime。中央提交成功后清理/日志异常仅 Warning，不改成普通失败。
- 部分写入：补偿失败抛 `SolutionSaveRecoveryRequiredException`，携带 AttemptedSnapshot、TargetPath、LocalPreviousTargetPath、CentralPreviousPath、PendingPath、ObservedTargetState 与恢复指引，不把目标可能已写藏成 Repository 普通 false；保留唯一回退文件。
- 已执行构建与测试：离线 restore 必要 10 项；必要 Workflow/Repository/Blob closure Release 编译通过。首次 Host 缺 using 编译失败保留，补后成功。首次 8 PASS/1 跨卷设施失败（GetTempPath 实际 D）；第二次 C LocalTemp 权限设施失败；改用已授权 C 盘 workspace 后跨 D/C 成功与 central 锁补偿 PASS。真实竞争锁使 rollback 失败的专用异常/回退保留已 PASS。最终固定版本结果见本任务 `artifacts/validation/blob-s1-snapshot/` source/PE/log manifests 与 report，先前失败不覆盖。
- 人工验收结果：无 GUI，无用户人工验收；不是 G1 结论。
- 已知风险：不是掉电/崩溃跨两文件原子事务；外部并发修改或补偿失败须停止继续保存并显式恢复。进程内 gate 不阻止外部进程写入。
- 调用方只读评估：SolutionManager.SaveCurrentDocument catch 将异常消息送 SaveFailure，CommitSave 在 try/catch 后，因此不推进 dirty baseline；SaveSolutionDirect 与 SolutionApplicationService 也有 general catch。类型化恢复属性在这些上层被压成消息，但消息带明确目标可能已写及恢复路径。未越白名单修改这些调用方，交 root 审查是否需要后继结构化承接。
- 未完成：root 串行源审、固定 PE 独立复现与整合后的字段闭集 validator 消费；本子线不改 LegacyBindingMigration，后者集成 validator 后共享候选门自动消费。
- 下一步：root 核收保存点与 manifest 后交独测；只合入本交接列明的自有文件。未改旧冻结树、未写 UI/算法/契约/程序集加载，未提交或推送主树。
