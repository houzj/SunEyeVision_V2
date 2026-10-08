# -*- coding: utf-8 -*-
"""把「软件开发进展汇报」与「文档体系与方案覆盖总结」合并为一份完整汇报。
- 输出：docs/02-实施总结/2026/10/01/视觉软件SunEyeVision-软件开发汇报.html（同名覆盖）
- 章节合并后统一重编号；两份附录合并；补齐文档侧所需 CSS。
"""
import io, re, sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
D = r"D:\MyWork\SunEyeVision_V2\docs\02-实施总结\2026\10\01"
MAIN = D + r"\软件开发进展汇报-v1-20261001-130000.html"
DOCS = D + r"\视觉软件SunEyeVision-软件开发汇报.html"        # 用户改名后的文档体系那份
OUT = D + r"\视觉软件SunEyeVision-软件开发汇报.html"

main = io.open(MAIN, encoding="utf-8").read()
docs = io.open(DOCS, encoding="utf-8").read()

SEC_RE = re.compile(r'<section class="slide(?: cover)?">.*?</section>', re.S)
main_secs = SEC_RE.findall(main)
docs_secs = SEC_RE.findall(docs)
print("进展汇报节数:", len(main_secs), "| 文档体系节数:", len(docs_secs))
assert len(main_secs) == 13 and len(docs_secs) == 8, "节数不符，检查文件"

head = main.split("<body>")[0] + "<body>\n<div class=\"wrap\">\n"

# ---------- 补齐文档侧 CSS ----------
extra_css = """
  /* 合并自文档体系汇报：文档类配色与卡片 */
  :root{ --doc:#7c8cf8; }
  .kpi.purple b{color:var(--doc)}
  .kpi.green b{color:var(--ok)}
  .p-ok{background:#e7f7ee;color:#12764a;border:1px solid #bfe7d1}
  .p-doc{background:#eef0ff;color:#4646b8;border:1px solid #d3d7ff}
  .p-warn{background:#fff5e2;color:#9a6209;border:1px solid #f6dfb0}
  .p-risk{background:#fdecec;color:#a92c2c;border:1px solid #f5c9c9}
  .code{font-family:Consolas,"Courier New",monospace;font-size:12px;background:#f4f7fa;border:1px solid var(--line);border-radius:6px;padding:2px 6px}
"""
head = head.replace("</style>", extra_css + "</style>")

# ---------- 章节组装 ----------
merged = []
merged.append(main_secs[0])            # 封面（稍后改写）
merged += main_secs[1:9]               # 进展 1..8（关键指标→工程化与质量）
merged += docs_secs[1:7]               # 文档 §1 资产总览 / §2 产量节奏 / §3 覆盖核查 / §4 闭环 / §5 制度保障 / §6 缺口与改进
merged += main_secs[9:12]              # 风险 / 下阶段计划 / 需领导支持
merged.append(main_secs[12])           # 进展 附录（数据口径）
merged.append(docs_secs[7])            # 文档 附录（统计口径）

# ---------- 统一重编号（封面与"附"保持不变） ----------
n = 0
out_secs = []
for s in merged:
    if 'class="slide cover"' in s:
        out_secs.append(s); continue
    if '<span class="no">附</span>' in s:
        out_secs.append(s); continue
    n += 1
    s = re.sub(r'<span class="no">[^<]*</span>', '<span class="no">%d</span>' % n, s, count=1)
    out_secs.append(s)
print("正文节编号 1..%d" % n)

body = "\n\n".join(out_secs) + "\n\n</div>\n</body>\n</html>\n"
html = head + body

# ---------- 改写封面（合并两份的结论与标签） ----------
html = html.replace(
    "<title>SunEyeVision V2 软件开发进展汇报 · 2026-10-01</title>",
    "<title>视觉软件 SunEyeVision · 软件开发汇报 · 2026-10-01</title>")
html = html.replace(
    "<div class=\"meta\">SunEyeVision V2 · 视觉引导抓取平台 &nbsp;|&nbsp; 汇报周期：2026-01-21 ~ 2026-10-01</div>",
    "<div class=\"meta\">视觉软件 SunEyeVision（V2）· 视觉引导抓取平台 &nbsp;|&nbsp; 汇报周期：2026-01-21 ~ 2026-10-01</div>")
html = html.replace("<h1>软件开发进展汇报</h1>", "<h1>软件开发汇报</h1>")
html = html.replace(
    '同期两条并行线（12prMO 现场调试、SunAgent AI 赋能原型）已分别交付与进入平台化。',
    '同期两条并行线（12prMO 现场调试、SunAgent AI 赋能原型）已分别交付与进入平台化；'
    '文档侧坚持「方案先行」——<b>337 篇文档 / 约 175 万字</b>，<b>52 / 54 个模块</b>有方案或设计文档，'
    '<b>19 / 19 个工具插件</b>全部有设计文档。')
html = html.replace(
    "<span>并行线：SunAgent AI 原型 71 次提交</span>",
    "<span>并行线：SunAgent AI 原型 71 次提交</span><span>337 篇文档 · 175 万字</span><span>方案覆盖 52/54 模块</span>")

# ---------- 附录标题区分 + 页脚 ----------
html = html.replace("<h2><span class=\"no\">附</span>数据来源与口径</h2>",
                    "<h2><span class=\"no\">附</span>数据来源与口径（代码与计划）</h2>", 1)
html = html.replace("<h2><span class=\"no\">附</span>统计口径</h2>",
                    "<h2><span class=\"no\">附</span>统计口径（文档体系）</h2>", 1)
html = html.replace(
    "SunEyeVision V2 软件开发进展汇报 · 2026-10-01 · 汇报稿 v1（HTML 评审版，确认后输出 PPT）",
    "视觉软件 SunEyeVision 软件开发汇报 · 2026-10-01 · 合并版（进展 + 文档体系）· 确认后输出 PPT")
html = html.replace(
    "SunEyeVision V2 文档体系与方案覆盖总结 · 2026-10-01 · v1（评审稿，可并入进展汇报 PPT）",
    "视觉软件 SunEyeVision 软件开发汇报 · 2026-10-01 · 合并版（进展 + 文档体系）· 确认后输出 PPT")

io.open(OUT, "w", encoding="utf-8", newline="\n").write(html)

# ---------- 结构校验 ----------
for tag in ["section", "div", "table", "ul", "svg"]:
    o = len(re.findall(r"<%s[ >]" % tag, html)); c = html.count("</%s>" % tag)
    print("  <%s> %d/%d %s" % (tag, o, c, "OK" if o == c else "!! 不匹配"))
print("章节:", re.findall(r'<span class="no">([^<]+)</span>([^<]+)</h2>', html))
print("输出:", OUT, "|", len(html.encode("utf-8")), "字节")
