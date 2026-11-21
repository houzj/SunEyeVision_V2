# 标准基本参数页

大多数工具的基本参数页共用同一套装配，工具不重复拼控件、不自行抑制行、不各自接位置修正。

## 共享入口

```csharp
var page = NativeStandardBasicPage.Compose(inputs, fields, options, new NativeStandardBasicPageSpec
{
    RoiFieldIds = MyRoi.Ports.Select(port => port.Id).ToArray(),
    PositionCorrectionFieldId = MyFields.PositionCorrectionEnabled,
    ComposeRoi = (i, attach, observe) => MyRoi.Add(i, attach, observe),
    ComposeAssist = MyAssist.Add,
    CreateRoiDocument = MyLocalRoi.Create
});
```

`NativeStandardBasicPage` 负责：

1. 抑制 ROI/Assist 端口对应的普通参数行；
2. 创建 ROI 组，插在「位置修正」前面；
3. 创建 ROI 编辑宿主和 ROI 文档；
4. 创建 Assist 组，插在「位置修正」后面；
5. 监听「位置修正」开关并同步 ROI/Assist 显隐；
6. 释放宿主、Assist 和编辑会话。

## 标准页结构

```text
图像输入（标签「输入源」）
ROI区域
  输出掩膜
  ROI区域类型
  ROI创建（绘制 / 继承）
  形状
  位置修正（开关）
  ↳ Assist 组
```

## 工具允许声明的差异

- 是否显示屏蔽区（`RoiGroupComposerSpec.ShowMaskGroup`）；
- 字段标签、分组标题、区域类型选项；
- ROI/Assist 字段身份和记录类型。

除此之外不得复制装配逻辑。差异必须有原厂证据，并写入该工具的 `unconfirmed`/证据文件。

## 字段身份变更

- 新增字段必须在同批更新测试；
- 旧测试方案缺字段时，工具在 `Bind` 前补默认值，避免“缺少必需参数”；
- 需要隐藏清除按钮时使用 `FieldLayout.ShowClearButton = false`，不要把字段改成可选来规避。

## 已接入

- IntensityMeasure（2026-11-20）。
- PixelCount（2026-11-20，改用同一入口，36/36 测试通过）。

## 常见错误

- 复制兄弟工具的 ROI/Assist 装配代码，而不是调用 `NativeStandardBasicPage`。
- 为了让「清除 ×」消失把字段改成可选；正确做法是 `FieldLayout.ShowClearButton = false`。
- 把「位置修正」开关和 ROI 组分开手写，导致开关与 Assist 显隐不同步。
- 新增设置字段后不处理已保存方案，启动时报「缺少必需参数」。
- 用控件创建顺序推断结果页分区顺序；顺序以 `FieldPresentation.Order` 为准。
