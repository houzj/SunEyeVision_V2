"""Refresh the existing O06 S1 summary and verification anchors from the stage receipt."""
from pathlib import Path
from html import escape
from html.parser import HTMLParser
import hashlib
import json
import os
import re
from urllib.parse import quote

ROOT = Path(__file__).resolve().parents[2]
PAGE = ROOT / 'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护/优化方案/optimization-o06.html'
RECEIPT = ROOT / 'docs/collaboration/S1_RELEASE_CLOSURE_20261025.json'


def table(headers, rows):
    return '<div class="br-scroll"><table><thead><tr>' + ''.join(
        '<th>' + escape(x) + '</th>' for x in headers
    ) + '</tr></thead><tbody>' + ''.join(
        '<tr>' + ''.join('<td>' + escape(x) + '</td>' for x in row) + '</tr>' for row in rows
    ) + '</tbody></table></div>'


def render_s1_stage(page):
    if not RECEIPT.exists():
        return page
    receipt = json.loads(RECEIPT.read_text(encoding='utf-8-sig'))
    if receipt.get('status') != 'S1_IMPLEMENTATION_AND_AUTOMATED_CHECKS_CLOSED':
        return page
    rows = receipt['implementationSummary']
    receipt_href = quote(os.path.relpath(RECEIPT, PAGE.parent).replace('\\', '/'), safe='/.')
    archive_href = quote(os.path.relpath(ROOT / receipt['evidenceArchive']['path'], PAGE.parent).replace('\\', '/'), safe='/.')
    history_href = quote(os.path.relpath(ROOT / 'docs/collaboration/history/O06_S1_before_summary_20261025.json', PAGE.parent).replace('\\', '/'), safe='/.')
    state = ('S1基础实施及本轮自动验证已闭合，D盘正式Release已更新。'
             '完整Blob界面、控件重组及交互链尚未完成；本阶段不单独安排人工验证。')
    link = '<a href="#blob-verification-steps">S1实施总结、优化效果与验证安排</a>'
    top = ('<div class="br-note" id="o06-blob-final-review"><h3>S1已完成什么，现在需要你做什么？</h3>'
           '<p><b>' + state + '</b></p>'
           '<p>本阶段落实了参数与字段规则、旧方案迁移、图像资源桥接、保存恢复和保存弹窗归属。'
           '新一轮工作完成的是主仓归并、正式Release构建和最终产物补验；没有重新编写整套Blob。</p>'
           '<p><b>当前无需你单独手验。</b>等参数配置、ROI编辑和结果联动等出现明显体验变化后，集中验证。'
           '本轮为当前对话执行的自动检查，不冒称独立验收或用户签收。</p><p>' + link + '</p></div>')
    pattern = r'<!-- O06-BLOB-FINAL-REVIEW:BEGIN -->.*?<!-- O06-BLOB-FINAL-REVIEW:END -->'
    page, count = re.subn(pattern, '<!-- O06-BLOB-FINAL-REVIEW:BEGIN -->\n' + top +
                         '\n<!-- O06-BLOB-FINAL-REVIEW:END -->', page, count=1, flags=re.S)
    if count != 1:
        raise ValueError('Missing original S1 summary region')

    a = page.index('<h3 id="blob-verification-steps">')
    b = page.index('<h3 id="blob-optimization-list">', a)
    previous = page[a:b]
    marker = '<!-- S1-FULL-CHAIN-PLAN:BEGIN -->'
    end_marker = '<!-- S1-FULL-CHAIN-PLAN:END -->'
    if marker in previous:
        plan = previous.split(marker, 1)[1].split(end_marker, 1)[0]
    else:
        plan = previous.split('</h3>', 1)[1]
    plan = plan.replace('参数/绑定/ROI按现有Snapshot/Repository原子恢复',
                        '参数/绑定/ROI按已确认的Snapshot/Repository保存恢复规则恢复（不承诺跨文件掉电原子性）')
    summary = ('<h3 id="blob-verification-steps">⑤ S1实施总结、优化效果与验证安排</h3>'
               '<p><b>结论：</b>S1完成了公共基础、兼容迁移和保存/资源安全边界的实施与本轮自动验证。'
               '正式Release已更新；控件库重构、工具工程Runtime/Wpf拆分、新参数页、ROI交互和结果联动仍在后续范围。</p>'
               '<h4>实施总结：具体改了什么</h4>' +
               table(['优化项', '已经实施', '解决的问题与效果', '本轮验证'], rows) +
               '<h4>优化总结：实际改善在哪里</h4><ol>' + ''.join(
                   '<li>' + escape(x) + '</li>' for x in receipt['optimizationSummary']) + '</ol>'
               '<h4>哪些已经落地，哪些还只是后续设计</h4><p>' +
               escape(receipt['implementationBoundary']) + '</p>'
               '<details><summary>查看工程归属、接口边界和保留限制</summary>' +
               table(['归属', '本阶段落地内容'], receipt['engineeringSummary']) +
               '<p>117个输入声明包含图像/ROI、算法设置和展示配置，不是新增117个可见业务参数。'
               '字段清单有33个分组、79个展示叶字段，原有30个稳定输出身份保留。'
               '坐标单位随坐标系声明，现有标量单位不自动换算，也不宣称已经全部编码进类型。</p>'
               '<p>HALCON仍是当前算法库。中立合同与桥接能力已经增加，尚不能据此宣称全框架已解除HALCON依赖或可直接替换为OpenCV。'
               '本轮未迁移VM的完整Blob算法逻辑，也未重写阈值、连通、筛选主算法。</p>'
               '<p>保存采用快照、提交及补偿恢复；外部竞争可能进入明确恢复状态，不承诺进程崩溃/掉电时跨文件原子性。'
               '设计时构建通过，但未在Visual Studio设计器实际打开；完整主窗口端到端交互未作为本阶段通过项。</p></details>'
               '<h4>现在需要人工验证什么？</h4><p><b>本阶段暂不安排。</b>按用户要求，底层规则没有明显新操作体验时，由自动检查完成阶段核收；'
               '后续出现明显变化再集中验证，不能代用户签收。</p>'
               '<h4>下一次集中验证什么？</h4><ol><li>从工具箱拖入Blob，确认节点和配置窗口正常。</li>'
               '<li>修改参数、绑定图像，绘制或继承检测/屏蔽ROI，检查确定与取消。</li>'
               '<li>执行后对照图像、数量、面积等结果，检查结果表与选中高亮。</li>'
               '<li>检查下游消费，并在保存重开后核对参数、绑定和ROI。</li>'
               '<li>连续运行、切换或关闭窗口，检查旧结果、其他节点图形及资源释放。</li></ol>'
               '<p>这些是后续完整链的人工检查范围，本次S1没有把它们报为已完成。</p>'
               '<details><summary>本轮验证明细、版本及已知问题</summary>' +
               table(['检查内容', '结果'], [[x['name'], x['scope'] + '，通过'] for x in receipt['checks']]) +
               '<p>正式目录：<code>D:\\MyWork\\SunEyeVision_V2\\Release\\SunEyeVision_v1.0.0\\SunEyeVision</code>；'
               '版本2.0.0.8 alpha，产物源码基线6e8d5f1d。全量构建0错误，1087条既有警告保留；'
               '742个发布文件SHA无差异，43条实际加载路径和SHA核验通过。实际程序启动/工具加载探针正常退出，含Blob共22个工具。</p>'
               '<p>截图中的Check.exe崩溃来自检查宿主JSON依赖冲突；已使用正式程序依赖清单和异常保护修正。'
               '最初失败、后续通过和跨卷临时数据清理记录均保留。这里只声明相应自动检查通过，未作独立测试或完整Blob验收声明。</p>'
               '<p><a href="' + receipt_href + '">完整机器回执</a> · <a href="' + archive_href + '">阶段日志、失败及通过证据归档</a> · '
               '<a href="' + history_href + '">更新前的历史摘要</a>。历史结论按各自时点保留，不参与本轮通过数量重复累加。</p></details>'
               '<details><summary>后续完整Blob链12步检查明细（尚未整链验收）</summary>' + marker + plan + end_marker + '</details>\n')
    page = page[:a] + summary + page[b:]

    current_note = '<div class="br-note"><b>当前S1状态：</b>' + state + '详见 ' + link + '。</div>'
    page = re.sub(r'<div class="br-note"><b>当前状态[^<]*</b>.*?</div>',
                  lambda m: current_note if ('de58' in m.group() or 'S1' in m.group()) else m.group(), page, flags=re.S)
    page = page.replace('href="#blob-verification-steps">12步验证', 'href="#blob-verification-steps">S1总结与验证')
    page = page.replace('V8.1证据驱动架构复审 · 2026-10-24 · 设计待整体审核 · 未改产品、未运行新测试',
                        '架构方案与阶段进度 · S1实施及本轮自动验证已闭合 · 完整Blob交互链仍待后续实施')
    page = page.replace('目标方向可审核；Blob完整生产接线、图像桥、七类手势和正式性能仍未验证，不能据本页宣布可实施或已验收。',
                        '完整方案已批准分阶段实施；S1必要图像桥及基础边界已完成本轮自动验证。新Runtime完整装配、七类手势、完整Blob交互链与正式性能仍在后续范围，不能据S1通过宣布整体已验收。')

    live_start = page.index('<!-- FRAMEWORK-LIVE-STATUS:BEGIN -->')
    live_end = page.index('<!-- FRAMEWORK-LIVE-STATUS:END -->', live_start)
    live = page[live_start:live_end]
    if '<!-- S1-LIVE-HISTORY:BEGIN -->' in live:
        history = live.split('<!-- S1-LIVE-HISTORY:BEGIN -->', 1)[1].split('<!-- S1-LIVE-HISTORY:END -->', 1)[0]
    else:
        section = re.search(r'<section\b[^>]*>(.*)</section>\s*$', live, re.S)
        if not section:
            raise ValueError('Missing original live-status section')
        history = section.group(1)
    live = ('<!-- FRAMEWORK-LIVE-STATUS:BEGIN --><section id="implementation-live-status" class="live-status">'
            '<h2>当前实施状态：S1基础改造已闭合</h2><p>' + state + '</p><p>' + link + '</p>'
            '<p>旧变量批次未原样恢复，S2/S3尚未启动；旧多岗位流程不再执行。'
            '下方保留原FT及旧独测记录，仅供按版本追溯。</p>'
            '<details><summary>历史实施与独测记录（原状态快照，不是当前进度）</summary>'
            '<!-- S1-LIVE-HISTORY:BEGIN -->' + history + '<!-- S1-LIVE-HISTORY:END -->'
            '</details></section>')
    return render_s2_stage(page[:live_start] + live + page[live_end:])


