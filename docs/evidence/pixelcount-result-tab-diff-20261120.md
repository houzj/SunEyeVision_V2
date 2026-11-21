# PixelCount「结果显示」页与原厂差异对照（2026-11-20）

## 取证范围

| 侧 | 材料 |
|---|---|
| 原厂 | `C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Measurement\IMVSPixelCountModuVA\IMVSPixelCountModuVAAlgorithmTab.xml:54-255`（ResultShow 页）、`...\IMVSPixelCountModuVADisplay.xml:1-74`、`...\Group.xml:2767-2901`（TextDisplayGroup）、`IMVSPixelCountModuVAControl.dll`（ilspycmd 11.2 反编译） |
| 本方 | `tools/SunEyeVision.PixelCount/Parameters/PixelCountParameters.xml:38-45`、`Results/PixelCountResultDisplay.cs`、`Results/PixelCountOutput.cs:68-88`、`Editor/PixelCountDisplayUi.cs`、`Resources/PixelCountDisplay.xml`、`src/Plugin.Wpf/Presenters/Display/*`、`src/UI/Services/Images/ResultWorkspacePresenter.Display.cs:136-210` |

工具说明：本会话没有注册 REA MCP，但本机已装 REA 6.9.0（`E:\SunEyeVision_V2\.tools` 配套 Ghidra 12.x 与 JDK 25），已通过 CLI 实际调用：

- `rea inspect-managed-artifact` 确认 `IMVSPixelCountModuVAControl.dll` 为托管 .NET Framework 4.6.1（MVID `f40f896e-5523-46b8-b9b4-08501027e349`，SHA-256 `1b0e731c…2ef0b1`），`IMVSPixelCountModuVA.dll` 为 **原生 x64、无 CLI 头**（需走 Ghidra 原生路线）。
- C# 反编译使用 `ilspycmd 11.2.0`。

**没有**做原厂运行窗口取证，因此行高、间距、悬浮等纯视觉效果列为未确认。

反编译结论：`IMVSPixelCountModuVAControl.dll` 只有 `PixelModuRangeConfigList`、`PixelModuRangeConfigItem`、`RangeConfigListPanel` 三个业务类型，资源只有 `ui/rangeconfiglistpanel.baml`、`datatemplate/paramusercontroldatatemplate.baml`、`themes/generic.baml`。**该工具没有专属结果页控件**，结果页完全由公共框架按 AlgorithmTab/Display XML 组装。本方把结果页做成公共声明链的方向与原厂一致。

## 差异清单

### 1. 结果判定区（已证实一致）

| 项 | 原厂 | 本方 | 结论 |
|---|---|---|---|
| 数量判断 | `CountLimitEnable` 默认 False，False 时隐藏范围 | `CountLimitEnabled` 默认 False，`visibleWhen` 隐藏 | 一致 |
| 数量范围 | `IntegerBettween` 0..999999999，low 0 / high 999999999 | 两个 Integer 0..999999999，`pairWith` 成对 | 可见行为一致；参数身份不同（原厂是复合项 + 两个子项，本方是两个独立字段） |
| 比率判断 | `RatioLimitEnable` 默认 False | `RatioLimitEnabled` 默认 False | 一致 |
| 比率范围 | `FloatBettween` 0..1，步长 0.01，low 0 / high 1 | 两个 Number 0..1，步长 0.01 | 同上 |

### 2. 图像显示区（存在结构差异）

| 项 | 原厂 | 本方 | 状态 |
|---|---|---|---|
| 行数 | 4 个 `ImageViewConfig`：ROI、Fixtured Point、Unfixtured Point、ResutlText | 3 个图形组：检测区域、基准点、运行点 | **已证实差异** |
| 文本行归属 | `ResutlText` 行在「图像显示」类，`PanVisible=False`（无样式弹层），只有显隐 | 文本显隐在「文本显示」区 | **已证实差异** |
| Fixtured/Unfixtured | 行存在，`CustomVisible=False`，触发器把 `IsDisplay` 同步给显示对象；位置修正启用后可见 | 声明了外观字段（`PixelCountResultDisplay.cs:153-154`），但 `PixelCountOutput.Ports` 没有点位输出，几何分组无从产生，实际不会渲染 | **已证实缺口** |
| 颜色/线宽来源 | `Display.xml` 固定值 + 显示对象属性（BridgeObj） | 工具参数字段持久化（`display.<group>.ok/ng/opacity/width`） | **已证实差异**（可见效果相同，数据来源与保存键不同） |
| 行交互 | 眼睛按钮切 `IsDisplay`，样式弹层改 OK/NG 色、透明度、粗细 | `NativeGraphicStylePresenter`：同一模板、同一角色 | 一致（模板 `GraphicStyle.xaml` 已迁移） |
| 默认色 | ROI `#0000ff` 线宽 2；Fixture `#66ff00`/`#ff0000`；Unfixtured 的 ok 也是 `#ff0000` | ROI `(0,0,255)` 线宽 2；基准点 `#66ff00`；运行点 ok 用 `(255,0,0)` | 一致 |

