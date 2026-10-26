"""Unify the four source lanes' real type/namespace maps. This is not a runtime test."""
from pathlib import Path
from collections import defaultdict
import hashlib
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
NATIVE = ROOT / 'src/Plugin.Wpf/Controls/Native'
SPECS = [NATIVE / 'Numeric/numeric-migration-manifest.json', NATIVE / 'Input/migration-manifest.json',
         NATIVE / 'Display/display-migration-manifest.json', NATIVE / 'Composition/composition-migration-manifest.json']
if (NATIVE / 'Dependencies/dependencies-migration-manifest.json').exists():
    SPECS.append(NATIVE / 'Dependencies/dependencies-migration-manifest.json')
if (NATIVE / 'Common/localization-host-manifest.json').exists():
    SPECS.append(NATIVE / 'Common/localization-host-manifest.json')
types = {}
namespaces = defaultdict(set)
reverse = defaultdict(set)
compiled_namespaces=set()
manifest_ids = []
embedded_resources = []
raw_xaml_resources = []
host_receipt_path = NATIVE / 'Display/visual-host-adaptations.json'
host_receipt = json.loads(host_receipt_path.read_text(encoding='utf-8')) if host_receipt_path.exists() else None
protected_host = {row['target']:row for row in host_receipt['files']} if host_receipt else {}
host_headers_updated = False

def without_header_imports(text):
    namespace = re.search(r'\bnamespace\s+[\w.]+', text)
    end = namespace.start() if namespace else 0
    return re.sub(r'^using\s+[\w.]+;[^\S\n]*\n', '', text[:end], flags=re.M) + text[end:]

def path(value):
    return Path(str(value).replace('\\', '/'))

for manifest in SPECS:
    data = json.loads(manifest.read_text(encoding='utf-8-sig'))
    embedded_resources.extend(data.get('embeddedResourcesForRootBuildIntegration', []))
    raw_xaml_resources.extend(item for item in data.get('build_items_for_root', {}).get('Resource', []) if isinstance(item, str) and item.endswith('.xaml'))
    manifest_ids.append({'path': manifest.relative_to(ROOT).as_posix(), 'sha256': hashlib.sha256(manifest.read_bytes()).hexdigest()})
    mapping = data['type_map']
    if isinstance(mapping, list):
        mapping = {item.get('source_type', item.get('original_full_type')): item.get('target_type', item.get('target_full_type')) for item in mapping}
    # The dedicated source lane wins over a composition lane's duplicate support copy.
    for old, new in mapping.items():
        types.setdefault(old, new)
    rows = data.get('entries', data.get('files', []))
    for item in rows:
        source = item.get('source')
        target = item.get('target', item.get('output'))
        if not source or not target:
            continue
        source, target = path(source), path(target)
        if not target.is_absolute():
            target = ROOT / target
        if not source.exists() or not target.exists() or '.cs' not in source.suffixes:
            continue
        before = re.search(r'\bnamespace\s+([\w.]+)', source.read_text(encoding='utf-8-sig'))
        after = re.search(r'\bnamespace\s+([\w.]+)', target.read_text(encoding='utf-8-sig'))
        if before and after:
            namespaces[before[1]].add(after[1]); reverse[after[1]].add(before[1])
            if not target.name.endswith('.pending'):compiled_namespaces.add(after[1])

