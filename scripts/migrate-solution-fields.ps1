<#
.SYNOPSIS
    方案文件字段迁移：从 .solution JSON 中删除指定前缀的工具设置字段。

.DESCRIPTION
    工具声明演进（例如结果页去掉「质心」显示项）后，存量方案里会残留已不存在的字段，
    加载器会以「Unknown Runtime field」拒绝整份方案。本脚本直接把 JSON 里这些字段删掉，
    使方案重新可加载；不修改其它任何内容，写入前自动备份 .bak。

    处理位置：
      - Nodes[].Configuration.Fields        运行/显示设置
      - Nodes[].Configuration.CurrentValues 当前值缓存
      - Nodes[].Configuration.ParameterBindingOrder 绑定顺序数组

.PARAMETER Path
    要处理的 .solution 文件或目录（目录会递归处理）。

.PARAMETER FieldPrefix
    要删除的字段前缀，默认 display.centroids.

.EXAMPLE
    ./migrate-solution-fields.ps1 -Path "$env:LOCALAPPDATA\SunEyeVision\Solutions"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$FieldPrefix = 'display.centroids.'
)

$files = @()
if (Test-Path $Path -PathType Container) {
    $files = Get-ChildItem -Path $Path -Recurse -Filter *.solution -File
} elseif (Test-Path $Path) {
    $files = @(Get-Item $Path)
} else {
    throw "路径不存在：$Path"
}

if ($files.Count -eq 0) { Write-Host '未找到 .solution 文件'; return }

$totalRemoved = 0
foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName, [System.Text.UTF8Encoding]::new($false))
    $json = $text | ConvertFrom-Json
    $removed = 0

    foreach ($workflow in @($json.Workflows)) {
        foreach ($node in @($workflow.Nodes)) {
            $config = $node.Configuration
            if (-not $config) { continue }

            if ($config.Fields) {
                foreach ($name in @($config.Fields.PSObject.Properties.Name)) {
                    if ($name -and $name.StartsWith($FieldPrefix, [StringComparison]::Ordinal)) {
                        $config.Fields.PSObject.Properties.Remove($name)
                        $removed++
                    }
                }
            }
            if ($config.CurrentValues) {
                foreach ($name in @($config.CurrentValues.PSObject.Properties.Name)) {
                    if ($name -and $name.StartsWith($FieldPrefix, [StringComparison]::Ordinal)) {
                        $config.CurrentValues.PSObject.Properties.Remove($name)
                        $removed++
                    }
                }
            }
            if ($config.ParameterBindingOrder) {
                $kept = @($config.ParameterBindingOrder | Where-Object { -not "$_".StartsWith($FieldPrefix, [StringComparison]::Ordinal) })
                $removed += @($config.ParameterBindingOrder).Count - $kept.Count
                $config.ParameterBindingOrder = $kept
            }
        }
    }

    if ($removed -gt 0) {
        Copy-Item $file.FullName ($file.FullName + '.bak') -Force
        $out = $json | ConvertTo-Json -Depth 20 -Compress
        [System.IO.File]::WriteAllText($file.FullName, $out, [System.Text.UTF8Encoding]::new($false))
        Write-Host ("{0}: 删除 {1} 个字段（已备份 .bak）" -f $file.Name, $removed)
        $totalRemoved += $removed
    } else {
        Write-Host ("{0}: 无需修改" -f $file.Name)
    }
}

Write-Host ("完成，共删除 {0} 个字段，处理 {1} 个文件" -f $totalRemoved, $files.Count)
