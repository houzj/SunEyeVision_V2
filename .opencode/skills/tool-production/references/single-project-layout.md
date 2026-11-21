# 单项目结构与迁移

目标是「一个工具 = 一个产品项目」。原厂的算法 DLL / Cs 桥接 DLL / XML 是**职责发布边界**，不是要求本方复制程序集数量。不得伪造 `Algorithm.dll` 或 `Bridge.dll`。

## 目录

```text
tools/SunEyeVision.<Tool>/
├─ SunEyeVision.Tool.<Tool>.csproj
├─ <Tool>Tool.cs                    # 工具入口（ITool 实现）
├─ Algorithms/                      # 算法调用与组合
├─ Parameters/                      # 参数字段、设置、ROI 文档
├─ Results/                         # 结果模型与显示投影
├─ Editor/                          # EditorFactory、专属面板、XAML
└─ Resources/                       # 原厂 XML、图标、模板资源
```

参考实现：`tools/SunEyeVision.PixelCount/`（样板）、`tools/SunEyeVision.Tool.BlobAnalysis/`（复杂链）。

## csproj 必备项

```xml
<PropertyGroup>
  <TargetFramework>net9.0-windows</TargetFramework>
  <PlatformTarget>x64</PlatformTarget>
  <UseWPF>true</UseWPF>
  <AssemblyName>SunEyeVision.Tool.<Tool>.Runtime</AssemblyName>
  <RootNamespace>SunEyeVision.Tool.<Tool>.Runtime</RootNamespace>
  <ToolComponentId>SunEyeVision.Tool.<Tool>.Runtime</ToolComponentId>
  <ToolComponentToolId><Tool></ToolComponentToolId>
  <ToolComponentRole>Runtime</ToolComponentRole>
  <ToolComponentEntryType>SunEyeVision.Tool.<Tool>.Runtime.<Tool>Tool</ToolComponentEntryType>
</PropertyGroup>
```

要求：

- 引用 `Plugin.Contracts`；引用 `Plugin.Wpf` 时设 `<Private>false</Private>`。
- 第三方算法（HALCON）引用设 `<Private>false</Private>`，由发布规则部署。
- 原厂 XML 以 `EmbeddedResource` 嵌入并固定 `LogicalName`（如 `PixelCount.PixelCountParameters.xml`）。
- 同一程序集通过 `ToolComponentAttribute` 声明 **Runtime 和 Editor 两个角色**：Runtime 用属性组，Editor 用 `AssemblyAttribute` 项，`_Parameter1` 为 Editor 组件 ID，`_Parameter6` 为 EditorFactory 类型全名，`_Parameter7` 为 ToolId。
- 命名空间统一 `SunEyeVision.Tool.<Tool>.Runtime.*`，Editor 代码也在同一程序集内，不再另建 Wpf 程序集。

## 三处注册必须同步

| 位置 | 内容 |
|---|---|
| `SunEyeVision.slnx` | 只登记单项目路径，删除旧 `<Tool>.Runtime.csproj` / `<Tool>.Wpf.csproj` 条目 |
| `product/ToolComponents.props` | 两条 `ProductToolComponent`：Runtime 一条；Editor 一条且 `SharedAssembly=true`、`CompanionToolId` 指向本工具 |
| `src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json` | 工具箱项 `ToolId` + `MappingVerified=true`，工具名对应原厂模块 |

产品组件总数当前为 24（12 工具 × 2 角色）；新增/迁移工具后同步更新门禁脚本里的期望数量与清单。

## 迁移顺序（固定）

1. 发布合同支持共享程序集双声明（已具备）。
2. 在工具目录建立 `Algorithms/Parameters/Results/Editor/Resources`，把旧 `Runtime/`、`Wpf/` 源码按职责移入。
3. 新建单项目 csproj，保留原 AssemblyName（Runtime 名），补 Editor 的 `AssemblyAttribute`。
4. 更新 slnx、props、工具箱目录；更新 `tool-batch-manifest-20261120.json` 的 `runtimeProject` / `wpfProject` 为同一路径，并记录 `architecture.singleProject=true`。
5. 构建工具项目 → 全量 Release → 确认载荷中只出现一个工具 DLL 且含 Runtime/Editor 双声明。
6. 跑 G2/G4/G5；通过后删除旧项目目录与残留锁文件。
7. 中文提交，提交信息说明迁移范围与验证结论。

## 注意

- 工具内部 `Editor/` 仍只允许依赖本工具 Runtime 语义 + `Plugin.Wpf` 公共合同，不得反向依赖 UI/App/Execution。
- 不要为了「结构像原厂」拆出空算法程序集；算法实现留在 `Algorithms/`。
- `packages.lock.json` 随项目迁移一起移动，否则 `--no-restore` 构建失败。
- 迁移期间的半成品 csproj 不要留在仓库根下或未登记状态，验证不通过就回退该工具。
