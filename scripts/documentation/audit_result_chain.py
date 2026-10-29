"""Audit result contracts in evaluated default-product Compile items, without building."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'docs/collaboration/evidence/I2_RESULT_CHAIN_INVENTORY_20261029.json'
SYMBOLS = ('RuntimeToolResults', 'ToolResults', 'DeclaredOutputSnapshot', 'DeclaredOutputReader',
           'ResultResourceOwner', 'ToolResultSnapshot', 'NodeResultSnapshot', 'ExecutionResult',
           'NodeExecutionResult', 'AlgorithmResult', 'IResultPublication', 'IResultValueLease',
           'GetDisplayImages', 'GetImageView', 'CopyImage', 'FreezeResult',
           'ToolExecutionIdentity', 'ToolExecutionOwner', 'ExecutionStatus')
PATTERN = re.compile(r'\b(' + '|'.join(SYMBOLS) + r')\b')


def evaluate(project):
    result = subprocess.run(['dotnet', 'msbuild', str(ROOT / project), '-nologo', '-getItem:Compile'],
                            cwd=ROOT, text=True, encoding='utf-8', capture_output=True)
    if result.returncode:
        raise RuntimeError(f'{project}: {result.stdout}\n{result.stderr}')
    data = json.loads(result.stdout)
    return project, [item['FullPath'] for item in data['Items']['Compile']]


def main():
    projects = [e.attrib['Path'] for e in ET.parse(ROOT / 'SunEyeVision.slnx').iter('Project')
                if not e.attrib['Path'].casefold().startswith('tests/')]
    owners = {}
    with ThreadPoolExecutor(max_workers=3) as executor:
        evaluated = list(executor.map(evaluate, projects))
    for project, files in evaluated:
        for file in files:
            try:
                path = Path(file).resolve().relative_to(ROOT).as_posix()
            except ValueError:
                continue
            if not path.casefold().startswith(('src/', 'tools/')) or not Path(file).is_file():
                continue
            owners.setdefault(path, []).append(project)
    records = []
    for name, source_projects in sorted(owners.items()):
        source = ROOT / name
        lines = source.read_text(encoding='utf-8-sig').splitlines()
        hits = [{'line': i, 'symbols': sorted(set(PATTERN.findall(line))), 'text': line.strip()}
                for i, line in enumerate(lines, 1) if PATTERN.search(line)]
        if hits:
            records.append({'file': name, 'sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                            'compiledBy': source_projects, 'hits': hits})
    summary = {symbol: {'files': sum(any(symbol in hit['symbols'] for hit in item['hits']) for item in records),
                        'lines': sum(symbol in hit['symbols'] for item in records for hit in item['hits'])}
               for symbol in SYMBOLS}
    report = {
        'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'method': '对默认解决方案非测试工程执行MSBuild -getItem:Compile；逐文件保存源码SHA与精确符号行，不执行构建。',
        'limitation': '符号引用是编译消费者上界，包含注释与未触发分支；不冒称运行时可达图、资源正确性或性能测试。',
        'evaluatedProjects': projects,
        'evaluatedProductSourceFiles': len(owners), 'matchedFiles': len(records),
        'summary': summary, 'files': records,
    }
    OUTPUT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'evidence': str(OUTPUT.relative_to(ROOT)), 'projects': len(evaluated),
                      'sourceFiles': len(owners), 'matchedFiles': len(records), 'summary': summary}, ensure_ascii=False))


if __name__ == '__main__':
    main()
