"""Inventory source project edges and non-reference product admission paths.

This is a source audit, not an MSBuild evaluation or a release acceptance test.
Conditional ProjectReference edges are deliberately retained with their conditions.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
import argparse

ROOT = Path(__file__).resolve().parents[2]
DESTINATION = ROOT / 'docs/collaboration/evidence/PRODUCT_ISOLATION_INVENTORY_20261028.json'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', default='docs/collaboration/evidence/PRODUCT_ISOLATION_CURRENT_20261028.json')
    args = parser.parse_args()
    destination = ROOT / args.output
    manifest = ET.parse(ROOT / 'product/ToolComponents.props') if (ROOT / 'product/ToolComponents.props').exists() else None
    component_projects = [e.attrib['Include'] for e in manifest.iter('ProductToolComponent')] if manifest else []
    paths = subprocess.check_output(
        ['rg', '--files', '-g', '*.csproj', '-g', '!Release/**', '-g', '!artifacts/**'],
        cwd=ROOT, text=True, encoding='utf-8').splitlines()
    actual = {p.replace('\\', '/').casefold(): p.replace('\\', '/') for p in paths}

    def canonical(path):
        relative = path.resolve().relative_to(ROOT).as_posix()
        return actual.get(relative.casefold(), relative)

    solution = ET.parse(ROOT / 'SunEyeVision.slnx')
    members = {canonical(ROOT / e.attrib['Path']) for e in solution.iter('Project')}
    projects = {}
    for name in sorted(actual.values()):
        file = ROOT / name
        tree = ET.parse(file)
        properties = {e.tag: e.text.strip() for group in tree.findall('PropertyGroup')
                      for e in group if e.text and e.text.strip()}
        references = []
        for group in tree.findall('ItemGroup'):
            for edge in group.findall('ProjectReference'):
                include = edge.attrib.get('Include', '')
                if include == "@(ProductToolComponent->'../../%(Identity)')":
                    for component in component_projects:
                        references.append({'project': canonical(ROOT / component),
                                           'condition': '', 'referenceOutputAssembly': 'false',
                                           'source': 'product/ToolComponents.props（明确展开，不代替MSBuild求值）'})
                    continue
                if '$(' in include or '@(' in include or '*' in include:
                    raise ValueError(f'Unresolved project edge: {name}: {include}')
                target = canonical(file.parent / include.replace('\\', '/'))
                if target not in actual.values():
                    raise ValueError(f'Missing project edge: {name}: {target}')
                references.append({'project': target,
                                   'condition': ' && '.join(filter(None, [group.get('Condition'), edge.get('Condition')])),
                                   'referenceOutputAssembly': edge.get('ReferenceOutputAssembly', 'true')})
        projects[name] = {
            'sourceSha256': hashlib.sha256(file.read_bytes()).hexdigest(),
            'inDefaultSolution': name in members,
            'legacyToolProducer': properties.get('IsToolPlugin', '').lower() == 'true',
            'component': {key: properties[key] for key in properties if key.startswith('ToolComponent')},
            'projectReferences': references,
        }

    legacy = {name for name, value in projects.items() if value['legacyToolProducer']}

    def legacy_paths(start):
        found = []

        def visit(node, route):
            if node in legacy:
                found.append(route)
                return
            for edge in projects[node]['projectReferences']:
                target = edge['project']
                if target not in route:
                    visit(target, route + [target])

        visit(start, [start])
        return found

    affected = {name: legacy_paths(name) for name in sorted(members) if name not in legacy}
    affected = {name: routes for name, routes in affected.items() if routes}
    mechanisms = {
        'installer/Release.Payload.targets': ['ResolveReleaseToolProjects', 'IsToolPlugin', 'SealDevelopmentReleasePayload'],
        'installer/Seal-ReleasePayload.ps1': ['IsToolPlugin', 'ExpectedPluginAssemblies', 'ProductComponents'],
        'installer/Pack-Release.ps1': ['IsToolPlugin', 'ExpectedPluginAssemblies', 'ProductComponents'],
        'src/UI/Shell/Controls/Toolbox/ToolboxViewModel.cs': ['vm:', 'VmToolCatalog', 'GetCategory(name)'],
        'Directory.Build.targets': ['IsToolPlugin', 'plugins'],
    }
    evidence = []
    for name, needles in mechanisms.items():
        file = ROOT / name
        lines = file.read_text(encoding='utf-8-sig').splitlines()
        hits = [{'line': i, 'text': line.strip()} for i, line in enumerate(lines, 1)
                if any(needle in line for needle in needles)]
        if not hits:
            raise ValueError(f'Admission mechanism no longer found: {name}')
        evidence.append({'file': name, 'sha256': hashlib.sha256(file.read_bytes()).hexdigest(), 'hits': hits})
    report = {
        'schemaVersion': 1,
        'baselineCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'scope': '源码项目引用上界；保留条件边；不包含MSBuild导入求值、动态程序集加载或运行时证明',
        'status': '当前源码引用盘点，不代表正式产物已通过验收',
        'defaultSolutionProjectCount': len(members),
        'legacyToolProjectsInDefaultSolution': sorted(members & legacy),
        'componentProjects': sorted(name for name, value in projects.items() if value['component']),
        'defaultSolutionReversePathsToLegacyTools': affected,
        'admissionMechanismsOutsideProjectReferences': evidence,
        'projects': projects,
    }
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'report': str(destination.relative_to(ROOT)), 'solutionProjects': len(members),
                      'legacyToolsInSolution': len(members & legacy),
                      'componentProjects': len(report['componentProjects']),
                      'otherSolutionProjectsReachingLegacyTools': len(affected)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
