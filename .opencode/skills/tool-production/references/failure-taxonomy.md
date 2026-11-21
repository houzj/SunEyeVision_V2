# 失败分类与修复顺序

失败先归类，再修。归类错误会导致改错层、扩大范围。

## 1. 入口（Entry）

症状：工具不出现在工具箱；拖入画布后无名/重复；`ToolComponentAttribute` 解析失败；组件准入被拒。

先查：

- `product/ToolComponents.props` 是否有 Runtime/Editor 两条且 ToolId 一致
- 工具箱 `MappingVerified` 与 ToolId
- 程序集是否进载荷、双声明是否完整
- `RuntimeEditorProductionProbe` 的静态解析输出

修：注册与声明。不改公共加载器，除非证据表明问题在准入合同本身（此时先停下报告）。

## 2. 声明（Declaration）

症状：字段缺失、默认值错误、范围错误、页签数量/顺序不符、输入端口缺失、结果字段缺失。

先查：工具 `Resources/*.xml` 与原厂 Algorithm/AlgorithmTab/Display XML 的逐项对照；`DeclaredParameterSchema` 合并结果；清单里的字段数。

修：工具声明与资源。不得用伪造字段占位页签；空页用 `PageOnlyPages` 表达输入归属。

## 3. 模板（Template）

症状：控件外观或交互与原厂不符；专属面板不出现；下拉/滑条/表格行结构不对。

先查：原厂资源键、控件类型、模板资源是否已迁移到 `Plugin.Wpf/Themes/Native`；专属面板是否挂到外部内容槽。

修：优先工具 Editor 接缝，其次公共模板（仅当证据表明多个工具共用且当前实现错误）。If 这类动态行必须有 `AddIO/RemoveIO` 证据后才能动。

## 4. 组装（Composition）

症状：参数页 Bind 阶段异常、页签空白、Ready 状态超时、ROI 字段读取为空、输入行缺端口。

先查：`ParameterPresentationTree` 页序、`NativeInputComposition`、`NativeInputPlacementMap`、`DeclaredInputFieldsView` 的异常隔离。

修：组装映射。修完必须重跑 `ControlNamingProbe` 全量断言，不能只看当前工具。

## 5. 执行（Execution）

症状：执行抛异常、结果为空、状态不对、取消不生效、连续执行结果漂移、引用未解析。

先查：Runtime 会话、参数快照、输入帧租约、`ToolOutcome.SelectedBranches`（分支工具）、结果声明与实际输出是否一致。

修：工具 Runtime 或已固定执行合同。算法等价问题单独记录，不用「能跑」当通过。

## 6. 释放（Release/Lifetime）

症状：窗口关闭后进程不退、程序集被占用、下次启动校验失败、事件重复触发、迟到回调写入已关闭会话。

先查：租约释放、事件退订、会话 Dispose、结果版本退役、载荷封存哈希。

修：生命周期与封存流程。定向编译后必须重新封存。

## 升级规则

- 同一阶段连续两轮失败 → 停止，登记差异条目，说明缺少的证据或需要决策的合同。
- 同一现象跨两个阶段 → 以最先出现的阶段为准，不并行改两层。
- 修复引入新失败 → 先回退该修复，再重新归类。
