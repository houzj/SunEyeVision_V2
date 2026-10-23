from pathlib import Path
import json, statistics, hashlib, math
root=Path(__file__).resolve().parent
run=root/'runs/component-v2'
summary=json.loads((run/'run-summary.json').read_text(encoding='utf-8'))
manifest=json.loads((run/'input-manifest.json').read_text(encoding='utf-8'))
def stats(values):
 return dict(n=len(values),medianMs=statistics.median(values),minMs=min(values),maxMs=max(values)) if values else None
rows=[];loaded={}
for n in [100,1000,5000]:
 for side in ['vm','self']:
  cases=[x for x in summary['rows'] if x['side']==side and x['lines']==n]
  good=[json.loads(Path(x['path']).read_text(encoding='utf-8')) for x in cases if x['valid']]
  for g in good:
   for f in g['loaded']:
    if f and Path(f).exists():loaded[f]=hashlib.sha256(Path(f).read_bytes()).hexdigest()
  rows.append(dict(side=side,nominalLines=n,actualLines=len((root/f'script-{n}.cs').read_text(encoding='utf-8').splitlines()),
   processTrials=len(cases),validTrials=len(good),
   service=stats([g['serviceMs'] for g in good]),
   referenceReadyWait=stats([g['referenceReadyWaitMs'] for g in good]) if side=='vm' else None,
   sourcePreparation=stats([g['parseMs'] if side=='vm' else g['documentMs'] for g in good]),
   semantic=stats([g['semanticMs'] for g in good]) if side=='self' else None,
   firstCompletion=stats([g['completion'][0]['ms'] for g in good]),
   repeatCompletion=stats([q['ms'] for g in good for q in g['completion'][1:]]),
   firstCompileAfterEditor=stats([g['compiler'][0]['ms'] for g in good]),
   forcedRepeatCompile=stats([c['ms'] for g in good for c in g['compiler'][1:]])))
report=dict(id='SCRIPT-COMPONENT-BASELINE-01',scope='原版组件隔离基线，非两软件完整窗口基线',
 complete=summary['valid']==summary['total'] and not summary['manifestDrift'],rows=rows,
 evidence={'inputManifest':str(run/'input-manifest.json'),'runSummary':str(run/'run-summary.json'),'rawResults':str(run),'loadedIdentities':loaded},
 measurements={'originalComponentCases':summary['total'],'validCases':summary['valid'],
 'applicationOpen':{'state':'blocked','reason':'VM引导窗口捕获两次超时、可访问性无文本；重开后仍失败，未操作未知窗口'},
 'applicationCompletionPopup':{'state':'not-measured','reason':'没有原软件触发到弹窗可见的可靠计时'},
 'applicationCompileResult':{'state':'not-measured','reason':'没有原软件提交到结果展示的可靠计时'},
 'componentPreparation':'measured','componentCompletion':'measured','compilerEngine':'measured'},
 limitations=[
 '新进程首次不等于操作系统磁盘冷缓存；没有清空系统缓存。每个尺寸每边5个新进程，规模有限，不报告总体P95。',
 '同进程20次查询用同一文档和光标，代表重复查询，不代表连续编辑、重开窗口或新节点。',
 'VM采用原件CSharpCompletion，已等待projectContent元数据引用计数达到4；这不是原软件公开就绪协议，也不含窗口。额外ProcessInput单列，不认定实际UI必经。',
 '自研测量使用Release内同SHA Roslyn程序集的AdhocWorkspace，不是CommonControl完整UI及脚本窗口；未覆盖全文逐行分类和弹窗渲染。',
 '编译是VM实际技术栈CodeDom与同Release Roslyn直接引擎；不是ScriptCom/SunEyeVision ScriptCompiler完整调用。相同脚本和4个同路径同SHA引用，VM输出文件、Roslyn输出内存；宿主包装/API与应用缓存未比较。',
 '首次编译测点在编辑语言服务与补全查询后，不是全新进程只编译的绝对冷启动。后两次强制重编，不是软件编译结果缓存命中。',
 '两边都要求预期Append候选与编译成功；候选总数存在差别，不证明全语言能力等价。',
 'v1及smoke为测量入口调试，空补全和并发环境样本全部排除正式比较，原始失败仍保留。',
 '用户观察VM实际界面更快仍作为反馈保留；组件数据不能反推或否定该体验。未改产品、未调系统设置或安装目录。'],
 recommendation={'confirmed':'基础首次准备与首次有效查询有可测成本；不能用VM构造返回快替代引用就绪时间，也不能简单取消Roslyn基础准备。',
 'designEvidence':'全文Show前门槛、取消未下传、缓存范围冲突和编译计时缺段沿原静态证据保留；本批未量化它们对UI的实际占比。',
 'next':'先补软件端到端打开/输入到弹窗/编译结果计时，及当前自研完整控件分类与取消链；之后冻结整批施工范围。组件替换暂不推荐，生命周期语义不自动变更。'},
 productModified=False,implementationApproved=False)
(root/'component-baseline.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'complete':report['complete'],'cases':summary['total'],'valid':summary['valid'],'rows':rows},ensure_ascii=True),flush=True)
