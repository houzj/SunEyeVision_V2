# 画布原厂规格 · 01 DiagramItem 精读（节点容器）

> 来源：`decompiled/WpfControls/VMControls.WPF.Base/DiagramItem.xaml.cs`（969 行）
> 每条带行号，作为统一对齐的规格依据。未读到的段落标注"未确认"。

## 1 常量与依赖属性

| 项 | 值 | 行号 |
|---|---|---|
| `MaxNameLenth` | **64** | L30 |
| DP | DisplayType / IsModuleFailed / IsModuleForbidden / RunState / NextItemsSource / IsHited / Position / IsOpenParams / IsSelected(ListViewItem) 等 11 个 | L32-52 |
| 私有字段 | ConnectorView / DragView / itemBorder / logoIm / rectangle / adornerPanel / subAdornerPanel / CurrentHotRegion / CurrentHotPoint / page / controlOffset / leftOffset / topOffset / _offset / isDragOvered … | L54-92 |

## 2 方法规格（按交互相关性排序）

### 2.1 `OnApplyTemplate`（L493-516）
| 行为 | 行号 |
|---|---|
| `ConnectorView = Template.FindName("PART_View")`（端口/拖动起手宿主） | L496 |
| `rectangle = Template.FindName("Rec")`（Margin=-10 透明热区） | L497 |
| `DragView = Template.FindName("Drag_View")`（PROCESS 模板标题栏） | L498 |
| `itemBorder = FindName("itemBorder")`、`logoIm = FindName("LogoIm")` | L499-500 |
| **`AppDataFactory.Instance().IsRightLayout` → `logoIm.Visibility = Collapsed`**（右布局隐藏图标） | L501-506 |
| 拖动起手：`(DragView ?? ConnectorView).MouseLeftButtonDown += ToolItemGrid_PreviewMouseDown` | L512-513 |
| `… .PreviewMouseLeftButtonUp += DiagramItem_PreviewMouseUp` | L514 |
| `itemBorder.MouseUp += ItemBorder_MouseUp` | L515 |

### 2.2 `OnPreviewMouseLeftButtonDown`（L471-484）
| 条件 | 行为 | 行号 |
|---|---|---|
| `IsOpenParams` 且 单击数<2 且 源不是 Button | `e.Handled = true`（**单击不选中**） | L473-478 |
| `IsOpenParams` 且 双击或点 Button | `IsSelected = true` | L479-482 |
| `IsOpenParams=false` | 不拦截，走 ListView 默认单选 | — |

### 2.3 按下（L604-621，`ToolItemGrid_PreviewMouseDown`）
| 行为 | 值/行号 |
|---|---|
| 前置条件 | `!IsLockWorkArea && e.OriginalSource != rectangle`（排除透明热区，防误拖）L606 |
| 记录 `_offset` | `e.GetPosition(this)`（鼠标在节点内坐标）L609 |
| 记录 `leftOffset` | 工具箱折叠 **52** / 展开 **230** / 右布局 **0** L610-618 |
| 记录 `topOffset` | **40** L619 |
| 挂接 | `ConnectorView.PreviewMouseMove += DiagramItem_PreviewMouseMove` L621 |

### 2.4 首次移动 `DiagramItem_PreviewMouseMove`（L623-714）
| 行为 | 行号 |
|---|---|
| 先 `-=` 自注销（只触发一次） | L625 |
| 左键已松 → 走 PreviewMouseUp 并退出 | L630-634 |
| `DataObject.SetData(new List<DiagramItem>{ this })` | L635-636 |
| 建 `AdornerPanelAttachToWorkArea` + `subAdornerPanel` | L637-641 |
| **热区**：`CurrentHotRegion = Rectangle{Fill=Orange, Visibility=Collapsed}`，加进 adornerPanel | L642-649 |
| 注册宿主 `PreviewDragOver/_dragOver`、`PreviewDrop/_dragDrop` | L651-652 |
| 预览：`VisualTreeHelper.GetDescendantBounds(this)` → `VisualBrush(this){Stretch=None, Opacity=0.7}` → `Rectangle{Margin=bounds.X/Y, W/H=bounds.W/H, IsHitTestVisible=false}` | L654-667 |
| 预览初始 **`Visibility = Hidden`** | L669 |
| `AdornerLayer.Children.Add(adornerPanel)` | L672 |
| 首次 HitTest 找 `ItemsPage` 得 `controlOffset = page.TranslatePoint(default, workArea)` | L673-692 |
| `adornerPanel.top = topOffset; adornerPanel.left = leftOffset` | L693-694 |
| `DoDragDrop(this, dataObject, Ctrl? Copy : Move)` —— **阻塞** | L695-701 |
| 拖拽结束清理：移除 adorner、置空 page/CurrentHotRegion、`CurrentHotPoint=(52,40)`、解绑 handler | L703-713 |

