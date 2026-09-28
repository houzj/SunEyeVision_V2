# ============================================================
# sync_catchup.ps1 - 计划同步「补齐」入口
#
# 解决：sync_plan.ps1 只处理 HEAD 单个提交，一旦漏跑（hook 被停用、
# 换 worktree、切分支、机器休眠、连续提交时上一个还没跑完）就永久漏掉。
#
# 做法：以 plans/.last_synced_commit 为水位线，把 marker..HEAD 的所有
# 非 merge 提交按时间顺序逐个交给 sync_plan.ps1 补齐（-NoBoard），
# 最后统一重建 weekly.json + 看板一次。
#
# 幂等：marker 已等于 HEAD 时直接返回；并发时靠运行锁跳过本次
#      （丢弃是安全的——水位线没有前进，下次提交会继续补齐）。
# ============================================================
param(
    [string]$PlanDir = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

if ($PlanDir -eq "") { $PlanDir = Split-Path -Parent $MyInvocation.MyCommand.Path }
$JsonPath    = Join-Path $PlanDir "plan.json"
$MarkerPath  = Join-Path $PlanDir ".last_synced_commit"
$SyncScript  = Join-Path $PlanDir "sync_plan.ps1"
$WeeklyGen   = Join-Path $PlanDir "generate_weekly.py"
$GenScript   = Join-Path $PlanDir "generate_plan.ps1"
$LockPath    = Join-Path $PlanDir ".sync.lock"

# ---------- 运行锁（并发保护，避免多个后台同步进程同时写 plan.json） ----------
if (Test-Path $LockPath) {
    $age = (Get-Date) - (Get-Item $LockPath).LastWriteTime
    if ($age.TotalMinutes -lt 15) {
        Write-Host "[catchup] 已有同步在运行（锁文件 $([int]$age.TotalMinutes) 分钟前创建），本次跳过；水位线未前进，下次提交会继续补齐" -ForegroundColor DarkGray
        exit 0
    }
    Write-Host "[catchup] 发现过期锁文件（$([int]$age.TotalMinutes) 分钟前），回收后继续" -ForegroundColor Yellow
    Remove-Item $LockPath -Force
}
Set-Content -Path $LockPath -Value $PID -Encoding ASCII

try {
    # ---------- 水位线 ----------
    $head = (git rev-parse HEAD 2>$null).Trim()
    if (-not $head) { Write-Host "[catchup] 无法获取 HEAD" -ForegroundColor Red; exit 1 }

    $marker = ""
    if (Test-Path $MarkerPath) { $marker = (Get-Content $MarkerPath -Raw -Encoding UTF8).Trim() }

    if ($marker -eq $head) {
        Write-Host "[catchup] 水位线已是最新（$($head.Substring(0,7))），无需补齐" -ForegroundColor DarkGray
        exit 0
    }

    # marker 无效（不存在且不是祖先）时，退化到只同步 HEAD，避免首次误扫全历史
    $range = ""
    if ($marker -ne "") {
        git merge-base --is-ancestor $marker HEAD 2>$null
        if ($LASTEXITCODE -eq 0) { $range = "$marker..HEAD" }
        else { Write-Host "[catchup] 水位线 $($marker.Substring(0,7)) 已不在当前历史（换分支/worktree），仅同步 HEAD" -ForegroundColor Yellow }
    }

    $commits = @()
    if ($range -ne "") {
        $commits = @(git rev-list --reverse --no-merges $range 2>$null | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
    if ($commits.Count -eq 0 -and $range -ne "") {
        Write-Host "[catchup] 区间内无非 merge 提交，仅刷新看板" -ForegroundColor DarkGray
    } elseif ($commits.Count -eq 0) {
        $commits = @($head)
    }

    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "[catchup] 待补齐提交: $($commits.Count) 条（水位线 $($marker.Substring(0,[Math]::Min(7,$marker.Length))) → HEAD $($head.Substring(0,7))）" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $ok = 0; $fail = 0
    foreach ($c in $commits) {
        try {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $SyncScript -PlanDir $PlanDir -CommitHash $c -Force -NoBoard
            if ($LASTEXITCODE -eq 0 -or $null -eq $LASTEXITCODE) { $ok++ } else { $fail++ }
        } catch {
            $fail++
            Write-Host "[catchup] 提交 $($c.Substring(0,7)) 同步失败: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    Write-Host "[catchup] 逐提交同步完成: 成功 $ok / 失败 $fail" -ForegroundColor Green

    # 水位线推进到 HEAD：区间内非 merge 提交已全部处理完。
    # 有失败时停在最后一条成功提交（sync_plan.ps1 已写），下次提交自动重试，不会漏。
    if ($fail -eq 0) {
        [System.IO.File]::WriteAllText($MarkerPath, $head, [System.Text.UTF8Encoding]::new($false))
        Write-Host "[catchup] 水位线推进到 HEAD $($head.Substring(0,7))" -ForegroundColor DarkGray
    } else {
        Write-Host "[catchup] 有 $fail 条失败，水位线停在最后成功提交，下次提交自动重试" -ForegroundColor Yellow
    }

    # ---------- 统一重建：周追溯 + 看板 ----------
    if (Test-Path $WeeklyGen) {
        $py = $null
        foreach ($cand in @("py", "python")) {
            if (Get-Command $cand -ErrorAction SilentlyContinue) { $py = $cand; break }
        }
        if ($py) {
            Write-Host "[catchup] 重建周追溯数据..." -ForegroundColor Cyan
            & $py $WeeklyGen 2>&1 | Out-Null
        }
    }
    if (Test-Path $GenScript) {
        Write-Host "[catchup] 重建看板..." -ForegroundColor Cyan
        & $GenScript -PlanDir $PlanDir 2>&1 | Out-Null
    }

    Write-Host "[catchup] 补齐完成 ✔" -ForegroundColor Green
}
finally {
    if (Test-Path $LockPath) { Remove-Item $LockPath -Force }
}
