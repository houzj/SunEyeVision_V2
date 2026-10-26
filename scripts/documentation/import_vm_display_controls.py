"""移植原 VM 显示/颜色/图像控件源码；不构建、不执行控件，不生成替代实现。"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import posixpath
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
PREFIX = 'SunEyeVision.Plugin.Wpf.Native.Display'
NUMERIC = 'SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls'
UPDOWNS = ('Byte', 'SByte', 'Short', 'UShort', 'Integer', 'UInteger', 'Long',
           'ULong', 'Single', 'Double', 'Decimal', 'Numeric', 'CommonNumeric')
NUMERIC_TYPES = {name + 'UpDown' for name in UPDOWNS}
NUMERIC_EDITORS = ({name + 'UpDownEditor' for name in UPDOWNS}
                   | {'PropertyGridEditor' + name + 'UpDown' for name in UPDOWNS}
                   | {'UpDownEditor'})
EXCLUDED_THEMES = {'controls/numericupdown/themes/generic.xaml',
                   'controls/numericupdown/themes/aero2.normalcolor.xaml'}
NAMESPACES = {'VM.Controls': PREFIX + '.VMControls',
              'VMBaseControls': PREFIX + '.VMBaseControls',
              'VM.Utility': PREFIX + '.VMUtility',
              'HikVision.Styles': PREFIX + '.HikVision.Styles',
              'VisionUi': PREFIX + '.VisionUi',
              'Apps.ColorFun': PREFIX + '.Apps.ColorFun'}
LIBRARIES = ('VM.Controls', 'VMBaseControls', 'Apps.ColorFun')
LIB_FOLDER = {'VM.Controls': 'VMControls', 'VMBaseControls': 'VMBaseControls',
              'Apps.ColorFun': 'AppsColorFun'}
ASSET_SUFFIXES = {'.png', '.jpg', '.jpeg', '.gif', '.bmp', '.ico', '.cur', '.ttf'}


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def excluded(path):
    stem = path.stem
    return (stem in NUMERIC_TYPES or stem in NUMERIC_EDITORS
            or path.as_posix().lower() in EXCLUDED_THEMES
            or (path.name == 'AssemblyInfo.cs' and path.parent.name == 'Properties'))


def target_for(library, relative, data=None):
    folder = LIB_FOLDER[library]
    if relative.suffix.lower() in ASSET_SUFFIXES:
        return Path('src/Plugin.Wpf/Themes/Assets/Native/Display') / folder / relative
    if relative.suffix.lower() == '.xaml':
        # XAML views must remain beside their code-behind; dictionaries retain original hierarchy.
        text = (data or b'').decode('utf-8-sig')
        if 'x:Class=' not in text:
            return Path('src/Plugin.Wpf/Themes/Native/Display') / folder / relative
    return Path('src/Plugin.Wpf/Controls/Native/Display') / folder / relative


def replace_namespaces(text):
    # Map concrete numeric types before their old parent namespace.
    for name in sorted(NUMERIC_EDITORS, key=len, reverse=True):
        text = re.sub(r'\bVM\.Controls\.PropertyGrid\.Editors\.' + re.escape(name) + r'\b',
                      NUMERIC + '.PropertyGrid.Editors.' + name, text)
    for name in sorted(NUMERIC_TYPES, key=len, reverse=True):
        text = re.sub(r'\bVM\.Controls\.' + re.escape(name) + r'\b', NUMERIC + '.' + name, text)
    for old, new in sorted(NAMESPACES.items(), key=lambda item: -len(item[0])):
        # The exported VMControls contains visual VM.Utility helpers, but not the
        # vendor CurrentPathHelper contract. Do not fabricate ownership of it.
        external = r'(?!\.SingleInstance\b)' if old == 'VM.Utility' else ''
        text = re.sub(r'(?<![\w.])' + re.escape(old) + external + r'(?=[.;:\s"\'<,)}\]]|$)', new, text)
    return text


def separate_numeric_xaml_types(text):
    additions = []
    for prefix, namespace in re.findall(r'xmlns:([\w]+)="clr-namespace:([^;"\s]+)(?:;assembly=[^"]+)?"', text):
        if namespace == PREFIX + '.VMControls':
            names, target_prefix, target_namespace = NUMERIC_TYPES, 'displayNumericTypes', NUMERIC
        elif namespace == PREFIX + '.VMControls.PropertyGrid.Editors':
            names, target_prefix, target_namespace = NUMERIC_EDITORS, 'displayNumericEditors', NUMERIC + '.PropertyGrid.Editors'
        else:
            continue
        used = False
        for name in sorted(names, key=len, reverse=True):
            text, count = re.subn(r'\b' + re.escape(prefix + ':' + name) + r'\b', target_prefix + ':' + name, text)
            used = used or count > 0
        if used:
            additions.append('xmlns:' + target_prefix + '="clr-namespace:' + target_namespace + ';assembly=SunEyeVision.Plugin.Wpf"')
    if additions:
        # Source dictionaries/views retain their body; only the exact relocated type
        # reference acquires an additional CLR namespace alias.
        text = re.sub(r'(<(?:\w+:)?[A-Za-z_][\w.]*)\b', lambda match: match[0] + ' ' + ' '.join(dict.fromkeys(additions)), text, count=1)
    return text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=Path('D:/MyWork/REA逆向测试'))
    args = parser.parse_args()
    exports = args.source_root / 'analysis/decompiled'
    inventory_path = ROOT / 'docs/collaboration/evidence/VM_CONTROL_LIBRARY_INVENTORY_20261026.json'
    inventory = json.loads(inventory_path.read_text(encoding='utf-8-sig'))
    entries = []
    excluded_files = []
    uri_map = {}
    logical_pages = {}
    original_build_items = []
    resource_xaml_targets = set()
    # 经审核的宿主边界适配有独立来源和输出哈希；重复导入不得恢复厂商全局依赖。
    host_receipt = ROOT / 'src/Plugin.Wpf/Controls/Native/Display/visual-host-adaptations.json'
    host_adaptations = {row['target']: row for row in json.loads(host_receipt.read_text(encoding='utf-8'))['files']} if host_receipt.exists() else {}
    for library in LIBRARIES:
        source = exports / library
        for path in sorted(source.rglob('*')):
            if not path.is_file():
                continue
            relative = path.relative_to(source)
            if any(part in ('bin', 'obj', '.git') for part in relative.parts):
                continue
            if path.suffix.lower() not in ({'.cs', '.xaml', '.resx', '.settings'} | ASSET_SUFFIXES):
                continue
            if excluded(relative):
                excluded_files.append({'library': library, 'path': relative.as_posix(),
                                       'reason': 'numeric writer or assembly metadata'})
                continue
            data = path.read_bytes()
            target = target_for(library, relative, data)
            entries.append((library, path, relative, target, data))
            uri_map[(library.lower(), relative.as_posix().lower())] = target

    # Recovered x:Class files are physically grouped by CLR namespace. Their original
    # BAML resource path lives in Page.LogicalName and is the base of relative merges.
    for library in LIBRARIES:
        project = ET.parse(exports / library / (library + '.csproj')).getroot()
        for item in project.iter():
            if item.tag not in ('Page', 'Resource', 'EmbeddedResource'):
                continue
            source_name = item.get('Include')
            logical = item.get('LogicalName')
            if not source_name:
                continue
            key = (library.lower(), source_name.replace('\\', '/').lower())
            mapped = uri_map.get(key)
            if mapped is not None:
                original_build_items.append({'library': library, 'sourceRelative': source_name,
                                             'itemType': item.tag, 'logicalName': logical, 'target': mapped.as_posix()})
                if item.tag == 'Resource' and mapped.suffix.lower() == '.xaml':
                    resource_xaml_targets.add(mapped.as_posix())
                if logical:
                    logical_pages[key] = logical
                    uri_map[(library.lower(), logical.lower())] = mapped

    for name in ('generic.xaml', 'aero2.normalcolor.xaml'):
        uri_map[('vm.controls', 'controls/numericupdown/themes/' + name)] = Path(
            'src/Plugin.Wpf/Themes/Native/Numeric/VMControls/controls/numericupdown/themes') / name
    uri_map[('vm.pcui', 'listview/sliderarrangedictionary.xaml')] = Path(
        'src/Plugin.Wpf/Themes/Native/Numeric/PcUi/SliderArrangeDictionary.xaml')
    local = args.source_root / 'src/VisionUi'
    for name in ('SourceAdvancedColorPicker.xaml', 'SourceAdvancedColorPicker.xaml.cs',
                 'SourceColorPalette.xaml', 'SourceColorPalette.xaml.cs', 'SourceColorPickerStyles.xaml'):
        source = local / name
        data = source.read_bytes()
        destination = ('src/Plugin.Wpf/Themes/Native/Display/VisionUi' if name == 'SourceColorPickerStyles.xaml'
                       else 'src/Plugin.Wpf/Controls/Native/Display/VisionUi')
        entries.append(('VisionUi', source, Path(name), Path(destination) / name, data))

    colorchart = local / 'SourceAssets/colorchart.png'
    colorchart_target = Path('src/Plugin.Wpf/Themes/Assets/Native/Display/VisionUi/SourceAssets/colorchart.png')
    entries.append(('VisionUi', colorchart, Path('SourceAssets/colorchart.png'), colorchart_target, colorchart.read_bytes()))
    uri_map[('visionui', 'sourceassets/colorchart.png')] = colorchart_target

    records = []
    unhandled_uris = []
    clr_dependencies = {}
    native_dependencies = []
    embedded_resources = []
    xaml_dependencies = {}
    for library, source, relative, target, data in entries:
        changes = []
        if source.suffix.lower() in {'.cs', '.xaml', '.resx', '.settings'}:
            original = data.decode('utf-8-sig')
            text = original
            # Resolve original assembly component paths before namespace substitution.
            def component(match):
                assembly, resource = match.group(1), match.group(2)
                key = (assembly.lower(), resource.replace('\\', '/').lower())
                mapped = uri_map.get(key)
                if mapped is None:
                    unhandled_uris.append({'source': str(source), 'assembly': assembly, 'resource': resource})
                    return match[0]
                changes.append('assembly component URI')
                return '/SunEyeVision.Plugin.Wpf;component/' + mapped.relative_to('src/Plugin.Wpf').as_posix()
            text = re.sub(r'/?(VM\.Controls|VMBaseControls|VM\.PcUI|Apps\.ColorFun|VisionUi);component/([^"\s<>;]+)', component,
                          text, flags=re.IGNORECASE)
            if library in LIBRARIES and source.suffix.lower() == '.xaml':
                def relative_dictionary(match):
                    attribute, value = match.group(1), match.group(2)
                    if value.startswith('/') or ':' in value:
                        return match[0]
                    base_resource = logical_pages.get((library.lower(), relative.as_posix().lower()), relative.as_posix())
                    resolved = posixpath.normpath(posixpath.join(posixpath.dirname(base_resource), value.replace('\\', '/')))
                    mapped = uri_map.get((library.lower(), resolved.lower()))
                    if mapped is None:
                        unhandled_uris.append({'source': str(source), 'assembly': library, 'resource': resolved})
                        return match[0]
                    changes.append('relative resource URI relocated to exact component path')
                    return attribute + '="/SunEyeVision.Plugin.Wpf;component/' + mapped.relative_to('src/Plugin.Wpf').as_posix() + '"'
                text = re.sub(r'(Source|ImageSource|UriSource)="([^"]+\.(?:xaml|png|jpg|jpeg|gif|bmp|ico|cur|ttf))"',
                              relative_dictionary, text, flags=re.IGNORECASE)
            for namespace, assembly in re.findall(r'clr-namespace:([^;"\s]+);assembly=([^"\s]+)', original):
                if assembly not in LIBRARIES and assembly not in ('mscorlib', 'System', 'System.Core', 'WindowsBase', 'PresentationCore', 'PresentationFramework'):
                    xaml_dependencies.setdefault(assembly, []).append({'namespace': namespace, 'target': target.as_posix()})
            # Original fully qualified namespaces and self-assembly names in XAML.
            text = re.sub(r';assembly=(?:VM\.Controls|VMBaseControls|Apps\.ColorFun)(?=["\s])',
                          ';assembly=SunEyeVision.Plugin.Wpf', text)
            text = replace_namespaces(text)
            if library == 'VMBaseControls' and relative.name == 'EditRoundedWindow.xaml':
                # The original window consumes SliderArrange; numeric owns its exact
                # port, not a new simplified slider in this group.
                text = text.replace('clr-namespace:HikPcUI.ListView;assembly=VM.PcUI',
                                    'clr-namespace:SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;assembly=SunEyeVision.Plugin.Wpf')
            if source.suffix.lower() == '.xaml':
                text = separate_numeric_xaml_types(text)
            if source.suffix.lower() == '.cs':
                # Preserve all original behavior, but give cross-group numeric types an explicit import.
                text = 'using ' + NUMERIC + ';\nusing ' + NUMERIC + '.PropertyGrid.Editors;\n' + text
                for namespace in re.findall(r'^using\s+([\w.]+)\s*;', original, re.M):
                    if namespace in ('System.Windows.Interactivity', 'System.Management', 'VM.Utility.SingleInstance') or not namespace.startswith(('System', 'VM.Controls', 'VMBaseControls', 'VM.Utility', 'HikVision.Styles')):
                        clr_dependencies.setdefault(namespace, []).append(target.as_posix())
                for dll in re.findall(r'DllImport\("([^"]+)"', original):
                    native_dependencies.append({'library': dll, 'source': target.as_posix()})
            if original != text:
                changes.append('namespace/assembly and cross-group numeric import')
            # The host repository declares CRLF for C#/XAML/XML. Make raw target
            # hashes stable across the handoff checkout; retain source whitespace.
            text = text.replace('\r\n', '\n').replace('\r', '\n').replace('\n', '\r\n')
            output = text.encode('utf-8')
        else:
            output = data
        if target.as_posix() in host_adaptations:
            adapted = host_adaptations[target.as_posix()]
            assert digest(data).lower() == adapted['original_source_sha256'].lower(), '宿主适配的原源码身份已变化：' + str(source)
            current = (ROOT / target).read_bytes()
            assert digest(current).lower() == adapted['adapted_sha256'].lower(), '宿主适配已修改，先核对差异并更新适配记录：' + str(target)
            output = current
            changes.append('verified host adaptation retained; see visual-host-adaptations.json')
        (ROOT / target).parent.mkdir(parents=True, exist_ok=True)
        (ROOT / target).write_bytes(output)
        record = {'library': library, 'source': str(source), 'sourceRelative': relative.as_posix(),
                  'target': target.as_posix(), 'sourceSha256': digest(data),
                  'targetSha256': digest(output), 'adaptations': sorted(set(changes))}
        if source.suffix.lower() in {'.cs', '.xaml', '.resx', '.settings'}:
            record['gitNormalizedTargetSha256'] = digest(output.replace(b'\r\n', b'\n'))
        records.append(record)
        if source.suffix.lower() == '.resx':
            # Root integration must assign these names explicitly; inferred SDK path names are wrong.
            name = source.name[:-5]
            embedded_resources.append({'target': target.as_posix(),
                                       'logicalName': replace_namespaces(name) + '.resources'})
        elif source.suffix.lower() in ASSET_SUFFIXES and library == 'VMBaseControls' and relative.parent == Path('.'):
            embedded_resources.append({'target': target.as_posix(), 'logicalName': replace_namespaces(source.name)})

    selected_sources = {(library, relative.as_posix().replace('/', '\\'))
                        for library, _, relative, _, _ in entries}
    controls = []
    for item in inventory['types']:
        if item['library'] not in LIBRARIES:
            continue
        available = [s for s in item['sources'] if (item['library'], s['file']) in selected_sources]
        if available:
            controls.append({'originalType': item['type'], 'targetType': replace_namespaces(item['type']),
                             'classification': item['classification'], 'sources': available,
                             'library': item['library']})
    type_map = {item['originalType']: item['targetType'] for item in controls}
    for _, _, _, _, data in entries:
        try:
            source_text = data.decode('utf-8-sig')
        except UnicodeDecodeError:
            continue
        declaration = re.search(r'^namespace\s+([\w.]+)\s*[;{]', source_text, re.M)
        if not declaration:
            continue
        for name in re.findall(r'^(?:(?:public|internal|abstract|sealed|static|readonly|partial)\s+)*(?:class|struct|enum|interface|record)\s+(\w+)', source_text, re.M):
            original_type = declaration[1] + '.' + name
            type_map[original_type] = replace_namespaces(original_type)
    for name in ('SourceAdvancedColorPicker', 'SourceColorPalette'):
        type_map['VisionUi.' + name] = PREFIX + '.VisionUi.' + name
    manifest = {'baseline': 'b87fb6857212ce8e1a7d0d0cb47c6c9a00c8e86b',
                'status': 'source_port_only_not_built_or_run',
                'namespace': PREFIX, 'sourceRoots': [str(exports / lib) for lib in LIBRARIES],
                'scope': 'VM.Controls非数值族全部源码与支撑、VMBaseControls视觉/ROI源码与原资源、Apps.ColorFun视觉依赖及VisionUi颜色还原；不导入厂商业务模型或调度',
                'files': records, 'types': controls, 'excludedFiles': excluded_files,
                'type_map': dict(sorted(type_map.items())),
                'source_to_target': {record['source'].replace('\\', '/'): record['target'] for record in records},
                'component_uri_map': {'/' + key[0] + ';component/' + key[1]: '/SunEyeVision.Plugin.Wpf;component/' + value.relative_to('src/Plugin.Wpf').as_posix()
                                      for key, value in sorted(uri_map.items())},
                'localReconstructionControls': [PREFIX + '.VisionUi.SourceAdvancedColorPicker', PREFIX + '.VisionUi.SourceColorPalette'],
                'build_items_for_root': {'Compile': [r['target'] for r in records if r['target'].endswith('.cs')],
                                         'Page': [r['target'] for r in records if r['target'].endswith('.xaml') and r['target'] not in resource_xaml_targets] + ['src/Plugin.Wpf/Themes/Native/Display/DisplayResources.xaml'],
                                         'Resource': [r['target'] for r in records if Path(r['target']).suffix.lower() in ASSET_SUFFIXES or r['target'] in resource_xaml_targets]},
                'externalNamespaces': {k: sorted(set(v)) for k, v in sorted(clr_dependencies.items())},
                'externalXamlAssemblyReferences': xaml_dependencies,
                'originalProjectAssemblyReferences': {lib: [item.get('Include') for item in ET.parse(exports / lib / (lib + '.csproj')).getroot().iter('Reference')]
                                                     for lib in LIBRARIES},
                'nativeDependencies': native_dependencies, 'unresolvedComponentUris': unhandled_uris,
                'embeddedResourcesForRootBuildIntegration': embedded_resources,
                'originalBuildItems': original_build_items,
                'originalPageLogicalNames': [{'library': key[0], 'sourceRelative': key[1], 'originalLogicalName': value}
                                             for key, value in sorted(logical_pages.items())],
                'entryDictionary': 'src/Plugin.Wpf/Themes/Native/Display/DisplayResources.xaml',
                'integration': ['根集中修改csproj/Page/Resource/EmbeddedResource与字典合并，当前未修改共享工程或Generic',
                                'Numeric组提供排除类型及numericupdown两主题；本文不复制其代码',
                                'Apps.BaseData/Mvvm/VMControls.RenderInterface等外部合同需根承接，未生成空stub或第二套业务ROI模型',
                                '反编译源码及原XAML已保留完整行为；源可用不等于编译/运行已通过'],
                'dependencyBoundary': ['Apps.ColorFun保留原ColorCFG/ColorStyle外部加载及Apps.BaseData/Apps.Log/VM.Utility.SingleInstance依赖，未造配置或日志替代',
                                       'Apps.ColorFun原colorstyle1/2/3.xaml为Resource非Page，按originalBuildItems保留构建身份',
                                       'ColorPicker/PropertyGrid模板指向实际原图，本地VisionUi色彩图保留',
                                       'aero2/common.xaml的resize_grip.png在现有原导出和本地还原树未找到，保留未解析身份，不以别图或手绘替代'],
                'built': False, 'tested': False}
    manifest_path = ROOT / 'src/Plugin.Wpf/Controls/Native/Display/display-migration-manifest.json'
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='')
    provenance_path = ROOT / 'src/Plugin.Wpf/Themes/Native/Display/display-resource-provenance.json'
    provenance_path.parent.mkdir(parents=True, exist_ok=True)
    provenance_path.write_text(json.dumps({'files': [r for r in records if r['target'].endswith('.xaml')
                                                   or Path(r['target']).suffix.lower() in ASSET_SUFFIXES],
                                           'adaptation': 'namespace and exact component URI only; no redrawn resources'},
                                          ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='')
    entry = ROOT / 'src/Plugin.Wpf/Themes/Native/Display/DisplayResources.xaml'
    entry.write_text('''<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMControls/themes/generic.xaml" />
    <ResourceDictionary Source="/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/themes/generic.xaml" />
    <ResourceDictionary Source="/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VisionUi/SourceColorPickerStyles.xaml" />
  </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
''', encoding='utf-8', newline='\r\n')
    print(json.dumps({'importedFiles': len(records), 'originalTypes': len(controls),
                      'excludedFiles': len(excluded_files), 'unresolvedComponentUris': len(unhandled_uris),
                      'built': False, 'tested': False}, ensure_ascii=False))


if __name__ == '__main__':
    main()
