# Rebuild ModuleControls/ and ../XmlParser/ from the locally installed VisionMaster 4.4.0.
#
# Usage (PowerShell 5.1):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\_import.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\_import.ps1 -ModuleSp "D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)"
#
# Requires: ilspycmd 11.x on PATH (dotnet tool install -g ilspycmd).
# Output:   this folder = 72 per-assembly directories + _sources.json; ../XmlParser = Apps.XmlParser.
# Note:     destructive - the target folders are recreated from scratch.
#           README.md is NOT generated here (kept as a hand-maintained document).

param(
    [string]$ModuleSp = 'D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)',
    [string]$XmlParserDll = 'D:\Program Files\VisionMaster4.4.0\Applications\myLibs\Apps.XmlParser.dll',
    [string]$WorkDir
)

$mcDir = $PSScriptRoot
$decRoot = Split-Path $mcDir -Parent
$xpDir = Join-Path $decRoot 'XmlParser'
if (-not $WorkDir) { $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) 'suneye-decomp-import' }

function Remove-Tree([string]$p) {
    if ([System.IO.Directory]::Exists($p)) { [System.IO.Directory]::Delete($p, $true) }
}

function Test-ManagedPe([string]$path) {
    try {
        $fs = [System.IO.File]::OpenRead($path)
        try {
            $br = New-Object System.IO.BinaryReader($fs)
            $fs.Position = 0x3C
            $peOff = $br.ReadInt32()
            $fs.Position = $peOff
            if ($br.ReadUInt32() -ne 0x00004550) { return $false }
            $fs.Position = $peOff + 24
            $magic = $br.ReadUInt16()
            $dirStart = if ($magic -eq 0x20B) { $peOff + 24 + 112 } else { $peOff + 24 + 96 }
            $fs.Position = $dirStart + (14 * 8)
            return ($br.ReadUInt32() -ne 0)
        } finally { $fs.Dispose() }
    } catch { return $false }
}

Remove-Tree $WorkDir
New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null

$errFile = Join-Path $WorkDir '_ilspy.err'
$records = New-Object System.Collections.ArrayList

function Invoke-Import($srcPath, $outName, $variant, $destRoot, $relDir) {
    $src = Get-Item -LiteralPath $srcPath
    $tmp = Join-Path $WorkDir $outName
    Remove-Tree $tmp
    if ([System.IO.File]::Exists($errFile)) { [System.IO.File]::Delete($errFile) }

    & ilspycmd -p -o $tmp $src.FullName 2>$errFile | Out-Null
    $files = @()
    if ([System.IO.Directory]::Exists($tmp)) { $files = @(Get-ChildItem -LiteralPath $tmp -Recurse -File) }

    $status = 'ok'; $note = ''
    if ($files.Count -eq 0) {
        $status = 'skipped'
        $note = if (Test-ManagedPe $src.FullName) { 'decompiler produced no output despite managed PE' } else { 'native assembly (no CLR runtime header), not decompilable' }
        Remove-Tree $tmp
    } else {
        $dest = Join-Path $destRoot $outName
        Remove-Tree $dest
        New-Item -ItemType Directory -Path $destRoot -Force | Out-Null
        Copy-Item -LiteralPath $tmp -Destination $dest -Recurse
    }

    $n = 0; $bytes = 0; $cs = 0; $xaml = 0
    if ($status -eq 'ok') {
        $of = @(Get-ChildItem -LiteralPath (Join-Path $destRoot $outName) -Recurse -File)
        $n = $of.Count
        $bytes = [int](($of | Measure-Object -Property Length -Sum).Sum)
        $cs = @($of | Where-Object { $_.Extension -eq '.cs' }).Count
        $xaml = @($of | Where-Object { $_.Extension -eq '.xaml' }).Count
    }

    [void]$records.Add([pscustomobject]@{
        outDir   = if ($status -eq 'ok') { $relDir + '/' + $outName } else { $null }
        assembly = $src.Name
        source   = $src.FullName.Replace('D:\Program Files\VisionMaster4.4.0\Applications\', '')
        variant  = $variant
        status   = $status
        note     = $note
        bytes    = $src.Length
        sha256   = (Get-FileHash -LiteralPath $src.FullName -Algorithm SHA256).Hash
        files    = $n
        cs       = $cs
        xaml     = $xaml
        outBytes = $bytes
    })
}

$dlls = Get-ChildItem -LiteralPath $ModuleSp -Filter *Control.dll -Recurse -File | Sort-Object FullName
Write-Output ('control dll = ' + $dlls.Count)
foreach ($d in $dlls) {
    $outName = $d.BaseName
    $variant = 'assembly'
    if ($d.BaseName -eq 'DynamicObjectConfigControl') {
        if ($d.FullName -match '\\\.CommonControl\\') { $variant = 'common-control-full' }
        elseif ($d.FullName -match '\\Logic\\') { $outName = 'DynamicObjectConfigControl.DataFilterModule'; $variant = 'module-local-variant' }
    }
    Invoke-Import $d.FullName $outName $variant $mcDir 'ModuleControls'
}
Invoke-Import $XmlParserDll 'Apps.XmlParser' 'library' $xpDir 'XmlParser'

$json = $records | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText((Join-Path $mcDir '_sources.json'), $json, (New-Object System.Text.UTF8Encoding($false)))

Remove-Tree $WorkDir

Write-Output ('records=' + $records.Count +
              ' ok=' + @($records | Where-Object { $_.status -eq 'ok' }).Count +
              ' skipped=' + @($records | Where-Object { $_.status -eq 'skipped' }).Count)
$records | Where-Object { $_.status -eq 'skipped' } | ForEach-Object { Write-Output ('  skipped: ' + $_.assembly + ' :: ' + $_.note) }
