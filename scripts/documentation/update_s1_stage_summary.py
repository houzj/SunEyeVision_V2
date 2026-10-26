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
    if s4['status'] not in ('ACTIVE_IMPLEMENTATION', 'READY_FOR_MANUAL', 'UI_REVIEW_REQUIRED'):
        return page
    ready = s4['status'] == 'READY_FOR_MANUAL'
    review = s4['status'] == 'UI_REVIEW_REQUIRED'
    state = escape(s4['current'])
    page = re.sub(r'(<div class="br-note" id="o06-blob-final-review"><h3>当前做到哪里？</h3><p><b>).*?(</b></p>)',
                  lambda m: m[1] + state + m[2], page, count=1, flags=re.S)
    page = re.sub(r'<!-- S4-CURRENT-STATUS:BEGIN -->.*?<!-- S4-CURRENT-STATUS:END -->', '', page, flags=re.S)
    block = ('<!-- S4-CURRENT-STATUS:BEGIN --><section id="s4-implementation-status"><h3>S4实施和试用状态</h3>'
             + '<p><b>' + state + '</b></p>'
             + ('<p><a href="optimization-o06-ui-preview.html" style="display:inline-block;padding:9px 16px;border:1px solid #d9a36a;border-radius:4px;color:#edc494">打开HTML方案预览 ↗</a>　HTML仅供方案对照，WPF实际实施与验证见下表；可切换三页、分割模式和明暗主题，下方有参数/输出及完整组装链。HTML自动渲染受本机file协议策略限制，不能当作产品视觉验收。</p>' if review and 'preview' in s4['uiReview'] else '')
             + ('<p><a href="#o06-current-control-mapping"><b>直接审核31类新职责、VM源、去留与文件组织</b></a> · 8项提取复用 / 15项重组 / 8项旧类退出；完整交互见同段下方。</p>' if (ROOT/'docs/collaboration/evidence/O06_CONTROL_REORGANIZATION_20261026.json').exists() else '')
             + table(['优化项', '现在已实现', '验证与剩余'], s4['rows'])
             + (('<h4>S4哪些重做，哪些复用（' + ('已批准，实施中' if s4.get('implementationApproved') else '推荐，待整体审核') + '）</h4>'
                  + ('<p><b>'+escape(s4['uiReview']['uiArchitectureDecision']['principle'])+'</b></p>'+table(['层次','现有基础','明确处理'],s4['uiReview']['uiArchitectureDecision']['rows']) if s4['uiReview'].get('uiArchitectureDecision') else '')
                  + table(['处理','本方对象','VM依据与本方差距','功能承接及工程调整'],s4['uiReview']['decisions'])
                  + ('<h4 id="o06-algorithm-ui-differences">算法职责、参数与输出怎样对应三页</h4>'
                     + '<p>已实现Blob单阈值、双阈值和不二值化；内置预处理/形态学已移出，均衡化交ImageFilter，严格二值形态学交Morphology，再由Blob不二值化接入。实际验证与剩余见本段当前检查表。VM原件已有不二值化声明，但不能据此推断其原生公式或数值等价；不能挪用ROI端口。</p>'
                     + table(['差异','双方实际证据','推荐取舍','UI承接','未确认/实施边界'],s4['uiReview']['algorithmDifferences'])
                     + '<h4>窗口完整组装链：VM与当前实现并排</h4>'
                     + table(['环节','VM静态链','本方当前链','推荐调整'],s4['uiReview']['chainComparison'])
                     + '<p>VM原件模式存在不证明native公式；本方窗口链有源码不证明外观合格。下表核收源码/实机/统筹的实际回件，不把三种状态合成完成百分比。</p>'
                     + table(['核收对象','实际成果与限制'],[[label,s4['uiReview']['threeRoleReceipt'][key]] for key,label in (('source','源码岗'),('runtime','实机岗'),('coordinator','逆向统筹'))])
                     + (table(['用户新截图补证','画面直接支持的结论','本方处理'],s4['uiReview']['userScreenshotFindings']) if s4['uiReview'].get('userScreenshotFindings') else '')
                     if 'algorithmDifferences' in s4['uiReview'] else '')
                  + ('<h4 id="o06-control-style-comparison">全部可见控件逐项对照</h4><p>'+escape(s4['uiReview']['controlStyleScope'])+'</p>'+table(['控件','VM定位','实际依据','推荐处理','未确认边界'],s4['uiReview']['controlStyleComparison']) if s4['uiReview'].get('controlStyleComparison') else '')
                  + ('<details><summary>查看控件样式的完整源码定位</summary>'+table(['源码路径','行','模板或定位键'],[[q['path'],str(q['line']),q['key']] for q in json.loads((ROOT / 'docs/collaboration/evidence/S4_UI_REVIEW_20261026.json').read_text(encoding='utf-8-sig')).get('controlStyleSources',[])])+ '</details>' if s4['uiReview'].get('controlStyleComparison') else '')
                  + ('<details><summary>输入源已有本地还原，如何适配</summary><p>'+escape(s4['uiReview']['inputSourceCorrection']['vmChain'])+'</p><p>'+escape(s4['uiReview']['inputSourceCorrection']['localRestoration'])+'</p><p>'+escape(s4['uiReview']['inputSourceCorrection']['engineering'])+'</p></details>' if s4['uiReview'].get('inputSourceCorrection') else '')
                  + ('<h4>参数交互与开关布局</h4>'+table(['状态','依据与处理'],[[label,s4['uiReview']['subscriptionModeReview'][key]] for key,label in (('facts','数值与订阅'),('range','单值与范围滑条'),('focus','焦点与收起'),('layout','开关与参数排布'),('proposal','本方推荐行为'),('limits','仍需验证'))]) if s4['uiReview'].get('subscriptionModeReview') else '')
                  + '<details><summary>查看VM实际组装链、六项差距与视觉核收标准</summary>'
                  + table(['链路','实际消费关系','证据边界'],s4['uiReview']['assemblyChain'])
                  + table(['对照项','VM实际依据','当前实现与差距','本轮处理'],s4['uiReview']['comparison'])
                  + table(['核收门','检查范围','通过依据'],s4['uiReview']['acceptance'])
                  + '<p>'+escape(s4['uiReview']['boundary'])+'</p></details>') if review and 'decisions' in s4['uiReview'] else '')
             + ('<details><summary>查看当前真实页面</summary><p>正式Release的自动检查宿主渲染，尚未人工验收。</p><img alt="当前Blob默认编辑页" style="max-width:100%;height:auto" src="'
                + quote(os.path.relpath(ROOT / s4['uiPreview']['path'], PAGE.parent).replace('\\', '/')) + '"></details>' if s4.get('uiPreview') else '')
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
        lambda _: '<h2>' + ('当前状态：S4外观未达标，先补UI对标' if review else '当前状态：S1—S4集中试用，人工结果待反馈' if ready else '当前实施状态：S1—S3自动闭合，S4实施中') + '</h2><p>' + state + '</p>',
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
        'configsetting': 'S4当前：正常Blob入口已改用Plugin.Wpf/Fields声明字段；109项常量与图像、位置、区域来源分开呈现，沿用原117项合同。旧专业调试页及其转换器已删除，整链尚未核收；不能把新默认页局部通过算成完整交付。',
        'parambinding': 'S4当前：来源选择由Plugin.Wpf/Fields/ReferenceSelectionControl承接，UI宿主组织窗口及草稿；已实测声明成员、显式/绑定序号、真实执行与保存重开。本行VM订阅组合链只作职责对标，不照搬字符串绑定。',
        'parambindingbase': 'S4当前：不再为Blob默认字段新建旧式万能绑定基类；局部字段保存非法原文，宿主登记草稿与验证，执行层编译唯一声明。旧SDK基类的其他消费者仍在，未宣称已全局删除。',
        'roieditorcontrol': 'S4当前：Plugin.Wpf/Regions/RegionGeometryEditor与RoiGeometryEditing承接七类几何的数值、选择及编辑意图；只依赖Contracts。VM对应RoiSelectControl.xaml:15–16,23–79,101–121及roiselectdatatemplate.xaml:27–35,76–132的形状/数值模板链；不声称每个鼠标手势已经VM实机核等价。',
        'roieditormanagercontrol': 'S4当前：正常Blob入口改由UI/Services/Editing/Regions/RoiViewportEditScope独占图像交互范围，旧Blob页与Manager接线已删除。共享字段只接收受限IRoiEditViewport，不能清主结果或持有全局Canvas。VM对应BoxROIStack.xaml:15–115及ROI模板→宿主交互链。本地草稿、换帧取消、关闭只释放自身已有定向验证；其他工具旧Manager消费者尚存，未全局删除。',
        'blobanalysistooldebugcontrol': 'S4当前：旧Wpf/Views/BlobAnalysisToolDebugControl.xaml及后台、专用BlobAnalysisConverters.cs已删除；不再沿旧整页重构。正常打开入口由UI/Services/Editing/Nodes/DeclaredToolConfigurationControl及其Sources/RegionSources/Regions分部组装，Plugin.Wpf只含局部字段和几何编辑，事务/交互归宿主。VM对标沿本行控件→模板→宿主组装证据。Wpf组件保留现行BlobDataAnalysisControl只读结果视图，S5未启动。',
        'roisubscribepanel': 'S4当前：RegionSources宿主适配按检测/屏蔽与几何/像素Region分别配置，几何为独立订阅项，切换不删本地底稿。VM对应ROISelecter模板与ButtonSelecter订阅链；现行SDK存储与运行校验归宿主，不能把模板相似当成底层实现相同。',
        'imagesourceselector': 'S4当前：Plugin.Wpf/Fields/SourceSelectionField仅展示宿主提供的声明候选；UI/Sources适配持久引用、当前作用域和显式集合序号，禁止猜默认来源。VM对应ButtonSelecter_sub:255→SubTreeView:296–302选择组合链。现行SDK矩阵缺完整中立空间元数据，保留明确适配而不伪造坐标标识；同帧校验继续生效。',
        'blobdataanalysiscontrol': 'S4关联修复：保留现有专用表格和列；Wpf/Presentation/BlobAnalysisRow将同一Runtime声明记录投影为无资源句柄的标量行，不再强转隔离加载的SDK门面结果，Wpf工程改引Runtime而非门面项目。主窗口真实调试、表/摘要、选中高亮及失败清理已有连续链验证。VM当前结果声明/宿主链作为本行对标证据；S5通用声明结果平台未启动。',
    }
    if ready:
        updates['configsetting'] = updates['configsetting'].replace('整链尚未核收；不能把新默认页局部通过算成完整交付。', '真实宿主连续链和同提交自动回归已核收，人工体验仍待用户反馈。')
    if review:
        for key in ('configsetting','roieditorcontrol','blobanalysistooldebugcontrol'):
            updates[key] += ' UI复核：按批准方案实施三页、同排字段、样式弹层及ROI条件组合；当前真实实现和新产物验证以本页实施表为准，旧五页外观问题属于历史。VM实机截图是外观验收参考，源码支持职责和状态依据；功能通过不代外观或整链。'
        updates['configsetting'] += ' 推荐重做正常页渲染；VM实际主链为ParamUserControl.DisplayType→UserTempConverter→ParamUserControlDataTemplate，不是PointF专用选择器。拆出并复用已有输入校验/非法原文/绑定行为，不默认保留DeclaredFieldEditor整类。'
        updates['imagesourceselector'] += ' UI推荐重做同排来源/订阅模板；复用候选查询、稳定引用、索引和类型校验，不原样保留应用/清除/撤销按钮排布。'
        updates['roisubscribepanel'] += ' UI推荐纳入绘制/继承及折叠几何组合；复用模式/引用和底稿，不另立第二参数存储。'
        updates['blobdataanalysiscontrol'] += ' 与结果显示配置页区分：本轮重做的是配置对象列表及文本模板；现行只读结果表保留并回归，不声称已做S5通用化。'
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
    if ready or review:
        trial=s4['trial']
        release_path=escape(trial['program'])
        sample_path=escape(trial['sample'])
        trial_intro=('<div class="br-note" id="o06-blob-final-review"><h3>'+('S4外观未达标，先补UI对标' if review else 'S1—S4可以集中试用了')+'</h3>'
                     '<p><b>'+state+'</b></p><p>固定程序：<code>'+release_path+'</code></p>'
                     '<p><a href="#s4-implementation-status">直接看当前差距与处理范围</a>。功能自动验证不代表UI达标；人工验收尚未通过。</p></div>')
        # The summary marker also owns the stage overview and S3/S4 sections.
        # Replacing it with the short intro alone silently deletes those deliverables.
        overview_match=re.search(r'<section id="o06-stage-overview">.*?</section>',page,re.S)
        s3_match=re.search(r'<section id="s3-implementation-status">.*?</section>',page,re.S)
        if overview_match is None or s3_match is None:
            raise ValueError('Missing stage overview or S3 section before S4 summary replacement')
        trial_intro=trial_intro[:-6]+overview_match.group()+block+s3_match.group()+'</div>'
        page=re.sub(r'<!-- O06-BLOB-FINAL-REVIEW:BEGIN -->.*?<!-- O06-BLOB-FINAL-REVIEW:END -->',
                    lambda _: '<!-- O06-BLOB-FINAL-REVIEW:BEGIN -->\n'+trial_intro+'\n<!-- O06-BLOB-FINAL-REVIEW:END -->',page,count=1,flags=re.S)
        a=page.index('<h3 id="blob-verification-steps">')
        b=page.index('<h3 id="blob-optimization-list">',a)
        historical=page[a:b].replace('<h3 id="blob-verification-steps">','<h4>',1).replace('</h3>','</h4>',1)
        manual=('<h3 id="blob-verification-steps">⑤ S1—S4集中试用：改了什么、怎么验证</h3>'
                '<p><b>版本：</b>'+escape(trial['version'])+'，产品提交 <code>'+escape(trial['productCommit'][:8])+'</code>。'
                '已完成同提交正式Release构建、真实宿主连续链和关联自动回归；等待你的实际试用反馈。</p>'
                '<p><b>启动程序：</b><code>'+release_path+'</code><br><b>打开示例方案：</b><code>'+sample_path+'</code></p>'
                '<p>'+escape(trial['example'])+'</p>'
                +table(['要验证什么','怎么操作','预期效果'],trial['checklist'])
                +'<p><b>反馈方式：</b>告诉我“哪一步、做了什么、实际看到了什么”；异常可附截图。'
                '目前人工状态为“待反馈”，不会代你填写通过。'+escape(trial['limitations'])+'</p>'
                '<details><summary>查看S1早期实施与原验证记录（历史，不是当前待办）</summary>'+historical+'</details>\n')
        page=page[:a]+manual+page[b:]
        if review:
            page=page.replace('<p>固定程序：<code>'+release_path+'</code></p>',
                              '<p><a href="optimization-o06-ui-preview.html"><b>先打开HTML预览，审核界面与职责调整 ↗</b></a>　产品程序本轮不改，原功能检查保留。</p>')
            page=page.replace('⑤ S1—S4集中试用：改了什么、怎么验证','⑤ 人工验证安排：先补齐UI，再集中验证')
            page=page.replace('已完成同提交正式Release构建、真实宿主连续链和关联自动回归；等待你的实际试用反馈。',
                              '同提交功能链与关联自动检查已通过，但当前外观被用户明确指出不符合要求。先补齐VM窗口布局、模板组合和样式对标；下列功能清单保留供修正后集中验证，现在不催用户测试。')
            page=page.replace('目前人工状态为“待反馈”，不会代你填写通过。','目前UI状态为“未达标、待修正”，不是只等用户验收；不会代你填写通过。')
        page=re.sub(r'<div class="br-note"><b>当前S1状态：</b>.*?</div>',
                    '<div class="br-note"><b>当前总状态：</b>'+state+' <a href="#blob-verification-steps">集中试用与反馈</a>。</div>',page,flags=re.S)
        page=page.replace('架构方案与阶段进度 · S1实施及本轮自动验证已闭合 · 完整Blob交互链仍待后续实施',
                          '架构方案与阶段进度 · S1—S4实施及自动验证完成 · 集中人工试用待反馈')
        if review:
            page=re.sub(r'<div class="br-note"><b>当前总状态：</b>.*?</div>',lambda _: '<div class="br-note"><b>当前总状态：</b>'+state+' <a href="#s4-implementation-status">UI差距与修正范围</a>。</div>',page,flags=re.S)
            page=page.replace('架构方案与阶段进度 · S1—S4实施及自动验证完成 · 集中人工试用待反馈','架构方案与阶段进度 · S1—S3历史自动核收 · S4优化实施中，当前结果见验证表')
            page=page.replace('S4完整窗口/ROI交互、S5通用结果及正式性能仍未完成；S1—S4人工测试待S4形成明显体验变化后集中进行。','S4窗口、算法与ROI正在按批准方案优化，当前完整链和外观结论以本页验证表为准；S5通用结果及正式性能尚未实施。')
            page=page.replace('S4/S5仍待实施，原候选及源码快照保留在历史证据中。','S4优化实施中、UI对标未达标，S5尚未实施；原候选及源码快照保留在历史证据中。')
            current_tree='''tools/SunEyeVision.BlobAnalysis/（实际文件）
├─ BlobAnalysisTool / BlobSdkAdapter / BlobInputAdapter：现行SDK入口适配
├─ BlobInputContract / BlobConfigurationFields / 参数、结果及输出合同
├─ Runtime/：BlobDefinition / BlobSettings / BlobSchema / BlobSession* / BlobOutput
└─ Wpf/：可选只读结果扩展
   ├─ BlobEditorFactory.cs
   ├─ Views/BlobDataAnalysisControl.xaml(.cs)
   └─ Presentation/BlobAnalysisRow.cs
已删除：Views/BlobAnalysisToolDebugControl.xaml(.cs)、Converters/BlobAnalysisConverters.cs
宿主→现行入口适配→唯一Runtime算法；Runtime不反向依赖Wpf/宿主。

'''+s4['uiReview']['engineeringBefore']+'\n\n'+s4['uiReview']['engineeringAfter']
            marker='<!-- O06-ENGINEERING-CLASS-MAP:END -->'
            end=page.index(marker);start=page.rfind('<pre',0,end)
            page=page[:start]+'<pre>'+escape(current_tree)+'</pre>'+page[end:]
            page=page.replace('现行设置/结果通过SDK适配承接，原界面移到Wpf；S4完成编辑交互，S5通用结果呈现不自动启动。',
                              '运行内核与可选结果Wpf已拆分；旧参数整页已删除。S4正在优化，当前整链结果见验证表，UI尚未整体达标；S5未启动。')
    return page



