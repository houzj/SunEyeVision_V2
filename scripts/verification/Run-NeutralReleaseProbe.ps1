[CmdletBinding()]
param(
    [string]$AppPath = '',
    [switch]$Build,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ([string]::IsNullOrWhiteSpace($AppPath)) {
    $AppPath = Join-Path $PSScriptRoot '../../Release/SunEyeVision_v1.0.0/SunEyeVision'
}
$app = (Resolve-Path $AppPath).Path
$probeRoot = Join-Path $repo 'Release/_validation/bin/NeutralReleaseProbe/Release'

if ($Build -and $SkipBuild) { throw '不能同时指定 -Build 和 -SkipBuild。' }

# 探针和产品载荷必须使用同一份当前源码。可选参数的构造签名等元数据不会
# 由 CLR 兼容，旧探针若混入新载荷会在运行时才以 MissingMethodException 暴露，
# 因此默认按源码时间戳自动刷新；只有明确 -SkipBuild 才复用现有产物。
$probeInputs = @(
    (Join-Path $repo 'tests/NeutralReleaseProbe/Program.cs'),
    (Join-Path $repo 'tests/NeutralReleaseProbe/NeutralReleaseProbe.csproj'),
    (Join-Path $repo 'src/Plugin.Runtime/ReleasePayloadAuthority.cs')
)
$latestProbeInput = ($probeInputs | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { (Get-Item -LiteralPath $_).LastWriteTimeUtc } | Measure-Object -Maximum).Maximum
$probeOutput = Join-Path $probeRoot 'NeutralReleaseProbe.dll'
$probeNeedsBuild = -not (Test-Path -LiteralPath $probeOutput) -or ((Get-Item -LiteralPath $probeOutput).LastWriteTimeUtc -lt $latestProbeInput)

if ($Build -or ($probeNeedsBuild -and -not $SkipBuild)) {
    dotnet restore (Join-Path $repo 'tests/NeutralReleaseProbe/NeutralReleaseProbe.csproj')
    dotnet build (Join-Path $repo 'tests/NeutralReleaseProbe/NeutralReleaseProbe.csproj') -c Release --no-restore
}

Get-ChildItem -LiteralPath $app -File -Filter 'SunEyeVision.Plugin.SDK.*' -ErrorAction SilentlyContinue | Remove-Item -Force
$commit = (& git -C $repo rev-parse HEAD).Trim()
& (Join-Path $repo 'installer/Seal-ReleasePayload.ps1') -AppPath $app -SourceCommit $commit

$probeFiles = @('NeutralReleaseProbe.dll', 'NeutralReleaseProbe.deps.json', 'NeutralReleaseProbe.runtimeconfig.json')
$copied = [System.Collections.Generic.List[string]]::new()
try {
    foreach ($name in $probeFiles) {
        $source = Join-Path $probeRoot $name
        if (-not (Test-Path -LiteralPath $source)) { throw "探针输出不存在：$source" }
        $target = Join-Path $app $name
        Copy-Item -LiteralPath $source -Destination $target -Force
        $copied.Add($target)
    }
    dotnet exec (Join-Path $app 'NeutralReleaseProbe.dll') $app
}
finally {
    foreach ($path in $copied) {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }
}
