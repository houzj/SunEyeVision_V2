param([string]$EvidenceRoot='D:/MyWork/REA逆向测试')
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$graphFile=Join-Path $EvidenceRoot 'analysis/full-audit/s1-resource-closure-20261030/resource-graph.json'
$graph=Get-Content -LiteralPath $graphFile -Raw | ConvertFrom-Json
$original=@{}
$documents=@($graph.documents.PSObject.Properties | ForEach-Object {$_.Value})
foreach($document in $documents){
    $path=Join-Path $EvidenceRoot $document.path
    $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if($hash -ne $document.sha256){throw "原资源文件已改变：$path"}
    $text=Get-Content -LiteralPath $path -Raw
    foreach($match in [regex]::Matches($text,'/(?<assembly>[^/"\s;]+);component/(?<asset>[^"\r\n]+\.(?:png|jpg|bmp))')){
        $uri=$match.Value
        if($original.ContainsKey($uri)){continue}
        $asset=Join-Path (Join-Path $EvidenceRoot ('analysis/decompiled/'+$match.Groups['assembly'].Value)) $match.Groups['asset'].Value
                $direct=Test-Path -LiteralPath $asset
        $normalized=$asset.Replace(' ','-')
        $extracted=if($direct){$asset}elseif(Test-Path -LiteralPath $normalized){$normalized}else{$null}
        $original[$uri]=[ordered]@{uri=$uri;path=$asset;extractedPath=$extracted;resolution=if($direct){'direct-extracted-path'}elseif($extracted){'normalized-extraction-candidate-raw-resource-name-unverified'}else{'not-found'};sha256=if($extracted){(Get-FileHash -LiteralPath $extracted -Algorithm SHA256).Hash}else{$null}}
    }
}
$assets=@(Get-ChildItem -LiteralPath @((Join-Path $workspace 'src/Plugin.Wpf/Themes/Assets/ImageSources'),(Join-Path $workspace 'src/Plugin.Wpf/Themes/Assets/Inputs/Switch')) -File | Sort-Object Name | ForEach-Object {
    $hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    $matches=@($original.Values | Where-Object {$_.sha256 -eq $hash} | ForEach-Object {$_.uri} | Sort-Object)
    [ordered]@{productPath=[IO.Path]::GetRelativePath($workspace,$_.FullName);sha256=$hash;originalUris=$matches;assetIdentity=if(!$matches.Count){'unmatched-needs-review'}elseif(@($original.Values | Where-Object {$_.sha256 -eq $hash -and $_.resolution -eq 'direct-extracted-path'}).Count){'byte-identical'}else{'byte-identical-normalized-extraction-candidate'};templateAndRuntimeReview='not-proven-by-asset-hash'}
})
$result=[ordered]@{scope='S1图像源资产字节身份核收；不证明模板、运行主题、行为或完整控件一致';resourceGraphSha256=(Get-FileHash -LiteralPath $graphFile -Algorithm SHA256).Hash;originalAssets=@($original.Values | Sort-Object uri);productAssets=$assets}
$out=Join-Path $workspace 'docs/collaboration/evidence/S1_ASSET_IDENTITY_REVIEW_20261030.json'
$result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $out -Encoding utf8
$assets | ForEach-Object {'{0}: {1}' -f $_.productPath,$_.assetIdentity}
'原件不可定位资产数：'+@($original.Values | Where-Object {!$_.sha256}).Count