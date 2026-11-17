$ErrorActionPreference = 'Stop'
$repo = 'd:\MyWork\SunEyeVision_V2'
$ourJson = Join-Path $repo 'docs\collaboration\acceptance\our-schemas.json'
$ours = Get-Content $ourJson -Raw -Encoding UTF8 | ConvertFrom-Json
$outDir = Join-Path $repo 'docs\collaboration\acceptance'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

# ToolId -> 原件目录
$map = [ordered]@{
  FrameMean        = 'imvsframemeanmodu'
  ColorExtract     = 'imvscolorextractmodu'
  DivideImage      = 'imvsdivideimagemodu'
  IntensityMeasure = 'imvsintensitymeasuremodu'
  PixelCount       = 'imvspixelcountmodu'
  If               = 'ifbranchmodule'
  BlobLabels       = 'imvsblobfindlabelsmodu'
  MarkFind         = 'imvsmarkfindmodu'
  ContourMatch     = 'imvscontourmatchmodu'
  GrayMatch        = 'imvsgraymatchmoduva'
  ImageLoad        = 'imagesourcemodule'
  BlobAnalysis     = 'imvsblobfindmodu'
}

function Get-VmParams([string]$path) {
  if (-not (Test-Path $path)) { return @() }
  $json = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
  $list = New-Object System.Collections.ArrayList
  $stack = New-Object System.Collections.Stack
  foreach ($p in $json.parameters) { $stack.Push($p) }
  while ($stack.Count -gt 0) {
    $node = $stack.Pop()
    if ($node.type -eq 'ParamItem') {
      $name = ($node.children | Where-Object { $_.type -eq 'Name' } | Select-Object -First 1).text
      $def  = ($node.children | Where-Object { $_.type -eq 'DefaultValue' } | Select-Object -First 1).text
      if ($name) { [void]$list.Add([pscustomobject]@{ Name = $name; Default = $def }) }
    }
    foreach ($c in $node.children) { if ($c) { $stack.Push($c) } }
  }
  return $list.ToArray()
}

$summary = New-Object System.Collections.ArrayList
foreach ($tool in $map.Keys) {
  $vmDir  = Join-Path $repo ("docs\07-工具逆向\tools\" + $map[$tool])
  $vmFile = Join-Path $vmDir 'parameters.json'
  $vmParams = Get-VmParams $vmFile
  $our = $ours.$tool
  $ourFields = if ($our.ok) { $our.fields } else { @() }
  $ourDefaults = if ($our.ok) { $our.defaults } else { $null }

  $lines = New-Object System.Collections.ArrayList
  [void]$lines.Add("# D2 声明对照 · $tool")
  [void]$lines.Add("")
  [void]$lines.Add("> 自动生成：原件 $($map[$tool]) / 我方 SettingsSchema")
  [void]$lines.Add("")
  [void]$lines.Add("## 0 数量对比")
  [void]$lines.Add("")
  [void]$lines.Add("| 项 | 数量 |")
  [void]$lines.Add("|---|---|")
  [void]$lines.Add("| 原件运行参数（ParamItem） | $($vmParams.Count) |")
  [void]$lines.Add("| 我方声明字段 | $($ourFields.Count) |")
  [void]$lines.Add("")
  [void]$lines.Add("## 1 原件参数清单")
  [void]$lines.Add("")
  [void]$lines.Add("| # | 参数名 | 默认值 |")
  [void]$lines.Add("|---|---|---|")
  $i = 0
  foreach ($p in $vmParams) { $i++; [void]$lines.Add("| $i | ``$($p.Name)`` | $($p.Default) |") }
  [void]$lines.Add("")
  [void]$lines.Add("## 2 我方字段清单")
  [void]$lines.Add("")
  [void]$lines.Add("| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |")
  [void]$lines.Add("|---|---|---|---|---|---|---|---|---|")
  $i = 0
  foreach ($f in $ourFields) {
    $i++
    $def = ''
    if ($ourDefaults -and $ourDefaults.PSObject.Properties.Name -contains $f.id) { $def = $ourDefaults.($f.id) }
    $def = ($def -replace '\r?\n', ' ')
    [void]$lines.Add("| $i | ``$($f.id)`` | $($f.label) | $($f.page)/$($f.section) | $($f.min) | $($f.max) | $($f.increment) | $($f.decimals) | $def |")
  }
  [void]$lines.Add("")
  [void]$lines.Add("## 3 名称映射（待人工确认）")
  [void]$lines.Add("")
  [void]$lines.Add("原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：")
  [void]$lines.Add("")
  [void]$lines.Add("| 原件参数 | 我方候选字段 | 置信度 |")
  [void]$lines.Add("|---|---|---|")
  foreach ($p in $vmParams) {
    $key = ($p.Name -replace '[^A-Za-z0-9]', '').ToLower()
    $cand = @()
    foreach ($f in $ourFields) {
      $fid = ($f.id -replace '[^A-Za-z0-9]', '').ToLower()
      if ($fid -eq $key) { $cand += $f.id }
      elseif ($fid -like "*$key*" -or $key -like "*$fid*") { $cand += $f.id }
    }
    $conf = if ($cand.Count -eq 1) { '高' } elseif ($cand.Count -gt 1) { '中' } else { '**无匹配**' }
    [void]$lines.Add("| ``$($p.Name)`` | $($cand -join ', ') | $conf |")
  }
  [void]$lines.Add("")

  $file = Join-Path $outDir ("D2-声明对照-" + $tool + ".md")
  [System.IO.File]::WriteAllText($file, ($lines -join "`r`n"), [System.Text.UTF8Encoding]::new($false))

  $matched = 0
  foreach ($p in $vmParams) {
    $key = ($p.Name -replace '[^A-Za-z0-9]', '').ToLower()
    foreach ($f in $ourFields) {
      $fid = ($f.id -replace '[^A-Za-z0-9]', '').ToLower()
      if ($fid -eq $key -or $fid -like "*$key*" -or $key -like "*$fid*") { $matched++; break }
    }
  }
  [void]$summary.Add([pscustomobject]@{ Tool = $tool; VmParams = $vmParams.Count; OurFields = $ourFields.Count; NameMatched = $matched })
}

$summary | Format-Table -AutoSize | Out-String -Width 120
[System.IO.File]::WriteAllText((Join-Path $outDir 'D2-汇总.md'), (($summary | ConvertTo-Csv -NoTypeInformation) -join "`r`n"), [System.Text.UTF8Encoding]::new($false))
