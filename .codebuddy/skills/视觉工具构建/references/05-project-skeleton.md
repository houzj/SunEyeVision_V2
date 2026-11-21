# 工程骨架（csproj 模板 + slnx 登记 + 构建验证衔接）

## 1. csproj 模板（单项目双角色，直接复制真实样板）

> 真实样板：`tools/SunEyeVision.PixelCount/SunEyeVision.Tool.PixelCount.csproj`。
> 一个 csproj 同时声明 **Runtime** 与 **Editor** 两个 `ToolComponent`，程序集名为 `*.Runtime.dll`；不再有独立 `.Wpf` 工程。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>SunEyeVision.Tool.Xxx.Runtime</AssemblyName>
    <RootNamespace>SunEyeVision.Tool.Xxx.Runtime</RootNamespace>
    <ToolComponentId>SunEyeVision.Tool.Xxx.Runtime</ToolComponentId>
    <ToolComponentToolId>Xxx</ToolComponentToolId>
    <ToolComponentRole>Runtime</ToolComponentRole>
    <ToolComponentEntryType>SunEyeVision.Tool.Xxx.Runtime.XxxTool</ToolComponentEntryType>
  </PropertyGroup>
  <ItemGroup>
    <!-- 原厂证据 XML 作为嵌入资源；文件名与 LogicalName 按工具证据调整 -->
    <EmbeddedResource Include="Resources/XxxParameters.xml" LogicalName="Xxx.XxxParameters.xml" />
    <EmbeddedResource Include="Resources/XxxAlgorithmTab.xml" LogicalName="Xxx.XxxAlgorithmTab.xml" />
    <EmbeddedResource Include="Resources/XxxTool.xml" LogicalName="Xxx.XxxTool.xml" />
    <EmbeddedResource Include="Resources/XxxDisplay.xml" LogicalName="Xxx.XxxDisplay.xml" />
    <EmbeddedResource Include="Resources/ToolItemInfo.xml" LogicalName="Xxx.ToolItemInfo.xml" />
    <ProjectReference Include="../../src/Plugin.Contracts/SunEyeVision.Plugin.Contracts.csproj" />
    <ProjectReference Include="../../src/Plugin.Wpf/SunEyeVision.Plugin.Wpf.csproj"><Private>false</Private></ProjectReference>
    <ProjectReference Include="../../src/SunEyeVision.HalAdapter/Runtime/SunEyeVision.HalAdapter.Runtime.csproj" />
    <Reference Include="halcondotnetxl"><HintPath>../../libs/halcon/net/halcondotnetxl.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
  <ItemGroup>
    <!-- 同一程序集声明 Editor 角色（双角色共享程序集） -->
    <AssemblyAttribute Include="SunEyeVision.Plugin.Contracts.ToolComponentAttribute">
      <_Parameter1>SunEyeVision.Tool.Xxx.Wpf</_Parameter1>
      <_Parameter2>Xxx</_Parameter2>
      <_Parameter3>Editor</_Parameter3>
      <_Parameter4>global::SunEyeVision.Plugin.Contracts.FoundationContract.Major</_Parameter4>
      <_Parameter4_IsLiteral>true</_Parameter4_IsLiteral>
      <_Parameter5>global::SunEyeVision.Plugin.Contracts.FoundationContract.Minor</_Parameter5>
      <_Parameter5_IsLiteral>true</_Parameter5_IsLiteral>
      <_Parameter6>SunEyeVision.Tool.Xxx.Wpf.XxxEditorFactory</_Parameter6>
      <_Parameter7>Xxx</_Parameter7>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

### 硬性要点（rule-016）

| 要点 | 值 | 原因 |
|---|---|---|
| 单项目 | 一个 csproj 同时声明 Runtime + Editor | 发布合同按"一程序集一个 Runtime/Editor 角色"校验；缺任一角色 → 组件封存/编辑器注册失败 |
| `AssemblyName` | `SunEyeVision.Tool.Xxx.Runtime`（带 `.Runtime`） | 与 `ToolComponentId` 一致，载荷按此登记 |
| `ToolComponentRole` | PropertyGroup 写 `Runtime` + `AssemblyAttribute` 写 `Editor` | 双角色共享同一程序集 |
| `EmbeddedResource` | 原厂 XML 按证据归档，LogicalName 与工具约定一致 | 生成器只填证据明确的内容；无证据不造声明文件（见 [07](07-evidence-pipeline.md) §4） |
| `Private` | 宿主依赖一律 `false` | 宿主依赖不复制进插件目录（否则插件目录膨胀/版本冲突） |
| `PlatformTarget` | `x64` | 对齐 Halcon native |
| halcondotnetxl | HintPath `../../libs/halcon/net/halcondotnetxl.dll`，`Private=false` | 与真实样板一致（旧 `dev/Debug` 路径已弃用） |
| 引擎引用 | **仅引所需**（HalAdapter.Runtime / CvAdapter） | 不污染插件输出目录 |

