#requires -Version 5.1
<#
.SYNOPSIS
    工具逆向卡片校验器 v2。

.DESCRIPTION
    检查 docs/07-工具逆向/tools/<toolid>/ 下七件套是否齐全、字段是否合法。
    契约来源：docs/07-工具逆向/contracts/card.schema.json

    v2 按 162 张真实卡片的实测形态建模：
      - evidence.json 字段分 core / standard / deep 三级
      - ui-preview.json 分 resource-control-evidence / schema-preview / empty 三形态
      - algorithm-semantics.md 的 9 步齐全度降为提示，另有内容段标题硬校验

    硬失败以退出码 1 结束并打印具体文件与字段。不补默认值继续（rule-015）。

.PARAMETER Root
    证据库根目录，默认按脚本位置推导 docs/07-工具逆向。

.PARAMETER OnlyTool
    只校验指定工具 ID，便于单卡调试。

.EXAMPLE
    pwsh -File scripts/tool-reverse/validate-cards.ps1
    pwsh -File scripts/tool-reverse/validate-cards.ps1 -OnlyTool imagesourcemodule
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string]$OnlyTool
)

$ErrorActionPreference = 'Stop'

if (-not $Root) {
    $Root = Join-Path $PSScriptRoot '..\..\docs\07-工具逆向'
}
if (-not (Test-Path $Root)) {
    throw "证据库根目录不存在: $Root"
}
$root = (Resolve-Path $Root).Path
$toolsDir = Join-Path $root 'tools'
$schemaPath = Join-Path $root 'contracts\card.schema.json'

if (-not (Test-Path $schemaPath)) {
    throw "契约文件不存在: $schemaPath"
}

$schema = Get-Content $schemaPath -Raw -Encoding UTF8 | ConvertFrom-Json
$requiredFiles = @($schema.requiredFiles)
$enums = $schema.enums
$extendedSections = $schema.extendedSections.fields

$pageContractPath = Join-Path $root 'contracts\page.sections.json'
if (-not (Test-Path $pageContractPath)) {
    throw "页面契约文件不存在: $pageContractPath"
}
$pageContract = Get-Content $pageContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
$stageIds = @($pageContract.analysisStages | ForEach-Object { $_.id })
$dimIds = @($pageContract.algorithmDimensions | ForEach-Object { $_.id })
$stepIds = @($pageContract.semanticSteps | ForEach-Object { $_.step })

$tiers = $schema.files.'evidence.json'.fieldTiers
$coreFields = @($tiers.core.fields)
$standardFields = @($tiers.standard.fields)
$deepFields = @($tiers.deep.fields)

$uiKindRule = $schema.files.'ui-preview.json'.kinds
$semRule = $schema.markdownRules.'algorithm-semantics.md'
$stepSpecs = @($semRule.steps)
$contentAnyOf = @($semRule.contentRule.anyOf)

$errors = New-Object 'System.Collections.Generic.List[string]'
$warnings = New-Object 'System.Collections.Generic.List[string]'
$scanned = 0
$profileCount = @{ standard = 0; deep = 0 }
$uiKindCount = @{ 'resource-control-evidence' = 0; 'evidence-layout' = 0; 'schema-preview' = 0; 'empty' = 0 }

function Add-Error {
    param([string]$Tool, [string]$Message)
    $script:errors.Add("[$Tool] $Message")
}

function Add-Warning {
    param([string]$Tool, [string]$Message)
    $script:warnings.Add("[$Tool] $Message")
}

function Test-Sha256 {
    param([string]$Value)
    if (-not $Value) { return $false }
    return ($Value -match '^[A-Fa-f0-9]{64}$')
}

if (-not (Test-Path $toolsDir)) {
    throw "工具卡目录不存在: $toolsDir"
}

$toolDirs = Get-ChildItem -Path $toolsDir -Directory | Sort-Object Name
if ($OnlyTool) {
    $toolDirs = @($toolDirs | Where-Object { $_.Name -eq $OnlyTool })
    if ($toolDirs.Count -eq 0) { throw "未找到工具卡: $OnlyTool" }
}