### 3. 文本显示区（语义基本一致，默认值与角色不同）

原厂 `TextDisplayGroup`（`Group.xml:2767-2901`）字段：`Content`、`OKColor`、`NGColor`、`CharacterSize`（6..72，步长 1）、`Transparency`（0..1，步长 0.1）、`PositionX`、`PositionY`。

| 项 | 原厂 | 本方 | 状态 |
|---|---|---|---|
| 字段集合 | 7 项 | 8 行（visible + 7） | **已证实差异**（多一个显隐行，位置也不同） |
| Content 默认 | 组内默认空；`Display.xml` 模板 `ModuleStatus:{0}PixelNum:{1} ,Rate{2}` | 默认 `模块状态:{0} 像素数:{1} 比率:{2}` | **已证实差异**：实际显示字符串不同 |
| 字号 | 6..72，步长 1 | 6..72，步长 1 | 一致 |
| 透明度 | 0..1，步长 0.1 | 0..1，步长 0.1 | 一致 |
| 位置 X/Y | `ButtonSelecter`，从 int/float 变量中选择，无默认值 | 数值字段，`Constant|Reference`，默认常量 0 | **已证实差异**：原厂是引用选择器；本方允许常量且默认 0 |
| 角度 | 显示对象有 `Angle=0`，参数页无对应行 | 无角度字段 | 一致（都不可编辑） |

### 4. Display.xml 对象清单（本方是残件且未被消费）

原厂 8 个对象：`InImage`、`Show Text`、`ROI`、`Fixtured Point`、`Unfixtured Point`、`Data Record`、`Result List`、`OutputMask`。

本方 `Resources/PixelCountDisplay.xml` 只有 4 项（Table rows、Overlay roi、Overlay mask、Text judgment），缺 5 个对象；且全仓库没有任何代码读取它（只作为嵌入资源存在）。实际显示由 `ResultDisplayDescriptor` + `ResultDisplayAppearance` 驱动。

结论：该文件目前是说明性残件，不是运行合同，也不参与任何静态门禁（`Verify-ToolReleaseUnitStructure.ps1` 只覆盖 ImageSource 与 Blob）。

### 5. 结果数据与叠加

| 项 | 原厂 | 本方 | 状态 |
|---|---|---|---|
| 数据身份 | `Count[]`、`Ratio[]` 数组，`Result List` 列为 `Count,Ratio` | 额外声明 `output.rows`（区间序号/阈值/计数/比率/矩形/区域/判定） | **已证实差异**（自研补充，已在 `PixelCountOutput.cs:9-10` 注明） |
| ROI 判定色 | ROI `Status` 映射 `ModuStatus`，NG 变红 | 矩形按 `passed` 取 ok/ng（`ResultWorkspacePresenter.Display.cs:186`） | 行为一致 |
| ROI 标注 | Tips 模板 `CenterX:..,CenterY:.. Width:.. Height:.. Angle:..` | `ShowMeasurements=true`，具体文本格式未对照 | **未确认** |
| 输出掩膜 | `OutputMask` 作为图像对象显示 | 输出端口有 `output.mask`，但显示描述符没有 mask 项，`PixelCountDisplay.xml` 的 Overlay 也未被消费 | **已证实缺口**（结果窗口不叠加掩膜） |
| 输入图像 | `InImage` 对象 | 结果工作区固定以当前帧为背景 | 视为等价，**未确认** |

### 6. 分组身份

原厂页签 `ResultShow`，类目 `Tab_Check Result`、`Tab_Image Display`、`文本显示`。本方只有结果判定组写了 `vmCategory="ResultShow"`；图像显示与文本显示字段没有 `vmCategory`/类目身份，静态映射无法区分 `Tab_Image Display` 与 `TextDisplayGroup`。

## 影响与优先级

| 优先级 | 差异 | 影响 |
|---|---|---|
| P1 | 文本显示行的归属与数量（原厂 4+7，本方 3+8） | 页签结构不同构，静态门禁无法通过逐行对照 |
| P1 | Fixtured/Unfixtured 有配置行但无输出数据 | 用户可编辑却永远不会显示；原厂默认隐藏、启用位置修正后出现 |
| P1 | 文本默认内容中英文不同 | 运行时叠加文字与原厂不同 |
| P2 | PositionX/Y 常量能力与默认 0 | 原厂只能引用变量；本方默认把文字钉在 (0,0)，行为可能与原厂默认位置不同 |
| P2 | Display.xml 残件、未消费、无门禁 | 无法用 Display.xml 做对象清单对照；缺 InImage/两点/Data Record/Result List/掩膜 |
| P2 | 输出掩膜未叠加 | 结果窗口少一个原厂显示对象 |
| P3 | 结果判定使用两个独立字段而非复合项 | 保存格式/参数身份与原厂不同，不影响可见行为 |
| P3 | 图像/文本显示组缺类目身份 | 追溯性缺口 |

