# 交接：轮廓匹配模板窗口（ROI / 掩膜 / 橡皮擦 / 历史）

> 交接对象：Codex
> 时间：2026-11-25
> 证据基线：`docs/collaboration/acceptance/轮廓匹配模板窗口-ROI掩膜历史对照.md`（28 条证据 + 24 项对照）

## 0. 先读这一节，避免白干

1. **只用 D 盘工作副本**：`d:\MyWork\SunEyeVision_V2`。
   `E:\SunEyeVision_V2` 是 11-23 的旧副本，缺 `createTemplateWithMasks` 接线与全部诊断日志，跑它会得到“模型创建未返回有效模板数据”的假故障。
2. **构建前先关程序**：`SunEyeVision.UI` 占用 `SunEyeVision.Tool.ContourMatch.Runtime.dll` 时构建会失败。
3. **跑测试前先杀 testhost**：残留 testhost 会持有旧 DLL 并把模态框弹到用户桌面（本轮已加 `SuppressDialogs` 抑制，但残留进程仍要清）。
4. **本轮改动量大且集中**，不要 `git reset` / `git clean`，先看 `git diff`。

## 1. 交付状态

| 项 | 结果 |
|---|---|
| 全量 Release 构建 | 0 错误，6008 个既有警告 |
| ContourMatch 专项测试 | 43/43 通过 |
| 手工验收 | 8 项已由用户确认通过 |

本批闭口范围为 ContourMatch 工具层和节点调试窗口内的模板/ROI/掩膜行为。多模板真实运行、跨节点生产链和运行后结果空间联动列入后续节点整链验收；橡皮擦源码形状矛盾不阻断本批。

## 2. 本轮改动清单（按功能域）

### 2.1 公共形状类（消除三处私有实现）

- 新增 `src/Plugin.Wpf/Presenters/Display/ShapePrimitives/`：`ShapePrimitive`、`MultiPoint`、`MicroPoint`、`OutlinePoint`
- 替换：`NativeShapeProjection.Outlines.cs` 的 `OutlineBatch`/`OutlineProjection`、`TemplateConfigData` 的 `TemplateOutlineBatch`/`TemplateMicroPoint`
- `RefreshModelOutline` 复用同一个 `MultiPoint`（对齐原厂 `RefreshPoints`）

### 2.2 缩略图与模板存图

- `MatchModel` 生成即写训练原图（`WithTrainingImage`），不再等“确定”
- `BuildTemplatePreview`：先按 98 长边缩放，再在缩略图坐标画 2px 绿点（`#66FF00`）

### 2.3 新建 / 编辑索引隔离

`TemplateIndex >= entries.Count` 即“新建、尚无自己的条目”，四处统一判断：
`LoadFromDraft`、`ApplyEraseMask`、`ApplyTemplateImage`、`EditOutlinePointsEnable`

### 2.4 模板列表

- 操作区改 8 网格 `1,*,1,*,1,*,1,*`，勾选框落在第 8 列
- 序号改 0 基；选中底色 `#FFF2F4F7`；`FocusVisualStyle=null`
- 合并 `NormalCheckBoxStyle`
- 操作列固定 106；勾选走 `WriteEntries(reload:false)`
- 名称框 `GotFocus` → 切选中行（原厂 `ModelItem.cs:479-489`）

### 2.5 匹配中心

- 新增 `CrossRoiItem : TemplateRoiItem, ICrossROI`（草绿准星，`crossroistyle.xaml:19-38`）；`PointRoiItem` 保留给文档 `point` 形状
- `SyncCrossRoi` 使用**像素坐标**（`CrossROI` 未实现 `INormalizedROI`）
- 生成模型后把 Find 姿态写回 `FixPoint`（第三阶段）；受 `IsSetCrossByManual` / `MatchPointLock` 保护
- `PreviewPose` 门槛由“点数≥20”改为“模型外接框短边≥24”——原因是 `GetShapeModelParams` 的 `NumPoints` 在本环境返回**金字塔层数**，拿它当点数会永远跳过 Find
- `PointCount` 改用真实轮廓点数（`ReadContourPoints`）；新增 `outline-points`、`fix-point` 载荷段

### 2.6 锁定

