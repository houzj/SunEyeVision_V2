# ============================================================
# generate_plan.ps1  v3.0 - 两级看板生成脚本
# 读取 plans/plan.json → 生成:
#   1. plans/plan.html          主控看板（模块/工具卡片 + 时间线 + 里程碑）
#   2. plans/tasks/{id}.html    每个模块/工具的子页面（完整开发档案）
# 用法: powershell -ExecutionPolicy Bypass -File plans/generate_plan.ps1
# 数据驱动（rule-016）：改 JSON → 跑脚本 → 刷新看板
# ============================================================
param(
    [string]$PlanDir = ""
)

$ErrorActionPreference = "Stop"

# 定位目录
if ($PlanDir -eq "") { $PlanDir = Split-Path -Parent $MyInvocation.MyCommand.Path }
$JsonPath = Join-Path $PlanDir "plan.json"
$HtmlPath = Join-Path $PlanDir "plan.html"
$TasksDir = Join-Path $PlanDir "tasks"

if (-not (Test-Path $JsonPath)) {
    Write-Host "错误: 未找到 plan.json ($JsonPath)" -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SunEyeVision 开发计划看板生成器 v3.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ---------- 读取 JSON ----------
$plan = Get-Content -Path $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$proj = $plan.project
$layers = $plan.layers
$modules = @($plan.modules)
$stages = @($plan.stages)
$milestones = @($plan.milestones)

Write-Host "读取计划: $($proj.name) v$($proj.version)" -ForegroundColor Green
Write-Host "模块/工具数: $($modules.Count), 阶段: $($stages.Count), 里程碑: $($milestones.Count)" -ForegroundColor Green

# ---------- 读取周追溯数据（weekly.json，可选） ----------
$weekly = $null
$WeeklyPath = Join-Path $PlanDir "weekly.json"
if (Test-Path $WeeklyPath) {
    try {
        # 注意：不用 ConvertFrom-Json -Depth（PowerShell 5.1 不支持该参数，会抛错）。
        # weekly.json 结构深度 < 10，默认深度足够。
        $weekly = Get-Content -Path $WeeklyPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Write-Host "周追溯数据: $($weekly.totalCommits) 提交 / $($weekly.weeks.Count) 周 / commits: $(@($weekly.moduleCommits.PSObject.Properties).Count) 模块" -ForegroundColor DarkGray
    } catch {
        Write-Host "周追溯数据解析失败: $($_.Exception.Message)（将跳过周追溯区块）" -ForegroundColor Yellow
    }
}
# 模块名映射（weekly 的 ID → 名称）
$weekModuleName = @{}
foreach ($m in $modules) { $weekModuleName[$m.id] = $m.name }

# ---------- 确保 tasks 目录存在 ----------
if (-not (Test-Path $TasksDir)) { New-Item -ItemType Directory -Path $TasksDir | Out-Null }

# ---------- 日期工具 ----------
$projStart = [datetime]::ParseExact($proj.startDate, "yyyy-MM-dd", $null)
$projEnd   = [datetime]::ParseExact($proj.endDate, "yyyy-MM-dd", $null)
$totalDays = [math]::Max(1, ($projEnd - $projStart).Days)

function Get-DaysBetween([string]$a, [string]$b) {
    $da = [datetime]::ParseExact($a, "yyyy-MM-dd", $null)
    $db = [datetime]::ParseExact($b, "yyyy-MM-dd", $null)
    return [math]::Max(0, ($db - $da).Days)
}

# ---------- ISO 周工具 ----------
# 解析 "2026-W37" → 周信息：年份/周号/周一/周日/该年总周数
# 注意：.NET DayOfWeek 周日=0...周六=6；ISO 周一=1...周日=7
function Get-WeekInfo([string]$weekStr) {
    $parts = $weekStr -split "-W"
    if ($parts.Count -ne 2) { return $null }
    $year = [int]$parts[0]
    $weekNum = [int]$parts[1]
    # 该周周一：1月4日所在周为 W01（ISO 8601），回退到周一
    $jan4 = [datetime]::ParseExact("$year-01-04", "yyyy-MM-dd", $null)
    $dow = ([int]$jan4.DayOfWeek + 6) % 7   # 转 ISO：周一=0...周日=6
    $mondayJan4 = $jan4.AddDays(-$dow)
    $monday = $mondayJan4.AddDays(($weekNum - 1) * 7)
    $sunday = $monday.AddDays(6)
    # 该年总周数：12月28日所在周的周号（永远在最后一周）
    $dec28 = [datetime]::ParseExact("$year-12-28", "yyyy-MM-dd", $null)
    $dowDec28 = ([int]$dec28.DayOfWeek + 6) % 7
    $mondayDec28 = $dec28.AddDays(-$dowDec28)
    $totalWeeks = [int][math]::Round((($mondayDec28 - $mondayJan4).Days) / 7) + 1
    return [pscustomobject]@{
        year       = $year
        weekNum    = $weekNum
        totalWeeks = $totalWeeks
        monday     = $monday
        sunday     = $sunday
    }
}

# 生成人类可读周标签："第 37/53 周 · 09-07~09-13"
function Get-WeekLabel([string]$weekStr) {
    $wi = Get-WeekInfo $weekStr
    if (-not $wi) { return $weekStr }
    return "第 $($wi.weekNum)/$($wi.totalWeeks) 周 · $($wi.monday.ToString('MM-dd'))~$($wi.sunday.ToString('MM-dd'))"
}

function Get-GanttStyle([string]$start, [string]$end) {
    $s = [datetime]::ParseExact($start, "yyyy-MM-dd", $null)
    $e = [datetime]::ParseExact($end, "yyyy-MM-dd", $null)
    $leftPct = [math]::Max(0, ($s - $projStart).Days) / $totalDays * 100
    $widthPct = [math]::Max(1.5, ($e - $s).Days + 1) / $totalDays * 100
    return "left:$($leftPct.ToString('F1'))%;width:$($widthPct.ToString('F1'))%"
}

# ---------- 状态/优先级/阶段配置 ----------
$statusMap = @{
    "todo"        = @{ label = "计划中"; cls = "st-todo";     color = "#B4B2A9" }
    "in_progress" = @{ label = "进行中"; cls = "st-progress"; color = "#378ADD" }
    "done"        = @{ label = "已完成"; cls = "st-done";     color = "#1D9E75" }
    "blocked"     = @{ label = "受阻";   cls = "st-blocked";  color = "#E24B4A" }
}
$phaseMap = @{
    "算法实现" = '<span class="phase ph-algo">算法实现</span>'
    "UI交互"   = '<span class="phase ph-ui">UI 交互</span>'
    "测试验证" = '<span class="phase ph-test">测试验证</span>'
}
$prioMap = @{
    "高" = '<span class="prio prio-high">高</span>'
    "中" = '<span class="prio prio-mid">中</span>'
    "低" = '<span class="prio prio-low">低</span>'
}

# 层颜色
$layerColor = @{}
foreach ($lk in @($layers.PSObject.Properties)) { $layerColor[$lk.Name] = $lk.Value.color }
$layerName  = @{}
foreach ($lk in @($layers.PSObject.Properties)) { $layerName[$lk.Name] = $lk.Value.name }

# 阶段 map（id → name）
$stageName = @{}
foreach ($st in $stages) { $stageName[$st.id] = $st.name }

# ---------- 统计 ----------
function Get-ModStatusCount($mod, [string]$status) {
    return @($mod.tasks | Where-Object { $_.status -eq $status }).Count
}
function Get-ModProgress($mod) {
    $tasks = @($mod.tasks)
    if ($tasks.Count -eq 0) { return 0 }
    $done = Get-ModStatusCount $mod "done"
    return [math]::Round($done / $tasks.Count * 100)
}
# 按任务进度自动推断模块状态（不依赖手工 status 字段，保证与实际进度匹配）
function Get-InferredStatus($mod) {
    $tasks = @($mod.tasks)
    if ($tasks.Count -eq 0) { return "todo" }
    $done = Get-ModStatusCount $mod "done"
    $inProg = Get-ModStatusCount $mod "in_progress"
    if ($done -eq $tasks.Count) { return "done" }
    # 历史模块：已完成但挂了"补测试"等 todo 任务 → 标为 in_progress 但标签特殊处理
    if ($mod.stage -eq "HIST" -and $done -gt 0) { return "in_progress" }
    if ($done -gt 0 -or $inProg -gt 0) { return "in_progress" }
    return "todo"
}
# 历史模块的标签文案：有未完成任务时显示"补测试中"而不是"进行中"
function Get-StatusLabel($mod, [string]$status) {
    if ($mod.stage -eq "HIST" -and $status -eq "in_progress") { return "补测试中" }
    return $statusMap[$status].label
}

# 总体统计
$allSubTasks = @()
foreach ($m in $modules) { $allSubTasks += @($m.tasks) }
$totalSub = $allSubTasks.Count
$doneSub = @($allSubTasks | Where-Object { $_.status -eq "done" }).Count
$overallPct = if ($totalSub -gt 0) { [math]::Round($doneSub / $totalSub * 100) } else { 0 }

# 层统计
$layerStats = ""
foreach ($lk in @($layers.PSObject.Properties)) {
    $name = $lk.Name
    $layerMods = @($modules | Where-Object { $_.layer -eq $name })
    $lt = @($layerMods | ForEach-Object { @($_.tasks) }).Count
    $ld = @($layerMods | ForEach-Object { @($_.tasks) } | Where-Object { $_.status -eq "done" }).Count
    $lp = if ($lt -gt 0) { [math]::Round($ld / $lt * 100) } else { 0 }
    $color = $layerColor[$name]
    $lname = $layers.$name.name
    $ldesc = $layers.$name.desc
    $layerStats += @"
            <div class="layer-stat">
              <div class="layer-stat-head">
                <span class="layer-dot" style="background:$color"></span>
                <strong>$lname</strong>
                <span class="layer-count">$ld / $lt 子任务</span>
                <span class="layer-pct">$lp%</span>
              </div>
              <div class="progress-bar"><div class="progress-fill" style="width:$lp%;background:$color"></div></div>
              <div class="layer-desc">$ldesc</div>
            </div>
"@
}

# ---------- 主控：开发路线总览图（完整历程：历史 → 当前 → 未来） ----------
# 本周 ISO 周（供路线图显示）
$todayNow = Get-Date
$ciInv = [System.Globalization.CultureInfo]::InvariantCulture
$calInv = $ciInv.Calendar
$isoNow = $calInv.GetWeekOfYear($todayNow, [System.Globalization.CalendarWeekRule]::FirstFourDayWeek, [DayOfWeek]::Monday)
$thisWeekLabel = "{0}-W{1:D2}" -f $todayNow.Year, $isoNow

# ---- 历史开发步骤（2026-01~07，已完成，从 plan.json 历史模块动态生成） ----
$histSteps = @()
$histNum = 0
foreach ($m in $modules) {
    # 历史模块：stage=HIST 或 start 在 2026-08 之前且已完成
    $isHist = ($m.stage -eq "HIST") -or ($m.status -eq "done" -and $m.start -lt "2026-08-01")
    if ($isHist) {
        $histNum++
        $histSteps += [pscustomobject]@{
            num   = $histNum
            name  = ($m.name -replace "（已完成[^）]*）", "") -replace "（基础完成[^）]*）", "" -replace "（.*）", ""
            stage = "HIST"
            modId = $m.id
            start = $m.start
        }
    }
}
# 按开始时间排序（历史开发顺序）；同日期的：视觉基础框架（M-HIST-CORE）优先排最前
$histSteps = @($histSteps | Sort-Object @{Expression={$_.start}}, @{Expression={if ($_.modId -eq "M-HIST-CORE") { 0 } else { 1 }}})
for ($hi = 0; $hi -lt $histSteps.Count; $hi++) { $histSteps[$hi].num = $hi + 1 }
$histCards = ""
foreach ($hs in $histSteps) {
    $histCards += @"
          <div class="route-step done">
            <div class="route-num">$($hs.num)</div>
            <div class="route-name">$($hs.name)</div>
            <div class="route-stage">HIST</div>
          </div>
"@
}

# ---- 当前 + 未来路线步骤 ----
$routeSteps = @(
    @{ id = "U0";  num = 1; name = "UI 重构+主题";       stage = "U0" },
    @{ id = "CA";  num = 2; name = "架构缓存机制";        stage = "CA" },
    @{ id = "CB";  num = 3; name = "条件分支完善";        stage = "CB" },
    @{ id = "CC";  num = 4; name = "测量工具链";          stage = "CC" },
    @{ id = "CD";  num = 5; name = "相机/通讯管理";       stage = "CD" },
    @{ id = "CE";  num = 6; name = "图像源+通信工具";     stage = "CE" },
    @{ id = "P4";  num = 7; name = "定位工具链";          stage = "P4" },
    @{ id = "P2";  num = 8; name = "相机标定";            stage = "P2" },
    @{ id = "P3";  num = 9; name = "手眼标定";            stage = "P3" },
    @{ id = "P6";  num = 10; name = "转换+机器人通信";    stage = "P6" },
    @{ id = "P8";  num = 11; name = "运行 UI";            stage = "P8" },
    @{ id = "P9";  num = 12; name = "交付上线";           stage = "P9" }
)
# 找到当前进行中的阶段（取最早 in_progress 的非 HIST/T0 阶段）
$currentStage = ""
foreach ($s in $stages) {
    if ($s.status -eq "in_progress" -and $s.id -ne "HIST" -and $s.id -ne "T0") { $currentStage = $s.id; break }
}
if ($currentStage -eq "") { $currentStage = "U0" }
$routeCards = ""
for ($i = 0; $i -lt $routeSteps.Count; $i++) {
    $rs = $routeSteps[$i]
    $cls = "route-step"
    # 判断该步骤对应阶段是否已完成（阶段 status=done）
    $stg = $null
    foreach ($s in $stages) { if ($s.id -eq $rs.stage) { $stg = $s; break } }
    if ($stg -and $stg.status -eq "done") { $cls += " done" }
    if ($rs.stage -eq $currentStage) { $cls += " current" }
    $badge = ""
    if ($rs.stage -eq $currentStage) { $badge = "<span class='route-now'>NOW</span>" }
    $routeCards += @"
          <div class="$cls">
            <div class="route-num">$($rs.num)</div>
            <div class="route-name">$($rs.name)$badge</div>
            <div class="route-stage">$($rs.stage)</div>
          </div>
"@
}
$routeOverviewHtml = @"
  <div class="card route-card">
    <h2>开发路线总览<span class="route-hint">本周 $(Get-WeekLabel $thisWeekLabel) · 所处步骤 → $currentStage</span></h2>
    <div class="route-seg">
      <span class="route-seg-label">历史开发（2026-01 ~ 07 · 已完成）</span>
      <div class="route-track">
        $histCards
      </div>
    </div>
    <div class="route-seg">
      <span class="route-seg-label">当前与未来路线（2026-08 ~ 2027-05）</span>
      <div class="route-track">
        $routeCards
      </div>
    </div>
    <div class="route-note">完整开发历程 · 绿色 = 已完成 · 蓝色高亮 = 当前进行中 · 按真实开发顺序</div>
  </div>
"@

# ---------- 主控：模块/工具列表（汇报用，一屏看全） ----------
$moduleRows = ""
foreach ($m in $modules) {
    $lc = $layerColor[$m.layer]
    $ln = $layerName[$m.layer]
    $infStatus = Get-InferredStatus $m
    $st = $statusMap[$infStatus]
    $pct = Get-ModProgress $m
    $subCount = @($m.tasks).Count
    $subDone = Get-ModStatusCount $m "done"
    $typeTag = if ($m.type -eq "tool") { "工具" } else { "模块" }
    $stageTag = $stageName[$m.stage]
    $stLabel = Get-StatusLabel $m $infStatus
    $moduleRows += @"
            <tr>
              <td class="td-id">$($m.id)</td>
              <td class="td-name"><a href="tasks/$($m.id).html" class="row-link">$($m.name)</a></td>
              <td><span class="layer-tag" style="color:$lc;border-color:$lc">$ln</span> <span class="type-tag2">$typeTag</span></td>
              <td class="td-stage">$stageTag</td>
              <td><span class="status $($st.cls)">$stLabel</span></td>
              <td>
                <div class="row-progress">
                  <div class="progress-bar" style="flex:1;min-width:60px"><div class="progress-fill" style="width:$pct%;background:$lc"></div></div>
                  <span class="row-pct">$pct%</span>
                </div>
              </td>
              <td class="td-sub">$subDone/$subCount</td>
              <td class="td-hours">$((($m.tasks | Measure-Object -Property hours -Sum).Sum))h</td>
              <td class="td-dep"><a class="row-go" href="tasks/$($m.id).html">打开 →</a></td>
            </tr>
"@
}


$moduleCards = ""
foreach ($m in $modules) {
    $lc = $layerColor[$m.layer]
    $ln = $layerName[$m.layer]
    $infStatus = Get-InferredStatus $m
    $st = $statusMap[$infStatus]
    $stLabel = Get-StatusLabel $m $infStatus
    $pct = Get-ModProgress $m
    $subCount = @($m.tasks).Count
    $subDone = Get-ModStatusCount $m "done"
    $typeTag = if ($m.type -eq "tool") { "工具" } else { "模块" }
    $stageTag = $stageName[$m.stage]
    # 状态显示：只在"当前开发中"模块加右上角徽标（避免与底部 status 重复）
    $curBadge = ""
    $curCls = ""
    if ($infStatus -eq "in_progress" -and $m.stage -eq $currentStage) {
        $curBadge = "<span class='cur-tag'>▶ 当前开发中</span>"
        $curCls = " mod-current"
    }
    # 其他进行中模块：仅底部 status 标签显示，避免视觉干扰
    $moduleCards += @"
        <a class="mod-card$curCls" href="tasks/$($m.id).html" style="border-top-color:$lc">
          <div class="mod-head">
            <span class="mod-id">$($m.id)</span>
            $curBadge
            <span class="layer-tag" style="color:$lc;border-color:$lc">$ln · $typeTag</span>
          </div>
          <div class="mod-name">$($m.name)</div>
          <div class="mod-stage">$stageTag · $($m.start) ~ $($m.end)</div>
          <div class="mod-progress">
            <div class="progress-bar"><div class="progress-fill" style="width:$pct%;background:$lc"></div></div>
            <span class="pct-text">$pct%</span>
          </div>
          <div class="mod-foot">
            <span class="status $($st.cls)">$stLabel</span>
            <span class="mod-sub">$subDone/$subCount 子任务</span>
          </div>
        </a>
"@
}

# ---------- 主控：阶段时间线（甘特） ----------
$ganttRows = ""
foreach ($st in $stages) {
    $stCls = $statusMap[$st.status].cls
    $style = Get-GanttStyle $st.start $st.end
    $ganttRows += @"
            <div class="gantt-row">
              <div class="gantt-label" title="$($st.name)">$($st.id) $($st.name)</div>
              <div class="gantt-track">
                <div class="gantt-bar $stCls" style="$style"></div>
              </div>
            </div>
"@
}

$ganttTicks = ""
$tickDate = $projStart
$tickIdx = 0
while ($tickDate -le $projEnd -and $tickIdx -lt 30) {
    $tickLeft = ($tickDate - $projStart).Days / $totalDays * 100
    $ganttTicks += "<div class='gantt-tick' style='left:$($tickLeft.ToString('F1'))%'>$($tickDate.ToString('MM-dd'))</div>"
    $tickDate = $tickDate.AddMonths(1)
    $tickIdx++
}

# ---------- 主控：临时任务池 ----------
$tempHtml = ""
if ($plan.tempTasks) {
    $tempList = @($plan.tempTasks)
    if ($tempList.Count -gt 0) {
        $tempRows = ""
        foreach ($tp in $tempList) {
            $ts = $statusMap[$tp.status]
            $tempRows += @"
            <div class="temp-item">
              <div class="temp-head">
                <span class="temp-id">$($tp.id)</span>
                <span class="temp-msg">$($tp.message)</span>
                <span class="temp-date">$($tp.date)</span>
                <span class="status $($ts.cls)">$($ts.label)</span>
              </div>
              <div class="temp-files">$((@($tp.files) | Select-Object -First 3) -join ' · ')</div>
            </div>
"@
        }
        $tempHtml = @"
  <div class="card temp-card">
    <h2>临时任务池（$($tempList.Count) 个 · 提交自动收录，未归入正式计划）</h2>
    $tempRows
  </div>
"@
    }
}

# ---------- 主控：周维度开发追溯 ----------
$weeklyHtml = ""
if ($weekly) {
    $maxCommits = 0
    foreach ($w in @($weekly.weeks)) { if ($w.commits -gt $maxCommits) { $maxCommits = $w.commits } }
    $weekRows = ""
    foreach ($w in @($weekly.weeks)) {
        $barW = if ($maxCommits -gt 0) { [math]::Round($w.commits / $maxCommits * 100) } else { 0 }
        $mods = ""
        $modCount = 0
        foreach ($kv in $w.modules.PSObject.Properties) {
            if ($modCount -ge 3) { break }
            $mid = $kv.Name
            $mname = if ($weekModuleName.ContainsKey($mid)) { $weekModuleName[$mid] } else { $mid }
            if ($mid -ne "UNKNOWN") {
                $mods += "<span class='wk-mod'>$mname ×$($kv.Value)</span>"
                $modCount++
            }
        }
        if ($mods -eq "") { $mods = "<span class='wk-mod muted'>未归类</span>" }
        $weekRows += @"
            <div class="wk-row">
              <div class="wk-label" title="$($w.week)">$(Get-WeekLabel $w.week)</div>
              <div class="wk-bar-wrap">
                <div class="wk-bar" style="width:$barW%"><span class="wk-count">$($w.commits)</span></div>
              </div>
              <div class="wk-mods">$mods</div>
            </div>
"@
    }
    $weeklyHtml = @"
  <div class="card">
    <h2>周维度开发追溯（$($weekly.totalCommits) 提交 · $($weekly.weeks.Count) 周 · 真实 git 用时）</h2>
    <div class="wk-list">
      $weekRows
    </div>
    <div class="wk-note">按提交时间统计，路径优先于关键词匹配（UNKNOWN 为未归类提交）· 由 generate_weekly.py 生成</div>
  </div>
"@
}

# ---------- 主控：每周计划（任务级 week 字段） ----------
$planWeeklyHtml = ""
$weekTasks = @{}
foreach ($m in $modules) {
    foreach ($t in @($m.tasks)) {
        if ($t.week) {
            if (-not $weekTasks.ContainsKey($t.week)) { $weekTasks[$t.week] = @() }
            $weekTasks[$t.week] += [pscustomobject]@{ modId = $m.id; modName = $m.name; taskId = $t.id; taskName = $t.name; hours = $t.hours; status = $t.status }
        }
    }
}
if ($weekTasks.Count -gt 0) {
    $today = Get-Date
    # 计算 ISO 周（周一为一周开始，FirstFourDayWeek）
    $ci = [System.Globalization.CultureInfo]::InvariantCulture
    $cal = $ci.Calendar
    $isoWeek = $cal.GetWeekOfYear($today, [System.Globalization.CalendarWeekRule]::FirstFourDayWeek, [DayOfWeek]::Monday)
    $thisWeek = "{0}-W{1:D2}" -f $today.Year, $isoWeek

    $planWeekRows = ""
    foreach ($wk in ($weekTasks.Keys | Sort-Object)) {
        $items = @($weekTasks[$wk])
        $totalH = ($items | Measure-Object -Property hours -Sum).Sum
        $isThis = ($wk -eq $thisWeek)
        $isNext = $false
        # 下周：本周 + 7 天
        $wkYear = [int]($wk.Substring(0,4)); $wkNum = [int]($wk.Substring(6))
        $wkDate = [datetime]::ParseExact($wkYear.ToString() + "-01-04", "yyyy-MM-dd", $null)
        $wkDate = $wkDate.AddDays(-[int]$wkDate.DayOfWeek)
        $wkMonday = $wkDate.AddDays(($wkNum - 1) * 7)
        if (($wkMonday - $today).Days -ge 1 -and ($wkMonday - $today).Days -le 7) { $isNext = $true }

        $badge = ""
        if ($isThis) { $badge = "<span class='wk-badge wk-this'>本周</span>" }
        elseif ($isNext) { $badge = "<span class='wk-badge wk-next'>下周</span>" }

        $taskChips = ""
        foreach ($it in $items) {
            $stCls = $statusMap[$it.status].cls
            $taskChips += "<span class='wk-task'><span class='wk-task-id'>$($it.taskId)</span>$($it.taskName) <span class='wk-task-h'>$($it.hours)h</span> <span class='status $stCls' style='font-size:10px'>$($statusMap[$it.status].label)</span></span>"
        }
        $planWeekRows += @"
            <div class="pw-row">
              <div class="pw-label" title="$wk">$(Get-WeekLabel $wk) $badge</div>
              <div class="pw-tasks">$taskChips</div>
              <div class="pw-hours">$totalH h</div>
            </div>
"@
    }
    $planWeeklyHtml = @"
  <div class="card">
    <h2>每周计划（$($weekTasks.Count) 周 · 按任务周排期 · 初版自动摊排可微调）</h2>
    <div class="pw-list">
      $planWeekRows
    </div>
    <div class="wk-note">任务 week 字段来自 plan.json（assign_weeks.py 摊排初版，可手动修改）· 本周/下周自动高亮</div>
  </div>
"@
}

# ---------- 主控：里程碑 ----------
$msHtml = ""
foreach ($m in $milestones) {
    $msHtml += @"
            <div class="milestone">
              <div class="ms-marker"></div>
              <div class="ms-body">
                <div class="ms-title"><strong>$($m.id): $($m.name)</strong><span class="ms-date">$($m.date)</span></div>
                <div class="ms-desc">$($m.desc)</div>
              </div>
            </div>
"@
}

# ---------- 主控 HTML ----------
$today = Get-Date -Format "yyyy-MM-dd"
$mainHtml = @"
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>$($proj.name) · 主控看板</title>
<style>
:root { --font: "Microsoft YaHei", "Segoe UI", sans-serif; }
* { margin:0; padding:0; box-sizing:border-box; }
body { font-family:var(--font); background:#f5f6f8; color:#2C2C2A; padding:24px; }
.container { max-width:1280px; margin:0 auto; }
.header { background:#fff; border-radius:14px; padding:24px 28px; margin-bottom:20px; border:1px solid #e8eaed; }
.header h1 { font-size:22px; font-weight:600; color:#185FA5; }
.header .sub { color:#666; font-size:13px; margin-top:6px; line-height:1.6; }
.header .meta { display:flex; gap:20px; margin-top:12px; font-size:12px; color:#888; flex-wrap:wrap; }
.overall { display:flex; align-items:center; gap:16px; margin-top:16px; }
.overall .pct-big { font-size:28px; font-weight:700; color:#185FA5; }
.overall .bar-wrap { flex:1; }
.progress-bar { height:10px; background:#eef0f2; border-radius:6px; overflow:hidden; }
.progress-fill { height:100%; background:#378ADD; border-radius:6px; transition:width .3s; }
.card { background:#fff; border-radius:14px; padding:20px 24px; margin-bottom:20px; border:1px solid #e8eaed; }
.card h2 { font-size:16px; font-weight:600; margin-bottom:14px; }
.grid-2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
.layer-stat { margin-bottom:14px; }
.layer-stat:last-child { margin-bottom:0; }
.layer-stat-head { display:flex; align-items:center; gap:8px; margin-bottom:6px; font-size:13px; }
.layer-dot { width:10px; height:10px; border-radius:50%; display:inline-block; }
.layer-count { color:#888; font-size:12px; margin-left:auto; }
.layer-pct { font-weight:600; font-size:13px; }
.layer-desc { font-size:12px; color:#999; margin-top:4px; }
.cards { display:grid; grid-template-columns:repeat(auto-fill,minmax(240px,1fr)); gap:14px; }
.mod-card { display:block; background:#fff; border:1px solid #e8eaed; border-top:3px solid #185FA5; border-radius:10px; padding:14px 16px; text-decoration:none; color:inherit; transition:box-shadow .15s,transform .15s; position:relative; }
.mod-card:hover { box-shadow:0 4px 14px rgba(0,0,0,.08); transform:translateY(-2px); }
.mod-head { display:flex; justify-content:space-between; align-items:center; margin-bottom:8px; }
.mod-id { font-family:Consolas,monospace; font-size:11px; color:#aaa; }
.layer-tag { font-size:11px; padding:2px 8px; border-radius:10px; border:1px solid; white-space:nowrap; }
.mod-name { font-size:14px; font-weight:600; margin-bottom:4px; }
.mod-stage { font-size:11px; color:#999; margin-bottom:10px; }
.mod-progress { display:flex; align-items:center; gap:8px; margin-bottom:8px; }
.pct-text { font-size:12px; font-weight:600; color:#185FA5; width:34px; text-align:right; }
.mod-foot { display:flex; justify-content:space-between; align-items:center; }
.mod-sub { font-size:11px; color:#aaa; }
.status { display:inline-block; font-size:11px; padding:2px 8px; border-radius:10px; white-space:nowrap; }
.st-todo { background:#f1efea; color:#888; }
.st-progress { background:#e6f1fb; color:#185FA5; }
.st-done { background:#e1f5ee; color:#0F6E56; }
.st-blocked { background:#fcebeb; color:#A32D2D; }
.gantt-row { display:flex; align-items:center; margin-top:6px; }
.gantt-label { width:190px; font-size:12px; color:#444; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; padding-right:10px; }
.gantt-track { position:relative; flex:1; height:22px; background:#f7f8fa; border-radius:4px; }
.gantt-bar { position:absolute; top:3px; height:16px; border-radius:3px; opacity:.85; min-width:6px; }
.gantt-bar.st-done { opacity:.45; }
.gantt-ticks { position:relative; height:18px; margin-top:4px; }
.gantt-tick { position:absolute; font-size:10px; color:#aaa; transform:translateX(-50%); }
.milestone { display:flex; gap:14px; position:relative; padding-bottom:16px; }
.milestone:not(:last-child)::before { content:""; position:absolute; left:6px; top:16px; bottom:0; width:2px; background:#e8eaed; }
.ms-marker { width:14px; height:14px; border-radius:50%; background:#185FA5; border:3px solid #dbe7f5; flex-shrink:0; margin-top:3px; }
.ms-body { flex:1; }
.ms-title { display:flex; justify-content:space-between; gap:10px; font-size:13px; }
.ms-date { font-size:12px; color:#888; }
.ms-desc { font-size:12px; color:#888; margin-top:2px; }
.footer { text-align:center; font-size:12px; color:#aaa; padding:16px 0 8px; }
.note { background:#fdf9ec; border:1px solid #f3e2b6; border-radius:10px; padding:12px 16px; font-size:12px; color:#7a5b1e; margin-bottom:20px; line-height:1.7; }
.temp-card { border-left:3px solid #993556; }
.temp-item { padding:10px 0; border-bottom:1px solid #f0f1f3; }
.temp-item:last-child { border-bottom:none; }
.temp-head { display:flex; align-items:center; gap:10px; font-size:13px; flex-wrap:wrap; }
.temp-id { font-family:Consolas,monospace; font-size:11px; color:#993556; background:#fbeaf0; padding:2px 8px; border-radius:4px; }
.temp-msg { font-weight:500; }
.temp-date { font-size:11px; color:#999; margin-left:auto; }
.temp-files { font-size:11px; color:#aaa; margin-top:4px; font-family:Consolas,monospace; }
.view-toggle { display:flex; gap:6px; margin-bottom:14px; }
.view-btn { font-size:12px; padding:4px 14px; border:1px solid #d5d8dd; border-radius:6px; background:#fff; color:#666; cursor:pointer; }
.view-btn.active { background:#185FA5; color:#fff; border-color:#185FA5; }
.cards { display:grid; grid-template-columns:repeat(auto-fill,minmax(240px,1fr)); gap:14px; }
.view-hidden { display:none; }
.mod-list { width:100%; border-collapse:collapse; font-size:13px; }
.mod-list th { text-align:left; font-size:12px; color:#888; font-weight:500; padding:8px 10px; border-bottom:2px solid #eef0f2; background:#fafbfc; }
.mod-list td { padding:8px 10px; border-bottom:1px solid #f0f1f3; vertical-align:middle; }
.mod-list tr:hover td { background:#f8fafc; }
.row-link { color:#333; text-decoration:none; font-weight:500; }
.row-link:hover { color:#185FA5; text-decoration:underline; }
.type-tag2 { font-size:11px; color:#999; }
.td-stage { font-size:12px; color:#888; white-space:nowrap; }
.row-progress { display:flex; align-items:center; gap:8px; }
.row-pct { font-size:12px; font-weight:600; color:#185FA5; width:36px; text-align:right; white-space:nowrap; }
.td-sub { font-size:12px; color:#888; white-space:nowrap; text-align:center; }
.td-hours { font-size:12px; color:#888; white-space:nowrap; text-align:right; }
.row-go { font-size:12px; color:#185FA5; text-decoration:none; white-space:nowrap; }
.row-go:hover { text-decoration:underline; }
.wk-list { display:flex; flex-direction:column; gap:4px; }
.wk-row { display:flex; align-items:center; gap:10px; font-size:12px; }
.wk-label { width:86px; font-family:Consolas,monospace; color:#888; white-space:nowrap; }
.wk-bar-wrap { width:120px; flex-shrink:0; }
.wk-bar { background:#185FA5; border-radius:3px; height:16px; min-width:4px; position:relative; }
.wk-count { position:absolute; right:4px; top:1px; font-size:10px; color:#fff; }
.wk-mods { display:flex; gap:8px; flex-wrap:wrap; flex:1; }
.wk-mod { font-size:11px; background:#eef4fb; color:#185FA5; border-radius:8px; padding:1px 8px; white-space:nowrap; }
.wk-mod.muted { background:#f1efea; color:#aaa; }
.wk-note { font-size:11px; color:#aaa; margin-top:10px; }
.pw-list { display:flex; flex-direction:column; gap:6px; }
.pw-row { display:flex; align-items:flex-start; gap:12px; padding:8px 0; border-bottom:1px solid #f0f1f3; }
.pw-row:last-child { border-bottom:none; }
.pw-label { width:110px; font-family:Consolas,monospace; font-size:12px; color:#444; font-weight:500; flex-shrink:0; padding-top:3px; }
.wk-badge { font-family:var(--font); font-size:10px; padding:1px 6px; border-radius:6px; margin-left:4px; }
.wk-this { background:#e1f5ee; color:#0F6E56; }
.wk-next { background:#e6f1fb; color:#185FA5; }
.pw-tasks { flex:1; display:flex; flex-wrap:wrap; gap:6px; }
.wk-task { font-size:11px; background:#f7f8fa; border:1px solid #e8eaed; border-radius:6px; padding:3px 8px; display:inline-flex; align-items:center; gap:6px; }
.wk-task-id { font-family:Consolas,monospace; color:#185FA5; }
.wk-task-h { color:#888; }
.pw-hours { font-size:12px; font-weight:600; color:#185FA5; white-space:nowrap; padding-top:3px; }
.route-card h2 { display:flex; align-items:center; gap:12px; }
.route-hint { font-size:12px; color:#888; font-weight:400; }
.route-track { display:grid; grid-template-columns:repeat(auto-fill,minmax(140px,1fr)); gap:8px; margin-top:12px; }
.route-step { border:1px solid #e0e3e8; border-radius:10px; padding:10px 12px; background:#fafbfc; position:relative; min-height:92px; box-sizing:border-box; }
.route-step.done { border-color:#9fdcbe; background:#f0faf5; }
.route-step.done .route-num { background:#1D9E75; }
.route-step.current { border-color:#185FA5; background:#e6f1fb; box-shadow:0 0 0 2px rgba(24,95,165,.15); }
.route-step.current .route-num { background:#185FA5; }
.route-num { width:20px; height:20px; border-radius:50%; background:#b4b2a9; color:#fff; font-size:11px; font-weight:700; display:flex; align-items:center; justify-content:center; margin-bottom:6px; }
.route-name { font-size:12px; font-weight:600; color:#333; line-height:1.4; }
.route-stage { font-size:10px; color:#999; font-family:Consolas,monospace; margin-top:4px; }
.route-now { font-size:9px; background:#185FA5; color:#fff; border-radius:6px; padding:1px 5px; margin-left:6px; font-weight:700; vertical-align:middle; }
.route-note { font-size:11px; color:#aaa; margin-top:10px; }
.route-seg { margin-top:14px; }
.route-seg:first-child { margin-top:6px; }
.route-seg-label { font-size:12px; font-weight:600; color:#666; display:block; margin-bottom:8px; padding-left:8px; border-left:3px solid #1D9E75; }
.route-seg:nth-of-type(2) .route-seg-label { border-left-color:#185FA5; }
.mod-current { border:2px solid #185FA5 !important; background:#f5f9fe; }
.cur-tag { display:inline-block; font-size:10px; font-weight:700; color:#fff; background:#185FA5; border-radius:8px; padding:2px 8px; margin-left:auto; }
@media (max-width:900px) { .grid-2 { grid-template-columns:1fr; } .gantt-label { width:130px; } }
</style>
</head>
<body>
<div class="container">

  <div class="header">
    <h1>$($proj.name)</h1>
    <div class="sub">$($proj.description)</div>
    <div class="meta">
      <span>版本: v$($proj.version)</span>
      <span>计划周期: $($proj.startDate) ~ $($proj.endDate)</span>
      <span>负责人: $($proj.owner)</span>
      <span>更新: $today</span>
      <span>模块/工具: $($modules.Count)</span>
      <span>子任务: $totalSub</span>
    </div>
    <div class="overall">
      <div class="pct-big">$overallPct%</div>
      <div class="bar-wrap">
        <div class="progress-bar"><div class="progress-fill" style="width:$overallPct%"></div></div>
      </div>
      <div style="font-size:12px;color:#888;white-space:nowrap">已完成 $doneSub / $totalSub 子任务</div>
    </div>
  </div>

  $routeOverviewHtml

  <div class="card">
    <h2>层级进度（源码层 / 工具层）</h2>
    <div class="grid-2">
      $layerStats
    </div>
  </div>

  <div class="card">
    <h2>功能模块与工具（$($modules.Count) 个）</h2>
    <div class="view-toggle">
      <button class="view-btn active" id="btnCard" onclick="switchView('card')">卡片视图</button>
      <button class="view-btn" id="btnList" onclick="switchView('list')">列表视图（汇报）</button>
    </div>
    <div class="cards" id="viewCard">
      $moduleCards
    </div>
    <div class="view-hidden" id="viewList">
      <table class="mod-list">
        <thead>
          <tr><th>ID</th><th>名称</th><th>层级</th><th>阶段</th><th>状态</th><th>进度</th><th>子任务</th><th>总工时</th><th></th></tr>
        </thead>
        <tbody>
          $moduleRows
        </tbody>
      </table>
    </div>
  </div>

  <script>
    function switchView(view) {
      var card = document.getElementById('viewCard');
      var list = document.getElementById('viewList');
      var bCard = document.getElementById('btnCard');
      var bList = document.getElementById('btnList');
      if (view === 'card') {
        card.classList.remove('view-hidden');
        list.classList.add('view-hidden');
        bCard.classList.add('active');
        bList.classList.remove('active');
      } else {
        card.classList.add('view-hidden');
        list.classList.remove('view-hidden');
        bCard.classList.remove('active');
        bList.classList.add('active');
      }
    }
  </script>

  <div class="card">
    <h2>阶段时间线（含 UI 交互与测试验证的正式开发周期）</h2>
    $ganttRows
    <div class="gantt-ticks">
      $ganttTicks
    </div>
  </div>

  $weeklyHtml

  $planWeeklyHtml

  $tempHtml

  <div class="card">
    <h2>里程碑</h2>
    $msHtml
  </div>

  <div class="footer">由 generate_plan.ps1 v3.0 自动生成 · 数据源 plan.json · 子页面在 plans/tasks/ · SunEyeVision</div>
</div>
</body>
</html>
"@

[System.IO.File]::WriteAllText($HtmlPath, $mainHtml, [System.Text.UTF8Encoding]::new($true))
Write-Host "主控看板已生成: $HtmlPath" -ForegroundColor Green

# ============================================================
# 子页面生成：每个模块/工具一个 HTML
# ============================================================
$subCount = 0
foreach ($m in $modules) {
    $lc = $layerColor[$m.layer]
    $ln = $layerName[$m.layer]
    $infStatus = Get-InferredStatus $m
    $st = $statusMap[$infStatus]
    $stLabel = Get-StatusLabel $m $infStatus
    $pct = Get-ModProgress $m
    $subTasks = @($m.tasks)
    $typeTag = if ($m.type -eq "tool") { "工具" } else { "功能模块" }
    $stageTag = $stageName[$m.stage]

    # 开发子任务表格
    $taskRows = ""
    foreach ($t in $subTasks) {
        $ts = $statusMap[$t.status]
        $ph = if ($phaseMap.ContainsKey($t.phase)) { $phaseMap[$t.phase] } else { $t.phase }
        $weekCell = if ($t.week) { "<span class='wk-cell' title='$($t.week)'>$(Get-WeekLabel $t.week)</span>" } else { "<span class='muted'>-</span>" }
        $taskRows += @"
            <tr>
              <td class="td-id">$($t.id)</td>
              <td class="td-name">$($t.name)</td>
              <td>$ph</td>
              <td>$($t.hours)h</td>
              <td>$weekCell</td>
              <td><span class="status $($ts.cls)">$($ts.label)</span></td>
            </tr>
"@
    }

    # 模块级周计划（该模块任务按周分组）
    $modWeekHtml = ""
    $weekGroups = @{}
    foreach ($t in $subTasks) {
        if ($t.week) {
            if (-not $weekGroups.ContainsKey($t.week)) { $weekGroups[$t.week] = @() }
            $weekGroups[$t.week] += $t
        }
    }
    if ($weekGroups.Count -gt 0) {
        $mwkRows = ""
        foreach ($wk in ($weekGroups.Keys | Sort-Object)) {
            $items = @($weekGroups[$wk])
            $h = ($items | Measure-Object -Property hours -Sum).Sum
            $chips = ""
            foreach ($it in $items) {
                $ts = $statusMap[$it.status]
                $chips += "<span class='wk-task'><span class='wk-task-id'>$($it.id)</span>$($it.name) <span class='wk-task-h'>$($it.hours)h</span> <span class='status $($ts.cls)' style='font-size:10px'>$($ts.label)</span></span>"
            }
            $mwkRows += @"
            <div class="pw-row">
              <div class="pw-label" title="$wk">$(Get-WeekLabel $wk)</div>
              <div class="pw-tasks">$chips</div>
              <div class="pw-hours">$h h</div>
            </div>
"@
        }
        $modWeekHtml = @"
  <div class="card">
    <h2>周计划（本模块任务排期）</h2>
    <div class="pw-list">
      $mwkRows
    </div>
  </div>
"@
    }

    # 验收标准列表
    $accHtml = ""
    if (@($m.acceptance).Count -gt 0) {
        $accHtml = "<ul class='acc-list'>"
        foreach ($a in @($m.acceptance)) { $accHtml += "<li>$a</li>" }
        $accHtml += "</ul>"
    } else {
        $accHtml = "<p class='muted'>暂无明确验收标准</p>"
    }

    # 关联文档
    $docsHtml = ""
    if (@($m.docs).Count -gt 0) {
        $docsHtml = "<ul class='doc-list'>"
        foreach ($d in @($m.docs)) {
            $fileName = Split-Path $d -Leaf
            $docsHtml += "<li><a href='../../../$d' target='_blank'>$fileName</a></li>"
        }
        $docsHtml += "</ul>"
    } else {
        $docsHtml = "<p class='muted'>暂无关联文档</p>"
    }

    # 真实 git 提交记录（直接读 plans/gitlogs/<id>.html 片段，由 generate_weekly.py 生成）
    $gitLogHtml = "<p class='muted'>无 git 提交记录（模块未匹配到提交）</p>"
    $gitLogFile = Join-Path $PlanDir "gitlogs\$($m.id).html"
    if (Test-Path $gitLogFile) {
        $gitLogHtml = Get-Content -Path $gitLogFile -Raw -Encoding UTF8
    }

    # 真实开发周分布（来自 weekly.json moduleWeeks）
    $weekDistHtml = "<p class='muted'>无周追溯数据（模块未匹配到提交）</p>"
    if ($weekly -and $weekly.moduleWeeks) {
        $mw = $weekly.moduleWeeks."$($m.id)"
        if ($mw) {
            $wkItems = ""
            $totalWk = 0
            foreach ($wi in @($mw)) {
                $totalWk += $wi.commits
                $wkItems += "<span class='wk-chip'>$($wi.week) ×$($wi.commits)</span>"
            }
            $weekDistHtml = "<p class='wk-total'>共 $totalWk 提交 / $($mw.Count) 周</p><div class='wk-chips'>$wkItems</div>"
        }
    }

    $subHtml = @"
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>$($m.id) $($m.name) · 开发管理</title>
<style>
:root { --font: "Microsoft YaHei", "Segoe UI", sans-serif; }
* { margin:0; padding:0; box-sizing:border-box; }
body { font-family:var(--font); background:#f5f6f8; color:#2C2C2A; padding:24px; }
.container { max-width:980px; margin:0 auto; }
.back { display:inline-block; margin-bottom:14px; font-size:13px; color:#185FA5; text-decoration:none; }
.back:hover { text-decoration:underline; }
.header { background:#fff; border-radius:14px; padding:22px 26px; margin-bottom:18px; border:1px solid #e8eaed; border-top:4px solid $lc; }
.header .id-line { display:flex; align-items:center; gap:10px; margin-bottom:6px; flex-wrap:wrap; }
.mod-id { font-family:Consolas,monospace; font-size:13px; color:#888; }
.type-tag { font-size:12px; padding:2px 10px; border-radius:10px; border:1px solid $lc; color:$lc; }
.header h1 { font-size:20px; font-weight:600; margin-bottom:6px; }
.meta-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(160px,1fr)); gap:10px; margin-top:12px; }
.meta-item { background:#f7f8fa; border-radius:8px; padding:8px 12px; }
.meta-item .k { font-size:11px; color:#999; }
.meta-item .v { font-size:13px; font-weight:500; margin-top:2px; }
.card { background:#fff; border-radius:14px; padding:20px 24px; margin-bottom:18px; border:1px solid #e8eaed; }
.card h2 { font-size:15px; font-weight:600; margin-bottom:12px; color:#333; }
.goal-box { background:#f0f7ff; border:1px solid #cfe4f7; border-radius:10px; padding:12px 16px; font-size:13px; color:#185FA5; margin-bottom:14px; line-height:1.6; }
.progress-bar { height:10px; background:#eef0f2; border-radius:6px; overflow:hidden; }
.progress-fill { height:100%; background:$lc; border-radius:6px; }
.progress-wrap { display:flex; align-items:center; gap:10px; }
.progress-wrap .pct { font-size:16px; font-weight:700; color:$lc; }
.status { display:inline-block; font-size:11px; padding:2px 8px; border-radius:10px; white-space:nowrap; }
.st-todo { background:#f1efea; color:#888; }
.st-progress { background:#e6f1fb; color:#185FA5; }
.st-done { background:#e1f5ee; color:#0F6E56; }
.st-blocked { background:#fcebeb; color:#A32D2D; }
table { width:100%; border-collapse:collapse; font-size:13px; }
th { text-align:left; font-size:12px; color:#888; font-weight:500; padding:8px 10px; border-bottom:2px solid #eef0f2; }
td { padding:9px 10px; border-bottom:1px solid #f0f1f3; vertical-align:middle; }
tr:hover td { background:#f8fafc; }
.td-id { font-family:Consolas,monospace; font-size:12px; color:#888; white-space:nowrap; }
.td-name { font-weight:500; }
.phase { font-size:11px; padding:2px 8px; border-radius:4px; white-space:nowrap; }
.ph-algo { background:#e6f1fb; color:#185FA5; }
.ph-ui { background:#faeeda; color:#854F0B; }
.ph-test { background:#e1f5ee; color:#0F6E56; }
.acc-list { padding-left:20px; font-size:13px; line-height:2; }
.muted { color:#aaa; font-size:13px; }
.doc-list { padding-left:20px; font-size:13px; line-height:2; }
.doc-list a { color:#185FA5; text-decoration:none; }
.doc-list a:hover { text-decoration:underline; }
.log-item { display:flex; gap:14px; padding:8px 0; border-bottom:1px solid #f0f1f3; font-size:13px; }
.log-item:last-child { border-bottom:none; }
.log-date { font-family:Consolas,monospace; font-size:12px; color:#888; white-space:nowrap; flex-shrink:0; }
.log-note { color:#444; line-height:1.6; }
.git-log { display:flex; flex-direction:column; max-height:420px; overflow-y:auto; border:1px solid #e8eaed; border-radius:8px; }
.git-log-item { display:flex; gap:10px; padding:6px 10px; border-bottom:1px solid #f0f1f3; font-size:12px; align-items:baseline; }
.git-log-item:last-child { border-bottom:none; }
.git-log-item:hover { background:#f7f9fc; }
.git-hash { font-family:Consolas,monospace; color:#185FA5; width:60px; flex-shrink:0; }
.git-date { font-family:Consolas,monospace; color:#888; width:80px; flex-shrink:0; }
.git-msg { color:#333; line-height:1.5; }
.wk-total { font-size:13px; color:#185FA5; font-weight:500; margin-bottom:8px; }
.wk-chips { display:flex; flex-wrap:wrap; gap:6px; }
.wk-chip { font-size:11px; font-family:Consolas,monospace; background:#eef4fb; color:#185FA5; border-radius:8px; padding:2px 8px; }
.wk-cell { font-size:11px; font-family:Consolas,monospace; background:#eef4fb; color:#185FA5; border-radius:6px; padding:2px 6px; white-space:nowrap; }
.pw-list { display:flex; flex-direction:column; gap:6px; }
.pw-row { display:flex; align-items:flex-start; gap:12px; padding:8px 0; border-bottom:1px solid #f0f1f3; }
.pw-row:last-child { border-bottom:none; }
.pw-label { width:100px; font-family:Consolas,monospace; font-size:12px; color:#444; font-weight:500; flex-shrink:0; padding-top:3px; }
.pw-tasks { flex:1; display:flex; flex-wrap:wrap; gap:6px; }
.pw-hours { font-size:12px; font-weight:600; color:#185FA5; white-space:nowrap; padding-top:3px; }
.footer { text-align:center; font-size:12px; color:#aaa; padding:16px 0 8px; }
</style>
</head>
<body>
<div class="container">
  <a class="back" href="../plan.html">&larr; 返回主控看板</a>

  <div class="header">
    <div class="id-line">
      <span class="mod-id">$($m.id)</span>
      <span class="type-tag">$ln · $typeTag</span>
      <span class="status $($st.cls)">$stLabel</span>
    </div>
    <h1>$($m.name)</h1>
    <div class="goal-box"><strong>目标：</strong>$($m.goal)</div>
    <div class="progress-wrap">
      <span class="pct">$pct%</span>
      <div class="progress-bar" style="flex:1"><div class="progress-fill" style="width:$pct%"></div></div>
    </div>
    <div class="meta-grid">
      <div class="meta-item"><div class="k">所属阶段</div><div class="v">$stageTag</div></div>
      <div class="meta-item"><div class="k">计划周期</div><div class="v">$($m.start) ~ $($m.end)</div></div>
      <div class="meta-item"><div class="k">开发子任务</div><div class="v">$(Get-ModStatusCount $m 'done')/@($subTasks).Count 完成</div></div>
      <div class="meta-item"><div class="k">总工时</div><div class="v">$((($subTasks | Measure-Object -Property hours -Sum).Sum))h</div></div>
    </div>
  </div>

  <div class="card">
    <h2>验收标准（Acceptance）</h2>
    $accHtml
  </div>

  <div class="card">
    <h2>开发子任务</h2>
    <table>
      <thead><tr><th>ID</th><th>任务</th><th>维度</th><th>工时</th><th>周排期</th><th>状态</th></tr></thead>
      <tbody>$taskRows</tbody>
    </table>
  </div>

  $modWeekHtml

  <div class="card">
    <h2>关联文档</h2>
    $docsHtml
  </div>

  <div class="card">
    <h2>开发日志（git 提交追溯，真实开发用时）</h2>
    $gitLogHtml
  </div>

  <div class="card">
    <h2>真实开发周分布（git 提交追溯）</h2>
    $weekDistHtml
  </div>

  <div class="footer">由 generate_plan.ps1 v3.0 自动生成 · 子页面 · SunEyeVision</div>
</div>
</body>
</html>
"@

    $subFile = Join-Path $TasksDir "$($m.id).html"
    [System.IO.File]::WriteAllText($subFile, $subHtml, [System.Text.UTF8Encoding]::new($true))
    $subCount++
}

Write-Host "子页面已生成: $subCount 个 (plans/tasks/)" -ForegroundColor Green
Write-Host ""
Write-Host "整体进度: $overallPct% ($doneSub/$totalSub 子任务)" -ForegroundColor Cyan
Write-Host "完成!" -ForegroundColor Green
Write-Host ""
