# -*- coding: utf-8 -*-
"""
generate_weekly.py - 周维度开发用时追溯数据生成
扫描 git 全部提交 → 按 ISO 周聚合 → 用与 sync_plan.ps1 相同的匹配规则归到模块 → 生成 weekly.json

用法: python plans/generate_weekly.py
输出: plans/weekly.json
  {
    "generated": "2026-08-17",
    "totalCommits": 467,
    "weeks": [
      { "week": "2026-W04", "commits": 28, "modules": {"M-HIST-CORE": 10, ...} },
      ...
    ],
    "moduleWeeks": {
      "M-HIST-CORE": [ {"week":"2026-W04","commits":10}, ... ]
    }
  }
"""
import json
import io
import os
import re
import subprocess
import sys
import datetime

REPO = r"D:\MyWork\SunEyeVision_V2-script"
OUT = os.path.join(REPO, "plans", "weekly.json")

# ---------- 与 sync_plan.ps1 一致的匹配规则 ----------
keywordMap = [
    (["圆查找", "CircleFinder", "找圆"], "T-CIRCLEFINDER"),
    (["直线查找", "LineFinder", "找线"], "T-LINEFINDER"),
    (["直线卡尺", "LineCaliper"], "T-LINECALIPER"),
    (["圆卡尺", "CircleCaliper", "圆环卡尺"], "T-CIRCLECALIPER"),
    (["弧形卡尺", "ArcCaliper"], "T-ARCCALIPER"),
    (["轮廓匹配", "ShapeMatch", "形状匹配", "亚像素"], "T-SHAPEMATCH"),
    (["Blob", "质心", "连通域"], "T-BLOB"),
    (["点点", "PointToPoint", "P2P"], "T-P2P"),
    (["点线", "PointToLine", "P2L"], "T-P2L"),
    (["点圆", "PointToCircle", "P2C"], "T-P2C"),
    (["线线", "LineToLine", "L2L"], "T-L2L"),
    (["线圆", "LineToCircle", "L2C"], "T-L2C"),
    (["圆圆", "CircleToCircle", "C2C", "中心距"], "T-C2C"),
    (["标定板", "CalibPlate"], "T-CALIBPLATE"),
    (["畸变", "Undistort", "校正"], "T-UNDIST"),
    (["手眼标定采集", "HECap"], "T-HECAP"),
    (["坐标转换", "坐标变换", "Convert", "Transform"], "T-CONVERT"),
    (["机器人通信", "RobotComm", "机器人", "位姿下发"], "T-ROBOTCOMM"),
    (["相机管理", "相机", "Camera", "MvCamera", "Hikvision"], "M-SRC-CAM"),
    (["通讯", "通信", "Communication", "TCP", "Modbus", "串口", "PLC"], "M-SRC-COMM"),
    (["相机标定", "内参标定", "Calibration"], "M-SRC-CAL"),
    (["手眼标定", "HandEye"], "M-SRC-HECAL"),
    (["位姿", "Pose", "OutputVariable", "输出契约"], "M-SRC-POSE"),
    (["运行UI", "运行界面", "RunUI", "操作员界面", "运行器"], "M-SRC-RUNUI"),
    (["联调", "集成", "Integrate", "端到端"], "M-SRC-INTEG"),
    (["交付", "上线", "手册", "Delivery"], "M-SRC-DELIVERY"),
    (["全局变量", "GlobalVariable"], "M-HIST-GLOBALVAR"),
    (["小地图", "MiniMap", "工作区"], "M-HIST-CANVAS"),
    (["画布", "节点", "连线", "工作流编辑器", "缩放", "撤销", "多选", "命中", "虚拟化"], "M-HIST-CANVAS"),
    (["解决方案", "方案管理", "序列化", "持久化"], "M-HIST-SOLUTION"),
    (["CvEngine", "CvOp", "算子"], "M-HIST-CVENGINE"),
    (["ParamBinding", "参数绑定", "绑定"], "M-HIST-PARAM"),
    (["区域", "RoI", "Roi"], "M-HIST-REGION"),
    (["日志", "Logger", "Log", "日志系统"], "M-HIST-CORE"),
    (["文件访问", "FileAccess"], "M-HIST-CORE"),
    (["插件", "Plugin", "工具箱"], "M-HIST-CORE"),
    (["图像预览", "预览", "缩略图", "GPU", "ImageControl"], "M-HIST-PREVIEW"),
    (["脚本", "Script", "Roslyn", "语法高亮"], "M-HIST-SCRIPT"),
    (["SdkTypeVocabulary", "类型系统", "几何类型", "RegionConverter"], "M-HIST-CONTRACT"),
    (["主题", "Theme", "样式", "UI优化", "重构(UI)"], "M-SRC-UI"),
    (["子程序", "Subroutine"], "T-SUBROUTINE"),
    (["条件", "Condition", "分支"], "T-CONDITION"),
    (["框架", "架构", "主界面", "重构"], "M-HIST-CORE"),
    (["工作流", "Workflow", "引擎", "执行"], "M-HIST-CANVAS"),
    (["显示", "渲染", "Display", "Render", "命中"], "M-HIST-CANVAS"),
    (["性能", "优化", "Performance"], "M-HIST-CANVAS"),
    (["工具", "Tool"], "M-HIST-CORE"),
    (["结果", "输出", "Result", "Output"], "M-HIST-CONTRACT"),
    (["适配", "Adapter"], "M-HIST-CANVAS"),
    (["数据", "Data"], "M-HIST-PARAM"),
    # 补充：SDK 控件 / 数据结构 / 结果树 / 路径算法（v5.11+）
    (["Roi 编辑器", "RoiEditor", "SDK 控件", "ParamComboBox", "ParamTextBox", "ToggleSwitch", "ColorPicker", "NumericUpDown", "控件库"], "M-HIST-SDKCTRL"),
    (["数据结构", "数据模型", "元数据", "绑定模型", "ResultManager", "结果管理器"], "M-HIST-DATAMODEL"),
    (["结果树", "绑定树", "模块结果", "NodeResultManager", "ArrayTree", "结果分组"], "M-HIST-RESULTTREE"),
    (["路径", "Path", "连线", "贝塞尔", "ConnectionPath", "路径计算"], "M-HIST-PATH"),
]