foreach ($dir in $toolDirs) {
    $toolId = $dir.Name
    if ($toolId.StartsWith('_')) { continue }
    $scanned++

    # 1. 七件套齐全
    foreach ($pattern in $requiredFiles) {
        $fileName = $pattern.Replace('{toolid}', $toolId)
        if (-not (Test-Path (Join-Path $dir.FullName $fileName))) {
            Add-Error $toolId "缺少必需文件: $fileName"
        }
    }

    # 2. evidence.json
    $evidencePath = Join-Path $dir.FullName 'evidence.json'
    if (Test-Path $evidencePath) {
        $ev = $null
        try {
            $ev = Get-Content $evidencePath -Raw -Encoding UTF8 | ConvertFrom-Json
        }
        catch {
            Add-Error $toolId "evidence.json 解析失败: $($_.Exception.Message)"
        }
        if ($ev) {
            $propNames = @($ev.PSObject.Properties.Name)
            $isDeep = $propNames -contains 'authority'
            $profile = 'standard'
            if ($isDeep) { $profile = 'deep' }
            $profileCount[$profile] = $profileCount[$profile] + 1

            $expected = @($coreFields)
            if ($isDeep) { $expected += $deepFields } else { $expected += $standardFields }
            foreach ($field in $expected) {
                if (-not ($propNames -contains $field)) {
                    Add-Error $toolId "evidence.json 缺少字段($profile): $field"
                }
            }

            if ($ev.stableToolId -and $ev.stableToolId -ne $toolId) {
                Add-Error $toolId "stableToolId=$($ev.stableToolId) 与目录名不一致"
            }
            if ($ev.status -and ($enums.status -notcontains $ev.status)) {
                Add-Error $toolId "status=$($ev.status) 不在枚举内"
            }
            if ($ev.priority -and ($enums.priority -notcontains $ev.priority)) {
                Add-Error $toolId "priority=$($ev.priority) 不在枚举内"
            }
            if ($ev.uiCategory -and ($enums.uiCategory -notcontains $ev.uiCategory)) {
                Add-Error $toolId "uiCategory=$($ev.uiCategory) 不在枚举内"
            }
            if ($propNames -contains 'unknowns') {
                if (@($ev.unknowns).Count -eq 0) {
                    Add-Error $toolId 'evidence.json 的 unknowns 为空数组（视为未取证）'
                }
            }
            $idx = 0
            foreach ($item in @($ev.sourceEvidence)) {
                $idx++
                if ($item -and $item.sha256 -and -not (Test-Sha256 $item.sha256)) {
                    Add-Error $toolId "sourceEvidence[$idx].sha256 格式非法"
                }
            }
        }
    }

    # 2b. evidence.json 扩展段：analysisStages / semanticChain / algorithmComparison
    # 规则：字段可以不填（渲染 unknown），一旦填了必须整组齐全。半组会把「未取证」伪装成「部分取证」。
    if ($ev) {
        $evProps = @($ev.PSObject.Properties.Name)
        foreach ($sectionName in $extendedSections.PSObject.Properties.Name) {
            if (-not ($evProps -contains $sectionName)) { continue }
            $spec = $extendedSections.$sectionName
            $items = @($ev.$sectionName | Where-Object { $_ -ne $null })

            $expectIds = @()
            if ($sectionName -eq 'analysisStages') { $expectIds = $stageIds }
            elseif ($sectionName -eq 'semanticChain') { $expectIds = $stepIds }
            elseif ($sectionName -eq 'algorithmComparison') { $expectIds = $dimIds }

            if ($items.Count -ne $expectIds.Count) {
                Add-Error $toolId "evidence.json.$sectionName 条目数 $($items.Count)，契约要求 $($expectIds.Count)（必须整组齐全）"
                continue
            }

            $idField = 'id'
            if ($sectionName -eq 'semanticChain') { $idField = 'step' }

            $idx = 0
            foreach ($item in $items) {
                $idx++
                $iprops = @($item.PSObject.Properties.Name)
                foreach ($req in @($spec.itemRequired)) {
                    if (-not ($iprops -contains $req)) {
                        Add-Error $toolId "evidence.json.$sectionName[$idx] 缺少字段: $req"
                    }
                }
                if ($iprops -contains $idField) {
                    $want = $expectIds[$idx - 1]
                    if ($item.$idField -ne $want) {
                        Add-Error $toolId "evidence.json.$sectionName[$idx].$idField=$($item.$idField)，契约顺序要求 $want"
                    }
                }
                if ($iprops -contains 'status') {
                    if (@($spec.statusEnum) -notcontains $item.status) {
                        Add-Error $toolId "evidence.json.$sectionName[$idx].status=$($item.status) 不在枚举内"
                    }
                }
                if ($iprops -contains 'conclusion') {
                    if (@($spec.conclusionEnum) -notcontains $item.conclusion) {
                        Add-Error $toolId "evidence.json.$sectionName[$idx].conclusion=$($item.conclusion) 不在枚举内"
                    }
                    if ($item.conclusion -ne 'confirmed') {
                        if (-not $item.unknown -or ([string]$item.unknown).Trim().Length -eq 0) {
                            Add-Error $toolId "evidence.json.$sectionName[$idx] 结论非 confirmed 但未写 unknown（缺什么证据）"
                        }
                    }
                }
                if ($sectionName -eq 'algorithmComparison' -and $item.status -ne 'established') {
                    if (-not $item.unknown -or ([string]$item.unknown).Trim().Length -eq 0) {
                        Add-Error $toolId "evidence.json.algorithmComparison[$idx] 未建立对比但未写 unknown"
                    }
                }
            }
        }
    }

    # 3. resources.json
    $resourcesPath = Join-Path $dir.FullName 'resources.json'
    if (Test-Path $resourcesPath) {
        try {
            $res = Get-Content $resourcesPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $idx = 0
            foreach ($item in @($res.xmlEvidence)) {
                if (-not $item) { continue }
                $idx++
                if (-not (Test-Sha256 $item.sha256)) {
                    Add-Error $toolId "resources.xmlEvidence[$idx].sha256 缺失或格式非法"
                }
                if (-not $item.root) {
                    Add-Error $toolId "resources.xmlEvidence[$idx].root 缺失"
                }
            }
        }
        catch {
            Add-Error $toolId "resources.json 解析失败: $($_.Exception.Message)"
        }
    }

    # 4. ui-preview.json —— 三形态
    $uiPath = Join-Path $dir.FullName 'ui-preview.json'
    if (Test-Path $uiPath) {
        $ui = $null
        try {
            $ui = Get-Content $uiPath -Raw -Encoding UTF8 | ConvertFrom-Json
        }
        catch {
            Add-Error $toolId "ui-preview.json 解析失败: $($_.Exception.Message)"
        }
        if ($ui) {
            $uiProps = @($ui.PSObject.Properties.Name)
            $uiKind = 'empty'
            if (($uiProps -contains 'previewKind') -and $ui.previewKind) { $uiKind = $ui.previewKind }
            elseif ($uiProps -contains 'kind') { $uiKind = 'schema-preview' }
            if (-not $uiKindCount.ContainsKey($uiKind)) { $uiKindCount[$uiKind] = 0 }
            $uiKindCount[$uiKind] = $uiKindCount[$uiKind] + 1

            if ($uiKind -eq 'empty') {
                Add-Error $toolId 'ui-preview.json 内容为空 {}，属真实缺失，需补齐'
            }
            else {
                $spec = $uiKindRule.$uiKind
                if ($spec) {
                    foreach ($field in @($spec.required)) {
                        if (-not ($uiProps -contains $field)) {
                            Add-Error $toolId "ui-preview.json($uiKind) 缺少字段: $field"
                        }
                    }
                }
                if ($uiKind -eq 'evidence-layout') {
                    if (-not ($uiProps -contains 'missing')) {
                        Add-Error $toolId 'ui-preview.json(evidence-layout) 缺少 missing 字段'
                    }
                    elseif (@($ui.missing | Where-Object { $_ -ne $null }).Count -eq 0) {
                        Add-Error $toolId 'ui-preview.json(evidence-layout) 的 missing 为空，未说明缺失什么'
                    }
                }
                if ($uiKind -eq 'resource-control-evidence') {
                    $idValue = $null
                    if ($uiProps -contains 'toolId') { $idValue = $ui.toolId }
                    elseif ($uiProps -contains 'stableToolId') { $idValue = $ui.stableToolId }
                    if ($idValue -and $idValue -ne $toolId) {
                        Add-Error $toolId "ui-preview 的 ID=$idValue 与目录名不一致"
                    }
                    $ctrlList = @($ui.controls | Where-Object { $_ -ne $null })
                    if ($ctrlList.Count -eq 0) {
                        Add-Error $toolId 'ui-preview.controls 为空数组（resource-control-evidence 形态必须有控件条目）'
                    }
                    else {
                        foreach ($ctrl in $ctrlList) {
                            if (-not $ctrl.type -or -not $ctrl.name) {
                                Add-Error $toolId 'ui-preview.controls 存在缺少 type 或 name 的条目'
                                break
                            }
                        }
                    }
                    if ($uiProps -contains 'missing') {
                        if (@($ui.missing | Where-Object { $_ -ne $null }).Count -eq 0) {
                            Add-Warning $toolId 'ui-preview.missing 为空数组，请确认是否真的无缺失项'
                        }
                    }
                }
            }
        }
    }

    # 5. algorithm-semantics.md
    $semPath = Join-Path $dir.FullName 'algorithm-semantics.md'
    if (Test-Path $semPath) {
        $semText = Get-Content $semPath -Raw -Encoding UTF8

        $missingSteps = @()
        foreach ($spec in $stepSpecs) {
            $hit = $false
            foreach ($kw in @($spec.match)) {
                if ($semText -match [regex]::Escape($kw)) { $hit = $true; break }
            }
            if (-not $hit) { $missingSteps += $spec.id }
        }
        if ($missingSteps.Count -gt 0) {
            Add-Warning $toolId ("algorithm-semantics.md 未覆盖 9 步中的: " + ($missingSteps -join ', '))
        }

        $hasContent = $false
        foreach ($heading in $contentAnyOf) {
            if ($semText -match [regex]::Escape($heading)) { $hasContent = $true; break }
        }
        if (-not $hasContent) {
            Add-Error $toolId 'algorithm-semantics.md 缺少已确认/未确认内容段标题'
        }
    }
}

