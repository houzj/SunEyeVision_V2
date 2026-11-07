# 图像源 VM 算法语义

- 工具 ID：imagesourcemodule
- 组装类别：image-roi
- 优先级：P1
- 当前状态：source-partial
- 语义状态：partial（界面与取图链路已由反编译源码直读确认；算法本体与原生桥仍 unknown）
- 本轮取证日期：2026-11-05
- 证据来源：`decompiled/WpfControls/VMControls.WPF/`、`decompiled/HostShell/ExeEntry/resources/zh-cn.xaml`

## 证据入口

| 文件 | SHA-256 | 可确认范围 |
|---|---|---|
| `VMControls.WPF/ImageListPanelBase.cs` | `DF02FAA325BABECCDAC3F44D3ED1D750B59874DA466BC0FDF033F997898D4B79` | 图像源条的取图、计数、执行模式、参数回写 |
| `VMControls.WPF/ImageListPanel.xaml` | `A5AC5A8101E7CCCB036A2430E6C1766EC3B4CAEC18010580DB178EC88937C7CF` | 条内布局、计数绑定、6 个按钮 |
| `VMControls.WPF/LocalImageListView.xaml` | `D22C8A3F7FCE0D94E966A22FCE9865C91DE629471D4F1AD0F93BAA2CF6B19883` | 三个运行控件的形态与可见条件 |
| `VMControls.WPF/LocalImageListView.xaml.cs` | `D887A0C8D363E9C6257977650AB3807B0A58547D91F424C93A369E9566451006` | 运行全部/运行选中、自动切换、取图间隔的解析 |
| `HostShell/ExeEntry/resources/zh-cn.xaml` | `C3AD9209618D7B36FA3F59C6AF2103FB2DEFB7066A03804E8DE80E992471610E` | 「自动切换」等术语的中文原文 |

## 执行语义链

| 步骤 | VM 原厂证据 | 当前结论 |
|---|---|---|
| 工具创建/初始化 | `ImageSourceModule` 模块类未入库，构造函数与注册链未取证 | unknown |
| 参数读取与条件显隐 | `LocalImageListView.xaml.cs:143-187` 从 `Root.FindBaseNodeByName` 解析 `AutoPlay` / `Interval` / `CurrentImagePath`；`LocalImageListView.xaml:45-81` 三处 `MultiDataTrigger` 控制显隐；`ImageListPanelBase.cs:1234-1236` 把 `Params["AutoPlay"]` 回写 UI 节点 | confirmed |
| 输入/ROI/变量准备 | `ImageListPanelBase.cs:442-475` 取图循环：`Params["ImageNumber"]` 定数量，`Params["ImagePath"+i]` 逐个取路径，`Params["CurImagePath.Value"]` 取当前图 | confirmed |
| Process/ProcessAlg/原生桥 | 模块 Process 实现与 `IMVS_ALGORITHM_INIT_RESULT_INFO`（`nType=268439552`）处理仅在 `ImageListPanelBase.cs:417-427` 见到回调订阅，函数体未取证 | unknown |
| 分支、失败与取消 | 仅见 `RunCheck` 前置校验（`ImageListPanelBase.cs:1339-1344`），失败路径与错误码未取证 | partial |
| 结果写回与判定 | `ImageListPanelBase.cs:1336` 写 `Params["CurrentImagePath"]`；`:1649` 读 `Params["GetAllExecutePic"]` 拆分已执行图集合 | partial |
| 图形/文本显示发布 | `ImageListPanel.xaml:244-253` 计数 `({0}/{1})` = `CurImageIndexT` / `Items.Count`；`:190` 缩略图 ListBox；`:220-243` 虚拟化与选中样式 | confirmed |
| 保存恢复 | `LocalImageListView.xaml.cs:243,276-289` 执行模式存 `Root.UserStringData["ExecuteType"]`（"1"=运行全部，"2"=运行选中）；`AutoPlay`/`Interval`/`CurrentImagePath` 存模块 `Params` | partial |
| DeInit/Destroy/释放 | 缩略图后台刷新线程（`ImageListPanelBase.cs:339-353`，`IsBackground=true`）的退出与 `reader` 释放顺序未取证 | unknown |

## 三条已确认的界面语义（供实现直接引用）

1. **计数**：`CurImageIndexT` = `CurImageIndex + 1`（1-based 对外，未选中为 0）；分母 = `lst.Items.Count`。删除当前图（`:589`）、清空（`:698`）都会把它打回 0。
2. **数据源**：列表 `DataContext` = `Root.FindBaseNodeByName("CurrentImagePath")`，模块 = `AlgorithmTabRoot.Owner as IVmModule`。**按参数页所属模块取参，不继承上游来源。**
3. **三个运行控件**：「运行全部/运行选中」= 一个 ComboBox 两选项（写 `ExecuteType`）；「自动切换」= `AutoPlay` 开关，语义为**每次执行后切下一张**；另有「取图间隔」= `Interval`。

## 尚未确认

- 多上游图像源汇入时的选择规则（需 `VM.Core` 或流程执行器）
- 选中下游节点时的实际视觉表现（需原机窗口）
- `ExecuteType=2`（运行选中）时勾选集合的逐张执行行为（需运行观察）
- 图像真正的解码、像素处理与 ROI 语义（属算法本体）

## 规则

此文件只接受原厂反编译源码、资源、汇编/ABI或受控运行证据。产品意图、截图或自研实现不能单独证明 VM 算法语义。
