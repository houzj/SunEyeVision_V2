"""Pure h4 merger: reads SHA-bound review artifacts and manager status; never writes a document."""
from pathlib import Path
import json,re,hashlib,html,os
from urllib.parse import unquote,urlsplit
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];SOURCE=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024'
REVIEW_SHA='30F8D805B0325C795666F5E6A1F77443F7E96E542EB0CD6E76FA63BD88C373BB';FRAGMENT_SHA='116DE8BFAC708BE3FFF31ABF8E3BB251DDB4FBD058C80A55BEC92B93748316D4'
OWNER_FRAGMENT=ROOT/'artifacts/analysis/document-owner-o06-20261024/fragment.html'
OWNER_FRAGMENT_SHA='8E37584852E7503D93CB8C3CD3B1FE19BC7BBC357488438F72B38725BB080C9B'
SCREENSHOT_MAP=SOURCE/'interface-deepening/vm-screenshot-control-map.json'
SCREENSHOT_MAP_SHA='3CC32436DEB492873776F083C7D66E43662A6719DD9A021DD72219088EBE43FE'
DESIGNER_MAP=SOURCE/'interface-deepening/implementation/designer-engine-screenshot-map.json'
DESIGNER_MAP_SHA='23A914745E4397E34587743F8CB512E26283F2BDBE29AA9705D1C57AEB9110A8'
SCREENSHOT_HARD_GATE=ROOT/'artifacts/analysis/document-owner-o06-20261024/vm-screenshot-hard-gate-review.json'
SCREENSHOT_HARD_GATE_SHA='E7C15F8047BD7F89A9C84702612F933FEAA5D4D62A53BB7206C712ABD4F90610'
SCREENSHOT_CORRECTION=ROOT/'artifacts/analysis/document-owner-o06-20261024/vm-screenshot-source-map-correction-20261025.json'
SCREENSHOT_CORRECTION_SHA='71920BBAD4DE5F4F8B8B5E5435640B29E7763D89D0D40FA517EEC5E250691054'
TYPED_CANDIDATE=ROOT/'artifacts/analysis/document-owner-o06-20261024/typed-candidate.html'
TYPED_CANDIDATE_SHA='D40EA4A30CDBB1B005AA8829298B88853267E531788BB55E4C195AA74CDE68BF'
FIELD_MAP=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/implementation/blob-field-map-final-v2.json'
FIELD_MAP_SHA='94DA12876AB19360F681378B1FFAB2D2139330EDF1701121CCFBADC752A8ADD3'
IMPL_DIR=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/implementation'
DELIVERY_MANIFEST=IMPL_DIR/'delivery-final-v2.json'
DELIVERY_MANIFEST_SHA='BFD13771F2C6968E76C80557989297DCFC90C9A1E63DB440FA70EA22503DDE66'
TYPED_V4=IMPL_DIR/'candidate-contracts-final-v4.json'
TYPED_V4_SHA='DE64D2145B54F16CB1590A0C2CCC8D093ECD7E887A1A6D13B12A3E5143809883'
ENGINEERING_V3=IMPL_DIR/'engineering-deployment-final-v3.json'
ENGINEERING_V3_SHA='B096FC76891A97F218FF0E4AE9836003E88FE2F07A6CF4D4043C127913CB7593'
DOMAIN_DELTA=IMPL_DIR/'image-domain-contract-delta-v1.json'
DOMAIN_DELTA_SHA='A3764269323D759DC3202B8282BAB9E038CA47B5307772C9333DB404926542BF'
DOMAIN_ACCEPTANCE=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/independent-test/image-valid-domain-acceptance-delta.json'
DOMAIN_ACCEPTANCE_SHA='75A755CE354EEC889C25FAD2EB8E500B3E0DC4C5DE465946306EC820F483C027'
PAGE_DIR=ROOT/'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护/优化方案'
def evidence_link(path,label):
    target=ROOT/path;assert target.is_file(),str(target)
    href=os.path.relpath(target,PAGE_DIR).replace('\\','/')
    return '<a href="'+html.escape(href,quote=True)+'">'+html.escape(label)+'</a>'

def repair_external_links(markup:str)->str:
    def convert(match):
        href=match.group(2);parts=urlsplit(href)
        if parts.scheme or href.startswith('#'):return match.group(0)
        target=(PAGE_DIR/unquote(parts.path)).resolve()
        if target.exists() and not target.is_relative_to(ROOT):
            return match.group(1)+html.escape(target.as_uri(),quote=True)+match.group(3)
        return match.group(0)
    return re.sub(r'(href=["\'])([^"\']+)(["\'])',convert,markup)

