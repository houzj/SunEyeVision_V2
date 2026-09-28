# 应用计划同步载荷到 plan.json（保持与 sync_plan.ps1 相同的序列化格式，避免大范围格式漂移）
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File plans/_apply_plan_sync.ps1
$ErrorActionPreference = 'Stop'
$logFile = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '_apply_out.txt'
if (Test-Path $logFile) { Remove-Item $logFile -Force }
function Log([string]$m) { Add-Content -Path $logFile -Value $m -Encoding UTF8 }
Log "== 应用计划同步 $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') =="
trap { Log ("ERROR: " + $_.Exception.Message); Log ($_.ScriptStackTrace); exit 1 }
$PlanDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PlanPath = Join-Path $PlanDir 'plan.json'
$PayloadPath = Join-Path $PlanDir '_sync_payload.json'

$plan = [System.IO.File]::ReadAllText($PlanPath, [System.Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$payload = [System.IO.File]::ReadAllText($PayloadPath, [System.Text.UTF8Encoding]::new($false)) | ConvertFrom-Json

$logAdded = 0
$logSkipped = 0
$statusChanged = 0

# ---------- 1. 追加模块日志 ----------
foreach ($mid in $payload.logs.PSObject.Properties.Name) {
    $mod = $plan.modules | Where-Object { $_.id -eq $mid }
    if (-not $mod) { Log "  警告: 模块不存在 $mid，日志未写入"; continue }
    $existing = @($mod.log)
    $notes = @()
    foreach ($e in $existing) { if ($e.note) { $notes += $e.note } }
    $add = @()
    foreach ($item in $payload.logs.$mid) {
        $hashTag = ($item.note -split '\]')[0] + ']'
        $dup = $false
        foreach ($n in $notes) { if ($n.StartsWith($hashTag)) { $dup = $true; break } }
        if ($dup) { $logSkipped++; continue }
        $add += [PSCustomObject]@{ date = $item.date; note = $item.note }
    }
    if ($add.Count -gt 0) {
        $mod.log = @($existing) + $add
        $logAdded += $add.Count
        Log ("  {0,-18} 日志 +{1}" -f $mid, $add.Count)
    }
}

# ---------- 2. 日志去重（同一提交可能被「语义批量同步」与「hook 单提交同步」各记一次） ----------
# 只对 note 形如 "[<hash>] ..." 的条目按 hash 去重，人工写的无 hash 备注一律保留。
$dedupRemoved = 0
foreach ($mod in $plan.modules) {
    $logs = @($mod.log)
    if ($logs.Count -lt 2) { continue }
    $seen = @{}
    $keptLogs = @()
    foreach ($e in $logs) {
        $note = [string]$e.note
        $m = [regex]::Match($note, '^\[([0-9a-fA-F]{7,40})\]')
        if ($m.Success) {
            $key = $m.Groups[1].Value.ToLower().Substring(0, 7)
            if ($seen.ContainsKey($key)) { $dedupRemoved++; continue }
            $seen[$key] = $true
        }
        $keptLogs += $e
    }
    if ($keptLogs.Count -ne $logs.Count) {
        $mod.log = $keptLogs
        Log ("  日志去重 {0}: -{1} 条" -f $mod.id, ($logs.Count - $keptLogs.Count))
    }
}
if ($dedupRemoved -gt 0) { $logSkipped += $dedupRemoved }

# ---------- 3. 模块状态 ----------
foreach ($mid in $payload.moduleStatus.PSObject.Properties.Name) {
    $mod = $plan.modules | Where-Object { $_.id -eq $mid }
    if (-not $mod) { continue }
    $newStatus = $payload.moduleStatus.$mid
    if ($mod.status -ne $newStatus) {
        Log ("  模块状态 {0}: {1} -> {2}" -f $mid, $mod.status, $newStatus)
        $mod.status = $newStatus
        $statusChanged++
    }
}

# ---------- 3. 子任务状态 ----------
foreach ($mid in $payload.taskStatus.PSObject.Properties.Name) {
    $mod = $plan.modules | Where-Object { $_.id -eq $mid }
    if (-not $mod) { Log "  警告: 模块不存在 $mid，子任务状态未更新"; continue }
    foreach ($tid in $payload.taskStatus.$mid.PSObject.Properties.Name) {
        $newStatus = $payload.taskStatus.$mid.$tid
        $hit = $mod.tasks | Where-Object { $_.id -eq $tid }   # 模块内定位，避免跨模块 ID 撞名
        if ($hit -and $hit.status -ne $newStatus) {
            Log ("  子任务 {0}/{1}: {2} -> {3}" -f $mid, $tid, $hit.status, $newStatus)
            $hit.status = $newStatus
            $statusChanged++
        }
    }
}

# ---------- 4. 阶段 / 里程碑字段修正 ----------
if ($payload.PSObject.Properties['stageFields']) {
    foreach ($sid in $payload.stageFields.PSObject.Properties.Name) {
        $stg = $plan.stages | Where-Object { $_.id -eq $sid }
        if (-not $stg) { Log "  警告: 阶段不存在 $sid"; continue }
        foreach ($f in $payload.stageFields.$sid.PSObject.Properties.Name) {
            $newVal = $payload.stageFields.$sid.$f
            if ($stg.$f -ne $newVal) {
                Log ("  阶段 {0}.{1}: {2} -> {3}" -f $sid, $f, $stg.$f, $newVal)
                $stg.$f = $newVal
                $statusChanged++
            }
        }
    }
}
if ($payload.PSObject.Properties['milestoneFields']) {
    foreach ($msName in $payload.milestoneFields.PSObject.Properties.Name) {
        $ms = $plan.milestones | Where-Object { $_.name -eq $msName }
        if (-not $ms) { Log "  警告: 里程碑不存在 $msName"; continue }
        foreach ($f in $payload.milestoneFields.$msName.PSObject.Properties.Name) {
            $newVal = $payload.milestoneFields.$msName.$f
            if ($ms.$f -ne $newVal) {
                Log ("  里程碑 {0}.{1}: {2} -> {3}" -f $msName, $f, $ms.$f, $newVal)
                $ms.$f = $newVal
                $statusChanged++
            }
        }
    }
}

# ---------- 5. 清空已归位的临时任务 ----------
$remove = @($payload.tempRemove)
$kept = @($plan.tempTasks | Where-Object { $remove -notcontains $_.id })
if ($kept.Count -ne @($plan.tempTasks).Count) {
    Log ("  临时任务池: {0} -> {1} 条" -f @($plan.tempTasks).Count, $kept.Count)
    $plan.tempTasks = $kept
}

# ---------- 6. 保存 ----------
$json = $plan | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($PlanPath, $json, [System.Text.UTF8Encoding]::new($false))
Log ("plan.json 已保存 | 新增日志 {0} 条（跳过重复 {1}）| 状态变更 {2} 处" -f $logAdded, $logSkipped, $statusChanged)
