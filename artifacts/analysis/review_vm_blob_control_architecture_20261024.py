"""Architect's review of a received, bounded static VM packet and self source.
Writes review material only. Main-document application belongs to delivery/document owner.
"""
from pathlib import Path
from hashlib import sha256
from html import escape as E
import json
ROOT=Path(__file__).resolve().parents[2]
VM=Path('D:/MyWork/REA逆向测试/analysis/selfdev-support/VM-CONTROL-BLOB-ASSEMBLY-20261024')
OUT=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024'
EXPECTED={
 'delivery.json':'2FC716D0DB273ACC4B4BC37940A0EBDDF16144893962E1D5B0D974C07742DD0D',
 'root/roi-input-bridge-review.json':'1B0425C2AAECEC72A7672A9782189D8353320DE3156F7E1A9388A700A261B70F',
 'root/coverage-boundary.json':'785723398B50D97E96186CFBA41137F6ED5DD6DBA2F4C1251431C76724838ED4',
 'source/source-findings.json':'BA52428728B7C3254D65125FA49E096E417643D61DD44B7398E036C989673E8F',
 'root/root-findings.json':'852D55134360088FE2EB12864A4A95E4305F464C46BEA1A8BD75DACEAB788021',
 'root/library-layer-inventory.json':'E64556334A5D97A645B3AC5160AADBEE533D84165FA525D4E1BA5A59DBADEBEF',
 'root/blob-group-links.json':'DF5B9B7AF06470823AAD5B27BD713825429DA8B799482ED6F811CB8CA8F71E46',
 'root/template-map.json':'E8038CE3AC0A8D196A3F203E896E530938F6FDCF32BB3EB81805E778E0281470',
}
SELF=[
 'tools/SunEyeVision.BlobAnalysis/Views/BlobAnalysisToolDebugControl.xaml.cs',
 'src/Plugin.SDK/UI/Controls/Roi/Views/RoiEditorManagerControl.xaml.cs',
 'src/Plugin.SDK/UI/Controls/Roi/Views/RoiEditorControl.xaml.cs',
 'src/Plugin.SDK/UI/Controls/ConfigSetting.cs',
 'src/Plugin.SDK/UI/Controls/ParamBindingBase.cs',
 'src/Plugin.Contracts/Editing.cs','src/Plugin.Contracts/ToolContracts.cs',
 'src/Plugin.Wpf/ParameterEditorControl.cs',
 'src/Plugin.SDK/UI/Controls/LabeledControl.cs',
 'src/Plugin.SDK/UI/Controls/Icons/ToolIcon.cs',
 'src/Plugin.SDK/UI/Controls/ColorPicker/ColorSelector.cs',
 'src/Plugin.SDK/UI/Controls/ColorPicker/ColorPicker.cs',
 'src/Plugin.SDK/UI/Controls/NumericUpDown.cs',
 'src/Plugin.SDK/UI/Controls/RangeSlider.cs',
 'src/Plugin.SDK/UI/Controls/ResultPresentation/GraphicsDisplayEditorControl.xaml.cs',
 'src/Plugin.SDK/UI/Controls/ResultPresentation/JudgmentEditorControl.xaml.cs',
 'tools/SunEyeVision.BlobAnalysis/BlobOutputContract.cs',
 'tools/SunEyeVision.BlobAnalysis/BlobAnalysisResults.cs',
]

