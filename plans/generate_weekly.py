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

REPO = r"D:\MyWork\SunEyeVision_V2"
OUT = os.path.join(REPO, "plans", "weekly.json")

# ---------- 与 sync_plan.ps1 一致的匹配规则 ----------
keywordMap = [
    (["圆查找", "CircleFinder", "CircleDetect", "找圆"], "T-CIRCLEFINDER"),
    (["直线查找", "LineFinder", "LineDetect", "找线"], "T-LINEFINDER"),
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
    (["图像源", "ImageSource", "图像采集工具", "采集工具", "采图工具", "相机采集", "ImageCapture"], "T-IMGCAPTURE"),
    (["交付", "上线", "手册", "Delivery", "安装包", "内测版", "打包", "Release"], "M-SRC-DELIVERY"),
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
    (["tools/SunEyeVision.Tool.CircleDetect", "tools/SunEyeVision.Tool.CircleFinder"], "T-CIRCLEFINDER"),
    (["tools/SunEyeVision.Tool.LineDetect", "tools/SunEyeVision.Tool.LineFinder"], "T-LINEFINDER"),
    (["tools/SunEyeVision.Tool.LineCaliper"], "T-LINECALIPER"),
    (["tools/SunEyeVision.Tool.CircleCaliper"], "T-CIRCLECALIPER"),
    (["tools/SunEyeVision.Tool.ArcCaliper"], "T-ARCCALIPER"),
    (["tools/SunEyeVision.Tool.ShapeMatch"], "T-SHAPEMATCH"),
    (["tools/SunEyeVision.BlobAnalysis"], "T-BLOB"),
    (["tools/SunEyeVision.Tool.PointPointMeasure", "tools/SunEyeVision.Tool.PointToPoint"], "T-P2P"),
    (["tools/SunEyeVision.Tool.PointLineMeasure", "tools/SunEyeVision.Tool.PointToLine"], "T-P2L"),
    (["tools/SunEyeVision.Tool.PointCircleMeasure", "tools/SunEyeVision.Tool.PointToCircle"], "T-P2C"),
    (["tools/SunEyeVision.Tool.LineLineMeasure", "tools/SunEyeVision.Tool.LineToLine"], "T-L2L"),
    (["tools/SunEyeVision.Tool.LineCircleMeasure", "tools/SunEyeVision.Tool.LineToCircle"], "T-L2C"),
    (["tools/SunEyeVision.Tool.CircleCircleMeasure", "tools/SunEyeVision.Tool.CircleToCircle"], "T-C2C"),
    (["src/Workflow/ImageSourceConfig", "src/Plugin.SDK/Vision/Contracts/ImageSourceKind", "src/UI/Features/WorkflowUI/ImageSourceSettingsControl"], "T-IMGCAPTURE"),
    (["tools/SunEyeVision.Tool.ImageLoad"], "T-IMAGELOAD"),
    (["tools/SunEyeVision.Tool.ImageThreshold"], "T-IMAGETHRESHOLD"),
    (["tools/SunEyeVision.Tool.ImageFilter"], "T-IMAGEFILTER"),
    (["tools/SunEyeVision.Tool.Morphology"], "T-MORPHOLOGY"),
    (["tools/SunEyeVision.Tool.Threshold"], "T-THRESHOLD"),
    (["installer/", "version.json", "Help/"], "M-SRC-DELIVERY"),
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

def _tool_hits(files):
    """返回该提交命中的工具模块列表（按 pathMap 顺序去重）。"""
    hits = []
    for paths, mid in pathMap:
        if not mid.startswith("T-"):
            continue
        for p in paths:
            if any(p.lower() in f.lower() for f in files):
                hits.append(mid)
                break
    seen, uniq = set(), []
    for m in hits:
        if m not in seen:
            seen.add(m)
            uniq.append(m)
    return uniq

# ---------- 历史提交语义覆盖表（人工判定，优先于路径/关键词匹配） ----------
# 交叉性「工具基座 / 契约 / 结果呈现」提交会同时改十几个工具目录，
# 单靠路径匹配会误认领给第一个命中者（如 T-CIRCLEFINDER），此处显式钉死真实归属；
# 命中的工具目录仍作为「关联模块」计入各自周统计与日志。
manualOverrides = {
    # —— 参数契约 / 绑定 / 运行快照 → M-HIST-PARAM
    "d975ece8": "M-HIST-PARAM", "0f1823f0": "M-HIST-PARAM", "da3944aa": "M-HIST-PARAM",
    "7ab9013d": "M-HIST-PARAM", "e90f5c2b": "M-HIST-PARAM", "d8163274": "M-HIST-PARAM",
    "038dda82": "M-HIST-PARAM", "abe0ce2f": "M-HIST-PARAM", "2ebb4ce0": "M-HIST-PARAM",
    # —— 工具契约 / 能力声明 / 结果结构与值分离 → M-HIST-CONTRACT
    "bc1ec5ae": "M-HIST-CONTRACT", "74f913ad": "M-HIST-CONTRACT", "f24fa8b7": "M-HIST-CONTRACT",
    # —— 结果树 / 结果命名 / 判定显示 → M-HIST-RESULTTREE
    "86673059": "M-HIST-RESULTTREE", "cea13947": "M-HIST-RESULTTREE",
    "e6f62dfa": "M-HIST-RESULTTREE", "e6b558e0": "M-HIST-RESULTTREE",
    "ae3b330b": "M-HIST-RESULTTREE",
    # —— SDK 控件（结果页数据驱动 / 双卡尺编辑器） → M-HIST-SDKCTRL
    "35b78e87": "M-HIST-SDKCTRL", "151a2121": "M-HIST-SDKCTRL", "c11ed704": "M-HIST-SDKCTRL",
    "eb09aa95": "M-HIST-SDKCTRL",
    # —— 渲染 / 图像预览 → M-HIST-PREVIEW
    "d2307dae": "M-HIST-PREVIEW", "0dee4701": "M-HIST-PREVIEW", "93c01884": "M-HIST-PREVIEW",
    "e621dffc": "M-HIST-PREVIEW", "3f22d184": "M-HIST-PREVIEW", "fb0beb2c": "M-HIST-PREVIEW",
    "8eb3d974": "M-HIST-PREVIEW", "51efe833": "M-HIST-PREVIEW", "1efe8331": "M-HIST-PREVIEW",
    # —— 工作流执行引擎（执行器 / 中断隔离 / 运行会话 / 租约） → M-HIST-CANVAS
    "78dd45fd": "M-HIST-CANVAS", "45a8834a": "M-HIST-CANVAS", "9489ce4f": "M-HIST-CANVAS",
    "07215632": "M-HIST-CANVAS", "ba2b3ab9": "M-HIST-CANVAS", "f6830203": "M-HIST-CANVAS",
    "378dd45f": "M-HIST-CANVAS", "4cb278ea": "M-HIST-CANVAS",
    # —— 相机设备列表随方案持久化（语义在相机管理） → M-SRC-CAM
    "d200be39": "M-SRC-CAM",
    # —— 版本注入 / 安装包 / 打包流水线 → M-SRC-DELIVERY
    "4bfe67c5": "M-SRC-DELIVERY", "cfcc77ce": "M-SRC-DELIVERY",
    # —— 构建体系（语言资源裁剪 / 构建路径基准） → M-HIST-CORE
    "d9675564": "M-HIST-CORE", "3792688e": "M-HIST-CORE",
    # —— 测试基建（测试工程入解决方案 / e2e 测试图 / 验证总计划） → M-SRC-TEST
    "5afa422b": "M-SRC-TEST", "e21fb5d5": "M-SRC-TEST",
    "1caceffb": "M-SRC-TEST", "3574c9f2": "M-SRC-TEST",
    # —— 工具分组声明式折叠（框架契约） → M-HIST-CONTRACT
    "af07153e": "M-HIST-CONTRACT",
    # —— 几何测量工具集诞生提交 → 点-点测量（六个测量工具共同计入）
    "ca2b9592": "T-P2P",
    # —— ROI 形状收敛 / 区域订阅 / 绑定源树 → M-HIST-REGION
    "c8024b3c": "M-HIST-REGION", "b3eedf0d": "M-HIST-REGION", "8ed840ba": "M-HIST-REGION",
    # —— 工具插件自包含改造（构建反转 / 隔间加载） → M-HIST-CORE
    "d1968cba": "M-HIST-CORE",
    # —— 渲染文本排版 → M-HIST-PREVIEW
    "3f22d184": "M-HIST-PREVIEW",
    # —— 目标参数与全局变量绑定 UI 方案 → M-HIST-GLOBALVAR
    "abbb9416": "M-HIST-GLOBALVAR", "485daf33": "M-HIST-GLOBALVAR",
}

# ---------- 不计入产品计划的提交（无产品语义，避免污染看板） ----------
skipCommits = {
    "72dddc7c",  # chore: checkpoint main worktree before sync（同步前临时检查点）
    "1ed32066",  # chore(collab): 协作规则 rule-021 与协作文档（非产品模块）
}

def match_module(msg, files):
    """返回 (主模块, 关联工具列表)。工具提交可能同时命中多个工具目录
    （如「圆/直线查找拟合统一」同时改 CircleDetect + LineDetect），
    主归属给第一个命中者，关联列表让该提交也计入其他工具模块的周统计/日志。

    例外：交叉性「工具基座 / 契约 / 结果呈现」提交会同时改十几个工具目录、
    每个只改 1~2 个文件（命中面广而浅），这类提交不应认领给某个具体工具，
    改判给 src 侧模块（契约/参数/结果树等），工具目录全部作为关联模块。"""
    related = _tool_hits(files)
    if related:
        # 浅命中判定：命中 ≥4 个工具模块且单模块命中文件数 ≤2 → 视为基座/契约类提交
        counts = []
        for mid in related:
            paths = [p for ps, m in pathMap if m == mid for p in ps]
            n = sum(1 for f in files if any(p.lower() in f.lower() for p in paths))
            counts.append(n)
        shallow = len(related) >= 4 and max(counts) <= 2
        if not shallow:
            return related[0], related[1:]
    # 路径匹配：按「命中文件数」选归属，而非「先到先得」。
    # 修复 bug：7605075 主要改 UI（src/ 32 + tools/ 13）但顺带改 tests/(3)，
    # 原逻辑 tests/ 规则排在前面，抢先归到 M-SRC-TEST，导致 UI 提交被错记。
    # 现改为统计每个模块命中多少个文件，选命中最多者；平局取 pathMap 靠前者。
    best_mid = None
    best_count = 0
    for paths, mid in pathMap:
        if mid.startswith("T-"):
            continue  # 工具已在上面精确处理
        cnt = 0
        for p in paths:
            for f in files:
                if p.lower() in f.lower():
                    cnt += 1
        if cnt > best_count:
            best_count = cnt
            best_mid = mid
    if best_mid and best_count > 0:
        return best_mid, related
    # 关键词匹配（仅当无路径命中时兜底）
    for kws, mid in keywordMap:
        for k in kws:
            if k.lower() in msg.lower():
                return mid, related
    return "UNKNOWN", related

def match_module_for(hash_, msg, files):
    """带人工覆盖表的匹配入口：hash 命中覆盖表时，主归属直接取覆盖值；
    命中 skipCommits 时返回 (None, [])，调用方应直接跳过该提交。"""
    if hash_[:8] in skipCommits:
        return None, []
    mid, related = match_module(msg, files)
    ov = manualOverrides.get(hash_[:8])
    if ov:
        return ov, [m for m in related if m != ov]
    return mid, related

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
    _business_prefixes = ("src/", "tools/", "tests/", "docs/", "libs/", "PluginDevKit/", "Help/",
                          "installer/", "version.json", "scripts/", "testdata/", "suneyevision.slnx",
                          "Directory.Build.props", "Directory.Build.targets")
    _has_biz = any(f.lower().startswith(_business_prefixes) for f in files)
    if not _has_biz:
        return

    mid, related = match_module_for(hash_, subject, files)
    if mid is None:      # 命中 skipCommits：不计入产品计划
        return
    # 主归属计入周模块统计（不重复计 commit 总数）
    w = weeks.setdefault(week, {"commits": 0, "modules": {}})
    w["commits"] += 1
    w["modules"][mid] = w["modules"].get(mid, 0) + 1

    # 记录主归属 + 关联工具（如 圆/直线查找同时改动的提交两边都记）
    _targets = [mid] + related if mid != "UNKNOWN" else [mid]
    for _mid in _targets:
        mw = module_weeks.setdefault(_mid, [])
        found = False
        for item in mw:
            if item["week"] == week:
                item["commits"] += 1
                found = True
                break
        if not found:
            mw.append({"week": week, "commits": 1})

        # 记录真实提交日志（按模块）
        mc = module_commits.setdefault(_mid, [])
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
