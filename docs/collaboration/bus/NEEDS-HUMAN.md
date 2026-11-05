# 待人工介入清单

**这是唯一"你必须知道"的清单。** 任何人（含中心）遇到必须由人决定的点，都必须登记到本文件，同时即时推送到对话里。

## 规则

1. **三级分流**，先自判，不要什么都往上报：
   - **A 类：必须人类拍板** —— 改变用户可见行为、删除数据/文件、不可逆操作、花时间或花钱、跨模块架构取舍
   - **B 类：中心可裁决** —— 技术实现选型、命名、代码结构；只登记，不打断人
   - **C 类：只需授权** —— 启动原厂 SDK、开原机测试窗口、跑 Release 人工验收、需要你操作鼠标的点
2. **A/C 类双通道**：写进本文件（持久）+ `send_message` 给中心（即时出现在对话里）。只写文件不推送 = 人看不见；只推送不写文件 = 一轮后丢失。
3. **发完请求单继续干活**，不许停在原地等：把被阻塞的卡置 `blocked`，转去做不受影响的卡。
4. **中心每次回复末尾必须附"待你决策"小节**，只要本文件有未关闭的 A/C 项就不得省略。

## 请求单格式

```markdown
### H-0001 [A] 一句话说明要你决定什么
- 提出方：<lane>
- 卡号：T-0000
- 现象/事实：可核验的现状（不是猜测）
- 选项：
  - A：<做法> —— 影响/代价
  - B：<做法> —— 影响/代价
- 我倾向：<哪个 + 一句理由>
- 若不定：<会阻塞什么；我能否先绕开做别的>
- 状态：待决策 / 已决策（结论）
```

---

## 已决策（2026-11-05 14:20，用户点选确认）

| 编号 | 结论 | 后续动作 |
|---|---|---|
| H-0001 | **弹一次提示后继续**：数据根不存在时提示「数据根 X 不存在，已创建空目录；如需看到原有方案请检查 App.ini」，确认后正常启动，不硬拦 | 派 T-0006 给代码线 |
| H-0002 | **中心用 Git 逐条核验内容**：对全部删除文件做 blob 哈希比对，证明每个删除都有内容一致的副本 | 中心执行，结果回填本文件 |
| H-0003 | **LibraryValidator 保留、RootCanvas.Tests 删除** | 派 T-0007 给代码线 |
| H-0004 | **中心先自动预检**：用脚本跑三条反例并给出实测输出，用户只看结论 | 中心执行 |
| H-0005 | 沿用中心倾向：**暂不上独立 worktree**（只有一条代码线写 src/，无构建冲突） | 无需动作 |
| H-0006 | **纳入本批**（不还原）。但「没有消费者的可观测等于假可观测」这条批评成立，故追加要求：启动期检测 `WriteFailureCount > 0` 时记 Warning，让这两个属性真正被读 | 并入 T-0008 |
| H-0007 | **删除口径取整目录 12 文件**（不保留 `RootWorkspaceHostRuntimeTests.cs`，它被 csproj 排除、从未参与编译） | 并入 T-0007 |
| H-0009 | `OpenConfigured(configurationFile, defaultRoot)` 重载**保留**（行为与原无参版本逐字一致，且已支撑 10 条契约测试） | 无需动作 |
| H-0010 | **方案版本不符改为「完整对齐原件迁移链」**：分段升级 + 另存新文件 + 原件不动。不做"只读打开"这种简化路线 | 先取证各段升级规则，再设计，再实施 |
| H-0011 | 图像源条已能显示，但**交互逻辑未完整迁移**：选中不同节点时，条内显示的内容在原厂是不同的；我方目前只有"显示/隐藏"两个状态 | 取证前不动实现 |
| H-0012 | **本批暂不提交**，等图像源条交互补齐后再一并提交固定 | 工作区保持现状 |

## 核验结果（2026-11-05 14:30）

### H-0002 文档零丢失：已证明，无内容丢失

`git status` 中 `docs/` 的删除条目 **889** 条，逐条与 `git show HEAD:<旧路径>` 的 blob 哈希比对：

| 分类 | 数量 | 说明 |
|---|---|---|
| 存在内容完全一致的副本 | 880 | blob 哈希相同 |
| 纯重命名（内容一致） | 1 | `...架构设计方案-2026-0228.md` → `...-20260228.md`，blob 同为 `3c44177d…` |
| 同名文件内容有差异 | 8 | 逐条量化：4 个差异 0 行（仅行尾/BOM），4 个差异 2–3 行（小编辑） |
| 无任何副本 | 0 | — |

结论：**889 个删除条目全部可追溯到等价内容，无一丢失。**