pathMap = [
    (["tests/"], "M-SRC-TEST"),
    (["tools/SunEyeVision.Tool.CircleFinder"], "T-CIRCLEFINDER"),
    (["tools/SunEyeVision.Tool.LineFinder"], "T-LINEFINDER"),
    (["tools/SunEyeVision.Tool.LineCaliper"], "T-LINECALIPER"),
    (["tools/SunEyeVision.Tool.CircleCaliper"], "T-CIRCLECALIPER"),
    (["tools/SunEyeVision.Tool.ArcCaliper"], "T-ARCCALIPER"),
    (["tools/SunEyeVision.Tool.ShapeMatch"], "T-SHAPEMATCH"),
    (["tools/SunEyeVision.BlobAnalysis"], "T-BLOB"),
    (["tools/SunEyeVision.Tool.PointToPoint"], "T-P2P"),
    (["tools/SunEyeVision.Tool.PointToLine"], "T-P2L"),
    (["tools/SunEyeVision.Tool.PointToCircle"], "T-P2C"),
    (["tools/SunEyeVision.Tool.LineToLine"], "T-L2L"),
    (["tools/SunEyeVision.Tool.LineToCircle"], "T-L2C"),
    (["tools/SunEyeVision.Tool.CircleToCircle"], "T-C2C"),
    (["tools/SunEyeVision.Tool.CalibPlate"], "T-CALIBPLATE"),
    (["tools/SunEyeVision.Tool.Undistort"], "T-UNDIST"),
    (["tools/SunEyeVision.Tool.HECap"], "T-HECAP"),
    (["tools/SunEyeVision.Tool.CoordinateConvert"], "T-CONVERT"),
    (["tools/SunEyeVision.Tool.RobotComm"], "T-ROBOTCOMM"),
    # ★ 补充：脚本 / 子程序 / 条件 工具路径（此前缺失，导致这些提交被按文件数误归到 M-SRC-UI/M-HIST-CANVAS）
    (["tools/SunEyeVision.Tool.Script"], "T-SCRIPT"),
    (["tools/SunEyeVision.Tool.Subroutine"], "T-SUBROUTINE"),
    (["tools/SunEyeVision.Tool.Condition"], "T-CONDITION"),
    (["src/DeviceDriver/Cameras", "src/UI/Features/Camera"], "M-SRC-CAM"),
    (["src/UI/Features/CommunicationManager", "src/Workflow/Communication"], "M-SRC-COMM"),
    (["src/UI/Features/GlobalVariableManager"], "M-HIST-GLOBALVAR"),
    (["src/UI/Shell/Controls/Shell/ShellWorkspaceControl"], "M-HIST-CANVAS"),
    (["src/SunEyeVision.CvEngine", "src/CvEngine"], "M-HIST-CVENGINE"),
    (["src/UI/Features/WorkflowUI"], "M-HIST-CANVAS"),
    (["src/Workflow"], "M-HIST-CANVAS"),
    (["src/Plugin.SDK/Logging", "src/Core/Services/Logging"], "M-HIST-CORE"),
    (["src/Plugin.SDK/UI/Controls/Roi"], "M-HIST-REGION"),
    (["src/UI/Themes", "src/UI/Shell", "src/Plugin.SDK/UI"], "M-SRC-UI"),
    (["src/UI/Features/Dialogs", "src/UI/Features/ParameterSetting"], "M-HIST-PARAM"),
    (["src/UI/Features/WorkflowUI/Canvas"], "M-HIST-CANVAS"),
    # 补充：SDK 控件 / 数据结构 / 结果树 / 路径算法（v5.11+）
    (["src/Plugin.SDK/UI/Controls/Roi", "src/Plugin.SDK/UI/Controls/ParamBinding", "src/Plugin.SDK/UI/Controls/ImageControl", "src/Plugin.SDK/UI/Controls/ColorPicker", "src/Plugin.SDK/UI/Controls/NumericUpDown", "src/Plugin.SDK/UI/Controls/ToggleSwitch", "src/Plugin.SDK/UI/Controls/RangeSlider"], "M-HIST-SDKCTRL"),
    (["src/UI/Services/Connection/ConnectionPathService", "src/UI/Services/Connection/ConnectionPathCache", "src/UI/Converters/Path"], "M-HIST-PATH"),
    (["src/UI/Services/Workflow/NodeResultManager", "src/UI/Services/Workflow/INodeResultSink", "src/Plugin.SDK/Execution/Parameters/ArrayTreeFormatter"], "M-HIST-RESULTTREE"),
    (["src/Plugin.SDK/Execution/Parameters", "src/Plugin.SDK/Models/SubroutineOutputConfig"], "M-HIST-DATAMODEL"),
]

