# -*- coding: utf-8 -*-
"""
assign_weeks.py - 未来任务自动摊排到周（初版）
按模块 start/end 区间 + 任务工时比例，把未来任务摊排到 ISO 周，写回 plan.json 的 week 字段。

规则：
- 模块 start/end 跨 N 周（按周一为界），每个任务按其 hours 占比落入对应周
- 任务保持模块内顺序，按累计工时切分到周
- 已有 week 字段的任务跳过（保留手动微调）
用法: python plans/assign_weeks.py
"""
import json
import io
import datetime

path = r"D:\MyWork\SunEyeVision_V2-script\plans\plan.json"
with io.open(path, encoding="utf-8-sig") as f:
    data = json.load(f)

def iso_week(d):
    iso = d.isocalendar()
    return f"{iso[0]}-W{iso[1]:02d}"

def week_monday(week_str):
    """'2026-W37' → 该周周一的 date"""
    year, w = week_str.split("-W")
    year = int(year); w = int(w)
    jan4 = datetime.date(year, 1, 4)
    monday_jan4 = jan4 - datetime.timedelta(days=jan4.weekday())
    return monday_jan4 + datetime.timedelta(weeks=w - 1)

def weeks_between(start_date, end_date):
    """返回 start 到 end 覆盖的 ISO 周列表（按周一归属）"""
    cur = start_date
    # 回到当前周周一
    cur = cur - datetime.timedelta(days=cur.weekday())
    end = end_date
    weeks = []
    while cur <= end:
        weeks.append(iso_week(cur))
        cur += datetime.timedelta(weeks=1)
    return weeks

assigned = 0
skipped = 0
changed_modules = []

for m in data["modules"]:
    if m["stage"] in ("HIST", "U0", "T0"):
        continue  # 已完成/进行中的不摊排
    if not m.get("start") or not m.get("end"):
        continue

    tasks = m["tasks"]
    # 跳过已有 week 的任务（保留手动微调）
    pending = [t for t in tasks if not t.get("week")]
    if not pending:
        continue

    try:
        start_d = datetime.date.fromisoformat(m["start"])
        end_d = datetime.date.fromisoformat(m["end"])
    except ValueError:
        continue

    weeks = weeks_between(start_d, end_d)
    if not weeks:
        continue

    total_hours = sum(t.get("hours", 0) for t in pending)
    if total_hours <= 0:
        continue

    # 每周可容纳工时 = 总工时 / 周数（均匀分配，可在此调整）
    per_week = total_hours / len(weeks)

    # 按任务顺序 + 累计工时切分到周
    week_idx = 0
    week_used = 0.0
    for t in pending:
        th = t.get("hours", 0)
        # 任务跨周：如果当前周剩余放不下整个任务，切到下一周（整任务归属，不拆分）
        if week_used + th > per_week and week_used > 0 and week_idx < len(weeks) - 1:
            week_idx += 1
            week_used = 0.0
        t["week"] = weeks[min(week_idx, len(weeks) - 1)]
        week_used += th
        assigned += 1

    changed_modules.append(m["id"])

with io.open(path, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, ensure_ascii=False, indent=4)
    f.write("\n")

print(f"已摊排 {assigned} 个任务（{len(changed_modules)} 个模块）")
print(f"模块: {', '.join(changed_modules[:10])}{'...' if len(changed_modules)>10 else ''}")

# 预览：按周统计
print("\n=== 每周计划预览 ===")
from collections import defaultdict
weekly = defaultdict(list)
for m in data["modules"]:
    for t in m["tasks"]:
        if t.get("week"):
            weekly[t["week"]].append((m["id"], t["id"], t["name"], t["hours"]))
for wk in sorted(weekly.keys()):
    items = weekly[wk]
    h = sum(x[3] for x in items)
    names = " | ".join(f"{x[1]}" for x in items[:5])
    print(f"{wk}: {len(items)} 任务 / {h}h  {names}")
