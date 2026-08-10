$root = Resolve-Path "docs"
$idxDir = Join-Path $root.Path "00-索引"
New-Item -ItemType Directory -Force -Path $idxDir | Out-Null

$typeMap = @{
    '01-方案设计' = '方案设计'
    '02-实施总结' = '实施总结'
    '03-测试报告' = '测试报告'
    '04-工具设计' = '工具设计'
    '05-框架架构' = '框架架构'
    '06-指南参考' = '指南参考'
}

$files = Get-ChildItem -Path $root.Path -Recurse -File -Filter *.md | Where-Object { $_.FullName -notlike (Join-Path $idxDir '*') }
$rows = @()
foreach($f in $files){
    $rel = $f.FullName.Substring($root.Path.Length + 1)
    $relU = $rel -replace '\\', '/'
    $top = $rel.Split('\')[0]
    $typeName = if($typeMap.ContainsKey($top)){ $typeMap[$top] } else { $top }
    if($f.Name -match '(\d{4})[-_]?(\d{2})[-_]?(\d{2})'){ $cd = $Matches[1] + '-' + $Matches[2] + '-' + $Matches[3] } else { $cd = '未知' }
    $lu = $f.LastWriteTime.ToString('yyyy-MM-dd')
    $sum = ''
    try{
        $lines = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.UTF8Encoding]::new($false))
        foreach($l in $lines){ if($l -match '^\s*#\s+(.*)'){ $sum = $Matches[1].Trim(); break } }
    }catch{}
    if($sum -eq ''){ $sum = $f.BaseName }
    $sum = $sum -replace '\|', '/'
    $rows += [pscustomobject]@{ Path=$relU; Type=$typeName; Cd=$cd; Lu=$lu; Sum=$sum }
}
$rows = $rows | Sort-Object Type, Cd, Path

$header = @'
# 文档索引

> 本文件由脚本自动生成，请勿手动编辑。查找文档请先在本表搜索关键词（路径、类型、日期、摘要）。
> 列说明：文档路径（相对 docs/，含年/月/日颗粒度）、类型、创建日期（取自文件名 -YYYYMMDD，未知则标“未知”）、最后更新（文件 LastWriteTime）、摘要（正文首行 # 标题）。

| 文档路径 | 类型 | 创建日期 | 最后更新 | 摘要 |
| --- | --- | --- | --- | --- |
'@
$body = ($rows | ForEach-Object { "| $($_.Path) | $($_.Type) | $($_.Cd) | $($_.Lu) | $($_.Sum) |" }) -join "`n"
[System.IO.File]::WriteAllText((Join-Path $idxDir '文档索引.md'), $header + $body, [System.Text.UTF8Encoding]::new($true))
"索引已生成，共 $($rows.Count) 条。" | Write-Output
