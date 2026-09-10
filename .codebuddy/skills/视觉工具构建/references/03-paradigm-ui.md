# UI 范式（三 Tab + 双卡尺装配 + 生命周期）

## 1. 三 Tab 结构

| Tab | 内容 | 备注 |
|---|---|---|
| **Tab1 元素编辑** | 双元素并排编辑器 / 区域编辑器 / 引导编辑器 | 绘制优先 + 参数订阅双来源；位置修正段（可选） |
| **Tab2 运行参数** | 算法参数（阈值/极性/拟合参数…） | 卡尺几何参数**不在此重复**（单一入口在 Tab1） |
| **Tab3 结果显示** | 判定列表（数据驱动）+ 图形/文本样式开关 | 判定用 `ItemsControl`，样式开关绑 `GraphicsStyle.IsVisible` |

### Tab1 三形态

| 范式线 | Tab1 控件 | 形态 |
|---|---|---|
| A 图像处理式 | `RoiEditorManagerControl` | 检测区/屏蔽区列表 + 区域启用开关 |
| B 引导检测式 | `MeasurementEditorManagerControl` | 单引导（线/圆/弧）+ 卡尺参数面板 |
| C 几何测量式 | `MeasureElementsEditorControl` | 双元素并排 + 运行参数卡尺切换 |

## 2. 双卡尺装配（范式线 C）

### 2.1 `MeasureElementsEditorControl` API（`src/Plugin.SDK/UI/Controls/Measure/Views/`）

| 成员 | 签名 | 用途 |
|---|---|---|
| `Element1` / `Element2` | `MeasureEditorControl` | 两个子编辑器 |
| `ActiveElementIndex` | `int`（DP） | 当前激活元素（互斥高亮） |
| `ConfigureElement(int index, params MeasureGeometryType[] allowed)` | void | 限定该元素允许的几何类型 |
| `SetMainImageControl(ImageControl?)` | void | 托管图像显示（zoom/overlay 共享） |
| `SetPositionCorrectionMatrix(AffineMatrix?)` | void | 位置修正矩阵下发两个子编辑器 |
| `ClearElementToolSelection(int index)` | void | 绘制开始时清除该元素的工具选择 |
| `RefreshOverlay()` | void | 主动刷新画布（数据恢复后首屏可见） |
| `ClearMainOverlay()` | void | **清理闭环**：解订阅 + 清共享 overlay + 解除子编辑器 canvas |

### 2.2 `MeasureEditorControl` API

| 成员 | 签名 |
|---|---|
| 事件 | `GuideGeometryChanged(EventHandler<GuideGeometryChangedEventArgs>)`、`CaliberParamsChanged(EventHandler<CaliberParamsChangedEventArgs>)`、`SelectionChanged(EventHandler<bool>)` |
| 方法 | `SetGuide(Line2d line, Point start, Point end)` / `SetGuide(Circle2d)` / `SetGuide(Arc2d)` |
| 方法 | `SetCaliberParams(double width, double length, int count)` — **全宽/全长口径** |
| 方法 | `SetAllowedGeometryTypes(params MeasureGeometryType[])` |

### 2.3 装配模板（Code-Behind）

```csharp
private MeasureEditorControl Element1Editor => ElementsEditor.Element1;
private MeasureEditorControl Element2Editor => ElementsEditor.Element2;
private bool _syncingElement1Caliber, _syncingElement2Caliber, _elementsHooked;

public XxxToolDebugControl()
{
    InitializeComponent();
    Parameters = _parameters;
    InitializeEnumCombos();
    HookElementsEditor();
    Loaded += (_, _) =>
    {
        HookElementsEditor();                                   // 防重复挂接
        if (Window.GetWindow(this) is { } window)
            window.Closing += (_, _) => Cleanup();               // 关闭即清理
    };
}

/// <summary>编辑器事件 → 参数回写（UI → 模型）</summary>
private void HookElementsEditor()
{
    if (_elementsHooked) return;
    _elementsHooked = true;

    Element1Editor.GuideGeometryChanged += (_, e) =>
    {
        if (Parameters == null) return;
        if (e.GeometryType == MeasureGeometryType.Line && e.Model.EditingLine.HasValue)
        {
            Parameters.E1StartX.Value = e.Model.LineStartPoint.X;
            Parameters.E1StartY.Value = e.Model.LineStartPoint.Y;
            Parameters.E1EndX.Value   = e.Model.LineEndPoint.X;
            Parameters.E1EndY.Value   = e.Model.LineEndPoint.Y;
        }
        if (e.IsDrawing) ElementsEditor?.ClearElementToolSelection(0);   // 绘制时取消工具选中
    };
    Element1Editor.CaliberParamsChanged += (_, e) =>
    {
        if (Parameters == null || _syncingElement1Caliber) return;       // 防回环
        Parameters.E1MeasureLength1.Value = e.Width / 2;                 // 全宽 → 半长
        Parameters.E1MeasureLength2.Value = e.Length / 2;
        Parameters.E1NumMeasures.Value    = e.Count;
    };
    // 元素2 同构（index 1）
}

/// <summary>参数 → 编辑器恢复（模型 → UI）</summary>
private void RestoreEditorsFromParameters()
{
    if (ElementsEditor == null || Parameters == null) return;

    var e1 = new Point(Parameters.E1StartX.Value, Parameters.E1StartY.Value);
    var e1End = new Point(Parameters.E1EndX.Value, Parameters.E1EndY.Value);
    if ((e1End - e1).Length > 1e-6)
        Element1Editor.SetGuide(Line2d.FromTwoPoints(e1, e1End), e1, e1End);
    // 元素2 同构

    PushCaliberToEditor(1);
    PushCaliberToEditor(2);
    ElementsEditor.RefreshOverlay();      // 数据恢复后立即刷新（无需等鼠标进入图像区）
}

private void PushCaliberToEditor(int index)
{
    if (ElementsEditor == null || Parameters == null) return;
    var e = index == 1 ? Element1Editor : Element2Editor;
    var l1 = index == 1 ? Parameters.E1MeasureLength1 : Parameters.E2MeasureLength1;
    var l2 = index == 1 ? Parameters.E1MeasureLength2 : Parameters.E2MeasureLength2;
    var n  = index == 1 ? Parameters.E1NumMeasures    : Parameters.E2NumMeasures;

    if (index == 1) _syncingElement1Caliber = true; else _syncingElement2Caliber = true;
    try { e.SetCaliberParams(l1.Value * 2, l2.Value * 2, n.Value); }     // 半长 → 全宽
    finally { if (index == 1) _syncingElement1Caliber = false; else _syncingElement2Caliber = false; }
}
```

