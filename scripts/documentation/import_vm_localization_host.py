"""迁入原语言资源和两个标记扩展；安装路径/全局状态改由窗口内宿主边界承接。"""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[2]
INSTALL=Path('D:/Program Files/VisionMaster4.4.0/Applications')
DLL=INSTALL/'myLibs/Apps.Localization.dll'
EXPECTED='ef43c79e30aaa5db98775569d7fe5f9b23f41af6280c7e4ed7cf5a902fcf2381'
ILSPY=Path('D:/MyWork/REA逆向测试/.tools/ilspycmd.exe')
RAW=ROOT/'artifacts/analysis/vm-visual-dependencies-20261026/Apps.Localization'
OUT=ROOT/'src/Plugin.Wpf/Controls/Native/Common/Localization'
NS='SunEyeVision.Plugin.Wpf.Native.Common.Localization'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(DLL)==EXPECTED
RAW.mkdir(parents=True,exist_ok=True);OUT.mkdir(parents=True,exist_ok=True)
MANIFEST=OUT.parent/'localization-host-manifest.json'
previous=json.loads(MANIFEST.read_text(encoding='utf-8')) if MANIFEST.exists() else {}
cached={row['source']:row['source_sha256'] for row in previous.get('entries',[])}
entries=[];types={}
for name in ('ResourceBinding','LangResource','AppLocalizationService','LocalizationResourceDictionary'):
    source=RAW/(name+'.cs')
    if not source.exists() or sha(source)!=cached.get(str(source)):
        result=subprocess.run([str(ILSPY),'-t','Apps.Localization.'+name,str(DLL)],check=True,capture_output=True,encoding='utf-8-sig')
        source.write_text(result.stdout,encoding='utf-8')
    target=OUT/(name+'.cs')
    if name in ('ResourceBinding','LangResource'):
        target.write_text(source.read_text(encoding='utf-8-sig').replace('namespace Apps.Localization;', 'namespace '+NS+';'),encoding='utf-8')
    else:
        assert target.exists(), '先提供并评审窗口级语言宿主适配：'+str(target)
    entries.append({'source':str(source),'source_sha256':sha(source),'target':target.relative_to(ROOT).as_posix(),'target_sha256':sha(target),'kind':'source_port' if name in ('ResourceBinding','LangResource') else 'explicit_host_adaptation'})
    types['Apps.Localization.'+name]=NS+'.'+name
for culture in ('zh-cn','en-us'):
    source=INSTALL/'Lang'/(culture+'.xaml')
    target=ROOT/'src/Plugin.Wpf/Themes/Native/Localization'/(culture+'.xaml')
    target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(source.read_bytes())
    entries.append({'source':str(source),'source_sha256':sha(source),'target':target.relative_to(ROOT).as_posix(),'target_sha256':sha(target),'kind':'original_language_data'})
manifest={'phase':'source_and_host_adaptation_not_runtime_acceptance','artifact':{'path':str(DLL),'sha256':EXPECTED,'rea_evidence':'ev_fa75ad7c5a1c32ec3fb63822416d2a6f92a27978e977ed42a3c495a866264a7c'},'entries':entries,'type_map':types,'boundary':['ResourceBinding/LangResource原算法仅改namespace；字典与查询服务为明确宿主适配','仅插件内部读取已迁入语言数据，不读写VM配置、不注入Application资源','当前迁入控件只承接GetLang/GetLangType/GetInternalLang查询；不宣称VM全局语言切换已恢复','未命中key保持原文字；内部字段标签可由宿主提供字典覆盖，不恢复厂商业务模型']}
MANIFEST.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'source_extensions':2,'host_adaptations':2,'original_language_dictionaries':2}))