def render_control_architecture(page):
    path=ROOT / 'docs/collaboration/evidence/O06_CONTROL_REORGANIZATION_20261026.json'
    if not path.exists():
        return page
    data=json.loads(path.read_text(encoding='utf-8-sig'))
    rows=data['rows']
    if len(rows)!=31 or len({row['id'] for row in rows})!=31:
        raise ValueError('31 control records must be unique')
    from urllib.parse import quote
    def vm_ref(key):
        item=data['sources'][key]
        return '<a href="file:///'+quote(item['path'].replace('\\','/'),safe='/:')+'">'+escape(item['path'].replace('\\','/').split('/')[-1])+':'+str(item['line'])+'</a> · '+escape(item['key'])
    body=[]
    for group in data['groups']:
        group_rows=[r for r in rows if r['group']==group]
        body.append('<tr class="br-group"><th colspan="5">'+escape(group)+'（'+str(len(group_rows))+'项）</th></tr>')
        for row in group_rows:
            paths='<br>'.join('<code>'+escape(p)+'</code>' for p in row['currentFiles']) or '旧文件已删除；原路径见历史记录。'
            actual='<br>'.join('<code>'+escape(p)+'</code>' for p in row.get('actualFiles',[]))
            implementation=('<p><b>实际承接文件：</b><br>'+actual+'</p>' if actual else '<p>尚未落地为新文件。</p>')
            implementation+='<p><b>旧消费者：</b>'+escape(row.get('consumerState','迁移状态待逐项核收，不能据词法提及删除。'))+'</p>'
            mentions=row['staticMentions']
            consumers='<details><summary>静态提及/消费者候选 '+str(len(mentions))+'处</summary><p>'+ '<br>'.join(escape(p) for p in mentions)+'</p><small>含声明/类型提及，不等于每处均为实际运行调用；迁移时逐处核收。</small></details>'
            cells=[
                '<code>'+escape(row['name'])+'</code><p>'+escape(row['responsibility'])+'</p>',
                '<b>当前状态：</b>'+escape(row['currentState'])+implementation+'<details><summary>原文件位置</summary>'+paths+'</details>'+consumers,
                '<b>'+escape(row['evidenceKind'])+'</b><p>'+'<br>'.join(vm_ref(k) for k in row['sourceRefs'])+'</p><small>'+escape(row['limits'])+'</small>',
                '<b>'+escape(row['action'])+'</b><p>提取/承接：'+escape(row['reuse'])+'</p><p>退出/剥离：'+escape(row['retire'])+'</p><p>原设计目标：<code>'+escape(row['target'])+'</code></p>',
                '<a href="#o06-control-interactions">'+escape(row['interaction'])+' 交互与验收</a><p>先迁消费者，再核资源、发布与关闭释放，最后退出旧文件。已批准持续实施；实际进度见当前实施表，未迁完不算删除完成。</p>'
            ]
            body.append('<tr id="o06-control-'+row['id']+'" data-control-name="'+escape(row['name'])+'" data-control-group="'+escape(row['group'])+'">'+''.join('<td>'+v+'</td>' for v in cells)+'</tr>')
    first=page.index('id="o06-control-labeledcontrol"')
    start=page.rfind('<table',0,first)
    end=page.index('</table>',page.index('id="o06-control-toolicon"',first))+len('</table>')
    # Old and new group ordering end on different records: include the last row of the table.
    end=page.index('</table>',first)+len('</table>')
    new='<table><thead><tr><th>对象与新职责</th><th>当前文件与消费者</th><th>VM对应控件/模板/组合链</th><th>去留、功能承接与文件组织</th><th>交互与退出门</th></tr></thead><tbody>'+''.join(body)+'</tbody></table>'
    page=page[:start]+new+page[end:]
    page=page.replace('31类对象唯一主记录：按证据形态分组','31类控件唯一主记录：按新架构职责分组')
    a=page.index('id="o06-current-control-mapping"');b=page.index('<div id="o06-control-migration-gate"',a)
    intro_end=page.index('</h5>',a)+len('</h5>')
    intro=('<p><b>8项提取复用、15项重组、8项旧类退出。</b>不是重新写31个控件，也不是将旧SDK目录整体搬到Wpf。按基础输入、字段模板、ROI交互、宿主装配、工具视图、图标资源分六组；每行单列VM来源形态。删除旧类后原功能由本行目标承接，不因无同名CLR就判功能无源。确认全局无源0项。</p>'
           '<p>现有草稿/执行/关闭链作为基础；数值/范围等提取复用，绑定和画布职责按新合同拆分。完整历史不丢：<a href="../../../../../../../collaboration/history/O06_CONTROL_RECORDS_BEFORE_REORGANIZATION_20261026.json">原31行来源、候选与历史记录</a>；<a href="../../../../../../../collaboration/evidence/O06_CONTROL_REORGANIZATION_20261026.json">当前逐行来源与完整文件清单</a>。以下为已批准的目标职责和去留，正在分项实施；当前成果见同页实施表，不把目标文件当成已完成迁移。</p>')
    page=page[:intro_end]+intro+page[b:]
    page=page.replace('最终31项分组为4控件/8模板/19组合链/0待核/0确认无源；四项归类以v2为准，局部未知留在对应行。','当前31项按六组职责组织，VM证据形态单列；旧4控件/8模板/19组合链为历史分组，不能当工程分层。局部运行未知不冒充已确认。')
    page=re.sub(r'<div id="o06-control-migration-gate".*?</div>', '<div id="o06-control-migration-gate" class="br-note"><b>工程与迁移边界：</b>旧Plugin.SDK仍含WPF与运行依赖，不能视为中立合同。Plugin.Wpf当前仅依赖Contracts且已进入主构建，已提取部分基础输入并接入字段模板和主题，旧消费者迁移仍在实施；UI宿主保留候选、窗口、ROI作用域与调度，工具Definition/Runtime不得依赖UI宿主。资源需统一Generic.xaml/ThemeInfo及构建发布注册，不能只复制cs。<p>'+escape(data['deletionGate'])+'</p><p>迁移正在实施，旧类型须完成消费者承接和回归再删除；当前文件、静态提及与VM源码行号逐行列出。旧工程候选与取证路径保存在本表历史记录，当前决策以本表为准。</p></div>',page,count=1,flags=re.S)
    # Original disposition location becomes navigation to the same authoritative rows.
    a=page.index('id="o06-shared-control-disposition"');start=page.index('</h6>',a)+len('</h6>');end=page.index('<p class="br-note"><b>工程结论',start)
    nav=[]
    for group in data['groups']:
        rr=[r for r in rows if r['group']==group]
        nav.append([group,'；'.join(r['name']+'：'+r['action'] for r in rr),'功能和目标逐行核收；不恢复旧类体系'])
    page=page[:start]+table(['职责组','当前去留','承接要求'],nav)+'<p><a href="#o06-current-control-mapping">直接查看31类完整记录与对应源</a>。旧十项候选不再作为独立当前方案；旧记录已归档到原31行历史。</p>'+page[end:]
    page=page.replace('原十项处置映射到类和调用方','去留总览：与31类主记录一致')
    if '<!-- O06-CONTROL-INTERACTIONS:BEGIN -->' in page:
        page=re.sub(r'<!-- O06-CONTROL-INTERACTIONS:BEGIN -->.*?<!-- O06-CONTROL-INTERACTIONS:END -->','',page,flags=re.S)
    interaction=('<!-- O06-CONTROL-INTERACTIONS:BEGIN --><section id="o06-control-interactions"><h5>完整交互怎样核收</h5><p>以下10条覆盖31类的实际用户路径，不为每个旧类复制一套交互。HTML能演示界面状态与草稿；绑定解析、HALCON、真实保存与资源释放仍由产品回归核收，不能由预览代签。</p>'+table(['路径','界面','必须覆盖的交互','VM定位键'],data['interactions'])+'<p>'+escape(data['deletionGate'])+'</p><p>'+escape(data.get('engineeringNote',''))+'</p><p>'+escape(data.get('buildAndVerification',''))+'</p></section><!-- O06-CONTROL-INTERACTIONS:END -->')
    last=page.index('</table>',page.index('id="o06-control-labeledcontrol"'))+len('</table>')
    page=page[:last]+interaction+page[last:]
    # Keep the original engineering location as the single before/after design.
    marker='<!-- O06-ENGINEERING-CLASS-MAP:END -->';end=page.index(marker);start=page.rfind('<pre',0,end)
    tree='''现在（真实文件；旧SDK与新字段层并存）
src/Plugin.SDK/UI/Controls/          旧31类中的基础控件、绑定组合、ROI协调及主题依赖
src/Plugin.Wpf/Controls/            已提取数值、范围、开关、取色器及纯交互
src/Plugin.Wpf/Fields/              声明页/字段/来源/显示对象私有样式草稿
src/Plugin.Wpf/Themes/              独立Generic/Combo/资源与局部主题边界
src/Plugin.Wpf/Regions/             RegionGeometryEditor、RoiGeometryEditing与受限交互合同
src/UI/Services/Editing/Nodes/      正常开窗、来源、ROI与私有草稿装配
src/UI/Services/Editing/Regions/    独占RoiViewportEditScope
src/UI/Shell/Controls/Panels/       主界面ImagePreviewControl
tools/SunEyeVision.BlobAnalysis/Runtime/  HALCON分割三模式、测量与输出；内置预处理/形态学已移出
tools/SunEyeVision.Tool.ImageFilter/     上游滤波及新增有效域直方图均衡化
tools/SunEyeVision.Tool.ImageThreshold/  显式二值化与BinaryMask输出
tools/SunEyeVision.Tool.Morphology/      严格0/255准入、形态学与新增Circle
Blob.Wpf/Views/                     现只读BlobDataAnalysisControl；旧BlobAnalysisToolDebugControl已删

保留的目标组织（实际落地见上方及31行文件路径；以下不代表目录均已建立，不新建31个类）
src/Plugin.Contracts/              中立字段、几何、结果与稳定引用；不含WPF/控件/主题
src/Plugin.Wpf/
  Controls/                        提取NumericUpDown、RangeSlider、RangeInputControl、ToggleSwitch、ColorPicker
  Fields/
    DeclaredFieldEditor.cs          复用新草稿/校验；改按声明选择模板
    SourceSelectionField.cs         宿主给IMAGE候选，整行ComboBox
    ReferenceSelectionControl.cs    宿主给类型化引用，控件只展示选择
    Templates/                     FieldRow / ScalarField / RangeField / ReferenceField / ImageSourceField
                                   ColorField / JudgmentGroup / DisplayObjectField / TextDisplayGroup
  Regions/                         复用现几何，按RoiField / RoiSourceField / RoiShapeSelector / GeometryFields组装
  Themes/                          Numeric / Range / Boolean / Color / Combo / Icons / Generic.xaml及资源注册
src/UI/Services/Editing/            窗口、草稿、候选、ROI scope、唯一运行与释放；不给基础控件调度权限
src/UI/Shell/Controls/Panels/       主界面图像源组合，不塞入Blob或共享SDK
tools/SunEyeVision.BlobAnalysis/Runtime/
  BlobDefinition.cs                 现中立入口；不另建Definition工程
  BlobPresentation.cs               已实现Contracts页/组/显隐描述，后续随算法与ROI继续适配，不引WPF
BlobConfigurationFields.cs（门面）   当前含WPF Color/旧SDK槽位，只作隔离适配，不搬入中立控件
Blob.Runtime/                       HALCON唯一算法；分割三模式与上游职责承接已实施，完整ROI和整体界面继续适配
Blob.Wpf/Views + Presentation/      当前只读表/标量投影继续使用；S5通用结果尚未实施

依赖：工具Definition/Runtime → Contracts/算法基础；Plugin.Wpf → Contracts；UI宿主 → Plugin.Wpf+运行服务。
退出旧SDK类：先迁实际消费者和资源，再完整Release与设计时/运行时回归，最后删除；当前已批准实施，未迁完的旧类继续保留。'''
    page=page[:start]+'<pre>'+escape(tree)+'</pre>'+page[end:]
    page=page.replace('现有41类逐件映射','本轮31类按新职责逐件映射（早期41类范围为历史）')
    page=page.replace('<span id="shared-control-design-inventory"></span>','')
    page=page.replace('<h5 id="o06-current-control-mapping">','<span id="shared-control-design-inventory"></span><h5 id="o06-current-control-mapping">')
    return page

def render_document(page):
    return render_control_architecture(render_s4_stage(render_s1_stage(page)))


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
    for key in ('o06-blob-final-review', 'blob-verification-steps', 'implementation-live-status',
                'o06-stage-overview','s3-implementation-status','s4-implementation-status'):
        if ids.count(key) != 1:
            raise ValueError('Missing primary anchor: ' + key)
    PAGE.write_text(page, encoding='utf-8', newline='')
    print(json.dumps({'updated': str(PAGE.relative_to(ROOT)), 'anchors': len(ids),
                      'idempotent': True, 'sha256': hashlib.sha256(page.encode()).hexdigest()}, ensure_ascii=False))


if __name__ == '__main__':
    main()
