param([Parameter(Mandatory=$true)][string]$Revision)
$ErrorActionPreference='Stop'
$repository=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$resolvedRevision=(git -C $repository rev-parse $Revision).Trim()
git -C $repository diff --quiet $resolvedRevision -- src tools tests
if($LASTEXITCODE -ne 0){throw 'Product/test source differs from the fixed commit.'}
$delivery=Join-Path $PSScriptRoot ('delivery-'+$resolvedRevision.Substring(0,8))
if(Test-Path -LiteralPath $delivery){throw 'Never overwrite a fixed delivery.'}
$release=(Resolve-Path (Join-Path $repository 'Release/SunEyeVision_v1.0.0/SunEyeVision')).Path
if(Get-ChildItem -LiteralPath $release -Recurse -Attributes ReparsePoint){throw 'Release contains links.'}
if(Get-ChildItem -LiteralPath $release -Filter 'Probe.*'){throw 'Foreign host output remains in Release.'}
New-Item -ItemType Directory -Path $delivery | Out-Null
$app=Join-Path $delivery 'app'
Copy-Item -LiteralPath $release -Destination $app -Recurse
$binaries=@(Get-ChildItem -LiteralPath $app -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($app,$_.FullName);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
$binaries | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $delivery 'binary-manifest.json') -Encoding utf8
$sourcePaths=@(git -C $repository ls-tree -r --name-only $resolvedRevision -- src tools tests Directory.Build.props Directory.Build.targets SunEyeVision.slnx)
$sources=@($sourcePaths | Where-Object {$_ -match '\.(cs|xaml|csproj|props|targets|json|slnx|txt)$'} | ForEach-Object {
    [ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $repository $_)).Hash}
})
$sources | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $delivery 'source-manifest.json') -Encoding utf8
$archiveFile=Join-Path $delivery 'source.zip'
git -C $repository archive --format=zip "--output=$archiveFile" $resolvedRevision -- src tools tests Directory.Build.props Directory.Build.targets SunEyeVision.slnx
if($LASTEXITCODE -ne 0){throw 'Source archive failed.'}
$archive=[IO.Compression.ZipFile]::OpenRead($archiveFile)
try {
    $archiveRows=@($archive.Entries | Where-Object {$_.Name} | ForEach-Object {
        $stream=$_.Open()
        try {[ordered]@{path=$_.FullName;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}}
        finally {$stream.Dispose()}
    })
} finally {$archive.Dispose()}
$archiveRows | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $delivery 'source-archive-manifest.json') -Encoding utf8
$vendorFiles=@(Get-ChildItem -LiteralPath (Join-Path $repository 'libs/halcon/runtime/x64-win64') -Filter '*.dll')
$vendorFiles+=Get-Item -LiteralPath (Join-Path $repository 'libs/halcon/net/halcondotnetxl.dll')
$vendors=@($vendorFiles | ForEach-Object {
    $deployed=Join-Path $app $_.Name
    [ordered]@{source=$_.FullName;path=$_.Name;sourceSha256=(Get-FileHash -LiteralPath $_.FullName).Hash;deployedSha256=(Get-FileHash -LiteralPath $deployed).Hash}
})
if(@($vendors | Where-Object {$_.sourceSha256 -ne $_.deployedSha256}).Count){throw 'Vendor deployment identity mismatch.'}
$vendors | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $delivery 'vendor-receipt.json') -Encoding utf8
[ordered]@{sourceCommit=$resolvedRevision;programFiles=$binaries.Count;sourceInputs=$sources.Count;
    binaryManifestSha256=(Get-FileHash (Join-Path $delivery 'binary-manifest.json')).Hash;
    sourceManifestSha256=(Get-FileHash (Join-Path $delivery 'source-manifest.json')).Hash;
    sourceZipSha256=(Get-FileHash $archiveFile).Hash;
    sourceArchiveManifestSha256=(Get-FileHash (Join-Path $delivery 'source-archive-manifest.json')).Hash;
    sourceManifestKind='actual built worktree bytes';sourceArchiveKind='git normalized committed bytes';foreignProbeFiles=0;vendorMatches=$vendors.Count
} | ConvertTo-Json | Set-Content (Join-Path $delivery 'identity.json') -Encoding utf8
Get-Content (Join-Path $delivery 'identity.json')
