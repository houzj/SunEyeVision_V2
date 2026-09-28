# -*- coding: utf-8 -*-
"""生成计划同步载荷：把区间提交归类为各模块日志 + 状态更新表 + 临时任务池归位。
输出 plans/_sync_payload.json，由 _apply_plan_sync.ps1 应用到 plan.json。
用法: py plans/_build_sync_payload.py <since_hash>
"""
import io
import json
import subprocess
import sys
import importlib.util
from collections import OrderedDict

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

REPO = r"D:\MyWork\SunEyeVision_V2"
SINCE = sys.argv[1] if len(sys.argv) > 1 else "b7c084c3"

spec = importlib.util.spec_from_file_location("gw", REPO + r"\plans\generate_weekly.py")
gw = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gw)

# ---------- 1. 分类区间提交 ----------
out = subprocess.run(["git", "log", SINCE + "..HEAD", "--no-merges",
                      "--format=@@%H|%ci|%s", "--name-only"],
                     capture_output=True, text=True, encoding="utf-8").stdout
commits, cur = [], None
for line in out.splitlines():
    line = line.rstrip()
    if line.startswith("@@"):
        h, ci, s = line[2:].split("|", 2)
        cur = {"hash": h, "date": ci[:10], "subject": s.strip(), "files": []}
        commits.append(cur)
    elif line.strip() and cur is not None:
        cur["files"].append(line.strip().strip('"'))

logs = OrderedDict()
skipped = []
BIZ = ("src/", "tools/", "tests/", "docs/", "libs/", "plugindevkit/", "help/",
       "installer/", "version.json", "scripts/", "testdata/", "suneyevision.slnx",
       "directory.build.props", "directory.build.targets")
for c in commits:
    if not any(f.lower().startswith(BIZ) for f in c["files"]):
        skipped.append(c)
        continue
    mid, related = gw.match_module_for(c["hash"], c["subject"], c["files"])
    if mid is None:
        skipped.append(c)
        continue
    # 只记主归属日志；多工具关联关系由 generate_weekly.py 的 gitlogs 承担，避免日志重复
    logs.setdefault(mid, []).append({
        "date": c["date"],
        "note": "[%s] %s" % (c["hash"], c["subject"]),
    })

# ---------- 2. 临时任务池归位（原有 5 条历史临时任务） ----------
tempReclass = {
    "TMP-b3f20b7": ("M-HIST-SDKCTRL", "[b3f20b7a39018c424d7b95ce701e77d5adb7d54a] fix(measure): 位置修正交互层按几何有向性对齐，修复直线测量带二次变换"),
    "TMP-ddd0d27": ("T-CIRCLEFINDER", "[ddd0d27] feat(ui): 查找工具卡尺输入回显（引导线/测量带）到模块结果树"),
    "TMP-8075050": ("T-P2P", "[8075050] docs: 几何测量工具集设计方案落盘 v1.0 与 v2.0 收敛版（六工具共用）"),
    "TMP-580444b": ("M-HIST-PARAM", "[580444b] docs: 几何输入订阅链路单模型重构方案落盘 v3 定稿"),
    "TMP-e258a3d": ("T-P2P", "[e258a3da] fix(measure): 点-点测量补调试窗口入口并移除重复的位置修正"),
}
temp_dates = {"TMP-b3f20b7": "2026-09-05", "TMP-ddd0d27": "2026-09-05",
              "TMP-8075050": "2026-09-07", "TMP-580444b": "2026-09-08",
              "TMP-e258a3d": "2026-09-10"}
for tid, (mid, note) in tempReclass.items():
    logs.setdefault(mid, []).append({"date": temp_dates[tid], "note": note})

# ---------- 3. 状态更新表（语义判定） ----------
module_status = {
    "T-P2P": "done", "T-P2L": "done", "T-P2C": "done",
    "T-L2L": "done", "T-L2C": "done", "T-C2C": "done",
    "T-IMGCAPTURE": "in_progress",
    "M-SRC-CAM": "in_progress",
    "M-SRC-COMM": "in_progress",
    "M-SRC-DELIVERY": "in_progress",
    "M-SRC-TEST": "in_progress",
}
task_status_flat = {}
for pre in ("P2P", "P2L", "P2C", "L2L", "L2C", "C2C"):
    for i in (1, 2, 3):
        task_status_flat["%s-%d" % (pre, i)] = "done"
