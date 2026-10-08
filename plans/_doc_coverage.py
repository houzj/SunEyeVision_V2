# -*- coding: utf-8 -*-
"""文档覆盖盘点：把 docs/ 下所有 md 映射到计划模块（关键词命中文件名/正文），
输出每模块的文档覆盖矩阵（方案设计/工具设计/实施总结/测试报告/框架架构/指南），用于"代码实施是否有方案文档"的核查。
用法: py plans/_doc_coverage.py [--gaps]
"""
import io, json, re, subprocess, sys
from collections import Counter, defaultdict

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
REPO = r"D:\MyWork\SunEyeVision_V2"

# ---------- 模块关键词表（语义优先，覆盖模块名/工具名/技术名词） ----------
KW = {
    "M-HIST-CORE":       ["基础框架", "视觉框架", "日志系统", "插件加载", "工具箱", "主界面", "框架重构"],
    "M-HIST-CANVAS":     ["画布", "工作流编辑器", "节点", "连线", "小地图", "撤销", "命中测试", "画布渲染"],
    "M-HIST-PATH":       ["路径算法", "贝塞尔", "ConnectionPath", "连线路径"],
    "M-HIST-PREVIEW":    ["图像预览", "渲染", "GPU", "缩略图", "显示性能"],
    "M-HIST-SOLUTION":   ["解决方案系统", "方案管理", "方案持久化", "序列化", "解决方案文件"],
    "M-HIST-CVENGINE":   ["CvEngine", "算子", "Halcon 对标", "Halcon对标"],
    "M-HIST-PARAM":      ["参数绑定", "ParamBinding", "绑定显示名", "订阅链路", "参数契约", "运行快照"],
    "M-HIST-RESULTTREE": ["结果树", "绑定树", "模块结果", "结果页"],
    "M-HIST-REGION":     ["区域", "RoI", "Region", "区域编辑器"],
    "M-HIST-MATCH":      ["ShapeMatch", "轮廓匹配", "形状匹配"],
    "M-HIST-SCRIPT":     ["脚本工具", "Script", "Roslyn", "脚本调试"],
    "M-HIST-CONTRACT":   ["输出变量契约", "类型系统", "契约", "SdkTypeVocabulary", "工具能力"],
    "M-HIST-SDKCTRL":    ["SDK 控件", "控件库", "RoiEditor", "控件"],
    "M-HIST-DATAMODEL":  ["数据结构", "数据模型", "绑定模型"],
    "M-HIST-GLOBALVAR":  ["全局变量"],
    "M-SRC-UI":          ["UI 项目优化", "主题", "UI 优化"],
    "M-SRC-CAM":         ["相机管理", "相机采集架构", "相机设备", "相机管理器", "相机调参", "相机持久化"],
    "M-SRC-CAL":         ["相机标定", "内参标定", "畸变校正", "标定板"],
    "M-SRC-HECAL":       ["手眼标定"],
    "M-SRC-POSE":        ["位姿", "Pose 输出"],
    "M-SRC-COMM":        ["通讯管理", "通讯运行时", "通讯管理器", "Modbus", "协议运行时"],
    "M-SRC-RUNUI":       ["运行界面", "运行 UI", "运行UI", "操作员界面"],
    "M-SRC-INTEG":       ["端到端联调", "联调", "集成测试"],
    "M-SRC-TEST":        ["测试体系", "测试分层", "假测试", "测试报告"],
    "M-SRC-DELIVERY":    ["交付", "发布", "版本管理", "安装包", "打包"],
    "T-CIRCLEFINDER":    ["圆查找", "找圆"],
    "T-LINEFINDER":      ["直线查找", "找线"],
    "T-LINECALIPER":     ["直线卡尺"],
    "T-CIRCLECALIPER":   ["圆卡尺"],
    "T-ARCCALIPER":      ["弧形卡尺", "圆弧卡尺"],
    "T-SHAPEMATCH":      ["ShapeMatch", "轮廓匹配"],
    "T-BLOB":            ["Blob", "连通域"],
    "T-P2P":             ["点点测量", "点-点测量", "点对点"],
    "T-P2L":             ["点线测量", "点-线测量"],
    "T-P2C":             ["点圆测量", "点-圆测量"],
    "T-L2L":             ["线线测量", "线-线测量"],
    "T-L2C":             ["线圆测量", "线-圆测量"],
    "T-C2C":             ["圆圆测量", "圆-圆测量"],
    "T-SUBROUTINE":      ["子程序"],
    "T-CONDITION":       ["条件分支"],
    "T-SCRIPT":          ["脚本工具"],
    "T-IMGCAPTURE":      ["图像源", "相机采集工具", "采集工具"],
    "T-COMMTOOL":        ["通信工具", "通讯工具", "OPC UA"],
    "T-CONVERT":         ["坐标转换", "坐标变换"],
    "T-ROBOTCOMM":       ["机器人通信", "机器人通讯"],
    "T-IMAGELOAD":       ["图像加载"],
    "T-THRESHOLD":       ["阈值工具", "阈值分割"],
    "T-IMAGEFILTER":     ["图像滤波"],
    "T-IMAGETHRESHOLD":  ["图像阈值"],
    "T-MORPHOLOGY":      ["形态学"],
    "T-CALIBPLATE":      ["标定板"],
    "T-UNDIST":          ["畸变"],
    "T-HECAP":           ["手眼标定采集"],
    "T-NPOINTCALIB":     ["N 点标定", "N点标定", "九点标定"],
}
TYPE_KEYS = {
    "01-方案设计": "方案设计", "04-工具设计": "工具设计", "02-实施总结": "实施总结",
    "03-测试报告": "测试报告", "05-框架架构": "框架架构", "06-指南参考": "指南参考",
}

