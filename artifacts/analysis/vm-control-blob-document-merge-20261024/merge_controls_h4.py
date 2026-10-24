"""Pure h4 merger: reads SHA-bound review artifacts and manager status; never writes a document."""
from pathlib import Path
import json,re,hashlib,html,os
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];SOURCE=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024'
REVIEW_SHA='30F8D805B0325C795666F5E6A1F77443F7E96E542EB0CD6E76FA63BD88C373BB';FRAGMENT_SHA='116DE8BFAC708BE3FFF31ABF8E3BB251DDB4FBD058C80A55BEC92B93748316D4'
PAGE_DIR=ROOT/'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护/优化方案'
def evidence_link(path,label):
    target=ROOT/path;assert target.is_file(),str(target)
    href=os.path.relpath(target,PAGE_DIR).replace('\\','/')
    return '<a href="'+html.escape(href,quote=True)+'">'+html.escape(label)+'</a>'

def _hash(b):return hashlib.sha256(b).hexdigest().upper()
def _history(block,heading_end):
    marker=re.search(rb'<details\b[^>]*\bdata-vm-control-review-history(?:=["\'][^"\']*["\'])?[^>]*>',block)
    if not marker:return block[heading_end:]
    depth=0;stop=None
    for t in re.finditer(rb'<details\b[^>]*>|</details\s*>',block[marker.start():],re.I):
        depth+=-1 if t.group().startswith(b'</') else 1
        if depth==0:stop=marker.start()+t.start();break
    assert stop is not None,'unbalanced original history'
    summary=re.search(rb'<summary\b[^>]*>.*?</summary>',block[marker.end():stop],re.S);assert summary
    return block[marker.end()+summary.end():stop]