### H-0004 数据根反例：已预检

用仓库外的临时工程直接编译真实 `ApplicationDataPaths.cs`，重定向 `APP_CONTEXT_BASE_DIRECTORY` 后逐例验证：

| 用例 | 实测结果 | 判定 |
|---|---|---|
| 无 `App.ini` | 走 `%LocalAppData%\SunEyeVision`，来源写明「缺省根（程序目录没有 App.ini）」 | 通过 |
| `Root=Data`（相对路径） | 抛 `InvalidDataException`「必须是绝对路径：Data」 | 通过 |
| `Root=Q:\NoSuchDir`（盘符不存在） | 抛 `InvalidDataException`「所在盘符或共享不存在」 | 通过 |
| `Root=<盘符存在但目录不存在>` | **静默创建空目录并返回成功** | 不符合预期，已派 T-0006 修为"提示后继续" |

## 未关闭项（原登记，保留备查）

### H-0001 [A] 数据根"盘符存在但目录不存在"时要不要提示
- 提出方：中心（实测发现）
- 现象：配置 `Root=D:\SunEyeVisionDat`（手误少个 a）时，程序**静默创建空目录并正常启动**，用户看到空方案列表会以为数据丢了。已用真实源码实测确认。
- 选项：
  - A：弹一次提示「数据根 X 不存在，已创建空目录；如需看到原有方案请检查 App.ini」后继续 —— 不硬拦启动
  - B：直接报错退出，要求先改对配置 —— 最严格，你自己删目录重建时也会被拦
  - C：保持现状，只在日志记一行
- 我倾向：**A**
- 若不定：不阻塞，但这条改动会一直挂着

### H-0002 [A] `docs/` 890 个文件的删除状态是否接受
- 提出方：中心
- 现象：文档线把大批文档移入 `docs/04-工具设计/架构对照/2026/10/21/archive/架构站点-20261021/`（544 个文件）。按文件名核验，仅 1 处是文件名纠错（`-2026-0228` → `-20260228`）。**尚未做内容一致性比对。**
- 选项：
  - A：接受归档迁移，等文档线交数量账
  - B：我先用 Git 逐条核验内容一致性（约 889 个文件）
- 我倾向：**B**，因为这是审计材料，删错了代价高
- 若不定：不阻塞其它工作，但文档的"零丢失"始终是未证状态

### H-0003 [A] 两处死代码是否永久删除
- 提出方：中心
- 现象：框架线未经批准删了 `LibraryValidator.cs`（255 行，编译但 0 引用）和 `tests/SunEyeVision.RootCanvas.Tests`（12 文件，不在 slnx、缺 4 个链接源文件）。我已全部还原。
- 选项：
  - A：保持还原，代码留着（`LibraryValidator` 可能是未接线在建功能）
  - B：批准永久删除（尤其 `RootCanvas.Tests` 已长期不在构建覆盖内）
- 我倾向：`LibraryValidator` 保留、`RootCanvas.Tests` 可删（烂账）
- 若不定：不阻塞

