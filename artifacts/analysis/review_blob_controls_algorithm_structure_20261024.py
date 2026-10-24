"""VM/self/target review, documentation only; declared nodes are not counted as CLR controls."""
from pathlib import Path
from html import escape as E
from hashlib import sha256
import json, os, re
ROOT=Path(__file__).resolve().parents[2]
PAGE=ROOT/'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护/优化方案/optimization-o06.html'
OUT=ROOT/'artifacts/analysis/blob-controls-algorithm-structure-20261024'
VMROOT=Path('D:/MyWork/REA逆向测试')
SOURCES={
 'V1':VMROOT/'docs/architecture-recovery/tool-rendering-entry-audit.resources/IMVSBlobFindModuAlgorithmTab.xml',
 'V2':VMROOT/'analysis/decompiled/VMControls.WPF/VMControls.WPF/ParamsConfigControlBase.xaml.cs',
 'V3':VMROOT/'analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/GroupLinkItem.cs',
 'V4':VMROOT/'analysis/decompiled/Apps.XmlUI/styles/paramusercontroldatatemplate.xaml',
 'V5':VMROOT/'analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/ROISelecter_Mul.cs',
 'V6':VMROOT/'analysis/decompiled/VMControls.WPF/VMControls.WPF.Base/ROISelecterHolder.cs',
 'V7':VMROOT/'analysis/decompiled/VMControls.WPF/VMControls.WPF/ParamsConfigWithRenderControlBase.xaml',
 'V8':VMROOT/'docs/plugins/BLOB-ANALYSIS-IDENTITY-SOURCE-INVENTORY.md',
 'S1':ROOT/'tools/SunEyeVision.BlobAnalysis/Views/BlobAnalysisToolDebugControl.xaml',
 'S2':ROOT/'tools/SunEyeVision.BlobAnalysis/Views/BlobAnalysisToolDebugControl.xaml.cs',
 'S3':ROOT/'tools/SunEyeVision.BlobAnalysis/BlobAnalysisTool.cs',
 'S4':ROOT/'tools/SunEyeVision.BlobAnalysis/BlobAnalysisResults.cs',
 'S5':ROOT/'tools/SunEyeVision.BlobAnalysis/BlobOutputContract.cs',
 'S6':ROOT/'tools/SunEyeVision.BlobAnalysis/BlobResult.cs',
 'S7':ROOT/'tools/SunEyeVision.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.csproj',
}

