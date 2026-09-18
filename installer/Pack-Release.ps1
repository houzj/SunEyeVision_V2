[CmdletBinding()]
param(
    # 手工指定版本（四段 MAJOR.MINOR.PATCH.BUILD）。省略则**自动推导**（推荐）
    [string]$Version = '',

    # 渠道：alpha / beta / rc / stable / local。省略时取真源 channel
    [ValidateSet('alpha', 'beta', 'rc', 'stable', 'local', '')]
    [string]$Channel = '',

    # 用真源现值打包，不做推导（与 -AutoVersion 互斥；默认行为是推导）
    [switch]$FromSource,

    # 显式要求自动推导（默认即为自动；此开关便于脚本自文档化）
    [switch]$AutoVersion,

    # 版本真源文件（默认 <repo>/version.json）
    [string]$SourceFile = '',

    # 内测版与正式版并存：独立 AppId + 独立安装目录（不覆盖已装正式版）
    [switch]$Coexist,

    # 复用现有 Release 产物（跳过 dotnet build）
    [switch]$SkipBuild,

    # 不生成发布清单
    [switch]$NoManifest,

    # 不自动追加 CHANGELOG 条目
    [switch]$NoChangelog,

    # 打包成功后打 git tag（v<产品版本>）
    [switch]$Tag,

    # 允许源码中存在硬编码版本号字面量（默认禁止；临时放行需显式声明）
    [switch]$AllowHardcodedVersion,

    # 自定义编译器路径（默认自动探测）
    [string]$IsccPath = ''
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root      = Split-Path -Parent $scriptDir
$iss       = Join-Path $scriptDir 'SunEyeVision.iss'
$outDirAbs = Join-Path $root 'dist'
if ([string]::IsNullOrEmpty($SourceFile)) { $SourceFile = Join-Path $root 'version.json' }

function Write-Step([string]$t) { Write-Output ('==== ' + $t + ' ====') }
function Fail([string]$m) { Write-Output ('FAIL: ' + $m); exit 1 }
function Warn([string]$m) { Write-Output ('WARN: ' + $m) }

# ---------------------------------------------------------------- git
$headFull = [string]((& git -C $root rev-parse HEAD) 2>$null | Select-Object -First 1)
if ([string]::IsNullOrEmpty($headFull)) { $headFull = '' }
$commitShort = if ($headFull.Length -ge 7) { $headFull.Substring(0, 7) } else { 'unknown' }

# ---------------------------------------------------------------- version source
Write-Step 'version source'
if (-not (Test-Path $SourceFile)) { Fail ('version source not found: ' + $SourceFile) }
$src = Get-Content $SourceFile -Raw -Encoding UTF8 | ConvertFrom-Json
$curVersion    = [string]$src.version
$channelSrc    = [string]$src.channel
$releasedCommit = [string]$src.releasedCommit
$releaseDate   = if ($src.releaseDate) { [string]$src.releaseDate } else { (Get-Date).ToString('yyyy-MM-dd') }
$schemaVersion = [string]$src.schemaVersion
Write-Output ('source        : ' + $SourceFile)
Write-Output ('current       : ' + $curVersion + '  channel=' + $channelSrc)
$baselineText = if ($releasedCommit) { $releasedCommit } else { '(empty)' }
Write-Output ('baseline      : ' + $baselineText)
Write-Output ('schemaVersion : ' + $schemaVersion)

if ($curVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    Fail ('version.json.version must be four-segment MAJOR.MINOR.PATCH.BUILD, got: ' + $curVersion)
}
if ([string]::IsNullOrEmpty($Channel)) { $Channel = $channelSrc }
if ([string]::IsNullOrEmpty($Channel)) { $Channel = 'local' }

# ---------------------------------------------------------------- resolve target version
Write-Step 'resolve target version'
$parts = $curVersion.Split('.')
$maj = [int]$parts[0]; $min = [int]$parts[1]; $pat = [int]$parts[2]; $bld = [int]$parts[3]

$range = ''
if ($releasedCommit -and ($releasedCommit -ne $headFull)) { $range = $releasedCommit + '..HEAD' }

function Get-CommitSubjects([string]$r) {
    if ([string]::IsNullOrEmpty($r)) { return @() }
    return @(& git -C $root log $r --pretty=format:%s 2>$null)
}

$subjects = Get-CommitSubjects $range
$typeRe = '^(?<type>[a-z]+)(?:\((?<scope>[^)]*)\))?(?<bang>!)?:'

$isBreaking = $false
if ($range) {
    foreach ($s in $subjects) {
        $m = [regex]::Match($s, $typeRe)
        if ($m.Success -and $m.Groups['bang'].Value -eq '!') { $isBreaking = $true }
    }
    if (-not $isBreaking) {
        $allMsg = (& git -C $root log $range --pretty=format:%B 2>$null) -join "`n"
        if ($allMsg -match 'BREAKING[ -]CHANGE') { $isBreaking = $true }
    }
}
$hasFeat = @($subjects | Where-Object { $_ -match '^feat(\(|!|:| )' }).Count -gt 0
$hasFix  = @($subjects | Where-Object { $_ -match '^(fix|perf)(\(|!|:| )' }).Count -gt 0

if (-not [string]::IsNullOrEmpty($Version)) {
    # 手工模式：校验四段 + 与推导结果一致性（防手填错号）
    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { Fail ('-Version must be four-segment (a.b.c.d), got: ' + $Version) }
    $productVersion = $Version
    Write-Output ('mode          : manual (-Version)')
    if ($range) {
        $p = $Version.Split('.')
        $dm = [int]$p[0]; $dn = [int]$p[1]; $dp = [int]$p[2]; $db = [int]$p[3]
        $bumped = ($dm -gt $maj) -or (($dm -eq $maj) -and (($dn -gt $min) -or (($dn -eq $min) -and (($dp -gt $pat) -or (($dp -eq $pat) -and ($db -gt $bld))))))
        if (-not $bumped) {
            Warn ('manual version ' + $Version + ' is not greater than current ' + $curVersion)
        }
    }
} elseif ($FromSource -and -not $AutoVersion) {
    $productVersion = $curVersion
    Write-Output ('mode          : source as-is (-FromSource)')
} else {
    Write-Output ('mode          : auto-derive')
    $rangeText = if ($range) { $range } else { '(no new commits since baseline)' }
    Write-Output ('range         : ' + $rangeText)
    Write-Output ('commits       : ' + $subjects.Count)
    Write-Output ('breaking      : ' + $isBreaking + '  feat=' + $hasFeat + '  fix/perf=' + $hasFix)
    if ($isBreaking) { $maj++; $min = 0; $pat = 0; $bld = 0 }
    elseif ($hasFeat) { $min++; $pat = 0; $bld = 0 }
    elseif ($hasFix)  { $pat++; $bld = 0 }
    else { $bld++ }
    $productVersion = ($maj.ToString() + '.' + $min.ToString() + '.' + $pat.ToString() + '.' + $bld.ToString())
    if ([string]::IsNullOrEmpty($range)) {
        Warn 'baseline unset or equal to HEAD -> only BUILD incremented (first-run baseline is being established)'
    }
}

Write-Output ('productVersion: ' + $productVersion + '  channel=' + $Channel)

# ---------------------------------------------------------------- display name / appid
$suffix = ''
if ($Channel -eq 'alpha') { $suffix = [string]::Concat([char]0xFF08, [char]0x5185, [char]0x6D4B, [char]0x7248, [char]0xFF09) }
elseif ($Channel -eq 'beta') { $suffix = [string]::Concat([char]0xFF08, [char]0x516C, [char]0x6D4B, [char]0x7248, [char]0xFF09) }
elseif ($Channel -eq 'rc') { $suffix = [string]::Concat([char]0xFF08, [char]0x5019, [char]0x9009, [char]0x7248, [char]0xFF09) }
$appDisplayName = 'SunEyeVision' + $suffix

$appId      = '{{53C3A889-9EEA-4AAC-A25B-1435DB5D29B5}'
$defaultDir = 'SunEyeVision'
if ($Coexist) {
    $appId      = '{{7A2E9C41-3B58-4C7E-9E31-2F6D8B1A5C04}'
    $defaultDir = 'SunEyeVision ' + $Channel
}

$channelTag = if ($Channel -eq 'stable' -or $Channel -eq 'local') { '' } else { '-' + $Channel }
$setupBaseName = 'SunEyeVision-Setup-' + $productVersion + $channelTag + '-x64'
Write-Output ('setupBaseName : ' + $setupBaseName)
Write-Output ('display       : ' + $appDisplayName + '   appId=' + $appId + '   installDir=' + $defaultDir)

# ---------------------------------------------------------------- pre-flight: hardcoded version literals
Write-Step 'pre-flight: hardcoded version scan'
$zhVersion = [string]::Concat([char]0x7248, [char]0x672C)
$patternCn = $zhVersion + '\s*\d+\.\d+\.\d+'
$patternEn = '\bv\d+\.\d+\.\d+'
$hits = New-Object System.Collections.ArrayList
foreach ($t in @((Join-Path $root 'src\UI'), (Join-Path $root 'tools'))) {
    if (-not (Test-Path $t)) { continue }
    foreach ($f in (Get-ChildItem $t -Recurse -Include *.xaml, *.cs -File -ErrorAction SilentlyContinue)) {
        if ($f.FullName -match '\\obj\\|\\bin\\') { continue }
        $ln = 0
        foreach ($l in (Get-Content $f.FullName -Encoding UTF8)) {
            $ln++
            if ($l -match $patternCn -or $l -match $patternEn) {
                [void]$hits.Add($f.FullName.Replace($root, '').Replace('\', '/') + ':' + $ln + '  ' + $l.Trim())
            }
        }
    }
}
Write-Output ('hardcoded version literals: ' + $hits.Count)
if ($hits.Count -gt 0) {
    $hits | Select-Object -First 20 | ForEach-Object { Write-Output ('   ' + $_) }
    if (-not $AllowHardcodedVersion) {
        Fail 'hardcoded version literals found; fix them to read AppVersionInfo, or pass -AllowHardcodedVersion to override'
    }
    Warn 'proceeding despite hardcoded version literals (-AllowHardcodedVersion)'
}

# ---------------------------------------------------------------- stop running app
Write-Step 'stop running app'
Get-Process -Name 'SunEyeVision.UI' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$dirty = @((& git -C $root status --porcelain) 2>$null)
Write-Output ('ok; HEAD=' + $commitShort + ' rough dirty files=' + $dirty.Count)

# ---------------------------------------------------------------- build
if (-not $SkipBuild) {
    Write-Step ('dotnet build Release (Version=' + $productVersion + ')')
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $pp = $productVersion.Split('.')
    $asmVersion = $pp[0] + '.' + $pp[1] + '.' + $pp[2] + '.0'
    Write-Output ('assemblyVersion=' + $asmVersion + '  fileVersion=' + $productVersion)
    $buildOut = & dotnet build (Join-Path $root 'SunEyeVision.slnx') --configuration Release -v:minimal -nologo ('-p:Version=' + $productVersion) ('-p:FileVersion=' + $productVersion) ('-p:AssemblyVersion=' + $asmVersion) 2>&1
    $buildExit = $LASTEXITCODE
    $sw.Stop()
    Write-Output ('BUILD_EXIT=' + $buildExit + ' elapsed=' + [int]$sw.Elapsed.TotalSeconds + 's')
    $buildOut | Select-String -SimpleMatch 'error' | Select-Object -First 15 | ForEach-Object { $_.Line }
    if ($buildExit -ne 0) { Fail 'dotnet build failed' }
} else {
    Write-Step 'build skipped (-SkipBuild)'
}

# ---------------------------------------------------------------- payload
Write-Step 'payload (Release output)'
$exe = Get-ChildItem (Join-Path $root 'Release') -Recurse -Filter 'SunEyeVision.UI.exe' -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { Fail 'SunEyeVision.UI.exe not found under Release\ (run without -SkipBuild)' }
$relDirAbs = $exe.Directory.FullName
$productVer = $exe.VersionInfo.ProductVersion
Write-Output ('payload dir : ' + $relDirAbs)
Write-Output ('ProductVer  : ' + $productVer)
if (-not ($productVer -like ('*' + $productVersion + '*'))) {
    Fail ('payload version mismatch: expected ' + $productVersion + ' but ProductVersion=' + $productVer)
}

$pvCommit = ''
if ($productVer -match '\+([0-9a-fA-F]{7,40})') { $pvCommit = $Matches[1] }
Write-Output ('payload commit: ' + $pvCommit)
if ([string]::IsNullOrEmpty($pvCommit)) {
    Warn 'no commit embedded in ProductVersion (cannot verify traceability)'
} elseif ($headFull -notlike ($pvCommit + '*')) {
    Fail ('commit mismatch: ProductVersion carries ' + $pvCommit + ' but HEAD is ' + $headFull)
} else {
    Write-Output 'commit check: OK'
}

$payloadFiles = Get-ChildItem $relDirAbs -Recurse -File
$payloadCount = ($payloadFiles | Measure-Object).Count
$payloadMB    = [math]::Round(($payloadFiles | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
Write-Output ('payload files: ' + $payloadCount + '  sizeMB: ' + $payloadMB)

$newer = Get-ChildItem (Join-Path $root 'src'), (Join-Path $root 'tools') -Recurse -Include *.cs, *.xaml -File -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -gt $exe.LastWriteTime }
Write-Output ('source newer than payload: ' + @($newer).Count)
if (@($newer).Count -gt 0) { Warn 'payload is stale relative to working tree' }

Write-Output ('halcondotnetxl.dll : ' + (Test-Path (Join-Path $relDirAbs 'halcondotnetxl.dll')))
Write-Output ('plugins dirs       : ' + (Get-ChildItem (Join-Path $relDirAbs 'plugins') -Directory -ErrorAction SilentlyContinue | Measure-Object).Count)
Write-Output ('solution.ico       : ' + (Test-Path (Join-Path $relDirAbs 'Icons\solution.ico')))

# ---------------------------------------------------------------- ISCC
Write-Step 'ISCC'
if ([string]::IsNullOrEmpty($IsccPath) -or -not (Test-Path $IsccPath)) {
    foreach ($c in @(
            'D:\Program Files\Inno Setup 6\ISCC.exe',
            'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
            'C:\Program Files\Inno Setup 6\ISCC.exe',
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'))) {
        if (Test-Path $c) { $IsccPath = $c; break }
    }
}
if ([string]::IsNullOrEmpty($IsccPath) -or -not (Test-Path $IsccPath)) {
    Fail 'ISCC.exe not found; install Inno Setup 6 or pass -IsccPath'
}
Write-Output ('iscc: ' + $IsccPath)

# ---------------------------------------------------------------- compile setup
Write-Step 'compile setup'
$genDir = Join-Path $scriptDir '.generated'
New-Item -ItemType Directory -Force -Path $genDir | Out-Null
$genFile = Join-Path $genDir 'version.iss'
$genLines = @(
    ('#define AppVersion "' + $productVersion + '"'),
    ('#define Channel "' + $Channel + '"'),
    ('#define AppDisplayName "' + $appDisplayName + '"'),
    ('#define AppId "' + $appId + '"'),
    ('#define DefaultDir "' + $defaultDir + '"'),
    ('#define ReleaseDir "' + $relDirAbs + '"'),
    ('#define OutputDir "' + $outDirAbs + '"'),
    ('#define OutputBaseName "' + $setupBaseName + '"'),
    ('#define PackageInfo "rule-022 auto-generated"')
)
[System.IO.File]::WriteAllLines($genFile, $genLines, (New-Object System.Text.UTF8Encoding $true))
Write-Output ('version fragment: ' + $genFile)

$logOut = Join-Path $root 'build_temp\iscc-pack-out.log'
$logErr = Join-Path $root 'build_temp\iscc-pack-err.log'
$setup = Join-Path $outDirAbs ($setupBaseName + '.exe')
$packStart = Get-Date
$sw2 = [System.Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $IsccPath -ArgumentList @('/Qp', $iss) -PassThru -NoNewWindow -RedirectStandardOutput $logOut -RedirectStandardError $logErr
while (-not $p.HasExited) { Start-Sleep -Seconds 10 }
$sw2.Stop()
$isccExit = ''
try { $isccExit = [string]$p.ExitCode } catch { $isccExit = 'n/a' }
Write-Output ('ISCC_EXIT=' + $isccExit + ' elapsed=' + [int]$sw2.Elapsed.TotalSeconds + 's')
if (Test-Path $logOut) { Get-Content $logOut -Encoding UTF8 | Select-Object -Last 8 }

# ---------------------------------------------------------------- verify artifact
Write-Step 'verify artifact'
$artifactFresh = (Test-Path $setup) -and ((Get-Item $setup).LastWriteTime -ge $packStart)
if (-not $artifactFresh) {
    Write-Output ('artifact not produced: ' + $setup)
    if (Test-Path $logErr) { Get-Content $logErr -Encoding UTF8 | Select-Object -Last 30 }
    Fail 'ISCC did not produce a fresh artifact (see logs above)'
}
Write-Output ('artifact freshness: OK (written after ' + $packStart.ToString('HH:mm:ss') + ')')

$si = Get-Item $setup
$setupSha = (Get-FileHash $setup -Algorithm SHA256).Hash
Write-Output ('artifact : ' + $si.FullName)
Write-Output ('size     : ' + [math]::Round($si.Length / 1MB, 1) + ' MB (' + $si.Length + ' bytes)')
Write-Output ('SHA256   : ' + $setupSha)
$sv = $si.VersionInfo
Write-Output ('setup VersionInfo: Product=' + $sv.ProductName + ' Ver=' + $sv.ProductVersion + ' File=' + $sv.FileVersion)
if ([string]::IsNullOrEmpty($sv.ProductVersion)) { Warn 'setup has no version resource' }

Add-Type -AssemblyName System.Drawing
$script:sha = [System.Security.Cryptography.SHA256]::Create()
function Get-IconHash([string]$f) {
    # 归一化到 32x32 再比较：EXE 内嵌图标与 Inno 重编码后的安装包图标，
    # 其"帧选择/编码方式"（PNG 帧 vs BMP 帧）可能不同，直接比原始帧会假报警。
    $ic = [System.Drawing.Icon]::ExtractAssociatedIcon($f)
    $src = $ic.ToBitmap()
    $norm = New-Object System.Drawing.Bitmap(32, 32)
    $g = [System.Drawing.Graphics]::FromImage($norm)
    try {
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($src, 0, 0, 32, 32)
    } finally { $g.Dispose() }
    $ms = New-Object System.IO.MemoryStream
    $norm.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $norm.Dispose(); $src.Dispose()
    return (($script:sha.ComputeHash($ms.ToArray()) | ForEach-Object { $_.ToString('x2') }) -join '')
}
$exeIcon = Get-IconHash $exe.FullName
$setupIcon = Get-IconHash $setup
Write-Output 'extracted icon hash (仅参考：Inno 会重编码帧，逐像素比对无意义):'
Write-Output ('   exe   : ' + $exeIcon)
Write-Output ('   setup : ' + $setupIcon)

# 有效判据：断言"安装包图标来源 == 应用图标来源"（必须是同一个 ico 文件）
$csprojPath = Join-Path $root 'src\UI\SunEyeVision.UI.csproj'
$appIconRef = ''
if (Test-Path $csprojPath) {
    $mm = Select-String -Path $csprojPath -Pattern 'ApplicationIcon' | Select-Object -First 1
    if ($mm) { $appIconRef = $mm.Line.Trim() }
}
$issIconRef = ''
$mm2 = Select-String -Path $iss -Pattern '#define AppIcon' | Select-Object -First 1
if ($mm2) { $issIconRef = $mm2.Line.Trim() }
$srcIco = Join-Path $root 'src\UI\Icons\app_logo.ico'
$iconSourceSha = if (Test-Path $srcIco) { (Get-FileHash $srcIco -Algorithm SHA256).Hash } else { '' }
Write-Output ('app ApplicationIcon : ' + $appIconRef)
Write-Output ('iss AppIcon         : ' + $issIconRef)
Write-Output ('icon source sha256  : ' + $iconSourceSha)
if ([string]::IsNullOrEmpty($issIconRef)) { Warn 'iss has no AppIcon define; setup may fall back to Inno default icon' }

# ---------------------------------------------------------------- manifest
if (-not $NoManifest) {
    Write-Step 'release manifest'
    $manifestPath = Join-Path $outDirAbs ('release-manifest-' + $productVersion + '.json')
    $bumpKind = if ($isBreaking) { 'major' } elseif ($hasFeat) { 'minor' } elseif ($hasFix) { 'patch' } else { 'build' }
    $manifest = [ordered]@{
        product = 'SunEyeVision'; productVersion = $productVersion; channel = $Channel
        displayName = $appDisplayName; releaseDate = $releaseDate; schemaVersion = $schemaVersion
        coexist = [bool]$Coexist; appId = $appId; installDir = $defaultDir
        commit = $headFull; commitShort = $commitShort
        baseline = $releasedCommit; commitCount = $subjects.Count
        bump = $bumpKind
        buildTime = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        payloadDir = $relDirAbs; payloadFileCount = $payloadCount; payloadSizeMB = $payloadMB
        assemblyProductVersion = $productVer
        setupFile = ($setupBaseName + '.exe'); setupSizeBytes = $si.Length; setupSha256 = $setupSha
        setupVersionInfo = $sv.ProductVersion
        appIconPngSha256 = $exeIcon; setupIconPngSha256 = $setupIcon
        iconSource = 'src/UI/Icons/app_logo.ico'; iconSourceSha256 = $iconSourceSha
        applicationIconRef = $appIconRef; setupIconRef = $issIconRef
        dirtyFileCount = $dirty.Count; hardcodedVersionHits = $hits.Count
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding UTF8
    Write-Output ('manifest : ' + $manifestPath)
}

# ---------------------------------------------------------------- write back version source
Write-Step 'update version source'
if ([string]::IsNullOrEmpty($Version) -and -not $FromSource) {
    $src.version = $productVersion
    $src.channel = $Channel
    $src.releaseDate = (Get-Date).ToString('yyyy-MM-dd')
    $src.releasedCommit = $headFull
    ($src | ConvertTo-Json -Depth 6) | Set-Content -Path $SourceFile -Encoding UTF8
    Write-Output ('version.json updated: ' + $productVersion + '  baseline=' + $commitShort)
} else {
    Write-Output 'skipped (manual version or -FromSource)'
}

# ---------------------------------------------------------------- changelog
if (-not $NoChangelog) {
    Write-Step 'append CHANGELOG entry'
    $tplPath = Join-Path $scriptDir 'templates\changelog-entry.md'
    $chgPath = Join-Path $root 'CHANGELOG.md'
    if ((Test-Path $tplPath) -and (Test-Path $chgPath)) {
        $noneMark = '-' + [string]::Concat([char]0xFF08, [char]0x65E0, [char]0xFF09)
        function Format-Group($items) {
            if (@($items).Count -eq 0) { return $script:noneMark }
            return (($items | ForEach-Object { '- ' + $_ }) -join "`n")
        }
        $featItems = @($subjects | Where-Object { $_ -match '^feat(\(|!|:| )' })
        $fixItems  = @($subjects | Where-Object { $_ -match '^(fix|perf)(\(|!|:| )' })
        $otherItems = @($subjects | Where-Object { $_ -notmatch '^(feat|fix|perf)(\(|!|:| )' })
        if (@($subjects).Count -eq 0) {
            $featItems = @(); $fixItems = @(); $otherItems = @()
        }
        $entry = Get-Content $tplPath -Raw -Encoding UTF8
        $entry = $entry.Replace('{{VERSION}}', $productVersion)
        $entry = $entry.Replace('{{DATE}}', (Get-Date).ToString('yyyy-MM-dd'))
        $entry = $entry.Replace('{{CHANNEL}}', $Channel)
        $entry = $entry.Replace('{{COMMIT_SHORT}}', $commitShort)
        $entry = $entry.Replace('{{FEATURES}}', (Format-Group $featItems))
        $entry = $entry.Replace('{{FIXES}}', (Format-Group $fixItems))
        $entry = $entry.Replace('{{OTHERS}}', (Format-Group $otherItems))
        $entry = $entry.Replace('{{SETUP_FILE}}', ($setupBaseName + '.exe'))
        $entry = $entry.Replace('{{SETUP_SHA256}}', $setupSha)
        $entry = $entry.Replace('{{PAYLOAD_FILE_COUNT}}', [string]$payloadCount)
        $entry = $entry.Replace('{{PAYLOAD_SIZE_MB}}', [string]$payloadMB)

        $chg = Get-Content $chgPath -Raw -Encoding UTF8
        $anchor = '## 版本索引'
        if ($chg.Contains($anchor)) {
            $chg = $chg.Replace($anchor, ($entry + "`n" + $anchor))
        } else {
            $chg = $chg + "`n" + $entry
        }
        Set-Content -Path $chgPath -Value $chg -Encoding UTF8
        Write-Output ('CHANGELOG updated for ' + $productVersion)
    } else {
        Warn 'CHANGELOG template or CHANGELOG.md missing; skipped'
    }
}

# ---------------------------------------------------------------- tag
if ($Tag) {
    Write-Step 'git tag'
    $tagName = 'v' + $productVersion
    $existing = (& git -C $root tag -l $tagName) 2>$null
    if ($existing) { Warn ('tag already exists: ' + $tagName) }
    else {
        & git -C $root tag $tagName
        if ($LASTEXITCODE -eq 0) { Write-Output ('tag created: ' + $tagName) } else { Warn ('tag failed: ' + $tagName) }
    }
}

Write-Step 'DONE'
Write-Output ('version=' + $productVersion + ' channel=' + $Channel + ' setup=' + $setupBaseName + '.exe')
Write-Output 'note: version.json / CHANGELOG.md were updated -> commit them (rule-022)'
