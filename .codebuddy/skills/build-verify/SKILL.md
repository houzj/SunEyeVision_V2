# build-verify

## 描述

MSBuild 构建验证技能。用于验证 `csproj` / `slnx` 的 MSBuild 配置正确性（`OutDir` 解析、Reference 解析、属性继承、工具插件输出污染拦截），区分 MSBuild 配置错误与业务代码错误。触发场景：项目配置重构后验证、csproj 改动后验证、新工具 csproj 创建后验证、`Directory.Build.props/targets` 改动后验证。

## 触发关键词

构建验证、build verify、MSBuild 验证、csproj 验证、OutDir 验证、参考解析、Reference 解析、编译验证、slnx、sln 编译、build、构建、编译、plugins 污染、工具插件输出

## 核心原则

1. **优先 slnx**：使用 `.slnx` 格式编译（XML，比 `.sln` 二进制格式更可靠）
2. **优先单 csproj**：验证配置时只编译单个 csproj（快速反馈）
3. **全量编译**仅在集成阶段
4. **MSBuild 配置错误 vs 业务代码错误**必须区分
5. **工具插件编译后必须验证 plugins/ 目录干净**（无 NuGet 污染）

## 工作流

### 步骤 1：选择编译入口

```powershell
# 验证单个 csproj 的 MSBuild 配置（推荐起点）
dotnet build "tools/SunEyeVision.Tool.XXX/SunEyeVision.Tool.XXX.csproj" `
  --configuration Debug -v:minimal -nologo

# 验证 slnx（集成阶段）
dotnet build SunEyeVision.slnx --configuration Debug -v:minimal -nologo
```

### 步骤 2：捕获输出

PowerShell 直接 `2>&1` 经常被截断，**必须**先落盘到日志文件：

```powershell
dotnet build "..." --configuration Debug -v:minimal -nologo 2>&1 |
  Out-File -FilePath "verify.log" -Encoding utf8
:"EXIT=$LASTEXITCODE" | Out-File -FilePath "verify.log" -Append -Encoding utf8
```

### 步骤 3：用 UTF-8 无 BOM 读取日志

```powershell
[System.IO.File]::ReadAllText("verify.log", [System.Text.UTF8Encoding]::new($false))
```

### 步骤 4：分析错误

**MSBuild 配置错误**（必须修复）：
- `error MSB4xxx`：MSBuild 内部错误（target 不存在、属性未定义等）
- `error MSB5xxx`：项目文件错误（格式、引用、属性）
- `error CS0xxx`：C# 编译错误（语法、类型、引用）
- `warning CS1xxx`：缺失引用警告

**业务代码错误**（不属于本次验证范围）：
- `error CS0508` / `error CS0246` / `error CS0103`：业务代码层方法签名不匹配、类型找不到、命名空间缺失
- 这类错误**只标记，不修复**（除非用户明确要求）

### 步骤 5：轻量属性检查（推荐）

不编译也能验证 `OutDir` 等关键属性解析：

```powershell
dotnet msbuild "tools/SunEyeVision.Tool.XXX/SunEyeVision.Tool.XXX.csproj" `
  -getProperty:OutDir,IntermediateOutputPath -nologo
```

## 工具插件输出污染验证（重要）

工具编译成功后，**必须**验证 `plugins/<ToolName>/` 目录是否符合 rule-016 §5.2 标准。

### 验证步骤

```powershell
$ToolName = "SunEyeVision.Tool.XXX"
$PluginPath = "d:\MyWork\SunEyeVision_V2-script\dev\Debug\plugins\$ToolName"

# 1. 检查主程序集
Get-ChildItem -Path $PluginPath -Filter "*.dll" -File

# 2. 检查无 NuGet 污染（应为空数组）
Get-ChildItem -Path $PluginPath -File |
  Where-Object { $_.Name -notmatch "^$ToolName\.(dll|pdb)$" -and $_.Name -ne "halcondotnetxl.dll" }

# 3. 检查无语言子目录
Get-ChildItem -Path $PluginPath -Directory |
  Where-Object { $_.Name -in @("cs","de","es","fr","it","ja","ko","pl","pt-BR","ru","tr","zh-Hans","zh-Hant") }

# 4. 检查无 .deps.json
Get-ChildItem -Path $PluginPath -Filter "*.deps.json" -File

# 5. 检查无 wpftmp 残留
Get-ChildItem -Path $PluginPath -Directory -Filter "*_wpftmp"

# 6. 检查无 runtimes/ 残留
Get-ChildItem -Path $PluginPath -Directory -Filter "runtimes"
```

### 预期结果

工具插件目录**只应包含**：
- `<ToolName>.dll` + `<ToolName>.pdb`
- 工具 csproj 显式 `<None>` + `<CopyToOutputDirectory>` 复制的资源

### 拦截机制（参考 rule-016 §5）

`Directory.Build.targets` 8 步拦截,清理 5 类 MSBuild 独立复制通道:
- Step 1: ReferenceCopyLocalPaths 移除 NuGet framework dll
- Step 2: 移除 Content/None Link 包含 'runtimes' 的原生 dll
- Step 4: 删除 .deps.json
- Step 5: 删除 13 种语言子目录
- Step 7: 删除 RID 解析空 runtimes/ 目录
- Step 8: 删除 WPF 临时构建 wpftmp/ 目录

## 验证清单

执行验证后**必须**报告：

- [ ] MSBuild 配置错误数（应 = 0）
- [ ] 业务代码错误数（应 = 0，或用户已知）
- [ ] `OutDir` 解析结果（路径是否符合预期）
- [ ] 引用解析（halcon / Plugin.SDK / CvEngine 是否能找到）
- [ ] 工具插件 plugins/ 目录无 NuGet 污染
- [ ] 工具插件 plugins/ 目录无 .deps.json / 语言子目录 / wpftmp / runtimes

## 禁止行为

- **禁止**对未加载的项目强行编译（浪费时间、引入噪声错误）
- **禁止**用 `Console.WriteLine` 输出中文（rule-013）
- **禁止**在 PowerShell 用 `Get-Content` 读文件（rule-013，编码问题）
- **禁止**混用 `.sln` 和 `.slnx`（rule-016）

## 参考

- rule-016：MSBuild 项目配置规范（含 8 步拦截说明）
- rule-013：文件编码与写入规范
- rule-008：报错优先 / 零兼容原则