task_status_flat.update({
    # 相机管理：采集链路 / 参数读写 / 管理 UI 已完成，真机测试进行中
    "CAM-1": "done", "CAM-2": "done", "CAM-3": "done", "CAM-4": "in_progress",
    # 通讯管理：驱动抽象 / TCP 真实实现 / 管理 UI 已完成，链路测试进行中
    "COMM-1": "done", "COMM-2": "done", "COMM-3": "done", "COMM-4": "in_progress",
    # 相机采集工具（图像源）：核心与配置已通，调试控件与测试进行中
    "IMGCAP-1": "done", "IMGCAP-2": "in_progress", "IMGCAP-3": "in_progress",
    # 交付：测试套件 / 手册 / 上线 均在推进（已出 2.0.0.1 内测版）
    "DELIVERY-1": "in_progress", "DELIVERY-2": "in_progress", "DELIVERY-3": "in_progress",
})

# 任务状态按模块归组，避免跨模块任务 ID 撞名（如 M-SRC-COMM 与 T-COMMTOOL 都有 COMM-1）
TASK_OWNER = {          # 任务 ID 前缀 -> 所属模块
    "P2P": "T-P2P", "P2L": "T-P2L", "P2C": "T-P2C",
    "L2L": "T-L2L", "L2C": "T-L2C", "C2C": "T-C2C",
    "CAM": "M-SRC-CAM", "COMM": "M-SRC-COMM",
    "IMGCAP": "T-IMGCAPTURE", "DELIVERY": "M-SRC-DELIVERY",
}
task_status = {}
for tid, st in task_status_flat.items():
    prefix = tid.rsplit("-", 1)[0]
    owner = TASK_OWNER.get(prefix)
    if owner:
        task_status.setdefault(owner, {})[tid] = st
    else:
        print("  警告: 任务 %s 无归属模块，未应用" % tid)

# ---------- 4. 阶段 / 里程碑字段修正（保证路线图"当前阶段"跟着真实进展走） ----------
stage_fields = {
    "U0": {"name": "源码层 UI 项目优化"},        # 去掉名称里残留的"（进行中）"
    # 测量工具链已完工（六个几何测量工具：算法 + 双卡尺调试控件 + 契约测试）
    "CC": {"name": "测量工具链", "status": "done", "end": "2026-09-28",
           "goal": "测量工具链：圆查找/直线查找 + 几何测量工具集（P2P/P2L/P2C/L2L/L2C/C2C）全部完成（算法 + 调试控件 + 契约测试）"},
    # 相机/通讯管理自 09-22 起已在开发；图像源自 09-28 开工，两阶段并行推进
    "CD": {"status": "in_progress", "start": "2026-09-22"},
    "CE": {"status": "in_progress", "start": "2026-09-28"},
}
milestone_fields = {
    "测量工具链完成": {"date": "2026-09-28"},     # 里程碑日期对齐实际完工日
}

payload = {
    "since": SINCE,
    "commitCount": len(commits),
    "logs": logs,
    "moduleStatus": module_status,
    "taskStatus": task_status,
    "stageFields": stage_fields,
    "milestoneFields": milestone_fields,
    "tempRemove": list(tempReclass.keys()),
}
with io.open(REPO + r"\plans\_sync_payload.json", "w", encoding="utf-8", newline="\n") as f:
    json.dump(payload, f, ensure_ascii=False, indent=1)

print("区间提交: %d | 计入: %d | 跳过: %d" % (
    len(commits), sum(len(v) for v in logs.values()), len(skipped)))
print("日志模块数: %d" % len(logs))
for mid, items in logs.items():
    print("  %-18s +%d" % (mid, len(items)))
print("跳过未计入: %s" % ", ".join(c["hash"][:8] for c in skipped))