def match_module(msg, files):
    # ★ 优先：工具专属路径精确匹配（tools/SunEyeVision.Tool.X → T-X）。
    #   工具提交核心改动通常在 tools/ 下，即使顺带改若干 src/UI 附带文件，
    #   也应归到对应工具模块，而非按「文件数统计」误归到宽泛的 M-SRC-UI。
    #   例：e8ab1af 改 ScriptVariableTab(tools/Script)+DarkTheme(src/UI/Themes)，
    #       原逻辑按文件数归到 M-SRC-UI；修正后 tools/Script 优先归 T-SCRIPT。
    for paths, mid in pathMap:
        if not mid.startswith("T-"):
            continue
        for p in paths:
            for f in files:
                if p.lower() in f.lower():
                    return mid
    # 路径匹配：按「命中文件数」选归属，而非「先到先得」。
    # 修复 bug：7605075 主要改 UI（src/ 32 + tools/ 13）但顺带改 tests/(3)，
    # 原逻辑 tests/ 规则排在前面，抢先归到 M-SRC-TEST，导致 UI 提交被错记。
    # 现改为统计每个模块命中多少个文件，选命中最多者；平局取 pathMap 靠前者。
    best_mid = None
    best_count = 0
    for paths, mid in pathMap:
        cnt = 0
        for p in paths:
            for f in files:
                if p.lower() in f.lower():
                    cnt += 1
        if cnt > best_count:
            best_count = cnt
            best_mid = mid
    if best_mid and best_count > 0:
        return best_mid
    # 关键词匹配（仅当无路径命中时兜底）
    for kws, mid in keywordMap:
        for k in kws:
            if k.lower() in msg.lower():
                return mid
    return "UNKNOWN"

def _finalize_commit(cur, weeks, module_weeks, module_commits):
    """处理一个完整提交：按周聚合 + 模块匹配 + 记录提交日志"""
    hash_, ci, subject = cur["hash"], cur["ci"], cur["subject"]
    files = cur["files"]
    try:
        d = datetime.date.fromisoformat(ci[:10])
    except ValueError:
        return
    iso = d.isocalendar()
    week = f"{iso[0]}-W{iso[1]:02d}"

    # 纯计划系统自我更新提交（无业务代码改动）跳过，与 sync_plan.ps1 的跳过逻辑一致。
    # 避免这类提交进入 moduleCommits/UNKNOWN，污染「最近提交追溯」。
    _business_prefixes = ("src/", "tools/", "tests/", "docs/", "libs/", "PluginDevKit/", "Help/")
    _has_biz = any(f.lower().startswith(_business_prefixes) for f in files)
    if not _has_biz:
        return

    mid = match_module(subject, files)

    w = weeks.setdefault(week, {"commits": 0, "modules": {}})
    w["commits"] += 1
    w["modules"][mid] = w["modules"].get(mid, 0) + 1

    mw = module_weeks.setdefault(mid, [])
    found = False
    for item in mw:
        if item["week"] == week:
            item["commits"] += 1
            found = True
            break
    if not found:
        mw.append({"week": week, "commits": 1})

    # 记录真实提交日志（按模块）
    mc = module_commits.setdefault(mid, [])
    mc.append({
        "hash": hash_[:7],
        "date": ci[:10],
        "message": subject.strip()
    })

