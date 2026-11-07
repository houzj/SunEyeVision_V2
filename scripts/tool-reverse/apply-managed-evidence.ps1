#requires -Version 5.1
<#
.SYNOPSIS
    把已反编译的托管源码登记进工具卡证据，并提升状态。

.DESCRIPTION
    只做机械登记，不做语义判断：
      - 在 docs/07-工具逆向/decompiled/Managed/<职责>/ 下找到该工具的 .cs
      - 按「工具类 / 参数类 / 结果类 / ROI 管理」分类写入 evidence.json.sourceEvidence
      - 每条带仓库相对路径、SHA-256、行数
      - 状态 inventory-only -> source-partial；source-indexed 保持不动
      - 删掉「尚未开始该工具的源码级闭环逆向」这类占位 unknown

    语义链（semanticChain）、分析环节（analysisStages）、算法对照（algorithmComparison）
    不在这里生成——那些结论必须有人看过代码才写，脚本编不出「已确认」。

.PARAMETER Tool
    工具卡 ID，可多个或逗号分隔。

.PARAMETER All
    对 decompiled/Managed 下已有产物的全部工具执行。
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string[]]$Tool,
    [switch]$All
)

$ErrorActionPreference = 'Stop'

trap {
    Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ('LINE : ' + $_.InvocationInfo.ScriptLineNumber) -ForegroundColor Red
    Write-Host ('CODE : ' + ([string]$_.InvocationInfo.Line).Trim()) -ForegroundColor Red
    exit 1
}