def _hash(b):return hashlib.sha256(b).hexdigest().upper()
def _render_field_map():
    raw=FIELD_MAP.read_bytes();assert _hash(raw)==FIELD_MAP_SHA,'final field-map input drift; owner review required'
    data=json.loads(raw.decode('utf-8-sig'));counts=data['counts']
    assert len(data['fields'])==33 and counts['canonicalTopLevelFields']==33
    assert len(data['presentationLeaves'])==79 and counts['presentationLeafFields']==79
    assert len(data['outputs'])==30 and counts['existingStableOutputs']==30
    e=lambda value:html.escape('' if value is None else str(value),quote=False)
    assert counts['candidateConcreteLeafPorts']==117
    parts=['<details data-blob-complete-field-map><summary>Blob完整字段核对清单（33个CLR映射、79个表现叶字段、117个候选具体叶Port、30个稳定输出；均为候选映射，不代表schema或实现通过）</summary>',
      '<p>'+e(data['status'])+'。字段ID策略：'+e(data['fieldIdPolicy'])+'。生成输入：'+evidence_link('artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/implementation/blob-field-map-final-v2.json','blob-field-map-final-v2.json')+' · SHA256 '+FIELD_MAP_SHA+'，与下方冻结 delivery-final-v2 manifest 一致；初稿错误统计已由本字段表取代。</p>',
      '<p>33是CLR顶层映射，79是表现叶字段，117是候选具体叶Port，30是稳定输出ID；按冻结映射原文分别显示，彼此不是同一统计层级，不通过相减或推导覆盖率。</p><h6>33项顶层字段与现属性映射</h6><div class="table-scroll"><table><thead><tr><th>候选 FieldId / 逻辑键</th><th>当前 CLR 属性 / 别名</th><th>CLR 类型 / 默认事实</th><th>来源与行</th><th>类型与 ROI 角色</th><th>模式 / 默认页要求</th></tr></thead><tbody>']
    for f in data['fields']:
      aliases='、'.join(f.get('aliases',[])) or '无'
      role=f.get('roiRole') or '—'
      representation=f.get('representation') or ''
      type_candidate=f.get('typeRefCandidate','')
      if f['logicalKey']=='input.pose':type_candidate='RecordOf(spatial.affine2d)：Float64六数 + from/to logical frame'
      elif f['logicalKey']=='input.image':type_candidate='ResourceOf(Image)'
      elif f['logicalKey']=='presentation':type_candidate='能力组映射：类型逐叶见presentationLeaves；不映射为单一object Port'
      parts.append('<tr><td><code>'+e(f['fieldIdCandidate'])+'</code><br>'+e(f['logicalKey'])+'</td><td><code>'+e(f['currentClrProperty'])+'</code><br>'+e(aliases)+'</td><td>'+e(f['currentClrType'])+'<br>'+e(f.get('currentDefault',''))+'</td><td><code>'+e(f['source'])+':'+e(f['line'])+'</code></td><td>'+e(type_candidate)+'<br>ROI：'+e(role)+' · '+e(representation)+'</td><td>'+e(' / '.join(f.get('modes',[])))+'<br>'+e(f.get('defaultPage',''))+'</td></tr>')
    parts.append('</tbody></table></div>')
    parts.append('<h6>79项表现叶字段与真实初始化表达式</h6><details><summary>展开逐叶字段、类型、默认表达式和源码行</summary><div class="table-scroll"><table><thead><tr><th>候选 FieldId / 键</th><th>当前 CLR 路径 / 类型</th><th>默认表达式与实际初始化</th><th>来源与行</th><th>模式 / 默认页能力</th></tr></thead><tbody>')
    for f in data['presentationLeaves']:
      parts.append('<tr><td><code>'+e(f['fieldIdCandidate'])+'</code><br>'+e(f['logicalKey'])+'</td><td><code>'+e(f['currentClrPath'])+'</code><br>'+e(f['currentClrType'])+'</td><td><code>'+e(f.get('currentDefaultExpression',''))+'</code><br>'+e(f.get('actualInitializer',''))+'</td><td><code>'+e(f['modelSource'])+':'+e(f['modelLine'])+'</code></td><td>'+e(' / '.join(f.get('modes',[])))+'<br>'+e(f.get('defaultPage',''))+'</td></tr>')
    parts.append('</tbody></table></div></details>')
    parts.append('<h6>30项当前稳定输出 ID</h6><details><summary>展开输出名称、现有 FieldId 与来源</summary><div class="table-scroll"><table><thead><tr><th>当前输出</th><th>稳定 Output FieldId</th><th>当前状态</th><th>来源</th></tr></thead><tbody>')
    for f in data['outputs']:
      parts.append('<tr><td><code>'+e(f['currentOutput'])+'</code></td><td><code>'+e(f['stableFieldId'])+'</code></td><td>'+e(f['current'])+'</td><td><code>'+e(f['source'])+'</code></td></tr>')
    parts.append('</tbody></table></div></details>')
    parts.append('<h6>检测区 / 屏蔽区：各自四个 typed 成员与模式语义</h6><div class="table-scroll"><table><thead><tr><th>ROI 角色 / 所有者</th><th>成员 FieldId / 键</th><th>真实类型候选</th><th>启用模式 / 默认页</th></tr></thead><tbody>')
    for group in data['regionModeGroups']:
      for member in group['members']:
       parts.append('<tr><td>'+e(group['role'])+' · '+e(group['owner'])+'</td><td><code>'+e(member['fieldIdCandidate'])+'</code><br>'+e(member['logicalKey'])+'</td><td>'+e(member.get('declaration',''))+'</td><td>'+e(' / '.join(member.get('modes',[])))+'<br>'+e(member.get('defaultPage',''))+'</td></tr>')
      parts.append('<tr><td colspan="4"><b>active：</b>'+e(group['activeSemantics'])+'<br><b>保存：</b>'+e(group['persistence'])+'</td></tr>')
    parts.append('</tbody></table></div><p><b>Region 合同：</b>'+e(data['regionDeclaration'])+'</p>')
    parts.append('<h6>别名、继承排除与待补证</h6><ul>')
    for a in data['aliases']:parts.append('<li>别名 <code>'+e(a['property'])+'</code> → '+e(a['canonical'])+'</li>')
    for a in data['inheritedExcluded']:parts.append('<li>排除继承项 <code>'+e(a['property'])+'</code>：'+e(a['reason'])+'</li>')
    for a in data['exactMissingRatherThanHidden']:parts.append('<li>'+e(a)+'</li>')
    parts.append('</ul></details>')
    return ''.join(parts)