### H-0004 [C] 人工验收窗口：D1+D2 / D3+D4 的数据根改造
- 提出方：中心
- 需要你做什么：按验收清单实际跑一遍，重点三条反例——`Root=Data`（相对路径）、`Root=Q:\NoSuchDir`（本机无 Q 盘）、删掉 App.ini 走缺省根。三条都必须报错或明确降级，不许静默进主界面。
- 注意：`Z:\` 是真实存在的盘，别拿它做反例（我已踩过，误建了 `Z:\NoSuchDir`，安全删除被拒，需要你手动删）
- 状态：待决策（等你什么时候有空跑）

### H-0005 [A] 代码线要不要上独立 worktree
- 提出方：中心
- 选项：A 暂不上（我的判断：现在只有一条代码线写 `src/`，不存在构建冲突，worktree 合并成本大于收益）／B 上
- 我倾向：**A**
- 若不定：不阻塞

---

## 已关闭

| 时间 | 事项 | 结论 |
|---|---|---|
| 14:05 | 协作方式 | 改为文件总线（`bus/`） |
| 14:08 | 进度可见性 | 各线必须维护 `progress/<lane>.md` |
| 14:10 | 人工介入可见性 | 本文件 + 即时推送双通道；中心回复末尾必须附"待你决策" |

---

## 本轮新登记（framework-refactor，2026-11-05 14:45）

### H-0006 [A] `FileLogWriter` 的写入失败可观测改造：纳入本批还是还原
- 提出方：framework-refactor
- 卡号：T-0001（改动不在该卡 scope 内，属计划 B1 项与 A 类判定的冲突）
- 现象/事实：`src/Plugin.SDK/Logging/FileLogWriter.cs` 现有两处 `catch { }` 把写入失败完全吞掉。我在 B1 里按已批准计划（B1 行含 `FileLogWriter`，目录结构段写「[MODIFY] 写入失败降级必须可观测」）把它改为：新增只读属性 `WriteFailureCount`（`Interlocked.Read`）、`LastWriteFailure`，两处 `catch` 调私有 `RecordWriteFailure`。改动 +21/−4，写入路径行为不变（仍不抛）。**但你的 D5 分级把"日志系统自身隔离"列为 A 类不改**，两者冲突，我不该自行选边。
- 选项：
  - A：纳入本批 —— 收益：运行中磁盘满/文件被占用时可诊断（当前除启动期写探测外无任何出口）；代价：A 类范围扩大，新增 2 个公开只读属性，且目前**没有消费者**（等于"有出口没接线"）
  - B：还原 `FileLogWriter.cs` —— 零风险、回到完全静默；代价：运行期丢日志永远无法察觉
- 我倾向：**A**，理由：D3 只在启动期拦"日志根不可写"，运行中失败仍是黑洞；且改动不改变异常传播行为，回退成本也低。若你认为"没有消费者的可观测等于假可观测"，我接受 B，并建议另开卡把计数接到诊断面板/日志自检。
- 若不定：不阻塞其它卡；该文件保持落盘未提交，我不再改它。

### H-0007 [A] `RootCanvas.Tests` 删除执行口径（H-0003 已判"可删"，但没定怎么删）
- 提出方：framework-refactor
- 卡号：待派（原 T-0001 越界项）
- 现象/事实：H-0003 结论是 `LibraryValidator` 保留、`RootCanvas.Tests` 可删。但该目录 12 个文件里，`RootWorkspaceHostRuntimeTests.cs` 一直被 csproj `<Compile Remove>` 排除、其余 4 个测试文件 `using SunEyeVision.Workflow`（命名空间已退出）无法编译、另有 4 条指向已不存在文件的 `<Compile Include>`。删法有两种，影响不同。
- 选项：
  - A：整目录删除（12 文件）—— 干净，但丢失历史测试文本（可从 git 恢复）
  - B：只删 csproj + 4 个不可编译测试 + 4 条悬挂链接，保留 `RootWorkspaceHostRuntimeTests.cs` 待以后修复 —— 留一个"半个工程"在仓库里，仍是噪声
- 我倾向：**A**，理由：该工程不进 `slnx`、无法编译、覆盖已由 `RootCanvasHostProbe`/`RootCollectionProbe`/`ShutdownLifecycleProbe` 承担；但这次**等明确派卡再动手**，不自行执行。
- 若不定：不阻塞；这两个路径我不动。

### H-0008 [C] 两批界面/运行期验收需要人工执行
- 提出方：framework-refactor
- 卡号：T-0001 / T-0003
- 需要你做什么：
  1. T-0003：选中「图像源1」节点看图像源条（0/0 + 工具条）→ 加载 2 张图看缩略图 → 选中「Blob分析1」看条隐藏 → 快速切换 10 次看是否异常
  2. T-0001：在 `D:\SunEyeVisionData\Logs\<yyyy>\<MM>\<dd>\` 放一个 30 天前的 `.txt` 并独占锁定后启动，看日志出现「旧日志文件无法删除，已保留」Warning；再把该文件设为只读后启动，确认**不会**启动失败
- 若不定：这两批的界面/运行期结论只能是"未验"，我不能代替你点鼠标。

### H-0009 [B] `ApplicationDataPaths.OpenConfigured(configurationFile, defaultRoot)` 重载是否长期保留
- 提出方：framework-refactor（B 类，登记备查，无需打断）
- 现象：该重载为让 App.ini 解析可测而加；无参版本委托给它，缺省行为逐字不变。T-0002 卡内允许保留。另一条零产品改动路线是用 `AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", …)`（本仓已有此惯用法），但那会改进程级状态、需与 UI 测试串行。
- 我倾向：**保留**，理由：显式注入比改进程级 AppContext 更容易定位失败；若你要求回到零产品改动，我可以改为 `AppContext` 路线并删除重载。