### 2.5 `_dragOver`（L739-789）——拖动每帧
| 步骤 | 内容 | 行号 |
|---|---|---|
| 1 | `point = e.GetPosition(WorkAreaElement)`；有缩放时 `point -= (leftOffset,topOffset)` 再 `transform.Inverse.Transform` | L747-755 |
| 2 | 取当前 `subAdornerPanel.OffsetLeft/Top` 作为 `num/num2` 基准 | L756-759 |
| 3 | 水平：`|point.X - _offset.X - offsetLeft| > 3.0` → `num = Round((point.X-_offset.X)/5)*5 + controlOffset.X`，`num2` 同式顺带算，`flag=true` | L762-767 |
| 4 | 垂直：`|point.Y - _offset.Y - offsetTop| > 3.0` → `num2 = Round(.../5)*5 + controlOffset.Y`，`flag=true` | L768-773 |
| 5 | **仅 `flag` 时**：清 adornerPanel 上所有 Line、`CurrentHotRegion.Visibility=Collapsed`、`subAdornerPanel.SetOffsets(num,num2)`、首次置 `Visible`、`DrawAlignLine(num,num2)`、`DrawHotRegion(num,num2)` | L774-788 |

### 2.6 `_dragDrop`（L716-737）
| 行为 | 行号 |
|---|---|
| `!isDragOvered` → `e.Handled=true` 直接返回（未拖入有效区域时不提交） | L718-722 |
| 落点 = `new Point(subAdornerPanel.OffsetLeft, OffsetTop) - vector(controlOffset)` | L726-727 |
| 热区可见时额外写 `HotRegionInfo` + `CurrentHotPoint - controlOffset` | L728-735 |

### 2.7 对齐线 `DrawAlignLine`（L855-870 + L872-925 + L929-944）
| 项 | 内容 | 行号 |
|---|---|---|
| 6 组判定 | 左(`p.Position.X == px`)、右(`p.X+p.W==px+W`)、水平中心(`p.X+p.W/2==px+W/2`)、上、下、垂直中心 | L864-869 |
| 目标筛选 | **同类命中多个时只取"最靠上/最靠左"一个**（`Min(Item1)` → `First`） | L881-882 |
| 线段范围 | 自身边缘 **±5px** 到目标边缘 **±5px**（必要时取 min/max 合并） | L887-925 |
| 线样式 | `Brushes.LightGreen`、`StrokeThickness=1`、`StrokeDashArray={5,2.5}`、`PenLineJoin.Round`、`IsHitTestVisible=false` | L929-938 |
| 容器 | 加到 `adornerPanel.Children` | L943 |

### 2.8 热区 `DrawHotRegion`（L794-850）
| 项 | 内容 | 行号 |
|---|---|---|
| 尺寸 | **66 × 22**，乘以当前 scale | L842-850 |
| 颜色 | `Colors.Orange` | L648 |
| span 规则 | 流程→流程 **160**；跨流程 **115**；普通 **40 + ModuleSpanCount + 30** | L824-840 |
| 时机 | 只在 `_dragOver` 的 flag 分支内重画 | L787 |

### 2.9 其他
| 方法 | 规格 | 行号 |
|---|---|---|
| `PositionChanged` | `Canvas.SetLeft/Top` ← Position | L265-271 |
| `GetConnectorPosition` | 左`(0,H/2)` 右`(W,H/2)` 上`(W/2,0)` 下`(W/2,H)` | L584-594 |
| `GetOrientation` | 0→Left 1→Top 2→Right 3→Bottom，其它→Right | L331-341 |
| `GetAllConnectorPosition` | 4 个锚点 - Position | L307-316 |
| `GetRectWithMargin` | `Rect(Position,W,H).Inflate(5)` | L318-323 |
| `Connect` | `OnConnect(new object[]{ target.DataContext, index, hitIndex })` | L295-301 |
| `ItemBorder_MouseUp` | 把捕获从 DiagramPage 转到自身再释放（清捕获残留） | L518-530 |
| `DataContextChanged` | 按类型包 `VmProcedureDecorator/VmGroupDecorator/VmModuleDecorator`；`ReplaceCurBox()` 同步框选集合 | L532-582 |
| `Delete`（KeyDown） | 条件 `!IsLockWorkArea && !IsOpenParams && IsAdmin` → `DeleteCommand` | L946-952 |

## 3 我方对照（本节随实施逐步补齐）

| 项 | 原厂 | 我方 | 判定 |
|---|---|---|---|
| 拖动预览载体 | AdornerPanel + 0.7 VisualBrush | DragPreviewAdorner（Adorner 层） | 一致 |
| 预览位移 | `SetOffsets` → `TranslateTransform` | 同 | 一致 |
| 吸附 | 3px 死区 + 5px 量化 + flag 才重绘 | 同（吸附落点未变则跳过） | 一致 |
| 落点 | Drop 用预览 offset | Drop 用 `session.After`（量化后位置） | 等价 |
| 对齐线 | 6 组、**每方向只对最靠上的目标画一条**、LightGreen 1px 5,2.5、两端 5px | 已按此实现 | 一致 |
| **热区** | 66×22 橙色、span 三档、仅在 flag 分支 | **未实现** | 缺失 |
| **IsOpenParams 单击拦截** | 单击被 Handled，仅双击/Button 选中 | 未实现 | 缺失 |
| **IsRightLayout 隐藏图标** | Collapsed | 未实现 | 缺失 |
| 拖动起手挂点 | `DragView ?? ConnectorView` | 节点整体 | 待核 |
| `Rec` 热区排除 | `e.OriginalSource != rectangle` | 待核 | 待核 |
