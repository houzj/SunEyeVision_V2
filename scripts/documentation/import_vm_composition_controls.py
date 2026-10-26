"""Import the VM composition/style lane; unresolved vendor contracts stay explicit.

No build, runtime test, replacement drawing, or synthetic implementation is performed here.
The other three source lanes own numeric, input and display controls respectively.
"""
from pathlib import Path
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
VM = Path('D:/MyWork/REA逆向测试/analysis/decompiled')
DEST = ROOT / 'src/Plugin.Wpf'
PREFIX = 'SunEyeVision.Plugin.Wpf.Native.Composition.'
NUMERIC = re.compile(r'numeric|number|slider|digit|decimal|rangerule|symboltype', re.I)
ASSET = {'.png', '.jpg', '.jpeg', '.bmp', '.gif', '.ico', '.cur', '.ttf', '.otf'}
entries = []
type_map = {}
manifest_path = DEST / 'Controls/Native/Composition/composition-migration-manifest.json'
previous = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {}
previous_by_source = {row['source']:row for row in previous.get('entries',[])}
numeric_manifest = json.loads((DEST / 'Controls/Native/Numeric/numeric-migration-manifest.json').read_text(encoding='utf-8-sig'))
numeric_types = {entry['source_type'] for entry in numeric_manifest['type_map']}
delegated = []

def record(source, target, kind, dependencies=()):
    row = {'source': str(source), 'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                    'target': target.relative_to(ROOT).as_posix(), 'target_sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                    'kind': kind, 'unresolved_dependencies': sorted(set(dependencies))}
    prior = previous_by_source.get(str(source),{})
    for key in ('admission_seed_dependencies','admission_previous_target'):
        if key in prior:row[key]=prior[key]
    entries.append(row)

def namespace(value):
    return PREFIX + value if value.startswith(('Apps.XmlUI', 'VisionMaster.ParamTab')) else value

