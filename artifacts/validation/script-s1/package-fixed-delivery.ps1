$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$revision = 'a254710d31af3a39619f9b7ac50eaca344ac30bf'
$deliveryRoot = Join-Path $PSScriptRoot 'delivery-a254710d'
$applicationSource = Join-Path $repository 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$applicationCopy = Join-Path $deliveryRoot 'app'
if (Test-Path -LiteralPath $deliveryRoot) { throw 'A fixed delivery already exists; never overwrite it.' }
if (Get-ChildItem -LiteralPath $applicationSource -Recurse -Attributes ReparsePoint) { throw 'Application output contains a reparse point.' }
New-Item -ItemType Directory -Path $deliveryRoot | Out-Null
Copy-Item -LiteralPath $applicationSource -Destination $applicationCopy -Recurse
$binaryManifest = @(Get-ChildItem -LiteralPath $applicationCopy -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path = [IO.Path]::GetRelativePath($applicationCopy, $_.FullName); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$binaryManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $deliveryRoot 'binary-manifest.json') -Encoding utf8
$sourcePaths = @(git -C $repository ls-tree -r --name-only $revision -- src tools tests Directory.Build.props Directory.Build.targets Directory.Packages.props SunEyeVision.slnx global.json)
$sourceManifest = @($sourcePaths | Where-Object { $_ -match '\.(cs|xaml|csproj|props|targets|json|slnx|txt)$' } | ForEach-Object {
    $file = Join-Path $repository $_
    [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
})
$sourceManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $deliveryRoot 'source-manifest.json') -Encoding utf8
$archiveFile = Join-Path $deliveryRoot 'source.zip'
git -C $repository archive --format=zip "--output=$archiveFile" $revision -- src tools tests Directory.Build.props Directory.Build.targets SunEyeVision.slnx
if ($LASTEXITCODE -ne 0) { throw 'Source archive failed.' }
$archive = [IO.Compression.ZipFile]::OpenRead($archiveFile)
try {
    $archiveManifest = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object {
        $entryStream = $_.Open()
        try { [ordered]@{ path = $_.FullName; sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entryStream)) } }
        finally { $entryStream.Dispose() }
    })
} finally { $archive.Dispose() }
$archiveManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $deliveryRoot 'source-archive-manifest.json') -Encoding utf8
[ordered]@{ sourceCommit = $revision; files = $binaryManifest.Count; sourceFiles = $sourceManifest.Count;
    sourceManifestKind = 'actual-built-worktree-bytes'; sourceArchiveKind = 'git-archive-committed-normalized-bytes';
    sourceArchiveManifestSha256 = (Get-FileHash -LiteralPath (Join-Path $deliveryRoot 'source-archive-manifest.json')).Hash;
    binaryManifestSha256 = (Get-FileHash -LiteralPath (Join-Path $deliveryRoot 'binary-manifest.json')).Hash;
    sourceManifestSha256 = (Get-FileHash -LiteralPath (Join-Path $deliveryRoot 'source-manifest.json')).Hash;
    sourceZipSha256 = (Get-FileHash -LiteralPath (Join-Path $deliveryRoot 'source.zip')).Hash
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $deliveryRoot 'identity.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $deliveryRoot 'identity.json')
