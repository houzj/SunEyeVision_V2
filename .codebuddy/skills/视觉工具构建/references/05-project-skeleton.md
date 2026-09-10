# 工程骨架（csproj 模板 + slnx 登记 + 构建验证衔接）

## 1. csproj 模板（新建工具工程直接复制）

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <OutputType>Library</OutputType>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <RootNamespace>SunEyeVision.Tool.XxxMeasure</RootNamespace>
    <AssemblyName>SunEyeVision.Tool.XxxMeasure</AssemblyName>
    <IsToolPlugin>true</IsToolPlugin>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Plugin.SDK\SunEyeVision.Plugin.SDK.csproj">
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>

  <ItemGroup>
    <Reference Include="halcondotnetxl">
      <HintPath>..\..\dev\Debug\halcondotnetxl.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <!-- Hal 实现（HalImage/GetHalconImage/HOperatorSet 扩展）；按实际引擎需要增删 -->
    <ProjectReference Include="..\..\src\SunEyeVision.HalAdapter\SunEyeVision.HalAdapter.csproj">
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>

</Project>
```

### 硬性要点（rule-016）

| 要点 | 值 | 原因 |
|---|---|---|
| `IsToolPlugin` | `true` | 输出到 `dev\Debug\plugins\<ToolAssemblyName>\`（唯一部署源） |
| `Private` | 一律 `false` | 宿主依赖不复制进插件目录（否则插件目录膨胀/版本冲突） |
| `PlatformTarget` | `x64` | 对齐 Halcon native |
| `ImplicitUsings` | `disable` | 项目统一风格（显式 using） |
| `Nullable` | `enable` | — |
| `UseWPF` | `true` | 调试控件为 WPF |
| halcondotnetxl | `HintPath` 指 `..\..\dev\Debug\halcondotnetxl.dll`，`Private=false` | 与 Plugin.SDK 先例一致，保证 P/Invoke 与 native 同目录 |
| 引擎引用 | **仅引所需** | 线-线先引 HalAdapter；后续工具按实际 measure 需要收敛 |

> ⚠️ **禁止**在工具 csproj 里写 OpenCvSharp `PackageReference`（除非确实走 Cv 引擎）——会污染插件输出目录（rule-016 §5.2）。

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
Get-ChildItem -Path "d:\MyWork\SunEyeVision_V2\dev\Debug\plugins\SunEyeVision.Tool.XxxMeasure" -File |
    Select-Object Name, Length
```

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
