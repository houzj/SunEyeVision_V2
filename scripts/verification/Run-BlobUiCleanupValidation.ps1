[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$product=Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$base=Join-Path $root 'Release/_validation/BlobUiCleanup-20261103'
$attempt=1;while(Test-Path (Join-Path $base ('attempt-{0:d3}' -f $attempt))){$attempt++}
$dir=Join-Path $base ('attempt-{0:d3}' -f $attempt);New-Item -ItemType Directory -Path $dir -Force|Out-Null
$refs=(@('SunEyeVision.Plugin.Contracts','SunEyeVision.Plugin.Wpf')|ForEach-Object {'<Reference Include="'+$_+'"><HintPath>'+[Security.SecurityElement]::Escape((Join-Path $product ($_+'.dll')))+'</HintPath><Private>false</Private></Reference>'}) -join "`n"
$source=Join-Path $PSScriptRoot 'BlobUiCleanupCases.cs'
$proj='<Project><PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets></PropertyGroup><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk"/><PropertyGroup><TargetFramework>net9.0-windows</TargetFramework><UseWPF>true</UseWPF><OutputType>Exe</OutputType><StartupObject>SafeEntry</StartupObject><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><OutputPath>out/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup>'+$refs+'<Compile Include="'+[Security.SecurityElement]::Escape($source)+'"/></ItemGroup><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>'
[IO.File]::WriteAllText((Join-Path $dir 'Check.csproj'),$proj)
[IO.File]::WriteAllText((Join-Path $dir 'NuGet.Config'),'<configuration><packageSources><clear/></packageSources></configuration>')
$runner=Get-Content (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Raw
$entry=[regex]::Match($runner,'(?s)\$entry=@''\r?\n(.*?)\r?\n''@').Groups[1].Value
if(!$entry){throw '未找到既有独立加载器'}
$entry=$entry.Replace('S2DocumentEditingCases','BlobUiCleanupCases').Replace('BindingFlags.Static|BindingFlags.NonPublic','BindingFlags.Static|BindingFlags.Public')
[IO.File]::WriteAllText((Join-Path $dir 'SafeEntry.cs'),$entry)
dotnet build (Join-Path $dir 'Check.csproj') -c Release *> (Join-Path $dir 'build.log')
if($LASTEXITCODE -ne 0){Get-Content (Join-Path $dir 'build.log') -Tail 15;throw '公共组件测试宿主未编译'}
$env:S2_SOURCE_ROOT=$root;$env:S2_PRODUCT_ROOT=$product;$env:S2_CASE_ROOT=$dir;$env:S2_TEST_HELPERS=Join-Path $root 'tests/bin/Release'
$env:S2_RUNTIME_PROBE=$null
& dotnet exec --depsfile (Join-Path $product 'SunEyeVision.UI.deps.json') --runtimeconfig (Join-Path $product 'SunEyeVision.UI.runtimeconfig.json') (Join-Path $dir 'out/Check.dll') *> (Join-Path $dir 'run.log')
$exit=$LASTEXITCODE
Get-Content (Join-Path $dir 'run.log') -Tail 25
$loaded=Get-Content (Join-Path $dir 'loaded-product.tsv')|ForEach-Object {$p=$_ -split "`t";[ordered]@{name=$p[0];path=$p[1];sha256=$p[2];matchesCandidate=([IO.Path]::GetDirectoryName($p[1]) -eq $product -and (Get-FileHash $p[1] -Algorithm SHA256).Hash -eq $p[2])}}
$names=@($loaded|ForEach-Object {$_.name})
$identityPassed=(@($loaded).Count -ge 2 -and @($loaded|Where-Object {!$_.matchesCandidate}).Count -eq 0 -and $names -contains 'SunEyeVision.Plugin.Wpf' -and $names -contains 'SunEyeVision.Plugin.Contracts')
$runText=Get-Content (Join-Path $dir 'run.log') -Raw
$passedCases=([regex]::Matches($runText,'(?m)^PASS ')).Count
$casesPassed=($passedCases -eq 4 -and $runText.Contains('公共清理4组通过'))
if(!$identityPassed -or !$casesPassed){$exit=1}
[ordered]@{scope='独立公共UI清理候选，不替代真实Blob三页/主仓/实际VS';exit=$exit;identityPassed=$identityPassed;passedCases=$passedCases;productSourceCommit=(git -C $root log -1 --format=%H -- src/Plugin.Wpf);commit=(git -C $root rev-parse HEAD);source=$source;sourceSha256=(Get-FileHash $source -Algorithm SHA256).Hash;runnerSha256=(Get-FileHash $PSCommandPath -Algorithm SHA256).Hash;product=$product;loaded=$loaded}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $dir 'receipt.json') -Encoding utf8
if($exit -ne 0){throw ('公共组件候选失败：'+$dir)}