if ($Tool) {
    $Tool = @($Tool | ForEach-Object { $_ -split '[,;]' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}
if (-not $Root) { $Root = Join-Path $PSScriptRoot '..\..\docs\07-工具逆向' }
$root = (Resolve-Path $Root).Path
$toolsDir = Join-Path $root 'tools'
$managedRoot = Join-Path $root 'decompiled\Managed'
$utf8 = [System.Text.UTF8Encoding]::new($false)

if (-not (Test-Path $managedRoot)) { throw "没有反编译产物: $managedRoot（先跑 decompile-managed.ps1）" }

function Get-DutyName {
    param([string]$Plugin)
    $n = $Plugin -replace '^IMVS', ''
    $n = $n -replace 'Modu$', ''
    $n = $n -replace 'ModuVA$', 'VA'
    if (-not $n) { $n = $Plugin }
    return ($n -replace '[^\w\-]', '')
}

function Get-RelPath {
    param([string]$Full)
    $docs = Split-Path $root -Parent
    $repo = Split-Path $docs -Parent
    return $Full.Replace($repo + '\', '').Replace('\', '/')
}

$dirs = Get-ChildItem -Path $toolsDir -Directory | Sort-Object Name
if ($Tool -and $Tool.Count -gt 0) {
    $want = @{}
    foreach ($t in $Tool) { $want[$t] = $true }
    $dirs = @($dirs | Where-Object { $want.ContainsKey($_.Name) })
}

$updated = New-Object 'System.Collections.Generic.List[object]'
$skipped = New-Object 'System.Collections.Generic.List[object]'

foreach ($dir in $dirs) {
    $toolId = $dir.Name
    if ($toolId.StartsWith('_')) { continue }
    $evPath = Join-Path $dir.FullName 'evidence.json'
    if (-not (Test-Path $evPath)) { continue }

    $raw = [System.IO.File]::ReadAllText($evPath, $utf8)
    $ev = $raw | ConvertFrom-Json

    $plugin = [string]$ev.originalPlugin
    if (-not $plugin) { $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = '缺 originalPlugin' }); continue }

    $duty = Get-DutyName $plugin
    $dutyDir = Join-Path $managedRoot $duty
    if (-not (Test-Path $dutyDir)) { $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = "无产物目录 $duty" }); continue }

    $csFiles = @(Get-ChildItem -Path $dutyDir -Recurse -File -Filter *.cs)
    if ($csFiles.Count -eq 0) { $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = '产物目录里没有 .cs' }); continue }

    $entries = New-Object 'System.Collections.Generic.List[object]'
    $managedNames = New-Object 'System.Collections.Generic.List[string]'

    foreach ($f in $csFiles) {
        $base = $f.BaseName
        $symbol = $null
        if ($base -match 'Tool$') { $symbol = '托管工具类：构造、ModuParams、ModuResult、Run(SyncSelfRun)' }
        elseif ($base -match 'Param$') { $symbol = '托管参数模型：参数名、类型、枚举定义' }
        elseif ($base -match 'Result$') { $symbol = '托管结果模型：输出字段与状态' }
        elseif ($base -match 'RoiManager$') { $symbol = '托管 ROI 管理：ROI 形状与生命周期' }
        elseif ($base -match 'ModelData$') { $symbol = '托管模型数据：模板模型载体' }
        elseif ($base -match 'UIData$') { $symbol = '托管 UI 数据：界面共享状态' }
        elseif ($base -match 'AssemblyInfo') { continue }
        else { $symbol = '托管类型' }

        $lineCount = (Get-Content -Path $f.FullName -Encoding UTF8).Count
        $hash = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash
        $entries.Add([pscustomobject]@{
            path    = (Get-RelPath $f.FullName)
            sha256  = $hash
            symbol  = $symbol + '（' + $base + '）'
            lines   = ('1-' + $lineCount)
            confirmable = '托管层源码直读：可确认类结构、成员、枚举与调用关系；不含算法本体（算法在原生 <Plugin>.dll）'
        })
        $managedNames.Add($base)
    }

    if ($entries.Count -eq 0) { $skipped.Add([pscustomobject]@{ toolId = $toolId; reason = '没有可登记的类型' }); continue }

    # 合并：按 path 去重，托管的放最前
    $existing = @($ev.sourceEvidence | Where-Object { $_ -ne $null })
    $kept = @($existing | Where-Object { $p = [string]$_.path; @($entries | Where-Object { $_.path -eq $p }).Count -eq 0 })
    # PS 5.1 里泛型 List 直接参与数组加法会报 Argument types do not match，必须先 ToArray()
    $merged = @()
    $merged += $entries.ToArray()
    $merged += $kept

    $oldStatus = [string]$ev.status
    $newStatus = $oldStatus
    if ($oldStatus -eq 'inventory-only' -or -not $oldStatus) { $newStatus = 'source-partial' }
    elseif ($oldStatus -eq 'source-indexed') { $newStatus = 'source-partial' }

    # 去掉占位 unknown
    $unk = @($ev.unknowns | Where-Object { $_ -ne $null -and ([string]$_) -notmatch '尚未开始该工具的源码级闭环逆向' })

    # 用有序哈希表重建对象，避免 5.1 下 Add-Member 对数组值的类型报错
    $out = [ordered]@{}
    foreach ($prop in $ev.PSObject.Properties) { $out[$prop.Name] = $prop.Value }
    $out['sourceEvidence'] = $merged
    $out['status'] = $newStatus
    if ($out.Contains('evidenceState')) { $out['evidenceState'] = $newStatus }
    $out['managedMembers'] = @($managedNames)
    $out['unknowns'] = @($unk)

    $json = [pscustomobject]$out | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($evPath, $json, $utf8)

    $updated.Add([pscustomobject]@{
        toolId    = $toolId
        duty      = $duty
        status    = ($oldStatus + ' -> ' + $newStatus)
        evidence  = $entries.Count
        files     = $managedNames.Count
    })
}

Write-Host ''
Write-Host '============ 托管证据登记 ============' -ForegroundColor Cyan
Write-Host ("更新: {0}  跳过: {1}" -f $updated.Count, $skipped.Count)
foreach ($u in $updated) {
    Write-Host ("  {0,-28} {1,-16} 证据 {2,2} 条  {3}" -f $u.toolId, $u.duty, $u.evidence, $u.status)
}
foreach ($s in $skipped) {
    Write-Host ("  跳过 {0,-26} {1}" -f $s.toolId, $s.reason) -ForegroundColor Yellow
}
Write-Host ''
