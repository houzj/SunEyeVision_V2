"""核对现有正式产物回执并集中归档S2；不构建或复制程序。"""
from pathlib import Path
import hashlib
import json
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
V = ROOT / 'Release/_validation'
C = ROOT / 'docs/collaboration'
PRODUCT = ROOT / 'Release/SunEyeVision_v1.0.0/SunEyeVision'

def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def write(p, data):
    p.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def rel(p):
    return p.relative_to(ROOT).as_posix()

def latest(track):
    p = sorted((V / 'S2-20261025' / track).glob('attempt-*'))[-1]
    assert read(p / 'receipt.json')['exit'] == 0
    return p

def main():
    core, related = latest('DocumentEditing'), latest('Related')
    assert '28通过' in (core / 'run.log').read_text(encoding='utf-8-sig')
    report = read(related / 'related-report.json')
    assert report['count'] == 52 and report['failures'] == 0
    compat = V / 'S2-committed-regression-20261025'
    cr = read(compat / 'validation-receipt.json')
    assert len(cr) == 3 and all(x['exit'] == 0 for x in cr)
    probe, toolbox = read(V / 'S2-release-probe.json'), read(V / 'S2-toolbox-probe.json')
    commit = probe['commit']
    assert toolbox['commit'] == commit and len(toolbox['toolboxTools']) == 22
    subprocess.check_call(['git', 'cat-file', '-e', commit + '^{commit}'], cwd=ROOT)
    subprocess.check_call(['git', 'diff', '--quiet', commit, '--', 'src', 'tools', 'tests', 'scripts/verification'], cwd=ROOT)
    for name in ('S2-committed-release-build.log', 'S2-ui-release-build.log'):
        assert '0 个错误' in (V / name).read_text(encoding='utf-8-sig')
    identities, helpers = [], []
    for p in [core / 'loaded-product.tsv', related / 'loaded-product.tsv'] + [Path(x['assemblyIdentity']) for x in cr]:
        for line in p.read_text(encoding='utf-8-sig').splitlines():
            if not line.strip():
                continue
            name, path, h = line.split('\t')
            f = Path(path)
            assert sha(f).upper() == h.upper(), path
            item = dict(name=name, path=rel(f), sha256=h, loadLog=rel(p))
            if f.is_relative_to(PRODUCT):
                identities.append(item)
            else:
                assert name.endswith('.Tests') and f.is_relative_to(ROOT / 'tests/bin/Release'), path
                helpers.append(item)
    manifest = [dict(path=rel(p), sha256=sha(p), bytes=p.stat().st_size) for p in sorted(PRODUCT.rglob('*')) if p.is_file()]
    write(V / 'S2-final-payload-manifest.json', dict(sourceCommit=commit, files=manifest))
    write(V / 'S2-final-loaded-identity.json', dict(sourceCommit=commit, verifiedProductLoads=len(identities), allProductLoadsOfficial=True, loads=identities, testHelpers=helpers))
    history = C / 'history/S2_before_final_summary_20261025.json'
    if not history.exists():
        history.write_bytes((C / 'S2_PROGRESS_20261025.json').read_bytes())
    files = set()
    for base in [V / 'S2-20261025', compat, V / 'S2-final-regression-20261025', V / 'S2-compat-regression-20261025']:
        files.update(p for p in base.rglob('*') if p.is_file() and p.suffix.lower() in ('.log', '.json', '.tsv') and not any(x in ('obj', 'out') for x in p.relative_to(base).parts))
    files.update(p for p in V.glob('S2-*') if p.is_file() and p.suffix.lower() in ('.log', '.json', '.tsv'))
    archive = C / 'evidence/S2-20261025.zip'
    archive.parent.mkdir(exist_ok=True)
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for p in sorted(files):
            z.write(p, rel(p))
    paths = subprocess.check_output(['git', '-c', 'core.quotePath=false', 'show', '--pretty=format:', '--name-only', commit], cwd=ROOT, text=True, encoding='utf-8').splitlines()
    sources = [dict(path=p, sha256=sha(ROOT / p)) for p in paths if p.startswith(('src/', 'tools/')) and (ROOT / p).is_file()]
    status = 'IMPLEMENTATION_AND_AUTOMATED_CHECKS_CLOSED'
    progress = dict(stage='S2', status=status, sourceCommit=commit, productRoot=rel(PRODUCT), version=probe['releaseVersion'], channel=probe['channel'], sdkToolContract=probe['toolContract'],
        checks=dict(core=dict(passed=28, failed=0, receipt=rel(core / 'receipt.json')), related=dict(passed=52, failed=0, receipt=rel(related / 'related-report.json')),
            compatibility=dict(receipt=rel(compat / 'validation-receipt.json'), scope='旧格式/绑定迁移、真实HALCON Blob工作流9项资源链、Snapshot15项；本轮未重复跨卷'),
            fullBuild=dict(errors=0, warnings=1280, log=rel(V / 'S2-committed-release-build.log')), uiBuild=dict(errors=0, warnings=1264, log=rel(V / 'S2-ui-release-build.log')),
            designTime='设计时编译成功，未在Visual Studio设计器实际打开', releaseProbe=rel(V / 'S2-release-probe.json'), toolboxProbe=rel(V / 'S2-toolbox-probe.json'), loadedIdentity=rel(V / 'S2-final-loaded-identity.json')),
        blockingGap=None, remaining=[], notClaimed=['S3控件/ROI/动态结果表重构及完整Blob新UI未做', 'VM实际编辑/执行/关闭时序等价未验证，本轮无新增VM取证', '实际VS设计器及完整主窗口人工交互未确认', '用户人工验收未执行，随下一次明显体验变化集中验证'],
        failuresPreserved='关联类型/引用失败、结构变化编辑失败和关闭取消失败均保留。保存oracle由错误标量改为既定long[] tuple，未修改产品迎合错误。',
        evidenceArchive=dict(path=rel(archive), sha256=sha(archive), files=len(files), bytes=archive.stat().st_size, scope='仅日志、回执和身份清单，保留失败；无程序或仓库副本'), sourceManifest=sources,
        manualAcceptance='未执行、未代签；随后续明显体验变化集中验证。')
    write(C / 'S2_PROGRESS_20261025.json', progress)
    p = C / 'S2_IMPLEMENTATION_PLAN_20261025.json'
    d = read(p)
    d.update(status=status,
        current='S2编辑、保存、运行事务及必要适配已实施；最终提交2165ba0d的正式Release全量/UI构建0错误，28项事务、52项关联检查通过，旧方案迁移/真实HALCON Blob资源链/保存恢复回归通过。启动及22工具加载探针通过。未代替用户验收，不启动S3。',
        summaryStatus='S2实施与本轮自动验证已闭合，正式Release已更新至2165ba0d；完整Blob新界面及VM实机等价不在通过声明内。',
        reporting='本轮完成代码、正式产物与原文档核收；不自动进入S3。用户人工验证随后续明显体验变化集中安排。',
        manual='本轮不要求单独手验。后续体验包集中验证：改参数试算但不确认，正式运行保持原值；确定只接受当前窗口；隐藏窗口中的编辑可保存重开，无效值阻止整批保存且保留输入；取消不撤销已确定内容，关闭不清其他结果；连续运行在下一完整轮采用新配置。用户未执行，未代签。',
        verification=[['正式构建与身份', '全量Release及UI Release：0错误，分别1280/1264条警告保留；产物内提交2165ba0d，启动与22工具加载探针通过。'], ['编辑与保存', '28项通过：范围隔离、隐藏/卸载和未失焦输入、真实Blob窗口、保存失败重试与重开、节点增删/重建、关闭取消及显示保留。'], ['关联运行和变量', '52项通过：CAS/ABA、锁外通知、连续整轮快照、本地图像、通信准入及快照释放等。'], ['受影响兼容链', '真实旧方案/绑定迁移、HALCON Blob实际执行与9项资源链、15项保存恢复检查通过。本轮未重复跨卷。'], ['未确认与未做', 'VS实际设计器、完整主窗口人工交互、VM对应实机时序及用户验收未确认；S3未做。']])
    row = ['S2阶段结果及原始证据', 'docs/collaboration/S2_PROGRESS_20261025.json', '最终回执、产物身份、源码清单及压缩日志归档；历史失败保留。']
    if row not in d['evidence']:
        d['evidence'].append(row)
    write(p, d)
    p = C / 'S2_SOURCE_ENTRY_AUDIT_20261025.json'
    d = read(p)
    d.update(status=status, remaining='S2无已知实施阻断；VM实机等价、VS实际设计器及人工交互未确认，明确不计本轮通过。', productBuild='2165ba0d正式Release全量/UI构建0错误；28项事务及52项关联检查通过，身份和原始证据见S2_PROGRESS。')
    d['entries'][8][2] = '隐藏/Unload保留scope；Closed才释放。取消关闭不撤草稿；Blob只释放自己的编辑覆盖，不清正式结果/其他范围，真实窗口反例通过。'
    write(p, d)
    print(json.dumps(dict(commit=commit, core=rel(core), related=rel(related), productLoads=len(identities), testHelperLoads=len(helpers), payloadFiles=len(manifest), archiveBytes=archive.stat().st_size), ensure_ascii=False))

if __name__ == '__main__':
    main()
