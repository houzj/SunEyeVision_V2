# 公共框架平面输入组装设计

日期：2026-11-18  
范围：`DeclaredSettingsView`、`DeclaredFieldsView`、`DeclaredInputFieldsView`、`NativeToolEditorFrame`、`RuntimeToolEditorWindow`  
状态：框架第一阶段已落地，生产节点入口已启用 `FlatNative`。缺少设置页的输入 page/section 映射会直接失败，必须先补齐原厂证据。

## 当前输入装配链

`RuntimeToolEditorWindow` 创建 `NativeToolEditorFrame`，把 `RuntimeNodeConfigurationControl` 作为 `EditorContent`。窗口拥有帮助、底栏命令、执行、确认、关闭排空和调试会话释放。

`RuntimeNodeConfigurationControl` 根据节点草稿建立输入 schema、引用选项和 `DeclaredInputFieldsView` 工厂，再调用 `DesktopToolPresentation.CreateSettingsView` 创建 `DeclaredSettingsView`。

`DeclaredSettingsView.Rebuild` 根据 settings schema 建立 `DeclaredFieldsView`。输入内容通过 `InputContent` 或 `InputContentFactory` 进入。生产节点入口显式传入 `InputCompositionMode.FlatNative`，输入字段按原厂 page/section 直接移交给外层 `DeclaredFieldsView`。`NativeInputPlacementMap` 要求每个输入字段有原厂 page、section 和 order，并要求目标 page 已在设置页声明。

`DeclaredInputFieldsView` 只作为输入字段声明和生命周期适配器。生产路径使用 `FlatNative`，它只提供输入字段声明、编辑器、校验、数值读取和生命周期接缝。字段移交后保留原字段描述和编辑器实例，支持参数页重绑复用，不再把输入列表挂入外层参数行。

## 当前结构性问题

### 嵌套参数列表改变了原厂页拓扑

原厂输入图像、ROI、引用行和设置字段属于同一个参数页容器。当前自研把完整 `DeclaredInputFieldsView` 当作外层参数行，形成 `ParamListView → NativeParameterRow → DeclaredInputFieldsView → ParamListView`。这会产生额外分组标题、空锚点组和不同的滚动/可见性边界。

### 输入字段没有进入设置页的统一字段集合

`DeclaredSettingsView` 的 `_fields` 只持有 settings schema 的 `DeclaredFieldsView`。输入字段由 `_inputContent` 单独持有。数值读取需要额外拼接输入模型，捕获和校验也需要额外分支，导致输入字段不能自然参与同一套字段状态、提交和错误投影。

### 输入校验入口不对称

`DeclaredSettingsView.TryCapture` 调用 `_fields.TryValidate`。输入视图拥有自己的字段和写回逻辑，但没有与 settings 字段等价的统一 `TryValidate` 接缝。输入字段的提交通常发生在行事件或宿主回调中，无法用同一个捕获闸门证明全部字段已经提交。

### 输入生命周期与页生命周期分开

`DeclaredSettingsView` 对 `_inputContent` 单独保留、重绑和释放。`DeclaredFieldsView` 对 settings rows、sections 和 pages 另行管理。重绑、页惰性物化、外部组合和关闭时需要两套代次判断。

### 模板和资源职责分散

`DeclaredFieldTemplateRegistry` 只解析有限的字段模板。复杂工具通过 `InputContent`、`AdditionalPageContents` 和 `AdditionalAnchoredContents` 注入视觉，工具消费者因此承担了原厂参数根和公共控件本应承担的装配责任。

## 平面输入组装目标

建立一个公共 `NativeInputComposition`，其输出是参数页可直接消费的外部字段行集合，而不是一个嵌套参数列表。

### 最小接口

```text
NativeInputComposition
  PageKey
  Rows
    FieldId
    SectionKey
    Order
    Port
    Presentation
    DeclaredFieldRow
    State/Changed/Pending/Validation hooks
  ReadNumericModels(scope, fieldId, current)
  TryValidate(current)
  CaptureAcceptedFields()
  RefreshAcceptedInputs()
  Dispose()
```

### `DeclaredSettingsView` 责任