type_pattern = re.compile(r'(?<![\w.])(' + '|'.join(re.escape(key) for key in sorted(types, key=len, reverse=True)) + r')(?![\w])')
changed = []
unresolved = defaultdict(set)
roots = [NATIVE, ROOT / 'src/Plugin.Wpf/Themes/Native']
for folder in roots:
    for file in folder.rglob('*'):
        if not file.is_file() or not (file.name.endswith(('.cs', '.xaml', '.cs.pending', '.xaml.pending'))):
            continue
        old_text = file.read_text(encoding='utf-8-sig')
        text = old_text
        text = text.replace('clr-namespace:System.Windows.Interactivity;assembly=System.Windows.Interactivity',
                            'clr-namespace:Microsoft.Xaml.Behaviors;assembly=Microsoft.Xaml.Behaviors')
        text = text.replace('clr-namespace:Microsoft.Expression.Interactivity.Core;assembly=Microsoft.Expression.Interactions',
                            'clr-namespace:Microsoft.Xaml.Behaviors.Core;assembly=Microsoft.Xaml.Behaviors')
        if '.cs' in file.suffixes:
            text = text.replace('using System.Windows.Interactivity;', 'using Microsoft.Xaml.Behaviors;')
            text = text.replace('using Microsoft.Expression.Interactivity.Core;', 'using Microsoft.Xaml.Behaviors.Core;')
        if '.cs' in file.suffixes:
            text = type_pattern.sub(lambda m: types[m[1]], text)
            def imports(match):
                # 追加依赖后，已归一的using也要补齐同一原namespace拆出的新分组。
                originals = reverse.get(match[1], {match[1]})
                destinations = {target for original in originals for target in namespaces.get(original, ())}
                destinations = (destinations & compiled_namespaces) or destinations
                # Third-party WPF extensions share framework namespaces in the
                # original DLL. Their relocated classes supplement the actual
                # framework; they must not replace the framework using.
                if match[1].startswith('System.') and destinations:
                    destinations.add(match[1])
                return '\n'.join('using ' + name + ';' for name in sorted(destinations)) if destinations else match[0]
            text = re.sub(r'^using\s+([\w.]+)\s*;', imports, text, flags=re.M)
            own = re.search(r'\bnamespace\s+([\w.]+)', text)
            if own:
                all_peers = {peer for original in reverse.get(own[1], ()) for peer in namespaces[original] if peer != own[1]}
                peers=all_peers & compiled_namespaces
                for peer in all_peers-compiled_namespaces:
                    text=re.sub(r'^using\s+'+re.escape(peer)+r';\s*\n','',text,flags=re.M)
                missing = [peer for peer in sorted(peers) if not re.search(r'^using\s+' + re.escape(peer) + r'\s*;', text, re.M)]
                if missing:
                    text = ''.join('using ' + peer + ';\n' for peer in missing) + text
            # 两个已拆分的using再次展开时会得到同一组namespace；只保留文件头的一份。
            # 不跨namespace作用域去重，也不改alias/static using。
            namespace_start = re.search(r'\bnamespace\s+[\w.]+', text)
            header_end = namespace_start.start() if namespace_start else len(text)
            seen_imports = set()
            def unique_import(match):
                name = match[1]
                if name in seen_imports:return ''
                seen_imports.add(name)
                return match[0]
            text = re.sub(r'^using\s+([\w.]+);[^\S\n]*\n', unique_import, text[:header_end], flags=re.M) + text[header_end:]
            directives=re.findall(r'^#(?:define|undef)\s+\w+[^\S\n]*\n?',text,re.M)
            if directives and not re.search(r'^\s*#(?:if|elif|else)\b',text[:text.rfind(directives[-1])+len(directives[-1])],re.M):
                text=''.join(line.rstrip()+'\n' for line in directives)+re.sub(r'^#(?:define|undef)\s+\w+[^\S\n]*\n?','',text,flags=re.M)
            text=re.sub(r'\s*\[DebuggerNonUserCode\]\s*\[GeneratedCode\("PresentationBuildTasks",\s*"[^"]+"\)\]\s*internal Delegate _CreateDelegate\(Type delegateType, string handler\)\s*\{\s*return Delegate.CreateDelegate\(delegateType, this, handler\);\s*\}', '\n',text)
        else:
            # Same-project CLR types are resolved during WPF's local-type pass.
            # Naming the output assembly here makes pass one search the old DLL.
            text = re.sub(r'(clr-namespace:SunEyeVision\.Plugin\.Wpf\.Native[^";]*);assembly=SunEyeVision\.Plugin\.Wpf', r'\1', text)
            # Decompiler emits generic base owners as bare XAML types. Resolve
            # inherited DPs against the concrete template/style target instead.
            text = re.sub(r'(?<![\w.])(?:\w+:)?(?:UpDownBase|InputBase|DateTimeUpDownBase)\.(\w+)', r'\1', text)
            declarations = dict(re.findall(r'xmlns:(\w+)="clr-namespace:([^";]+)(?:;assembly=[^"]+)?"', text))
            additions = {}
            for alias, declared in declarations.items():
                original = reverse.get(declared, {declared})
                def qname(match):
                    name = match[1]
                    segments = name.split('.')
                    for length in range(len(segments), 0, -1):
                        candidates = {types[ns + '.' + '.'.join(segments[:length])] for ns in original if ns + '.' + '.'.join(segments[:length]) in types}
                        if len(candidates) != 1:
                            continue
                        target = candidates.pop()
                        ns, local = target.rsplit('.', 1)
                        if declared == ns and local == segments[0] and length == 1:
                            return match[0]
                        prefix = next((key for key, value in additions.items() if value == ns), None)
                        if prefix is None:
                            prefix = 'vm_port_' + str(len(additions))
                            while prefix in declarations:
                                prefix += '_'
                            additions[prefix] = ns
                        tail = '.' + '.'.join(segments[length:]) if length < len(segments) else ''
                        return prefix + ':' + local + tail
                    if not declared.startswith('System'):
                        unresolved[file.relative_to(ROOT).as_posix()].add(declared + '.' + name)
                    return match[0]
                text = re.sub(r'(?<![\w.])' + re.escape(alias) + r':([A-Za-z_]\w*(?:\.\w+)*)', qname, text)
            if additions:
                opening = re.search(r'<(?![!?])[^>]+>', text)
                if opening:
                    block = ''.join(' xmlns:' + alias + '="clr-namespace:' + ns + '"' for alias, ns in additions.items())
                    pos = opening.end() - (2 if opening[0].endswith('/>') else 1)
                    text = text[:pos] + block + text[pos:]
            if 'Property="InputBase.' in text:
                text = text.replace('Property="InputBase.', 'Property="')
                if 'xmlns:vmInputBase=' not in text:
                    opening = re.search(r'<(?![!?])[^>]+>', text)
                    pos = opening.end() - 1
                    text = text[:pos] + ' xmlns:vmInputBase="clr-namespace:SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives"' + text[pos:]
            if file.name == 'OutputConfigTreeView.xaml':
                event = '<EventSetter Event="MouseDoubleClick" Handler="TextBlock_MouseDown" />'
                if event in text and '</Style.Setters>' in text and text.index(event) > text.index('</Style.Setters>'):
                    text = text.replace(event, '', 1).replace('<Style.Setters>', '<Style.Setters>\n        ' + event, 1)
                elif event in text and '</Style.Setters>' not in text:
                    # Decompiled implicit Setter content cannot straddle Style.Triggers.
                    start = text.rfind('<Style ', 0, text.index(event))
                    trigger = text.find('<Style.Triggers>', start)
                    if start >= 0 and trigger >= 0 and trigger < text.index(event):
                        text = text.replace(event, '', 1)
                        text = text[:trigger] + event + '\n        ' + text[trigger:]
            def concrete_setters(match):
                owner=match[2]
                if not owner.endswith(('UpDown','Picker')):
                    return match[0]
                # Repair an earlier owner substitution as well as bare DPs.
                # Self-closing styles cannot own the following style's body.
                body=re.sub(r'(<Setter\b[^>]*\bProperty=")\{x:Static [\w]+:[\w]+\.(TextAlignment|WatermarkTemplate|ShowButtonSpinner|AllowSpin|ButtonSpinnerLocation)Property\}("|\s)',
                            lambda setter:setter[1]+'{x:Static '+owner+'.'+setter[2]+'Property}'+setter[3],match[3])
                body=re.sub(r'(<Setter\b[^>]*\bProperty=")(TextAlignment|WatermarkTemplate|ShowButtonSpinner|AllowSpin|ButtonSpinnerLocation)(")',
                            lambda setter:setter[1]+owner+'.'+setter[2]+setter[3],body)
                return match[1]+body+match[4]
            text=re.sub(r'(<Style\b[^>]*TargetType="\{x:Type ([^}]+)\}"[^>]*(?<!/)>)([\s\S]*?)(</Style>)',concrete_setters,text)
            text=re.sub(r'(<Setter\b[^>]*\bProperty=")([\w]+:[\w]+)\.(TextAlignment|WatermarkTemplate|ShowButtonSpinner|AllowSpin|ButtonSpinnerLocation)(")',
                        lambda setter:setter[1]+'{x:Static '+setter[2]+'.'+setter[3]+'Property}'+setter[4],text)
            # Recovered BAML declares CLR aliases on Setter/property elements.
            # WPF's build parser interprets those local mappings as attributes;
            # hoist only unambiguous aliases, preserving their exact URI.
            opening=re.search(r'<(?![!?])[^>]+>',text)
            if opening:
                aliases=defaultdict(set)
                for alias,uri in re.findall(r'xmlns:(\w+)="([^"]+)"',text):aliases[alias].add(uri)
                head=text[opening.start():opening.end()];tail=text[opening.end():]
                for alias,uris in aliases.items():
                    if len(uris)!=1:continue
                    uri=next(iter(uris))
                    if not re.search(r'xmlns:'+re.escape(alias)+r'=',head):
                        head=head[:-1]+' xmlns:'+alias+'="'+uri+'">'
                    tail=re.sub(r'\s+xmlns:'+re.escape(alias)+r'="'+re.escape(uri)+'"','',tail)
                text=text[:opening.start()]+head+tail
            # After concrete QName remapping the old vendor alias may be unused.
            # WPF still imports its CLR namespace into generated C#, so retain
            # aliases only when referenced (including markup compatibility lists).
            for alias, uri in re.findall(r'xmlns:(\w+)="(clr-namespace:[^"]+)"', text):
                if not uri.startswith(('clr-namespace:Apps.', 'clr-namespace:VMControls',
                                       'clr-namespace:VM.', 'clr-namespace:HikPcUI')):
                    continue
                declaration = r'\s+xmlns:' + re.escape(alias) + r'="' + re.escape(uri) + '"'
                without_declaration = re.sub(declaration, '', text)
                qualified_reference = re.search(r'(?<![\w.])' + re.escape(alias) + r':', without_declaration)
                compatibility_reference = any(alias in value.split() for value in re.findall(
                    r'\b(?:Ignorable|MustUnderstand|ProcessContent|PreserveAttributes|PreserveElements)="([^"]*)"', text))
                if not qualified_reference and not compatibility_reference:
                    text = without_declaration
        if text != old_text:
            protected = protected_host.get(file.relative_to(ROOT).as_posix())
            if protected:
                assert hashlib.sha256(file.read_bytes()).hexdigest() == protected['adapted_sha256'], 'Review edited host adapter before normalization: '+str(file)
                assert '.cs' in file.suffixes and without_header_imports(text) == without_header_imports(old_text), 'Host adapter body normalization requires explicit review: '+str(file)
            file.write_text(text, encoding='utf-8')
            if protected:
                protected['adapted_sha256'] = hashlib.sha256(file.read_bytes()).hexdigest()
                protected['integration_change'] = 'Verified source hash before normalization; only C# header using directives changed, namespace/body unchanged.'
                host_headers_updated = True
            changed.append(file.relative_to(ROOT).as_posix())