plan = json.load(io.open(REPO + r"\plans\plan.json", encoding="utf-8"))
mod_names = {m["id"]: m["name"] for m in plan["modules"]}
mod_status = {m["id"]: m["status"] for m in plan["modules"]}

files = subprocess.run(["git", "ls-files", "docs/"], capture_output=True, text=True,
                       encoding="utf-8", cwd=REPO).stdout.split()
docs = []
for f in files:
    if not f.endswith(".md"):
        continue
    path = REPO + "\\" + f.replace("/", "\\")
    try:
        text = io.open(path, encoding="utf-8", errors="replace").read()
    except Exception:
        text = ""
    docs.append({"path": f, "name": f.split("/")[-1], "text": text})

print("文档总数: %d" % len(docs))

# ---------- 覆盖矩阵 ----------
cover = defaultdict(Counter)     # mid -> {类型: 篇数}
examples = defaultdict(lambda: defaultdict(list))
for d in docs:
    typ = next((v for k, v in TYPE_KEYS.items() if d["path"].startswith("docs/" + k)), "其他")
    hay = d["path"] + "\n" + d["name"] + "\n" + d["text"][:20000]
    for mid, kws in KW.items():
        for k in kws:
            if k in hay:
                cover[mid][typ] += 1
                if len(examples[mid][typ]) < 3:
                    examples[mid][typ].append(d["name"])
                break

gaps = "--gaps" in sys.argv
print("\n%-18s %-24s %5s %5s %5s %5s %5s  %s" % ("模块", "名称", "方案", "工具", "实施", "测试", "架构", "状态"))
no_design = []
for mid in KW:
    c = cover[mid]
    total = sum(c.values())
    design = c["方案设计"] + c["工具设计"]
    flag = "✔" if design else ("—" if total else "�’无文档")
    if not design:
        no_design.append(mid)
    name = mod_names.get(mid, "(计划外)")
    print("%-18s %-24s %5d %5d %5d %5d %5d  %s" % (
        mid, name[:22], c["方案设计"], c["工具设计"], c["实施总结"], c["测试报告"], c["框架架构"],
        ("有方案" if design else ("仅其他类" if total else "无文档"))))

print("\n=== 无方案/设计文档的模块: %d 个" % len(no_design))
for mid in no_design:
    print("   %-18s %s (其他类文档 %d 篇)" % (mid, mod_names.get(mid, "计划外"), sum(cover[mid].values())))

if gaps:
    print("\n=== 样例（方案/工具设计的前 3 篇） ===")
    for mid, c in examples.items():
        if c["方案设计"] or c["工具设计"]:
            print(" ", mid, "|", " / ".join((c["方案设计"] + c["工具设计"])[:3]))

# 汇总
print("\n=== 汇总 ===")
print("  有方案/设计文档的模块: %d / %d" % (len(KW) - len(no_design), len(KW)))
print("  方案设计命中总篇数: %d（含跨模块重复计数）" % sum(c["方案设计"] for c in cover.values()))
