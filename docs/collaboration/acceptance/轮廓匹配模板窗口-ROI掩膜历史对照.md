# 轮廓匹配模板窗口：ROI / 掩膜 / 历史 对照基线

> 依据「逆向对标」技能流程产出。证据级别：① 反编译源码（`docs/07-工具逆向/decompiled/`）优先。
> 所有结论必须能指到 `文件:行号`；无证据项单独标「待取证」，不写成结论。

## 证据

| 项 | 原厂值 / 行为 | 证据 |
|---|---|---|
| 工具互斥 | `GroupName="ty"`，Checked 时设 `DrawROIType` + `SelectedROIList` + `IsStateForDrawROI=true` + `curSelectedRadioButton` | `HPFeaturepatMatchDialogUI.cs:1549-1621` |
| 普通形状写训练集合 | `SelectedROIList = ItemsData` | 同上 `:1595` |
| `hikCross` 写特殊集合 | `SelectedROIList = SpecialRoiList` | 同上 `:1600` |
| 取消工具回箭头 | `if (curSelectedRadioButton == null && radioButton != Arrow) Arrow.IsChecked = true` | 同上 `:1614-1617` |
| 一次性绘制 | `RoiAddEvent`：画完取消当前工具并置 null | 同上 `:490-497` |
| 删除按钮 | `ItemsData.Remove(button.Tag)`（只删该对象） | 同上 `:1805-1813` |
| 删除后联动 | `AutoSetFixPoint()` → `RecordHistory()` → 非空 `MatchSelfExecute()` / 空 `DoClear()` | 同上 `:850-863` |
| `DoClear` 内容 | 清 `otherMask`/`otherMask2`/`ShapeList`/`ClearExternShapes`/`ClearMaskData`；**不清 FixPoint** | 同上 `:870-889` |
| 自动中心 | 样本几何中心均值；空列表清零并清 `IsSetCrossByManual` | `HPFeaturepatMatchDialogData.cs:497-551` |
| 换图清中心 | `if (!MatchPointLock) { FixPointX=0; FixPointY=0; }` | `HPFeaturepatMatchDialogUI.cs:837-848` |
| 锁定禁用底栏输入框 | `HikTextBoxStyle` 的 `DataTrigger MatchPointLock=True → IsEnabled=False` | `hpfeaturepatmatchdialoguibasestyle.xaml:9-17` |
| 撤销 / 重做 | Back：`history.Count>historyIndex && historyIndex>0`；Forward：`history.Count>historyIndex+1 && historyIndex>-2`；恢复后通知 `BackEnable`/`ForwardEnable` | `HPFeaturepatMatchDialogUI.cs:1302-1328` |
| 历史快照内容 | 只写 `ItemsData`/`otherMask`/`otherMask2`/`_FixPointX`/`_FixPointY`，并**在保存前把 `ModelData` 置 null**；序列化成 `./Temporary/FeaturepatMatch_N.xml` | `HPFeaturepatMatchDialogUI.cs:779-803`（`:789` 是 `ModelData = null`） |
| 历史恢复内容 | `loadXml` 后恢复 `ItemsData` + `ReFreshData()` + 两层掩膜 + `FixPointX/Y` + `AddBindingCollectionEvent()` + `FreshOtherMask()` | `HPFeaturepatMatchDialogUI.cs:805-820` |
| 橡皮擦生命周期 | `new RubberControl` + `RegisterRoi` + `DrawROIType=ToolROIModel` + `IsStateForDrawROI=true` + `Cursor=None` | 同上 `:1330-1360` |
| 橡皮擦写层 | `rubber_OnWipe`：`otherMask[idx]=1` | 同上 `:1416-1456` |
| 画笔写层 | `rubber2_OnWipe`：`otherMask2[idx]=1` | 同上 `:1458-1500` |
| 擦除覆盖层颜色 | `otherMask`：≤10 → `0x4CFF1A1A`；>10 → `0x99FF1A1A` | 同上 `:1222` |
| 屏蔽覆盖层颜色 | `otherMask2`：≤10 → `0x800F6AF3`；>10 → `0xB00F6AF3` | 同上 `:1275` |
| 样本填充色 | 正样本 `#3884FF` Opacity 0.25；背景 `#EC0000` Opacity 0.45；特殊 `#60FF00` Opacity 0.30 | `roicontrolbasestyle.xaml:62-64` + `rectroistyle.xaml:214-220` |
| 样本区域描边 | `ROIBaseColor` = `#0F6AF3`（不透明） | `roicontrolbasestyle.xaml:47` |
| 匹配中心样式 | `#7CFC00` 四向短线 + 空心圆环，`IsHitTestVisible=False` | `crossroistyle.xaml:19-38` |
| 中心坐标语义 | `CenterPoint` 为图像像素；仅 `INormalizedROI` 才除宽高 | `CrossROI.cs:41-52` |
| 中心渲染 | `ContainerPosition = CenterPoint × ControlToImageRadio` | `CrossROI.cs:60-65` |
| 笔刷光标 | 2px 黑边 + 白 50% 方块；`Cursor=None`；指针越界隐藏 | `RubberControl.xaml.cs:180-260` |
| 笔刷尺寸 | `Width = DWidth × ControlToImageRadio`；`DWidth/DHeight` 直接 `SetBinding("PanRectWidth")` | `RubberControl.xaml.cs:257-259`、`:1340-1341` |
| 网格点选中心 | 门控 `CrossIsReady`，但 `SetCrossReady` **无任何调用点** → 该路径实际不可用 | `HPFeaturepatMatchDialogUI.cs:131`、`:1623`、`:1864-1873` |
| 名称焦点切选中 | `listView.SelectedItem = this` | `ModelItem.cs:479-489` |
| 列表布局 | 操作列 8 网格 `1,*,1,*,1,*,1,*`；序号 0 基；勾选在第 8 列 | `featurematchconfigpanal.xaml:137-168` |