if host_headers_updated:
    host_receipt_path.write_text(json.dumps(host_receipt, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')

integrated_files = []
for folder in roots:
    for file in sorted(folder.rglob('*')):
        if file.is_file() and file.name.endswith(('.cs', '.xaml', '.cs.pending', '.xaml.pending')):
            integrated_files.append({'path': file.relative_to(ROOT).as_posix(),
                                     'sha256': hashlib.sha256(file.read_bytes()).hexdigest(),
                                     'pending': file.name.endswith('.pending')})
receipt = {'phase': 'source_consistency_only_no_build_or_test', 'lane_manifests': manifest_ids,
           'types': len(types), 'namespace_groups': len(namespaces), 'changed_files': changed,
           'integrated_files': integrated_files,
           'unresolved_xaml_names': {key: sorted(value) for key, value in unresolved.items()},
           'not_claimed': 'Source integration is not compilation, runtime, visual or user acceptance.'}
out = NATIVE / 'source-integration-receipt.json'
out.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
project = ET.Element('Project')
items = ET.SubElement(project, 'ItemGroup')
for item in raw_xaml_resources:
    relative = path(item).relative_to(Path('src/Plugin.Wpf')).as_posix()
    ET.SubElement(items, 'Page', {'Remove': relative})
    ET.SubElement(items, 'Resource', {'Include': relative})
for item in embedded_resources:
    relative = path(item['target']).relative_to(Path('src/Plugin.Wpf')).as_posix()
    operation = 'Update' if relative.endswith('.resx') else 'Include'
    element = ET.SubElement(items, 'EmbeddedResource', {operation: relative})
    ET.SubElement(element, 'LogicalName').text = item['logicalName']
ET.indent(project, space='  ')
(ROOT / 'src/Plugin.Wpf/NativeResources.props').write_text(ET.tostring(project, encoding='unicode') + '\n', encoding='utf-8')
print(json.dumps({'types': len(types), 'changed_files': len(changed), 'unresolved_xaml_files': len(unresolved)}, ensure_ascii=False))