def table(headers,rows):
 return '<div style="overflow:auto"><table><thead><tr>'+''.join('<th>'+E(x)+'</th>' for x in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+E(x)+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table></div>'

def render():
 rows=[
 ('页面与窗口壳','ParamsConfigControlBase加载普通AlgorithmTabRoot；ModuleParam.GetParamRoot/DoIniter/GetItemsValueUnit后设DataContext。V2；原Blob无专属Control DLL的库存V8。','BlobAnalysisToolDebugControl / ToolDebugControlBase / DefaultDebugWindow / CustomDebugWindow','借鉴通用壳+能力组装，调整工具页直接持有主视口/执行上下文的接线；不强制照抄XML生成所有页面。'),
 ('图像输入及继承来源','原Blob显式GroupLinkItem(ImageSourceGroup)；GroupLinkItem从GroupsRoot取组、Clone并插入父集合。V1/V3。具体该组全体视觉子控件及来源过滤待核。','ImageSourceSelector / ParamBinding / ParamBindingBase','复用来源编辑交互，统一稳定字段与作用域过滤；声明不等于VM来源树完整校验已证。'),
 ('数值/枚举/布尔字段与订阅','原Blob有Integer/Enumeration/Boolean声明；Apps.XmlUI的Integer_Sub以SliderArrange绑定CurValueA/Min/Max和ValueChangedCommand；模板与订阅动作V4。','ConfigSetting / NumericUpDown / RangeSlider / RangeInputControl / ToggleSwitch','保留合格基础视觉；语义、数量、校验与常量/引用切换归统一字段合同。不能因VM模板存在就认为我们的这些控件设计合理。'),
 ('布局/图标/标签','VM模板有LeftRightControl等承载，V4；未核出与我们每个标签/图标一一对应的Blob专用CLR类。','LabeledControl / ToolIcon','可复用表现原语，统一主题/DPI/键盘；不把装饰类提升成工具业务API，也不虚构VM同名控件。'),
 ('检测区绘制/继承与管理','原Blob声明ROISelecter Mul、类型集、RoiMaxNum和InheritWayGroup2；V4模板ROISelecter→RoiSelectControl；V5局部状态联动。组展开后的全部继承逻辑待核。','RoiEditorManagerControl / RoiEditorControl / RoiSubscribePanel','替换Manager跨节点/视口/执行仲裁责任；保留字段局部编辑与组内互斥，交宿主编辑会话。VM16数量限制不自动复制。'),
    ('工具栏/几何回填/画布桥','VM ROISelecter模板和ROISelecterHolder有ROIList及RoiChangedCommand；组合壳RenderControlBase绑定选择/删除/复制/可见命令。V4/V6/V7。不能当作Blob运行实测。','ShapeToolBar / ToolBarControl / DrawingParameterPanel','复用此前区域样式方案和已确认VM证据，不另设计。工具声明允许形状和所属参数；框架管理手势、坐标、命中、草稿回填与退役。具体7类交互沿原证据逐类验；不是工具页手动连全局Canvas。'),
 ('结果图形/文字样式','原Blob ResultShow和Display声明；通用Render组合V7。具体颜色/文字模板到Blob字段的完整映射待核。','GraphicsDisplayEditorControl / TextDisplayEditorControl / DispItemColorControl / DispTextColorControl / ColorSelector / ColorPicker','复用颜色/字体等视觉控件；调整样式编辑与算法计算需求的界线。样式隐藏不能删除下游需要的输出。'),
 ('业务判定编辑','原Blob运行筛选声明与结果显示声明V1；不能从这些声明认定VM拥有与我们JudgmentEditorControl等价的独立编辑器。','JudgmentEditorControl','保留业务筛选和NG判定分离；公共控件仅编辑规则，工具解释业务判定，结果层展示。VM无完整对应时明确不作样式/结构复制依据。'),
 ('中央图像/只读结果渲染','通用ParamsConfigWithRenderControlBase组合ParamsConfigControlBase与RenderControlBase并传Module/Image/ROI命令，V7；原BlobDisplay输出证据V8。','ImageControl / ResultRenderLayer','借鉴组合与明确消息边界；不照搬渲染库。复核既有三层、批量轮廓/缓存/结果身份，替换页面清全局结果路径。'),
 ('图像预览与数据分析','VM通用图像/参数组合存在V7，但未证其缩略图/Blob表格与我们这两类完整同等实现；不能拿SDK WinForms样例充当VM主界面。','ImagePreviewControl / BlobDataAnalysisControl','复用已验缓存/取消机制；数据分析接指定Run的reader与元素地址。补真实VM机制证据前不宣称当前控件已与VM逐项对齐。'),
 ]
 controls=json.loads((ROOT/'artifacts/analysis/blob-control-consumer-closure-20261024.json').read_text(encoding='utf-8'))['controls']
 names={x['name'] for x in controls}
 mapped={x.strip() for row in rows for x in row[2].split(' / ')}
 assert mapped==names,{'missing':list(names-mapped),'extra':list(mapped-names)}
 out=['''<!-- BLOB-CONTROL-COMPARISON:BEGIN -->
<div class="br-note"><b>纠正此前举证方式：</b>31项是我们静态可达的CLR控件/基类/宿主清单，不是VM对照矩阵。下表按10个消费者能力维度将全部31项与VM已证路径、未知及目标职责并排；XML参数节点、GroupLink和视觉CLR控件分别标识，不能混计数量。当前未完成VM全部模板实例化的运行取证，不宣称VM也有31个对应控件。</div>
<nav class="br-nav"><a href="#br-controls-vm-compare">控件与组装</a><a href="#br-algorithm-before-after">算法逻辑调整</a><a href="#br-output-before-after">输出调整</a><a href="#br-tool-project-compare">工具工程结构</a></nav>
<h4 id="br-controls-vm-compare">VM / 我们 / 目标：控件能力与实际调用对照</h4>
<div class="br-grid"><div class="br-card"><b>VM已证组装链</b><p>Blob AlgorithmTab声明 → GroupLinkItem展开公共组 → AlgorithmTabRoot参数节点 → 通用DataTemplate → 参数/ROI控件 → Render桥</p></div><div class="br-card"><b>我们当前链</b><p>Blob手写XAML → 公共编辑控件/Manager → 页面连接主ImageControl → 参数与结果</p></div><div class="br-card"><b>推荐职责</b><p>工具业务/字段声明 → 宿主注入编辑与绘图能力 → 公共控件作用于私有草稿 → 正式发布/运行 → 只读结果消费</p></div></div>''']
 out.append(table(['能力维度','VM实际证据 / 未确认边界','我们实际可达类型（全部31项映射）','推荐与取舍'],rows))
 out.append('<h4 id="br-algorithm-before-after">算法逻辑到底怎么调整：当前代码与拟议改变分开</h4>')
 out.append(table(['阶段','VM证据边界','我们当前实际代码','本轮建议 / 已实施辨别'],[
 ('输入/ROI/预处理','输入/ROI/Mask与样例调用已证，原ProcessAlg内部完整次序未知。','Run先GetHalconImage（在try外）；图像转灰，HalconRegionPipeline构建作用域，ReduceDomain和可选预处理。S3。','必须调整失败清理边界与输入所有权；处理顺序按实际业务/有效域验证，不能仅按旧注释或VM样例重排。未改产品。'),
 ('阈值/形态学/连通','SDK有单/双/自动/软阈值及4/8邻域声明，核心谓词未知。','实际Threshold(min,max)，BinaryInv在有效域内反选，可选形态学后再限制有效域，Connection。','保持HALCON方案，明确支持模式/端点/邻域/孔洞和边界；本轮不补全部VM软阈值，不靠全局set_system改变别的工具。'),
 ('筛选/特征/判断','VM有筛选/排序配置与BlobInfo结果接口，核心选择次序未知。','SelectShape原生area/circularity/rectangularity组合筛选；对候选批提面积/外接矩形/圆度等；JudgmentPassed标NG但不直接删除。','复用先减少候选的机制；区分统计、输出、显示和判定需求，不宣称每项都已按需计算。页首旧注释仍写“判断通过才保留”，与实际循环不同，以函数体为准并列后续清理。'),
 ('排序/轮廓/统计','VM输出开关和多段轮廓已证，排序并列规则未知。','最终集合映射区域，已有按需轮廓与批量显示路径；灰度统计在当前代码里仍执行。','明确最终集合、排序、标签与元素地址关系；按真实消费者需求规划特征/统计，不承诺关显示就省全部算法成本；已实现机制复用验收。'),
 ]))
 out.append('<h4 id="br-output-before-after">输出调整：语义、数据合同和渲染投影</h4>')
 out.append(table(['输出方面','VM','我们当前 / 推荐调整'],[
 ('Blob图像 vs原始灰度','SDK分别有BlobImage（目标连通域绘制）与BinaryImage。','当前OutputBlobImage控制GrayImage原灰度克隆，不等同VM BlobImage。必须明确本地字段含义与命名/兼容迁移；不把改名当已生成等价图像，也不强制新增VM每种输出。'),
 ('记录与平行数组','代码生成按同一BlobInfo[index]取中心/矩形等。','BlobOutputContract已有Sequence<Record>、稳定FieldId；平行数组由FilteredBlobs派生。复用该成果，继续保证同集合和同运行，不增第二权威。'),
 ('筛选与业务NG','VM核心NG处理次序未知，不能据界面推断。','当前NG仍在FilteredBlobs，OK/NG区域分开，Label与ElementId含义不同。保留可解释合同，明确排序后显示序号与运行内身份；不声称跨帧追踪。'),
 ('轮廓/区域与空间类型','SDK支持SequentialContours；数量/公式需独验。','当前BlobResult.Contour单Point[]且使用WPF几何；IVisionRegion也未表达完整集合/孔洞。需按业务明确多段与孔洞表示并接既有中立几何合同；替换会丢信息的消费路径，不要求所有输出转成整图mask。'),
 ('生命周期/失败','Clear后不可续读、GetResult失败返回NULL为SDK合同。','复用ResultResourceOwner与reader；明确未请求输出、空结果、算法失败及资源退休状态，旧树/旧图/下游不能各保一份成功真值。'),
 ]))
 out.append('<h4 id="br-tool-project-compare">工具工程结构：需要优化职责与依赖，不按VM DLL数量照搬</h4>')
 out.append(table(['结构层次','VM原Blob实际结构','我们当前结构','架构推荐 / 本轮边界'],[
 ('插件/算法入口','native IMVSBlobFindModu.dll、托管Cs入口及Algorithm/AlgorithmTab/Display等声明；无Blob专属Control DLL。V8。','单一net9.0-windows/UseWPF工具工程，入口同时实现IToolPlugin与IToolEditorFactory，直接new两种Views。S3/S7。','运行和可选UI注册职责分离是必要方向；先复用现有Contracts/Runtime/Plugin.Wpf与发现机制，不新增一套接口，也不因VM拆DLL就一刀切每工具拆同样数量工程。'),
 ('业务算法实现','原模块→MVDBlobFindCpp→MVD_Algorithm依赖已证，dispatcher/核心体仍有缺口。','BlobAnalysisTool同时含HALCON处理、业务判定、显示项构建与WPF几何；专用HalAdapter已存在。','业务规则归工具，原生算法及转换集中在实现/适配边界；显示投影可用中立描述，由宿主/渲染消费者执行。未来OpenCV换实现，不重建公共UI。'),
 ('参数/控件装配','AlgorithmTab声明公共GroupLink，解析器展开，通用模板映射；有可选专属Control加载通道。V1–V4。','手写Blob XAML引用ConfigSetting/ParamBinding/ROIManager等，页面负责视口及执行接线。S1/S2。','采用能力声明+受限上下文+可选专业View。重做越界接线，视觉原语可独立复用；不全量复制VM XML/触发器系统。'),
 ('结果/分析View','原结果API与Display输出声明分开；具体Blob数据表装配未贯通。','Results、BlobOutputContract与Views/BlobDataAnalysisControl同工程，运行实例工厂依赖FrameworkElement。','结果合同与只读消费者分离；改以稳定字段/运行身份接入分析，不由表格掌握结果与native资源。目标是无UI运行不必引用WPF视图，不以搬文件夹代替验收。'),
 ('依赖与发布','原模块/SDK/公共控件分布式部署，当前静态匹配不等于实际加载路径已证。','工具HALCON HintPath仍指向dev/Debug/halcondotnetxl.dll；Private=false，依赖由宿主发布治理。S7。','本轮纳入构建依赖来源审查，采用权威SDK/发布输入，避免Debug目录成隐式前提；只核所需改动，发布依赖沿在途既有规则，不另造加载器。'),
 ]))
 out.append('<p><b>整体结论：</b>先确定消费者/业务合同和所有权，再决定复用或替换。控件、算法和工程不能分别做三套局部方案；本节与 <a href="#br-blob-unified-scope">B1–B7完整闭环范围</a> 是同一方案。以上算法/输出/工程调整均为本轮待审建议，现有代码与有限验收事实另列，未进行产品修改。</p><details><summary>VM与本方直接证据索引 / SHA快照</summary><ul>')
 for key,path in SOURCES.items():
  url=os.path.relpath(path,PAGE.parent).replace('\\','/')
  out.append('<li>'+key+' · <a href="'+E(url)+'">'+E(str(path))+'</a></li>')
 out.append('</ul></details><!-- BLOB-CONTROL-COMPARISON:END -->')
 from runpy import run_path
 merger=run_path(str(ROOT/'artifacts/analysis/vm-control-blob-document-merge-20261024/merge_controls_h4.py'))['merge_controls_h4']
 return merger('\n'.join(out).encode('utf-8')).decode('utf-8')

def apply():
 OUT.mkdir(parents=True,exist_ok=True)
 data=PAGE.read_bytes();page=data.decode('utf-8')
 a=page.index('<h3 id="br-blob-controls">');b=page.index('<h3 id="br-region-result-rendering">',a)
 old=page[a:b]
 heading,body=old.split('</h3>',1)
 if '<!-- BLOB-CONTROL-COMPARISON:BEGIN -->' in body:
  body=body.split('<details data-blob-self-inventory><summary>本方31项静态类型明细（用于追溯，不替代VM对照）</summary>',1)[1].rsplit('</details>',1)[0]
 new=heading+'</h3>'+render()+'<details data-blob-self-inventory><summary>本方31项静态类型明细（用于追溯，不替代VM对照）</summary>'+body+'</details>\n'
 PAGE.write_bytes((page[:a]+new+page[b:]).encode('utf-8'))
 report={'scope':'declaration/template/source evidence, not runtime closure; no product modification','selfTypesMapped':31,'capabilityRows':10,'pageBefore':sha256(data).hexdigest(),'pageAfter':sha256(PAGE.read_bytes()).hexdigest(),'outsideControlsSectionUnchanged':True,'sources':[{'id':k,'path':str(p),'sha256':sha256(p.read_bytes()).hexdigest()} for k,p in SOURCES.items()]}
 (OUT/'review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k!='sources'},ensure_ascii=False))

if __name__=='__main__':apply()