def _render_final_supplement():
    def load(path,digest):
        raw=path.read_bytes();assert _hash(raw)==digest,f'{path.name} drift'
        return json.loads(raw.decode('utf-8-sig'))
    manifest=load(DELIVERY_MANIFEST,DELIVERY_MANIFEST_SHA)
    typed=load(TYPED_V4,TYPED_V4_SHA);engineering=load(ENGINEERING_V3,ENGINEERING_V3_SHA)
    domain=load(DOMAIN_DELTA,DOMAIN_DELTA_SHA);acceptance=load(DOMAIN_ACCEPTANCE,DOMAIN_ACCEPTANCE_SHA)
    file_index={f['path']:f for f in manifest['files']}
    for name,digest in [('candidate-contracts-final-v4.json',TYPED_V4_SHA),('engineering-deployment-final-v3.json',ENGINEERING_V3_SHA),('blob-field-map-final-v2.json',FIELD_MAP_SHA)]:
        assert file_index[name]['sha256']==digest,f'{name} not bound by delivery manifest'
    assert domain['frozenManifestSHA256']==DELIVERY_MANIFEST_SHA
    assert acceptance['status']=='NOT_EXECUTED_DESIGN_ONLY' and all(c['execution']=='NOT_EXECUTED' for c in acceptance['cases'])
    e=lambda value:html.escape('' if value is None else str(value),quote=False)
    parts=['<details data-final-o06-delivery><summary>最终收件合同补充：图像有效 Domain、工程B 定稿映射与未执行验收（候选，不是产品通过）</summary><p>本补充的 frozen typed v4 与独立 Domain delta 优先于上方早期接口摘要；最终合同以冻结包和下列单独 Domain delta 为准。</p>',
      '<p>本节以冻结交付清单为准：'+evidence_link(str(DELIVERY_MANIFEST.relative_to(ROOT)).replace('\\','/'),'delivery-final-v2.json')+' · SHA256 '+DELIVERY_MANIFEST_SHA+'。manifest中字段表、typed v4、工程v3指纹逐项核对；整包仍不是产品实施或运行验收通过。</p>',
      '<h6>同帧图像与有效 Domain：最终候选</h6><div class="table-scroll"><table><thead><tr><th>边界</th><th>合同</th></tr></thead><tbody>']
    for key,label in [('sameFrame','同一帧'),('sameOwner','同一 owner / reader'),('fullDomain','完整图源'),('emptyAndUnset','Unset 与有效空域'),('bridge','native 到中立 bridge'),('persistence','持久化'),('errors','拒绝条件')]:
        parts.append('<tr><td>'+e(label)+'</td><td>'+e(domain['contract'][key])+'</td></tr>')
    parts.append('</tbody></table></div><pre><code>'+e(domain['candidateCSharp'])+'</code></pre><p>独立设计 delta：'+evidence_link(str(DOMAIN_DELTA.relative_to(ROOT)).replace('\\','/'),'image-domain-contract-delta-v1.json')+' · SHA256 '+DOMAIN_DELTA_SHA+'，与冻结 manifest 分开保存并明确引用。</p><p><b>同尺寸异 Domain、孔洞、有效空域、reader 冻结/释放、显式全域保存重开：</b>均为 NOT_EXECUTED；不得由 Width/Height 推断 full-domain，也不得用矩形包围框代替真实 Domain。</p>')
    parts.append('<h6>五项 Domain 验收夹具（全部尚未执行）</h6><div class="table-scroll"><table><thead><tr><th>用例</th><th>输入情形</th><th>验收重点</th><th>状态</th></tr></thead><tbody>')
    for case in acceptance['cases']:
        parts.append('<tr><td><code>'+e(case['id'])+'</code></td><td>'+e(case['input'])+'</td><td>'+e('；'.join(case['assertions']))+'</td><td>NOT_EXECUTED</td></tr>')
    parts.append('</tbody></table></div><p>验收增量：'+evidence_link(str(DOMAIN_ACCEPTANCE.relative_to(ROOT)).replace('\\','/'),'image-valid-domain-acceptance-delta.json')+' · SHA256 '+DOMAIN_ACCEPTANCE_SHA+'。本增量不增加已通过数或验收分母。</p>')
    parts.append('<h6>typed v4：资源、scope、视图与 ROI 三模式合同</h6><p>候选源：'+evidence_link(str(TYPED_V4.relative_to(ROOT)).replace('\\','/'),'candidate-contracts-final-v4.json')+' · SHA256 '+TYPED_V4_SHA+'。新类型均未实施/编译；下面保留源包逐项处置与代码形状。</p>')
    parts.append('<p><b>复用的既有类型：</b>'+e('、'.join(typed['existingTypesReused']))+'。新类型不增加 Publisher、ROI Store 或全局 Locator。</p><div class="table-scroll"><table><thead><tr><th>边界</th><th>候选类型与位置</th></tr></thead><tbody>')
    for boundary,names in typed['candidateTypePlacement'].items():parts.append('<tr><td>'+e(boundary)+'</td><td>'+e('；'.join(names))+'</td></tr>')
    parts.append('</tbody></table></div>')
    for item in typed['fragments']:
        lifetime=item['errorAndLifetime']
        if item['id']=='CS07':lifetime+='；Region 的封闭只读 reader 形状已在 CS08 定义，EffectiveDomain 与同帧/同 owner 约束由独立 image-domain-contract-delta-v1 补齐；这些均是未实现候选。'
        parts.append('<details><summary>'+e(item['id'])+' · '+e(item['proposedFile'])+'</summary><p><b>职责 owner：</b>'+e(item['owner'])+'</p><p><b>当前源码：</b>'+e(item['current'])+'</p><p><b>输入与候选责任：</b>'+e(item['input'])+'</p><p><b>输出与验收：</b>'+e(item['output'])+'</p><p><b>错误与生命周期：</b>'+e(lifetime)+'</p><pre><code>'+e(item['candidateCSharp'])+'</code></pre></details>')
    for key,label in [('regionDefaults','ROI 缺省/空值/失效语义'),('specificRemainingContractGap','Region 选型、配置与运行边界'),('viewportContract','viewport/迟到写回/释放'),('frameCorrection','frame 与坐标身份'),('defaultPageRequired','默认页能力'),('notificationAndRaceClosure','通知、Apply 回执与竞态')]:
        value=typed[key]
        if isinstance(value,dict):value='；'.join(e(k)+': '+e(v) for k,v in value.items())
        parts.append('<p><b>'+e(label)+'：</b>'+e(value)+'</p>')
    parts.append('<h6 id="o06-engineering-class-map">工程B：非31类对象的候选类映射、依赖与兼容门</h6><p>候选源：'+evidence_link(str(ENGINEERING_V3.relative_to(ROOT)).replace('\\','/'),'engineering-deployment-final-v3.json')+' · SHA256 '+ENGINEERING_V3_SHA+'。涉及本页31类控件的逐类事实与目标只见 <a href="#o06-current-control-mapping">31类对象唯一主记录</a>，这里不重复处置；本表仅保留其余运行、参数/结果合同与项目组织对象。工程B仍是候选，未构建、未证明no-UI闭环；HALCON DLL权威与Release发布依赖须按门验证。</p><div class="table-scroll"><table><thead><tr><th>现类 / 工程</th><th>处置</th><th>候选落点</th><th>消费者 / 退出门</th></tr></thead><tbody>')
    control_rows=json.loads((ROOT/'artifacts/analysis/blob-control-consumer-closure-20261024.json').read_text(encoding='utf-8-sig'))['controls']
    control_names={x['name'] for x in control_rows}
    retained=[];folded=[]
    for row in engineering['projectAndClassChanges']:
        row_text=' '.join(str(v) for v in row.values())
        overlap=[name for name in control_names if name in row_text]
        if overlap:
            folded.extend(overlap)
            continue
        retained.append(row)
        parts.append('<tr><td>'+e(row['current'])+'</td><td>'+e(row['action'])+'</td><td>'+e(row['target'])+'<br><b>依赖：</b>'+e(row['dependency'])+'</td><td>'+e(row['consumers'])+'<br><b>退出门：</b>'+e(row['exit'])+'</td></tr>')
    parts.append('</tbody></table></div><p>与31主记录对象重叠的engineering-v3逐类建议已收敛到主记录，不在此重复。')
    parts.append('<b>Plugin.Wpf 与控件原语：</b>'+e(engineering['primitiveAndThemePlacement']['decision'])+'。兼容门：'+e(engineering['primitiveAndThemePlacement']['compatibilityGate'])+'</p><p><b>嵌套项目 glob 与角色：</b>'+e(engineering['nestedProjectGlobAndRoles']['parent'])+' 候选 MSBuild：<code>'+e(engineering['nestedProjectGlobAndRoles']['candidateXml'])+'</code>。'+e(engineering['nestedProjectGlobAndRoles']['solutionAndEnumeration'])+' 尚未评估真实编译清单。</p>')
    parts.append('<h6>HALCON 权威、版本迁移与顺序门</h6><div class="table-scroll"><table><thead><tr><th>核对面</th><th>决定 / 条件</th></tr></thead><tbody>')
    for key,value in engineering['halconAuthority'].items():parts.append('<tr><td>'+e(key)+'</td><td>'+e(value)+'</td></tr>')
    for row in engineering['contractAndRevisionGates']:
        parts.append('<tr><td>'+e(row.get('domain','兼容/版本门'))+'</td><td><b>当前：</b>'+e(row.get('current',''))+'<br><b>候选：</b>'+e(row.get('decisionCandidate',''))+'<br><b>重建：</b>'+e(row.get('rebuild',''))+'</td></tr>')
    parts.append('</tbody></table></div><ol>')
    for step in engineering['migrationOrder']:parts.append('<li>'+e(step)+'</li>')
    parts.append('</ol><p><b>仍未证明：</b>'+e('；'.join(engineering.get('notProven',[])))+'。工程合同候选不是改代码授权；HALCON/native owner、API/Schema 兼容、XAML/BAML/pack URI 与 ToolId 唯一 producer 必须按清单设置迁移门。</p></details>')
    return ''.join(parts)
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
    # Preserve renderer-owned current matrix across this frozen-fragment replacement.
    heading=re.search(rb'<h4\b[^>]*\bid="br-controls-vm-compare"[^>]*>.*?</h4>',page,re.S);assert heading,'original controls h4 missing'
    nxt=re.search(rb'<h[1-4]\b',page[heading.end():]);end=heading.end()+nxt.start() if nxt else len(page)
    old=page[heading.start():end];original_history=_history(old,len(heading.group()))
    matrix_matches=list(re.finditer(rb'<!-- O06-CAPABILITY-MATRIX:BEGIN -->(.*?)<!-- O06-CAPABILITY-MATRIX:END -->',old,re.S))
    assert len(matrix_matches)<=1,'duplicate renderer-owned O06 capability matrices in the original inventory'
    # A legacy canonical page may not yet contain the matrix. The render hook supplies it
    # on regeneration; merger-only updates preserve it whenever the stable markers exist.
    # Keep the stable markers with the matrix so another render can find and preserve it.
    capability_matrix=matrix_matches[0].group(0).strip() if matrix_matches else b''
    # A previous merge may have archived the renderer block inside the historical body.
    # Keep the prior evidence while removing that duplicate current matrix and its old anchors.
    original_history=re.sub(rb'<!-- O06-CAPABILITY-MATRIX:BEGIN -->.*?<!-- O06-CAPABILITY-MATRIX:END -->',b'',original_history,flags=re.S)
    original_history=re.sub(rb'<div id="o06-capability-matrix">.*?</div>',b'',original_history,flags=re.S)
    # Keep compatibility anchors once in the current matrix. Historical copies remain readable.
    for anchor in (b'br-blob-interface-deepening',b'o06-capability-matrix',b'o06-screenshot-engineering-matrix'):
        original_history=re.sub(rb'\s+id="'+anchor+rb'"',b'',original_history)
    raw=fragment.decode('utf-8-sig').strip();body_match=re.fullmatch(r'<section\b[^>]*>\s*<h3>.*?</h3>(.*)</section>\s*',raw,re.S);assert body_match
    body=re.sub(r'<(/?)h4\b',r'<\1h5',body_match[1])
    # Keep the frozen 10-item section as navigation only; all object decisions live in the 31 primary rows.
    body=body.replace('<td>R04</td><td>重做数据分析消费者通道</td>','<td>R04</td><td>通用当前结果投影承接；旧专用视图退出</td>')
    body=body.replace('可保留业务列与表格视觉。数据请求/选择发稳定地址，不由控件掌握native寿命。','由宿主通用当前结果表、摘要与稳定元素选择投影承接；不删除数据分析能力或另造Blob专属表。数据请求/选择发稳定地址，不由控件掌握native寿命。')
    body=body.replace('<td>K01</td><td>复用有限视觉原语</td>','<td>K01</td><td>局部布局与字段模板按需承接；ToolIcon转资源解析</td>')
    body=body.replace('保留这些局部表现能力的理由是职责单一与实际输入合同匹配，不是已经写了。样式资源沿既有已确认VM规格；业务协调和状态不随视觉部件保留。','局部布局/字段原语按职责候选承接；ToolIcon以资源解析和标准Image/DataTemplate替换旧专用绘制类，适用的矢量素材可转为可部署资源。样式资源沿既有已确认VM规格；业务协调和状态不随视觉部件保留。')
    v2_href=os.path.relpath(SOURCE/'interface-deepening/four-pending-final-architecture-decision-v2-20261025.json',PAGE_DIR).replace('\\','/')
    body += '<p class="br-note" id="o06-four-v2-decision-note"><b>四项最终架构推荐（2026-10-25）：</b>BlobDataAnalysisControl、JudgmentEditorControl、ToolIcon、ToolBarControl均由“待核”更新为“组合链”，全表分组为4控件/8模板/19组合链/0待核/0确认无源。详细差异、承接方式、验收和未知边界以<a href="'+html.escape(v2_href,quote=True)+'">v2冻结决策及31类原唯一主记录</a>为准；这是待用户审核的架构建议，不代表实施批准。旧状态保留在历史记录中。</p>'
    for source_path,source_sha in [(SCREENSHOT_MAP,SCREENSHOT_MAP_SHA),(DESIGNER_MAP,DESIGNER_MAP_SHA),(SCREENSHOT_HARD_GATE,SCREENSHOT_HARD_GATE_SHA),(SCREENSHOT_CORRECTION,SCREENSHOT_CORRECTION_SHA)]:
        assert _hash(source_path.read_bytes())==source_sha,f'O06 screenshot input drift: {source_path}'
    screenshot_review=json.loads(SCREENSHOT_HARD_GATE.read_text(encoding='utf-8-sig'))
    screenshot_map=json.loads(SCREENSHOT_MAP.read_text(encoding='utf-8-sig'))
    review_status=screenshot_review['status']['screenshotMapReview']
    assert review_status['sourceMapSha256'].upper()==SCREENSHOT_MAP_SHA and review_status['sourceMapSizeBytes']==SCREENSHOT_MAP.stat().st_size
    assert len(screenshot_map['rows'])==7
    owner_bytes=OWNER_FRAGMENT.read_bytes()
    assert _hash(owner_bytes)==OWNER_FRAGMENT_SHA,'O06 owner fragment drift; review before regeneration'
    owner=owner_bytes.decode('utf-8-sig').strip()
    assert owner.startswith('<section id="br-blob-interface-deepening"') and owner.endswith('</section>')
    assert owner.endswith('</section>')
    owner=owner.replace('<section id="br-blob-interface-deepening" class="br-card">','<section class="br-card">',1)
    owner,nshot=re.subn(r'<section\b[^>]*\bid="o06-screenshot-engineering-matrix"[^>]*>.*?</section>','',owner,count=1,flags=re.S)
    assert nshot==1,'screenshot matrix was not found for integration into the original inventory'
    supplement=_render_final_supplement()
    class_start=supplement.index('<h6 id="o06-engineering-class-map">')
    class_end=supplement.index('</div>',supplement.index('</table>',class_start))+len('</div>')
    engineering_classes=supplement[class_start:class_end]
    supplement=supplement[:class_start]+supplement[class_end:]
    owner=owner[:-len('</section>')]+_render_field_map()+supplement+'</section>'
    # Keep the owner-authored section, frozen typed v4, field map and Domain delta in one existing h4 merge.
    # Replace the previous class map by a stable block so repeated merges stay unique.
    marked_class_map=re.compile(rb'<!-- O06-ENGINEERING-CLASS-MAP:BEGIN -->.*?<!-- O06-ENGINEERING-CLASS-MAP:END -->',re.S)
    page,nmarked=marked_class_map.subn(b'',page)
    assert nmarked<=1,'duplicate marked engineering class maps in page input'
    if nmarked==0:
        page,nlegacy=re.subn(rb'<h6\b[^>]*\bid="o06-engineering-class-map"[^>]*>.*?</table>\s*</div>',b'',page,count=1,flags=re.S)
        assert nlegacy<=1,'duplicate legacy engineering class maps in page input'
    # The project heading is located again after removing any previous generated map.
    project_heading=re.search(rb'<h4\b[^>]*\bid="br-tool-project-compare"[^>]*>.*?</h4>',page,re.S)
    assert project_heading,'original tool-project structure heading missing'
    project_heading_end=project_heading.end()
    project_table=page.find(b'<table',project_heading_end)
    assert project_table>=0,'original tool-project table missing'
    page=page[:project_table]+b'<!-- O06-ENGINEERING-CLASS-MAP:BEGIN -->'+engineering_classes.encode()+b'<!-- O06-ENGINEERING-CLASS-MAP:END -->'+page[project_table:]
    body=body+owner
    body=body.replace('同目录architect-review.json',evidence_link('artifacts/analysis/vm-blob-control-architecture-review-20261024/architect-review.json','架构复审原件 architect-review.json'))
    for d in review['decisions']:
        needle='<tr><td>'+html.escape(d['id'])+'</td>';assert body.count(needle)==1,('decision row count',d['id'],body.count(needle))
        body=body.replace(needle,'<tr id="vm-control-disposition-'+d['id'].lower()+'"><td>'+html.escape(d['id'])+'</td>',1)
    old_disposition_heading='<h5>删除、重做、复用及原因</h5>'
    assert body.count(old_disposition_heading)==1,'original ten-item disposition heading missing or duplicated'
    disposition_heading='<h5>D/R/K/X事项导航：跳转31类唯一主记录</h5>'
    body=body.replace(old_disposition_heading,disposition_heading,1)
    nav_intro='<p class="br-note">本表仅为D/R/K/X事项导航。每项事实、处置、目标候选、迁移与验收条件只以31类对象唯一主记录为准；不在本表重复解释或另作结论。</p>'
    body=body.replace(disposition_heading,disposition_heading+nav_intro,1)
    nav_objects={
      'D01':['BlobAnalysisToolDebugControl','ImageControl','ResultRenderLayer'],
      'D02':['RoiEditorManagerControl'],
      'R01':['RoiEditorControl','RoiSubscribePanel','ShapeToolBar','DrawingParameterPanel'],
      'R02':['ConfigSetting','ParamBindingBase','ParamBinding','ImageSourceSelector','RangeInputControl','NumericUpDown','RangeSlider','ToggleSwitch','JudgmentEditorControl'],
      'R03':['ToolDebugControlBase','DefaultDebugWindow','CustomDebugWindow','BlobAnalysisToolDebugControl'],
      'R04':['BlobDataAnalysisControl','GraphicsDisplayEditorControl','TextDisplayEditorControl','ImageControl','ResultRenderLayer'],
      'K01':['LabeledControl','ToolIcon','ToggleSwitch','DispItemColorControl','DispTextColorControl','ColorSelector','ColorPicker'],
      'K02':['ImageControl','ResultRenderLayer','BlobDataAnalysisControl','ImagePreviewControl'],
      'K03':['ImageControl','ResultRenderLayer','GraphicsDisplayEditorControl','TextDisplayEditorControl','DispItemColorControl','DispTextColorControl','ShapeToolBar','ToolBarControl','BlobAnalysisToolDebugControl'],
      'X01':['ParamBindingBase','ParamBinding','ToolDebugControlBase','DefaultDebugWindow','CustomDebugWindow','ImageControl','ResultRenderLayer','BlobAnalysisToolDebugControl']
    }
    nav_start=body.index(disposition_heading)+len(disposition_heading)
    old_table=re.search(r'<table\b[^>]*>.*?</table\s*>',body[nav_start:],re.I|re.S)
    assert old_table,'original ten-item table missing'
    rows=['<table class="br-disposition-nav"><thead><tr><th>事项编号</th><th>31类对象主记录</th></tr></thead><tbody>']
    slug=lambda name:re.sub(r'[^a-z0-9]+','-',name.lower()).strip('-')
    for decision_id,names in nav_objects.items():
        assert len(names)>0
        links='、'.join('<a href="#o06-control-'+slug(name)+'">'+html.escape(name)+'</a>' for name in names)
        rows.append('<tr id="vm-control-disposition-'+decision_id.lower()+'"><td><b>'+decision_id+'</b></td><td>'+links+'</td></tr>')
    rows.append('</tbody></table>')
    body=body[:nav_start]+body[nav_start:nav_start+old_table.start()]+''.join(rows)+body[nav_start+old_table.end():]
    for u in review['runtimeUnknowns']:
        needle='<tr><td>'+html.escape(u['question'])+'</td>';assert body.count(needle)==1
        body=body.replace(needle,'<tr id="vm-control-unknown-'+u['id'].lower()+'"><td><b>'+u['id']+'</b> '+html.escape(u['question'])+'</td>',1)
    notice='<p class="br-card"><b>阅读方式：</b>下方31类主记录按控件、模板、组合链、待核、确认无源分组；每个对象只有一条权威记录。此处D/R/K/X只提供跳转，不复述处置理由。架构候选仍待用户批准，静态对照不等于产品实施。</p>'
    notice+='<p>'+states[status['integrationState']]+'。这是有界静态架构复审，实机与完整组装链未验，待整体审核；不构成Blob、变量或全工具产品改造批准。</p><p>既有区域样式只引用 <a href="#preview-style-batch">VM样式/交互证据</a>，不重画。U01–U05未知及停止条件保留，样式不代算法采样、事务或性能等价。</p>'
    cap=json.loads((ROOT/'artifacts/analysis/vm-control-blob-evidence-receive-20261024/runtime-capability-receipt.json').read_text(encoding='utf-8-sig'));assert cap['runtimeExecuted'] is False
    notice+='<details><summary>历史设施记录（2026-10-24 21:49 / 22:04）</summary><p>当时未发现支持的原VM桌面/native路径，UIA不可绕过；原程序未运行，能力/许可/开测时间未知。这是该日的设施状态，不是当前执行安排。<b>当前状态：方案暂停，未获用户恢复实施授权。</b></p></details>'
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
        notice+='<details><summary>历史设施检查点明细（22:04）</summary><p>'+html.escape(checkpoint_data['actualReassessmentAt'])+'；当时未获新的支持桌面设施或独占VM窗口，原程序未运行，许可未知，无可靠开测时刻。</p><table><thead><tr><th>负责人</th><th>缺项</th><th>当时解除动作</th><th>当时估计</th></tr></thead><tbody>'
        for row in checkpoint_data['owners']:
            notice+='<tr>'+''.join('<td>'+html.escape('暂无可靠时间' if row[key] is None else str(row[key]))+'</td>' for key in ['owner','missing','action','readyEstimate'])+'</tr>'
        notice+='</tbody></table><p>当时的下一次重估记录：'+html.escape(checkpoint_data['nextReestimate'])+'</p></details>'
    body=capability_matrix.decode('utf-8')+body
    new=heading.group()+notice.encode()+b'<div id="vm-blob-control-architecture-review">'+body.encode()+b'</div><details data-vm-control-review-history><summary>'+ '历史静态对照（旧默认保留推荐已撤回，仅供证据追溯）'.encode()+b'</summary>'+original_history+b'</details>\n'
    new=repair_external_links(new.decode('utf-8')).encode('utf-8')
    merged=page[:heading.start()]+new+page[end:]
    return repair_external_links(merged.decode('utf-8')).encode('utf-8')