**防回环铁律**：参数 → UI 的推送必须置 `_syncingXxxCaliber` 标志，否则 `CaliberParamsChanged` 回写会与推送互相触发。

**运行参数 Tab 的卡尺切换**：Tab2 用 `ComboBox`（卡尺1/卡尺2）切换两组参数面板可见性——同一参数在 Tab2 只出现一次（分组显示，不是重复编辑）。

## 3. `ToolDebugControlBase` 生命周期实现模板

```csharp
public override void SetParameters(ToolParameters parameters)
{
    base.SetParameters(parameters);
    if (parameters is not XxxParameters typed)
        throw new InvalidOperationException($"XXX 节点参数类型异常（期望 {nameof(XxxParameters)}，实际 {parameters?.GetType().Name ?? "null"}）。");   // rule-015
    Parameters = typed;
    RestoreEditorsFromParameters();     // 双卡尺特有：恢复引导 + 卡尺
    SyncEnumsFromParameters();          // 双卡尺特有：恢复枚举下拉
}

public override void SetDataProvider(object dataProvider)
{
    base.SetDataProvider(dataProvider);
    _dataProvider = dataProvider;
    Parameters?.RestoreImageSource(AvailableDataSources);
    Parameters?.RestorePositionCorrectionSource(AvailableDataSources);
    UpdatePositionCorrectionMatrix();
}

public override void SetMainImageControl(ImageControl? imageControl)
{
    base.SetMainImageControl(imageControl);
    ElementsEditor?.SetMainImageControl(imageControl);
    ElementsEditor?.ConfigureElement(0, MeasureGeometryType.Line);   // 按工具限定几何类型
    ElementsEditor?.ConfigureElement(1, MeasureGeometryType.Line);
    RestoreEditorsFromParameters();
    UpdatePositionCorrectionMatrix();
}

protected override object ExecuteTool()
{
    if (Parameters == null) throw new InvalidOperationException("Parameters not initialized.");
    var imageSource = Parameters.ImageSource;
    if (imageSource == null) throw new InvalidOperationException("请选择输入图像源");
    if (_dataProvider is not DataSourceQueryService query)
        throw new InvalidOperationException("数据提供者未初始化");
    if (!query.HasNodeExecuted(imageSource.SourceNodeId))
        throw new InvalidOperationException($"当前工具缺少输入图像：图像源 '{imageSource.SourceNodeId}.{imageSource.PropertyName}' 没有值");

    var image = query.GetPropertyValue(imageSource.SourceNodeId, imageSource.PropertyName) as IVisionImage;
    if (image == null || image.IsEmpty) throw new InvalidOperationException("无法获取图像数据或图像为空");

    Parameters.ResolvedInputImage = image;
    var result = new XxxTool().Run(Parameters);
    UpdatePositionCorrectionMatrix();
    return result;
}

protected override bool CanExecuteTool()
    => Parameters != null && Parameters.ImageSource != null && _dataProvider != null;

public override void OnExternalResultArrived() => UpdatePositionCorrectionMatrix();

private void UpdatePositionCorrectionMatrix()
{
    if (ElementsEditor == null || Parameters == null) return;
    ElementsEditor.SetPositionCorrectionMatrix(
        Parameters.IsPositionCorrectionEnabled
            ? PositionCorrectionResolver.ResolveFirstMatrix(Parameters.PositionCorrectionBindingSource)
            : null);
}

public void Cleanup()
{
    ElementsEditor?.ClearMainOverlay();
    RemoveLastResultOverlay();
}
```

