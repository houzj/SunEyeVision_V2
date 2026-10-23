from pathlib import Path
import subprocess, json, hashlib, time, platform, sys

root = Path(__file__).resolve().parent
run = root / 'runs' / 'component-v2'
run.mkdir(parents=True, exist_ok=True)
if (run / 'run-summary.json').exists():
    raise SystemExit('Refusing to overwrite completed measurements')
def digest(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
files = list((root/'vm-binaries').glob('*.dll')) + list((root/'self-binaries').glob('*.dll'))
files += [root/'BenchReference.dll',root/'VmBenchReady.exe',root/'SelfBench/bin/Release/net9.0/SelfBench.dll']
files += list(root.glob('script-*.cs')) + [root/'VmBenchReady.cs',root/'SelfBench/Program.cs']
refs = json.loads((root/'references.json').read_text(encoding='utf-8'))
files += [Path(p) for p in refs]
manifest = {'scope':'isolated original components, NOT full application UI; v2 waits for VM reference readiness; 5 fresh processes per case',
 'environment':{'platform':platform.platform(),'python':sys.version,'processor':platform.processor()},
 'files':[{'path':str(p),'sha256':digest(p)} for p in files], 'references':refs,
 'sourceCases':[{'linesLabel':n,'path':str(root/f'script-{n}.cs'),'sha256':digest(root/f'script-{n}.cs')} for n in [100,1000,5000]],
 'definitions':{'processFirst':'new benchmark process, OS filesystem cache uncontrolled; NOT proven disk-cold',
 'completion':'same source and caret, explicit Ctrl+Space query; first + 20 repeated same-document queries',
 'compile':'first and two forced recompiles after language preparation; no application result cache, VM CodeDom file output vs Roslyn memory output',
 'open':'application UI not measured; service/document preparation is NOT window opening',
 'capability':'Append candidate required, member counts may differ; not full language capability equivalence'}}
(run/'input-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
rows=[]
for n in [100,1000,5000]:
 for trial in range(5):
  for side in (['vm','self'] if trial%2==0 else ['self','vm']):
   case=run/f'{side}-{n}-{trial:02}'
   case.mkdir(exist_ok=True)
   output=case/'result.json'
   if output.exists(): raise SystemExit('Refusing to overwrite '+str(output))
   prefix=[str(root/'VmBenchReady.exe')] if side=='vm' else ['dotnet',str(root/'SelfBench/bin/Release/net9.0/SelfBench.dll')]
   started=time.time()
   try:
    p=subprocess.run(prefix+[str(root),str(root/f'script-{n}.cs'),str(output)],cwd=root,capture_output=True,text=True,timeout=90,creationflags=0x08000000)
    row={'side':side,'lines':n,'trial':trial,'exit':p.returncode,'wallSeconds':time.time()-started,'path':str(output)}
    (case/'stdout.txt').write_text(p.stdout,encoding='utf-8');(case/'stderr.txt').write_text(p.stderr,encoding='utf-8')
    if output.exists():
     data=json.loads(output.read_text(encoding='utf-8'))
     row['valid']=bool(data.get('success')) and all(x['hasAppend'] for x in data.get('completion',[])) and len(data.get('completion',[]))==21 and all(x['success'] for x in data.get('compiler',[]))
    else: row['valid']=False
   except subprocess.TimeoutExpired:
    row={'side':side,'lines':n,'trial':trial,'valid':False,'timeout':True,'wallSeconds':time.time()-started}
   rows.append(row)
   (run/'progress.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
   print(json.dumps(row,ensure_ascii=True),flush=True)
manifestDrift=[str(p) for p in files if digest(p)!=next(x['sha256'] for x in manifest['files'] if x['path']==str(p))]
summary={'rows':rows,'valid':sum(x['valid'] for x in rows),'total':len(rows),'manifestDrift':manifestDrift,'applicationUiMeasured':False}
(run/'run-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'complete':True,'valid':summary['valid'],'total':len(rows),'drift':manifestDrift}),flush=True)
