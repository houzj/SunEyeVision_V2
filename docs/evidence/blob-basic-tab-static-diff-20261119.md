# Blob 基本参数页静态差异清单

- 复核日期：2026-11-19
- 原厂基线：VisionMaster 4.4.0 Build20250516
- 原厂证据：`C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Location\IMVSBlobFindModu\IMVSBlobFindModuAlgorithmTab.xml`
- 组装证据：`docs/07-工具逆向/decompiled/Groups/Group.xml`
- 自研消费者：`tools/SunEyeVision.BlobAnalysis/Runtime/BlobTool.cs`、`tools/SunEyeVision.BlobAnalysis/Wpf/Views/BlobRoiUi.cs`、`src/Plugin.Wpf/Presenters/Parameters/DeclaredSettingsView.cs`、`src/Plugin.Wpf/Fields/DeclaredFieldsView.cs`

## 原厂基本页顺序

1. `ImageSourceGroup`
2. `OutputMaskImageGroup`
3. `InputMaskImageGroup`
4. `RoiSelectGroup`
5. `RoiType`
6. `InheritWayGroup2`
7. `ExternROITypeGroup`
8. `AssistGroup`

`AlgorithmTab.xml` 的 `Tab_Basic Params` 中，`ImageSourceGroup` 位于 `Tab_ROI Area` 类别之前。ROI 类别内部的 `GroupLinkItem` 顺序是输出掩膜、输入掩膜、ROI 选择、ROI 形状、继承方式、外部 ROI、辅助/位置修正。

## 自研静态实现

- `BlobTool.BuildExecutionSchema` 声明输入顺序为 `input.image`、`input.pose`、本地 ROI、继承 ROI、图像掩膜和位置修正叶字段。
- `BlobParameterDefinition.InputPresentation` 把 `input.image` 映射为 `基本参数/图像输入`，但宿主用 `DeclaredSettingsView.AttachInputContent` 在参数页已经构造后再安装正式输入行。
- `BlobRoiUi.Add` 把输出掩膜、ROI 类型、ROI 来源和屏蔽区组合放到一个 `NativeRoiUiDraftControls` 中，并以 `input.image` 作为插入锚点。
- `DeclaredFieldsView.AddToGroupCore` 对尚未存在的输入专属分组统一追加到页面末尾，无法表达原厂 `ImageSourceGroup` 的首位位置。
- `BlobUiComposition.BuildPage` 在正式输入尚未安装时调用 `AddAfterField("input.image", ...)`。该动作被排队到页构造阶段，随后输入才安装，原有排队动作没有在正式输入注册后重放。

## 差异与修复决策

| ID | 原厂事实 | 当前差异 | 证据级别 | 修复 |
|---|---|---|---|---|
| BLOB-BASIC-001 | ImageSourceGroup 位于 ROI 类别之前 | 图像来源输入追加在页面末尾 | 原厂 XML + 当前源码 | `AddToGroupCore` 对显式 `index` 的新分组按索引插入，保证图像输入首位 |
| BLOB-BASIC-002 | ROI 组按 XML 链接顺序呈现 | 组合依赖输入字段注册时序，页面初建时输入尚未注册 | 当前框架源码 + 当前 Blob 组装源码 | `RegisterExternalField` 注册正式输入后重放该字段的待处理锚定动作 |
| BLOB-BASIC-003 | 输出掩膜、输入掩膜、ROI 选择、形状、继承、外部 ROI、Assist 分组存在独立原件 | 自研把多个字段收拢为一个组合控件 | 原厂 Group.xml + 当前 `NativeRoiUiDraftControls` | 本轮保留现有共享原件控件，先纠正宿主顺序和生命周期。独立分组视觉迁移列为后续证据项，不凭猜测拆控件 |
| BLOB-BASIC-004 | 原厂 `AssistGroup` 位于最后 | 自研位置修正字段属于设置页首个 ROI 分组 | 原厂 Group.xml + `BlobParameters.xml` | 通过图像输入首位和 ROI 组合锚定修正相对顺序。是否需要独立 Assist 模板仍需运行截图确认 |
| BLOB-BASIC-005 | 原厂输入掩膜组包含 ROI Area Type 和 Mask Image，并由触发器控制显隐 | 自研已有 area、mask image、output mask 和继承模式状态，但它们的安装依赖宿主队列 | 原厂 Group.xml + `BlobRoiUi.cs` | 先修复安装顺序和待处理动作重放，再测试显隐与提交链 |

## 本轮边界

本轮只修改有静态证据的通用页组装顺序和待处理动作重放，不新增原厂未确认的控件、不改变算法默认值、不启动 Agent Desktop。代码修复后先跑 Blob UI 回归和 Release 构建，再做 Agent Desktop 复核。
