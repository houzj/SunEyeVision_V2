# ============================================================
# sync_plan.ps1 - 提交驱动的计划自动同步脚本
#
# 由 git post-commit hook 触发。每次 git commit 后：
#   1. 读取最近一次提交的 message + 改动文件清单
#   2. 关键词/文件路径 → 匹配计划中的模块/工具
#   3. 状态推断：完成→done / 开始→in_progress / 修复→仅记日志
#   4. 匹配到 → 更新子任务状态 + 追加开发日志
#      未匹配 → 插入「临时任务池」（计划外工作不丢失）
#   5. 同步前自动备份 plan.json → plans/backup/
#   6. 保存 plan.json → 重新生成 HTML 看板
#
# 安全设计：只更新「状态/日志/临时任务池」，不删除、不重排已有内容。
# 幂等设计：记录已处理提交 hash，重复触发不重复处理。
# ============================================================
param(
    [string]$PlanDir = "",
    [string]$CommitHash = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# ---------- 编码修复（关键） ----------
# 控制台默认 gb2312 时，git 输出的 UTF-8 中文会乱码（钟舵€佹爮...）
# 统一按 UTF-8 解读外部命令输出
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$OutputEncoding = [System.Text.Encoding]::UTF8

# ---------- 定位目录 ----------
if ($PlanDir -eq "") {
    # 从脚本位置向上找解决方案根
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $PlanDir = $ScriptDir
}
$JsonPath = Join-Path $PlanDir "plan.json"
$HtmlPath = Join-Path $PlanDir "plan.html"
$BackupDir = Join-Path $PlanDir "backup"
$MarkerPath = Join-Path $PlanDir ".last_synced_commit"
$GenScript = Join-Path $PlanDir "generate_plan.ps1"

if (-not (Test-Path $JsonPath)) {
    Write-Host "[sync] 错误: 未找到 plan.json ($JsonPath)" -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SunEyeVision 提交驱动计划同步 v1.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ---------- 获取提交信息 ----------
$RepoRoot = git rev-parse --show-toplevel 2>$null
if (-not $RepoRoot) {
    Write-Host "[sync] 警告: 不在 git 仓库中，尝试使用最近提交文件" -ForegroundColor Yellow
}

if ($CommitHash -eq "") {
    $CommitHash = (git rev-parse HEAD 2>$null).Trim()
}
if (-not $CommitHash) {
    Write-Host "[sync] 错误: 无法获取提交 hash" -ForegroundColor Red
    exit 1
}

# 幂等：已处理过的提交跳过
if (-not $Force -and (Test-Path $MarkerPath)) {
    $last = (Get-Content $MarkerPath -Raw -Encoding UTF8).Trim()
    if ($last -eq $CommitHash) {
        Write-Host "[sync] 提交 $CommitHash 已同步过，跳过" -ForegroundColor DarkGray
        exit 0
    }
}

# 提交信息 + 文件清单
$CommitMsg = (git log -1 --format=%s $CommitHash 2>$null).Trim()
$FilesRaw = git diff-tree --no-commit-id --name-only -r $CommitHash 2>$null
$Files = @($FilesRaw | Where-Object { $_ -and $_.Trim() -ne "" } | ForEach-Object { $_.Trim() })
$CommitTime = (git log -1 --format=%ci $CommitHash 2>$null).Trim().Substring(0,10)

Write-Host "提交: $CommitHash" -ForegroundColor Green
Write-Host "信息: $CommitMsg" -ForegroundColor Green
Write-Host "时间: $CommitTime, 改动文件: $($Files.Count)" -ForegroundColor Green
if ($Files.Count -gt 0) {
    Write-Host "文件(前5):" -ForegroundColor DarkGray
    $Files | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
}

# ---------- 读取 plan.json ----------
$plan = Get-Content -Path $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

# ---------- 匹配规则表 ----------
# 关键词 → 模块 ID（顺序优先，先匹配先命中）
$keywordMap = @(
    @{ kw = @("圆查找", "CircleFinder", "找圆");            id = "T-CIRCLEFINDER" },
    @{ kw = @("直线查找", "LineFinder", "找线");            id = "T-LINEFINDER" },
    @{ kw = @("直线卡尺", "LineCaliper");                   id = "T-LINECALIPER" },
    @{ kw = @("圆卡尺", "CircleCaliper", "圆环卡尺");       id = "T-CIRCLECALIPER" },
    @{ kw = @("弧形卡尺", "ArcCaliper");                    id = "T-ARCCALIPER" },
    @{ kw = @("轮廓匹配", "ShapeMatch", "形状匹配", "亚像素"); id = "T-SHAPEMATCH" },
    @{ kw = @("Blob", "质心", "连通域");                    id = "T-BLOB" },
    @{ kw = @("点点", "PointToPoint", "P2P");               id = "T-P2P" },
    @{ kw = @("点线", "PointToLine", "P2L");                id = "T-P2L" },
    @{ kw = @("点圆", "PointToCircle", "P2C");              id = "T-P2C" },
    @{ kw = @("线线", "LineToLine", "L2L");                 id = "T-L2L" },
    @{ kw = @("线圆", "LineToCircle", "L2C");               id = "T-L2C" },
    @{ kw = @("圆圆", "CircleToCircle", "C2C", "中心距");   id = "T-C2C" },
    @{ kw = @("标定板", "CalibPlate");                      id = "T-CALIBPLATE" },
    @{ kw = @("畸变", "Undistort", "校正");                 id = "T-UNDIST" },
    @{ kw = @("手眼标定采集", "HECap");                     id = "T-HECAP" },
    @{ kw = @("坐标转换", "坐标变换", "Convert", "Transform"); id = "T-CONVERT" },
    @{ kw = @("机器人通信", "RobotComm", "机器人", "位姿下发"); id = "T-ROBOTCOMM" },
    @{ kw = @("图像采集工具", "采集工具", "采图工具", "相机采集", "ImageCapture"); id = "T-IMGCAPTURE" },
    @{ kw = @("通信工具", "通讯工具", "TCP收发", "Modbus读写", "通信节点"); id = "T-COMMTOOL" },
    @{ kw = @("相机管理", "相机", "Camera", "MvCamera", "Hikvision"); id = "M-SRC-CAM" },
    @{ kw = @("通讯", "通信", "Communication", "TCP", "Modbus", "串口", "PLC"); id = "M-SRC-COMM" },
    @{ kw = @("相机标定", "内参标定", "Calibration");       id = "M-SRC-CAL" },
    @{ kw = @("手眼标定", "HandEye");                       id = "M-SRC-HECAL" },
    @{ kw = @("位姿", "Pose", "OutputVariable", "输出契约"); id = "M-SRC-POSE" },
    @{ kw = @("运行UI", "运行界面", "RunUI", "操作员界面", "运行器"); id = "M-SRC-RUNUI" },
    @{ kw = @("联调", "集成", "Integrate", "端到端");       id = "M-SRC-INTEG" },
    @{ kw = @("交付", "上线", "手册", "Delivery");          id = "M-SRC-DELIVERY" },
    @{ kw = @("子程序", "Subroutine");                      id = "T-SUBROUTINE" },
    @{ kw = @("条件分支", "条件", "Condition", "分支");     id = "T-CONDITION" },
    @{ kw = @("脚本", "Script", "Roslyn", "语法高亮", "监视"); id = "T-SCRIPT" },
    @{ kw = @("测试", "Test", "TestCase", "断言", "xUnit", "假测试", "回归测试"); id = "M-SRC-TEST" },
    @{ kw = @("全局变量", "GlobalVariable", "消费者绑定");  id = "M-HIST-GLOBALVAR" },
    @{ kw = @("画布", "节点", "连线", "小地图", "缩放", "撤销", "多选", "工作流编辑器"); id = "M-HIST-CANVAS" },
    @{ kw = @("解决方案", "方案管理", "序列化", "持久化");  id = "M-HIST-SOLUTION" },
    @{ kw = @("CvEngine", "CvOp", "算子", "Halcon对标");    id = "M-HIST-CVENGINE" },
    @{ kw = @("ParamBinding", "参数绑定", "绑定");          id = "M-HIST-PARAM" },
    @{ kw = @("区域", "RoI", "Roi", "Region");              id = "M-HIST-REGION" },
    @{ kw = @("图像预览", "预览", "缩略图", "GPU");         id = "M-HIST-PREVIEW" },
    @{ kw = @("日志", "Logger", "日志系统");                id = "M-HIST-CORE" },
    @{ kw = @("插件", "Plugin", "工具箱", "框架");          id = "M-HIST-CORE" },
    @{ kw = @("SdkTypeVocabulary", "类型系统", "几何类型", "RegionConverter", "输出变量契约"); id = "M-HIST-CONTRACT" },
    @{ kw = @("主题", "Theme", "UI优化", "样式", "双主题"); id = "M-SRC-UI" }
)

# 文件路径片段 → 模块 ID
$pathMap = @(
    @{ path = @("tools/SunEyeVision.Tool.CircleFinder"); id = "T-CIRCLEFINDER" },
    @{ path = @("tools/SunEyeVision.Tool.LineFinder");   id = "T-LINEFINDER" },
    @{ path = @("tools/SunEyeVision.Tool.LineCaliper");  id = "T-LINECALIPER" },
    @{ path = @("tools/SunEyeVision.Tool.CircleCaliper"); id = "T-CIRCLECALIPER" },
    @{ path = @("tools/SunEyeVision.Tool.ArcCaliper");   id = "T-ARCCALIPER" },
    @{ path = @("tools/SunEyeVision.Tool.ShapeMatch");   id = "T-SHAPEMATCH" },
    @{ path = @("tools/SunEyeVision.BlobAnalysis");      id = "T-BLOB" },
    @{ path = @("tools/SunEyeVision.Tool.PointToPoint"); id = "T-P2P" },
    @{ path = @("tools/SunEyeVision.Tool.PointToLine");  id = "T-P2L" },
    @{ path = @("tools/SunEyeVision.Tool.PointToCircle"); id = "T-P2C" },
    @{ path = @("tools/SunEyeVision.Tool.LineToLine");   id = "T-L2L" },
    @{ path = @("tools/SunEyeVision.Tool.LineToCircle"); id = "T-L2C" },
    @{ path = @("tools/SunEyeVision.Tool.CircleToCircle"); id = "T-C2C" },
    @{ path = @("tools/SunEyeVision.Tool.CalibPlate");   id = "T-CALIBPLATE" },
    @{ path = @("tools/SunEyeVision.Tool.Undistort");    id = "T-UNDIST" },
    @{ path = @("tools/SunEyeVision.Tool.HECap");        id = "T-HECAP" },
    @{ path = @("tools/SunEyeVision.Tool.CoordinateConvert"); id = "T-CONVERT" },
    @{ path = @("tools/SunEyeVision.Tool.RobotComm");    id = "T-ROBOTCOMM" },
    @{ path = @("tools/SunEyeVision.Tool.ImageCapture", "tools/SunEyeVision.Tool.CameraCapture"); id = "T-IMGCAPTURE" },
    @{ path = @("tools/SunEyeVision.Tool.Communication", "tools/SunEyeVision.Tool.CommTool"); id = "T-COMMTOOL" },
    @{ path = @("src/DeviceDriver/Cameras", "src/UI/Features/Camera"); id = "M-SRC-CAM" },
    @{ path = @("src/UI/Features/CommunicationManager", "src/Workflow/Communication"); id = "M-SRC-COMM" },
    @{ path = @("src/UI/Features/GlobalVariableManager");  id = "M-HIST-GLOBALVAR" },
    @{ path = @("src/Workflow", "src/UI/Features/WorkflowUI"); id = "M-HIST-CANVAS" },
    @{ path = @("src/SunEyeVision.CvEngine", "src/CvEngine"); id = "M-HIST-CVENGINE" },
    @{ path = @("src/Plugin.SDK/UI/Controls/Roi");         id = "M-HIST-REGION" },
    @{ path = @("src/Plugin.SDK/Logging", "src/Core/Services/Logging"); id = "M-HIST-CORE" },
    @{ path = @("tests/");                                 id = "M-SRC-TEST" },
    @{ path = @("src/UI/Themes", "src/UI/Shell");          id = "M-SRC-UI" },
    @{ path = @("tools/SunEyeVision.Tool.Subroutine");     id = "T-SUBROUTINE" },
    @{ path = @("tools/SunEyeVision.Tool.Condition");      id = "T-CONDITION" },
    @{ path = @("tools/SunEyeVision.Tool.Script");         id = "T-SCRIPT" }
)

# 状态推断：完成词 / 开始词
$doneWords = @("完成", "实现", "完毕", "done", "finish", "complete", "closed", "close", "落地", "交付")
$startWords = @("开始", "着手", "开发中", "进行中", "start", "wip", "init", "新增", "添加")
$logOnlyWords = @("修复", "优化", "调整", "重构", "改进", "fix", "refactor", "update", "改善", "适配")

function Match-ModuleId([string]$msg, [string[]]$files) {
    # 1. 关键词匹配（提交信息）
    foreach ($rule in $keywordMap) {
        foreach ($k in $rule.kw) {
            if ($msg -match [regex]::Escape($k)) { return $rule.id }
        }
    }
    # 2. 文件路径匹配
    foreach ($rule in $pathMap) {
        foreach ($p in $rule.path) {
            foreach ($f in $files) {
                if ($f -like "*$p*") { return $rule.id }
            }
        }
    }
    return ""
}

function Get-IntendedStatus([string]$msg) {
    foreach ($w in $doneWords)   { if ($msg -match $w) { return "done" } }
    foreach ($w in $startWords)  { if ($msg -match $w) { return "in_progress" } }
    foreach ($w in $logOnlyWords){ if ($msg -match $w) { return "log" } }
    return "in_progress"
}

# ---------- 工具函数：确保对象属性存在（PowerShell 5.1 坑） ----------
# ConvertFrom-Json 返回的 PSCustomObject 不能直接赋值新属性（静默失败），
# 必须先 Add-Member 添加属性再赋值，否则 ConvertTo-Json 输出会丢失该字段。
function Ensure-Property($obj, [string]$name, $defaultValue) {
    if ($null -eq $obj.PSObject.Properties[$name]) {
        $obj | Add-Member -NotePropertyName $name -NotePropertyValue $defaultValue -Force
    }
}

# ---------- 备份 ----------
if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir | Out-Null }
$backupName = "plan-$($CommitTime)-$($CommitHash.Substring(0,7)).json"
Copy-Item $JsonPath (Join-Path $BackupDir $backupName) -Force
Write-Host "已备份: $backupName" -ForegroundColor DarkGray

# ---------- 匹配与更新 ----------
$matchedId = Match-ModuleId $CommitMsg $Files
$logNote = "[$CommitHash] $CommitMsg"

if ($matchedId) {
    Write-Host "匹配模块: $matchedId" -ForegroundColor Green
    $status = Get-IntendedStatus $CommitMsg
    Write-Host "推断状态: $status" -ForegroundColor Green

    $mod = $null
    foreach ($m in @($plan.modules)) { if ($m.id -eq $matchedId) { $mod = $m; break } }

    if ($mod) {
        # 更新子任务状态
        if ($status -eq "done") {
            # 把第一个未完成子任务标记为 done
            foreach ($t in @($mod.tasks)) {
                if ($t.status -ne "done") { $t.status = "done"; break }
            }
        } elseif ($status -eq "in_progress") {
            # 把第一个 todo 子任务标记为 in_progress
            foreach ($t in @($mod.tasks)) {
                if ($t.status -eq "todo") { $t.status = "in_progress"; break }
            }
        }
        # 更新模块状态（有 done 就 done，有 in_progress 就 in_progress，否则 todo）
        $allDone = @($mod.tasks | Where-Object { $_.status -ne "done" }).Count -eq 0
        $anyProg = @($mod.tasks | Where-Object { $_.status -eq "in_progress" }).Count -gt 0
        if ($allDone) { $mod.status = "done" }
        elseif ($anyProg) { $mod.status = "in_progress" }

        # 追加开发日志（log 属性不存在时需先 Add-Member）
        $log = [pscustomobject]@{ date = $CommitTime; note = $logNote }
        Ensure-Property $mod "log" @()
        $mod.log = @($mod.log) + $log

        Write-Host "已更新模块 $($mod.name): 状态=$($mod.status), 日志+1" -ForegroundColor Green
    } else {
        Write-Host "匹配到 $matchedId 但未在 plan.json 中找到模块，按临时任务处理" -ForegroundColor Yellow
        $matchedId = ""
    }
}

# ---------- 未匹配 → 临时任务池 ----------
if (-not $matchedId) {
    # 治本：判断是否"纯计划系统自我更新"（无业务代码改动，如提交只是 plan.json 入库）
    # 这类提交会触发 hook → hook 又记临时任务 → plan.json 变脏 → 再提交 → 无限循环。
    # 仅当改动文件含业务路径（src/tools/tests/docs 等）才记临时任务，纯 plans/.codebuddy 更新跳过。
    $businessPrefixes = @("src/", "tools/", "tests/", "docs/", "libs/", "PluginDevKit/", "Help/")
    $hasBusinessChange = $false
    foreach ($f in $Files) {
        foreach ($bp in $businessPrefixes) {
            if ($f.StartsWith($bp)) { $hasBusinessChange = $true; break }
        }
        if ($hasBusinessChange) { break }
    }

    if (-not $hasBusinessChange) {
        Write-Host "纯计划系统自我更新（无业务代码改动），跳过临时任务池，避免自我更新污染" -ForegroundColor DarkGray
    } else {
        Write-Host "未匹配到计划内模块 → 记入临时任务池" -ForegroundColor Yellow
        Ensure-Property $plan "tempTasks" @()
        $tempTask = [pscustomobject]@{
            id       = "TMP-$($CommitHash.Substring(0,7))"
            message  = $CommitMsg
            date     = $CommitTime
            commit   = $CommitHash
            files    = $Files
            status   = "in_progress"
        }
        $plan.tempTasks = @($plan.tempTasks) + $tempTask
        Write-Host "临时任务已加入: $($tempTask.id) - $CommitMsg" -ForegroundColor Yellow
    }
}

# ---------- 保存 plan.json（保持格式） ----------
$json = $plan | ConvertTo-Json -Depth 20
# 转换后 null 数组清理（可选，保持兼容即可）
[System.IO.File]::WriteAllText($JsonPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "plan.json 已保存" -ForegroundColor Green

# 记录已同步提交（幂等标记）
[System.IO.File]::WriteAllText($MarkerPath, $CommitHash, [System.Text.UTF8Encoding]::new($false))

# ---------- 重新生成 HTML 看板 ----------
if (Test-Path $GenScript) {
    Write-Host "重新生成看板..." -ForegroundColor Cyan
    try {
        & $GenScript -PlanDir $PlanDir 2>&1 | Out-Null
        Write-Host "看板已更新" -ForegroundColor Green
    } catch {
        Write-Host "看板生成失败: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "警告: 未找到 generate_plan.ps1，跳过看板生成" -ForegroundColor Yellow
}

Write-Host "同步完成 ✔" -ForegroundColor Green
Write-Host ""
