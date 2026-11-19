# Blob 输入页组装偏差分析

日期：2026-11-18  
范围：VM 原厂输入页组装与自研 `FlatNative` 生产链

## 证据结论

当前已确认的事实如下：

- Blob Runtime 的 `InputSchema` 保留 `input.image`、`input.pose` 和原厂 page 元数据。
- 18 工具 page/section/order 静态映射审计通过。
- 正式窗口的 `ToolSettingsViewOptions` 已设置 `FlatNative` 和 `InputContentFactory`。
- 真实窗口诊断显示 `factory=True`、`inputPresentation=2`，但 `factoryInvoked` 未出现，`declaredInputs=0`。
- 因此故障发生在输入工厂回调之前，当前不是字段声明、持久化或 `BlobRoiUi` 控件创建阶段。

## 原厂与自研差异

| 阶段 | VM 原厂证据 | 当前自研实现 | 偏差 |
|---|---|---|---|
| 参数根创建 | 参数根同时持有页、组、输入行和专用组装槽 | `DeclaredSettingsView` 先创建 settings 页，再依赖 `PageBuilt` 回调创建输入组合 | 输入组合不是参数根的同一事务 |
| 输入页身份 | 输入 declaration 直接绑定到参数根的 page 节点 | 先由 `InputSection` 推导 `inputPage`，再与 settings page 回调字符串比较 | page 身份有二次推导 |
| 页物化 | 参数根组装时输入槽和字段一并物化 | `DeclaredFieldsView` 的页构建回调间接触发 `InputContentFactory` | 输入工厂依赖外层页构建时序 |
| 字段所有权 | 输入字段和设置字段由同一参数页所有者创建 | `DeclaredInputFieldsView` 先独立创建，再由外层迁移字段 | 创建和安装分属两个对象 |
| 专用 ROI 组装 | ROI 槽直接引用参数根中的输入行 | `BlobRoiUi` 先前引用嵌套输入宿主，现改为外层锚点 | 旧接缝残留造成二次偏差 |
| 生命周期 | 页、输入、专用组装同一代次 | 输入工厂、外层页、Blob 组合各有回调入口 | 可能出现页已建而输入未建 |

## 根因判定

根因是参数页组装事务被拆开。`InputContentFactory` 仍然是由外层 page callback 条件触发的可选回调。FlatNative 只改变了字段安装方式，没有把输入 declaration 提升为参数根的正式 page composition。这样会出现静态 page 映射正确而运行时工厂不被调用的状态。

## 新增原厂源码证据

VM `IMVSBlobFindModuCs.BlobFindRoiManager` 直接持有原厂 `RoiManager`。矩形和环形 ROI 写入时先设置 `ROIAreaType`，再通过原厂 `InputItem` 写入五个或六个几何字段。遮挡多边形和 Mask 图像也直接交给同一个 `RoiManager`。这证明 Blob 的 ROI 组装拥有一个原厂输入管理器，ROI 字段创建、模式切换和算法写回属于同一个输入组装事务。

当前自研 `BlobRoiUi` 将字段创建、外层锚点安装和 ROI owner 生命周期拆在三个对象中，且输入工厂还依赖 page callback，因此与原厂事务边界不一致。

## 修复要求

1. `DeclaredSettingsView` 在创建 `DeclaredFieldsView` 前确定唯一输入 page，并把 `INativeInputComposition` 作为同一组装事务的正式输入。
2. `DeclaredFieldsView` 提供显式 page composition 入口，输入字段安装不依赖 `PageBuilt` 事件字符串比较。
3. `PageBuilt` 只负责通知和探针，不再负责创建正式输入字段。
4. 专用组装只允许引用外层已注册的 external field anchor。
5. 测试必须证明 Blob 的输入工厂被调用、`declaredInputs=2`、`input.image` 和 `input.roi.area-type` 出现在同一参数根视觉树中。

## 当前状态

本分析已将问题从“控件缺失”收敛为“参数根组装事务与原厂 page 物化时序不一致”。在修复正式 page composition 前，不继续批量迁移其他复合工具。