def merge_controls_h4(page:bytes)->bytes:
    if not isinstance(page,bytes):raise TypeError('bytes required')
    review_bytes=(SOURCE/'architect-review.json').read_bytes();fragment=(SOURCE/'review-fragment.html').read_bytes()
    assert _hash(review_bytes)==REVIEW_SHA and _hash(fragment)==FRAGMENT_SHA,'review inputs changed; manager re-review required'
    review=json.loads(review_bytes.decode('utf-8-sig'));status=json.loads((HERE/'manager-status.json').read_text(encoding='utf-8-sig'))
    assert status['reviewSHA']==REVIEW_SHA and status['fragmentSHA']==FRAGMENT_SHA
    assert status['wholeAccepted'] is False and status['productImplementationApproved'] is False
    states={'waiting-2204-receipt':'22:04静态整合包尚待经理核收','received-limited-static-awaiting-director-addenda':'静态包已提前实收完整性；新增证据处置复核中，原VM未运行/产品未批准'}
    states['received-limited-static-director-reviewed']='静态包完整性已实收，补证处置已作有限静态复核；原VM未运行/产品未批准'
    if status['integrationState']=='received-limited-static-director-reviewed':
        assert status.get('addendaPath') and status.get('addendaSHA'),'director addenda path/SHA required'
        assert _hash((ROOT/status['addendaPath']).read_bytes())==status['addendaSHA'],'director addenda drift'
        addenda=json.loads((ROOT/status['addendaPath']).read_text(encoding='utf-8-sig'))
        assert addenda['decision']=='approve original O06 documentation merge only' and addenda['reviewSHA'].upper()==REVIEW_SHA and addenda['deliverySHA']==status['vmDeliverySHA'],'director decision identity mismatch'
        assert addenda['wholeAccepted'] is False and addenda['productImplementationApproved'] is False and addenda['runtimeVerified'] is False,'director scope mismatch'
    assert status['integrationState'] in states,'unsupported acceptance state'
    if status['integrationState'].startswith('received-limited-static'):
        receipt=ROOT/status['integratedReceipt']['path'];assert _hash(receipt.read_bytes())==status['integratedReceipt']['sha256'],'manager receipt drift'
    heading=re.search(rb'<h4\b[^>]*\bid="br-controls-vm-compare"[^>]*>.*?</h4>',page,re.S);assert heading,'original controls h4 missing'
    nxt=re.search(rb'<h[1-4]\b',page[heading.end():]);end=heading.end()+nxt.start() if nxt else len(page)
    old=page[heading.start():end];original_history=_history(old,len(heading.group()))
    raw=fragment.decode('utf-8-sig').strip();body=re.fullmatch(r'<section\b[^>]*>\s*<h3>.*?</h3>(.*)</section>\s*',raw,re.S);assert body
    body=re.sub(r'<(/?)h4\b',r'<\1h5',body[1])
    body=body.replace('同目录architect-review.json',evidence_link('artifacts/analysis/vm-blob-control-architecture-review-20261024/architect-review.json','架构复审原件 architect-review.json'))
    for d in review['decisions']:
        needle='<tr><td>'+html.escape(d['id'])+'</td>';assert body.count(needle)==1
        body=body.replace(needle,'<tr id="vm-control-disposition-'+d['id'].lower()+'"><td>'+html.escape(d['id'])+'</td>',1)
    for u in review['runtimeUnknowns']:
        needle='<tr><td>'+html.escape(u['question'])+'</td>';assert body.count(needle)==1
        body=body.replace(needle,'<tr id="vm-control-unknown-'+u['id'].lower()+'"><td><b>'+u['id']+'</b> '+html.escape(u['question'])+'</td>',1)
    notice='<p class="br-card"><b>本节及完整Blob章节相关推荐已修订：</b>旧表默认保留旧Manager、整类编辑组织或旧字符串公开接线的建议已撤回，被下方10项处置及理由替代。推荐重做公共接入与协调并删除越界权限；只有限复用职责正确且证实的视觉原语、数据基础和算法/渲染函数。</p>'
    notice+='<p>'+states[status['integrationState']]+'。这是有界静态架构复审，实机与完整组装链未验，待整体审核；不构成Blob、变量或全工具产品改造批准。</p><p>既有区域样式只引用 <a href="#preview-style-batch">VM样式/交互证据</a>，不重画。U01–U05未知及停止条件保留，样式不代算法采样、事务或性能等价。</p>'
    cap=json.loads((ROOT/'artifacts/analysis/vm-control-blob-evidence-receive-20261024/runtime-capability-receipt.json').read_text(encoding='utf-8-sig'));assert cap['runtimeExecuted'] is False
    notice+='<p>21:49实机能力：当前无支持的原VM桌面/native路径，UIA不可绕过，恢复测试执行器不等VM桌面。BMP仅候选；支持能力就绪后沿原授权使用自有非设备图与可丢弃方案，不追加批准门。实机岗核能力、逆向负责人核版本/实例/样本、经理协调设施与独占窗；就绪未知、22:04重估，不阻变量Save及Release。</p>'
    delivery_path='artifacts/analysis/vm-control-blob-evidence-receive-20261024/delivery-snapshot.json'
    delivery_bytes=(ROOT/delivery_path).read_bytes();assert _hash(delivery_bytes)==status['vmDeliverySHA']
    chain=json.loads(delivery_bytes.decode('utf-8-sig'))['staticChain'];assert len(chain)==12
    assert all(c['runtimeObserved'] is False and c['staticPathLocated'] is True for c in chain)
    table='<h5 id="vm-control-selected-static-chain">选定12段静态组装路径</h5><p>本选定静态路径已定位，实机均未运行。仅列本次选定路径，不代表全部分支或验收分母。</p><table><thead><tr><th>路径</th><th>原始引用</th></tr></thead><tbody>'
    for c in chain:table+='<tr><td>'+html.escape(c['name'])+'</td><td>'+html.escape(' · '.join(c['refs']))+'</td></tr>'
    table+='</tbody></table><p>证据：'+' / '.join(evidence_link(path,label) for path,label in [(delivery_path,'选定静态链原件'),('artifacts/analysis/vm-control-blob-evidence-receive-20261024/integrated-static-receipt.json','经理完整性收据'),('artifacts/analysis/vm-blob-control-architecture-review-20261024/architect-review.json','架构复审原件'),('artifacts/analysis/vm-control-blob-evidence-receive-20261024/runtime-capability-receipt.json','原VM实机能力收据')])+'</p>'
    notice+=table
    if status.get('runtimeCheckpoint'):
        checkpoint=status['runtimeCheckpoint']; checkpoint_bytes=(ROOT/checkpoint['path']).read_bytes()
        assert _hash(checkpoint_bytes)==checkpoint['sha256'],'runtime checkpoint drift'
        checkpoint_data=json.loads(checkpoint_bytes.decode('utf-8-sig'));assert checkpoint_data['runtimeStarted'] is False
        notice+='<p><b>22:04检查点实际复核：</b>'+html.escape(checkpoint_data['actualReassessmentAt'])+'；未获新的支持桌面设施或独占VM窗口，原程序未运行，许可未知，无可靠开测时刻。</p><table><thead><tr><th>负责人</th><th>缺项</th><th>解除动作</th><th>就绪时间</th></tr></thead><tbody>'
        for row in checkpoint_data['owners']:
            notice+='<tr>'+''.join('<td>'+html.escape('暂无可靠时间' if row[key] is None else str(row[key]))+'</td>' for key in ['owner','missing','action','readyEstimate'])+'</tr>'
        notice+='</tbody></table><p>'+html.escape(checkpoint_data['nextReestimate'])+'</p>'
    new=heading.group()+notice.encode()+b'<div id="vm-blob-control-architecture-review">'+body.encode()+b'</div><details data-vm-control-review-history><summary>'+ '历史静态对照（旧默认保留推荐已撤回，仅供证据追溯）'.encode()+b'</summary>'+original_history+b'</details>\n'
    return page[:heading.start()]+new+page[end:]