- 创建唯一参数页宿主。
- 把 settings fields 和 `NativeInputComposition.Rows` 作为同一页的声明行输入 `DeclaredFieldsView`。
- 对 settings 和输入字段使用同一个 generation、draft identity、revision 和 current guard。
- `TryCapture`、`HasPendingEdits`、`HasInvalidInput` 和数值读取统一覆盖两类字段。
- 重绑时一起退订和释放，不再保留独立的嵌套列表拥有者。

### `DeclaredFieldsView` 责任

- 按 page/section 建立唯一 `NativeParameterPage` 和 `ParamListView`。
- 接受 settings rows 和 external input rows 的统一声明集合。
- 外部输入行使用 section metadata、order、visibility、state projection 和 row margin 的同一套装配。
- 不创建外部 `ParamListView` 作为组合行。
- 保留 `AddAfterField` 作为工具专用视觉插槽，插槽内容不能承担正式输入字段集合。

### `DeclaredInputFieldsView` 迁移边界

- 当前类型先保留为兼容适配器。
- 新接口从它提取字段声明、行对象、引用会话和写回回调。
- 兼容模式继续提供旧的嵌套视觉，迁移模式只输出 `NativeInputComposition.Rows`。
- 兼容模式不能与迁移模式共享同一个字段实例或状态订阅。

## 迁移开关

`ToolSettingsViewOptions` 已增加明确的装配模式：

```text
InputCompositionMode.NestedLegacy
InputCompositionMode.FlatNative
```

正式 `RuntimeNodeConfigurationControl` 显式选择 `FlatNative`。缺少 page/section 映射时直接阻断窗口组装，阻断信息进入工具迁移矩阵，禁止通过旧嵌套视觉绕过原厂页签映射。

## 测试闸门

### 结构闸门

- 参数页只存在一个正式 `ParamListView`。
- 图像输入、ROI、引用和设置字段出现在同一页的 section group 中。
- 无嵌套 `DeclaredInputFieldsView` 参数列表。
- 隐藏锚点不会留下空 `NativeParameterGroup`。
- section 顺序、字段顺序和原厂快照一致。

### 状态闸门

- 输入编辑写回同一个 draft，并产生统一 revision。
- 输入字段的 pending、invalid、read error 和 write error 进入同一行状态投影。
- `TryCapture` 同时验证 settings 和输入字段。
- 数值模型读取在 `Field`、`CurrentPage` 和 `All` 三种范围下覆盖输入字段。

### 生命周期闸门

- 重绑时所有输入和 settings handler 都被退订。
- 旧 generation 的输入回调不能写入新 draft。
- 页惰性构建和重复 Tab 切换不会重复创建输入字段。
- 关闭时输入、ROI、引用窗口、命令和调试资源按同一释放链排空。

### 生产闸门

- FrameMean、BlobLabels、ContourMatch、If、ColorExtract 五种代表模式全部通过。
- 全量工具定向测试通过。
- Release 构建通过。
- Agent Desktop 控件树确认窗口尺寸、Tab、section、输入行和关闭释放。

### 已完成的框架验证

- `ParameterTemplateInteractionTests.Flat_input_composition_uses_one_native_parameter_list_and_validates_input` 通过。
- `ParameterTemplateInteractionTests.Flat_input_composition_preserves_input_only_section_on_existing_page` 通过，覆盖 Blob 类“输入 section 没有 settings 字段但 page 存在”的组装方式。
- `Plugin.Wpf` Release 编译通过，0 errors。完整解决方案的生产验证仍待代表工具接入后执行。
- Blob 真实调试链当前未闭合，`input.roi.area-type` 未出现在统一参数宿主的视觉树中。初步断点是 Blob 专用 ROI 组合仍依赖旧输入宿主锚点，已改为外层字段宿主接缝，待定向测试重新通过后才可记为生产闸门通过。

## 禁止的短路方案

- 不在每个工具中继续增加新的 `InputContent` 嵌套列表。
- 不用隐藏字段或空行伪造原厂 section。
- 不通过截图相似度替代字段状态和生命周期合同。
- 不在 `DeclaredSettingsView` 外部再建立第二套输入提交或校验链。

## 结论

平面输入组装是公共框架迁移的第一道代码闸门。它通过后，图像/ROI 工具、模板工具和动态字段工具才能共享同一套页拓扑、状态提交和生命周期。当前不应继续对单个工具的分组或模板样式做局部修补。