- 新增 `IsMatchPointEditable`，底栏 `FixX`/`FixY` 绑定 `IsEnabled`（原厂 `HikTextBoxStyle` 触发器）
- `SetCrossFromImage` 增加 `MatchPointLock` 早退

### 2.7 橡皮擦

| 子项 | 说明 |
|---|---|
| `PxSlider` | 恢复为 **`Slider` 子类**（原厂结构），用 `XamlReader.Parse` 提供「白底面板 + 轨道 + 数值框 + px」模板；`Value` 即 `RangeBase.Value`，双向绑定原生生效 |
| 尺寸联动 | 删除 `BrushSize` 中间量，改 `WipeSize => _wipeToPaintMask ? PanRectWidth2 : PanRectWidth` 实时派生 |
| 命中/平移屏蔽 | 新增 `ToolRoiItem : TemplateRoiItem, IToolROI`；笔刷模式设 `DrawROIType=ToolRoiItem` + `IsStateForDrawROI=true`，框架据此 `Handled`，不命中 ROI、不平移图像 |
| 光标 | `_brushCursor`：2px 黑边 + `#80FFFFFF`；`Cursor=None`；越界隐藏；边框 `2/ImageScale` |
| 覆盖层时机 | `ExtenAbove` 在 `ImageViewBox.OnApplyTemplate` 后才存在，抽出幂等 `EnsureOverlays()`，在构造/Loaded/OnFreshExtendContent/IsEraseMode 变化时调用 |
| 擦除算法 | 沿「上一笔→当前笔」插值填 `size×size` 方块；`EndWipe` 在抬手时 `Snapshot()` 记一次历史 |

### 2.8 建模端两层掩膜语义（关键修复）

```csharp
// ContourMatchTemplate.Create
using var eraseRegion = MaskToRegion(mask, maskWidth, maskHeight);        // 擦除：扣除
using var paintRegion = MaskToRegion(paintMask, paintMaskWidth, paintMaskHeight); // 屏蔽：并入
using var adjustedRoi = ComposeTrainingDomain(effectiveRoi!, eraseRegion, paintRegion);
// (domain − erase) ∪ paint
```

- 删除 `ContourMatchTemplatePanel.MergeMasks`（原来是 OR 并集，把屏蔽区域也扣掉了）
- `MaskToRegion` 改用 `GenImage1Extern` + `GCHandle` + `Threshold(1..255)`；原实现 `new HTuple(int[])` 传 `PixelPointer` 触发 HALCON `#1404`

### 2.9 历史（撤销 / 重做）

`HistorySnapshot` = `Document` + 两层掩膜 + 尺寸 + `FixPointX/Y` + `IsSetCrossByManual`，**不含模型载荷**——
证据：`RecordHistory:789` 在保存前把 `ModelData = null`；`ChangeHistory:805-820` 只恢复样本/掩膜/中心。
`RestoreHistory` 置 `_modelDirty = true`（本方用于判断是否需要重建，原厂无此字段）。

### 2.10 工具切换与删除

- `SelectShape`：`SelectedROIList = draw == typeof(CrossRoiItem) ? _specialRoiList : _items`
- `UncheckDrawingButtons`：覆盖 `fun`/`fun2`，取消后 `Arrow.IsChecked = true`
- `SelectShape` 两个分支补 `EndWipe()`
- `DeleteRoi`：空列表时清两层掩膜 + `_modelOutline` + `_shapeList`；非空只 `AutoSetFixPoint`；删除十字时清 `_crossRoi`

### 2.11 测试友好

`TemplateConfigData.SuppressDialogs`：抑制建模成功/失败的模态框。`ContourMatchPresentationViewTests` 两处构造后置 true，测试耗时从 1m25s 降到约 0.48s（此前每次都在等弹窗点确定）。

## 3. 遗留（需产品决策，不要擅自改）

| # | 项 | 原厂 | 本方 | 备注 |
|---|---|---|---|---|
| 1 | 删样本后是否重新匹配 | `ItemsData_CollectionChanged` 非空时调 `MatchSelfExecute()` | 不调，只 `RoiVisualChanged` 重绘 | 模型载荷未变，重匹配结果相同 |
| 2 | 网格点选匹配中心 | `GirdMouseDownCommandExecute` 门控 `CrossIsReady`，而 `SetCrossReady` **无任何调用点** → 实际不可用 | `SetCrossFromImage` 可用（含锁定检查） | 属“比原厂多出的能力” |
| 3 | 坏模型行 | `IsCorrect=false` 时整行红框、禁止改名 | 解析失败直接丢弃 | 容错策略差异 |

