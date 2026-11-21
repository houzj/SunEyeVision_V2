# 新工具最短路径

目标：按固定顺序把工具从证据带到可运行，减少来回补洞。

## 0. 选模板

- 数值/范围/结果表类：抄 PixelCount
- ROI、AssistGroup、结果叠加类：抄 Blob
- 模板资源、特征面板类：抄 ContourMatch / GrayMatch

只抄结构，不抄业务字段。

## 1. 单项目骨架

```text
tools/SunEyeVision.<Tool>/
├─ SunEyeVision.Tool.<Tool>.csproj
├─ <Tool>Tool.cs
├─ Algorithms/
├─ Parameters/
├─ Results/
├─ Editor/
└─ Resources/
```

csproj 要点见 `single-project-layout.md`。

## 2. 先写声明，不写 UI

1. `Resources/<Tool>Parameters.xml`：页签、分组、字段 id、默认值、显隐、成对关系
2. `Parameters/<Tool>Settings.cs`：唯一持久化模型
3. `Parameters/<Tool>ParameterDefinition.cs`：XML → 字段读取/材质化
4. `Parameters/<Tool>Fields.cs`：稳定字段 id 常量

声明完成即可跑声明测试，不需要打开窗口。

## 3. 工具入口

`<Tool>Tool.cs` 实现：

- `Describe()`：输入/输出端口
- `Validate(settings)`
- `CreateSession()`
- `SettingsSchema` / `ReadSettings` / `MaterializeSettings`
- 如需要：`IToolResultPresentationProvider`、`IToolResultDisplayProvider`

## 4. 结果声明

- `Results/<Tool>Output.cs`：输出端口与结果树节点
- `Results/<Tool>ResultDisplay.cs`：`ResultDisplayDescriptor` + `CapturedAppearance`

先保证结果表能显示，再做图像叠加。

## 5. 编辑器

- `Editor/<Tool>EditorFactory.cs`
- 默认用 `DeclaredSettingsView`
- 专属面板只做命令和接缝，不手写输入行
- 数量驱动的行用固定槽位 + `SetFieldRowsVisibility`

## 6. 注册

- `SunEyeVision.slnx`
- `product/ToolComponents.props`（Runtime/Editor 两条）
- `src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json`
- `docs/evidence/tool-batch-manifest-20261120.json`

## 7. 测试

- 声明测试：端口集合、页签顺序、成对关系、默认值
- 执行测试：默认、边界、非法输入、引用未解析
- 探针：`ControlNamingProbe` 增加该工具条目

## 8. 验证与封存

```powershell
dotnet test tests/SunEyeVision.Tools.LogicData.Tests/... --filter <Tool>
dotnet run --project tests/ControlNamingProbe/...
powershell -File .opencode/skills/tool-production/scripts/Invoke-BuildSealLaunch.ps1 -Project src/UI/SunEyeVision.UI.csproj
```

启动后逐项截图：基本参数、运行参数、结果显示、底栏、关闭释放。

## 常见返工点

- 先写 UI 再补声明 → 字段身份对不上
- 动态数量用动态 schema → 加载/重绑/成对提交三处缺口
- 只改工具不改测试 → 专项测试红灯被当成回归
- 改了 Release 不封存 → 启动页卡住
