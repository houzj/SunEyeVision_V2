# -*- coding: utf-8 -*-
"""计划同步预览：用修正后的匹配规则对指定区间提交做语义归类，输出模块分布 + 明细，供人工核对。
用法: py plans/_sync_preview.py <since_hash> [--all]
"""
import io
import subprocess
import sys
import importlib.util
from collections import Counter, defaultdict

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

HERE = r"D:\MyWork\SunEyeVision_V2\plans"
spec = importlib.util.spec_from_file_location("gw", HERE + r"\generate_weekly.py")
gw = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gw)

SINCE = sys.argv[1] if len(sys.argv) > 1 else "b7c084c3"
SHOW_ALL = "--all" in sys.argv

BIZ = ("src/", "tools/", "tests/", "docs/", "libs/", "plugindevkit/", "help/",
       "installer/", "version.json", "scripts/")
FILTER = ("M-SRC-UI", "T-CIRCLEFINDER", "M-HIST-CORE", "M-SRC-TEST", "M-SRC-INTEG",
          "M-HIST-DATAMODEL", "M-HIST-SOLUTION", "M-HIST-CONTRACT", "M-SRC-CAM",
          "M-SRC-COMM", "M-SRC-DELIVERY", "M-SRC-CAL", "M-HIST-SDKCTRL", "M-HIST-PREVIEW")

out = subprocess.run(["git", "log", SINCE + "..HEAD", "--format=@@%H|%ci|%s", "--name-only"],
                     capture_output=True, text=True, encoding="utf-8").stdout
commits, cur = [], None
for line in out.splitlines():
    if line.startswith("@@"):
        h, ci, s = line[2:].split("|", 2)
        cur = {"h": h, "ci": ci, "s": s.strip(), "files": []}
        commits.append(cur)
    elif line.strip() and cur is not None:
        cur["files"].append(line.strip())

buckets = defaultdict(list)
skipped = []
for c in commits:
    if not any(f.lower().startswith(BIZ) for f in c["files"]):
        skipped.append(c)
        continue
    mid, related = gw.match_module_for(c["h"], c["s"], c["files"])
    buckets[mid].append((c, related))

print("总提交 %d | 业务 %d | 跳过 %d" % (len(commits),
                                        sum(len(v) for v in buckets.values()), len(skipped)))
print("---- 模块分布 " + "-" * 40)
for k, v in sorted(buckets.items(), key=lambda x: -len(x[1])):
    print("  %-16s %d" % (k, len(v)))

if SHOW_ALL:
    ids = sorted(buckets.keys())
else:
    ids = [i for i in FILTER if i in buckets]
for mid in ids:
    print("\n==== %s (%d) ====" % (mid, len(buckets[mid])))
    for c, rel in buckets[mid]:
        tops = sorted(set("/".join(f.split("/")[:2]) for f in c["files"]))
        print("  %s %s | %s" % (c["h"][:8], c["s"][:56], ", ".join(tops)[:70]))
        if rel:
            print("        +关联: %s" % ",".join(rel))

if skipped:
    print("\n---- 跳过（无业务路径） ----")
    for c in skipped:
        print("  %s %s" % (c["h"][:8], c["s"][:60]))
