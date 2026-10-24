"""Node-associated control, preview, analysis and rendering review. HTML only."""
import json,os,runpy
from pathlib import Path
from html import escape as E
from urllib.parse import quote

def render(root,page,ref,table,flow):
    inventory=json.loads((root/'artifacts/analysis/blob-control-consumer-closure-20261024.json').read_text(encoding='utf-8'))
    parts=['''<h3 id="br-node-peripherals">节点与周边能力：已优化复核，未优化纳入分析</h3>
<div class="br-note"><b>这是一套关联架构，不是把所有逻辑塞进节点类。</b>围绕节点身份、配置版本与明确运行/帧，检查各周边的提供者、消费者、权限、更新、失效与释放。Blob作为完整消费者验证，优化范围不只限于Blob窗口；本轮仅分析，实施仍需审核。</div>
<div class="br-grid"><div class="br-card"><b>定义 / 图 / 持久化</b><p>类型发现、节点创建复制删除、连线与作用域、正式配置、保存重开与撤销。</p></div><div class="br-card"><b>参数 / 来源 / 公共控件</b><p>业务校验、常量与引用、来源树、草稿事务、窗口与专业视图、控件库适配。</p></div><div class="br-card"><b>图像源 / 预览 / 中央显示</b><p>选择、执行源和显示源分开；帧/坐标一致、加载取消、缓存与预热、历史查看。</p></div><div class="br-card"><b>区域编辑 / 算法 / 执行</b><p>绘制/继承/几何回填、运行准入、快照、算法与引擎、停止失败及恢复。</p></div><div class="br-card"><b>结果树 / 数据分析 / 下游</b><p>同一输出快照的不同投影；分页统计、选择高亮、字段寻址与数量变化。</p></div><div class="br-card"><b>资源 / 诊断 / 退出</b><p>模型与图像寿命、事件/任务退役、最后reader、发布依赖及可测功能与性能。</p></div></div>
<p><b>统一处置规则：</b>已有优化先复核职责与真实效果，保留有效实现和有限验收；已证设计问题明确替换边界，已证实现问题定向修复，证据不足标待核。未优化能力纳入候选问题和验证清单，不因旧48未列而遗漏，不因进入分析清单就自动授权产品改造。</p>
<span id="shared-control-design-inventory"></span><h3 id="br-control-library-governance">控件库管理设计：管理能力合同与责任，不只管理文件和样式</h3>''']
    parts.append(table(['管理对象','架构推荐','当前基础/需调整','验证'],[
      ('公共能力目录','以字段/能力语义标识适配器，列支持类型/数量/坐标/读写模式/诊断；不依赖UI类名作为业务协议。','现有Contracts类型、SDK元数据和WPF模板可复用；缺失描述先核实际需要，扩现有合同，不造并行注册中心。','Blob与第二消费者同能力同语义；不支持能力明确拒绝。'),
      ('控件责任分级','基础视觉、语义字段、复合交互、宿主协调、结果只读、专门能力分开。','31项本次静态使用图与原41类盘点分开；内部部件不都作为插件公共API。','公共API依赖单向、运行可无WPF；专业扩展不访问全局可变状态。'),
      ('装配和依赖','宿主用已注册能力装配View/模板并注入所属上下文；Contracts不反向引用UI或引擎。','复用已有Plugin.Wpf边界；旧SDK和Blob运行/视图耦合需真实迁出，不以换目录假装解耦。','缺可选UI仍能执行；同Release插件/模板/资源部署完整，设计时与运行时分别核。'),
      ('状态和生命周期','控件仅拥有局部输入/手势状态；正式配置归发布事务，图层归会话，运行资源归owner。','替换页面和Manager跨节点清层、UI自有权威类型规则等越界路径。','切节点/关窗/取消/失败/异步迟到不回写错误实例，不提前或重复释放。'),
      ('版本和维护','合同、模板、主题和控件行为有同源兼容约束；记录消费者/替代关系/验收覆盖。','生成依赖和使用矩阵作为文档事实，不凭文件数量宣布公共能力完整。','重命名不改变稳定字段身份；重大行为改变先核所有关联消费者。'),
      ('质量及可访问性','样式与交互分开，主题/DPI/键盘/诊断、性能与资源预算分别验。','保留WPF原生布局/表格能力；只在证据支持时替换后端或引入复杂缓存。','同场景、同能力、同发布身份实测；复用不等于已有行为全部PASS。')]))
    parts.append(f'<h3 id="br-blob-controls">Blob实际控件使用矩阵：{inventory["count"]}项静态可达控件/基类与宿主</h3>')
    compared=runpy.run_path(str(root/'artifacts/analysis/review_blob_controls_algorithm_structure_20261024.py'))
    parts.append(compared['render']())
    parts.append('<details data-blob-self-inventory><summary>本方31项静态类型明细（用于追溯，不替代VM对照）</summary>')
    parts.append('<p>从Blob调试页与Blob数据分析页出发，递归核XAML实例、代码new、已知自研基类及Generic默认模板；另明确纳入主视口、图像预览及默认/专业壳的关联候选。<b>不是31项全部同时实例化，也不是整个公共库总数。</b>反射、外部主题覆盖和动态factory仍需运行验证；静态使用图不能证明运行闭包或性能。</p>')
    rows=[]
    for c in inventory['controls']:
        name=c['name']
        if name=='BlobDataAnalysisControl':
            recommendation='替换裸结果/图元高亮的数据通道；保留业务列及统计语义'
            design='按指定Run的reader读值，选择发稳定元素地址；批量/按需行与聚合，不让表格成为结果真源。'
        elif name=='BlobAnalysisToolDebugControl':
            recommendation='重构应用接线，保留合格业务编辑内容'
            design='参数、ROI、来源同草稿；移除直接调全局视口/清结果和旁路执行职责。'
        elif name=='ImagePreviewControl':
            recommendation='复核已实施版本/缓存/取消，保留有效机制'
            design='显示/执行/勾选来源与节点身份分开；预算覆盖解码、缓存、UI持有及在途，迟到结果拒收。'
        else:
            recommendation=c.get('recommendation','按能力适配')
            design=c.get('design','核所属会话、语义、生命周期和验收边界')
        path=root/c['source'];url=quote(os.path.relpath(path,page.parent).replace('\\','/'),safe='/:')
        rows.append([E(name),E('；'.join(c['via'])),E(recommendation)+'<br>'+E(design),f'<a href="{url}">{E(c["source"])}</a>'])
    parts.append(table(['实际类型','使用路径','处置候选与依据（服从整体架构）','源码'],rows))
    parts.append('<details><summary>原生WPF基元、非视觉数据与静态边界</summary><p>原生元素：'+E('、'.join(inventory['nativeWpfElements']))+'</p><p>值/排序声明，不算控件：'+E('、'.join(inventory['nonVisualValueTags']))+'</p><p>WPF布局、输入、表格原语按宿主合同使用，不能因“全部控件分析”就重写系统控件。静态未解析自定义标签：'+E('、'.join(inventory['unresolvedCustomTags']) or '本次限定规则内未发现；不代表动态运行已验证')+'</p></details>')

    parts.append('</details>')
    parts.append('<h3 id="br-region-result-rendering">区域编辑与结果渲染：两条机制分别审，围绕同一节点/场景闭合</h3>')
    parts.append(flow([('区域编辑','字段声明 → 绘制/继承 → 手势/命中/坐标 → 私有草稿'),('编辑发布','数值回填/校验 → 一次确认或取消 → 正式配置版本'),('结果显示','算法输出 → 同次快照 → 图像/轮廓/文字/只读选择'),('作用域退役','旧任务拒收 → 编辑层/显示层各自退出 → reader最后释放')]))
    parts.append(table(['能力','当前实际机制/历史优化','架构及实现复核','推荐与验证'],[
      ('交互ROI绘制','WpfShapeRenderer每区域DrawingVisual、几何快照差量、视口缩放补偿已有实现。'+ref('S03 S31'),'轻量绘制方向合理，不证明命中/手势/会话边界完整；当前_forceAllRedraw在SetViewportTransform置true，正常Render后未见复位，Clear才复位。','保留轻量图元；失效按渲染周期消费。核缩放后空悬停、拖动、遮挡与捕获丢失，不能以这一处修复代替公共交互架构。'),
      ('区域编辑协调','Manager集中两组ROI、renderer和窗口/视口事件。'+ref('S02 S03'),'组内互斥合理；全局Canvas、正式参数和运行不应由工具控件一并拥有。仅显示非空图不证明该图可用于当前节点编辑。','迁出跨窗/节点仲裁和运行职责；工具用字段作用域。7类真实创建/移动/尺寸/旋转/取消/继承与回填分别核，样式沿VM已证规则。'),
      ('大数量交互图形','已有差量与轻量visual，不等于鼠标候选搜索和手柄更新已可扩展。','避免把每个结果轮廓做成可交互控件；交互图形仅需对邻近候选精确命中，全部数据仍完整。','在正确性与原批准档下比较空间索引、脏更新/合帧、可见裁剪及手柄复用；不凭HTML流畅承诺产品速度。'),
      ('结果轮廓批量路径','ResultRenderLayer有Task.Run构建、分块StreamGeometry缓存、版本/取消门；Blob有AddContourBatch及SourceElements。'+ref('S07 S30'),'已实施部分应保留其价值；批量和普通路径不能混同。后台build失败/取消收尾、旧新在途预算及消费者身份仍须验。','保留合格批量/分块结构，补状态/预算/完整性；只改变化分块，平移缩放不无条件重建全部点。'),
      ('普通轮廓和混型分组','普通RenderContours超过5000使用contourStep；DisplayCollector按GraphicsStyle键累积、item.Type只在创建时设置。'+ref('S30 S32'),'静默跳索引可能漏关键结果；同style跨类型存在进入错误派发类型的源码风险。当前Blob主要批量路径，不能泛化成Blob所有轮廓已漏。','按类型+样式语义分组；视口裁剪/分块优先，密度显示须显式且能精确查看；异型同style与NG落在跳过索引的样本独验。'),
      ('文字/坐标/选择与缓存','已有文字缓存、视口更新与结果高亮接口。'+ref('S29 S30 S33 S34'),'缓存/缩放必须保持Frame、单位、字体/DPI和内容版本；只读图形选择与参数编辑分离。','保留按实际内容键的缓存；核样式/文字原位变化、换帧、换图与高亮身份；显示和算法数据不同寿命。')]))
    parts.append('<h3 id="br-preview-analysis-recheck">节点周边实现复核：中央显示、图像预览、数据分析一并审</h3>')
    parts.append('<div class="br-note"><b>生产入口核实后的重要边界：</b>主预览与Blob专业ROI页共用同一ImageControl，不是两个独立视口；现有独立模板编辑视口是模态窗口，主窗输入被禁用；全屏只是重挂原控件。模板取图会灰度转换，部分直转路径未走通用frame标记桥。当前结果层有缓存字典和CPU诊断，但未见实际字节计量，ROI命中仍有线扫。'+ref('S38')+'<br>因此，多视口、三格式、同帧、可见反馈与资源预算必须按真实场景核验；不能用DTO/reader PASS冒充视口场景租约，也不能自动新增双非模态窗口来满足测试。已有功能限制与新增业务需求分开决策。</div>')
    parts.append(table(['对象','已实施/现有实现证据','应保留什么','待纠正、补验或重新设计'],[
      ('中央图像显示控件 · 已有优化待复核验收','源提交历史含0dee4701分Tile/文本缓存/视图合帧，e621dffc回到确认过的批量/异步结构；当前代码存在三层及viewport。'+ref('S29 S30 S31'),'三层责任、轻量显示、批量几何和有效合帧机制具有合理性；提交名perf不是性能证据。','显示场景一致性、普通/批量全信息、渲染取消/资源及正式p95未由有限数据测试证明；复核当前调用而非自动全盘推翻。'),
      ('图像预览/图像源面板 · 现有优化实现待同源验收','ImageInfo有版本/弱引用/取消；PreviewBitmapCache有内容hash、有界LRU和独立等待者取消。'+ref('S36 S37'),'来源版本、缓存预算和异步取消机制值得保留为候选；原图与缩略图用途应分开。','核选择节点/来源节点/执行节点、勾选保存、显示序列与运行序列一致性；hash命中也可能有读文件成本，需测首张/再次/换同名文件，预热不先定答案。'),
      ('节点数据分析装配','MainVM按当前节点new工具及分析控件，加载当前context缓存并订阅高亮；协议收ToolResults并发DisplayItem列表。'+ref('S33 S34'),'分析视图独立于调试窗口是合理需求；保留专业分析能力，不强制所有工具只显示一棵树。','此协议未显式携带Run/Frame/reader责任；该段退订高亮不证明控件/工具实例寿命完全闭合。应改为同一只读输出会话的适配，核旧回调与当前节点门。'),
      ('Blob数据分析表','OnResultUpdated逐项Add裸BlobResult到ObservableCollection；多项统计重新聚合；选中时新建BoundingBox高亮图元。'+ref('S35'),'业务列、统计、只读表格及选中联动可保留；原生DataGrid可虚拟化视觉，不等于数据已按需。','替换裸对象/无运行身份高亮通道，选择稳定元素地址；批量/分页值和聚合复用同快照。逐项通知和统计有成本风险，未测不称已卡顿。'),
      ('结果树与下游','已有schema、指定Run、reader、paging及绑定计划；专业分析与树可能不同适配。'+ref('S08 S10 S13'),'同一声明、多种投影、明确资源持有是合理方向。','不得让分析表/树/渲染分别解释类型与结果状态；下游不从UI取值，0结果/失败/旧行独立验证。')]))
    parts.append('''<div class="br-note"><b>已优化复核的真实验收边界：</b>T01的几何/reader/Presenter有限独验（2成熟+9组1992断言），T02的真实Blob/native/输出有限独验（69成熟+9组398断言）已有正式报告。它们证明各自坐标/身份/分页/租约及真实区域数据能力，<b>不证明中央WPF屏幕渲染、7类交互操作、树→图形点击联动、缓存/LOD和正式性能已验</b>。“7种几何数据”也不是“7类交互控件操作”。旧PASS保留，未验部分明确补证；本轮无新增产品运行。</div>''')
    parts.append('<p>'+ref('T01 T02')+' · 本页证据索引包含上述实际源与两份有限独验报告，并记录逐文件SHA。现有实现与已验范围分开，未取得专项优化签收的内容不能猜为“从未优化”或“已完成”。</p>')
    return '\n'.join(parts)
