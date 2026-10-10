function Get-ReleaseIdentity {
    param([Parameter(Mandatory)]$Source, [switch]$Next)
    if ($Source.version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw '产品版本必须为三段数字且无前导零。' }
    if ($Source.channel -notin 'alpha', 'beta', 'rc', 'stable', 'local') { throw '无效发布渠道。' }
    foreach ($field in 'fileRevision', 'schemaVersion') {
        if ($Source.$field -isnot [long] -and $Source.$field -isnot [int]) { throw "$field 必须是整数。" }
        if ($Source.$field -lt 0) { throw "$field 不得为负数。" }
    }
    $revision = [int]$Source.fileRevision
    if ($Next) { $revision++ }
    foreach ($part in @($Source.version.Split('.')) + @($revision)) {
        if ([long]$part -gt 65534) { throw 'Windows 文件版本每段必须在 0..65534。' }
    }
    $date = [datetime]::MinValue
    if (![datetime]::TryParseExact($Source.releaseDate, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$date)) { throw '发布日期无效。' }
    $release = "$($Source.version).$revision"
    $artifact = $release
    $package = $Source.version
    if ($Source.channel -ne 'stable') { $artifact += "_$($Source.channel)"; $package += "-$($Source.channel).$revision" }
    [pscustomobject]@{Version=$Source.version; Channel=$Source.channel; Revision=$revision;
        ReleaseVersion=$release; ArtifactVersion=$artifact; PackageVersion=$package;
        FileVersion=$release; ReleaseDate=$Source.releaseDate; SchemaVersion=$Source.schemaVersion}
}

function Get-InstallerBranding {
    param([ValidateSet('alpha','beta','rc','stable','local')][string]$Channel,
        [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+(_(alpha|beta|rc|local))?$')][string]$ArtifactVersion)
    $suffix=if($Channel -eq 'stable'){''}else{"_$Channel"}
    if ($ArtifactVersion -notmatch ('^\d+\.\d+\.\d+\.\d+'+[regex]::Escape($suffix)+'$')) {throw '安装名称的版本与发布渠道不一致。'}
    if ($Channel -eq 'local') {
        return [pscustomobject]@{DisplayName="SunEyeVision $ArtifactVersion";DefaultDir='SunEyeVision Local';
            AppId='{{D25646BF-9B3A-420F-B507-FA34FBB43DF6}';AssociationId='SunEyeVision.Local.SolutionFile'}
    }
    # Keep the identity of the already distributed alpha installer for upgrades.
    [pscustomobject]@{DisplayName="SunEyeVision $ArtifactVersion";DefaultDir='SunEyeVision';
        AppId='{{7A2E9C41-3B58-4C7E-9E31-2F6D8B1A5C04}';AssociationId='SunEyeVision.SolutionFile'}
}

function Invoke-ReleaseNative {
    param([string]$Executable, [string[]]$Arguments, [switch]$Capture)
    if ($Capture) { $result = & $Executable @Arguments; $exitCode=$LASTEXITCODE }
    else { & $Executable @Arguments | Out-Host; $exitCode=$LASTEXITCODE }
    if ($exitCode -ne 0) { throw "$Executable 失败，退出码 $exitCode" }
    if ($Capture) { return ($result -join "`n") }
}

function Assert-ReleaseFileVersionAdvance {
    param([string]$Current, [string]$Previous)
    if ([version]$Current -le [version]$Previous) { throw "Windows 文件版本必须递增：当前 $Current，上次 $Previous。" }
}

function Assert-ReleasePayload {
    param([string]$Path, [string]$FileVersion, [string[]]$Plugins, [string]$IconSourceRoot)
    foreach ($file in 'SunEyeVision.UI.exe', 'SunEyeVision.Core.dll', 'SunEyeVision.Plugin.SDK.dll', 'halcondotnetxl.dll', 'halconxl.dll', 'Icons/suneyevision.ico', 'Icons/solution.ico') {
        if (!(Test-Path (Join-Path $Path $file))) { throw "载荷缺失：$file" }
    }
    if ($IconSourceRoot) {
        foreach ($name in 'suneyevision.ico', 'solution.ico') {
            if ((Get-FileHash (Join-Path $IconSourceRoot $name)).Hash -ne
                (Get-FileHash (Join-Path $Path "Icons/$name")).Hash) {
                throw "发布图标与当前品牌源不一致：$name"
            }
        }
    }
    foreach ($plugin in $Plugins) {
        if (!(Test-Path "$Path/plugins/$plugin/$plugin.dll")) { throw "插件缺失：$plugin" }
    }
    foreach ($file in Get-ChildItem $Path -Recurse -File) {
        if ($file.Name -match '\.Tests\.(dll|exe)$') { throw "测试程序集混入载荷：$($file.Name)" }
        if ($file.Name -like 'SunEyeVision*.dll' -or $file.Name -eq 'SunEyeVision.UI.exe') {
            if ($file.VersionInfo.FileVersion -ne $FileVersion) { throw "载荷版本不一致：$($file.FullName)" }
        }
    }
}

function Get-ReleaseTestSummary {
    param([string]$Path)
    $total=0; $passed=0; $failed=0
    foreach ($file in Get-ChildItem $Path -Filter '*.trx') {
        [xml]$result = Get-Content $file.FullName -Raw
        $counters = $result.TestRun.ResultSummary.Counters
        $total += [int]$counters.total; $passed += [int]$counters.passed; $failed += [int]$counters.failed
    }
    if ($total -eq 0 -or $failed -gt 0 -or $passed -ne $total) { throw '发布测试结果为空、失败或有未通过项。' }
    [ordered]@{total=$total;passed=$passed;failed=$failed}
}

function Get-ReleaseBinaryFileVersion {
    param($VersionInfo)
    @($VersionInfo.FileMajorPart, $VersionInfo.FileMinorPart, $VersionInfo.FileBuildPart, $VersionInfo.FilePrivatePart) -join '.'
}

function Write-ReleaseArtifacts {
    param([string]$OutputPath, [System.Collections.IDictionary]$Manifest)
    $setup = Join-Path $OutputPath $Manifest.installer
    $info = (Get-Item -LiteralPath $setup).VersionInfo
    if ((Get-ReleaseBinaryFileVersion $info) -ne $Manifest.fileVersion -or $info.ProductVersion.Trim() -ne $Manifest.artifactVersion) {
        throw '安装包二进制版本或文本产品版本不匹配。'
    }
    $sha = (Get-FileHash $setup -Algorithm SHA256).Hash
    $Manifest.sha256 = $sha
    "$sha  $($Manifest.installer)" | Set-Content "$setup.sha256" -Encoding utf8
    $Manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputPath "release-manifest-$($Manifest.artifactVersion).json") -Encoding utf8
    $sha
}

function Get-ReleaseSourceSnapshot {
    param([string]$Root)
    $paths = @(Invoke-ReleaseNative git @('-C',$Root,'-c','core.quotePath=false','ls-files','--cached','--others','--exclude-standard','--',
        'src','tools','tests','installer','Directory.Build.props','Directory.Build.targets') -Capture)
    $files = @($paths -split "`n" | Where-Object { $_ -and $_ -notlike '*.md' } | Sort-Object -Unique | ForEach-Object {
        $path = Join-Path $Root $_
        [ordered]@{path=$_;sha256=$(if(Test-Path -LiteralPath $path -PathType Leaf){(Get-FileHash -LiteralPath $path).Hash}else{'MISSING'})}
    })
    $json = ConvertTo-Json -InputObject $files -Depth 4 -Compress
    [ordered]@{sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json)));files=$files}
}
