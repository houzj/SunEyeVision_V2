# 门禁与命令

所有命令在仓库根目录执行。PowerShell 5.1 与 pwsh 7 均可；脚本内部按 `$PSScriptRoot` 解析路径。

## G0 清单结构自检（秒级）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .opencode/skills/tool-production/scripts/Check-ToolManifest.ps1 -ToolId <ToolId>
```

检查：工具在清单中存在、证据路径存在、项目路径存在、页签非空、`algorithm.equivalenceStatus` 合法、`goldenCases` 为空时不得声明 `equivalent`。

## G0.5 Release 前置检查（秒级）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .opencode/skills/tool-production/scripts/Check-ReleasePreflight.ps1 -StopUi
```

检查：当前仓库根、Release 程序、`release-payload.json`、HALCON 托管/native 依赖和关键清单文件可写性。清单或 `.halcon-native-deployed` 被占用时先停在环境门禁，禁止继续构建。脚本输出 `Status=blocked` 时，先关闭占用进程或修复目录权限，再重跑。

## G1 静态同构门禁

| 脚本 | 检查内容 |
|---|---|
| `scripts/verification/Verify-ToolReleaseUnitStructure.ps1` | 工具发布单元职责与程序集结构 |
| `scripts/verification/Verify-ToolThirdPartyBoundary.ps1` | 第三方算法边界（工具不得管理 HALCON 生命周期） |
| `scripts/verification/Verify-ImageSourceMappingStatic.ps1` | ImageSource 字段映射 |
| `scripts/verification/Compare-BlobArchitectureStatic.ps1` | Blob 参数分类与外部组装链接（输出 `pass` 字段） |
| `scripts/verification/Verify-BlobBasicTabStatic.ps1` | Blob 基本页组装 |
| `tools/**/Resources/*.xml` 对照 | 页签数、字段数、默认值、顺序 |

门禁失败先看它输出的布尔字段，不靠日志猜。

## G2 组装与 Runtime 探针

```powershell
dotnet run --project tests/ControlNamingProbe/ControlNamingProbe.csproj -c Release --no-build
```

通过条件：退出码 0。`ControlNamingProbe` 是所有工具的统一样板检查：

- 默认设置诊断数为 0
- Runtime 会话创建与异步释放
- 参数页 Measure/Arrange
- 原厂页签顺序断言
- 专属面板挂载断言（PixelCountRangePanel、FrameMeanCommandPanel、ColorExtract 范围面板、ContourMatch/GrayMatch/MarkFind 模板面板、BlobLabels 阈值面积面板、IfBranchPanel）
- 结果视图创建与输出声明

新增工具时在探针里增加该工具条目，不要只跑专项测试。

`tests/RuntimeEditorProductionProbe` 覆盖 24 个产品组件的静态解析和 ImageSource→Blob 生产场景；窗口构造阶段在无桌面环境会等待，命令行结果不能替代 Agent Desktop 验证。

## G3 Release 构建

```powershell
dotnet build SunEyeVision.slnx -c Release --no-restore
```

通过条件：0 错误。既有 XML 文档、可空性、xUnit 分析器警告不构成失败。

构建完成后必须重新运行 `installer/Seal-ReleasePayload.ps1`。启动前检查 `release-payload.json` 的更新时间晚于最后一个写入 Release 的程序集。载荷哈希不一致属于封存流程失败，不归因于工具 UI 或算法。

构建前确认 `Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe` 未运行，否则文件锁导致假失败。定向编译改写过程序集后必须重新封存载荷。

## G4 批量验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verification/Invoke-ToolBatchReleaseVerification.ps1 -SkipBuild
```

依次执行：ControlNamingProbe → LogicData 测试 → ImageProcessing 测试 → Inspection 测试 → 产品目录门禁（12 工具、24 组件、清单路径存在）。

产物：`Release/_validation/ToolBatchVerification/summary.json` + `logs/*.log`。

`-SkipBuild` 复用已封存载荷；源码改动后不加该参数，或先跑 G3。

## G5 Agent Desktop 运行验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/agent-desktop/Invoke-ToolBatchAgentDesktopVerification.ps1
```

行为：从 `C:\Users\Administrator\AppData\Local\SunEyeVision\Solutions\11111.solution` 复制隔离方案，只保留 12 个工具节点，逐个启动 Release、按真实 UIA 节点名打开窗口、保存快照、关闭并确认进程退出。源方案不修改。

产物：`Release/_validation/AgentDesktopToolReview/batch-all/batch-summary-exact.json`。

通过条件：12/12 工具窗口打开且快照生成、进程退出。任何工具打不开就是该工具 G5 失败，不得用其他工具通过替代。

## G6 单工具迭代

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/tool-production/Invoke-ToolProductionIteration.ps1 -ToolId <ToolId> -SkipBuild
```

执行：清单路径门禁 → （可选）Release 构建 → 批量验证，并把阶段结果写入 `Release/_validation/ToolProduction/<ToolId>-<时间戳>/iteration-report.json`。

脚本目前复用批量门禁，**不等于单工具行为验收**；单工具仍要单独看它自己的窗口快照和测试。

## 资源释放检查

窗口关闭后确认：帧租约释放、事件退订、引用解除、迟到回调拒写、程序集未被占用。批量脚本的关闭步骤覆盖前四项；后两项在改动执行链或图像源时必须手工核对。

## 通过判定

| 门禁 | 自动可判 | 备注 |
|---|---|---|
| G0 清单 | 是 | 结构错误直接阻挡 |
| G1 静态 | 是 | 以脚本布尔输出为准 |
| G2 探针 | 是 | 退出码 |
| G3 构建 | 是 | 0 错误 |
| G4 批量 | 是 | summary.json `Status=passed` |
| G5 运行 | 是（窗口级） | 不覆盖交互细节 |
| 人工视觉 | 否 | 必须单独记录 |
| 算法等价 | 否 | 需黄金样本 |

## 单工具闭口记录

一个工具完成以下项目后才可进入下一个工具：原厂页签/分组/字段/显示对象静态对照、单项目结构和注册、声明与执行测试、Release 构建与封存、目标窗口打开/关闭探针、人工视觉记录和剩余 `unconfirmed`。多个智能体可以并行处理不同工具，但每个工具都单独生成记录，不能用同批其他工具的通过结果替代。
