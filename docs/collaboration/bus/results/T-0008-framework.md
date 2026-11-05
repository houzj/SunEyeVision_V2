# T-0008 回执（framework）— 让日志写入失败真正被读一次

- 卡号：T-0008（`status.json` 已登记，scope：`src/UI/Features/WorkflowUI/Canvas/Adapters/NodeDisplayAdapterConfig.cs`）
- 状态：**代码 + 全量构建完成（done）**；两条运行时行为（正常启动无该 Warning / 目录不可写时出现该 Warning）由人工验收，见第五节。

## 一、做了什么

| 文件:行 | 改动 |
|---|---|
| `src/UI/Features/WorkflowUI/Canvas/Adapters/NodeDisplayAdapterConfig.cs:257-258` | `var fileWriter = new FileLogWriter(logRoot)` 改为保存到已有字段 `_fileLogWriter`（该字段此前从未使用，构建里一直报 CS0169），随后 `logger.AddWriter(_fileLogWriter)` |
| 同上 `:272-295` | 新增 `public static void ReportFileLogWriteFailure()`：取 `_fileLogWriter`（未初始化即抛 `InvalidOperationException("日志系统尚未初始化。")`，与该类 `DocumentFilesDirectory` 同一语义）→ 催一次刷新并等队列排空（线程池上等待，上限 300ms，避免 UI 同步上下文死锁、避免读到未更新的计数）→ `WriteFailureCount > 0` 时记一条 `Warning`（累计次数 + `LastWriteFailure.Message`）→ 计数为 0 时**不写任何内容** |
| `src/UI/App.xaml.cs:282` | 启动流程中 `ServiceInitializer.InitializeServices()` + `LogRuntimeIdentity()` 之后调用 `ServiceInitializer.ReportFileLogWriteFailure();` |

## 二、与上一轮落点的差异（需交代）

上一轮我曾把这段逻辑写进 `src/UI/App.xaml.cs` 的私有方法，并为此在 `src/Plugin.SDK/Logging/VisionLogger.cs` 新增 `GetWriters()` 公共快照 API 去扫出 `FileLogWriter`。三点收口：

1. 本卡 scope 就是 `NodeDisplayAdapterConfig.cs`，而 `FileLogWriter` 正是该类创建并持有的，直接读自己的写入器比为一次性用途扩日志门面的公共 API 更合理（YAGNI）。
2. 因此 **已撤回** `VisionLogger.GetWriters()` 与其配套测试（`tests/SunEyeVision.Plugin.SDK.Tests/VisionLoggerRoutingTests.cs` 恢复原状，仍是 4 个用例），日志门面公共面不变。
3. 副作用是 `_fileLogWriter` 由「从不使用的死字段」变成真正被读，构建里 4 行 `CS0169` 告警随之消失。

## 三、约束核对

| 卡内禁止项 | 结果 |
|---|---|
| 不得把 `FileLogWriter` 写入路径改为会抛 | 未改 `FileLogWriter.cs`（该文件仅保留 H-0006 已批的 `WriteFailureCount` / `LastWriteFailure`） |
| 不得新增全局单例或静态状态 | 未新增：复用的是既有的 `private static FileLogWriter? _fileLogWriter` 字段 |
| 不得扫改其它 catch | 未改任何 catch |

## 四、实测数字

| 项 | 命令 | 结果 |
|---|---|---|
| 全量 Release 构建 | `dotnet build SunEyeVision.slnx -c Release --no-incremental` | **0 错误 / 6143 警告 / 53 秒**（`.codebuddy\tmp\build-t6t7t8-final.log`） |
| 本次警告增减 | 与 `.codebuddy\tmp\build-t7t8t6.log` 逐码比对 | 唯一变化：`CS0169`（`ServiceInitializer._fileLogWriter`）72→68 行（文件被 UI 与 `_wpftmp` 两工程各编一次），**无新增警告** |
| 日志路由回归 | `dotnet test tests/SunEyeVision.Plugin.SDK.Tests/... -c Release --no-build --filter "FullyQualifiedName~Logger"` | **4 通过 / 0 失败**（撤回 API 后恢复原用例集） |

## 五、运行期验收（2026-11-05 实测）

1. **反例：通过**。本轮共起停程序 6 次（数据根分别为 `D:\SunEyeVisionData`、`_probe`、`_probe2`、`_probe_tmp`），逐个读当日日志文件（共享读），**没有任何一次出现「日志文件写入已失败」**；正常启动不被这条自检污染。
2. **正例：未验**。让日志目录不可写的那一步没做——用户当场把方向改成了「按 VM 流程拆掉数据根机制并摘除 App.ini 校验」，验收在此收口；该自检代码仍在（`NodeDisplayAdapterConfig.ReportFileLogWriteFailure`，启动期调用），若要补验按下面方法即可。

**自验方法（可照抄）**

**自验方法（可照抄）**

- 反例：
  ```powershell
  # 双击启动 Release 程序，正常使用 10 秒后关闭
  Select-String -Path "D:\SunEyeVisionData\Logs\*\*\*\SunEyeVision_*.txt" -Pattern "日志文件写入已失败"   # 期望：无输出
  ```
- 正例（人为让日志目录不可写）：
  1. 关闭程序；
  2. 把 `D:\SunEyeVisionData\Logs` 临时改名为 `Logs.bak`，并在原位新建一个**同名文件** `Logs`（无扩展名），使 `Directory.CreateDirectory("...\Logs")` 与写入都失败；
  3. 启动程序 → 期望日志面板/日志链里出现「日志文件写入已失败 N 次，最近一次原因：……；磁盘上的日志可能不完整。」；
  4. 关闭程序，删掉那个 `Logs` 文件，把 `Logs.bak` 改回 `Logs`。

## 六、登记项（未动手）

- `NodeDisplayAdapterConfig.ResolveProjectRoot()` 现为无人调用的私有方法，且注释描述的「日志根与 docs/ 平级」已随 D3 失效；属死代码清理，登记给 D5/遗留清理批次。
