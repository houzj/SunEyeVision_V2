# 重建 材料.html / 存档.html
# 规则：
#   1) 列表只收正文；目录页（index.html / *-index.html）不进列表，
#      作为专区入口挂在专区标题右侧（idx）。
#   2) 历史归档 / 内部工具 目录直接进存档。
#   3) 标题含"已拆分"的页面进存档（空壳兜底分类，当前应为空组）。
#   4) index-old.html 归入历史归档，改名"旧版首页（已停用）"。
$ErrorActionPreference = 'Stop'
$enc = New-Object Text.UTF8Encoding($false)
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$tpl = [IO.File]::ReadAllText((Join-Path $root '_list.html'), $enc)

# 专区目录入口（不进列表，挂专区标题旁）
$idxMap = @{
  '能力对照' = @(@{ t = '能力对照目录'; f = '能力对照/capability-index.html' })
  '优化方案' = @(@{ t = '目录'; f = '优化方案/optimization-index.html' }, @{ t = '参考件'; f = '优化方案/references/index.html' })
}

$rootIndexNames = @('关系图.html','index.html','功能.html','工具.html','优化包.html','材料.html','存档.html','_list.html')
$matOrd = @('总览','框架设计','目标架构','现状分析','能力对照','SDK设计','关系图解','优化方案','证据审计','维护记录')
$arcOrd = @('内部工具','历史归档')
$mat = @{}; $arc = @{}

