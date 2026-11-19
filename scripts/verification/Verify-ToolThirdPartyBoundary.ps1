param([string]$RepoRoot='D:\MyWork\SunEyeVision_V2')

$tools=@(
  [ordered]@{name='Blob'; root='tools\SunEyeVision.Tool.BlobAnalysis'; project='tools\SunEyeVision.Tool.BlobAnalysis\SunEyeVision.Tool.BlobAnalysis.csproj'},
  [ordered]@{name='ImageSource'; root='tools\SunEyeVision.Tool.ImageSource'; project='tools\SunEyeVision.Tool.ImageSource\SunEyeVision.Tool.ImageSource.csproj'}
)
$rows=@()
foreach($tool in $tools){
  $projectPath=Join-Path $RepoRoot $tool.project
  $project=Get-Content -LiteralPath $projectPath -Raw
  $directReference=$project -match 'halcondotnetxl|OpenCvSharp'
  $sourceFiles=@(Get-ChildItem -LiteralPath (Join-Path $RepoRoot $tool.root) -Recurse -File -Include *.cs | Where-Object {$_.FullName -notmatch '[\\/]obj[\\/]'} )
  $directTypes=@($sourceFiles | Where-Object {$_.FullName -notmatch '[\\/]Algorithms[\\/]'} | Select-String -Pattern 'using\s+HalconDotNet|using\s+OpenCvSharp|\bHImage\b|\bHRegion\b|\bHOperatorSet\b' -AllMatches)
  $algorithmFiles=@($sourceFiles | Where-Object {$_.FullName -match '[\\/]Algorithms[\\/]'} )
  $rows += [ordered]@{tool=$tool.name;check='third-party reference is paired with an explicit algorithm implementation boundary';pass=((-not $directReference) -or $algorithmFiles.Count -gt 0);details=if($directReference){'algorithm implementation may use operators; adapter owns type/resource/lifecycle boundary'}else{'no direct project reference'}}
  $rows += [ordered]@{tool=$tool.name;check='tool source has no third-party native types';pass=($directTypes.Count -eq 0);details=@($directTypes|ForEach-Object {$_.Path.Replace($RepoRoot+'\','')+':'+$_.LineNumber})}
}
$out=[ordered]@{generatedAt=(Get-Date).ToString('o');rule='algorithm implementation may use HALCON/OpenCvSharp operators; adapters own type/resource/lifecycle boundaries';checks=$rows;pass=(@($rows|Where-Object {-not $_.pass}).Count -eq 0)}
$outPath=Join-Path $RepoRoot '.testdata\tool-third-party-boundary-gate-20261119.json';$out|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $outPath -Encoding UTF8;$out|ConvertTo-Json -Depth 8
