"""Static Blob view/template dependency inventory; never loads WPF or the product."""
from pathlib import Path
import hashlib,json,re,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/analysis'
base=json.loads((OUT/'blob-shared-controls-inventory-20261024.json').read_text(encoding='utf-8'))
known={x['name']:dict(x) for x in base['controls']}
extras={
 'BlobAnalysisToolDebugControl':'tools/SunEyeVision.BlobAnalysis/Views/BlobAnalysisToolDebugControl.xaml.cs',
 'BlobDataAnalysisControl':'tools/SunEyeVision.BlobAnalysis/Views/BlobDataAnalysisControl.xaml.cs',
 'ImagePreviewControl':'src/UI/Shell/Controls/Panels/ImagePreviewControl.xaml.cs',
 'ImageSourceSettingsControl':'src/UI/Features/WorkflowUI/ImageSourceSettingsControl.xaml.cs',
}
for name,path in extras.items():
    p=ROOT/path
    if p.exists():known[name]=dict(name=name,source=path,role='节点周边实际消费者',recommendation='按所属应用会话和输出reader适配',design='核输入/输出身份、状态、异步及资源边界，不直接把现有接线认作正确')
theme=ROOT/'src/Plugin.SDK/Themes/Generic.xaml'
theme_tree=ET.fromstring(theme.read_text(encoding='utf-8-sig'))
styles={}
for el in theme_tree.iter():
    target=el.attrib.get('TargetType','')
    m=re.search(r'(?:x:Type\s+)?(?:\w+:)?(\w+)\}?$',target)
    if el.tag.split('}')[-1]=='Style' and m:styles.setdefault(m.group(1),[]).append(el)
wpf_ns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
def descendants(el):
    custom=set();native=set();unknown=set()
    for node in el.iter():
        if not isinstance(node.tag,str):continue
        uri,_,name=node.tag[1:].partition('}') if node.tag.startswith('{') else ('','',node.tag)
        if '.' in name:continue
        if name in known:custom.add(name)
        elif uri==wpf_ns:native.add(name)
        elif uri.startswith('clr-namespace:') and not any(t in name for t in ('Converter','Resource','Style','Key','Binding','Command')):
            unknown.add(name)
    return custom,native,unknown
roots=['BlobAnalysisToolDebugControl','BlobDataAnalysisControl']
host_roots=['ImageControl','ImagePreviewControl','DefaultDebugWindow','CustomDebugWindow']
queue=[(x,'Blob直接入口') for x in roots]+[(x,'节点关联宿主/视口，非声明同时创建') for x in host_roots]
seen={};edges=[];native=set();unknown=set();files={}
while queue:
    name,via=queue.pop(0)
    if name not in known:unknown.add(name);continue
    if name in seen:
        if via not in seen[name]['via']:seen[name]['via'].append(via)
        continue
    info=dict(known[name]);info['via']=[via];info['files']=[];seen[name]=info
    p=ROOT/info['source']
    paths=[p]
    if p.name.endswith('.xaml.cs'):paths.append(p.with_suffix(''))
    paths.extend(sorted(p.parent.glob(name+'.*.cs')))
    for path in dict.fromkeys(paths):
        if not path.exists():continue
        text=path.read_text(encoding='utf-8-sig');relative=path.relative_to(ROOT).as_posix()
        files[relative]=hashlib.sha256(path.read_bytes()).hexdigest();info['files'].append(relative)
        if path.suffix=='.xaml':
            custom,nat,unk=descendants(ET.fromstring(text));native.update(nat);unknown.update(unk)
            for child in custom-{name}:edges.append(dict(parent=name,child=child,kind='XAML实例'));queue.append((child,name+' / XAML'))
        else:
            for child in set(re.findall(r'\bnew\s+(\w+)\s*\(',text)) & set(known):
                if child!=name:edges.append(dict(parent=name,child=child,kind='代码创建'));queue.append((child,name+' / new'))
    parent=info.get('base','').strip().split(',')[0].split('.')[-1]
    if parent in known:edges.append(dict(parent=name,child=parent,kind='自研基类'));queue.append((parent,name+' / 基类'))
    for style in styles.get(name,[]):
        custom,nat,unk=descendants(style);native.update(nat);unknown.update(unk)
        for child in custom-{name}:edges.append(dict(parent=name,child=child,kind='默认模板'));queue.append((child,name+' / Generic模板'))
files[theme.relative_to(ROOT).as_posix()]=hashlib.sha256(theme.read_bytes()).hexdigest()
nonvisual=sorted(unknown & {'ImageRunMode','SortDescription','String'})
unknown-=set(nonvisual)
report=dict(kind='Blob-static-control-and-template-consumer-closure',entryRoots=roots,associatedHostRoots=host_roots,
    controls=list(seen.values()),count=len(seen),edges=edges,nativeWpfElements=sorted(native),nonVisualValueTags=nonvisual,unresolvedCustomTags=sorted(unknown),files=files,
    boundary='Static XAML, code new expressions, known custom bases, Generic.xaml target styles; includes declared host candidates, not simultaneous runtime instantiation. Reflection, custom external theme overrides and dynamic factory branches require runtime validation. No full-runtime or performance acceptance.',
    productModified=False,runtimeStarted=False)
(OUT/'blob-control-consumer-closure-20261024.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(controls=len(seen),dependencyEdges=len(edges),sourceFiles=len(files),unresolved=sorted(unknown)),ensure_ascii=False))