def render_stage_overview():
    source = ROOT / 'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/blob-complete-final-review-design-input-20261025.json'
    sequence = json.loads(source.read_text(encoding='utf-8-sig'))['implementationSequence']
    if [x['id'] for x in sequence] != ['S1', 'S2', 'S3', 'S4', 'S5', 'S6']:
        raise ValueError('Approved stage sequence changed; review the plain-language overview')
    rows = [
        ['S1 基础规则', '参数、输出和图像怎样传递，旧方案怎样兼容与可靠保存。', '字段与绑定合同、旧格式迁移、图像/区域桥接和保存基础。', '主要是底层可靠性改善，不是新界面。', '实施及本轮自动验证已闭合；未代用户验收'],
        ['S2 编辑与运行', '调试、确定、取消、保存和正式运行各用哪份数据，避免相互污染。', '草稿隔离、当前确认、整体保存、变量冲突与完整轮次快照。', '试参数不误改生产；取消不撤已确定内容；保存不漏隐藏窗口。', '实施及本轮自动验证已闭合；未代用户验收'],
        ['S3 算法运行与工程拆分', '让Blob算法不依赖窗口运行，明确算法步骤、图像/区域与结果的交接及释放。', '分开运行层与WPF界面；沿用HALCON，落实分割→连通→筛选→排序→限数；统一结果集合、图像/区域/完整轮廓和资源生命期；核验插件装配与发布。', '主要改善运行与结果的正确性、结构和可维护性；界面体感仍可能有限。', '完整阶段未启动；S1已有图像桥可作基础'],
        ['S4 窗口与控件交互', '把工具箱拖入节点后的配置操作做完整、做清楚。', '按VM已证控件/模板/组合链组织窗口、输入绑定和ROI绘制/数值编辑；接通调试、确定/取消、保存重开。', '这是窗口布局、控件和区域编辑等明显体验变化的重点，形成可操作包后集中试用。', '未启动'],
        ['S5 结果显示与关联使用', '结果表、图像、下游工具是否使用同一份结果，运行久了是否正确释放与保持性能。', '统一表格/结果树/图像场景/下游消费；实现选中联动，核验空结果、失败、多轮运行、最后读者释放及受影响工具回归。', '结果可读、表图联动一致，下游可用；同时核查稳定性和性能。', '未启动'],
        ['S6 最终交付与人工验收', '把前面改造汇总为同一正式版本，处理实际反馈。', '集中修复与复测，统一Release、版本身份、操作清单和已知问题，记录真实人工反馈。', '在指定Release目录集中验收，清楚知道测什么、哪些已解决、哪些仍有限制。', '未启动；不能代用户通过'],
    ]
    s3_path = ROOT / 'docs/collaboration/S3_IMPLEMENTATION_PLAN_20261025.json'
    if s3_path.exists() and json.loads(s3_path.read_text(encoding='utf-8-sig'))['status'] in ('ACTIVE_IMPLEMENTATION','IMPLEMENTATION_AND_AUTOMATED_CHECKS_CLOSED'):
        rows[2][-1] = json.loads(s3_path.read_text(encoding='utf-8-sig')).get('stageStatus', '实施中：依赖/线程语义与中立图像桥首批通过；算法本体及装配未完成')
    s3_closed = s3_path.exists() and json.loads(s3_path.read_text(encoding='utf-8-sig'))['status'] == 'IMPLEMENTATION_AND_AUTOMATED_CHECKS_CLOSED'
    rows[3][-1] = ('当前接续阶段；' if s3_closed else 'S3闭合后开始；') + '实施完成且有明显操作变化后，集中人工测试S1—S4'
    return ('<section id="o06-stage-overview"><h3>整个Blob优化分几步？每一步解决什么？</h3>'
            '<p><b>基础规则（S1） → 编辑与运行（S2） → 算法与工程（S3） → 窗口操作（S4） → 结果联动（S5） → 最终验收（S6）</b></p>'
            + table(['阶段', '主要解决的问题', '主要改什么', '你能看到的效果', '当前状态'], rows)
            + '<p><b>当前推进顺序：</b>S3完整实施、自动验证及文档核收闭合（不等待人工验证）→S4窗口与控件交互→集中人工测试S1—S4，修复复测后阶段性测试闭合。'
            'HALCON继续使用；算法运行与工程在S3，窗口与控件重点在S4，通用结果显示与联动重点在S5。</p>'
            '<p>以上沿用原方案的六个阶段，不新增批次；本次已授权S3完成后接续S4，S5不自动启动。后续设计继续区分VM已证事实、独立设计选择和待实机确认项；'
            '详细合同、对标证据及完整验收要求保留在原方案正文。已有基础实现不等于后续完整阶段已完成。</p></section>')