Get-ChildItem -Path $root -Recurse -Include *.html -File | ForEach-Object {
  $rel = $_.FullName.Substring($root.Length + 1)
  if ($rel -match '^主题\\') { return }
  $name = $_.Name
  if (($rel -notmatch '\\') -and ($rootIndexNames -contains $name)) { return }
  if ($name -eq 'index.html' -or $name -match '-index\.html$') { return }   # 目录页不进列表

  $h = [IO.File]::ReadAllText($_.FullName, $enc)
  $m = [regex]::Match($h, '<h1[^>]*>(.*?)</h1>', 'Singleline')
  $ti = if ($m.Success) { ($m.Groups[1].Value -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim() } else { '' }
  if ([string]::IsNullOrWhiteSpace($ti)) { $ti = [IO.Path]::GetFileNameWithoutExtension($name) }

  $sec = if ($rel -match '\\') { ($rel -split '\\')[0] } else { '总览' }
  $f = $rel -replace '\\', '/'
  $k = [int][math]::Round($_.Length / 1KB)
  $item = [pscustomobject]@{ t = $ti; f = $f; st = ''; k = $k; id = '' }

  $grp = $null
  if (@('历史归档','内部工具') -contains $sec) { $grp = $sec }
  elseif ($ti -match '已拆分|已按专题拆分') { $grp = '已拆分专题' }
  elseif ($name -eq 'index-old.html') { $grp = '历史归档'; $item.t = '旧版首页（已停用）' }

  if ($grp) {
    if (-not $arc.ContainsKey($grp)) { $arc[$grp] = New-Object Collections.ArrayList }
    [void]$arc[$grp].Add($item)
  } else {
    if (-not $mat.ContainsKey($sec)) { $mat[$sec] = New-Object Collections.ArrayList }
    [void]$mat[$sec].Add($item)
  }
}

$gM = @()
foreach ($s in $matOrd) {
  if ($mat.ContainsKey($s)) { $gM += [pscustomobject]@{ h = $s; items = @($mat[$s]); collapsed = $true; idx = $idxMap[$s] } }
}
foreach ($s in @($mat.Keys)) {
  if ($matOrd -notcontains $s) { $gM += [pscustomobject]@{ h = $s; items = @($mat[$s]); collapsed = $true; idx = $idxMap[$s] } }
}
$gA = @()
foreach ($s in $arcOrd) {
  if ($arc.ContainsKey($s)) { $gA += [pscustomobject]@{ h = $s; items = @($arc[$s]); collapsed = $false; idx = $null } }
}

$nM = 0; foreach ($g in $gM) { $nM += $g.items.Count }
$nA = 0; foreach ($g in $gA) { $nA += $g.items.Count }

$page1 = $tpl.Replace('__TITLE__', '材料 · ' + $nM + ' 篇').
  Replace('__NAV__', '<span class="cur">材料</span> <span class="sep">·</span> <a href="存档.html">存档</a>').
  Replace('__LEDE__', '在用的原始材料，按专区收着。已停用的旧版本和工具另放「存档」。').
  Replace('__DATA__', (ConvertTo-Json $gM -Compress -Depth 6))
$page2 = $tpl.Replace('__TITLE__', '存档 · ' + $nA + ' 篇').
  Replace('__NAV__', '<span class="cur">存档</span> <span class="sep">·</span> <a href="材料.html">材料</a>').
  Replace('__LEDE__', '已停用的旧版本和工具页。只留档，不建议从这里读。').
  Replace('__DATA__', (ConvertTo-Json $gA -Compress -Depth 6))

[IO.File]::WriteAllText((Join-Path $root '材料.html'), $page1, $enc)
[IO.File]::WriteAllText((Join-Path $root '存档.html'), $page2, $enc)

"材料页: $($gM.Count) 专区 / $nM 篇"
foreach ($g in $gM) { "  {0,-10} {1,4} 篇{2}" -f $g.h, $g.items.Count, $(if ($g.idx) { '  [目录入口: ' + (($g.idx | ForEach-Object { $_.t }) -join ' / ') + ']' } else { '' }) }
"存档页: $($gA.Count) 组 / $nA 篇"
foreach ($g in $gA) { "  {0,-12} {1,4} 篇" -f $g.h, $g.items.Count }

# ---- 首页搜索索引注入（index.html 的 __INDEX__） ----
$idx = New-Object Collections.ArrayList

function Get-Lede([string]$html) {
  $m = [regex]::Match($html, '<body[\s\S]*?<p[^>]*>([\s\S]*?)</p>')
  if (-not $m.Success) { return '' }
  $t = ($m.Groups[1].Value -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim()
  if ($t.Length -gt 60) { $t = $t.Substring(0, 60) + '…' }
  return $t
}

# 根入口页也进索引（材料/存档/功能/工具/优化包/关系图）
foreach ($n in @('功能.html','工具.html','优化包.html','关系图.html','材料.html','存档.html')) {
  $p = Join-Path $root $n
  if (Test-Path $p) {
    $h = [IO.File]::ReadAllText($p, $enc)
    $m = [regex]::Match($h, '<h1[^>]*>(.*?)</h1>', 'Singleline')
    $ti = if ($m.Success) { ($m.Groups[1].Value -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim() } else { [IO.Path]::GetFileNameWithoutExtension($n) }
    [void]$idx.Add([pscustomobject]@{ t = $ti; id = ''; f = $n; s = '入口页' })
  }
}

Get-ChildItem -Path $root -Recurse -Include *.html -File | ForEach-Object {
  $rel = $_.FullName.Substring($root.Length + 1)
  if ($rel -match '^主题\\') { return }
  if ($rel -notmatch '\\') { return }   # 根目录页面已由入口页段处理，不重复收录
  $name = $_.Name
  if ($name -in @('_list.html')) { return }

  $h = [IO.File]::ReadAllText($_.FullName, $enc)
  $m = [regex]::Match($h, '<h1[^>]*>(.*?)</h1>', 'Singleline')
  $ti = if ($m.Success) { ($m.Groups[1].Value -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim() } else { '' }
  if ([string]::IsNullOrWhiteSpace($ti)) { return }

  $id = ''
  if ($name -match '-(o|c|m|x)(\d{2})\.html$') { $id = ($matches[1] + $matches[2]).ToUpper() }
  elseif ($name -match '-t(\d{2})\.html$') { $id = 'T' + $matches[1] }
  elseif ($name -match '-tools-(\d+)\.html$') { $id = 'TOOLS-' + $matches[1] }

  $s = Get-Lede $h
  if (-not $s) { $s = ($rel -split '\\')[0] + ' · 参考归档' }

  [void]$idx.Add([pscustomobject]@{ t = $ti; id = $id; f = ($rel -replace '\\', '/'); s = $s })
}

$json = ConvertTo-Json @($idx) -Compress -Depth 4
$ip = Join-Path $root 'index.html'
$ih = [IO.File]::ReadAllText($ip, $enc)
$ih2 = [regex]::Replace($ih, 'const INDEX = (?:__INDEX__|\[[\s\S]*?\]);', ('const INDEX = ' + $json + ';'))
[IO.File]::WriteAllText($ip, $ih2, $enc)
"首页索引: $($idx.Count) 条" + $(if ($ih2 -eq $ih) { '（未变化）' } else { '（已注入）' })
