# DivideImage（划片拆分）单项目闭口复核记录

日期：2026-11-20  
范围：划片拆分的工具结构、原厂 UI 组装边界、ROI 继承字段、算法执行、Release 和 Agent Desktop 打开/关闭。

## 原厂证据

- `docs/07-工具逆向/tools/imvsdivideimagemodu/resources.json`
- `docs/07-工具逆向/tools/imvsdivideimagemodu/parameters.json`
- `docs/07-工具逆向/decompiled/Managed/DivideImage/IMVSDivideImageModuCs/DivideImageParam.cs`
- `docs/07-工具逆向/decompiled/Managed/DivideImage/IMVSDivideImageModuCs/DivideImageRoiManager.cs`
- `docs/07-工具逆向/tools/imvsdivideimagemodu/证据补充-划片拆分托管与原生算法-20261117.md`

静态证据确认原厂有「基本参数」「运行参数」页签，基本页包含图像来源、ROI 选择/继承和位置修正，运行页包含 NumX、NumY、OverlaprateX、OverlaprateY。托管 ROI 管理器确认本工具只支持 Full/Box 形态。原生 `ProcessAlg@0x18001f510` 反编译已经给出浮点划片公式和行优先写入顺序，本方按该公式实现。像素采样取整、空 ROI 和 ROI 分支路径仍没有同版本运行证据，继续登记为未确认。

## 本方目标结构与实现

当前工具已经迁移到单项目结构：

```text
tools/SunEyeVision.DivideImage/
├─ SunEyeVision.Tool.DivideImage.csproj
├─ DivideImageTool.cs
├─ Algorithms/
├─ Parameters/
├─ Results/
├─ Editor/
└─ packages.lock.json
```

工具使用 `NativeStandardBasicPage`，由平台统一负责输入行、ROI 区域、位置修正、Assist、页签生命周期和释放。工具层只声明原厂字段身份、ROI 模式、Box 形状白名单、运行参数和结果结构。

## 本轮发现与修复

切换 ROI「按参数」时，公共 `NativeRoiGroupComposer` 按正式 Schema 查找叶字段。划片拆分工具原先传入了裸成员名 `center-x`、`width` 等，实际 Schema 声明的是 `input.roi.rectangle.center-x`、`input.roi.rectangle.width`，因此触发 `DeclaredInputFieldsView.ComposeInputEditor` 的“输入未声明”异常。

已按 Blob 已验证的 `RoiInheritanceMaterializer.LeafId` 命名边界修正：

- `input.roi.rectangle.center-x`
- `input.roi.rectangle.center-y`
- `input.roi.rectangle.width`
- `input.roi.rectangle.height`
- `input.roi.rectangle.angle-degrees`
- 对应的 `input.roi.annulus.*` 叶字段

同类裸成员映射同步修正 PixelCount、IntensityMeasure，避免下一工具重复触发同一框架异常。未修改公共控件行为，也未把工具特例泛化到所有工具。

## 验证结果

- `Check-ToolManifest.ps1 -ToolId DivideImage`：通过。工具仍标记 `AlgorithmUnconfirmed`，因为没有黄金样本数值证据。
- `dotnet test tests/SunEyeVision.Tools.ImageProcessing.Tests/SunEyeVision.Tools.ImageProcessing.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~DivideImage`：24/24 通过。
- 覆盖声明、基本页字段、单项目注册、ROI/输入端口、参数域、默认值、分片几何、重叠率、失败诊断、取消和会话释放。
- 最终 Release 构建和封存：0 错误，保留既有编译警告，当前全量 Release 载荷哈希 `5CDC77BBD3004318EA47A8EDF00AE833BDD06F008C413D0BC42C61AC9594C5BE`。
- 最终封存后 Agent Desktop 启动 `SunEyeVision.UI.exe` 进入 `SunEyeVision · 起始页`，窗口可访问，强制关闭后进程退出证据为 `process_instance_exited`。
- Agent Desktop 审计目录：`Release/_validation/AgentDesktopToolReview/divide-image-after-leaf-fix/20261120-134759-划片拆分`
- Agent Desktop 已打开 `11 划片拆分1`，基本/运行页签和四个运行参数可见，关闭流程完成。点击“确定”而没有配置图像输入时出现“配置中有无效或未完成的编辑，请检查当前输入”的参数校验提示，这是输入前置校验，不是 ROI 组装崩溃。

## 未确认项

以下项目不因本轮通过而冒充原厂事实：

1. 原厂按参数模式的像素采样/下游裁剪取整、空区域和 `Full`/`Box` 分支的动态规则。
2. `JudgmentRoi`、`RSIntersection` 在结果过滤中的实际调用边界。
3. 位置修正 `FIXTURE` 输入如何参与原生计算。
4. HALCON/OpenCvSharp 算法结果与海康自研算法的数值等价。当前仅确认原厂浮点划片公式、参数域、输出结构和会话生命周期。
5. 已保存方案在不同 ROI 继承模式之间切换后的持久化回执，需要后续带完整图像/ROI 输入的方案级复核。

## 算法公式复核

旧实现使用“向上取整 + 末片贴边”公式，这与原厂 `ProcessAlg` 的浮点写出不一致。现已按证据改为：

```text
base = extent / count
size = min(extent, (extent - base) * overlap / 100 + base)
step = size - extent * overlap / 100
start[0] = 0
start[i] = step * i
```

`DivideImageTile` 的起点、宽度和高度保持 `double`，输出层再按契约写成 Float32。非整除、50% 重叠、行优先和窄图覆盖均有 Release 测试覆盖。像素级取整仍不在工具层猜测。

## 有限经验沉淀

本轮新增的一条可复用规则已写入 `.opencode/skills/tool-production/SKILL.md`：只有在真实证据、可重复复现且至少影响两个工具时，才把经验提升为技能规则；单工具特例留在证据文件，每个闭口最多新增一条规则。这样可以复用“正式 ROI 叶字段必须使用完整 ID”的经验，同时避免无限扩张公共框架。

## Release Agent Desktop 运行探针

- 六工具精确节点名批量探针已通过，划片拆分窗口成功打开并关闭。
- 结果记录在 `docs/evidence/six-tool-agentdesktop-runtime-20261121.json`，对应运行目录为 `Release/_validation/AgentDesktopToolReview/batch-all/20261120-001241-划片拆分1`。
- 该探针确认窗口准入和关闭链路，不替代划片公式之外仍未确认的原厂运行语义。