def render_s2_stage(page):
    """Show the active phase beside the existing S1 summary, retaining its evidence."""
    plan_path = ROOT / 'docs/collaboration/S2_IMPLEMENTATION_PLAN_20261025.json'
    if not plan_path.exists():
        return page
    plan = json.loads(plan_path.read_text(encoding='utf-8-sig'))
    if plan['status'] not in ('ACTIVE_SCOPE_AND_SOURCE_AUDIT', 'ACTIVE_IMPLEMENTATION', 'IMPLEMENTATION_AND_AUTOMATED_CHECKS_CLOSED'):
        return page
    closed = plan['status'] == 'IMPLEMENTATION_AND_AUTOMATED_CHECKS_CLOSED'
    link = '<a href="#s2-implementation-plan">S2优化项、顺序、验证与旧变量修复关系</a>'
    state = plan.get('summaryStatus', 'S1基础实施及本轮自动验证已闭合；S2已进入范围与源码核对，产品尚未改动、S2尚未验证。')
    s3_path = ROOT / 'docs/collaboration/S3_IMPLEMENTATION_PLAN_20261025.json'
    s3 = json.loads(s3_path.read_text(encoding='utf-8-sig')) if s3_path.exists() else None
    active_s3 = s3 is not None and s3['status'] in ('ACTIVE_IMPLEMENTATION','IMPLEMENTATION_AND_AUTOMATED_CHECKS_CLOSED')
    s3_closed = active_s3 and s3['status'] == 'IMPLEMENTATION_AND_AUTOMATED_CHECKS_CLOSED'
    if s3_closed:
        page = page.replace('完整方案已批准分阶段实施；S1必要图像桥及基础边界已完成本轮自动验证。新Runtime完整装配、七类手势、完整Blob交互链与正式性能仍在后续范围，不能据S1通过宣布整体已验收。',
                            '完整方案已批准分阶段实施；S1—S3实施及本轮自动验证已闭合，Runtime与可选编辑器的真实装配已验证。S4完整窗口/ROI交互、S5通用结果及正式性能仍未完成；S1—S4人工测试待S4形成明显体验变化后集中进行。')
    if active_s3:
        state = s3['current']
    top = ('<div class="br-note" id="o06-blob-final-review"><h3>当前做到哪里？</h3><p><b>' + state + '</b></p>'
           '<p>S2解决“编辑、确认、保存和运行各用正确的配置”，不等于完整Blob新界面已经交付。'
           '底层变化先自动验证，有明显操作变化后集中人工验证。当前仅本对话推进，不恢复旧变量批次。</p>'
           '<p><a href="#o06-stage-overview">先看S1—S6各阶段解决什么</a> · ' + link + ' · <a href="#blob-verification-steps">S1实施总结与验证</a></p>'
           + render_stage_overview()
           + ('<section id="s3-implementation-status"><h3>' + ('S3实施及自动验证总结' if s3_closed else 'S3现在改到哪里？') + '</h3>'
              + (table(['优化项', '已实现的效果', '还缺什么'], s3['implementationSummary']) if s3.get('implementationSummary') else '')
              + '<details><summary>VM依据、自动验证明细和未确认边界</summary>'
              + (table(['VM已证事实', '本方案怎样采用', '证据边界'], s3['vmComparison']) if s3.get('vmComparison') else '')
              + table(['内容', '实际结果'], [[x['name'], x['result']] for x in s3.get('checks', [])]) + '</details>'
              + '<p><b>' + ('下一步：' if s3_closed else '还没完成：') + '</b>' + escape(s3['next']) + '</p>'
              + '<p>' + escape(s3['manual']) + '</p><p>本轮不原样恢复旧变量批次。'
              'VM已有材料只支持已证范围，不把本方HALCON通过说成VM内部算法已确认。</p></section>' if active_s3 else '') + '</div>')
    page = re.sub(r'<!-- O06-BLOB-FINAL-REVIEW:BEGIN -->.*?<!-- O06-BLOB-FINAL-REVIEW:END -->',
                  lambda _: '<!-- O06-BLOB-FINAL-REVIEW:BEGIN -->' + top + '<!-- O06-BLOB-FINAL-REVIEW:END -->',
                  page, count=1, flags=re.S)
    body = ('<!-- S2-CURRENT-PLAN:BEGIN --><div id="s2-implementation-plan"><h4>' + escape(plan['title']) + '</h4>'
            '<p><b>实际进度：</b>' + escape(plan['current']) + '</p>'
            '<p>编辑草稿 → 调试试算（不发布） → 确定/Apply当前范围；保存则统一准备全部登记范围。'
            '正式运行读取已发布快照；连续执行在下一完整轮采用显式发布的新配置。</p>'
            + table(['优化项', '具体调整', '改善效果' if closed else '预期效果'], plan['items'])
            + ('<h4>本轮实际验证</h4>' + table(['检查', '结果和边界'], plan['verification']) if plan.get('verification') else '')
            + '<details><summary>实施顺序与完整验证标准</summary><h4>实施顺序与检查点</h4>' + table(['顺序', '实施内容', '怎样进入下一步'], plan['order'])
            + '<h4>通过标准：要实际验证这些行为</h4>' + table(['场景', '预期结果'], plan['checks'])
            + '</details>'
            + '<h4>与旧变量修复是什么关系？</h4><p>' + escape(plan['oldVariableRelation']) + '</p>'
            + '<p><b>范围边界：</b>' + escape(plan['excluded']) + '</p>'
            + '<p><b>人工验证：</b>' + escape(plan['manual']) + '</p>'
            + '<p><b>持续推进及汇报：</b>' + escape(plan['reporting']) + '</p>'
            + '<h4>VM对标证据到哪里，哪些仍未确认？</h4>' + table(['性质', '实际边界'], plan.get('evidenceBoundary', []))
            + '<details><summary>批准依据与当前源码证据（保留详细位置）</summary>'
            + table(['证据', '文件位置', '能证明什么'], plan['evidence'])
            + '</details></div><!-- S2-CURRENT-PLAN:END -->')
    # The S1 renderer recreates the original summary region, so no parallel latest chapter accumulates.
    page = re.sub(r'<!-- S2-CURRENT-PLAN:BEGIN -->.*?<!-- S2-CURRENT-PLAN:END -->', '', page, flags=re.S)
    anchor = '<h3 id="blob-verification-steps">⑤ S1实施总结、优化效果与验证安排</h3>'
    page = page.replace(anchor, anchor + body, 1)
    live_head = '<h2>当前实施状态：S1基础改造已闭合</h2>'
    start = page.index(live_head, page.index('<!-- FRAMEWORK-LIVE-STATUS:BEGIN -->'))
    end = page.index('<details><summary>历史实施与独测记录', start)
    live_title = '当前实施状态：S2实施与本轮自动验证已闭合' if closed else '当前实施状态：S1已闭合，S2实施与验证中'
    if active_s3:
        live_title = '当前实施状态：S3实施及自动验证已闭合，接续S4' if s3_closed else '当前实施状态：S1/S2已闭合，S3实施中'
    page = page[:start] + ('<h2>' + live_title + '</h2><p>' + state + '</p><p>' + link + '</p>'
                           '<p>旧变量批次未原样恢复；历史多岗位记录仅用于追溯。</p>') + page[end:]
    if active_s3 and s3.get('engineeringRows'):
        anchor = '<h6 id="o06-engineering-class-map">'
        start = page.index(anchor)
        first_table = page.index('<table', start)
        first_end = page.index('</table>', first_table) + len('</table>')
        if page[first_end:].startswith('</div>'):
            first_end += len('</div>')
        if page[first_end:].startswith('<pre>'):
            first_end = page.index('</pre>', first_end) + len('</pre>')
        replacement = (anchor + '工程B：运行、兼容与可选编辑器的实际组织</h6>'
                       '<p>S3已实施部分如下；共享控件交互仍属S4，不因搬入Wpf工程就算已重做。'
                       '拆工程本身不提高速度或精度，收益是算法可脱离窗口、界面可替换、只有一个算法实现。</p>'
                       + table(['对象', '当前工程与职责', '实际验证 / 剩余边界'], s3['engineeringRows'])
                       + '<pre>' + escape(s3['engineeringTree']) + '</pre>')
        page = page[:start] + replacement + page[first_end:]
        second_start = page.index('<table', start + len(replacement))
        second_end = page.index('</table>', second_start) + len('</table>')
        if page[second_end:].startswith('</div>'):
            second_end += len('</div>')
        wrapper = page.rfind('<div class="br-scroll">', start + len(replacement), second_start)
        if wrapper >= 0:
            second_start = wrapper
        page = page[:second_start] + table(['对照点', 'VM已证结构', '本方实际落地 / 未完成'], s3['engineeringComparison']) + page[second_end:]
        page = page.replace('以上算法/输出/工程调整均为本轮待审建议，现有代码与有限验收事实另列，未进行产品修改。',
                            'S3实际工程状态以本节和首屏进度为准；S4/S5仍待实施，原候选及源码快照保留在历史证据中。')
    return page


