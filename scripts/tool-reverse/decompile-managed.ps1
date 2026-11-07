#requires -Version 5.1
<#
.SYNOPSIS
    把原厂工具的托管程序集（<Plugin>Cs.dll）反编译为 C# 并入库。

.DESCRIPTION
    VisionMaster 的工具模块是「原生 DLL + 托管包装」双程序集：
      IMVSxxxModu.dll    原生 C++，无托管元数据，只能汇编级分析（Process/算法体在这里）
      IMVSxxxModuCs.dll  托管程序集，含 Tool / Param / Result / RoiManager 等类

    本脚本只处理托管那一半。它给出的是参数模型、结果模型、工具生命周期入口、显示配置，
    不包含算法本体。任何据此得出的结论都不得声称已证算法。

    输出目录按职责命名，不直接沿用原厂目录名；来源程序集、SHA-256、抓取时间写在 README。

.PARAMETER Tool
    工具卡 ID（docs/07-工具逆向/tools/<toolId>），可传多个或逗号分隔。

.PARAMETER All
    对全部有原件路径的卡片执行。

.PARAMETER OutRoot
    输出根目录，默认 docs/07-工具逆向/decompiled/Managed。

.EXAMPLE
    powershell -File scripts/tool-reverse/decompile-managed.ps1 -Tool imvslinefindmodu,imvscirclefindmodu
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string[]]$Tool,
    [switch]$All,
    [string]$OutRoot
)

$ErrorActionPreference = 'Stop'

if ($Tool) {
    $Tool = @($Tool | ForEach-Object { $_ -split '[,;]' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}
if (-not $Root) { $Root = Join-Path $PSScriptRoot '..\..\docs\07-工具逆向' }
if (-not (Test-Path $Root)) { throw "工具卡根目录不存在: $Root" }
$root = (Resolve-Path $Root).Path
$toolsDir = Join-Path $root 'tools'
if (-not $OutRoot) { $OutRoot = Join-Path $root 'decompiled\Managed' }
if (-not (Test-Path $OutRoot)) { New-Item -ItemType Directory -Path $OutRoot -Force | Out-Null }

$utf8 = [System.Text.UTF8Encoding]::new($false)

$ilspy = Get-Command ilspycmd -ErrorAction SilentlyContinue
if (-not $ilspy) {
    throw 'ilspycmd 未安装。先执行：dotnet tool install --global ilspycmd'
}

# 原厂模块名 -> 职责目录名：去掉 IMVS 前缀与 Modu 后缀
function Get-DutyName {
    param([string]$Plugin)
    $n = $Plugin
    $n = $n -replace '^IMVS', ''
    $n = $n -replace 'Modu$', ''
    $n = $n -replace 'ModuVA$', 'VA'
    if (-not $n) { $n = $Plugin }
    return ($n -replace '[^\w\-]', '')
}

$records = New-Object 'System.Collections.Generic.List[object]'
$skipped = New-Object 'System.Collections.Generic.List[object]'

$dirs = Get-ChildItem -Path $toolsDir -Directory | Sort-Object Name
if ($Tool -and $Tool.Count -gt 0) {
    $want = @{}
    foreach ($t in $Tool) { $want[$t] = $true }
    $dirs = @($dirs | Where-Object { $want.ContainsKey($_.Name) })
    if ($dirs.Count -eq 0) { throw "未找到工具卡: $($Tool -join ',')" }
}

foreach ($dir in $dirs) {
    $toolId = $dir.Name
    if ($toolId.StartsWith('_')) { continue }
    $evPath = Join-Path $dir.FullName 'evidence.json'
    if (-not (Test-Path $evPath)) { continue }
    $ev = Get-Content $evPath -Raw -Encoding UTF8 | ConvertFrom-Json

    if (-not $ev.originalSourcePath) {
        $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = '卡片缺 originalSourcePath' })
        continue
    }
    $srcDir = [string]$ev.originalSourcePath
    if (-not (Test-Path $srcDir)) {
        $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = '原厂目录不存在: ' + $srcDir })
        continue
    }
    $plugin = [string]$ev.originalPlugin
    if (-not $plugin) {
        $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = '卡片缺 originalPlugin' })
        continue
    }

    $csName = $plugin + 'Cs.dll'
    $csPath = Join-Path $srcDir $csName
    if (-not (Test-Path $csPath)) {
        $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = '找不到托管程序集 ' + $csName })
        continue
    }

    $duty = Get-DutyName $plugin
    $outDir = Join-Path $OutRoot $duty
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

    # -p 关闭项目文件生成，-o 指定输出目录，整程序集反编译
    $log = & ilspycmd -p -o $outDir $csPath 2>&1
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = '反编译失败: ' + (($log | Select-Object -First 3) -join ' ') })
        continue
    }

    $files = @(Get-ChildItem -Path $outDir -Recurse -File -Filter *.cs)
    $csHash = (Get-FileHash -Path $csPath -Algorithm SHA256).Hash
    $records.Add([pscustomobject]@{
        toolId    = $toolId
        plugin    = $plugin
        duty      = $duty
        csDll     = $csName
        csSha256  = $csHash
        sourceDir = $srcDir
        outDir    = $duty
        fileCount = $files.Count
    })
}

