[CmdletBinding()]
param(
    [string]$MappingPath = (Join-Path $PSScriptRoot '..\..\docs\collaboration\evidence\PluginSDK消费者源级映射-20261108.json'),
    [switch]$ReportOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$mapping = Get-Content -LiteralPath (Resolve-Path $MappingPath) -Raw | ConvertFrom-Json
$consumers = @($mapping.Consumers)
$direct = @($consumers | Where-Object {
    @($_.ProjectReferences) -match 'src[\\/]Plugin\.SDK[\\/]'
})
$toolDirect = @($direct | Where-Object { $_.Project -like 'tools/*' })
$srcSdk = Join-Path (Split-Path -Parent $PSScriptRoot | Split-Path -Parent) 'src\Plugin.SDK'
$sdkEntries = @(if (Test-Path $srcSdk) { Get-ChildItem -LiteralPath $srcSdk -Force -ErrorAction Stop })
$scoped = @($consumers | Where-Object { $_.Project -like 'tools/SunEyeVision.ImageSource/*' -or $_.Project -like 'tools/SunEyeVision.BlobAnalysis/*' })
$scopedLegacy = @($scoped | Where-Object {
    @($_.ProjectReferences) -match 'src[\\/]Plugin\.SDK[\\/]'
    -or @($_.ConsumerFiles) -match 'Plugin\.SDK'
})

Write-Output ("Plugin.SDK 退役门禁：直接项目消费者 {0}，工具消费者 {1}，SDK目录条目 {2}" -f $direct.Count,$toolDirect.Count,$sdkEntries.Count)
if ($scopedLegacy.Count -ne 0) {
    throw ('ImageSource/BlobAnalysis 仍存在旧 SDK 消费者：' + (($scopedLegacy | ForEach-Object Project) -join ', '))
}

if ($ReportOnly) {
    $direct | ForEach-Object { Write-Output ("待迁移：{0}" -f $_.Project) }
    exit 0
}

if ($direct.Count -ne 0 -or $sdkEntries.Count -ne 0) {
    $direct | ForEach-Object { Write-Output ("阻断删除：{0}" -f $_.Project) }
    throw 'Plugin.SDK 退役条件未满足。必须先迁移全部直接消费者，再删除 SDK 目录。'
}

Write-Output 'PASS Plugin.SDK 已无消费者且目录为空，可进入删除批次。'