def render_s4_stage(page):
    path = ROOT / 'docs/collaboration/S4_IMPLEMENTATION_PLAN_20261026.json'
    if not path.exists():
        return page
    s4 = json.loads(path.read_text(encoding='utf-8-sig'))
    if s4['status'] != 'ACTIVE_IMPLEMENTATION':
        return page
    state = escape(s4['current'])
    page = re.sub(r'(<div class="br-note" id="o06-blob-final-review"><h3>当前做到哪里？</h3><p><b>).*?(</b></p>)',
                  lambda m: m[1] + state + m[2], page, count=1, flags=re.S)
    page = re.sub(r'<!-- S4-CURRENT-STATUS:BEGIN -->.*?<!-- S4-CURRENT-STATUS:END -->', '', page, flags=re.S)
    block = ('<!-- S4-CURRENT-STATUS:BEGIN --><section id="s4-implementation-status"><h3>S4现在改到哪里？</h3>'
             + table(['优化项', '现在已实现', '验证与剩余'], s4['rows'])
             + '<p><b>接下来：</b>' + escape(s4['next']) + '</p><p>' + escape(s4['estimate']) + '</p>'
             + '<details><summary>VM对应源、工程组织与实际检查</summary>'
             + table(['原清单对象', 'VM对应模板或组合链', '本方实现与边界'], s4['vmMappings'])
             + '<p>' + '<br>'.join(escape(item) for item in s4['projectOrganization']) + '</p>'
             + table(['检查', '实际结果'], [[item['name'], item['result']] for item in s4['checks']])
             + '<p>' + escape(s4['productIdentity']) + '</p></details></section><!-- S4-CURRENT-STATUS:END -->')
    page = page.replace('<section id="s3-implementation-status">', block + '<section id="s3-implementation-status">', 1)
    overview_start = page.index('<section id="o06-stage-overview">')
    overview_end = page.index('</section>', overview_start)
    overview = page[overview_start:overview_end]
    overview = re.sub(r'(<tr><td>S4 窗口与控件交互</td>.*?<td>)([^<]*)(</td></tr>)',
                      lambda m: m[1] + escape(s4['summaryStatus']) + m[3], overview, count=1, flags=re.S)
    page = page[:overview_start] + overview + page[overview_end:]
    live_start = page.index('<section id="implementation-live-status"')
    live_end = page.index('<details><summary>历史实施与独测记录', live_start)
    page = page[:live_start] + re.sub(r'<h2>.*?</h2><p>.*?</p>',
        lambda _: '<h2>当前实施状态：S1—S3自动闭合，S4实施中</h2><p>' + state + '</p>',
        page[live_start:live_end], count=1, flags=re.S) + page[live_end:]
    old = ('Plugin.Wpf真实存在，但目前只有PluginFoundation样例消费者，未进入主slnx且无src/UI/tools生产消费者；'
           'OutDir指向artifacts/validation/plugin-foundation-01/bin；ParameterEditorControl自称Minimal editor contract consumer，'
           '并非生产编辑器替代，当前没有本表31类实现或Generic.xaml/ThemeInfo。')
    page = page.replace(old, 'S4已将Plugin.Wpf接入主slnx和UI，Release输出进入正式目录；Fields现有DeclaredFieldEditor、DeclaredFieldsView与ReferenceSelectionControl，仅依赖Contracts。原ParameterEditorControl仍为样例，不充当生产默认页。当前字段/绑定已有实际验证，ROI、专业页与其余消费者尚未完成迁移；旧SDK控件不能据此全局删除。')
    page = page.replace('Plugin.Wpf正式生产接入、设计器/资源/输出迁移仍待批准。Plugin.Wpf本身目前只在PluginFoundation样例验证接入，不是生产控件层。',
                        '字段与绑定的生产接入见本页S4实际进度；设计时编译已做，实际VS设计器未验证。ROI及其余控件、资源和消费者迁移仍待实施核收。')
    page = page.replace('目标项目位置均为候选，不表示批准或现行实施。',
                        '原始候选与历史工程事实保留追溯；S4已落地部分直接标在对应行，其余尚未实施，不因字段层接入算31类整体完成。')
    page = page.replace('Blob完整方案实施准入（设计决策已闭口，全部未执行）',
                        '实施准入与历史门（设计条件保留；S1—S3自动闭合，S4实施中）')
    updates = {
        'configsetting': 'S4当前：原默认页改用Plugin.Wpf/Fields/DeclaredFieldEditor和DeclaredFieldsView，按原Port类型编辑109项常量，BlobConfigurationFields仅适配本工具模型；来源为本行VM类型模板。完整图像/ROI特殊字段及专业页替换仍未完成。',
        'parambinding': 'S4当前：来源选择由Plugin.Wpf/Fields/ReferenceSelectionControl承接，UI宿主组织窗口及草稿；已实测声明成员、显式/绑定序号、真实执行与保存重开。本行VM订阅组合链只作职责对标，不照搬字符串绑定。',
        'parambindingbase': 'S4当前：不再为Blob默认字段新建旧式万能绑定基类；局部字段保存非法原文，宿主登记草稿与验证，执行层编译唯一声明。旧SDK基类的其他消费者仍在，未宣称已全局删除。',
    }
    for key, note in updates.items():
        pattern = r'(<tr id="o06-control-' + key + r'"[^>]*>)(.*?)(</tr>)'
        def update_row(match):
            cell_index = -1
            def update_cell(cell):
                nonlocal cell_index
                cell_index += 1
                if cell_index != 3:
                    return cell[0]
                body = re.sub(r'<!-- S4-ROW:BEGIN -->.*?<!-- S4-ROW:END -->', '', cell[2], flags=re.S)
                return cell[1] + '<!-- S4-ROW:BEGIN --><p><b>' + escape(note) + '</b></p><!-- S4-ROW:END -->' + body + cell[3]
            return match[1] + re.sub(r'(<td\b[^>]*>)(.*?)(</td>)', update_cell, match[2], flags=re.S) + match[3]
        page, count = re.subn(pattern, update_row, page, count=1, flags=re.S)
        if count != 1:
            raise ValueError('Missing original control row: ' + key)
    return page


def render_document(page):
    return render_s4_stage(render_s1_stage(page))


def main():
    original = PAGE.read_text(encoding='utf-8-sig')
    page = render_document(original)
    if render_document(page) != page:
        raise ValueError('S1 document update is not idempotent')
    class Anchors(HTMLParser):
        def __init__(self):
            super().__init__()
            self.ids = []

        def handle_starttag(self, tag, attrs):
            self.ids.extend(value for name, value in attrs if name == 'id')

    parsed = Anchors()
    parsed.feed(page)
    ids = parsed.ids
    if len(ids) != len(set(ids)):
        raise ValueError('Duplicate document anchors')
    for key in ('o06-blob-final-review', 'blob-verification-steps', 'implementation-live-status'):
        if ids.count(key) != 1:
            raise ValueError('Missing primary anchor: ' + key)
    PAGE.write_text(page, encoding='utf-8', newline='')
    print(json.dumps({'updated': str(PAGE.relative_to(ROOT)), 'anchors': len(ids),
                      'idempotent': True, 'sha256': hashlib.sha256(page.encode()).hexdigest()}, ensure_ascii=False))


if __name__ == '__main__':
    main()
