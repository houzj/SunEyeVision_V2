"""Audit source/XAML admission as a dependency closure, preserving all candidate source.

An unavailable source family remains a migration obligation, never an implemented
control. --apply only moves newly blocked files to .pending if no admitted static
consumer outside this family refers to those types. No implementation is removed.
"""
from pathlib import Path
import argparse,hashlib,json,posixpath,re,subprocess
import xml.etree.ElementTree as ET
from import_vm_input_controls import declared_types

ROOT=Path(__file__).resolve().parents[2]
MANIFEST=ROOT/'src/Plugin.Wpf/Controls/Native/Composition/composition-migration-manifest.json'
PREFIX='SunEyeVision.Plugin.Wpf.Native.Composition.'
ALLOWED=(ROOT/'src/Plugin.Wpf/Controls/Native/Composition',ROOT/'src/Plugin.Wpf/Themes/Native/Composition')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def stripped(text):
    return re.sub(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"',lambda m:' '*len(m[0]),text)

def audit(apply=False):
    data=json.loads(MANIFEST.read_text(encoding='utf-8-sig'))
    nodes={};types={};classes={};resources={}
    for row in data['entries']:
        source=Path(row['source'])
        if source.suffix not in ('.cs','.xaml'):continue
        assert sha(source)==row['source_sha256'], 'Original source changed: '+str(source)
        text=source.read_text(encoding='utf-8-sig')
        key=row['source'];namespace=re.search(r'\bnamespace\s+([\w.]+)',text)
        node={'row':row,'text':text,'namespace':namespace[1] if namespace else None,'edges':set(),
              'blocked':list(row.get('admission_seed_dependencies',row.get('unresolved_dependencies',[])))}
        if row['target'].endswith('.pending') and not node['blocked']:
            node['blocked']=['Prior pending admission requires an explicit adapter']
        nodes[key]=node
        if source.suffix=='.cs' and namespace:
            for name in declared_types(text,namespace[1]):types[name]=key
        else:
            tree=ET.fromstring(text)
            cls=tree.get('{http://schemas.microsoft.com/winfx/2006/xaml}Class')
            if cls:classes[cls]=key
        library=next((name for name in ('Apps.XmlUI','Apps.Style') if name in source.parts),None)
        if library:
            index=source.parts.index(library)
            resources[(library.casefold(),'/'.join(source.parts[index+1:]).casefold())]=key
    for key,node in nodes.items():
        source=Path(key);text=node['text'];edges=node['edges']
        if source.suffix=='.cs':
            code=stripped(text);uses=set(re.findall(r'^using\s+([\w.]+)\s*;',code,re.M))
            tokens=set(re.findall(r'\b[A-Za-z_]\w*\b',code))
            for name,target in types.items():
                ns,short=name.rsplit('.',1)
                if target!=key and short in tokens and (ns==node['namespace'] or ns in uses or name in code):edges.add(target)
            for name in declared_types(text,node['namespace']) if node['namespace'] else ():
                if name in classes:
                    edges.add(classes[name]);nodes[classes[name]]['edges'].add(key)
        else:
            aliases=dict(re.findall(r'xmlns:(\w+)="clr-namespace:([^";]+)',text))
            for alias,namespace in aliases.items():
                for name in re.findall(r'(?<![\w.])'+re.escape(alias)+r':(\w+)',text):
                    if namespace+'.'+name in types:edges.add(types[namespace+'.'+name])
            for reference in re.findall(r'\bSource="([^"]+\.xaml)"',text):
                match=re.match(r'(?:pack://application:,,,)?/([^;]+);component/(.+)',reference,re.I)
                if match:lookup=(match[1].casefold(),match[2].replace('\\','/').casefold())
                else:
                    library=next((name for name in ('Apps.XmlUI','Apps.Style') if name in source.parts),None)
                    if not library:continue
                    index=source.parts.index(library)
                    rel='/'.join(source.parts[index+1:-1])
                    lookup=(library.casefold(),posixpath.normpath(posixpath.join(rel,reference.replace('\\','/'))).casefold())
                if lookup in resources:edges.add(resources[lookup])
    blocked={key for key,node in nodes.items() if node['blocked']}
    while True:
        updated=blocked|{key for key,node in nodes.items() if node['edges']&blocked}
        if updated==blocked:break
        blocked=updated
    changes=[key for key in sorted(blocked) if not nodes[key]['row']['target'].endswith('.pending')]
    unavailable_types={PREFIX+name:nodes[key]['row']['target'] for name,key in types.items() if key in changes}
    consumers=[]
    files=subprocess.run(['rg','--files','src','tools','-g','*.cs','-g','*.xaml','-g','!**/obj/**','-g','!**/bin/**'],cwd=ROOT,check=True,capture_output=True,text=True).stdout.splitlines()
    for relative in files:
        path=ROOT/relative
        if any(path.resolve().is_relative_to(folder.resolve()) for folder in ALLOWED):continue
        text=path.read_text(encoding='utf-8-sig');code=stripped(text) if path.suffix=='.cs' else text
        uses=set(re.findall(r'^using\s+([\w.]+)\s*;',code,re.M))
        own=re.search(r'\bnamespace\s+([\w.]+)',code)
        aliases=dict(re.findall(r'xmlns:(\w+)="clr-namespace:([^";]+)',text)) if path.suffix=='.xaml' else {}
        for name in unavailable_types:
            ns,short=name.rsplit('.',1)
            direct=name in code
            imported=path.suffix=='.cs' and (ns in uses or own and own[1]==ns) and re.search(r'\b'+re.escape(short)+r'\b',code)
            xaml=any(namespace==ns and re.search(r'\b'+re.escape(alias)+':'+re.escape(short)+r'\b',text) for alias,namespace in aliases.items())
            if direct or imported or xaml:consumers.append({'file':relative,'type':name})
    report={'scope':'static admission closure; NOT feature migration, compilation, runtime or visual acceptance',
            'pending_before':sum(n['row']['target'].endswith('.pending') for n in nodes.values()),'newly_pending':len(changes),
            'static_external_consumers':consumers,'changes':[{'source':key,'target':nodes[key]['row']['target'],
                'direct_blocked_dependencies':[nodes[x]['row']['target'] for x in sorted(nodes[key]['edges']&blocked)]} for key in changes],
            'limits':['Dynamic/reflection/BAML-only consumers are not proved absent','No requirement removed; blocked source families still require adaptation against the original control list']}
    if apply:
        assert not consumers, 'Admitted static consumers require adaptation before changing source admission'
        for key in changes:
            row=nodes[key]['row'];old=ROOT/row['target'];target=Path(str(old)+'.pending')
            assert any(old.resolve().is_relative_to(folder.resolve()) for folder in ALLOWED)
            assert any(target.resolve().is_relative_to(folder.resolve()) for folder in ALLOWED)
            assert old.exists() and not target.exists(), 'Preserve both versions and review before admission: '+str(target)
        for key in changes:
            row=nodes[key]['row'];old=ROOT/row['target'];target=Path(str(old)+'.pending')
            old.rename(target)
            row['admission_seed_dependencies']=nodes[key]['blocked']
            row['admission_previous_target']=row['target']
            row['target']=target.relative_to(ROOT).as_posix();row['target_sha256']=sha(target)
            row['kind']='pending_vendor_contract'
            row['unresolved_dependencies']=['Dependency closure unavailable: '+nodes[x]['row'].get('admission_previous_target',nodes[x]['row']['target']) for x in sorted(nodes[key]['edges']&blocked)]
        if changes or 'admission_audit' not in data:
            data['admission_audit']={'scope':report['scope'],'newly_pending':len(changes),'static_external_consumers':consumers,'movements':report['changes'],'not_claimed':'Pending source is preserved, not an implemented feature or an exited old consumer.'}
        MANIFEST.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
        report['applied']=True
    output=ROOT/'Release/_validation/O06-RoiCore-20261026/composition-admission-audit-001.json'
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps({'newly_pending':len(changes),'external_static_consumers':len(consumers),'applied':apply,'report':str(output)}))
    return report

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--apply',action='store_true')
    audit(parser.parse_args().apply)
