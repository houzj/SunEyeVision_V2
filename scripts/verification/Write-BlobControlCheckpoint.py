"""Audit selected checks against the current formal payload; never reuse stale receipts."""
import argparse
from datetime import datetime, timezone
from functools import lru_cache
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
PRODUCT = ROOT / 'Release/SunEyeVision_v1.0.0/SunEyeVision'


@lru_cache(maxsize=None)
def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT).decode('utf-8').strip()


def build_result(path):
    content = path.read_text(encoding='utf-8-sig', errors='replace')
    errors = re.findall(r'^\s*(\d+) 个错误\s*$', content, re.M)
    warnings = re.findall(r'^\s*(\d+) 个警告\s*$', content, re.M)
    if not errors or errors[-1] != '0':
        raise ValueError('构建未证明0错误: ' + str(path))
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path), errors=0,
                warnings=int(warnings[-1]) if warnings else None,
                elapsed=re.findall(r'已用时间\s+(\S+)', content)[-1])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ui-log', required=True)
    parser.add_argument('--release-log', required=True)
    parser.add_argument('--receipt', action='append', required=True)
    parser.add_argument('--probe', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    output = ROOT / args.output
    if output.exists():
        raise ValueError('保留已有证据，请使用新的输出名: ' + str(output))
    payload_path = PRODUCT / 'release-payload.json'
    payload = read(payload_path)
    base = git('rev-parse', payload['sourceCommit'] + '^{commit}')
    entries = {entry['path'].replace('\\', '/'): entry['sha256'].lower() for entry in payload['entries']}
    for relative, expected in entries.items():
        path = (PRODUCT / relative).resolve()
        path.relative_to(PRODUCT.resolve())
        if not path.is_file() or sha(path) != expected:
            raise ValueError('当前正式产物与发布清单不一致: ' + relative)
    checks, loaded = [], []
    for value in args.receipt:
        path = ROOT / value
        receipt = read(path)
        if receipt['exit'] != 0 or Path(receipt['productRoot']).resolve() != PRODUCT.resolve():
            raise ValueError('检查未通过或不来自正式目录: ' + value)
        current_source = ROOT / 'scripts/verification' / receipt['caseSource']
        if sha(current_source) != receipt['caseSourceSha256'].lower():
            raise ValueError('检查源码已改变，必须重新执行: ' + value)
        if sha(path.parent / 'cases.cs.txt') != sha(current_source):
            raise ValueError('检查归档源码不同源: ' + value)
        run = (path.parent / 'run.log').read_text(encoding='utf-8-sig')
        group_count = len(re.findall(r'^(?:PASS |通过：)', run, re.M))
        for line in (path.parent / 'loaded-product.tsv').read_text(encoding='utf-8-sig').splitlines():
            name, location, digest = line.split('\t')
            actual = Path(location).resolve()
            relative = actual.relative_to(PRODUCT.resolve()).as_posix()
            if sha(actual) != digest.lower() or entries.get(relative) != digest.lower():
                raise ValueError('检查加载的是其他产物: ' + relative)
            loaded.append(dict(track=receipt['caseSource'], name=name, path=relative, sha256=digest.lower()))
        checks.append(dict(receipt=value, receiptSha256=sha(path), stage=receipt['stage'],
                           source=receipt['caseSource'], caseSourceSha256=sha(current_source),
                           passedGroups=group_count, exit=0))
    source_paths = set(git('diff', '--name-only', '-z', base).split('\x00'))
    source_paths.update(git('ls-files', '--others', '--exclude-standard', '-z').split('\x00'))
    delta = []
    for name in sorted(source_paths):
        path = ROOT / name
        if not name.startswith(('src/', 'tools/')) or path.suffix not in (
                '.cs', '.xaml', '.csproj', '.props', '.targets', '.resx', '.xml'):
            continue
        delta.append(dict(path=name, status='present' if path.is_file() else 'deleted',
                          sha256=sha(path) if path.is_file() else None))
    catalog = ROOT / 'src/Plugin.Wpf/Catalog/controls.json'
    for entry in read(catalog)['entries']:
        for item in entry['sourceFiles']:
            if sha(ROOT / item['localPath']) != item['localSha256'].lower():
                raise ValueError('控件清单源码SHA已过时: ' + item['localPath'])
        for item in entry['dependencies']:
            if sha(ROOT / item['path']) != item['sha256'].lower():
                raise ValueError('控件清单依赖SHA已过时: ' + item['path'])
    probe_path = ROOT / args.probe
    probe = read(probe_path)
    if (probe.get('status') == 'Rejected' or len(probe.get('toolboxTools', [])) != 22
            or not probe.get('commit') or not base.startswith(probe['commit'])):
        raise ValueError('真实Release工具箱探针未证明22工具准入: ' + str(probe_path))
    result = dict(schemaVersion=1, generatedUtc=datetime.now(timezone.utc).isoformat(),
                  scope='Blob所需公共原控件及组合的当前集中批验证；范围以所列源码增量和实际检查为准，不是全部控件、完整Blob显示/ROI或整体目标验收',
                  source=dict(compiledBase=base, headAtAudit=git('rev-parse', 'HEAD'), delta=delta),
                  payload=dict(path=payload_path.relative_to(ROOT).as_posix(), buildId=payload['buildId'],
                               sha256=sha(payload_path), verifiedEntries=len(entries)),
                  builds=[build_result(ROOT / args.ui_log), build_result(ROOT / args.release_log)],
                  checks=checks, loadedProductRecords=loaded, catalogSha256=sha(catalog),
                  actualExecutableProbe=dict(path=args.probe, sha256=sha(probe_path), toolboxCount=22),
                  manual='既有VS三页与索引切换用户反馈保留。本批原色板、完整ROI/位姿/显示及最终外观尚未取得真实VS/用户验收，不能代签。')
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(output=args.output, sourceDelta=len(delta), verifiedPayloadEntries=len(entries),
                          loadedRecords=len(loaded), checks=len(checks)), ensure_ascii=False))


if __name__ == '__main__':
    main()
