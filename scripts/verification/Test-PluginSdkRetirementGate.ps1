[CmdletBinding()]
param(
    [string]$MappingPath,
    [switch]$ReportOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($MappingPath)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $MappingPath = Join-Path $scriptRoot '..\..\docs\collaboration\evidence\PluginSDK消费者源级映射-20261108.json'
}

$mappingPathResolved = (Resolve-Path $MappingPath).Path
$mappingJson = [System.IO.File]::ReadAllText($mappingPathResolved, [System.Text.Encoding]::UTF8)
$mapping = $mappingJson | ConvertFrom-Json
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
$consumers = @($mapping.Consumers)
$direct = @($consumers | Where-Object {
    @($_.ProjectReferences) -match 'src[\\/]Plugin\.SDK[\\/]'
})
$toolDirect = @($direct | Where-Object { $_.Project -like 'tools/*' })
$actualDirect = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tools') -Recurse -Filter '*.csproj' -File |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and $_.Name -notmatch '_wpftmp\.csproj$' } |
    Where-Object {
        $text = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
        $text -match 'src[\\/]Plugin\.SDK[\\/]'
    } |
    ForEach-Object {
        $rootPrefix = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + '\'
        $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
        $fullPath.Substring($rootPrefix.Length).Replace('\','/')
    })
$mappedDirect = @($direct | ForEach-Object { $_.Project })
$missingFromMapping = @($actualDirect | Where-Object { $_ -notin $mappedDirect })
$missingFromSource = @($mappedDirect | Where-Object { $_ -notin $actualDirect })
if ($missingFromMapping.Count -ne 0 -or $missingFromSource.Count -ne 0) {
    $details = @(
        ($missingFromMapping | ForEach-Object { "源码有引用但映射缺失：$_" })
        ($missingFromSource | ForEach-Object { "映射有记录但源码无引用：$_" })
    )
    throw ('Plugin.SDK 消费者映射与工作树不一致。' + ($details -join '；'))
}
$srcSdk = Join-Path (Split-Path -Parent $PSScriptRoot | Split-Path -Parent) 'src\Plugin.SDK'
$sdkEntries = @(if (Test-Path $srcSdk) { Get-ChildItem -LiteralPath $srcSdk -Force -ErrorAction Stop })
$scoped = @($consumers | Where-Object { $_.Project -like 'tools/SunEyeVision.ImageSource/*' -or $_.Project -like 'tools/SunEyeVision.BlobAnalysis/*' })
$scopedLegacy = @($scoped | Where-Object {
    @($_.ProjectReferences) -match 'src[\\/]Plugin\.SDK[\\/]'
    -or @($_.ConsumerFiles) -match 'Plugin\.SDK'
})

Write-Output ("Plugin.SDK 退役门禁：直接项目消费者 {0}，工具消费者 {1}，源码实时扫描 {2}，SDK目录条目 {3}" -f $direct.Count,$toolDirect.Count,$actualDirect.Count,$sdkEntries.Count)
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
