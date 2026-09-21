[CmdletBinding()]
param(
    # 源 PNG（默认从现有 app_logo.ico 的最大帧提取，保证与当前软件图标视觉一致）
    [string]$SourcePng = '',

    # 输出目录（默认 src/UI/Icons）
    [string]$IconsDir = '',

    # 是否同时把 solution.ico 生成为与软件图标一致（默认是）
    [bool]$MakeSolutionIcon = $true
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root      = Split-Path -Parent $scriptDir
if ([string]::IsNullOrEmpty($IconsDir)) { $IconsDir = Join-Path $root 'src\UI\Icons' }

$appIco = Join-Path $IconsDir 'app_logo.ico'
$solIco = Join-Path $IconsDir 'solution.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)

function Write-Step([string]$t) { Write-Output ('==== ' + $t + ' ====') }

# ---------------------------------------------------------------- backup
Write-Step 'backup existing icons'
$stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
$backupDir = Join-Path $root ('build_temp\icon-backup-' + $stamp)
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
foreach ($f in @($appIco, $solIco)) {
    if (Test-Path $f) {
        Copy-Item $f (Join-Path $backupDir (Split-Path -Leaf $f)) -Force
        Write-Output ('  backed up: ' + $f)
    }
}
Write-Output ('backup dir: ' + $backupDir)

# ---------------------------------------------------------------- resolve source png
Write-Step 'resolve source image'
if ([string]::IsNullOrEmpty($SourcePng)) {
    if (-not (Test-Path $appIco)) { throw ('app_logo.ico not found: ' + $appIco) }
    $b = [System.IO.File]::ReadAllBytes($appIco)
    $count = [BitConverter]::ToUInt16($b, 4)
    $best = $null
    for ($i = 0; $i -lt $count; $i++) {
        $off = 6 + $i * 16
        $w = [int]$b[$off]; if ($w -eq 0) { $w = 256 }
        $len = [BitConverter]::ToUInt32($b, $off + 8)
        $imgOff = [BitConverter]::ToUInt32($b, $off + 12)
        if (($null -eq $best) -or ($w -gt $best.W)) { $best = [pscustomobject]@{ W = $w; Off = $imgOff; Len = $len } }
    }
    $pngBytes = New-Object byte[] $best.Len
    [Array]::Copy($b, [int]$best.Off, $pngBytes, 0, [int]$best.Len)
    $SourcePng = Join-Path $root 'build_temp\icon-source.png'
    [System.IO.File]::WriteAllBytes($SourcePng, $pngBytes)
    Write-Output ('extracted ' + $best.W + 'x' + $best.W + ' frame -> ' + $SourcePng)
} else {
    Write-Output ('source png: ' + $SourcePng)
}
if (-not (Test-Path $SourcePng)) { throw ('source png not found: ' + $SourcePng) }

# ---------------------------------------------------------------- write multi-size ico
Add-Type -AssemblyName System.Drawing

function Write-MultiSizeIco([string]$srcPng, [string]$outIco, [int[]]$sizeList) {
    $img = [System.Drawing.Image]::FromFile($srcPng)
    try {
        $frames = @()
        foreach ($s in ($sizeList | Sort-Object -Descending)) {
            $bmp = New-Object System.Drawing.Bitmap($s, $s)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            try {
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $g.Clear([System.Drawing.Color]::Transparent)
                $g.DrawImage($img, 0, 0, $s, $s)
            } finally { $g.Dispose() }
            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $bmp.Dispose()
            $frames += [pscustomobject]@{ Size = $s; Data = $ms.ToArray() }
            $ms.Dispose()
        }

        $fs = [System.IO.File]::Create($outIco)
        $bw = New-Object System.IO.BinaryWriter($fs)
        try {
            $bw.Write([uint16]0)
            $bw.Write([uint16]1)
            $bw.Write([uint16]$frames.Count)
            $offset = 6 + 16 * $frames.Count
            foreach ($fr in $frames) {
                $dim = if ($fr.Size -ge 256) { [byte]0 } else { [byte]$fr.Size }
                $bw.Write($dim)
                $bw.Write($dim)
                $bw.Write([byte]0)
                $bw.Write([byte]0)
                $bw.Write([uint16]1)
                $bw.Write([uint16]32)
                $bw.Write([uint32]$fr.Data.Length)
                $bw.Write([uint32]$offset)
                $offset += $fr.Data.Length
            }
            foreach ($fr in $frames) { $bw.Write($fr.Data) }
        } finally { $bw.Dispose(); $fs.Dispose() }
        return $frames
    } finally { $img.Dispose() }
}

Write-Step 'generate app_logo.ico'
$f1 = Write-MultiSizeIco $SourcePng $appIco $sizes
Write-Output ('app_logo.ico   frames=' + $f1.Count + '  bytes=' + (Get-Item $appIco).Length)

if ($MakeSolutionIcon) {
    Write-Step 'generate solution.ico (same source as app icon)'
    $f2 = Write-MultiSizeIco $SourcePng $solIco $sizes
    Write-Output ('solution.ico   frames=' + $f2.Count + '  bytes=' + (Get-Item $solIco).Length)
}

# ---------------------------------------------------------------- verify
Write-Step 'verify frames'
function Show-Ico([string]$p) {
    $b = [System.IO.File]::ReadAllBytes($p)
    $n = [BitConverter]::ToUInt16($b, 4)
    $list = @()
    for ($i = 0; $i -lt $n; $i++) {
        $off = 6 + $i * 16
        $w = [int]$b[$off]; if ($w -eq 0) { $w = 256 }
        $list += ($w.ToString() + 'x' + $w)
    }
    Write-Output ('  ' + $p + '  ->  ' + ($list -join ', '))
}
Show-Ico $appIco
if ($MakeSolutionIcon) { Show-Ico $solIco }

Write-Step 'done'
Write-Output ('backup: ' + $backupDir)