Write-Host ''
Write-Host '================ 工具逆向卡片校验 ================' -ForegroundColor Cyan
Write-Host ("扫描工具卡: {0}" -f $scanned)
Write-Host ("档案分布: standard={0}  deep={1}" -f $profileCount['standard'], $profileCount['deep'])
Write-Host ("UI 预览形态: resource-control-evidence={0}  evidence-layout={1}  schema-preview={2}  empty={3}" -f $uiKindCount['resource-control-evidence'], $uiKindCount['evidence-layout'], $uiKindCount['schema-preview'], $uiKindCount['empty'])
Write-Host ("硬失败: {0}" -f $errors.Count) -ForegroundColor $(if ($errors.Count -gt 0) { 'Red' } else { 'Green' })
Write-Host ("提示: {0}" -f $warnings.Count) -ForegroundColor Yellow

if ($errors.Count -gt 0) {
    Write-Host ''
    Write-Host '--- 硬失败明细 ---' -ForegroundColor Red
    foreach ($e in $errors) { Write-Host $e -ForegroundColor Red }
}
if ($warnings.Count -gt 0) {
    Write-Host ''
    Write-Host '--- 提示明细 ---' -ForegroundColor Yellow
    foreach ($w in $warnings) { Write-Host $w -ForegroundColor Yellow }
}

if ($errors.Count -gt 0) {
    exit 1
}
Write-Host ''
Write-Host '全部通过。' -ForegroundColor Green
exit 0
