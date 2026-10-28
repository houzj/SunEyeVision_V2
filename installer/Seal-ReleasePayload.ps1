[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppPath,[string]$FrameworkOriginPath,[string]$SourceCommit)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Release.Common.ps1"
$root=Split-Path -Parent $PSScriptRoot
$appRoot=[IO.Path]::TrimEndingDirectorySeparator((Resolve-Path -LiteralPath $AppPath).Path)
$retiredBlob=[IO.Path]::GetFullPath((Join-Path $appRoot 'plugins/SunEyeVision.Tool.BlobAnalysis'))
$expectedRetired=[IO.Path]::Combine($appRoot,'plugins','SunEyeVision.Tool.BlobAnalysis')
if($retiredBlob -cne $expectedRetired -or -not $retiredBlob.StartsWith($appRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '退出工程清理路径越界'}
if(Test-Path -LiteralPath $retiredBlob){Remove-Item -LiteralPath $retiredBlob -Recurse -Force;Write-Output '已清理退出的Blob SDK发布目录。'}
$retiredImageSource=[IO.Path]::GetFullPath((Join-Path $appRoot 'plugins/SunEyeVision.Tool.ImageLoad'))
$expectedRetiredImageSource=[IO.Path]::Combine($appRoot,'plugins','SunEyeVision.Tool.ImageLoad')
if($retiredImageSource -cne $expectedRetiredImageSource -or -not $retiredImageSource.StartsWith($appRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '图像源退出工程清理路径越界'}
if(Test-Path -LiteralPath $retiredImageSource){Remove-Item -LiteralPath $retiredImageSource -Recurse -Force;Write-Output '已清理退出的图像源SDK发布目录。'}
[xml]$solution=Get-Content (Join-Path $root 'SunEyeVision.slnx') -Raw
$tools=@($solution.SelectNodes('//Project')|ForEach-Object{Get-Item (Join-Path $root $_.Path)}|
    Where-Object{(Get-Content $_.FullName -Raw)-match '<IsToolPlugin>true</IsToolPlugin>'})
$head=$SourceCommit
if([string]::IsNullOrWhiteSpace($head)) {
    $head=(& git -C $root rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0){throw 'Cannot identify the source commit.'}
}
$version=Get-Content (Join-Path $root 'version.json') -Raw|ConvertFrom-Json
$identity=Get-ReleaseIdentity $version
$payload=New-ReleasePayload -AppPath $AppPath -BuildId ('development-'+[guid]::NewGuid().ToString('N')) -SourceCommit $head `
    -ArtifactVersion $identity.ArtifactVersion -ExpectedPluginAssemblies $tools.BaseName -FrameworkOriginPath $FrameworkOriginPath
$sha=Write-ReleasePayload -AppPath $AppPath -Payload $payload
Write-Output "Build payload sealed: $sha"
