"""Recover the real visual contracts/converters needed by migrated VM controls.

Only static ILSpy decompilation; no VM assembly execution or copied vendor DLLs.
REA artifact/member evidence authenticates the explicit input identities below.
"""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT=Path(__file__).resolve().parents[2]
INSTALL=Path('D:/Program Files/VisionMaster4.4.0/Applications/myLibs')
ILSPY=Path('D:/MyWork/REA逆向测试/.tools/ilspycmd.exe')
TARGET=ROOT/'src/Plugin.Wpf/Controls/Native/Dependencies'
EXPORT=ROOT/'artifacts/analysis/vm-visual-dependencies-20261026'
artifacts=[];entries=[];types={}
manifest_path=TARGET/'dependencies-migration-manifest.json'
previous=json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {}
cached_sources={entry['source']:entry['source_sha256'] for entry in previous.get('entries',[])}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def port(text):
    for old in ('VMControls.RenderInterface','Apps.BaseData','Apps.UIHelper.Extern','VM.Utility'):
        text=re.sub(r'(?<![\w.])'+re.escape(old)+r'(?!\w)','SunEyeVision.Plugin.Wpf.Native.Dependencies.'+old,text)
    return text
def save(source,target,original,artifact):
    namespace=re.search(r'namespace\s+([\w.]+)',original)
    if namespace:
        # These exports use file-scoped namespaces; retain the real type names.
        for name in re.findall(r'^public\s+(?:(?:abstract|sealed|static|readonly)\s+)*(?:class|interface|enum|struct|delegate\s+\w+)\s+(\w+)',original,re.M):
            old=namespace[1]+'.'+name;types[old]='SunEyeVision.Plugin.Wpf.Native.Dependencies.'+old
    target.parent.mkdir(parents=True,exist_ok=True);target.write_text(port(original),encoding='utf-8')
    entries.append({'source':str(source),'artifact':str(artifact),'source_sha256':hashlib.sha256(original.encode()).hexdigest(),'target':target.relative_to(ROOT).as_posix(),'target_sha256':sha(target),'kind':'source_port','adaptations':['namespace only; original converter/interface bodies retained']})
for name,expected,evidence in [
 ('VMControls.RenderInterface','67d5988152401745e160f0e6a0dd257a3451c519f7740334887bacd202622ac2','ev_18c3872c6efc942f0bf47b30062a476770eabaf60d2bd4ff0af675265516a8df'),
 ('Apps.BaseData','435628fd494d33dbd4c242d98939162e0406101b1e77287556b1a04c7a4e1d6a','ev_8c68ba52e71fbaedc0819c39a328f1c8525bb9db7f3589089f7b7d59123a57b5'),
 ('Apps.UIData','d063decd9f6a5bf6992b37b26416b46e0a76e715973b077f0cc281eaf36f2b24','ev_0ca6f0a7a59062067334e0c7e4346690cbe017465b65fb95a5c76ade33f34d42'),
 ('Apps.UIHelper','be53f11087a4bcd338530edc283ca49a37d595bfd1a64c6f67232c007cfa189c','ev_9b0a9ef9796730075b30bb07123f5ad5f184a3f55490968bf266c5602d2d321b'),
 ('VM.Utility','65ebb5edabf77465fa01f8c6d70efdcbd4b6a87f510911249cd93d73649d1cdf','ev_ff90a25ff7174a267deac758048e4cc18f99ed9a1b9aa7d73434aad58f4ed551')]:
    artifact=INSTALL/(name+'.dll');assert sha(artifact)==expected,artifact
    artifacts.append({'path':str(artifact),'sha256':expected,'rea_evidence':evidence,'executed':False})
    out=EXPORT/name
    if name=='VMControls.RenderInterface':
        out.mkdir(parents=True,exist_ok=True)
        cached=[Path(entry['source']) for entry in previous.get('entries',[]) if entry.get('artifact')==str(artifact)]
        if not cached or not all(source.exists() and sha(source)==cached_sources.get(str(source)) for source in cached):
            subprocess.run([str(ILSPY),'-p','-o',str(out),str(artifact)],check=True,capture_output=True)
        for source in sorted(out.rglob('*.cs')):
            if source.name=='AssemblyInfo.cs':continue
            save(source,TARGET/name/source.relative_to(out),source.read_text(encoding='utf-8-sig'),artifact)
    else:
        selected=('UIExtern','ObjectEventArgs','ObjectEventHandler') if name=='Apps.UIHelper' else (('DynamicResourceDictionary',) if name=='Apps.UIData' else ('NormalMultiplicationConverter','RightFixdMultiplicationConverter','RightFixdMultiplicationForCaliperConverter','PointRightFixdMultiplicationConverter','PointListRightFixdMultiplicationConverter','TypeFilterConverter','MulParamsMultiplicationConverter','NULLJudgeConverter'))
        source_namespace='Apps.UIHelper.Extern.' if name=='Apps.UIHelper' else 'Apps.BaseData.'
        if name=='VM.Utility':
            selected=('DelegateCommand',)
            source_namespace='VM.Utility.'
        for short in selected:
            source=out/(short+'.cs')
            if source.exists() and sha(source)==cached_sources.get(str(source)):
                original=source.read_text(encoding='utf-8')
            else:
                result=subprocess.run([str(ILSPY),'-t',source_namespace+short,str(artifact)],check=True,capture_output=True,encoding='utf-8-sig')
                original=result.stdout;source.parent.mkdir(parents=True,exist_ok=True);source.write_text(original,encoding='utf-8')
            save(source,TARGET/name/source.name,original,artifact)
TARGET.mkdir(parents=True,exist_ok=True)
(TARGET/'dependencies-migration-manifest.json').write_text(json.dumps({'phase':'static_reconstruction_not_runtime_acceptance','artifacts':artifacts,'entries':entries,'type_map':types,'not_claimed':['Not a second workflow or algorithm model','No vendor DLL deployed','Compilation and host consumption still require verification']},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'files':len(entries),'types':len(types),'artifacts':len(artifacts)}))