## 对照

| 项 | 原厂 | 我方 | 判定 |
|---|---|---|---|
| 工具互斥 | `GroupName="ty"` + `SelectShape` 切换 | 同构 | 已一致 |
| 画完回箭头 | `Arrow.IsChecked = true` | `UncheckDrawingButtons` 补 `fun`/`fun2` 并回箭头 | 已一致（本轮） |
| `hikCross` 目标集合 | `SpecialRoiList` | `SelectedROIList = draw == typeof(CrossRoiItem) ? _specialRoiList : _items` | 已一致（本轮修正类型判断） |
| 删除命令 | 按对象 `Remove` | `DeleteRoi(object?)` 按对象移除 | 已一致 |
| 删除后联动 | 非空 `AutoSetFixPoint` / 空 `DoClear` | 同构（空时清掩膜 + `_shapeList` + `_modelOutline`） | 已一致（本轮） |
| 删除清 `FixPoint` | 不清 | 不清 | 已一致 |
| 撤销边界 | `>0` / `>-2` | `>0` / `<Count-1`（初始 0） | 等价 |
| 撤销恢复范围 | `ChangeHistory` 恢复 `ItemsData`+两层掩膜+`FixPoint`，**不含 `ModelData`**（`RecordHistory:789` 置 null） | `HistorySnapshot` = `Document`+两层掩膜+尺寸+`FixPoint`+`IsSetCrossByManual`；**不含** `PendingPayload` | 已一致（取证后回退了先前误加的载荷字段） |
| 橡皮擦写层 | `otherMask` | `_wipeToPaintMask=false` → `EraseMask` | 已一致（本轮） |
| 画笔写层 | `otherMask2` | `_wipeToPaintMask=true` → `PaintMask` | 已一致（本轮） |
| 擦拭域语义 | 擦除层 `Difference`、屏蔽层 `Union2` | `ComposeTrainingDomain`：`(domain − erase) ∪ paint` | 已一致（本轮） |
| 覆盖层颜色 | 红 `0x4C/0x99`、蓝 `0x80/0xB0` | 同值 | 已一致 |
| 样本填充色 | `#3884FF`@0.25 / `#EC0000`@0.45 | 同值（此前误用 DrawMask 色） | 已一致（本轮） |
| 样本描边 | `#0F6AF3` 不透明 | 同值 | 已一致（本轮） |
| 中心图元 | `ICrossROI` / `CrossROI` | `CrossRoiItem : ICrossROI` | 已一致 |
| 中心坐标语义 | 像素坐标 | `CenterPoint = (FixPointX, FixPointY)` | 已一致 |
| 笔刷光标 | 方块 + 黑边 + `Cursor=None` + 越界隐藏 | 同构 | 已一致 |
| 笔刷尺寸联动 | `DWidth` 直接绑 `PanRectWidth` | `WipeSize => _wipeToPaintMask ? PanRectWidth2 : PanRectWidth` 实时派生 | 已一致（本轮移除中间量） |
| 锁定禁用输入框 | `HikTextBoxStyle` 触发器 | `IsMatchPointEditable` + 两框绑定 | 已一致（本轮） |
| 删除后重新匹配 | `MatchSelfExecute()` | 不重匹配（模型未变，`RoiVisualChanged` 重绘） | **不一致（有意）** |
| 网格点选中心 | 门控常量恒 false，实际不可用 | `SetCrossFromImage` 可用（含锁定检查） | **我方多出能力** |
| 坏模型行 | `IsCorrect=false` 保留并标红 | 解析失败直接丢弃 | **不一致（容错策略）** |
| 橡皮擦 `RemovePoints` | 收集 `tempByte==0` 的索引 | 未照抄，改用方块轨迹插值 | 待取证（该路径与光标方块形状存在矛盾） |