# README：记录来源程序集、哈希、抓取时间与用途声明
if ($records.Count -gt 0) {
    $readmePath = Join-Path $OutRoot 'README.md'
    $existing = ''
    if (Test-Path $readmePath) { $existing = [System.IO.File]::ReadAllText($readmePath, $utf8) }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('# 托管程序集反编译（按职责命名）')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('- 来源：本机 `D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\` 下的 `<Plugin>Cs.dll`')
    [void]$sb.AppendLine('- 工具：`ilspycmd`（ICSharpCode.Decompiler）')
    [void]$sb.AppendLine('- 用途：内部对照，用于确认参数模型、结果模型、工具入口与显示配置')
    [void]$sb.AppendLine('- 边界：**只有托管包装，不含算法本体**。算法在原生 `<Plugin>.dll`（无托管元数据），需汇编级分析')
    [void]$sb.AppendLine('- 不分发：反编译产物仅为内部逆向对照，不对外分发，也不作为产品源码')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('| 工具卡 | 原厂程序集 | SHA-256 | 职责目录 | .cs 文件数 | 抓取时间 |')
    [void]$sb.AppendLine('|---|---|---|---|---|---|')
    $now = Get-Date -Format 'yyyy-MM-dd HH:mm'
    foreach ($r in $records) {
        [void]$sb.AppendLine('| `' + $r.toolId + '` | `' + $r.csDll + '` | `' + $r.csSha256.Substring(0, 16) + '…` | `' + $r.outDir + '/` | ' + $r.fileCount + ' | ' + $now + ' |')
    }
    if ($existing) {
        # 保留旧表，避免只跑一批时把其它批次记录清掉
        $oldRows = ($existing -split "`n") | Where-Object { $_ -match '^\| `' }
        foreach ($row in $oldRows) {
            $keep = $true
            foreach ($r in $records) { if ($row -like '*`' + $r.toolId + '`*') { $keep = $false } }
            if ($keep) { [void]$sb.AppendLine($row.TrimEnd()) }
        }
    }
    [System.IO.File]::WriteAllText($readmePath, $sb.ToString(), $utf8)
}

Write-Host ''
Write-Host '============ 托管程序集反编译 ============' -ForegroundColor Cyan
Write-Host ("成功: {0}  跳过: {1}" -f $records.Count, $skipped.Count)
foreach ($r in $records) {
    Write-Host ("  {0,-28} -> {1,-22} {2,3} 个 .cs  {3}" -f $r.toolId, $r.duty, $r.fileCount, $r.csSha256.Substring(0, 12))
}
foreach ($s in $skipped) {
    Write-Host ("  跳过 {0,-26} {1}" -f $s.toolId, $s.reason) -ForegroundColor Yellow
}
Write-Host ''