def main():
    os.chdir(REPO)
    # ========== 一次性拉取全部提交日志（含文件列表） ==========
    # 格式: meta 行 = "40位hex \x1f ci \x1f subject"，后跟该提交的文件列表
    # （git --name-only 输出：meta 行后直接是文件行，无空行分隔，故用 meta 特征识别）
    out = subprocess.run(
        ["git", "log", "--no-merges", "--name-only",
         "--format=%H%x1f%ci%x1f%s"],
        capture_output=True, text=True, encoding="utf-8", errors="replace"
    ).stdout

    import re
    meta_re = re.compile(r"^[0-9a-f]{40}\x1f")

    weeks = {}          # week -> {commits: n, modules: {mid: n}}
    module_weeks = {}   # mid -> [ {week, commits} ]
    module_commits = {} # mid -> [ {hash, date, message} ]  真实提交日志

    cur = None
    for line in out.splitlines():
        line = line.rstrip()
        if not line.strip():
            continue
        if meta_re.match(line):
            if cur:
                _finalize_commit(cur, weeks, module_weeks, module_commits)
            parts = line.split("\x1f")
            cur = {"hash": parts[0], "ci": parts[1], "subject": parts[2], "files": []}
        elif cur:
            # 去掉 git 对含特殊字符路径的引号包裹
            f = line.strip().strip('"')
            if f:
                cur["files"].append(f)
    if cur:
        _finalize_commit(cur, weeks, module_weeks, module_commits)

    # 排序
    weeks_sorted = []
    for wk in sorted(weeks.keys()):
        entry = weeks[wk]
        entry["modules"] = dict(sorted(entry["modules"].items(), key=lambda x: -x[1]))
        weeks_sorted.append({"week": wk, "commits": entry["commits"], "modules": entry["modules"]})

    module_weeks_sorted = {}
    for mid, lst in module_weeks.items():
        module_weeks_sorted[mid] = sorted(lst, key=lambda x: x["week"])

    # 提交日志按日期排序（旧→新），每模块最多保留 100 条
    module_commits_sorted = {}
    for mid, lst in module_commits.items():
        lst_sorted = sorted(lst, key=lambda x: x["date"])
        module_commits_sorted[mid] = lst_sorted[-100:]

    result = {
        "generated": datetime.date.today().isoformat(),
        "totalCommits": sum(w["commits"] for w in weeks_sorted),
        "weeks": weeks_sorted,
        "moduleWeeks": module_weeks_sorted,
        "moduleCommits": module_commits_sorted
    }

    with io.open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write("\n")

    # 拆分模块提交日志到独立文件（供 PowerShell 简单读取，避免深层 JSON 问题）
    gitlog_dir = os.path.join(REPO, "plans", "gitlogs")
    os.makedirs(gitlog_dir, exist_ok=True)
    # 先清空旧的 gitlogs 文件（.html/.json），避免模块归属调整后残留过时记录
    for stale in os.listdir(gitlog_dir):
        if stale.endswith(".html") or stale.endswith(".json"):
            os.remove(os.path.join(gitlog_dir, stale))
    for mid, commits in module_commits_sorted.items():
        with io.open(os.path.join(gitlog_dir, f"{mid}.json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(commits, f, ensure_ascii=False, indent=2)
            f.write("\n")

    # 生成每个模块的 git 日志 HTML 片段（供子页面直接嵌入，绕开 PowerShell JSON 数组问题）
    # 倒序（最新在前），最多 50 条
    for mid, commits in module_commits_sorted.items():
        rev = sorted(commits, key=lambda x: x["date"], reverse=True)[:50]
        parts = []
        for c in rev:
            msg = c["message"].replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
            parts.append(
                "              <div class='git-log-item'>\n"
                f"                <span class='git-hash'>{c['hash']}</span>\n"
                f"                <span class='git-date'>{c['date']}</span>\n"
                f"                <span class='git-msg'>{msg}</span>\n"
                "              </div>"
            )
        total = len(commits)
        more = f"<p class='muted'>仅显示最近 {min(total,50)} 条 / 共 {total} 条</p>" if total > 50 else ""
        html = (
            "            <div class='git-log'>\n"
            + "\n".join(parts)
            + f"\n            </div>\n            {more}"
        )
        with io.open(os.path.join(gitlog_dir, f"{mid}.html"), "w", encoding="utf-8", newline="\n") as f:
            f.write(html)
            f.write("\n")

    print(f"周追溯数据已生成: {OUT}")
    print(f"总提交: {result['totalCommits']}, 周数: {len(weeks_sorted)}")
    print(f"覆盖模块数: {len(module_weeks_sorted)}, 日志文件: {len(module_commits_sorted)}")

if __name__ == "__main__":
    main()