for library in ('Apps.XmlUI', 'Apps.Style'):
    source_root = VM / library
    for source in sorted(source_root.rglob('*')):
        if not source.is_file():
            continue
        relative = source.relative_to(source_root)
        if 'Properties' in relative.parts or source.name.endswith('.csproj'):
            continue
        if library == 'Apps.XmlUI' and NUMERIC.search(source.name):
            continue  # This exact source family is owned by the numeric lane.
        if source.suffix.lower() in ASSET:
            target = DEST / 'Themes/Assets/Native/Composition' / library / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            record(source, target, 'original_asset')
            continue
        if source.suffix not in ('.cs', '.xaml'):
            continue
        text = source.read_text(encoding='utf-8-sig')
        declared_namespace = re.search(r'namespace\s+([\w.]+)', text)
        declared_names = re.findall(r'\b(?:class|struct|enum|interface)\s+(\w+)', text)
        if declared_namespace and declared_names and declared_namespace[1] + '.' + declared_names[0] in numeric_types:
            delegated.append(str(source))
            continue
        external = set()
        if source.suffix == '.cs':
            declared = re.search(r'namespace\s+([\w.]+)', text)
            if declared:
                old = declared.group(1)
                for name in re.findall(r'\b(?:class|struct|enum|interface)\s+(\w+)', text):
                    type_map[old + '.' + name] = namespace(old) + '.' + name
            for used in re.findall(r'^using\s+([\w.]+)\s*;', text, re.M):
                if not used.startswith(('System', 'Microsoft.Windows.Themes', 'Apps.XmlUI', 'VisionMaster.ParamTab', 'HikPcUI', 'VMControls', 'VM.Controls', 'VMBaseControls')):
                    external.add(used)
            text = re.sub(r'\b(namespace|using)\s+((?:Apps\.XmlUI|VisionMaster\.ParamTab)[\w.]*)',
                          lambda m: m[1] + ' ' + namespace(m[2]), text)
            target = DEST / 'Controls/Native/Composition' / library / relative
        else:
            for assembly in re.findall(r'assembly=([^";]+)', text):
                if not assembly.startswith(('System', 'Presentation', 'WindowsBase', 'mscorlib', 'Apps.XmlUI', 'Apps.Style', 'VM.Controls', 'VMControls', 'VMBaseControls', 'VM.PcUI')):
                    external.add(assembly)
            text = re.sub(r'clr-namespace:((?:Apps\.XmlUI|VisionMaster\.ParamTab)[\w.]*)', lambda m: 'clr-namespace:' + namespace(m[1]), text)
            text = re.sub(r'x:Class="((?:Apps\.XmlUI|VisionMaster\.ParamTab)[\w.]*)"', lambda m: 'x:Class="' + namespace(m[1]) + '"', text)
            target = DEST / 'Themes/Native/Composition' / library / relative
        # Keep original vendor-contract-bound pages separate from usable standalone controls.
        # A .pending file is explicitly NOT a compiled/migrated/accepted control.
        if source.name == 'ROIStackControl.cs':
            # 只开放已按原件补齐命令库的纯视觉组合基类，不放行厂商参数模型。
            external.discard('Mvvm.Commands')
            old_registration = 'DependencyProperty.Register("IsShow", typeof(IROI), typeof(ROIStackControl), new PropertyMetadata(null))'
            assert old_registration in text, 'ROIStackControl来源已变化，先复核布尔依赖属性适配'
            text = text.replace(old_registration, 'DependencyProperty.Register("IsShow", typeof(bool), typeof(ROIStackControl), new PropertyMetadata(false))')
        prior=previous_by_source.get(str(source),{})
        if 'admission_previous_target' in prior:
            # A previous closure block needs an explicit adapter/promotion, not a silent reactivation on import.
            external.update(prior.get('unresolved_dependencies',[]))
        if external:
            target = Path(str(target) + '.pending')
        for assembly in ('Apps.XmlUI', 'Apps.Style'):
            pattern = re.compile(r'/' + re.escape(assembly) + r';component/([^"\s<>]+)', re.I)
            def uri(match):
                path = match[1].replace('\\', '/')
                base = 'Themes/Assets/Native/Composition' if Path(path).suffix.lower() in ASSET else 'Themes/Native/Composition'
                return '/SunEyeVision.Plugin.Wpf;component/' + base + '/' + assembly + '/' + path
            text = pattern.sub(uri, text)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding='utf-8')
        record(source, target, 'pending_vendor_contract' if external else 'source_port', external)

manifest = {'group': 'composition', 'baseline': 'b87fb685', 'phase': 'source_migration_only_no_build_or_tests',
            'namespace_prefix': PREFIX, 'entries': entries, 'type_map': type_map, 'delegated_to_numeric': delegated,
            'not_claimed': ['pending is not compiled', 'source port is not a runtime result', 'not full VM visual acceptance'],
            'next': 'Merge all lane type/resource maps, resolve cross-lane references and vendor contracts, then unified build/test/fix.'}
if 'admission_audit' in previous:manifest['admission_audit']=previous['admission_audit']
current_targets = {entry['target'] for entry in entries}
for old in previous.get('entries', []):
    if old['target'] in current_targets:
        continue
    stale = ROOT / old['target']
    allowed = (DEST / 'Controls/Native/Composition', DEST / 'Themes/Native/Composition', DEST / 'Themes/Assets/Native/Composition')
    if stale.exists() and any(stale.resolve().is_relative_to(folder.resolve()) for folder in allowed):
        if hashlib.sha256(stale.read_bytes()).hexdigest() == old['target_sha256']:
            stale.unlink()  # Only remove the unchanged output of this importer, never edited work.
path = manifest_path
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
from audit_vm_composition_closure import audit
audit(apply=True)
admitted_manifest = json.loads(path.read_text(encoding='utf-8'))
print(json.dumps({'files': len(admitted_manifest['entries']), 'declared_types': len(type_map), 'pending_vendor_contract_files': sum(e['kind'] == 'pending_vendor_contract' for e in admitted_manifest['entries'])}, ensure_ascii=False))