> ⚠️ 旧模板（`IsToolPlugin=true` / 独立 `.Wpf` 工程 / `dev/Debug/halcondotnetxl.dll`）**已弃用，禁止照抄**。禁止在工具 csproj 写 OpenCvSharp `PackageReference`（除非确实走 Cv 引擎）——会污染插件输出目录（rule-016 §5.2）。

## 2. slnx 登记（必做，漏登记 = IDE 不编译）

新工具工程必须登记到 **两个** slnx：

| 文件 | 用途 | 是否必登 |
|---|---|---|
| `SunEyeVision.slnx` | 完整解决方案（src + tools + tests），构建/测试默认目标 | ✅ 必登 |
| `SunEyeVision.App.slnx` | App 专用视图（src + tools，发布构建用） | ✅ 必登 |

```xml
<!-- 放在 <Folder Name="/tools/"> 下，与同类工具并列 -->
<Project Path="tools/SunEyeVision.Tool.XxxMeasure/SunEyeVision.Tool.XxxMeasure.csproj" />
```

**自检命令**（登记后立刻验证）：

```powershell
Select-String -Path SunEyeVision.slnx,SunEyeVision.App.slnx -Pattern "XxxMeasure" |
    ForEach-Object { "$($_.Filename): $($_.Line.Trim())" }
```

> 历史教训：`LineLineMeasure` 曾只登记 `SunEyeVision.App.slnx`，`SunEyeVision.slnx` 漏登——IDE 打开完整解决方案时看不到该工程、不参与构建。

## 3. 构建验证衔接（`[skill:构建验证]`）

| 时机 | 动作 | 门禁 |
|---|---|---|
| 新建 csproj 后 | 单 csproj 构建（区分 MSBuild 配置错误 vs 业务代码错误） | 0 error |
| 代码主体完成后 | 工具 csproj 构建 | 0 error |
| slnx 登记后 | 全量构建 `dotnet build SunEyeVision.slnx -c Debug` | 0 error |
| 构建前 | **关闭运行中的 `SunEyeVision.UI.exe`**（锁 SDK.dll 会构建失败） | — |

**构建输出布局**（2026-09 起）：

| 项目类型 | Debug 输出 |
|---|---|
| 主程序/框架（非 `.Tests`、非 `IsToolPlugin`） | `dev\Debug\` |
| 工具插件（`IsToolPlugin=true`） | `dev\Debug\plugins\<ToolAssemblyName>\` |
| 测试项目（`*.Tests.csproj`） | `tests\bin\Debug\`（共享输出，`AppendTargetFrameworkToOutputPath=false`） |

**验证部署**（`dev\` 被 `.gitignore` 屏蔽，`search_*` 工具看不到 → **必须用 PowerShell**，rule-017）：

```powershell
Get-ChildItem -Path "d:\MyWork\SunEyeVision_V2\dev\Debug\plugins" -Directory |
    Where-Object Name -like "*Xxx*" | Select-Object -ExpandProperty FullName
```

> 目录名以程序集 `AssemblyName`（单项目通常带 `.Runtime`）为准，不凭工具目录名猜测。

插件目录应**只有**主程序集 + pdb（+ 资源），不应出现 `halcondotnetxl.dll` / `OpenCvSharp.dll`。

## 4. 运行验证

构建完成后从 `dev\Debug\SunEyeVision.UI.exe` 启动（**不要**用 `dotnet run --project src\UI\...`——那只构建主程序，加载的是旧插件 DLL）。

```powershell
# 全量构建（等价 build-fast.bat all）：主程序 + 全部插件 → dev\Debug\plugins\
dotnet build SunEyeVision.App.slnx -c Debug

# 启动
Start-Process "d:\MyWork\SunEyeVision_V2\dev\Debug\SunEyeVision.UI.exe"
```

## 5. 全链路验收衔接（`[skill:方案测试]`）

工具完成后移交 `[skill:方案测试]`，验收项：

| 类别 | 验收点 |
|---|---|
| 参数订阅 | 逐字段绑定上游输出 → 运行取到上游值（常量/绑定两态） |
| 双来源 | 绘制路径与订阅路径结果一致 |
| 判定 | 上下限 OK/NG 双向验证；启用项无值 → NG（rule-015） |
| 双卡尺互斥 | 激活切换、绘制时另一元素工具选择清除 |
| 生命周期 | 打开→运行→关闭→重开：无残留、无重复订阅 |
| 引擎引用独立 | 插件目录不含 halcon 托管 dll / OpenCvSharp |
| 结果分组 | 结果树分组顺序符合 `ModuleResultGroupOrder`（见 references/04） |
| 算法等价 | 有 GoldenCase 才判 `AlgorithmEquivalent`；无基准保持 `AlgorithmUnconfirmed` 并登记台账（见 [07](07-evidence-pipeline.md) §7） |
