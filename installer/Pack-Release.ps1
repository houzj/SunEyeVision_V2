[CmdletBinding()]
param(
    [switch]$Local,
    [switch]$Repack,
    [switch]$CheckOnly,
    [switch]$RequireClean,
    [string]$IsccPath,
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'https://timestamp.digicert.com',
    [string]$SignToolPath,
    [string]$InstallationTestScript
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Release.Common.ps1"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$versionPath = Join-Path $root 'version.json'
$lock=$null; $transcript=$false; $published=$false; $versionUpdated=$false
$stage=$null; $destination=$null; $nextHash=$null
try {
    if ($Local -and $Repack) { throw '-Local 和 -Repack 不能同时使用。' }
    if (!$CheckOnly) {
        New-Item -ItemType Directory -Force $artifacts | Out-Null
        $lock = [IO.File]::Open("$artifacts/pack-release.lock", 'OpenOrCreate', 'ReadWrite', 'None')
    }
    $sourceHash=(Get-FileHash $versionPath).Hash
    $source=Get-Content $versionPath -Raw | ConvertFrom-Json
    $current=Get-ReleaseIdentity $source
    if (!$Local -and $current.Channel -eq 'local') { throw '正常发布请将 version.json.channel 设置为 alpha、beta、rc 或 stable。' }
    # Existing release folders also reserve their revisions, including interrupted counter updates.
    if (!$Local -and !$Repack) {
        foreach ($directory in Get-ChildItem "$root/dist" -Directory -ErrorAction SilentlyContinue) {
            if ($directory.Name -match ('^' + [regex]::Escape($source.version) + '\.(\d+)(?:_|$)')) {
                $source.fileRevision=[math]::Max([int]$source.fileRevision,[int]$Matches[1])
            }
        }
    }
    $identity=Get-ReleaseIdentity $source -Next:(!$Local -and !$Repack)
    if (!$Local -and !$Repack) { $identity.ReleaseDate=Get-Date -Format 'yyyy-MM-dd' }
    if ($Local) {
        $identity.Channel='local'
        $identity.ArtifactVersion="$($identity.FileVersion)_local"
        $identity.PackageVersion="$($identity.Version)-local.$($identity.Revision)"
    }
    $head=(Invoke-ReleaseNative git @('-C',$root,'rev-parse','HEAD') -Capture).Trim()
    $dirty=@((Invoke-ReleaseNative git @('-C',$root,'status','--porcelain') -Capture) -split "`n" | Where-Object {$_})
    if ($RequireClean -and $dirty.Count) { throw '-RequireClean 要求先提交工作区改动。' }
    Invoke-ReleaseNative dotnet @('--version')
    if (!$IsccPath) {
        $IsccPath=@('C:/Program Files (x86)/Inno Setup 6/ISCC.exe','C:/Program Files/Inno Setup 6/ISCC.exe',
            'D:/Program Files/Inno Setup 6/ISCC.exe',"$env:LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe") |
            Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
    }
    if (!$IsccPath -or !(Test-Path -LiteralPath $IsccPath)) { throw '找不到 Inno Setup 6；请指定 -IsccPath。' }
    $signed=![string]::IsNullOrWhiteSpace($CertificateThumbprint)
    if ($signed) {
        if (!$SignToolPath) {$SignToolPath=(Get-Command signtool.exe -ErrorAction Stop).Source}
        $certificate=Get-Item "Cert:/CurrentUser/My/$CertificateThumbprint" -ErrorAction Stop
        if (!$certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) {throw '代码签名证书无私钥或已过期。'}
        $usage=@($certificate.Extensions | Where-Object {$_ -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]} |
            ForEach-Object {$_.EnhancedKeyUsages} | Where-Object {$_.Value -eq '1.3.6.1.5.5.7.3.3'})
        if (!$usage.Count) {throw '指定证书不支持代码签名。'}
    }
    if ($InstallationTestScript -and !(Test-Path -LiteralPath $InstallationTestScript)) {throw '安装验收脚本不存在。'}
    [xml]$solution=Get-Content "$root/SunEyeVision.slnx" -Raw
    $projects=@($solution.SelectNodes('//Project') | ForEach-Object {Get-Item (Join-Path $root $_.Path)})
    foreach ($project in $projects) {
        if (!(Test-Path (Join-Path $project.DirectoryName 'packages.lock.json'))) {throw "缺少依赖锁：$($project.FullName)"}
    }
    $releaseVersion=$identity.ReleaseVersion
    $artifactVersion=$identity.ArtifactVersion
    $channel=$identity.Channel
    $setupName="SunEyeVision-Setup-$artifactVersion-x64"
    if ($Repack) {
        $previousPath="$root/dist/$artifactVersion/release-manifest-$artifactVersion.json"
        if (!(Test-Path $previousPath)) {throw '没有当前版本的发布记录，先执行正常发布。'}
        $previous=Get-Content $previousPath -Raw | ConvertFrom-Json
    }
    Write-Host "目标 $artifactVersion | 产品/文件版本 $releaseVersion | 签名=$signed | 源码改动数=$($dirty.Count)"
    if ($CheckOnly) {Write-Host '只读预检通过；未递增版本。';return}
    $snapshot=Get-ReleaseSourceSnapshot $root
    if ($Repack -and $snapshot.sha256 -ne $previous.sourceSnapshot.sha256) {throw '源码与已发布版本不同，请正常发布新版本。'}
    $buildId=(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+$head.Substring(0,8)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8)
    $stage=Join-Path $artifacts "staging/$buildId"
    New-Item -ItemType Directory -Force "$stage/output" | Out-Null
    Start-Transcript "$stage/pack.log" | Out-Null
    $transcript=$true
    $buildArgs=@("-p:PackagingRoot=$stage","-p:Version=$releaseVersion","-p:FileVersion=$releaseVersion",
        "-p:PackageVersion=$($identity.PackageVersion)","-p:VersionChannel=$channel","-p:VersionReleaseDate=$($identity.ReleaseDate)",
        '-p:ContinuousIntegrationBuild=true','-p:UseSharedCompilation=false','-p:PackagingRuntimeIdentifier=win-x64','-p:SelfContained=true')
    Invoke-ReleaseNative dotnet (@('restore',"$root/SunEyeVision.slnx",'--locked-mode')+$buildArgs)
    Invoke-ReleaseNative dotnet (@('build',"$root/SunEyeVision.slnx",'-c','Release','--no-restore','-m:1','-v:minimal')+$buildArgs)
    $filter='FullyQualifiedName~Communication|FullyQualifiedName~OpcUa|FullyQualifiedName~CameraCategory|FullyQualifiedName~ImageSourceSavePersistence|FullyQualifiedName~NumericUpDownBinding|FullyQualifiedName~TcpServerBroadcast|FullyQualifiedName~AboutWindowTests|FullyQualifiedName~ApplicationLifetimeGateTests|FullyQualifiedName~SolutionManagementTests|FullyQualifiedName~SolutionManagerUiTests|FullyQualifiedName~SolutionTransitionTests|FullyQualifiedName~ContinuousRunStopNotificationTests|FullyQualifiedName~BaselineIntegrationTests|FullyQualifiedName~SingleInstanceCoordinatorTests|FullyQualifiedName~BrandingLogoTests'
    Invoke-ReleaseNative dotnet (@('test',"$root/tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj",'-c','Release','--no-build','--no-restore',
        '--filter',$filter,'--logger','trx','--results-directory',"$stage/tests")+$buildArgs)
    $tests=Get-ReleaseTestSummary "$stage/tests"
    $uiProject=$projects | Where-Object {$_.Name -eq 'SunEyeVision.UI.csproj'} | Select-Object -First 1
    Invoke-ReleaseNative dotnet (@('publish',$uiProject.FullName,'-c','Release','-r','win-x64','--self-contained','true','--no-build','--no-restore',
        '-p:PublishSingleFile=false',"-p:PublishDir=$stage/app/")+$buildArgs)
    $app="$stage/app"
    $tools=@($projects | Where-Object {(Get-Content $_.FullName -Raw) -match '<IsToolPlugin>true</IsToolPlugin>'})
    Assert-ReleasePayload $app $releaseVersion $tools.BaseName -IconSourceRoot "$root/src/UI/Icons"
    $probePath="$stage/probe.json"
    $probe=Start-Process "$app/SunEyeVision.UI.exe" -ArgumentList @('--release-probe',"`"$probePath`"") -PassThru -WindowStyle Hidden
    if (!$probe.WaitForExit(30000)) {$probe.Kill();throw '程序探针超时。'}
    if ($probe.ExitCode -ne 0 -or !(Test-Path $probePath)) {throw '程序探针失败。'}
    $runtime=Get-Content $probePath -Raw | ConvertFrom-Json
    if ($runtime.releaseVersion -ne $releaseVersion -or $runtime.channel -ne $channel -or $runtime.commit -ne $head -or $runtime.releaseDate -ne $identity.ReleaseDate) {
        throw '程序版本、渠道、发布日期或提交身份不一致。'
    }
    Get-ChildItem $app -Recurse -File | Where-Object {$_.Extension -in '.pdb','.log' -or
        ($_.Extension -eq '.xml' -and (Test-Path (Join-Path $_.DirectoryName ($_.BaseName+'.dll')))) -or $_.Name -eq '.halcon-native-deployed'} | Remove-Item -Force
    if ($signed) {
        foreach ($file in Get-ChildItem $app -Recurse -File | Where-Object {$_.Name -like 'SunEyeVision*.dll' -or $_.Name -in 'CommonControl.dll','SunEyeVision.UI.exe'}) {
            Invoke-ReleaseNative $SignToolPath @('sign','/sha1',$CertificateThumbprint,'/fd','SHA256','/tr',$TimestampUrl,'/td','SHA256',$file.FullName)
            Invoke-ReleaseNative $SignToolPath @('verify','/pa',$file.FullName)
        }
    }
    $branding=Get-InstallerBranding $channel $artifactVersion
    $definitions=[ordered]@{AppVersion=$releaseVersion;ReleaseVersion=$artifactVersion;Channel=$channel;AppDisplayName=$branding.DisplayName;AppId=$branding.AppId;
        DefaultDir=$branding.DefaultDir;ReleaseDir=$app;OutputDir="$stage/output";OutputBaseName=$setupName;AppIcon="$app/Icons/suneyevision.ico";AssociationId=$branding.AssociationId}
    $isccArgs=@('/Qp');foreach($key in $definitions.Keys){$isccArgs+="/D$key=$($definitions[$key])"};$isccArgs+="$PSScriptRoot/SunEyeVision.iss"
    Invoke-ReleaseNative $IsccPath $isccArgs
    $setup="$stage/output/$setupName.exe"
    if ($signed) {
        Invoke-ReleaseNative $SignToolPath @('sign','/sha1',$CertificateThumbprint,'/fd','SHA256','/tr',$TimestampUrl,'/td','SHA256',$setup)
        Invoke-ReleaseNative $SignToolPath @('verify','/pa',$setup)
    }
    $installationVerified=$false
    if ($InstallationTestScript) {
        Invoke-ReleaseNative pwsh @('-NoProfile','-File',$InstallationTestScript,'-SetupPath',$setup,'-EvidencePath',"$stage/installation.json")
        $installation=Get-Content "$stage/installation.json" -Raw | ConvertFrom-Json
        foreach($check in 'install','launch','association','uninstall'){if($installation.$check -isnot [bool] -or !$installation.$check){throw "安装验收失败：$check"}}
        $installationVerified=$true
    }
    if ((Invoke-ReleaseNative git @('-C',$root,'rev-parse','HEAD') -Capture).Trim() -ne $head -or
        (Get-ReleaseSourceSnapshot $root).sha256 -ne $snapshot.sha256 -or (Get-FileHash $versionPath).Hash -ne $sourceHash) {throw '构建期间源码或版本声明变化，拒绝发布。'}
    $manifest=[ordered]@{releaseVersion=$releaseVersion;artifactVersion=$artifactVersion;fileVersion=$releaseVersion;channel=$channel;
        packageVersion=$identity.PackageVersion;releaseDate=$identity.ReleaseDate;commit=$head;workingTreeClean=($dirty.Count -eq 0);
        dirtyFiles=$dirty;sourceSnapshot=$snapshot;versionSourceBeforeSha256=$sourceHash;buildId=$buildId;schemaVersion=$identity.SchemaVersion;
        toolContractVersion=$runtime.toolContract;runtime='win-x64';selfContained=$true;signed=$signed;installationVerified=$installationVerified;
        defaultTheme=$runtime.defaultTheme;displayName=$branding.DisplayName;defaultInstallDirectory="{autopf}\$($branding.DefaultDir)";
        installerAppId=$branding.AppId;associationId=$branding.AssociationId;installer="$setupName.exe";sha256='';plugins=@($tools.BaseName);tests=$tests;
        icons=@{application='Icons/suneyevision.ico';shortcut='Icons/suneyevision.ico';solution='Icons/solution.ico';sourceHashesVerified=$true};
        externalPrerequisites=@('HALCON runtime license','Camera vendor SDK where required');
        payload=@(Get-ChildItem $app -Recurse -File | ForEach-Object {@{path=[IO.Path]::GetRelativePath($app,$_.FullName);sha256=(Get-FileHash $_.FullName).Hash}})}
    $sha=Write-ReleaseArtifacts "$stage/output" $manifest
    $destination=if($Local){"$root/dist/local/$buildId"}elseif($Repack){"$root/dist/repack/$artifactVersion/$buildId"}else{"$root/dist/$artifactVersion"}
    if(Test-Path $destination){throw "目标已存在：$destination"}
    New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
    if (!$Local -and !$Repack) {
        $source.fileRevision=$identity.Revision;$source.releaseDate=$identity.ReleaseDate
        $source.PSObject.Properties.Remove('channelRevision')
        [IO.File]::WriteAllText("$stage/version-next.json",($source | ConvertTo-Json -Depth 6)+"`n",[Text.UTF8Encoding]::new($false))
        $nextHash=(Get-FileHash "$stage/version-next.json").Hash
    }
    Move-Item -LiteralPath "$stage/output" -Destination $destination;$published=$true
    if (!$Local -and !$Repack) {
        [IO.File]::Replace("$stage/version-next.json",$versionPath,"$stage/version-before.json")
        $versionUpdated=$true
    }
    Write-Host "发布成功：$destination | SHA256=$sha"
    if ($versionUpdated){Write-Host "发布序号已更新为 $($identity.Revision)，下次正常发布自动递增。"}
} catch {
    if($versionUpdated -and (Get-FileHash $versionPath).Hash -eq $nextHash){[IO.File]::Replace("$stage/version-before.json",$versionPath,$null)}
    if($published){Move-Item -LiteralPath $destination -Destination "$stage/output"}
    Write-Error "发布失败；保留诊断目录 $stage。$($_.Exception.Message)" -ErrorAction Continue
    exit 1
} finally {
    if($transcript){Stop-Transcript | Out-Null};if($lock){$lock.Dispose()}
}
