# T-0007 回执（framework）— 删除 `tests/SunEyeVision.RootCanvas.Tests`

- 卡号：T-0007（`status.json` 已登记，口径取 **A**：删 7 文件 + 清两条悬空链接，保留 `RootCanvasHostProbe` 本体）
- 状态：**执行完成（done）**

## 一、做了什么

| 动作 | 对象 | 结果 |
|---|---|---|
| 删除 | `tests/SunEyeVision.RootCanvas.Tests/` 整目录 7 个文件 | `git status` 显示 7 条 `D`：`CurrentFormatAndResourceTests.cs`、`HostInputCoordinatorTests.cs`、`RestoreCommitBoundaryTests.cs`、`RootWorkspaceHostRuntimeTests.cs`、`ScopedNodeInfoProviderTests.cs`、`SunEyeVision.RootCanvas.Tests.csproj`、`packages.lock.json` |
| 清理悬空链接 | `tests/RootCanvasHostProbe/RootCanvasHostProbe.csproj:3` | 删除两条 `Compile Include`：`../SunEyeVision.UI.Tests/RootCanvasFixture.cs`、`../SunEyeVision.RootCanvas.Tests/RootWorkspaceHostRuntimeTests.cs`；保留 `PackageReference xunit` 与 `ProjectReference ../../src/UI/SunEyeVision.UI.csproj` |

未触碰 `src/UI/Features/WorkflowUI/Canvas/Adapters/LibraryValidator.cs`（`git status` 无该文件）。

## 二、前置核实（实测）

1. `SunEyeVision.slnx` 无 `RootCanvas.Tests` 字样；仓库内不存在 `SunEyeVision.App.slnx`。→ slnx 0 引用 ✔
2. 所有 `*.csproj/*.slnx/*.sln/*.props/*.targets` 中对 `RootCanvas.Tests` 的引用：**0 命中**（唯一曾命中的 `RootCanvasHostProbe.csproj` 已在本次清除）✔
3. 全仓剩余 `RootCanvas.Tests` 字样只出现在 `docs/`（证据文件、bus 账本与回执）——不参与构建 ✔

## 三、实测数字

| 项 | 命令 | 结果 |
|---|---|---|
| 全量 Release 构建 | `dotnet build SunEyeVision.slnx -c Release --no-incremental` | **0 错误 / 6143 警告 / 53 秒**（`.codebuddy\tmp\build-t6t7t8-final.log`） |

## 四、未验项

- `tests/RootCanvasHostProbe` 本体仍不在 `SunEyeVision.slnx`，本次只清悬空链接，**没有单独构建它**；其去留按中心口径另议。
- 该 Probe 另一处业务代码是否还能编译，本次未评估（缺 `RootCanvasFixture.cs` 的事实已随链接清除一并消失，但工程内容未审）。

## 五、自验方法（可照抄）

```powershell
cd d:\MyWork\SunEyeVision_V2
git status --short -- tests/SunEyeVision.RootCanvas.Tests     # 期望：7 条 D
git status --short -- tests/RootCanvasHostProbe               # 期望：1 条 M（csproj）
Select-String -Path tests/RootCanvasHostProbe/RootCanvasHostProbe.csproj -Pattern 'Compile Include'   # 期望：无输出
```