# Decisions concern the proposed Blob/framework migration, not immediate product authorization.
DECISIONS=[
 ('D01','删除旧路径','Blob页面 Cleanup 中清共享交互层/主结果层，以及页面直接接主视口与旁路执行的公开接线','本方Cleanup直接调用Manager.ClearMainOverlay及主ResultRenderLayer.Clear；VM F04/F13–17分开窗口、借用视口、主视图和模块寿命。','窗口退出只能退还自己的编辑作用域，不能清共享结果；由宿主维护编辑层租约与执行入口。删除这些调用及其工具侧权限。','Blob关窗不清其他节点/在途结果；运行仍由同一准入快照链；关闭/切节点/异常与迟到回调独验。'),
 ('D02','整体替换并退役','RoiEditorManagerControl作为工具层公开绘图协调器的旧合同','本方Manager持ImageControl/Canvas/renderer/窗体事件，固定两组RoiEditor并可Children.Clear；VM ROOT-F04/F06和F16表明字段模型、控件、视图桥可分层。','现有公共类及SetMainImageControl/ClearMainOverlay模式不作为目标接口延续。迁移消费者后删除旧公开协调入口；交互仲裁/场景所有权归宿主，局部视觉组合可提取内部部件，不能让旧Manager继续拥有全局视口。','所有调用者先有映射及过渡退出条件；工具只声明区域字段/能力，不能获得全局Canvas；不在本轮擅自改全部消费者。'),
 ('R01','整体重做职责组织','RoiEditorControl中的编辑状态、手势、订阅与画布回填组织','本方公开OverlayCanvas/SetMainImageControl/LoadImage/ProcessMouse等，控件同时持字段集合与手势状态；VM字段节点→模板→ROI桥有直接源码。','以字段会话组织本地/继承/数值编辑，由宿主授予交互作用域，模型/手势/视觉分开。已有几何计算可逐函数验证后复用，不保留整类作为未审默认。','绘制/继承/参数拼装切换、旋转/取消/数值回填同一草稿；切换工具后旧会话不能操作新场景。'),
 ('R02','重做公共字段合同及接入','ConfigSetting/ParamBindingBase/ParamBinding/来源选择的旧字符串接线','ConfigSetting直接维护BindingSource/AvailableDataSources及__GlobalVars__名称重写；已有Contracts具稳定Port/OutputReference/编译绑定与草稿，但仅覆盖其已证边界。','控件消费同一字段和绑定计划，不自行成为类型、数量、来源或重命名权威。淘汰旧字符串协议作为公共合同；映射旧数据由适配层完成，不能再造平行类型系统。','重命名/作用域/数量/非法常量/失效引用/取消/保存均从同一合同判定；现有基础合同不能冒已覆盖ROI完整手势。'),
 ('R03','重做运行与可选UI装配边界','ToolDebugControlBase/工具IToolEditorFactory到执行、视口和保存的整套接入','Blob工具同时实现运行接口与FrameworkElement工厂；专业页ExecuteWithLiveContext及主图接线。VM F03–05存在公共/扩展视图选择，不能据其DLL结构推导我们必须照拆。','运行合同、编辑服务和可选专业视图分别注册/适配，优先扩现有Contracts/Runtime/Plugin.Wpf。旧基类混合职责合同退役，不以给旧基类继续加字段解决；是否拆物理程序集以无UI依赖验证决定。','缺少UI扩展仍能执行；UI退出不影响运行实例；发布依赖沿既有规则，不能用开发目录DLL冒正式来源。'),
 ('R04','重做数据分析消费者通道','BlobDataAnalysisControl的裸ToolResults/图元高亮通道','已有分析表持BlobResult裸对象并构造无Run身份高亮，结果合同已有Field/Element/Run及reader。VM输出索引映射只能证明VM字段对应，不能证明跨帧稳定身份。','表格/树/高亮改为同一只读结果会话的投影；可保留业务列与表格视觉。数据请求/选择发稳定地址，不由控件掌握native寿命。','旧行、迟到回调、排序/分页、0结果/失败、关窗最后reader及图形联动同源验证。'),
 ('K01','复用有限视觉原语','LabeledControl、ToolIcon、ToggleSwitch、颜色部件及纯ItemsSource编辑视图','已读标签/图标/颜色组件及19行ItemsSource编辑壳，职责集中在WPF布局、绘制、局部输入；未见其拥有工作流/全局主窗。','保留这些局部表现能力的理由是职责单一与实际输入合同匹配，不是已经写了。样式资源沿既有已确认VM规格；业务协调和状态不随视觉部件保留。','复用不等于已验DPI/主题/所有手势。NumericUpDown/RangeSlider的double API仅在浮点/可表示区间使用，不能因此充当完整Int64字段编辑器。'),
 ('K02','复用经证实的数据基础','中立声明、不可变输出、稳定字段与reader/owner机制','本方Contracts、BlobOutputContract及已有T01/T02有限独验证据符合运行数据与显示分离需求。','按其已证能力复用，并核消费者和图像/区域桥缺口；不因新代码较新就自动优先，也不为对齐VM重造一套接口。','原有限PASS保留；新显示/编辑适配仍须全链验证，不能用1992断言等数量代完整用户链。'),
 ('K03','复用已确认规格与有边界算法实现','VM区域样式规格、HALCON算子路径、已有批量渲染原语','用户已要求HALCON本轮使用且未来保留迁移；原VM样式证据和已有批量路径各有来源。','复用规格及职责正确的算法/渲染函数，不默认保留当前调度/缓存/交互组织。性能与资源预算另验；HALCON类型只留在专用实现与适配边界。','不重画自拟VM样式；不强求算法参数/特征数值相同，不冒OpenCV可立即替换。'),
 ('X01','明确不采用VM做法','普通字段直接写模块+确认路由关闭、共享静态模板缓存/程序集保留与全局UI引用','VM F12证明确定链没有统一Commit，ROOT-F08证明共享模板/ExternAssembly缓存；F08–09关闭和连续循环存在未测边界。','只借鉴可证机制，不复制这些生命周期与写入策略。我们的配置发布、可卸载边界和编辑作用域由实际需求决定；不能声称VM当前有本方私有草稿事务。','实机验证VM路由和关闭时序；不以未知VM缺陷作为我们方案必然更优的证据。'),
]