## 结论

**已一致**：工具互斥与一次性绘制、删除命令与联动、撤销边界与恢复范围、擦除/屏蔽分层、覆盖层与样本填充色、中心图元与坐标语义、笔刷光标与尺寸联动、锁定禁用。

**不一致（有意保留，需产品确认）**：

1. 删样本后不重新匹配（`MatchSelfExecute`）——模型载荷未变，重匹配结果相同，只做重绘。
2. 网格点选中心——原厂该路径因 `CrossIsReady` 无调用点而不可用，我方保留为额外能力。
3. 坏模型行不保留（原厂标红保留，禁止改名）。
4. 橡皮擦 `RemovePoints` 的 `==0` 收集语义与光标为方块的视觉相矛盾，待进一步取证后再决定是否照抄。

**待取证**：

- 橡皮擦 `wipe:301-350` 与 `RemovePoints:352-393` 的配合存在语义矛盾：`wipe` 把菱形区域置 `array=1`，而 `RemovePoints` 收集的是 `tempByte==0` 的索引交给 `OnWipe` 擦除。按字面理解，擦掉的是“包围盒减去菱形带”，与 `RubberControl.xaml` 的方块光标形状不符。在拿到实机行为证据前，本方保持“沿轨迹填方块”（与光标视觉一致），不照抄该分支。

**已取证消除的疑问**：

- `ChangeHistory(path)` 的恢复范围：只恢复 `ItemsData`/两层掩膜/`FixPoint`，`ModelData` 在保存历史时就被置 null（`RecordHistory:789`），因此**历史不含模型载荷**。本方据此回退了先前误加的 `PendingPayload` 字段。

## 实施顺序（剩余）

1. 人工验证本轮四项：样本填充色、删除后填充消失、撤销回退载荷、锁定禁用输入框。
2. 若第 1 项通过，再处理上面「不一致」里的 2、3 两项（需产品决策）。