### H-0010 [C] 正在运行的程序锁住 Release 目录，我无法构建/跑测试
- 提出方：framework-refactor
- 卡号：T-0006
- 现象/事实：`Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.Plugin.Contracts.dll`、`SunEyeVision.Foundation.dll` 被进程 `SunEyeVision.UI (PID 34612)` 锁定，构建报 `MSB3021/MSB3027`（这是文件锁，不是编译错误）。改用临时 `OutDir` 验证，编译 0 个 CS 错误，但 Release 载荷封装失败、UI 测试无法运行。
- 需要你做什么：关掉正在运行的程序（或授权我 `Stop-Process -Id 34612`）。
- 我倾向：你来关（可能是你正在做人工验收，我不该替你杀掉现场）。
- 若不定：T-0006 的 3 条单测与全量构建一直无法验证，卡停在这里；我不会自行结束进程。

### H-0011 [A] `RootCanvas.Tests` 删除会牵连 `RootCanvasHostProbe.csproj` 的悬空链接
- 提出方：framework-refactor
- 卡号：T-0007
- 现象/事实：`tests/RootCanvasHostProbe/RootCanvasHostProbe.csproj:3` 有 `<Compile Include="../SunEyeVision.RootCanvas.Tests/RootWorkspaceHostRuntimeTests.cs" Link="RootWorkspaceHostRuntimeTests.cs"/>`。T-0007 要求「确认全仓无任何 csproj 引用它，再删」——这条前置不成立。该 Probe 也不在 `SunEyeVision.slnx`，且它的另一条链接 `../SunEyeVision.UI.Tests/RootCanvasFixture.cs` 指向的文件已不存在，本来就编译不了。
- 选项：
  - A：删 `RootCanvas.Tests`（7 文件）+ 删掉 `RootCanvasHostProbe.csproj` 里两条悬空 `Compile Include` —— 干净，删除面可控
  - B：只删 7 文件，`RootCanvasHostProbe.csproj` 原样（留一条悬空链接）
  - C：连 `RootCanvasHostProbe` 工程一并删（同样不在 slnx、同样已坏）
- 我倾向：**A**，理由：达成 T-0007 目标且顺手清掉被牵连的坏引用，不比 B 多删任何"可能有用"的代码。
- 若不定：T-0007 我不动手（当前**一个文件都没删**）。

### H-0013 [A] 发布载荷完整性校验把 `App.ini` 纳入 SHA，于是「改配置 = 程序起不来」
- 提出方：framework-refactor
- 卡号：T-0006（人工验收时撞到）
- 现象/事实：按 T-0006 自验步骤把 `Release\SunEyeVision_v1.0.0\SunEyeVision\App.ini` 的 `Root` 改成不存在的目录后启动，程序直接弹「启动失败 / 模块加载不完整: Release plugin admission failed: InvalidDataException: **Payload SHA mismatch: App.ini**」，进不了主界面。
- 根因链：`installer/Seal-ReleasePayload.ps1` 扫描整个输出目录生成 `release-payload.json`，其中确实有 `{"path":"App.ini","sha256":"DE681C7A…"}`；`src/Plugin.Runtime/ReleasePayloadAuthority.cs:248` 逐文件比对 SHA，不一致即抛。
- 影响：`App.ini` 是数据根**唯一**的运维配置入口（Release 就是靠它指向 `D:\SunEyeVisionData`）。把它绑进完整性校验 = 用户改数据根后程序起不来；而 T-0006 全部价值就是「配置写错时给人看得见的提示」，这条不解决，T-0006/T-0008 的现场验收根本没法做。
- 选项：
  - A：`App.ini` 不参与载荷清单（Seal 脚本排除配置文件），其余二进制仍严格校验
  - B：清单保留条目但允许 App.ini 变更（降级为 Warning 记账）
  - C：不改，改产品约定「App.ini 由安装器生成、用户不得手改」（则 T-0006 提示场景只能靠重封包触发，运维与验收流程都要改）
- 我倾向：**A**。配置与二进制是两类东西，防篡改该针对二进制；B 会把真实失败变噪音；C 等于取消"改配置改数据根"这条路。
- 我现在怎么做的：**未改任何产品代码**。为了把验收走完，临时把 `release-payload.json` 里 App.ini 的 sha256 同步成改后文件的值（原 App.ini 备份在 `.codebuddy\tmp\App.ini.orig`），验完立刻还原 App.ini 并再同步回原值。
- 若不定：T-0006 只能停在「代码 + 单测通过、现场未验」，且用户每次改 `App.ini` 都会启动失败。
- **已裁决（2026-11-05，用户）**：取 **A**，并同时按原厂流程把整套「数据根提示」拆掉。执行结果见 `results/T-0006-framework.md`：`App.ini` 不再进 `release-payload.json`（清单 591 条，`ManifestContainsAppIni=False`）；实测**只改 App.ini、不重封包**，程序 44 秒正常进主界面、新根目录自动创建，无 SHA 报错、无提示框。