def html_table(head,rows):
 return '<div style="overflow:auto"><table><thead><tr>'+''.join('<th>'+E(x)+'</th>' for x in head)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+E(str(x))+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table></div>'

def main():
 OUT.mkdir(parents=True,exist_ok=True)
 received=[]
 for f,h in EXPECTED.items():
  p=VM/f;actual=sha256(p.read_bytes()).hexdigest().upper()
  if actual!=h:raise RuntimeError('Frozen evidence changed: '+f)
  received.append({'path':str(p),'sha256':actual})
 selfEvidence=[{'path':p,'sha256':sha256((ROOT/p).read_bytes()).hexdigest()} for p in SELF]
 source=json.loads((VM/'source/source-findings.json').read_text(encoding='utf-8'))
 bridge=json.loads((VM/'root/roi-input-bridge-review.json').read_text(encoding='utf-8'))
 implications=[
 ('B01–B02','输入图像来源是EnumerationG的上游订阅投影，独立于显示图像选择；不能共用一个含混的选择器合同。来源显示文本不应作为本方稳定地址。'),
 ('B03–B04','绘制/继承是字段模式与订阅关系切换；区域整体与标量参数继承分开。复用既有中立绑定合同，ROI字段模式适配重做，不能把辅助序列化当继承入口。'),
 ('B05','数值与手势共用参数写入桥值得借鉴；本方接到同一私有草稿，按明确准入触发预览，不复制VM直接写正式模块。'),
 ('B06','回填跨Dispatcher且受拖动/可见性门控制；本方必须有会话和运行身份，丢弃迟到更新，不能只靠可见性防串帧。'),
 ('B07','清理必须沿控件、视图、模块三种所有权分别验证；无XML DeIniters不能推导无清理，关闭也不能推导事务回滚。')]
 lib=json.loads((VM/'root/library-layer-inventory.json').read_text(encoding='utf-8'))
 for a in lib['assemblies']:
  if sha256(Path(a['binaryPath']).read_bytes()).hexdigest().upper()!=a['sha256'].upper():raise RuntimeError(a['assembly'])
 report={'role':'技术总监｜架构与决策','status':'bounded-static-architecture-review; whole evidence/runtime verification pending, not product implementation approval',
 'requirements':['工具声明业务字段与能力，不持有全局UI','多个消费者使用同一运行结果与身份','本地/继承/数值编辑同一草稿，明确发布/取消','窗口、视口、运行、资源各有所有者','HALCON本轮，未来后端可替换','多工具可复用但不强制自动生成全部页面'],
 'alternatives':[
 {'option':'在原Manager和页面上补补丁','assessment':'迁移成本低，但保留错误的视口/事务权限与多权威类型；退回'},
 {'option':'整套复制VM XML/operation/控件体系','assessment':'可减少部分手工页面，但继承VM直接写值/反射和静态缓存，未证明符合本方事务与资源需求；不采用整套复制'},
 {'option':'按统一字段/会话/运行/结果合同重做公共接入与协调，视觉及独立原语逐项复用','assessment':'推荐；明确删除旧公开协调协议和全局清理路径，接受真实迁移成本；接口沿现有工程演进，不另起平行体系'}],
 'decisions':[dict(zip(['id','action','object','evidenceAndCause','replacementAndReason','acceptance'],x)) for x in DECISIONS],
 'correction':'此前将Manager描述为直接运行调度者过宽；本方实际执行在Blob页面ExecuteTool/ExecuteWithLiveContext，Manager主要路由输入/窗体事件与共享overlay。删除重构决定依据实际权限，不依据错误概括。',
 'vmKnown':source['findings'],'bridgeEvidence':bridge['findings'],'bridgeDecisionImplications':implications,
 'coverageBoundary':json.loads((VM/'root/coverage-boundary.json').read_text(encoding='utf-8')),
 'runtimeUnknowns':source['unknowns'],'received':received,'selfEvidence':selfEvidence,
 'delivery':'经理22:04核静态整合，21:49核实机解除条件；文档由经理统排原O06。不改变变量修复和Release队列，不在本次改产品'}
 (OUT/'architect-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 html=['<section id="vm-blob-control-architecture-review"><h3>VM控件库与Blob组装链：主控静态架构复审</h3><p><b>当前结论：</b>退回默认沿用现有控件组织的方案；推荐重做公共接入与协调，删除越界权限路径。以下为有界静态推荐，原版实机尚未运行，不代表完整组装链或产品实施已批准。</p>',
 '<div style="display:flex;gap:12px;flex-wrap:wrap"><div><b>工具</b><p>业务规则 / 参数与结果声明</p></div><div>→ <b>宿主应用能力</b><p>编辑发布 / 执行准入 / 场景与资源所有权</p></div><div>→ <b>表现与专用实现</b><p>参数控件 / ROI手势 / HALCON算法 / 只读结果</p></div></div>',
 '<h4>实际程序集身份已核：模型、接口、模板和视觉类型不能混计</h4>',
 html_table(['程序集','TypeDef总行数','有限继承规则确认的公开视觉类型','边界'],[(a['assembly'],a['typeDefinitionRows'],a['publicVisualBaseConfirmed'],'全部类型不等于全部控件/稳定公共API，当前加载组合未观测') for a in lib['assemblies']]),
 '<h4>删除、重做、复用及原因</h4>',html_table(['编号','处置','对象','证据与根因','替代机制及理由','验收'],DECISIONS),
 '<h4>最终整合新增证据如何影响架构决定</h4>',html_table(['证据','决定及边界'],implications),
 '<p>本次13库是选定核心库，不是完整依赖闭包；Style、ColorFun、Localization直接依赖的内部实现未审，WinForms Release尚未证实进入本WPF链。运行时扩展Control选择仍待实机，不把静态存在当作实际加载。</p>',
 '<h4>仍需实机或进一步证据的边界</h4>',html_table(['问题','当前状态','停止条件'],[(x['question'],x['status'],x.get('stopCondition','')) for x in source['unknowns']]),
 '<p><b>纠正：</b>'+E(report['correction'])+'</p><p>本文是对已冻结证据的架构审核材料，静态整合已核收，实机未验。完整原版来源、哈希、18项链结论和本方源码快照见同目录architect-review.json；最终合并归原主文档，不能用本片段另起平行方案。</p></section>']
 (OUT/'review-fragment.html').write_text('\n'.join(html),encoding='utf-8')
 verification={'passed':True,'selfSources':len(selfEvidence),'receivedFiles':len(received),'assemblies':len(lib['assemblies']),'decisions':len(DECISIONS),'scope':'static architecture material only; runtime and main-document application not verified by this script','reviewSHA':sha256((OUT/'architect-review.json').read_bytes()).hexdigest(),'fragmentSHA':sha256((OUT/'review-fragment.html').read_bytes()).hexdigest()}
 (OUT/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print(json.dumps({'review':str(OUT/'architect-review.json'),'sha256':sha256((OUT/'architect-review.json').read_bytes()).hexdigest(),'frozenFilesVerified':len(received),'assembliesVerified':len(lib['assemblies']),'selfFiles':len(selfEvidence),'decisions':len(DECISIONS),'productChanged':False},ensure_ascii=False))

if __name__=='__main__':main()