## 未确认项

1. 原厂结果页实际行高、间距、图标与悬浮效果（无运行截图）。
2. `TextDisplayGroup.Content` 的实际初值：是空串还是 Display.xml 的模板（配置两处不一致）。
3. 原厂 PositionX/Y 是否允许手输常量（`ButtonSelecter` 证据只显示变量选择）。
4. ROI Tips 标注文本的具体格式。
5. `InImage` 对象与结果工作区背景是否完全等价。

## 运行取证尝试（Agent Desktop 1.3.1）

已用 Agent Desktop 对运行中的 VisionMaster 4.4.0（PID 13444，方案 `C:\Users\Administrator\Desktop\test.sol`）取快照：

- 主窗口「当前结果」表格列已确认：`序号`、`像素数量`、`比率`，与 Display.xml 的 `Result List` 列 `Count,Ratio` 对应；像素统计节点已执行过一次，输出掩膜为当前显示图像。
- 画布/流程节点在 UIA 中暴露为无稳定标识的行：右键语义操作返回 `POLICY_DENIED`，物理双击未打开模块参数窗口，`list-windows` 始终只有主窗口。这与 2026-11-18 记录的「节点无名重复 UIA row」问题一致。
- 因此原厂「结果显示」页的逐行视觉证据仍未取得。需要一次人工打开参数窗口（双击画布节点），或在取得稳定 UIA 标识后再由 Agent Desktop 自动采集。

## 2026-11-20 修复：图像显示行标题

**问题**：本方结果页「图像显示」三行全部显示为「检测区域」。`PixelCountResultDisplay` 把 `DialogTitle` 写死为 `"检测区域"`，而 `NativeGraphicStylePresenter` 用 `Layout.DialogTitle` 作为行标题，导致基准点、运行点丢失名称。

**修复**：`tools/SunEyeVision.PixelCount/Results/PixelCountResultDisplay.cs` 的 `Add` 增加 `dialogTitle` 参数，`GraphicFields` 按组传入原厂显示名（检测区域 / 基准点 / 运行点），不再写死。

**验证**：

- `dotnet build tools/SunEyeVision.PixelCount/SunEyeVision.Tool.PixelCount.csproj -c Release`：0 错误。
- 重新封存 Release 载荷：`70DA7F777D3E96F89B15E2377948FAC7432FA8501141B84F6BB8E365F53DC281`。
- Release 启动后打开「8 像素统计1」结果显示页，三行标题为 `检测区域`、`基准点`、`运行点`；截图 `Release/_validation/AgentDesktopToolReview/pixelcount-result-self/pixelcount-result-fixed.png`。
- `PixelCount` 测试 34/36：2 项失败为工作区未提交的 PixelCount 单项目/ROI 输入迁移导致（测试仍按 18 个显示字段和旧输入集合断言，当前为 23 个且包含 `input.roi.*`），不是本次标题修改引入。

**仍未做**：文本行归属（原厂在「图像显示」内，本方在「文本显示」）、文本默认内容中英文、点位输出数据源、Display.xml 对象清单。

## 2026-11-20 修复二：文本可见行归入「图像显示」

**问题**：原厂 `ResultShow` 页的「文本」是 `Tab_Image Display` 的第 4 个 `ImageViewConfig`（只有眼睛、没有样式笔），「文本显示」组从「内容」开始。本方把文本可见行放在「文本显示」，导致图像显示 3 行、文本显示 8 行。

**修复**：`PixelCountResultDisplay` 中 `display.text.visible` 的 Section 改为「图像显示」。视觉组由 `VisibilityGroups` 声明，仍走眼睛-only 行，与 `PanVisible=False` 对应。

**验证**：Release 重新封存 `35C377E0D8C47E07249A18EAFE65352758221BCE145DBB9F9F50DA459E1E4155`，启动后「图像显示」为 检测区域/基准点/运行点/文本 四行，「文本显示」为 内容/OK颜色/NG颜色/字号/透明度/位置X/位置Y 七行；截图 `Release/_validation/AgentDesktopToolReview/pixelcount-result-self/pixelcount-result-structure.png`。

**剩余差异**：文本默认内容格式（本方 `模块状态:{0} 像素数:{1} 比率:{2}`，原厂 `模块状态:{模块状态}像素数量:{像素数量}…`）与位置 X/Y 的输入形态（本方数值+引用，原厂纯 `ButtonSelecter`）。
