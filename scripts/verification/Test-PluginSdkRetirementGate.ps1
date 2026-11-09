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
$solutionPath = Join-Path $repoRoot 'SunEyeVision.slnx'
[xml]$solution = [System.IO.File]::ReadAllText($solutionPath, [System.Text.Encoding]::UTF8)
$solutionProjects = @($solution.SelectNodes('//Project') | ForEach-Object { $_.Path.Replace('\\','/') })
$consumers = @($mapping.Consumers)
$direct = @($consumers | Where-Object {
    @($_.ProjectReferences) -match 'src[\\/]Plugin\.SDK[\\/]'
})
$activeDirect = @($direct | Where-Object { $_.Project -in $solutionProjects })
$toolDirect = @($activeDirect | Where-Object { $_.Project -like 'tools/*' })
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
    } | Where-Object { $_ -in $solutionProjects })
$inactiveDirect = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tools') -Recurse -Filter '*.csproj' -File |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and $_.Name -notmatch '_wpftmp\.csproj$' } |
    Where-Object {
        $text = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
        $text -match 'src[\\/]Plugin\.SDK[\\/]'
    } |
    ForEach-Object {
        $rootPrefix = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + '\'
        $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
        $fullPath.Substring($rootPrefix.Length).Replace('\\','/')
    } | Where-Object { $_ -notin $solutionProjects })
$mappedDirect = @($activeDirect | ForEach-Object { $_.Project })
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

Write-Output ("Plugin.SDK 退役门禁：现行解决方案消费者 {0}，其中工具 {1}，源码实时扫描 {2}，历史映射移出消费者 {3}，历史工具目录仍含旧引用 {4}，SDK目录条目 {5}" -f $activeDirect.Count,$toolDirect.Count,$actualDirect.Count,($direct.Count-$activeDirect.Count),$inactiveDirect.Count,$sdkEntries.Count)
if ($inactiveDirect.Count -ne 0) {
    Write-Output ("历史工具目录旧引用（不在解决方案/Release）：{0}" -f ($inactiveDirect -join ', '))
}
if ($scopedLegacy.Count -ne 0) {
    throw ('ImageSource/BlobAnalysis 仍存在旧 SDK 消费者：' + (($scopedLegacy | ForEach-Object Project) -join ', '))
}

if ($ReportOnly) {
    $activeDirect | ForEach-Object { Write-Output ("待迁移：{0}" -f $_.Project) }
    exit 0
}

if ($activeDirect.Count -ne 0 -or $sdkEntries.Count -ne 0) {
    $activeDirect | ForEach-Object { Write-Output ("阻断删除：{0}" -f $_.Project) }
    throw 'Plugin.SDK 退役条件未满足。必须先迁移全部直接消费者，再删除 SDK 目录。'
}

Write-Output 'PASS Plugin.SDK 已无消费者且目录为空，可进入删除批次。'