## 4. 元素来源双路径（绘制 / 参数订阅）

| 来源 | 机制 | 适用元素 |
|---|---|---|
| 绘制优先 | 用户在图像上绘制引导 → `GuideGeometryChanged` → 回写 `ParamValue.Value` | 线 / 圆（可绘可订） |
| 参数订阅 | `ConfigSetting` 切绑定模式 → 上游字段由执行器 `ApplyBindings` 注入 → 写入 `Value` | 全部字段 |

**关键点**：
- 两条路径最终落在**同一份 `ParamValue.Value`** → Run 只读 `Value`，无需分支判断来源
- 绘制路径：引导几何 + 卡尺参数 → measure 拟合被测几何
- 订阅路径：逐字段数值**仍走 measure**（点元素除外）
- **点元素仅参数订阅**：X/Y 为 `ParamValue<double>`，**无绘制入口**（Tab1 不提供其绘制区，只保留 X/Y 订阅输入），Run 直接取坐标，不做 measure

## 5. 图像源与位置修正

| 项 | 做法 |
|---|---|
| 图像源选择 | `ImageSourceSelector`（Tab1 顶部）；`SetDataProvider` 内 `Parameters.RestoreImageSource(AvailableDataSources)` |
| 位置修正 | ToggleSwitch 启用 + 矩阵绑定；`RestorePositionCorrectionSource(AvailableDataSources)` |
| 矩阵同源 | 交互层 `UpdatePositionCorrectionMatrix()` 与 Run 的 `PositionCorrectionResolver.ResolveFirstMatrix` **必须同源** |
| 无需位置修正的工具 | 移除该段（参考 `tools/SunEyeVision.Tool.Threshold/`） |

## 6. 生命周期完整覆盖表（打开→运行→关闭→重开）

| 阶段 | 宿主回调 | 工具侧动作 | 验证点 |
|---|---|---|---|
| 构造 | — | `InitializeComponent` + `Parameters = _parameters` + 枚举初始化 + 挂接编辑器事件 | 无异常 |
| 打开 | `SetParameters` | 类型校验（不符 **throw**）→ `RestoreEditorsFromParameters` + `SyncEnumsFromParameters` | 引导/卡尺/枚举正确回显 |
| 打开 | `SetDataProvider` | `RestoreImageSource` + `RestorePositionCorrectionSource` + 矩阵同步 | 图像源下拉有值 |
| 打开 | `SetMainImageControl` | 托管子编辑器 + `ConfigureElement` 限定类型 + 恢复 + 矩阵 | 元素面板可用 |
| 打开 | `Loaded` | 再挂接（幂等）+ 订阅 `Window.Closing → Cleanup` | — |
| 打开 | `InitializeAsync`（可选） | 异步预载 | — |
| 打开 | `OnExternalResultArrived` | 矩阵同步 | — |
| 运行 | `ExecuteTool` | 取图 → `ResolvedInputImage` → `Run` → 矩阵同步 | 结果落位 |
| 运行 | `UpdateResultDisplay` | 结果 → UI（ResultText / 判定状态） | 数值与判定一致 |
| 交互 | 元素编辑 | `GuideGeometryChanged` / `CaliberParamsChanged` → 参数回写 | 参数随绘制更新 |
| 交互 | 缩放平移 | zoom/overlay 共享（由容器托管） | 双元素同步 |
| 关闭 | `Window.Closing → Cleanup` | `ClearMainOverlay` + 移除结果叠加 | 无残留 |
| 重开 | 再次 `SetParameters`… | 同"打开" | **无重复订阅、无残留叠加、可正常执行** |

## 7. 常见坑

| 坑 | 症状 | 规避 |
|---|---|---|
| 事件重复挂接 | 关闭重开后同一操作触发多次 | `_elementsHooked` 幂等标志 + `Loaded` 里再挂一次 |
| 参数推送回环 | 卡尺值自增/自减 | `_syncingXxxCaliber` 标志 |
| 数据恢复后画布空白 | 需鼠标进入图像区才显示 | 恢复后立即 `RefreshOverlay()` |
| 关闭后叠加残留 | 重开出现旧图元 | `Closing → Cleanup → ClearMainOverlay()` |
| Tab2 重复编辑卡尺 | 改一处另一处不生效 | 卡尺参数单一入口（Tab1 面板） |
| 位置修正不一致 | 交互看到的位置与 Run 用的不同 | 两处都走 `PositionCorrectionResolver.ResolveFirstMatrix` |
| 自建参数控件 | 与 SDK 风格不一致、绑定失效 | 一律用 SDK 控件（`ConfigSetting`/`NumericUpDown`/`ToggleSwitch`…） |
