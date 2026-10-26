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
artifacts=[];entries=[];types={};embedded_resources=[]
manifest_path=TARGET/'dependencies-migration-manifest.json'
previous=json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {}
cached_sources={entry['source']:entry['source_sha256'] for entry in previous.get('entries',[])}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def cache_matches(source):
    if not source.exists():return False
    expected=cached_sources.get(str(source))
    # C#来源哈希记录的是解码正文；Windows写入时的CRLF不能导致每轮重复反编译。
    if sha(source)==expected:return True
    return source.suffix=='.cs' and hashlib.sha256(source.read_text(encoding='utf-8-sig').encode()).hexdigest()==expected
def port(text):
    for old in ('VMControls.RenderInterface','Apps.BaseData','Apps.UIHelper.Extern','VM.Utility','Mvvm'):
        text=re.sub(r'(?<![\w.])'+re.escape(old)+r'(?!\w)','SunEyeVision.Plugin.Wpf.Native.Dependencies.'+old,text)
    return text
def save(source,target,original,artifact):
    namespace=re.search(r'namespace\s+([\w.]+)',original)
    if namespace:
        # These exports use file-scoped namespaces; retain the real type names.
        for name in re.findall(r'^(?:public|internal)\s+(?:(?:abstract|sealed|static|readonly)\s+)*(?:class|interface|enum|struct|delegate\s+\w+)\s+(\w+)',original,re.M):
            old=namespace[1]+'.'+name;types[old]='SunEyeVision.Plugin.Wpf.Native.Dependencies.'+old
    target.parent.mkdir(parents=True,exist_ok=True);target.write_text(port(original),encoding='utf-8')
    entries.append({'source':str(source),'artifact':str(artifact),'source_sha256':hashlib.sha256(original.encode()).hexdigest(),'target':target.relative_to(ROOT).as_posix(),'target_sha256':sha(target),'kind':'source_port','adaptations':['namespace only; original converter/interface bodies retained']})
for name,expected,evidence in [
 ('VMControls.RenderInterface','67d5988152401745e160f0e6a0dd257a3451c519f7740334887bacd202622ac2','ev_18c3872c6efc942f0bf47b30062a476770eabaf60d2bd4ff0af675265516a8df'),
 ('Apps.BaseData','435628fd494d33dbd4c242d98939162e0406101b1e77287556b1a04c7a4e1d6a','ev_8c68ba52e71fbaedc0819c39a328f1c8525bb9db7f3589089f7b7d59123a57b5'),
 ('Apps.UIData','d063decd9f6a5bf6992b37b26416b46e0a76e715973b077f0cc281eaf36f2b24','ev_0ca6f0a7a59062067334e0c7e4346690cbe017465b65fb95a5c76ade33f34d42'),
 ('Apps.UIHelper','be53f11087a4bcd338530edc283ca49a37d595bfd1a64c6f67232c007cfa189c','ev_9b0a9ef9796730075b30bb07123f5ad5f184a3f55490968bf266c5602d2d321b'),
 ('VM.Utility','65ebb5edabf77465fa01f8c6d70efdcbd4b6a87f510911249cd93d73649d1cdf','ev_ff90a25ff7174a267deac758048e4cc18f99ed9a1b9aa7d73434aad58f4ed551'),
 ('Mvvm','5685c13c79321e7d7ec4ed85e764d00c915ca54401d5ccb6314414d54f76e120','ev_6f9b6761f00a733d4aa20ae8f02e4174b6a42920de912495fe814194616485be')]:
    artifact=(INSTALL.parent/'3rdLib/MVVM/Mvvm.dll') if name=='Mvvm' else INSTALL/(name+'.dll')
    assert sha(artifact)==expected,artifact
    artifacts.append({'path':str(artifact),'sha256':expected,'rea_evidence':evidence,'executed':False})
    out=EXPORT/name
    if name in ('VMControls.RenderInterface','Mvvm'):
        out.mkdir(parents=True,exist_ok=True)
        cached=[Path(entry['source']) for entry in previous.get('entries',[]) if entry.get('artifact')==str(artifact)]
        if not cached or not all(cache_matches(source) for source in cached):
            subprocess.run([str(ILSPY),'-p','-o',str(out),str(artifact)],check=True,capture_output=True)
        for source in sorted(out.rglob('*.cs')):
            if source.name=='AssemblyInfo.cs':continue
            save(source,TARGET/name/source.relative_to(out),source.read_text(encoding='utf-8-sig'),artifact)
        for source in sorted(out.rglob('*.resx')):
            target=TARGET/name/source.relative_to(out)
            target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(source.read_bytes())
            entries.append({'source':str(source),'artifact':str(artifact),'source_sha256':sha(source),'target':target.relative_to(ROOT).as_posix(),'target_sha256':sha(target),'kind':'original_embedded_resource'})
            embedded_resources.append({'target':target.relative_to(ROOT).as_posix(),'logicalName':'SunEyeVision.Plugin.Wpf.Native.Dependencies.'+source.stem+'.resources'})
    else:
        selected=('UIExtern','UIHelper','ObjectEventArgs','ObjectEventHandler') if name=='Apps.UIHelper' else (('DynamicResourceDictionary',) if name=='Apps.UIData' else ('NormalMultiplicationConverter','RightFixdMultiplicationConverter','RightFixdMultiplicationForCaliperConverter','PointRightFixdMultiplicationConverter','PointListRightFixdMultiplicationConverter','TypeFilterConverter','MulParamsMultiplicationConverter','NULLJudgeConverter'))
        source_namespace='Apps.UIHelper.Extern.' if name=='Apps.UIHelper' else 'Apps.BaseData.'
        if name=='VM.Utility':
            selected=('DelegateCommand',)
            source_namespace='VM.Utility.'
        for short in selected:
            source=out/(short+'.cs')
            if cache_matches(source):
                original=source.read_text(encoding='utf-8')
            else:
                result=subprocess.run([str(ILSPY),'-t',source_namespace+short,str(artifact)],check=True,capture_output=True,encoding='utf-8-sig')
                original=result.stdout;source.parent.mkdir(parents=True,exist_ok=True);source.write_text(original,encoding='utf-8')
            save(source,TARGET/name/source.name,original,artifact)
TARGET.mkdir(parents=True,exist_ok=True)
(TARGET/'dependencies-migration-manifest.json').write_text(json.dumps({'phase':'static_reconstruction_not_runtime_acceptance','artifacts':artifacts,'entries':entries,'type_map':types,'embeddedResourcesForRootBuildIntegration':embedded_resources,'not_claimed':['Not a second workflow or algorithm model','No vendor DLL deployed','Compilation and host consumption still require verification']},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'files':len(entries),'types':len(types),'artifacts':len(artifacts)}))
