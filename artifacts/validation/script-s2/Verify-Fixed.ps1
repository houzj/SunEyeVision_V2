param([string]$Delivery='delivery-a59f446c',[string]$ReceiptName='post-runtime-identity.json')
$ErrorActionPreference='Stop'
$deliveryPath=Join-Path $PSScriptRoot $Delivery
$appPath=Join-Path $deliveryPath 'app'
$manifest=Get-Content (Join-Path $deliveryPath 'binary-manifest.json') -Raw | ConvertFrom-Json
$drift=@($manifest | Where-Object {
    $target=Join-Path $appPath $_.path
    -not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne $_.sha256
})
$expected=@{};foreach($entry in $manifest){$expected[$entry.path]=$true}
$extra=@(Get-ChildItem -LiteralPath $appPath -Recurse -File | Where-Object {-not $expected.ContainsKey([IO.Path]::GetRelativePath($appPath,$_.FullName))})
$sourceRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$sources=Get-Content (Join-Path $deliveryPath 'source-manifest.json') -Raw | ConvertFrom-Json
$sourceDrift=@($sources | Where-Object {(Get-FileHash -LiteralPath (Join-Path $sourceRoot $_.path)).Hash -ne $_.sha256})
[ordered]@{programFiles=$manifest.Count;binaryDrift=$drift.Count;extraFiles=$extra.Count;sourceInputs=$sources.Count;sourceDrift=$sourceDrift.Count;
    changedBinaryPaths=@($drift | ForEach-Object {$_.path});extraPaths=@($extra | ForEach-Object {$_.FullName});changedSourcePaths=@($sourceDrift | ForEach-Object {$_.path});
    note='Run before S3 product changes; binary identity concerns fixed app copy only.'
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $deliveryPath $ReceiptName) -Encoding utf8
if($drift.Count -or $extra.Count -or $sourceDrift.Count){throw 'Fixed identity drift found; inspect receipt.'}
