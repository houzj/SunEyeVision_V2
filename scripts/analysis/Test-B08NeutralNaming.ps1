[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$plan = Get-Content (Join-Path $root 'docs/collaboration/evidence/B08_NEUTRAL_NAMING_FILE_MAP_20261030.json') -Raw | ConvertFrom-Json
$failures = [Collections.Generic.List[string]]::new()
$assetCount = 0
foreach ($move in $plan.fileMoves) {
    if (Test-Path -LiteralPath (Join-Path $root $move.source)) { $failures.Add("Old product file remains: $($move.source)") }
    $destination = Join-Path $root $move.destination
    if (!(Test-Path -LiteralPath $destination -PathType Leaf)) { $failures.Add("Missing destination: $($move.destination)"); continue }
    if ($move.kind -eq 'Asset') {
        $assetCount++
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $move.sha256) { $failures.Add("Asset changed: $destination") }
    }
}
foreach ($file in @(& rg --files (Join-Path $root 'src') (Join-Path $root 'tools') (Join-Path $root 'scripts/verification') -g '*.cs' -g '*.xaml' -g '*.props' -g '!**/obj/**' -g '!**/bin/**')) {
    $text = [IO.File]::ReadAllText($file)
    foreach ($type in $plan.typeMoves) {
        if ([regex]::IsMatch($text,'(?<![\w.])' + [regex]::Escape($type.sourceType) + '(?![\w])')) { $failures.Add("Old full type consumer: $file => $($type.sourceType)") }
        $relativeType = $type.sourceType.Replace('SunEyeVision.Plugin.Wpf.','')
        if ([regex]::IsMatch($text,'(?<![\w.])' + [regex]::Escape($relativeType) + '(?![\w])')) { $failures.Add("Old relative type consumer: $file => $relativeType") }
    }
}
$assemblyPath = Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.Plugin.Wpf.dll'
$assembly = [Reflection.Assembly]::LoadFile($assemblyPath)
$stream = $assembly.GetManifestResourceStream('SunEyeVision.Plugin.Wpf.g.resources')
if (!$stream) { throw 'Missing WPF resource manifest' }
$reader = [Resources.ResourceReader]::new($stream)
$resourceKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
try {
    $enumerator = $reader.GetEnumerator()
    while ($enumerator.MoveNext()) { [void]$resourceKeys.Add([Uri]::UnescapeDataString([string]$enumerator.Key)) }
} finally { $reader.Dispose(); $stream.Dispose() }
foreach ($move in $plan.fileMoves | Where-Object { $_.kind -ne 'CSharp' }) {
    $key = $move.destination.Substring('src/Plugin.Wpf/'.Length).ToLowerInvariant()
    if ($key.EndsWith('.xaml') -and $key -notmatch '/colorstyle[123]\.xaml$') { $key = $key.Substring(0,$key.Length-5) + '.baml' }
    if (!$resourceKeys.Contains($key)) { $failures.Add("Missing compiled resource: $key") }
    $oldKey = $move.source.Substring('src/Plugin.Wpf/'.Length).ToLowerInvariant()
    if ($oldKey.EndsWith('.xaml') -and $oldKey -notmatch '/colorstyle[123]\.xaml$') { $oldKey = $oldKey.Substring(0,$oldKey.Length-5) + '.baml' }
    if ($resourceKeys.Contains($oldKey)) { $failures.Add("Old compiled resource remains: $oldKey") }
}
$catalog = Get-Content (Join-Path $root 'src/Plugin.Wpf/Catalog/controls.json') -Raw | ConvertFrom-Json
$localCount = 0
function Test-CatalogNode($node) {
    if ($node -is [pscustomobject]) {
        if ($node.PSObject.Properties['localPath'] -and $node.PSObject.Properties['localSha256']) {
            $local = Join-Path $root $node.localPath
            $script:localCount++
            if (!(Test-Path -LiteralPath $local -PathType Leaf)) { $failures.Add("Missing catalog local file: $($node.localPath)") }
            elseif ((Get-FileHash -LiteralPath $local -Algorithm SHA256).Hash -ne $node.localSha256) { $failures.Add("Stale catalog local hash: $($node.localPath)") }
        }
        foreach ($property in $node.PSObject.Properties) { Test-CatalogNode $property.Value }
    } elseif ($node -is [array]) { foreach ($item in $node) { Test-CatalogNode $item } }
}
Test-CatalogNode $catalog
$report = [ordered]@{ status=$(if($failures.Count){'failed'}else{'passed'}); movedFiles=$plan.fileMoves.Count; movedTypes=$plan.typeMoves.Count; unchangedAssets=$assetCount; compiledResourceKeys=$resourceKeys.Count; catalogLocalFiles=$localCount; assemblySha256=(Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash; failures=@($failures); limits='Resource and consumer identity audit only; not functional, visual, VS designer, or manual acceptance.' }
$report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'docs/collaboration/evidence/B08_NEUTRAL_NAMING_RESOURCE_AUDIT_20261030.json') -Encoding utf8
$report | ConvertTo-Json -Depth 8
if ($failures.Count) { exit 1 }