## 4. 待取证（不要凭推断改）

**橡皮擦 `wipe` 的菱形填充与 `RemovePoints` 的收集条件自相矛盾**

- `wipe:301-350`：把两个对角三角置 `array=1`
- `RemovePoints:352-393`：收集 `tempByte==0` 的索引 → 交给 `OnWipe` 擦除
- 按字面理解，擦掉的是“包围盒减去菱形带”，与 `RubberControl.xaml` 的**方块**光标形状冲突

本方保留“沿轨迹填方块”，与光标视觉一致。若要按字面照抄，需要先拿到原厂实机行为（拖一笔看擦除形状）作为证据。

## 5. 诊断日志（本批已清理）

写入 `%TEMP%\SunEyeVision.ContourMatch.TemplateConfig.log`，`TemplateConfigDiagnostics` 统一入口。当前埋点：

| key | 用途 | 是否保留 |
|---|---|---|
| `window-create` | 开窗时 `delegate/commit/index/frames` | 保留 |
| `match-model` | `delegate/generated/after` | 保留 |
| `outline-read` / `outline-published` | 轮廓点数量与 `pose` | 保留 |

建模入口、笔刷轨迹、光标、覆盖层和刷新排障埋点已从正式代码移除；本机 `%TEMP%` 诊断文件已清理。

## 6. 验证方法

```powershell
# 构建
cd d:\MyWork\SunEyeVision_V2
dotnet build SunEyeVision.slnx -c Release --no-restore

# 专项测试（41/41）
dotnet test tests\SunEyeVision.Tools.Inspection.Tests\SunEyeVision.Tools.Inspection.Tests.csproj `
  -c Release --no-build --filter "FullyQualifiedName~ContourMatch"

# 程序
d:\MyWork\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe
```

**人工验收（已由用户确认）**：

1. 画正样本矩形 → 填充应是**很淡的蓝（25%）**；切成背景样本 → **中等红（45%）**
2. 删除该样本 → 填充立刻消失；日志 `[roi-overlay] items=` 应同步减一
3. 橡皮擦擦过的红色覆盖 → 删 ROI 后**仍保留**（原厂行为，掩膜独立于样本）
4. 点橡皮擦 → 画布出现黑边白方块，系统箭头消失；拖一笔轨迹连续；斜拖边缘为方块带
5. 拖浮层滑块 → 方块立即变大，不再抛 `ThumbMouseLeftButtonUp`
6. 画完任意 ROI → 箭头自动亮起；`hikCross` 画的中心**不进**左侧样本列表
7. 勾选底栏「锁定」→ `FixX/FixY` 变灰；锁定下点画布中心不变
8. 生成模型 → 点「后退」：样本/掩膜/中心回退，**模型载荷不回退**（与原厂一致）

## 7. 后续范围

详细目标驱动计划与 A01-A11 验收矩阵见 `docs/09-现行文档/轮廓匹配节点与框架联动目标驱动计划-20261125.html`。

1. **P0 调试窗口关闭恢复**：正式运行后打开调试、绘制 ROI、调试运行并关闭后，主界面 ViewBox/结果表/几何/文字必须恢复正式 publication；当前“关闭后 ViewBox 清空”已按框架/节点宿主 publication 生命周期增加关闭兜底，“正式运行→调试 ROI→私有运行→关闭”定向 UI 回归 2/2 通过，用户 Release 验收待做。
2. 节点调试窗口整链：节点创建、图像/ROI/Fixture 引用、模板确认回写、参数页重绑、关闭释放。
3. 运行后结果空间：结果表、匹配框/区域/轮廓/文本、结果行高亮、重新执行替换、节点/方案切换清理。
4. 跨节点生产链：ImageSource → ContourMatch → 结果工作区/兼容下游消费者，保存重开后身份和连线恢复；共享图合同的 ImageSource→ContourMatch 自动图像引用回归已通过 1/1，但 ROI/Fixture、真实运行结果、结果工作区和下游消费仍未计通过。
5. 原厂 D12 盲区相关端口（InputMask、盲区多边形、OutputMask）另列功能批次，不在本批补空端口。
