"""VM/self/target review, documentation only; declared nodes are not counted as CLR controls."""
from pathlib import Path
from html import escape as E
from hashlib import sha256
import json, os, re
ROOT=Path(__file__).resolve().parents[2]
PAGE=ROOT/'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护/优化方案/optimization-o06.html'
OUT=ROOT/'artifacts/analysis/blob-controls-algorithm-structure-20261024'
FOUR_DECISIONS=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/four-pending-final-architecture-decision-v2-20261025.json'
FOUR_DECISIONS_SHA='815EC47E4EE2ED6096E912EFE653C5646F62E3C68B91EA69ED295230086F7113'
FOUR_EVIDENCE=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/vm-four-duty-evidence.snapshot.json'
FOUR_EVIDENCE_SHA='0B8B31EF2F3A6A11D4B57729FD5B7B490ED3AC97816CBC7A35B9FC5E018D19D9'
FOUR_SELF=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/four-pending-self-duty-review-20261025.json'
FOUR_SELF_SHA='F92F3715697E7D8A0358A6A1AFBFE6AA7D14561FAA7443A1AEE2D86DFEA35A43'
RESULT_DIAGRAM_INPUT=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/current-result-side-by-side-design-input-20261025.json'
RESULT_DIAGRAM_INPUT_SHA='59B6AD113515795C78797780FEAF7693FB9509E41F6D78BE89E793A223A0CFED'
RESULT_DIAGRAM_BASELINE_SHA='48321D628A08F4B00A62EE3B81AD0957DA20BF63F7F6436E7830BB126B24380E'
CROSS_TOOL_RESULT_INPUT=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/cross-tool-result-declaration-design-input-v3-20261025.json'
CROSS_TOOL_RESULT_INPUT_SHA='1C59DD560CC58FD846779494392767EFB14AAD38FE169B0808F9CE11A17D7132'
CROSS_TOOL_RESULT_V2=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/cross-tool-result-declaration-design-input-v2-20261025.json'
CROSS_TOOL_RESULT_V2_SHA='BFB7F77766CB46289CE1554262A6AE505C2E6B0E47A8BDE215D27755AF56565D'
CROSS_TOOL_RESULT_V1=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/cross-tool-result-declaration-design-input-20261025.json'
CROSS_TOOL_RESULT_V1_SHA='0FEBEB20A69F0B7DEC55D21A102A2518D894F32623FE26D0C4917C3784F8089D'
CROSS_TOOL_RESULT_BASELINE_SHA='CE620F44B88A6ABE462A3A88C1EE9BDD8C5A54BEBFE6EAAB184E6B3A89D32300'
FOUR_SCREENSHOT=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/evidence/vm-blob-current-result-user-20261025.png'
FOUR_SCREENSHOT_SHA='F1FF72C266C590D18774041E3B7DBDDD668B2EBC085B9815944A6AF7D61C9000'
O06_BASELINE_SHA='9B0401B85C438444F7C0782C70C4B964CBCC7291FADCF302C1A5F58D51E97D63'
BLOB_FINAL_INPUT=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/blob-complete-final-review-design-input-20261025.json'
BLOB_FINAL_INPUT_SHA='8798577FECDB611C3A2BEF14E0ABA8B09D3509AA4069294A1EB9F75410494E05'
BLOB_CONTRACT_AUDIT=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/blob-final-contract-audit-20261025.json'
BLOB_CONTRACT_AUDIT_SHA='3E2521783E4B1C82F3A01513E9CDE270274471E4B1CE1EE5BD253396BCBEDF18'
BLOB_CHAIN_AUDIT=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/blob-final-chain-audit-20261025.json'
BLOB_CHAIN_AUDIT_SHA='F645EC41D06C2B1E38069FA87BF0A344BB7B33B7D8DB20237B3D7960DABCFAF9'
BLOB_IMPLEMENTATION_AUTH=ROOT/'docs/collaboration/BLOB_IMPLEMENTATION_AUTHORIZATION_20261025.json'
BLOB_IMPLEMENTATION_AUTH_SHA='BBD44F5A3EABB5F30A089E3E9D1ED54CF08064270E094FDBC77B6457902FF778'
BLOB_IMPLEMENTATION_PLAN=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/implementation/blob-implementation-master-plan-20261025.md'
BLOB_IMPLEMENTATION_PLAN_SHA='947F6C82C022F63D99B8DE6D5590F21594D502CC5BC05B2B259C8E4304F9A74D'

def load_four_decisions():
 raw=FOUR_DECISIONS.read_bytes()
 assert sha256(raw).hexdigest().upper()==FOUR_DECISIONS_SHA,'final four-item architecture decision drift'
 evidence=FOUR_EVIDENCE.read_bytes()
 assert sha256(evidence).hexdigest().upper()==FOUR_EVIDENCE_SHA,'frozen four-item evidence drift'
 assert sha256(FOUR_SCREENSHOT.read_bytes()).hexdigest().upper()==FOUR_SCREENSHOT_SHA,'user screenshot drift'
 decision=json.loads(raw.decode('utf-8-sig'))
 assert decision['baselinePageSHA256']==O06_BASELINE_SHA
 assert decision['status']=='FINAL_FOUR_ITEM_ARCHITECTURE_RECOMMENDATION_FOR_USER_REVIEW_NOT_IMPLEMENTATION'
 by_name={row['name']:row for row in decision['rows']}
 assert set(by_name)=={'BlobDataAnalysisControl','JudgmentEditorControl','ToolIcon','ToolBarControl'}
 assert all(row['primaryGroup']=='组合链' for row in by_name.values())
 return decision,by_name

def load_blob_final_review():
 raw=BLOB_FINAL_INPUT.read_bytes()
 assert sha256(raw).hexdigest().upper()==BLOB_FINAL_INPUT_SHA,'frozen Blob final-review input drift'
 assert sha256(BLOB_CONTRACT_AUDIT.read_bytes()).hexdigest().upper()==BLOB_CONTRACT_AUDIT_SHA,'Blob contract audit drift'
 assert sha256(BLOB_CHAIN_AUDIT.read_bytes()).hexdigest().upper()==BLOB_CHAIN_AUDIT_SHA,'Blob chain audit drift'
 d=json.loads(raw.decode('utf-8-sig'))
 assert d['baselinePageSHA256']=='A4FDA6064E2501CC78AE1F44944ED1452B2DEA4FFA90673395F76DD5F293FA7D'
 assert d['status']=='ARCHITECTURE_DECISIONS_CLOSED_FOR_WHOLE_USER_REVIEW_NOT_IMPLEMENTATION'
 assert d['summary']['architectureSelectionUndecided']==0 and len(d['decisions'])==9
 assert len(d['criticalGates'])==5 and len(d['implementationSequence'])==6 and len(d['userReview'])==2
 return d

def load_blob_implementation_authorization():
 raw=BLOB_IMPLEMENTATION_AUTH.read_bytes()
 assert sha256(raw).hexdigest().upper()==BLOB_IMPLEMENTATION_AUTH_SHA,'Blob implementation authorization drift'
 assert sha256(BLOB_IMPLEMENTATION_PLAN.read_bytes()).hexdigest().upper()==BLOB_IMPLEMENTATION_PLAN_SHA,'Blob implementation plan drift'
 d=json.loads(raw.decode('utf-8-sig'))
 assert d['approved']['scopeAndCompatibilityU1'] and d['approved']['editingBehaviorU2']
 assert d['approved']['implementationWithinOriginalO06'] and d['approved']['independentTestingAndFixRetest']
 assert d['approved']['unifiedReleaseAfterAcceptance'] and not d['approved']['automaticMergeOrPush']
 clarifications=d.get('executionClarifications',[])
 assert any('无需单独许可探测' in x.get('instruction','') for x in clarifications),'HALCON license clarification missing'
 assert any('native实际加载、算子调用仍未验证' in x.get('operationalEffect','') for x in clarifications),'native verification boundary missing'
 return d

def final_review_table(headers,rows):
 return '<div class="br-scroll"><table><thead><tr>'+''.join('<th>'+E(str(h))+'</th>' for h in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+str(c)+'</td>' for c in row)+'</tr>' for row in rows)+'</tbody></table></div>'

def final_review_flow(steps):
 return '<div class="br-flow">'+''.join('<div><b>'+E(s['name'])+'</b><small>'+E(s['deliver'])+' · 门：'+E(s['gate'])+' · 前置：'+E('、'.join(s['depends']) or '无')+'</small></div>' for s in steps)+'</div>'

def replace_review_section(page,anchor,next_anchor,body):
 start=re.search(r'<h([1-6])\b[^>]*id="'+re.escape(anchor)+r'"[^>]*>.*?</h\1\s*>',page,re.S)
 end=re.search(r'<h[1-6]\b[^>]*id="'+re.escape(next_anchor)+r'"',page[start.end():] if start else '',re.S)
 assert start and end,(anchor,next_anchor,'review anchor missing')
 cut=start.end()+end.start()
 return page[:start.end()]+'\n'+body+'\n'+page[cut:]

def inject_review_after_heading(page,anchor,marker,body):
 begin='<!-- '+marker+':BEGIN -->';finish='<!-- '+marker+':END -->'
 if begin in page and finish in page:
  a=page.index(begin);b=page.index(finish,a)+len(finish)
  if a>0 and page[a-1]=='\n':a-=1
  if b<len(page) and page[b]=='\n':b+=1
  page=page[:a]+page[b:]
 h=re.search(r'<h([1-6])\b[^>]*id="'+re.escape(anchor)+r'"[^>]*>.*?</h\1\s*>',page,re.S)
 assert h,('heading missing',anchor)
 return page[:h.end()]+'\n'+begin+'\n'+body+'\n'+finish+'\n'+page[h.end():]

def render_final_decision_body(d):
 decision_rows=[]
 for x in d['decisions']:
  detail=x.get('details','')
  if x['id']=='FD04': detail+=' '+x.get('poseClosure','')
  if x['id']=='FD07': detail+=' '+x.get('contourContract','')+' '+x.get('compatibility','')
  decision_rows.append([E(x['id']+' · '+x['topic']),E(x['decision']),E(detail),E(x.get('gate',''))])
 return final_review_table(['冻结决定','现行技术推荐','补充语义/兼容边界','进入实施前的门'],decision_rows)

def render_scope_rows(d):
 return final_review_table(['原范围映射','必须覆盖','复用','替换/退出'],[[E(x['id']+' · '+x['area']),E(x['must']),E(x['reuse']),E(x['replace'])] for x in d['finalScope']])

def render_gate_rows(d):
 return final_review_table(['门','阻断什么','缺口/解除证据','责任/阶段'],[[E(x['id']),E(x['severity']+'；'+x['gap']),E(x['resolution']),E(x['owner']+'；'+x['when'])] for x in d['criticalGates']])

def render_blob_final_reconciliation(page):
 d=load_blob_final_review(); summary=d['summary']; rel=lambda p:E(os.path.relpath(p,PAGE.parent).replace('\\','/'))
 auth=load_blob_implementation_authorization()
 input_link='<a href="'+rel(BLOB_FINAL_INPUT)+'">总监冻结输入</a> · SHA256 '+BLOB_FINAL_INPUT_SHA
 audit_links=' · <a href="'+rel(BLOB_CONTRACT_AUDIT)+'">合同审计</a> SHA256 '+BLOB_CONTRACT_AUDIT_SHA+' · <a href="'+rel(BLOB_CHAIN_AUDIT)+'">算法/链路审计</a> SHA256 '+BLOB_CHAIN_AUDIT_SHA
 auth_link='<a href="'+rel(BLOB_IMPLEMENTATION_AUTH)+'">用户实施授权记录</a> · SHA256 '+BLOB_IMPLEMENTATION_AUTH_SHA
 plan_link='<a href="'+rel(BLOB_IMPLEMENTATION_PLAN)+'">Blob实施主计划</a> · SHA256 '+BLOB_IMPLEMENTATION_PLAN_SHA
 checkpoint=ROOT/'artifacts/worktrees/blob-s1-component-metadata-20261025/artifacts/validation/blob-s1/delivery/s1-first-checkpoint.json'
 independent_receipt=ROOT/'artifacts/independent-testing/START-BLOB-01/s1-own-contracts-07999183/receipt.json'
 independent_report=ROOT/'artifacts/independent-testing/START-BLOB-01/s1-own-contracts-07999183/independent-report.json'
 prep_receipt=ROOT/'artifacts/independent-testing/START-BLOB-01/first-preparation-delivery.json'
 bridge_receipt=ROOT/'artifacts/worktrees/blob-s1-image-bridge-20261025/artifacts/validation/blob-s1-image-bridge/handoff-receipt.json'
 boundary_receipt=ROOT/'artifacts/independent-testing/START-BLOB-01/bridge-own-boundaries-84d88634/receipt.json'
 boundary_report=boundary_receipt.with_name('report.json')
 fixed_bridge=boundary_receipt.with_name('fixed-acceptance-receipt.json')
 fixed_data=json.loads(fixed_bridge.read_text(encoding='utf-8-sig'));assert fixed_data['status']=='FIXED_BRIDGE_LIMITED_INDEPENDENT_PASS' and fixed_data['uniqueCases']==20
 fixed_bridge_sha=sha256(fixed_bridge.read_bytes()).hexdigest().upper()
 assert fixed_bridge_sha=='80447A64DB0BD79E4C0328335AEFD2DCEC441232BD15A95B5784E870ADAAF607'
 identity_receipt=Path(fixed_data['identity']['path'])
 identity_sha=sha256(identity_receipt.read_bytes()).hexdigest().upper()
 assert identity_sha==fixed_data['identity']['sha256']=='864BEC683B40336751AF3DCB3ED1B12DAD5D32DF62D102D381620DA9F9BE0913'
 assert fixed_data['identity']['sourceGitAndEmbeddedPdb']==6 and fixed_data['identity']['binaryRows']==101
 assert fixed_data['pass']==20 and fixed_data['fail']==0 and fixed_data['commit']=='84d88634430a54e6d141e4bb0e02ceef7e4a1a31'
 assert fixed_data['identity']['binaryDifferences']==0 and fixed_data['identity']['sourcePdbDifferences']==0
 boundary_data=json.loads(boundary_receipt.read_text(encoding='utf-8-sig'));assert boundary_data['pass']==20 and boundary_data['fail']==0
 assert boundary_data['status']=='LIMITED_BRIDGE_20_PASS_PENDING_PRODUCER_PE_BINDING'
 assert json.loads(boundary_report.read_text(encoding='utf-8-sig'))['passed']==20
 for entry in boundary_data['files']:
  assert sha256(Path(entry['path']).read_bytes()).hexdigest().upper()==entry['sha256'],'bridge evidence drift'
 bridge_fact='独立桥接首轮16项复测加4项新边界，共20项unique案例PASS/0 FAIL（不累计成36项）；自有PID41436自然exit0。真实HALCON像素/Domain经SDK Normalize和DeclaredOutputSnapshot，覆盖unsupported source、错frame拒绝清理、实际native composite owner释放及发布reader/frame；46文件原件与接收副本共92项post核验零差异。固定验收回执SHA256 '+fixed_bridge_sha+'，PE身份回执SHA256 '+identity_sha+'。本批captured桥接PE已绑定commit84d88634；101个binary delta文件独核零差，6/6改动源码、embedded PDB与Git84 blob身份相符，35个captured runtime/harness文件和6个实际loaded模块路径匹配。首轮身份比较因未列辅助PDB停止的失败记录保留，5个附属PDB/XML分类后保留在接收manifest；没有删改旧失败记录。该证据不证明完整仓构建来源或全解决方案Release provenance。真实WorkflowBindingContext admission、多图逐元素来源、并行Unwrap变更纪律及完整Blob/UI/Release仍未验。G1/T0仍开放。'
 owner_receipt=ROOT/'artifacts/independent-testing/START-BLOB-01/owner-own-pathfix-dd380367/corrected-receipt.json'
 owner_report=owner_receipt.with_name('report.json')
 old_owner_receipt=ROOT/'artifacts/independent-testing/START-BLOB-01/owner-own-dd380367/receipt.json'
 old_owner_report=old_owner_receipt.with_name('report.json')
 owner_before=ROOT/'artifacts/worktrees/blob-s1-owner-migration-20261025/artifacts/validation/blob-s1-owner/repository-negatives-02-before-unknown-fix.json'
 owner_after=ROOT/'artifacts/worktrees/blob-s1-owner-migration-20261025/artifacts/validation/blob-s1-owner/repository-negatives-03-after-unknown-fix.json'
 owner_data=json.loads(owner_receipt.read_text(encoding='utf-8-sig'))
 owner_report_data=json.loads(owner_report.read_text(encoding='utf-8-sig'))
 owner_sha=sha256(owner_receipt.read_bytes()).hexdigest().upper()
 assert owner_sha=='21E6F3D7B4DE615C5D217525C4B8F31E1F347D20004333F606A68CE9837F8D33'
 assert owner_data['status']=='FIXED_OWNER_REPOSITORY_LIMITED_INDEPENDENT_PASS_PATH_PRESERVED'
 assert owner_data['commit']=='dd380367620eec0d92bd540e8111e1a7289fba37' and owner_data['pass']==19 and owner_data['fail']==0
 assert owner_data['pathPreservedFiles']==64 and owner_data['postChecks']==128 and owner_data['postDifferences']==[]
 assert owner_data['sourceProtection']['originalSourceBefore']==owner_data['sourceProtection']['originalSourceAfter']
 assert owner_data['loadedEncoding'][0]['sha']=='65B457623602962BB2D420EB5EBA0D64C88A676326FF8E37F2C543E734BB6200'
 assert owner_data['loadedOpenCvOrHalconNativeCount']==0 and owner_data['naturalExit']==0
 assert owner_report_data['passed']==19 and owner_report_data['failed']==0
 assert sha256(old_owner_receipt.read_bytes()).hexdigest().upper()==owner_data['historicalReceiptSHA']=='C8D995AE7DCE00A64A6C9B675768D3EDC1C1B126CA617ABFF564CA929795B926'
 assert len(json.loads(old_owner_receipt.read_text(encoding='utf-8-sig'))['postDifferences'])==4
 owner_fact='旧binding按真实身份/声明映射到候选owner并在Repository隔离fixture中核验：独立回放19 PASS/0 FAIL；64个保留相对路径文件、128项原件/接收副本检查零差异。覆盖重复NodeId、未知Workflow/Node成员、双次保存及精确备份、文件锁定保存失败时保留目标和旧备份。原源保护前后SHA/长度/mtime完全相同；实际加载的.NET 9 Encodings.Web SHA与批准值一致，未加载OpenCV/HALCON native。当前有限回执SHA256 '+owner_sha+'；独测报表SHA256 '+sha256(owner_report.read_bytes()).hexdigest().upper()+'。此前平铺回执SHA256 '+owner_data['historicalReceiptSHA']+' 的4条差异是同名文件跨RID/TFM覆盖碰撞；撤回此前“零差异”说法，旧回执与失败记录保留。该fixture仍不能证明深层参数/全局变量/Device未知成员保留、完整Snapshot事务、真实资源Workflow、生产插件发现/持久ABI4 admission、并发Unwrap/多图来源、UI/VS或全解决方案Release；G1/T0仍开放。'
 workflow_before=ROOT/'artifacts/worktrees/blob-s1-owner-migration-20261025/artifacts/validation/blob-s1-owner/workflow-build-before-bridge.log'
 workflow_after=ROOT/'artifacts/worktrees/blob-s1-owner-migration-20261025/artifacts/validation/blob-s1-owner/workflow-build-after-bridge.log'
 evidence_links=' · '.join('<a href="'+rel(p)+'">'+label+'</a>' for p,label in [
  (checkpoint,'S1首件冻结回执'),(independent_receipt,'独测合同回执'),(independent_report,'37项独测报表'),
  (prep_receipt,'HALCON设施准备/独测回执'),(bridge_receipt,'图像桥隔离回执'),
  (fixed_bridge,'20项独立桥接固定验收回执'),(identity_receipt,'PE来源身份核验回执'),(boundary_receipt,'桥接补证前历史回执'),(boundary_report,'20项独立桥接报表'),(owner_receipt,'Repository路径保留独立回执'),(owner_report,'Repository 19项独测报表'),(old_owner_receipt,'旧平铺回执（4条碰撞差异保留）'),(owner_before,'未知成员修复前开发侧反例'),(owner_after,'未知成员修复后开发侧反例'),(workflow_before,'桥接前Workflow编译日志'),(workflow_after,'桥接后Workflow编译日志')])
 status_rows=[
  ['S1首件冻结','基线30d94078；首件固定提交0799918373bb5f00ac5a00f30591a7e79463a84e（codex/blob-s1-component-metadata），冻结树clean、未合并/推送。冻结回执SHA256 4EE87330DE1A2538E782628A815032DADC1DD24D36C782CFAC767B26D92AC078；源码26项、payload 5项、日志10项合计41/41经核验。局部合同/设施通过不等于G1或Blob产品通过。'],
  ['合同组件与独测','Contracts/Runtime/Runner隔离Release构建0警告/0错误；开发runner两阶段PASS。独测自有runner引用固定PE，37 PASS/0 FAIL；独测回执SHA256 9A8E4C523F3B2F5D79F343BC9CD4277BFA20405D8D2561F568057F47C718DD9A、报表SHA256 892A52202883F22CF01835232E1758587DD588D9382680F2CC3DFF1F90E52133。strict serializer4拒绝；ABI4目录候选拒绝不等于真实ABI4 PE admission已验；显式4→5用例仅验证API形状，真实document owner mapping未实现。'],
  ['HALCON与图像桥','现有加密狗直接使用，许可不独立设门/不探测。独测vendor设施1×1正控与坏path负控通过，只证明vendor设施。图像桥从07999183另建clean隔离worktree并固定提交84d88634430a54e6d141e4bb0e02ceef7e4a1a31，桥接回执SHA256 901E68F4EC03A199701D9C6400BD151FE20EA472D9F58F92ED6BE15609CBBC42；6个源SHA/长度逐项匹配。HAL adapter构建0警告/0错误，25个native DLL与批准源SHA匹配。开发侧BridgeCheck最终unique13个实际案例PASS，含Gray 1×1、奇宽BGR/BGRA、非满域/空域、来源释放后reader lease及normalize-publish-reader链；首轮10 PASS/1 fixture FAIL原样保留，修正仅限run-list fixture后定向PASS。'+bridge_fact+''],
  ['Workflow前后差异','独立owner-migration树从07999183起步；桥接前14:00 Workflow编译有4个不同CS0535（BorrowedImage缺Layout/Frame/EffectiveDomain/AcquirePixels），作为依赖重排证据；桥接后父集成Workflow Release编译exit0、0错误、360警告。编译闭合仍不等真实owner映射、Repository fixture或产品链验收。'],
  ['Repository有限独立核验',owner_fact],
  ['G1仍开放的缺口','有限Repository fixture已验证其声明范围内的旧binding身份映射，不等于完整owner链闭合。深层参数/全局变量/Device未知字段策略、完整Snapshot事务、真实resource Workflow、生产插件发现与持久ABI4 PE admission、并发Unwrap/多图来源、Blob UI/VS及全解决方案Release仍未验；G1/T0未通过。'],
  ['后续门与排期','桥接20项和Repository路径保留19项是两组独立、有限范围的独测结果，不合并分母。旧平铺回执4条路径碰撞差异继续保留并撤回零差异主张。S2/S3其他生产消费者继续等待G1；用户可运行首包试用与统一Release仍未验。'],
  ['执行限制','不自动merge/push；旧变量机制只按FD03重接DocumentSession/scope/正式Run代，不原样恢复旧收口。历史PASS/FAIL不继承为本轮验证。']]
 user_table=final_review_table(['改什么','改完能看到什么','当前进度（实现/开发/独测/用户）'],[
  [E('Release依赖'),E('Release目录使用同次Release生成的Contracts/Runtime，不再混入Debug依赖。'),E('隔离版构建与核心独测通过；D盘交付版未更新，尚未交用户验收。')],
  [E('参数和保存规则'),E('默认值、有效值和未设置状态按统一规则校验；无效保存保留原文件。'),E('已实现；开发字段保存13项通过；独测核心及补充用例有通过记录；汇总核对待补。')],
  [E('旧方案归属和恢复'),E('旧绑定按所属工作流迁移；保存失败不覆盖原方案和备份。'),E('开发快照16项通过；独测归属6项、恢复5项通过；待匹配版本交付后验收操作体验。')],
  [E('图像和区域结果'),E('同一轮运行的图像、ROI与结果保持对应，读者按生命周期释放资源。'),E('已接入；桥接20项、资源3项独测通过；另有受限边界通过回报，汇总待核。')],
   [E('保存提示跟随主窗口'),E('修复保存和关闭确认时的提示窗口归属。'),E('修复已提交；UI Release构建通过；主题资源补齐后STA组件测试4/4通过；真实主窗口保存端到端体验仍待验证。')]
 ])
 isolated_exe=r'C:\Users\houzhongjie\.codex\visualizations\2026\10\23\01a1663d-2d35-7bd2-bc12-ab0d4051bb8e\s1-release-configuration\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe'
 independent_root=r'C:\Users\houzhongjie\.codex\visualizations\2026\10\22\01a1620c-5a60-7fb1-a1b1-9b85173896e9\independent-final-s1-20261025'
 independent_facts=[('ns',6,'D19CDCFE0544AE7BBA420F870E50531F957D72146D580B8BE28F6D4694764F02','7F14C03B1731137680AFA82017FCFBF0194F771EC9FB4D93A3F4413226C62D03',41552),('nm',5,'0754F9C624A643B077BF5633303ADA3D01AFC53C9F93EB66747C46C09525029D','2C1264D24D8B7FF7F590A9D45638D63DBD59462149333AA403551E5D271740BA',39420),('nf',20,'7781683174C9A1A0DE861156DEACCCCDC223C024F1CC483413FAABC2F3DF77D0','60779573DD57DF7392D4F8BDD3FE19DE2F6FFEA42CE4EAFCEB9DD8B7CA05ED4C',62512),('nr',3,'4285EA7FFBB5933193B0A4A81AF9C3B2D58F9F6AED28675FD0451326334EE9B9','ABEE8336C172218A7135E08502149B8AB05DA75F0F3C332578CE09CDE2824B04',48812)]
 independent_evidence=' · '.join('<a href="file:///'+independent_root.replace('\\','/')+'/'+name+'/'+file+'">'+name+'/'+file+'</a> SHA256 '+digest for name,report_sha,report_hash,exit_hash,pid in independent_facts for file,digest in [('report.json',report_hash),('exit.json',exit_hash)])
 additional_independent='独立矩阵记录11条测试轨、113次通过执行（用例有重叠，不代表113个独立功能）；de58核心34项通过，整体G1仍未签核。format1直接保存用例因测试宿主缺少System.IO而阻塞，不是产品失败；完整UI/VS、性能、S2/S3未测，运行后源文件/副本复核待补。'
 current_detail_rows=[
  ['固定版本',E('提交 de58d829980defd1ad61e38fe8ed1c39aea7dd09；隔离构建目录 '+isolated_exe+'。这不是D:\\MyWork\\SunEyeVision_V2\\Release\\SunEyeVision_v1.0.0\\SunEyeVision主Release；主Release当前SHA256 D1ABFA7BBD846A61632C470AD89A3BA1AE10CDED5160EBEE4FAF0F019223C8BD，未被本轮替换。隔离exe SHA256 3D95711F9736F4CF65525DC4687E956608F8DD8FB848D7312B86826E2C1A1265。')],
  ['核心独立测试',E('合计34/34 PASS：ns旧方案归属/拒绝6项，nm快照恢复5项，nf字段合同20项，nr资源边界3项。四份报告的FixedResolver均指向独立n/Release；4个独立PID均exit0。逐项报告、退出回执及SHA见证据链接。该有限结果不代表G1放行。')],
  ['程序启动观察',E('de58隔离exe已启动、主内容已显示。PID 52456由用户手动关闭，进程退出已观察到，但退出码未采集，不推断为exit0。')],
   ['保存提示组件复测',E('修复提交66ff2c20；UI Release构建0错误。主题资源夹具补齐后4/4 PASS、0 FAIL、0跳过，日志SHA256 FC103644A85A4659EAC709594A96E540D12E9A433AFD10BFEE9D7E4F331390FB，TRX SHA256 D01A44610616DBAA12AB3B28C7DA5E6D5DB681AA489E53532471CFB823268D6。前三项覆盖模拟延迟关闭宿主及真实ThemeMessageDialog/HWND和Yes/No/Cancel，第四项覆盖真实Prepare错误owner/reentry；不是生产MainWindow完整保存E2E。')],
   ['补充结果与收口节点',E(additional_independent+'独立矩阵SHA256 627B906359349FFCDCE976B1582755602D051D5B3A3B508D172E6FCA1F0DA1AF；锚点语义补件SHA256 7C12E3570DA01AD7F43581CEC338C3A3F911B8BA656B811BDEAA299153DC6010。保存提示跟随主窗口修复提交66ff2c20；STA组件复测于17:06左右完成4/4。矩阵状态仍列Owner增量待纳入，不能以组件用例替代独立Owner验收；主窗口用户体验和D盘交付仍未完成。为独立复验，计划以de58完整Release为底，仅叠加Owner修复所需文件并记录双提交、逐文件来源及最终包身份；当前尚未出包。S1仍G1 open，无可靠最终完成时间。')],
  ['旧阶段材料',E('原14:35摘要、合同37项、图像桥20项、Repository19项及全部长SHA/原始失败/修正记录保留在下方历史证据区；各项属于各自提交和时点，不合并成de58的34项。')]
 ]
 evidence_detail='<details class="br-evidence"><summary>展开：de58构建、当前独测缺口与完整历史证据</summary><h4>本轮de58证据与状态边界</h4>'+final_review_table(['证据项','可确认内容'],current_detail_rows)+'<p><b>当前机器证明，不需要用户重复手工测：</b>Release构建结果、程序集/PDB来源与46份依赖副本身份、自动化用例结果均由日志/哈希证明；用户手验只补界面与真实操作体验，不代替这些自动检查。</p><h4>原有技术证据、失败与修正（按原版本保留）</h4><p>以下37/20/19项为先前固定版本结果，历史时点、回执、分母和失败修正保持原样；不能冒充de58同源独测。</p><p>'+E(bridge_fact)+' 独测回执SHA256 '+sha256(boundary_receipt.read_bytes()).hexdigest().upper()+'；报表SHA256 '+sha256(boundary_report.read_bytes()).hexdigest().upper()+'</p><p><b>原进度卡（2026-10-25 14:35历史快照，现行状态以上方为准）：</b>当前活动目标仅为持续完成S1，并由固定源码与PE形成真实G1放行/不放行结论；不推进S2/S3，后续阶段等待明确授权。U1/U2及原O06范围保留。桥接独测20项有限PASS与Repository路径保留独测19项有限PASS是两套独立证据，不合并分母；Repository回放覆盖64个保留路径文件、128项副本核验零差异。此前平铺接收的4条跨RID/TFM同名碰撞差异已明确撤回“零差异”说法并保留历史回执。深层参数/全局变量/Device、Snapshot事务、真实资源Workflow、生产发现与持久ABI4 admission、UI/VS/Release仍开放；T0/G1未通过，不能宣布Blob产品通过。</p>'+evidence_links+'<h4>原U1/U2实施状态与历史记录</h4>'+final_review_table(['项目','批准状态与记录'],[[E(k),E(v)] for k,v in status_rows])+'</details>'
 user_view=('<div class="br-note" id="o06-blob-final-review"><h3>这轮S1在改什么，现在能不能测？</h3><p>这轮先把工具参数、旧方案保存和图像结果的底层规则接起来，并修正Release依赖配置；<b>de58核心独测回报34/34通过，但Owner保存弹窗问题尚未修好，合同/资源边界还有待测项，所以S1/G1没有闭合，也不是完整Blob新界面交付。</b></p><p><b>本轮可打开的固定测试版本：</b><code>'+E(isolated_exe)+'</code>（C盘隔离新包）。D盘主Release仍是旧版本，不能用来判断本轮优化效果。</p><h4>优化项、实际效果和怎么验证</h4>'+user_table+'<h4>现在你可以测</h4><ol><li>只启动上面的de58隔离包，确认主窗口和内容正常显示；已自动验证启动和内容呈现，但这不代表完整人工验收。</li><li>使用工作流副本，打开已有Blob配置，修改一个有效参数，保存后关闭并重开；看修改值是否保留。只做允许的交互，不要用原方案测试迁移。</li><li>用一张简单图片跑一次Blob，核对结果数值和叠加区域是否都对应当前输入及当前运行。取消、异常恢复、多图来源和Owner弹窗目前不作为已通过项。</li></ol><p><b>暂时不用你手工复测：</b>Release构建、依赖/PDB身份、自动化用例属于机器证据；它们不需要用户靠界面猜测或重复执行。<b>目前不能验收：</b>Owner保存弹窗、未完成的合同/choices/resource边界、完整Blob新UI、用户主Release更新、G1及整体交付。</p><p><b>单列未完成：保存弹窗Owner。</b>用户反馈已确认问题，修复未落地、开发和独测均未完成。此处保留为阻断项，不以de58 34/34核心结果覆盖。</p>'+evidence_detail+'</div>')
 independent_evidence=' · '.join('<a href="file:///'+independent_root.replace('\\','/')+'/'+name+'/'+file+'">'+name+'/'+file+'</a> SHA256 '+digest for name,report_sha,report_hash,exit_hash,pid in independent_facts for file,digest in [('report.json',report_hash),('exit.json',exit_hash)])
 additional_independent='catalog/resource 37项、scope/projection 21项、choices 4项、受限resource 4项、Snapshot 5项、RGBA 6项均有通过回报；用例存在重叠，完整汇总矩阵预计17:05补齐，未核对前不相加为独立总数。'
 evidence_detail=evidence_detail.replace(evidence_links+'<h4>原U1/U2实施状态与历史记录</h4>',independent_evidence+'<p>'+E(additional_independent)+'</p>'+evidence_links+'<h4>原U1/U2实施状态与历史记录</h4>')
 user_view=('<div class="br-note" id="o06-blob-final-review"><h3>本轮优化进度（更新至2026-10-25 17:10）</h3><h4>1. 改什么</h4><p>先统一Blob参数校验与保存、旧工作流绑定迁移和图像/区域结果交接，并修正Release依赖来源。当前范围是S1底层规则，不是完整Blob新界面交付。</p>'+user_table+'<h4>2. 进度分开看</h4><p><b>实现：</b>de58已包含参数/保存规则、旧绑定迁移、图像结果桥接和匹配Release依赖。保存提示跟随主窗口的修复已提交（66ff2c20）。</p><p><b>开发测试：</b>全量Release构建通过；字段保存13项、快照16项通过。新修复UI Release构建通过；补齐主题资源夹具后，STA组件测试4/4通过。前三项用模拟延迟关闭宿主验证真实ThemeMessageDialog/HWND与Yes/No/Cancel，第四项验证真实Prepare错误owner/reentry；不等于生产MainWindow完整保存E2E。</p><p><b>独立测试：</b>固定de58核心34/34通过。独立矩阵记录11条测试轨、113次通过执行（存在重叠，不能视为113个独立功能）；format1直接保存用例因测试宿主缺少System.IO而阻塞，并非产品失败。运行后源/副本复核待补，完整UI/VS、性能和S2/S3未测。矩阵仍将Owner增量列为待纳入，G1未签核。</p><p><b>用户体验：</b>D盘主Release尚未更新；还没有可供你手工验收的本轮交付版。生产主窗口保存体验与完整Blob界面也未验收。</p><h4>3. 何时完成</h4><p>STA组件复测于17:06左右完成；截至17:10，独立Owner增量核收、最终组合包验证和D盘交付仍未完成。下一步按de58完整Release叠加Owner必要修复文件，记录双提交、逐文件来源和最终包身份，再做组合独立复验。17:15是有延期风险的条件性结论检查点，不是承诺交付时间；当前没有可靠最终ETA。</p><h4>4. 优化后效果</h4><p>核心规则已在匹配的隔离Release上通过34项独立测试，补充矩阵有113次通过执行但彼此重叠。组件复测确认弹窗owner/HWND行为在指定边界用例中通过；主窗口真实保存体验和D盘交付未验证，所以不能宣称整体优化已交付。</p><h4>5. 你怎样手工验收</h4><p>拿到匹配本轮改动的D盘交付版本后，再用工作流副本检查：①打开已有Blob配置并修改一个有效参数；②保存、关闭、重开，确认数值保留；③用一张图片运行简单流程，确认数值和叠加区域对应当前图片与本次运行。并实际操作生产主窗口中的保存提示和关闭确认。构建、依赖身份和自动化测试已有机器证据，不需要你重复手工执行。</p>'+evidence_detail+'</div>')
 top=user_view
 funnel=('<div class="br-note"><b>当前状态（2026-10-25 17:10）：</b>de58核心独测34项通过；独立矩阵记录113次有重叠的通过执行。保存提示组件复测4/4通过，但生产主窗口完整保存E2E未验证。最终组合包待生成/独测，D盘主Release未更新，G1仍开放。详情见 <a href="#o06-blob-final-review">首屏S1说明</a>。</div>'+final_review_flow(d['implementationSequence']))
 page=inject_review_after_heading(page,'br-title','O06-BLOB-FINAL-REVIEW',top)
 page=page.replace('href="#br-controls">41控件分级','href="#o06-current-control-mapping">31类控件与去留')
 page=inject_review_after_heading(page,'br-funnel','O06-BLOB-FINAL-FUNNEL',funnel)
 controls_note=('<div class="br-note"><b>当前状态（2026-10-25 17:10）：</b>de58核心独测34项通过；矩阵记录113次有重叠的通过执行。保存提示组件复测4/4通过，生产主窗口完整保存E2E未验；最终组合包待生成/独测，D盘主Release未更新，G1仍开放。其余31类控件清单保留。详见 <a href="#o06-blob-final-review">首屏优化项、进度和测试说明</a> 与 <a href="#br-blob-unified-scope">B1–B7原范围</a>。</div>')
 page=inject_review_after_heading(page,'br-blob-controls','O06-BLOB-CONTROL-STATUS',controls_note)
 decisions=render_final_decision_body(d)
 contract_body=('<p><b>现行技术推荐已冻结，未决技术选型为0。下表是当前唯一决策来源；用户只审核U1/U2，认可不自动恢复实施。</b> '+input_link+audit_links+'</p>'+decisions)
 page=replace_review_section(page,'br-contracts','br-acceptance',contract_body)
 scope=render_scope_rows(d)
 gates=render_gate_rows(d)
 sequence=final_review_flow(d['implementationSequence'])
 acceptance=('<p><b>原R1–R6/B1–B7与BV01–12、独测29维反例保持原分母和映射；下面是本轮冻结后的工作范围、阶段顺序和实施准入门。</b>未执行门均为阻断项，不记PASS；产品暂停。</p>'+scope+'<h4>六阶段依赖顺序（用户另行批准实施后）</h4>'+sequence+'<h4>五类未执行实施准入缺口</h4>'+gates+'<h4>用户整包审核</h4>'+final_review_table(['审核项','待审核内容','建议/产品影响'],[[E(x['id']),E(x['question']),E(x.get('recommended','')+' '+x.get('impact',''))] for x in d['userReview']]))
 page=replace_review_section(page,'br-acceptance','br-gaps',acceptance)
 corrections=d['documentIntegration']['replaceStale']
 history_rows=[]
 for x in corrections:
  old,new=x.split('→',1) if '→' in x else (x,'按本次冻结输入重绑定为现行合同/范围；该旧问题仅留历史，不再视为未决项。')
  history_rows.append([E(old.strip()),E(new.strip())])
 history=final_review_table(['冻结前旧表述（保留追溯）','现行更正'],history_rows)
 gaps=('<p><b>未决技术选型：0。产品状态：暂停。实施批准：否。运行/实机/性能/最终Release：未验证。</b>以下是审核边界和实施验收缺口，不再把它们写成设计决策待核。</p>'
       +final_review_table(['边界','当前状态','处理方式'],[
        ['U1/U2用户决策',E('整体架构/兼容代价；编辑行为。详见本页审核卡。'),'用户整体审核前不恢复实施。'],
        ['VM native细节',E('部分核心阈值/公式/NG/轮廓实机同版行为仍未知；不阻断我方HALCON明确口径。'),'VM只作已证能力边界；正确性按独立真值和已冻结业务语义。'],
        ['设计器/运行/性能/Release',E('均未执行或未达到G2–G5。'),'逐门收证；未跑不标PASS，遵循原SLO。'],
        ['独测revision06.questions',E('文件保留为早期历史输入，不再代表现行未决清单。'),'现行合同重绑定typed-v4+Domain delta、四项v2、跨工具v3和本输入；不改历史证据。']])
       +'<h4>冻结前表述的更正记录</h4>'+history+'<p>本核收是静态文档设计审核，不是产品实施、构建、运行、性能或人工验收通过。来源：'+input_link+audit_links+'</p>')
 page=replace_review_section(page,'br-gaps','br-evidence',gaps)
 unified=('<p><b>完整范围同一方案，不是新批次。技术推荐已冻结，待用户U1/U2整体审核；产品暂停，未批准实施。</b>统一合同、执行、结果、资源和消费者范围如下，旧B1–B7词条的冻结前措辞以本表为准。</p>'+scope
         +'<h4>不在本轮实施范围</h4>'+final_review_table(['排除项','边界'],[[E(x),E(d['scopeBoundary'])] for x in d.get('exclusions',[])])
         +'<h4>审核与实施状态</h4>'+final_review_table(['状态项','结论'],[[E(k),E(str(v))] for k,v in [('技术选型未决',summary['architectureSelectionUndecided']),('产品实施批准',summary['implementationApproved']),('运行/性能验证',summary['runtimeVerified']),('用户审核项',summary['userPackageReviewItems'])]])
         +'<p>冻结输入：'+input_link+audit_links+'</p>')
 page=replace_review_section(page,'br-blob-unified-scope','br-backend-acceptance',unified)
 algorithm_rows=[]
 for did in ('FD05','FD06'):
  x=next(z for z in d['decisions'] if z['id']==did)
  current=('现有源码有HALCON Threshold/区域流水线、可选形态学、连接与候选筛选；这是静态代码事实，运行正确性尚未独验。' if did=='FD05' else '当前代码及过往文案对并列顺序、数量上限、NG保留、统计集合与WhitePixelRatio分母有混写风险；现以冻结合同统一。')
  algorithm_rows.append([E(did+' · '+x['topic']),E(current),E(x['decision']),E(x.get('details',''))])
 algorithm=('<p><b>算法选择已冻结：</b>HALCON用于本轮Blob业务；VM native未证细节不作为复刻前置。这里区分当前源码事实、现行口径和未完成验收。</p>'+final_review_table(['步骤/输出','当前静态事实','冻结后应有语义','边界/验收'],algorithm_rows)
            +'<p><b>历史更正：</b>此前“端点/邻域/孔洞处理还需选择”现已具体冻结为闭区间阈值、Binary/有效域补集、已有形态学、8邻域、无新增填孔；此前建议ROI分母的审查子包意见已被总监排除，WhitePixelRatio保全幅正式语义。未实测仍是实施门，不是技术未决。</p>')
 page=replace_review_section(page,'br-algorithm-before-after','br-output-before-after',algorithm)
 x4=next(z for z in d['decisions'] if z['id']=='FD04');x7=next(z for z in d['decisions'] if z['id']=='FD07')
 output=('<p><b>输出合同已冻结，未实现验收。</b>图像Port、区域真值与显示轮廓分离；不把渲染投影当数据真源。</p>'+final_review_table(['对象','冻结合同','边界/兼容'],[
  ['Image/Domain/pose',E(x4['decision']),E(x4['details']+' '+x4['poseClosure'])],
  ['Blob图像与二值输出',E(x7['decision']),E(x7.get('details',''))],
  ['多环轮廓',E(x7['contourContract']),E(x7['compatibility'])]])
  +'<p><b>独立Reader与同Frame/owner语义需在G1–G4实证；本页不声称资源/运行链已通过。</b></p>')
 page=replace_review_section(page,'br-output-before-after','br-tool-project-compare',output)
 f1=next(z for z in d['decisions'] if z['id']=='FD01');f2=next(z for z in d['decisions'] if z['id']=='FD02');f3=next(z for z in d['decisions'] if z['id']=='FD03')
 project=('<p><b>现行工程推荐：</b>'+E(summary['finalRecommendation'])+' 现有单体WPF工具工程仍是当前事实，物理拆分、注册/发布与缺UI验证尚未实施。HALCON本轮固定；未来OpenCV不实现。</p>'+final_review_table(['边界','冻结后的推荐','迁移门'],[
  ['版本/装配',E(f1['decision']+' '+f1['details']),E(f1['gate'])],
  ['ToolId/Runtime/Wpf',E(f2['decision']+' '+f2['details']),E('G1/G2：唯一producer、同ToolId可选editor，元数据/工程/Release一致，缺UI默认页')],
  ['编辑/运行合同',E(f3['decision']+' '+f3['details']),E('G1/G3：同DocumentSession唯一owner、显式scope、事务反例通过')],
  ['候选工程结构',E(summary['finalRecommendation']),E('G2–G5全部未执行，不是迁移批准')]])
       +'<p>工程文件路径仅为推荐职责边界；目标Runtime与可选Wpf未因此宣称已经创建。父项目glob、HALCON vendor输入SHA、构建发布依赖均须按G1/G2清单核实。</p>')
 page=replace_review_section(page,'br-tool-project-compare','o06-engineering-class-map',project)
 migration=('<h4>Blob完整方案实施准入（设计决策已闭口，全部未执行）</h4>'+gates+'<p><b>不能将技术冻结、静态文档QA或31项去留推荐写成实施PASS/实施批准。</b> U1/U2审核后仍需用户另行明确恢复实施。</p>')
 migration_begin='<!-- O06-FINAL-MIGRATION-GATES:BEGIN -->';migration_end='<!-- O06-FINAL-MIGRATION-GATES:END -->'
 if migration_begin in page and migration_end in page:
  ma=page.index(migration_begin);mb=page.index(migration_end,ma)+len(migration_end);page=page[:ma]+page[mb:]
 gate=re.search(r'<div id="o06-control-migration-gate"[^>]*>.*?</div>',page,re.S)
 assert gate,'Blob migration gate missing'
 page=page[:gate.end()]+migration_begin+migration+migration_end+page[gate.end():]
 row_marker_begin='<!-- O06-BLOB-PRIMARY-REVIEW:BEGIN -->';row_marker_end='<!-- O06-BLOB-PRIMARY-REVIEW:END -->'
 if row_marker_begin in page and row_marker_end in page:
  ra=page.index(row_marker_begin);rb=page.index(row_marker_end,ra)+len(row_marker_end);page=page[:ra]+page[rb:]
 row=re.search(r'<tr id="o06-control-blobdataanalysiscontrol"[^>]*>.*?</tr>',page,re.S)
 assert row,'BlobDataAnalysisControl primary row missing'
 insert=row_marker_begin+'<div class="br-note"><b>本次整体核收：</b>FD01–FD09已冻结；BlobDataAnalysisControl按FD08退出专用结果分析UC，由宿主通用结果投影承接。同Run/Node/Element/Frame的结果表、树、图像场景和下游读取；当前VM静态链已有Display datalist/Colume/Num→动态列/组行/行选中高亮证据，行身份及同版实机仍归G3/G4验收。<a href="#br-blob-unified-scope">回B1–B7完整范围</a> · <a href="#o06-blob-final-review">看U1/U2审核摘要</a></div>'+row_marker_end
 last_td=page.rfind('</td>',row.start(),row.end());assert last_td>=0
 page=page[:last_td]+insert+page[last_td:]
 return page,{'inputSHA256':BLOB_FINAL_INPUT_SHA,'updatedSections':['br-title','br-funnel','br-blob-controls','br-contracts','br-acceptance','br-gaps','br-blob-unified-scope','br-algorithm-before-after','br-output-before-after','br-tool-project-compare','o06-control-migration-gate','o06-control-blobdataanalysiscontrol'],'technicalDecisions':len(d['decisions']),'technicalSelectionUndecided':d['summary']['architectureSelectionUndecided'],'implementationApproved':d['summary']['implementationApproved'],'runtimeVerified':d['summary']['runtimeVerified']}

def load_result_diagram_input():
 raw=RESULT_DIAGRAM_INPUT.read_bytes()
 assert sha256(raw).hexdigest().upper()==RESULT_DIAGRAM_INPUT_SHA,'result side-by-side design input drift'
 source=json.loads(raw.decode('utf-8-sig'))
 assert source['baselinePageSHA256']==RESULT_DIAGRAM_BASELINE_SHA
 inputs={Path(x['path']).name:x['sha256'].upper() for x in source['inputs']}
 assert inputs.get(FOUR_EVIDENCE.name)==FOUR_EVIDENCE_SHA and inputs.get(FOUR_DECISIONS.name)==FOUR_DECISIONS_SHA
 assert len(source['lanes'])==3 and len(source['responsibilityRows'])==4
 assert len(source['beforeTree'])>0 and len(source['afterTree'])>0
 return source

def load_cross_tool_result_input():
 raw=CROSS_TOOL_RESULT_INPUT.read_bytes()
 assert sha256(raw).hexdigest().upper()==CROSS_TOOL_RESULT_INPUT_SHA,'cross-tool result design input drift'
 source=json.loads(raw.decode('utf-8-sig'))
 assert source['baselinePageSHA256']==CROSS_TOOL_RESULT_BASELINE_SHA
 assert source['supersedes']['path'].endswith('cross-tool-result-declaration-design-input-v2-20261025.json')
 assert source['supersedes']['sha256'].upper()==CROSS_TOOL_RESULT_V2_SHA
 v2_raw=CROSS_TOOL_RESULT_V2.read_bytes()
 assert sha256(v2_raw).hexdigest().upper()==CROSS_TOOL_RESULT_V2_SHA,'superseded v2 evidence drift'
 v2=json.loads(v2_raw.decode('utf-8-sig'))
 assert v2['supersedes']['sha256'].upper()==CROSS_TOOL_RESULT_V1_SHA
 assert sha256(CROSS_TOOL_RESULT_V1.read_bytes()).hexdigest().upper()==CROSS_TOOL_RESULT_V1_SHA,'superseded v1 evidence drift'
 inventory=source['observedFactoryInventory']
 assert (inventory['explicitMethods'],inventory['dedicatedViews'],inventory['sharedCommunicationConsumers'],inventory['explicitNull'])==(17,5,4,8)
 assert len(source['ourEvidence'])==6 and len(source['vmEvidence'])==6 and len(source['specializedEditorEvidence'])==2
 assert len(source['recommendation']['acceptance'])==8 and len(source['unknowns'])==6
 assert 'None' in source['recommendation']['availability']
 expected_tools=['CircleDetect','LineDetect','PointPointMeasure','PointLineMeasure','PointCircleMeasure','LineLineMeasure','LineCircleMeasure','CircleCircleMeasure']
 decisions=source['eightToolDecisions']
 assert [x['tool'] for x in decisions]==expected_tools
 assert all(x['choice']=='StandardTable' and x['rowCardinality']=='One' and x['needsAnalysis'] is True for x in decisions)
 summary=source['eightToolSummary']
 assert (summary['total'],summary['StandardTable'],summary['None'],summary['otherPresentation'],summary['specializedAnalysisUserControl'],summary['undecided'])==(8,8,0,0,0,0)
 assert len(source['eightToolEngineering'])==8 and len(source['eightToolAcceptance'])==8
 for item in source['evidencePacks']+source['hostEvidence']:
  evidence_path=Path(item['path']);evidence_bytes=evidence_path.read_bytes()
  assert len(evidence_bytes)==item['bytes'] and sha256(evidence_bytes).hexdigest().upper()==item['sha256'].upper(),('v3 evidence drift',evidence_path)
 for item in decisions:
  assert all(item.get(k) for k in ('vmModule','vmColumns','vmRowCount','columns','publishedFields','matchingBasis','difference','declaration','dataSource','host','rowValidity','disposition','designClosure'))
 for group in ('ourEvidence','vmEvidence','specializedEditorEvidence'):
  for item in source[group]:
   evidence_path=Path(item['path']);evidence_bytes=evidence_path.read_bytes()
   assert sha256(evidence_bytes).hexdigest().upper()==item['sha256'].upper(),(group,evidence_path,'source drift')
 return source

def render_cross_tool_result_boundary(source):
 recommendation=source['recommendation'];inventory=source['observedFactoryInventory']
 cards=[
  ('通用声明与结果View',recommendation['common']),
  ('可扩展呈现器',recommendation['rendererExtension']),
  ('专用结果UI与编辑流程',recommendation['specialized']),
 ]
 cards_html=''.join('<div class="br-card"><b>'+E(title)+'</b><p>'+E(body)+'</p></div>' for title,body in cards)
 self_examples='；'.join(Path(item['path']).name for item in source['ourEvidence'])
 vm_examples='；'.join(Path(item['path']).name for item in source['vmEvidence'])
 editor_examples='；'.join(Path(item['path']).name for item in source['specializedEditorEvidence'])
 availability_cards=[
  ('None｜不提供分析入口','这是可选合法方式；本次冻结的8个工具均不选None，算法输出、模块结果和绑定仍按各自合同处理。'),
  ('标准结果投影','本次8项逐工具决定均为StandardTable + One；按各自声明和同一结果快照生成一条结果行。'),
  ('其他呈现','本次8项均不选其他呈现或专用分析UserControl；其他工具需求仍按实际业务另行决定。'),
 ]
 availability_html=''.join('<div class="br-card"><b>'+E(title)+'</b><p>'+E(body)+'</p></div>' for title,body in availability_cards)
 return ('<h6>先按实际需求选择是否提供结果分析</h6><div class="br-grid">'+availability_html+'</div>'
  '<p class="br-note"><b>适用规则：</b>'+E(recommendation['availability'])+'</p>'
  '<h6>选择分析后，再确定呈现方式：通用 / 可扩展 / 经评审的专用UI（建议，未实施）</h6>'
  '<div class="br-grid">'+cards_html+'</div>'
  '<p class="br-note"><b>双方代表样本：</b>我方证据含5个专用分析视图（Blob、ShapeMatch、Filter、Threshold、Morphology）和4个通信工具消费者共用的1个分析视图；对应VM证据样本含轮廓匹配集合、P2P单行标量、直方图datachart+共享呈现、形态学无datalist，以及通用结果宿主/可选扩展视图装配。模型创建与脚本编辑是独立编辑流程，不是只读结果UI专用性的证据。此清单只覆盖冻结输入列出的文件，不推广全部VM工具，也不证明专用结果例外完整清单。</p>'
  '<p><b>工厂盘点与本轮决定：</b>显式工厂方法'+str(inventory['explicitMethods'])+'个、其中专用分析视图'+str(inventory['dedicatedViews'])+'个、通信共享消费者'+str(inventory['sharedCommunicationConsumers'])+'个、显式null工厂'+str(inventory['explicitNull'])+'个；'+E(inventory['caveat'])+' 冻结输入已逐项核定下表8个空工厂：标准结果表'+str(source['eightToolSummary']['StandardTable'])+'项、None '+str(source['eightToolSummary']['None'])+'项、其他呈现 '+str(source['eightToolSummary']['otherPresentation'])+'项、专用分析控件 '+str(source['eightToolSummary']['specializedAnalysisUserControl'])+'项、未决定 '+str(source['eightToolSummary']['undecided'])+'项。该设计决定可阶段性闭口，实施与运行验收尚未完成。</p>'
  +render_eight_tool_decision_table(source)
  +'<details><summary>展开双方代表源码、SHA与未闭合边界</summary>'
  +'<p><b>我方分析视图证据：</b>'+E(self_examples)+'</p>'
  +'<p><b>VM代表证据：</b>'+E(vm_examples)+'</p>'
  +'<p><b>专用编辑流程证据：</b>'+E(editor_examples)+'</p>'
  +'<div class="br-scroll"><table><thead><tr><th>证据组</th><th>文件 / 行</th><th>SHA-256</th><th>观察 / 可信边界</th></tr></thead><tbody>'
  +''.join('<tr><td>'+E(group)+'</td><td><code>'+E(item['path'])+'</code><br>'+E(item.get('lines',''))+'</td><td><code>'+E(item['sha256'])+'</code></td><td>'+E(item['observation'])+'</td></tr>' for group in ('ourEvidence','vmEvidence','specializedEditorEvidence') for item in source[group])
  +'</tbody></table></div><ul>'+''.join('<li>'+E(x)+'</li>' for x in source['unknowns'])+'</ul></details>')

def render_eight_tool_decision_table(source):
 evidence_by_tool={}
 measure_pack=json.loads(Path(source['evidencePacks'][0]['path']).read_text(encoding='utf-8-sig'))
 detection_pack=json.loads(Path(source['evidencePacks'][1]['path']).read_text(encoding='utf-8-sig'))
 for item in measure_pack['items']:
  evidence_by_tool[item['tool']]=[x for x in item['sources'] if x['kind'] in ('vmDisplay','selfResults','selfTool')]
 for item in detection_pack['items']:
  tool=item['tool'];module=item['vm_module']
  keys=[tool+'Results',tool+'Tool',module+'Display',module]
  evidence_by_tool[tool]=[dict(kind=('vmDisplay' if 'Display' in k else 'selfResults' if k.endswith('Results') else 'selfTool' if k.endswith('Tool') else 'vmMain'),**detection_pack['sources'][k]) for k in keys]
 rows=[]
 for item in source['eightToolDecisions']:
  vm='<b>'+E(item['vmModule'])+'</b><p>VM列：'+E('、'.join(item['vmColumns']))+'</p><p>'+E(item['vmRowCount'])+'</p><p>'+E(item['matchingBasis'])+'</p>'
  ours='<b>正式输出字段：</b>'+E('、'.join(item['publishedFields']))+'<ul>'+''.join('<li><b>'+E(col['label'])+'</b>：'+E(col['source'])+'</li>' for col in item['columns'])+'</ul>'
  semantics='<p><b>需求：</b>'+E(item['reason'])+'</p><p><b>差异：</b>'+E(item['difference'])+'</p><p><b>摘要：</b>'+E(item['summary'])+'</p><p><b>其他呈现边界：</b>'+E(item['otherPresentation'])+'</p><p><b>失败/诊断边界：</b>'+E(item.get('diagnosticBoundary','由Status/Diagnostics表达；不旁路未声明CLR字段。'))+'</p>'
  contract='<p><b>声明：</b>'+E(item['declaration'])+'</p><p><b>数据源：</b>'+E(item['dataSource'])+'</p>'
  host='<p>'+E(item['host'])+'</p>'
  validity='<p>'+E(item['rowValidity'])+'</p>'
  engineering='<p><b>工程处置：</b>'+E(item['disposition'])+'</p><p><b>阶段决定：</b>'+E(item['designClosure'])+'</p>'
  if item.get('sceneAdaptation'): engineering+='<p><b>场景适配：</b>'+E(item['sceneAdaptation'])+'</p>'
  refs=[]
  for ref in evidence_by_tool[item['tool']]:
   file_path=Path(ref['path']);href=source_href(file_path)
   selected=ref.get('selectedLines',[])
   excerpt=''
   if selected:
    excerpt='<details><summary>关键源码行（'+str(len(selected))+'条）</summary><pre>'+E('\n'.join(str(x['line'])+': '+x['text'] for x in selected))+'</pre></details>'
   refs.append('<li><b>'+E(ref['kind'])+'</b>：<a href="'+E(href)+'">'+E(ref['path'])+'</a><br>行：'+E(ref.get('lines','; '.join(str(x['line']) for x in selected)))+'<br>SHA-256：<code>'+E(ref['sha256'])+'</code>'+excerpt+'</li>')
  evidence='<details><summary>VM Display 与我方 Results/Tool 逐项证据（展开查看文件、行号、SHA）</summary><ul>'+''.join(refs)+'</ul></details>'
  cells=[item['tool']+'｜StandardTable + One',vm,ours,semantics,contract,host,validity,engineering+evidence]
  rows.append('<tr>'+''.join('<td>'+cell+'</td>' for cell in cells)+'</tr>')
 pack_links=[]
 for pack in source['evidencePacks']:
  pack_path=Path(pack['path'])
  href=os.path.relpath(pack_path,PAGE.parent).replace('\\','/')
  pack_links.append('<a href="'+E(href)+'">'+E(pack_path.name)+'</a> · SHA-256 '+E(pack['sha256']))
 return ('<h6>8个显式null工厂：冻结后的逐工具决定与对照</h6>'
  '<p>'+E(source['eightToolSummary']['note'])+' VM及我方列、结果语义、正式声明、数据源、宿主责任、有效行和工程处置均按冻结输入逐项列出。</p>'
  '<div class="br-scroll"><table><thead><tr><th>工具与决定</th><th>VM对应、列与基数</th><th>我方字段与列来源</th><th>需求与语义差异</th><th>结果声明与数据源</th><th>宿主职责</th><th>有效行与失败状态</th><th>工程处置与阶段闭口</th></tr></thead><tbody>'+''.join(rows)+'</tbody></table></div>'
  '<p><b>冻结证据包：</b>'+'；'.join(pack_links)+'；输入SHA-256：'+CROSS_TOOL_RESULT_INPUT_SHA+'</p>')

def render_cross_tool_engineering_tree(source):
 return '\n'.join([
  '候选目录（均未创建/未实施）：',
  'tools/SunEyeVision.Tool.<Tool>/Runtime/Presentation/<Tool>ResultProjection.cs  [中立结果投影声明；业务字段与算法统计口径归工具]',
  'src/Plugin.Contracts/Results/Presentation/ResultProjectionDescriptor.cs  [复用现有ToolSchema/PortId/TypeRef/OutputReference]',
  'src/UI/Features/Results/Views/CurrentResultView.xaml(.cs)  [仅标准结果投影被工具显式选择时生成；None不显示分析入口]',
  'src/UI/Features/Results/ViewModels/CurrentResultViewModel.cs  [同一运行结果快照]',
  'src/UI/Features/Results/Selection/ResultSelectionCoordinator.cs  [RunId/NodeId/ElementId与场景作用域]',
  'src/UI/Features/Results/Renderers/  [按已声明呈现类型选择可复用呈现器；曲线/直方图/图像为例，其他类型按实际需求扩展]',
  'src/Plugin.Wpf/Themes/Presentation/ResultTableTemplates.xaml  [仅视觉模板；Plugin.Wpf当前未作为生产共享库]',
  'tools/SunEyeVision.Tool.<Tool>/Wpf/  [交互/状态/创作确需且通过验收的可选专业视图或编辑器；编辑流程不进Runtime/共享Plugin.Wpf]',
  '本轮8个显式null工厂已逐项选择StandardTable + One（None/其他呈现/专用分析控件/未决定均为0）；该选择来自逐工具业务输入，不是null自动转换成UI。',
  '迁移门：候选落点，不代表接口批准；无Schema显式不支持，不对任意CLR反射；WPF/HALCON类型不进Runtime合同。'
 ])

def render_cross_tool_migration_gate(source):
 recommendation=source['recommendation']
 return ('<details><summary>跨工具只读结果视图：适用范围、工程边界与迁移验收（本轮建议，未实施）</summary>'
  '<p><b>8项阶段决定：</b>标准结果表 '+str(source['eightToolSummary']['StandardTable'])+' / '+str(source['eightToolSummary']['total'])+'；None、其他呈现、专用分析UserControl、未决定均为0。逐工具设计决定已闭口；产品实施、运行与实机验收尚未完成。</p>'
  '<p><b>适用范围：</b>'+E(recommendation['disposition'])+'</p>'
  '<p><b>统计与身份：</b>'+E(recommendation['statistics'])+' '+E(recommendation['identity'])+'</p>'
  '<p><b>声明边界：</b>'+E(recommendation['boundedDeclaration'])+'</p>'
  '<p><b>候选工程职责：</b>'+E(recommendation['engineering'])+'</p>'
  '<p><b>VM对照边界：</b>'+E(recommendation['notVmClone'])+'</p>'
  '<p><b>证据来源：</b>技术总监对本冻结输入列出的源码/XML做静态核对并绑定文件SHA；不是VM负责人的独立第三方复核，也不是运行/实机验收。该样本不能推广到全部VM工具或证明专用结果界面完整清单。</p>'
  '<h6>八项共同工程落点</h6><ul>'+''.join('<li>'+E(x)+'</li>' for x in source['eightToolEngineering'])+'</ul>'
  '<h6>实施与运行验收门</h6><ul>'+''.join('<li>'+E(x)+'</li>' for x in source['eightToolAcceptance'])+'</ul>'
  '<p><b>阶段边界：</b>'+E(source['eightToolDecisionBoundary'])+'</p>'
  '<ul>'+''.join('<li>'+E(x)+'</li>' for x in recommendation['acceptance'])+'</ul></details>')
VMROOT=Path('D:/MyWork/REA逆向测试')
SOURCES={
 'V1':VMROOT/'docs/architecture-recovery/tool-rendering-entry-audit.resources/IMVSBlobFindModuAlgorithmTab.xml',
 'V2':VMROOT/'analysis/decompiled/VMControls.WPF/VMControls.WPF/ParamsConfigControlBase.xaml.cs',
 'V3':VMROOT/'analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/GroupLinkItem.cs',
 'V4':VMROOT/'analysis/decompiled/Apps.XmlUI/styles/paramusercontroldatatemplate.xaml',
 'V5':VMROOT/'analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/ROISelecter_Mul.cs',
 'V6':VMROOT/'analysis/decompiled/VMControls.WPF/VMControls.WPF.Base/ROISelecterHolder.cs',
 'V7':VMROOT/'analysis/decompiled/VMControls.WPF/VMControls.WPF/ParamsConfigWithRenderControlBase.xaml',
 'V8':VMROOT/'docs/plugins/BLOB-ANALYSIS-IDENTITY-SOURCE-INVENTORY.md',
 'S1':ROOT/'tools/SunEyeVision.BlobAnalysis/Views/BlobAnalysisToolDebugControl.xaml',
 'S2':ROOT/'tools/SunEyeVision.BlobAnalysis/Views/BlobAnalysisToolDebugControl.xaml.cs',
 'S3':ROOT/'tools/SunEyeVision.BlobAnalysis/BlobAnalysisTool.cs',
 'S4':ROOT/'tools/SunEyeVision.BlobAnalysis/BlobAnalysisResults.cs',
 'S5':ROOT/'tools/SunEyeVision.BlobAnalysis/BlobOutputContract.cs',
 'S6':ROOT/'tools/SunEyeVision.BlobAnalysis/BlobResult.cs',
 'S7':ROOT/'tools/SunEyeVision.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.csproj',
}

def table(headers,rows):
 return '<div style="overflow:auto"><table><thead><tr>'+''.join('<th>'+E(x)+'</th>' for x in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+E(x)+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table></div>'

def source_href(path):
 return os.path.relpath(path,PAGE.parent).replace('\\','/') if path.is_relative_to(ROOT) else path.as_uri()

def render():
 result_diagram=load_result_diagram_input()
 cross_tool_result=load_cross_tool_result_input()
 current_tree=E('\n'.join(result_diagram['beforeTree']))
 target_tree=E('\n'.join(result_diagram['afterTree']))
 rows=[
 ('页面与窗口壳','VM具体装配是 ParamsConfigControlBase → AlgorithmTabRoot；普通页经 ModuleParam.GetParamRoot/DoIniter/GetItemsValueUnit 后设 DataContext，VM F03/F04为部分参数窗口/扩展窗组合链：GetExternTabWin选TabWinBase派生类或ParamsTabControlBase回退，注入ROI/参数后由宿主Show并挂Close。DefaultDebugWindow / CustomDebugWindow是我方31类中的名称，不是VM类型；不证明同构或全局无源。','BlobAnalysisToolDebugControl / ToolDebugControlBase / DefaultDebugWindow / CustomDebugWindow','R03/X01：重做宿主窗口与算法角色/可选 UI 注册边界；保留职责，不声称接口等价。去向是宿主选择默认/专业窗口，验收覆盖无 UI 注册和实际加载路径；当前均未验。'),
 ('图像输入及继承来源','VM参数模型是 Blob XML 的 GroupLinkItem(ImageSourceGroup)；通用 GroupLinkItem 从 GroupsRoot 取组、Clone 并插入父集合。与我方 ImageSourceSelector / ParamBinding / ParamBindingBase 是参数来源及绑定职责的复合映射；借鉴字段组引用/展开，不照搬 ComboBox 外观作为身份合同。具体组子控件与来源过滤待核。','ImageSourceSelector / ParamBinding / ParamBindingBase','R02：重做来源字段合同，按稳定字段/作用域接线。去向是字段声明与绑定分离；验收覆盖稳定身份、继承来源过滤和位置修正迁移，VM无一对一位置修正字段映射，旧来源样本未知。'),
 ('数值/枚举/布尔字段与订阅','VM参数模型 Integer / Enumeration / Boolean；Integer_Sub DataTemplate:550–560 用 SliderArrange 绑定 CurValueA/Min/Max 并引用 ButtonSelecter_sub:255，订阅选择进入 SubTreeView:296–302；另有 Enumeration:379、Boolean:113、Integer:562、IntegerBettween:594、FloatBettween:821。对应我方 ConfigSetting / NumericUpDown / RangeSlider / RangeInputControl / ToggleSwitch 是多模板到多控件复合映射；借鉴字段类型/范围/订阅通道，不照搬布局或旧字符串身份。','ConfigSetting / NumericUpDown / RangeSlider / RangeInputControl / ToggleSwitch','R02+K01：字段类型、值、范围和订阅合同由字段层统一；视觉原语仅按需评估。去向是字段合同与局部视觉控件分层；验收逐字段核默认值、范围、订阅和拒绝条件。模板不证业务校验。'),
 ('布局/图标/标签','VM具体模板：paramusercontroldatatemplate.xaml 的 Boolean DataTemplate 使用 LeftRightControl（113起）；这是局部左右布局组合，不是 VM 图标控件。对应我方 LabeledControl 仅属复合布局映射；VMControls.WPF图标资源与XAML图像用法存在；未闭合到同职责可复用ToolIcon组件。','LabeledControl / ToolIcon','K01：LabeledControl 不作为整类保留依据，仅允许按需重建为通用布局职责；ToolIcon为资源级部分证据；CLR组件及消费者链待核，不能按全局无源退役。图标由宿主资源/命令承接仅为候选方向。验收核主题/DPI/键盘。'),
 ('检测区绘制/继承与管理','VM参数模型：Blob XML ROISelecter:99–110（不是 ROISelecter_Com）；视觉控件：DataTemplate ROISelecter:853–855 实例化 Apps.XmlUI.ParamTabControl.RoiSelectControl:854；通用 ROISelecterHolder.cs:15 与 ParamsConfigWithRenderControlBase.xaml:9 提供 holder/画布桥。与我方 RoiEditorManagerControl / RoiEditorControl / RoiSubscribePanel 是参数、视觉控件和桥接复合对应，不是一对一替换；可借鉴参数到控件组合，不能照搬跨节点/视口管理。','RoiEditorManagerControl / RoiEditorControl / RoiSubscribePanel','D02+R01：重做 Manager 跨节点/视口权限与清理责任；RoiEditorControl 编辑状态/手势由宿主协调；ShapeToolBar相关局部形状选择链已证（RoiSelectControl.xaml:23–79 ToggleButton→ValueChangedCommand/RoiType），但不等价完整工具栏；命令/手势差异仍待映射。验收参数绑定、视口隔离、取消/卸载清理。弹窗动作未证，XML未声明 ROISelecter_Com。'),
 ('工具栏/几何回填/画布桥','VM局部组合：RoiSelectControl.xaml的形状ToggleButton→ValueChangedCommand/RoiType；ROISelecter/DataTemplate→RoiSelectControl→ROISelecterHolder/RenderControlBase画布桥。DrawingParameterPanel还有ROIListStyle/BoxROIStack矩形字段编辑链。它们分别对应形状选择、矩形参数编辑和画布桥的局部职责，不证明完整工具栏、全部形状参数或运行手势等价。','ShapeToolBar / ToolBarControl / DrawingParameterPanel','R01+K03：交互scope/坐标/草稿回填由宿主协调；可复用的形状选择和矩形数值能力按局部职责迁移评估，不据部分组合链删除当前能力。其他形状参数、命中/取消/释放仍须验收。不能推出七类交互已运行验收。'),
 ('结果图形/文字样式','VM参数模型 ImageViewConfig XML:2561–2714、TextDisplayGroup XML:2767–2875；对应视觉组合在 XAML:2503–2556 使用 PopupButton、ColorPickerControlView:2527–2528，数值样式由 SliderArrangeFloat:803–812，文字字段模板 StringConfig:2601、ColorConfig:2608、FloatConfig:2618、ButtonSelecter:1545。对照我方 GraphicsDisplayEditorControl / TextDisplayEditorControl / 颜色部件是两组参数模型与多模板复合映射，不是一体控件。','GraphicsDisplayEditorControl / TextDisplayEditorControl / DispItemColorControl / DispTextColorControl / ColorSelector / ColorPicker','R04+K01：算法输出与图层/文字投影分离；去向：稳定结果合同供只读投影，局部样式按字段承接。BlobDataAnalysisControl同职责结果表与读者链待核；不据未闭合结论删除或保留。验收分开核显示状态、文字属性与数据存在，不推断点击路由。'),
 ('业务判定编辑','VM Blob XML 的 Tab_Run 参数声明（IMVSBlobFindModuAlgorithmTab.xml:124–1483）含筛选/输出配置，但本轮范围未闭合到独立判定编辑器；Tab_Run筛选/输出参数声明不等于本方独立编辑器。全VM消费者与模板链未穷尽，归待核而非确认无源。','JudgmentEditorControl','R02：判定字段归统一字段合同/参数模板是候选方向；旧类去留待消费者与源码链确认。具体筛选顺序和NG语义由算法合同另行明确。'),
 ('中央图像/只读结果渲染','VM参数模型是 Blob 的 ImageViewConfig 图层清单，由 ImageViewConfig 模板及通用渲染桥呈现；它对应本方 ImageControl / ResultRenderLayer 的部分显示职责，属于职责复合映射，不是同类完整 cache/owner 控件。借鉴具体已证渲染函数与样式，不照搬整个缓存或资源所有权；模板未证明眼睛/铅笔点击路径。','ImageControl / ResultRenderLayer','R04+K03：只承接已证的具体渲染函数/样式范围，不默认保留整套 cache 或 owner。去向：只读结果投影与宿主资源生命周期分离；验收覆盖显示更新、视口共享清理权与资源退休，运行时仍待测。'),
 ('图像预览与数据分析','VM主界面组合链已由源文件确认：ModuleResultView.xaml:268→LocalImageListView；ModuleResultView.xaml.cs:439注入ModuleDisplayDecorator；后者按流程范围解析当前图像源；LocalImageListView绑定自动切换/运行模式，ImageListPanel提供缩略图、加图/目录、删除/清空；ImageListPanelBase选择后更新来源并Run。主界面能力链与本方ImagePreviewControl部分相似，不是一对一；运行全部只是模式字段，切换不证明立即批量执行。BlobDataAnalysisControl结果表仍待核。','ImagePreviewControl（UI宿主） / BlobDataAnalysisControl（Blob页）','R04+K02：ImagePreviewControl为VM主界面组合链与UI宿主相关功能，视图/VM、目录/缩略图服务、命令协调职责分开比较；不并入公共Plugin.Wpf。BlobDataAnalysisControl的同职责结果表链待核；稳定Run reader仅作候选数据承接，不据此处置旧类。'),
 ]
 four_decision, four_by_name=load_four_decisions()
 matrix_updates={
  '布局/图标/标签':('VM工具图标链为模块目录/ToolItemInfo→ImageEnablePath→工具箱Image；VmModule标识→NormalLogo/StateLogo→节点Image模板。与本方ToolIcon的Geometry→OnRender是不同表现路径；不是同名CLR控件的一对一对应。','LabeledControl / ToolIcon','K01：ToolIcon按资源解析+标准Image/DataTemplate替换旧专用绘制类和硬编码目录；矢量素材可转为可部署资源复用。VM扩展窗口标题消费仍未知；详见四项唯一主记录。'),
  '工具栏/几何回填/画布桥':('VM RoiSelectControl的RoiType ToggleButton/ValueChangedCommand/选中态进入ROISelecter模板与局部绘图命令，是局部ROI类型选择组合，不是主工具栏，也未证VM通用ToolBarControl。','ShapeToolBar / ToolBarControl / DrawingParameterPanel','R01+K03：ToolBarControl改为编辑会话驱动的局部类型选择模板。选画笔只切临时绘图模式，不发布算法参数；完成有效几何编辑才进入参数草稿。VM命令体、测量同责链和运行语义仍未知。'),
  '结果图形/文字样式':('VM ImageViewConfig、TextDisplayGroup及相应模板对应图形/文字样式字段；本维度不再把Blob逐项结果表混入样式编辑器职责。','GraphicsDisplayEditorControl / TextDisplayEditorControl / DispItemColorControl / DispTextColorControl / ColorSelector / ColorPicker','R04+K01：稳定结果合同供只读投影，样式按字段承接；Blob当前结果表由宿主通用投影承接，见BlobDataAnalysisControl主记录。'),
  '业务判定编辑':('VM ResultShow/Tab_Check声明OKWhenNumIsZero、计数/面积/总面积开关与范围，通过Boolean/IntegerBettween/FloatBettween等通用字段模板组合；不是Tab_Run特征筛选，也未证同名独立编辑器。','JudgmentEditorControl','R02：删除旧独立判定编辑器组合类，以统一字段组/条件模板承接；保留业务规则与用户配置。VM native阈值到OK/NG状态的控制流仍未知。'),
  '图像预览与数据分析':('VM直接对照对象仍是图像区下方CurResultTabItem的m_ResultDataGrid，绑定ModuleCurrentResultList；BlobDisplay.xml声明列/Num，ModuleDisplayDecorator组装通用DataGrid行列。跨工具样本另见轮廓匹配集合、P2P单行标量、直方图datachart+共享呈现、形态学无datalist；后者不证明无结果或应有专用UI。通用宿主/扩展/专用界面的结论限于冻结样本，不代表全VM。','ImagePreviewControl（UI宿主） / BlobDataAnalysisControl（Blob页）','R04+K02：Blob既有推荐仍由宿主承接标准只读结果投影；跨工具按每个消费者显式选择None / 标准结果投影 / 其他呈现，不能因Schema/输出存在自动造表。6个现有分析视图需求由共享声明能力候选承接，逐个确认并通过等价验收后再退出旧视图。冻结的8项空工厂均已逐项决定为StandardTable + One（None/其他呈现/专用分析UI/未决定均为0），设计阶段闭口，实施与实机验收未做；模型/脚本编辑另按编辑流程验收。VM统计口径、截图同版性、行序/稳定身份及运行高亮仍未知。')
 }
 rows=[(row[0],*matrix_updates.get(row[0],row[1:])) for row in rows]
 screenshot_evidence = [
  '无窗口壳一对一截图；S06工程树只证目录结构，不能证明运行注册。',
  'SS01 9a6ff874：图像源、掩膜、ROI类型/模式、位置修正；S01：输入来源同职责，位置修正无VM一对一映射。',
  'SS02 92a0eac5、SS03 41324add、SS04 c084fa69：运行参数、几何开关、排序/连通/内接矩形。模板组1 XML 631-1261 / XAML Boolean 113-119、IntegerBettween 576-600、FloatBettween 821-844；组2 XML 1274-1414、1415-1437 / XAML Enumeration 370-385、Integer 550-568。SS03不含排序。',
  '无Blob专属截图/CLR一对一映射；通用布局模板只作结构证据，不据此判定视觉组件可直接复用。',
  'SS01 + S02 ROI画面；IMVSBlobFindModuAlgorithmTab.xml 99-110、128-295；DataTemplate ROISelecter 853-855（ROISelecter_Com 856-858非此XML声明）。基础数值/枚举/ROI模板组，弹窗动作未证。',
  'S02展示本方ROI编辑职责；VM ROISelecterHolder/RenderControlBase是通用静态路径，不构成截图动作或完整交互的逐项映射。',
  'SS06 ae13e8ee → ImageViewConfig：XML 2561-2714 / Display.xml 30-141；XAML 2503-2556。SS07 073de6fe → TextDisplayGroup XML 2767-2875；StringConfig 2557-2607、ColorConfig 2608-2614、FloatConfig 2615-2618、ButtonSelecter 1545+。图层清单与文本样式分开。',
  'SS02/SS03/SS04/SS05涉及参数或输出选择；这些画面不证明核心算法谓词、筛选顺序或NG语义。',
  'SS06只显示结果图层及眼睛/铅笔图标；ImageViewConfig静态XAML 2503-2556核对IsDisplayA/PanVisibleA绑定，不证明点击/运行效果。',
  '没有VM Blob数据分析一对一画面；图像预览/分析行为与表格联动保持未知。'
 ]
 rows = [row + (evidence,) for row, evidence in zip(rows, screenshot_evidence)]
 controls=json.loads((ROOT/'artifacts/analysis/blob-control-consumer-closure-20261024.json').read_text(encoding='utf-8'))['controls']
 names={x['name'] for x in controls}
 mapped={name for row in rows for name in names if name in re.sub(r'（[^）]*）','',row[2]).split(' / ')}
 assert mapped==names,{'missing':list(names-mapped),'extra':list(mapped-names)}
 out=['''<!-- BLOB-CONTROL-COMPARISON:BEGIN -->
<h4 id="br-controls-vm-compare">VM / 我们 / 目标：控件能力与实际调用对照</h4>
<!-- O06-CAPABILITY-MATRIX:BEGIN -->
<div id="o06-capability-matrix"><span id="br-blob-interface-deepening"></span><span id="o06-screenshot-engineering-matrix"></span><h5>控件能力映射：VM证据、本方实现与处置</h5>
<div class="br-note"><b>纠正此前举证方式：</b>31项是我们静态可达的CLR控件/基类/宿主清单，不是VM对照矩阵。下表按10个消费者能力维度将全部31项与VM已证路径、未知及目标职责并排；XML参数节点、GroupLink和视觉CLR控件分别标识，不能混计数量。当前未完成VM全部模板实例化的运行取证，不宣称VM也有31个对应控件。</div>
<div class="br-grid"><div class="br-card"><b>VM已证组装链</b><p>Blob AlgorithmTab声明 → GroupLinkItem展开公共组 → AlgorithmTabRoot参数节点 → 通用DataTemplate → 参数/ROI控件 → Render桥</p></div><div class="br-card"><b>我们当前链</b><p>Blob手写XAML → 公共编辑控件/Manager → 页面连接主ImageControl → 参数与结果</p></div><div class="br-card"><b>推荐职责</b><p>工具业务/字段声明 → 宿主注入编辑与绘图能力 → 公共控件作用于私有草稿 → 正式发布/运行 → 只读结果消费</p></div></div>''']
 out.append(render_cross_tool_result_boundary(cross_tool_result))
 vmmap=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/vm-screenshot-control-map.json'
 desmap=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/implementation/designer-engine-screenshot-map.json'
 hard=ROOT/'artifacts/analysis/document-owner-o06-20261024/vm-screenshot-hard-gate-review.json'
 links=' / '.join('<a href="'+E(source_href(path))+'">'+label+'</a> · SHA256 '+sha256(path.read_bytes()).hexdigest().upper() for path,label in [(vmmap,'VM 7组截图映射'),(desmap,'本方设计器/工程树6行映射'),(hard,'5组模板硬门核对')])
 out.append('<p class="br-note">本表按职责列VM证据与本方31类覆盖；唯一去留推荐见 <a href="#o06-shared-control-disposition">原10项处置映射</a>及 <a href="#o06-current-control-mapping">31类逐控件VM源对照与去向</a>。VM源码岗位意见只作证据，不代替架构处置。静态职责耦合可支持重做建议；运行时未知只限定未验证行为，不自动否定有静态依据的处置。截图与模板按职责关联，无一对一处明确说明。冻结来源：'+links+'。</p>')
 out.append(table(['能力维度','VM静态类型、模板或截图证据/未知','我们实际可达类型（31类覆盖）与职责','对应原10项处置','截图/模板证据及边界'],rows))
 out.append('</div><!-- O06-CAPABILITY-MATRIX:END -->')
 out.append('<h4 id="br-algorithm-before-after">算法逻辑到底怎么调整：当前代码与拟议改变分开</h4>')
 out.append(table(['阶段','VM证据边界','我们当前实际代码','本轮建议 / 已实施辨别'],[
 ('输入/ROI/预处理','输入/ROI/Mask与样例调用已证，原ProcessAlg内部完整次序未知。','Run先GetHalconImage（在try外）；图像转灰，HalconRegionPipeline构建作用域，ReduceDomain和可选预处理。S3。','必须调整失败清理边界与输入所有权；处理顺序按实际业务/有效域验证，不能仅按旧注释或VM样例重排。未改产品。'),
 ('阈值/形态学/连通','SDK有单/双/自动/软阈值及4/8邻域声明，核心谓词未知。','实际Threshold(min,max)，BinaryInv在有效域内反选，可选形态学后再限制有效域，Connection。','保持HALCON方案，明确支持模式/端点/邻域/孔洞和边界；本轮不补全部VM软阈值，不靠全局set_system改变别的工具。'),
 ('筛选/特征/判断','VM有筛选/排序配置与BlobInfo结果接口，核心选择次序未知。','SelectShape原生area/circularity/rectangularity组合筛选；对候选批提面积/外接矩形/圆度等；JudgmentPassed标NG但不直接删除。','复用先减少候选的机制；区分统计、输出、显示和判定需求，不宣称每项都已按需计算。页首旧注释仍写“判断通过才保留”，与实际循环不同，以函数体为准并列后续清理。'),
 ('排序/轮廓/统计','VM输出开关和多段轮廓已证，排序并列规则未知。','最终集合映射区域，已有按需轮廓与批量显示路径；灰度统计在当前代码里仍执行。','明确最终集合、排序、标签与元素地址关系；按真实消费者需求规划特征/统计，不承诺关显示就省全部算法成本；已实现机制复用验收。'),
 ]))
 out.append('<h4 id="br-output-before-after">输出调整：语义、数据合同和渲染投影</h4>')
 out.append(table(['输出方面','VM','我们当前 / 推荐调整'],[
 ('Blob图像 vs原始灰度','SDK分别有BlobImage（目标连通域绘制）与BinaryImage。','当前OutputBlobImage控制GrayImage原灰度克隆，不等同VM BlobImage。必须明确本地字段含义与命名/兼容迁移；不把改名当已生成等价图像，也不强制新增VM每种输出。'),
 ('记录与平行数组','代码生成按同一BlobInfo[index]取中心/矩形等。','BlobOutputContract已有Sequence<Record>、稳定FieldId；平行数组由FilteredBlobs派生。复用该成果，继续保证同集合和同运行，不增第二权威。'),
 ('筛选与业务NG','VM核心NG处理次序未知，不能据界面推断。','当前NG仍在FilteredBlobs，OK/NG区域分开，Label与ElementId含义不同。保留可解释合同，明确排序后显示序号与运行内身份；不声称跨帧追踪。'),
 ('轮廓/区域与空间类型','SDK支持SequentialContours；数量/公式需独验。','当前BlobResult.Contour单Point[]且使用WPF几何；IVisionRegion也未表达完整集合/孔洞。需按业务明确多段与孔洞表示并接既有中立几何合同；替换会丢信息的消费路径，不要求所有输出转成整图mask。'),
 ('生命周期/失败','Clear后不可续读、GetResult失败返回NULL为SDK合同。','复用ResultResourceOwner与reader；明确未请求输出、空结果、算法失败及资源退休状态，旧树/旧图/下游不能各保一份成功真值。'),
 ]))
 out.append('<h4 id="br-tool-project-compare">工程依赖图：当前事实与候选边界（仅表示项目依赖）</h4>')
 cross_tree=E(render_cross_tool_engineering_tree(cross_tool_result))
 out.append('<div class="br-grid"><div class="br-card"><b>现有文件组织（仓库事实）</b><pre style="white-space:pre-wrap;overflow-wrap:anywhere">'+current_tree+'</pre><p>当前项目边界：工具项目含算法与WPF视图工厂；宿主 MainWindowViewModel 和 NodeResultManager 接入表格/高亮。Plugin.Wpf 当前仅有PluginFoundation样例接入，未入主slnx/生产依赖。</p></div><div class="br-card"><b>候选文件组织（未创建 / 未实施）</b><pre style="white-space:pre-wrap;overflow-wrap:anywhere">'+target_tree+'</pre><h5>跨工具结果声明候选边界（均未创建/未实施）</h5><pre style="white-space:pre-wrap;overflow-wrap:anywhere">'+cross_tree+'</pre><p>以上路径均为候选职责落点，不生成指向不存在文件的链接。Runtime/Wpf分离还须处理项目/资源/主题/发布边界；这里只表达结果表职责，不扩成新的迁移批次。</p></div></div>')
 out.append(table(['结构层次','VM原Blob实际结构','我们当前结构','架构推荐 / 本轮边界'],[
 ('插件/算法入口','native IMVSBlobFindModu.dll、托管Cs入口及Algorithm/AlgorithmTab/Display等声明；无Blob专属Control DLL。V8。','单一net9.0-windows/UseWPF工具工程，入口同时实现IToolPlugin与IToolEditorFactory，直接new两种Views。S3/S7。','推荐本Blob物理拆分Runtime与可选WPF视图，待用户批准；复用既有Contracts/Runtime/Plugin.Wpf与发现机制，不因此机械拆分全部工具。'),
 ('业务算法实现','原模块→MVDBlobFindCpp→MVD_Algorithm依赖已证，dispatcher/核心体仍有缺口。','BlobAnalysisTool同时含HALCON处理、业务判定、显示项构建与WPF几何；专用HalAdapter已存在。','业务规则归工具，原生算法及转换集中在实现/适配边界；显示投影可用中立描述，由宿主/渲染消费者执行。未来OpenCV换实现，不重建公共UI。'),
 ('参数/控件装配','AlgorithmTab声明公共GroupLink，解析器展开，通用模板映射；有可选专属Control加载通道。V1–V4。','手写Blob XAML引用ConfigSetting/ParamBinding/ROIManager等，页面负责视口及执行接线。S1/S2。','采用能力声明+受限上下文+可选专业View。重做越界接线，视觉原语可独立复用；不全量复制VM XML/触发器系统。'),
 ('结果/分析View','已证Display datalist/Colume/Num→ModuleResultView动态列→ModuleDisplayDecorator组行→行选中高亮调用；逆序/分页下元素映射稳定性及同版实机身份另验。','Results、BlobOutputContract与Views/BlobDataAnalysisControl同工程，运行实例工厂依赖FrameworkElement。','结果合同与只读消费者分离；按稳定字段/运行身份接入宿主通用投影，不由表格掌握结果与native资源。目标是无UI运行不必引用WPF视图，不以搬文件夹代替验收。'),
 ('依赖与发布','原模块/SDK/公共控件分布式部署，当前静态匹配不等于实际加载路径已证。','工具HALCON HintPath仍指向dev/Debug/halcondotnetxl.dll；Private=false，依赖由宿主发布治理。S7。','本轮纳入构建依赖来源审查，采用权威SDK/发布输入，避免Debug目录成隐式前提；只核所需改动，发布依赖沿在途既有规则，不另造加载器。'),
 ]))
 out.append('<p><b>整体结论：</b>先确定消费者/业务合同和所有权，再决定复用或替换。控件、算法和工程不能分别做三套局部方案；本节与 <a href="#br-blob-unified-scope">B1–B7完整闭环范围</a> 是同一方案。以上算法/输出/工程调整均为本轮待审建议，现有代码与有限验收事实另列，未进行产品修改。</p><details><summary>VM与本方直接证据索引 / SHA快照</summary><ul>')
 for key,path in SOURCES.items():
  url=source_href(path)
  out.append('<li>'+key+' · <a href="'+E(url)+'">'+E(str(path))+'</a></li>')
 out.append('</ul></details><!-- BLOB-CONTROL-COMPARISON:END -->')
 from runpy import run_path
 merger=run_path(str(ROOT/'artifacts/analysis/vm-control-blob-document-merge-20261024/merge_controls_h4.py'))['merge_controls_h4']
 return merger('\n'.join(out).encode('utf-8')).decode('utf-8')

def inventory_inner(body):
 start=body.index('<details data-blob-self-inventory>')
 open_end=body.index('>',start)+1
 depth=0; close_start=None
 for token in re.finditer(r'<details\b[^>]*>|</details\s*>',body[start:],re.I):
  if token.group().lower().startswith('</'):
   depth-=1
   if depth==0:
    close_start=start+token.start();break
  else: depth+=1
 assert close_start is not None,'unbalanced 31-type inventory details'
 inner=body[open_end:close_start]
 summary=re.search(r'<summary\b[^>]*>.*?</summary\s*>',inner,re.I|re.S)
 assert summary,'inventory summary missing'
 return inner[summary.end():]

VM_CONTROL_MAP={
 'BlobAnalysisToolDebugControl':('ParamsConfigControlBase.xaml.cs:37 + AlgorithmTabRoot.cs:27; XML普通参数页路径是 ParamsConfigControlBase→AlgorithmTabRoot。是宿主组合对应，不是同名控件。','移除其视口清理/旁路执行职责；参数页能力由宿主+字段声明承接。'),
 'BlobDataAnalysisControl':('VM负责人本轮复核：当前已核范围未闭合到同职责结果表；通用 RenderControlBase 不是数据表。属于待核，不能据此声称VM全量无此能力。','结果应由同一Run/稳定元素地址的只读消费者读取是目标方向；BlobDataAnalysisControl旧类去留待消费者→源码链补齐后决定。'),
 'ImageControl':('ParamsConfigWithRenderControlBase.xaml:9 明确组合 RenderControlBase 并绑定 ModuleSource/ImageData/ROI 命令；是通用图像视口组合对应，不是完整 ImageControl 一对一。','按职责拆分视口/结果投影/资源寿命；仅复用有证渲染函数。'),
 'ImagePreviewControl':('VM主界面组合链已由VM负责人补证：VMControls.WPF.ModuleResultView.xaml:268装配LocalImageListView；ModuleResultView.xaml.cs:439创建ModuleDisplayDecorator并注入view；ModuleDisplayDecorator.cs:376解析当前图像/上游模块参数根与流程范围；LocalImageListView.xaml:45绑定自动切换/运行模式；ImageListPanel.xaml:190虚拟缩略图/删除；ImageListPanelBase.cs:1314选图、更新来源参数、触发流程。对应主界面能力链，不等同本方ImagePreviewControl的行为实现；VM原工程目录及运行交互未核。','归入“组合链”。本方ImagePreviewControl仍是UI宿主图像结果区域；VM链作为职责对照，撤回无源退役和并入主视口的原建议。添加/目录/删除/缩略图/自动切换/运行模式可由静态源码支持；“运行全部”切换即批量执行未证。'),
 'DefaultDebugWindow':('VM负责人补证部分参数窗口组合链：VMControls.WPF.ParamsTabControlBase.xaml声明ParamsTabControlBase→TabWinBase承载→第40行装入ParamsConfigControlBase→Apps.XmlUI.ParamTabUserControl；code-behind处理订阅子窗口。VM反编译树未找到同名类型，但不等于全局无源；具体宿主分支待核。','归入“组合链/待核”。我方类型路径是src/Plugin.SDK/UI/Windows/DefaultDebugWindow.xaml.cs；VM链只证明部分参数窗口装配，不证明等价。类去留待消费者与完整窗口链核实。'),
 'CustomDebugWindow':('VM负责人补证部分参数窗口组合链：VMControls.WPF.ParamsTabControlBase.xaml→TabWinBase→ParamsConfigControlBase→Apps.XmlUI.ParamTabUserControl；该链不证明本方专业窗口等价。反编译树无同名类型不等于全局无源；程序集/宿主分支待核。','归入“组合链/待核”。我方类型路径是src/Plugin.SDK/UI/Windows/CustomDebugWindow.cs；保留参数视图与窗口壳职责拆分方向，旧类去留待消费者与完整窗口链核实。'),
 'ParamBinding':('仅订阅选择子职责有据组合：Integer_Sub DataTemplate 的 SliderArrange:550–560 引用 ButtonSelecter_sub:255，并含 SubTreeView:296–302；StringConfig_sub 亦含 SubTreeView:2589–2595。不是稳定字符串来源绑定协议。','退役旧字符串身份/来源合同；必要订阅选择UI承接到字段订阅模板，稳定来源ID由字段合同处理。'),
 'GraphicsDisplayEditorControl':('Blob XML ImageViewConfig:2561–2714→DataTemplate ImageViewConfig XAML:2503–2556；其具体组合含 PopupButton、ColorPickerControlView:2527–2528、TwoWay IsDisplayA，后接 ObjectBase.cs:234–249 setter；仅图层/样式字段组合，不是整控件一对一。','只读图层投影承接显示状态；退役旧控件对数据/缓存的拥有权。'),
 'ToggleSwitch':('DataTemplate Boolean:113；对应布尔字段的局部视觉编辑，不证明 VM 使用同一 ToggleSwitch CLR 类型。','可按需复用视觉原语；字段订阅和值发布走统一字段合同。'),
 'JudgmentEditorControl':('VM负责人本轮复核：当前已核范围未闭合到独立判定编辑器；Tab_Run 参数声明不等于该控件，完整消费者与模板链仍待核。','判定语义与通用字段承接是目标方向；旧类去留待消费者→源码链确认，不基于当前未闭合直接删除。'),
 'ImageSourceSelector':('IMVSBlobFindModuAlgorithmTab.xml:84–97 的 ImageSourceGroup/Mask/ROI GroupLinkItem；具体参数模板 EnumerationG/RadioSelecter 系列按 XML 类型分派。是源字段组合，不是 ImageSourceSelector 类一对一。','重做稳定来源合同；不保留 ComboBox 外观作为身份/继承语义。'),
 'ConfigSetting':('DataTemplate Integer:562、Enumeration:379、Boolean:113、StringConfig:2601、ColorConfig:2608、FloatConfig:2618；VM按字段类型分模板，不存在单个 ConfigSetting 对应控件。','拆为字段合同+所需模板职责；旧大一统设置类不整体保留。'),
 'ToolDebugControlBase':('ParamsConfigControlBase.xaml.cs:37→AlgorithmTabRoot.cs:27 是参数树装配路径；不是执行基类的一对一替代。','移除旧基类的执行/视口/保存耦合；按宿主能力分别承接。'),
 'RangeInputControl':('DataTemplate IntegerBettween:594、FloatBettween:821；模板类型和值范围组合对应，非同一 CLR 控件。','范围语义进字段合同；按需选择数字输入视觉控件。'),
 'TextDisplayEditorControl':('Blob Group.xml TextDisplayGroup:2767–2875；StringConfig:2601、ColorConfig:2608、FloatConfig:2618、ButtonSelecter:1545；相邻 ImageViewConfig XAML:2503–2556 具体组合 PopupButton + ColorPickerControlView:2527–2528 + SliderArrangeFloat:803–812 仅属局部样式编辑链，不是一个 VM 结果文字编辑控件。','结果文字属性由稳定字段只读投影承接；不把样式编辑器当结果真源。'),
 'LabeledControl':('paramusercontroldatatemplate.xaml:3968–3983 的 LeftRightControl 是左右内容布局组合；仅映射局部布局职责，不含我方通用标签控件全部行为。','不整体保留旧类；需要左右布局时由通用布局原语承接。'),
 'RoiEditorManagerControl':('Blob XML ROISelecter:99–110→DataTemplate ROISelecter:853–855 内 Apps.XmlUI.ParamTabControl.RoiSelectControl:854→ROISelecterHolder.cs:15/RenderControlBase XAML:9；参数、视觉控件、画布桥复合映射，不是一对一 Manager。','重做宿主协调与权限；移除跨节点/共享视口清理权。'),
 'ResultRenderLayer':('ImageViewConfig XML:2561–2714→DataTemplate:2544→ObjectBase.cs:234–249 setter；RenderControlBase 画布桥在 ParamsConfigWithRenderControlBase.xaml:9。仅局部显示投影组合。','只复用具体渲染函数；旧 layer 不拥有全局缓存/reader。'),
 'ToolIcon':('VM负责人确认VMControls.WPF图标资源及XAML图像用法可见；当前证据未闭合到与我方ToolIcon同职责的CLR组件。资源存在不等于控件同构，资源宿主/调用者映射待核。','归入“待核”。仅有图标资源/XAML图像用法线索，不足以判为模板或同责组件；撤回无源退役结论，消费XAML与资源链核对后再定旧类去留。'),
 'ParamBindingBase':('Integer_Sub:550–560→ButtonSelecter_sub:255→SubTreeView:296–302 只证明数值订阅选择子视图；当前冻结材料未定位来源树过滤/绑定错误基类，VM全量消费者检索待核。','撤回仅凭本轮未定位到源而退役旧基类的结论；必要订阅选择由具体模板承接、来源校验由稳定字段/作用域合同承接的方向保留，旧类去留待核。'),
 'DispItemColorControl':('ImageViewConfig XML:2561–2714→ColorPickerControlView XAML:2527–2528 的局部颜色属性绑定 + ColorConfig:2608 通用模板；是显示字段组合证据，无同名 VM 控件。','按字段模板承接颜色值；不保留独立旧控件/数据所有权。'),
 'NumericUpDown':('DataTemplate Integer:562 是整数参数模板；仅证明字段类型到模板的映射，不证明同类步进器 CLR。','按需保留局部数字输入视觉能力；验默认值/范围/键盘/提交。'),
 'RangeSlider':('Integer_Sub:550 绑定 SliderArrange/CurValueA/Min/Max；是模板内 Slider 与范围字段组合，对应局部交互而非整类。','仅在字段合同需要范围拖动时复用；范围校验和提交仍归字段层。'),
 'DispTextColorControl':('TextDisplayGroup:2786–2809 的 OKColor/NGColor + ColorConfig:2608；字段/模板复合对应，不是同名控件。','颜色属性由结果字段映射承接；不保留旧编辑控件作为数据源。'),
 'RoiEditorControl':('ROISelecter XML:99–110→DataTemplate ROISelecter:853–855 内 Apps.XmlUI.ParamTabControl.RoiSelectControl:854→ROISelecterHolder.cs:15/RenderControlBase XAML:9；参数、具体视觉控件、画布桥组合，不是完整编辑器一对一。','重做必要手势/草稿边界并由宿主协调；旧编辑状态/订阅模型退役。'),
 'ColorSelector':('ColorConfig DataTemplate:2608 是颜色字段视觉模板；未证明与本方 ColorSelector CLR 一对一。','仅按需采用颜色字段编辑原语；不承接结果真值。'),
 'RoiSubscribePanel':('XML InheritWayGroup2:128–295 + DataTemplate Enumeration:379/ButtonSelecter:1545；继承来源参数链有据，不等价整面板。','继承关系按稳定来源字段承接；退役旧面板越权订阅/路由。'),
 'ShapeToolBar':('VM ROISelecter XML→DataTemplate→Apps.XmlUI.ParamTabControl.RoiSelectControl→ROISelecterHolder/RenderControlBase形成局部绘制组合链；不等价同名形状工具栏。工具命令/手势对应待核。','归入“组合链”。宿主命令/交互能力是候选承接方向；不按“无工具栏源”删除，待命令消费者和差异映射后定旧类去留。'),
 'DrawingParameterPanel':('VM ROISelecter XML:99–110→DataTemplate:853–855→RoiSelectControl:854提供字段入口与局部绘制组合；未证明与我方几何参数面板同构。数值字段与手势边界待映射。','归入“组合链”。ROI字段合同/通用数值模板是候选承接方向；不按“无面板源”删除，待消费者与职责差异映射后定旧类去留。'),
 'ColorPicker':('ColorConfig DataTemplate:2608 是 VM 颜色字段模板；只映射局部选择职责，不等价本方组合控件。','不整体保留旧组合类；所需颜色字段编辑由模板化字段能力承接。'),
 'ToolBarControl':('VM负责人确认主界面工具栏/相关资源可见；RenderControlBase仍只是画布，当前证据没有闭合到与我方ToolBarControl相同的命令/呈现职责。具体类型、资源与消费者链待核。','归入“待核”。撤回无源退役结论；必要命令由宿主系统承接仅为候选，完成全量资源/消费者映射后再决定旧类去留。'),
}

DECISION_OBJECTS={
 'D01':['BlobAnalysisToolDebugControl','ImageControl','ResultRenderLayer'],
 'D02':['RoiEditorManagerControl'],
 'R01':['RoiEditorControl','RoiSubscribePanel','ShapeToolBar','DrawingParameterPanel'],
 'R02':['ConfigSetting','ParamBindingBase','ParamBinding','ImageSourceSelector','RangeInputControl','NumericUpDown','RangeSlider','ToggleSwitch','JudgmentEditorControl'],
 'R03':['ToolDebugControlBase','DefaultDebugWindow','CustomDebugWindow','BlobAnalysisToolDebugControl'],
 'R04':['BlobDataAnalysisControl','GraphicsDisplayEditorControl','TextDisplayEditorControl','ImageControl','ResultRenderLayer'],
 'K01':['LabeledControl','ToolIcon','ToggleSwitch','DispItemColorControl','DispTextColorControl','ColorSelector','ColorPicker'],
 'K02':['ImageControl','ResultRenderLayer','BlobDataAnalysisControl','ImagePreviewControl'],
 'K03':['ImageControl','ResultRenderLayer','GraphicsDisplayEditorControl','TextDisplayEditorControl','DispItemColorControl','DispTextColorControl','ShapeToolBar','ToolBarControl','BlobAnalysisToolDebugControl'],
 'X01':['ParamBindingBase','ParamBinding','ToolDebugControlBase','DefaultDebugWindow','CustomDebugWindow','ImageControl','ResultRenderLayer','BlobAnalysisToolDebugControl'],
}

VM_CANDIDATE_SUMMARY={
 'ImagePreviewControl':'VM主结果区图像源组合链已定位；本方控件同样涉及图像列表、缩略图、自动切换和运行模式，但宿主与输入对象不同。静态证据不证明运行顺序、画面或响应性能等价。',
 'ShapeToolBar':'VM有ROI形状按钮及ValueChangedCommand/RoiType局部组合；未证明完整工具栏、全部命令路由或手势行为。',
 'DrawingParameterPanel':'VM有ROI模板选择和BoxROIStack矩形参数编辑链；仅与矩形数值编辑职责部分对应，其他形状字段与绘制手势未闭合。',
 'DefaultDebugWindow':'VM有普通参数窗口装配/宿主回退链；我方类名不是VM类型名，职责仅部分对应。查证限于ProcedureConfigControl F03/F04及所选参数窗源码，未证扩展程序集运行时选择。',
 'CustomDebugWindow':'VM有模块专用Control.dll扩展窗选择与通用打开/关闭链；我方类名不是VM类型名，专业窗职责未证明等价。模块目录未见DLL不能排除已加载程序集。',
 'ToolIcon':'仅见VM图标资源与XAML图像用法；CLR控件、模板、资源键和消费者映射未闭合，归待核。',
 'BlobDataAnalysisControl':'已选ModuleResultView含通用结果视图；本轮未闭合到同职责Blob分析表及其结果消费者，归待核。检索未覆盖全部VM工具、扩展程序集或运行时注册。',
 'JudgmentEditorControl':'Blob XML存在筛选/输出参数声明；本轮未找到独立判定编辑器类型/模板或完整的声明到控件再到宿主链，归待核。检索限于Blob声明及选定核心界面/模板。',
 'ToolBarControl':'VM主界面工具栏及资源可见；未闭合到我方控件的具体CLR类型、命令路由和同职责关系，归待核。',
}

def render_current_control_table():
 four_decision,four_by_name=load_four_decisions()
 result_diagram=load_result_diagram_input()
 cross_tool_result=load_cross_tool_result_input()
 self_raw=FOUR_SELF.read_bytes()
 assert sha256(self_raw).hexdigest().upper()==FOUR_SELF_SHA,'frozen self-duty evidence drift'
 self_by_name={row['name']:row for row in json.loads(self_raw.decode('utf-8-sig'))['rows']}
 data=json.loads((ROOT/'artifacts/analysis/blob-control-consumer-closure-20261024.json').read_text(encoding='utf-8'))['controls']
 assert len(data)==31 and {row['name'] for row in data}==set(VM_CONTROL_MAP)
 org_path=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/implementation/control-project-organization-delta-v1.json'
 org_raw=org_path.read_bytes();org=json.loads(org_raw.decode('utf-8-sig'))
 digest='5657F2553E63CD579C867DDBA117EBA510A77E40A71204F065BC7054CE24753C'
 assert sha256(org_raw).hexdigest().upper()==digest and len(org['rows'])==31
 org_by_name={r['name']:r for r in org['rows']}
 assert set(org_by_name)=={r['name'] for r in data}
 vm_pack_path=Path(r'D:\MyWork\REA逆向测试\analysis\selfdev-support\VM-CONTROL-BLOB-ASSEMBLY-20261024\root\interface-deepening\o06-no-source-candidate-review-20261025.json')
 vm_pack_bytes=vm_pack_path.read_bytes();vm_pack=json.loads(vm_pack_bytes.decode('utf-8-sig'))
 vm_pack_sha='F17B9621A748AE51CCEEF71E5938961B5EF1AB63726B84AC8FEB141DF3D7BAA6'
 assert sha256(vm_pack_bytes).hexdigest().upper()==vm_pack_sha
 assert vm_pack['scope']['parentList']['sha256'].upper()=='1DED4CE4586380CC800C635BC4C1CFA10E4C327B48176B615F6671E4464425E2'
 outcome=vm_pack['reviewOutcome']
 assert (outcome['candidateCount'],outcome['compositionChain'],outcome['templateResource'],outcome['pending'],outcome['confirmedGlobalNoSourceCount'])==(9,5,1,3,0)
 arch_path=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/five-group-architecture-review-20261025.json'
 arch_raw=arch_path.read_bytes();arch=json.loads(arch_raw.decode('utf-8-sig'))
 arch_sha='56D022C760785EBF3FF315D2B123FE10C2DF678AF4D7C15903C5BCC113C58DF3'
 assert sha256(arch_raw).hexdigest().upper()==arch_sha
 assert arch['reviewer']=='技术总监｜架构与决策'
 assert arch['documentInstructions'].find('原31类主记录')>=0
 vm_candidates={x['name']:x for x in vm_pack['nineCandidateReview']}
 assert len(vm_candidates)==9
 # The director's nine specific decisions are the final classification; the source package's 5/1/3 is only initial triage.
 final_decisions={x['name']:x for x in arch['specificDecisions']}
 assert set(final_decisions)==set(vm_candidates) and len(final_decisions)==9
 final_chain={'ImagePreviewControl','ShapeToolBar','DrawingParameterPanel','DefaultDebugWindow','CustomDebugWindow'}
 final_pending={'ToolIcon','BlobDataAnalysisControl','JudgmentEditorControl','ToolBarControl'}
 assert all(final_decisions[n]['primaryGroup']=='组合链' for n in final_chain)
 assert all(final_decisions[n]['primaryGroup']=='待核' for n in final_pending)
 assert not any(x['primaryGroup']=='确认无源' for x in final_decisions.values())
 vm_external_href=vm_pack_path.as_uri()
 groups={
  '控件':['LabeledControl','RoiEditorControl','RangeSlider','ColorPicker'],
  '模板':['ParamBinding','ParamBindingBase','ConfigSetting','ImageSourceSelector','RangeInputControl','NumericUpDown','ToggleSwitch','ColorSelector'],
  '组合链':['BlobAnalysisToolDebugControl','ImageControl','ResultRenderLayer','GraphicsDisplayEditorControl','TextDisplayEditorControl','DispItemColorControl','DispTextColorControl','ImagePreviewControl','RoiEditorManagerControl','RoiSubscribePanel','ShapeToolBar','DrawingParameterPanel','ToolDebugControlBase','DefaultDebugWindow','CustomDebugWindow','BlobDataAnalysisControl','JudgmentEditorControl','ToolIcon','ToolBarControl'],
  '待核':[],
  '确认无源':[]
 }
 flat=[n for values in groups.values() for n in values]
 assert len(flat)==31 and len(set(flat))==31 and set(flat)==set(org_by_name)
 group_for={n:g for g,values in groups.items() for n in values}
 expected_vm_groups={'composition-chain':'组合链','template-resource':'组合链','pending':'组合链'}
 for name,item in vm_candidates.items(): assert group_for.get(name)==expected_vm_groups[item['group']],(name,group_for.get(name),item['group'])
 delta_href=os.path.relpath(org_path,PAGE.parent).replace('\\','/')
 arch_href=os.path.relpath(arch_path,PAGE.parent).replace('\\','/')
 vm_inv=ROOT/'artifacts/analysis/vm-control-library-inventory-20261024/metadata-summary.json'
 vm_href=os.path.relpath(vm_inv,PAGE.parent).replace('\\','/')
 engineering=ROOT/'artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/implementation/engineering-deployment-final-v3.json'
 eng_href=os.path.relpath(engineering,PAGE.parent).replace('\\','/')
 def link(target,label):
  href=os.path.relpath(target,PAGE.parent).replace('\\','/')
  return '<a href="'+E(href)+'">'+E(label)+'</a>'
 def origin(name,mapping):
  if name=='ImagePreviewControl':
   return 'VMControls.WPF；ModuleResultView.xaml:268装配LocalImageListView→ModuleResultView.xaml.cs:439创建并注入DataForUI.ModuleDisplayDecorator→ModuleDisplayDecorator.cs:376解析当前图像/上游参数根与流程范围→LocalImageListView.xaml:45绑定自动切换/运行模式→ImageListPanel.xaml:190缩略图/删除→ImageListPanelBase.cs:1314选图、改来源参数并触发流程。VM反编译重建组织不证明原.csproj目录，运行时交互未核。'
  if name in ('DefaultDebugWindow','CustomDebugWindow'):
   return 'ProcedureConfigControl.cs F03 GetExternTabWin:808–834：先从已加载程序集按 ModuleTypeName+Control 查找，否则尝试模块目录 ModuleTypeName+Control.dll，并选首个 TabWinBase 派生类型；F04 OpenParamsTabControl:712–787：无扩展窗时回退 ParamsTabControlBase，注入 ParamsConfig/ROI，设置 Owner、modeless Show 并挂 Closed。ParamsTabControlBase.cs:31–68转发 ParamsConfig 并绑定窗口尺寸；paramstabcontrolbase.xaml:2,40；ParamsConfigControlBase.cs:37–53、151–185、206–268承载参数树/控件。仅为局部窗口装配/宿主职责证据，不是同名窗口或类型对应。反编译重建树不证明原csproj。'
  if name=='ToolIcon': return 'VMControls.WPF图标资源及XAML图像用法可见；CLR组件、资源键与消费者链尚待核。'
  if name=='ToolBarControl': return 'VM主界面工具栏/相关资源可见；具体CLR类型、程序集、命令路由及同职责关系待核。'
  if name in ('BlobDataAnalysisControl','JudgmentEditorControl'):
   return 'VM负责人复核：当前已核范围未闭合到同职责结果表/独立编辑器；属待核，不可写为确认无源。'
  assemblies=[]
  if any(x in mapping for x in ('ParamsConfig','RenderControlBase','ROISelecterHolder')): assemblies.append('VMControls.WPF')
  if any(x in mapping for x in ('RoiSelectControl','ColorPickerControlView','SliderArrange','LeftRightControl')) or 'DataTemplate' in mapping: assemblies.append('Apps.XmlUI')
  if any(x in mapping for x in ('AlgorithmTabRoot','ButtonSelecter','GroupLinkItem')) or 'XML' in mapping: assemblies.append('Apps.XmlParser（参数/解析来源）')
  if not assemblies: return '未闭合到VM同名/同职责CLR类型；仅列本行能证的模板/组合，完整消费者边界待核。'
  return '可核程序集/来源类型：'+'、'.join(dict.fromkeys(assemblies))+'。模板键/资源文件见本行路径引用；反编译重建树不证明原项目目录或运行时BAML/pack URI装配。'
 def esc(value): return E('' if value is None else str(value))
 def render_blob_result_responsibility_triptych():
  cross=cross_tool_result
  rec=cross['recommendation']
  keys=['VM','current','target']
  labels=result_diagram['lanes']
  cards=[]
  for key,label in zip(keys,labels):
   content=''.join('<p><b>'+E(row['topic'])+'</b><br>'+E(row[key])+'</p>' for row in result_diagram['responsibilityRows'])
   cards.append('<div class="br-card"><b>'+E(label)+'</b>'+content+'</div>')
  evidence=[]
  for group,refs in result_diagram['evidenceLocations'].items():
   evidence.append('<p><b>'+E(group)+'</b><br>'+E('；'.join(refs))+'</p>')
  criteria=''.join('<li>'+E(x)+'</li>' for x in result_diagram['acceptanceForThisDocument'])
  return ('<style>.o06-result-duty-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;align-items:stretch;width:100%}.o06-result-duty-grid>div{min-width:0}@media(max-width:900px){.o06-result-duty-grid{grid-template-columns:1fr}}</style>'
   '<section id="o06-blob-result-responsibility-triptych" class="br-section" aria-labelledby="o06-blob-result-responsibility-title" style="display:block;width:100%;margin:18px 0">'
   '<h5 id="o06-blob-result-responsibility-title">跨工具结果呈现责任图：VM样本 / 我方视图 / 目标分工</h5>'
   '<p class="br-note"><b>阅读说明：</b>'+E(result_diagram['directAnswer'])+'三栏均为此架构建议的阅读投影，不增加第二推荐。跨工具8项空工厂已逐工具决定为StandardTable + One（8/8）；本页给出列、来源、宿主与有效行对照，实施/实机验收未做。</p>'
   '<div class="o06-result-duty-grid">'+''.join(cards)+'</div>'
   '<h6>先按实际需求选择是否提供结果分析</h6><div class="o06-result-duty-grid">'+''.join('<div class="br-card"><b>'+E(title)+'</b><p>'+E(body)+'</p></div>' for title,body in [('None｜不提供分析入口','工具明确选择None时，宿主不造空表、不显示分析入口；算法输出、模块结果和绑定仍按各自合同可用。'),('标准结果投影','工具选择标准投影后，才按声明生成通用只读视图。'),('其他类型呈现','按工具需要选择可复用呈现器；曲线、直方图、图像等是例子，不限定其他类型。')])+'</div>'
   '<p>'+E(rec['availability'])+'</p>'
   '<h6>选择分析后，再确定呈现方式：通用 / 可扩展 / 经评审的专用UI</h6>'
   '<div class="o06-result-duty-grid">'+''.join('<div class="br-card"><b>'+E(title)+'</b><p>'+E(body)+'</p></div>' for title,body in [('通用结果能力',rec['common']),('可扩展呈现器',rec['rendererExtension']),('专用结果UI/编辑器',rec['specialized'])])+'</div>'
   '<p><b>我方代表：</b>'+E('；'.join(Path(x['path']).name for x in cross['ourEvidence']))+'。5个专用分析视图与4个通信消费者共用的1个视图是现有证据范围，不新增31项记录。</p>'
   '<p><b>VM代表：</b>'+E('；'.join(Path(x['path']).name for x in cross['vmEvidence']))+'。轮廓匹配/P2P通用表，直方图datalist+datachart共享呈现，形态学文件无datalist；这些样本不推广全部工具，也不证明形态学无结果或需专用UI。</p>'
   '<p><b>编辑流程例外：</b>'+E('；'.join(Path(x['path']).name for x in cross['specializedEditorEvidence']))+'。模型创建/脚本编辑涉及创作与提交生命周期，必须与只读结果视图分开验收。</p>'
   '<details><summary>展开双方源码证据、验收与未知边界</summary>'+''.join(evidence)+'<ul>'+criteria+''.join('<li>'+E(x)+'</li>' for x in cross['recommendation']['acceptance'])+''.join('<li>未知：'+E(x)+'</li>' for x in cross['unknowns'])+'</ul><p>输入 v3 SHA-256：'+CROSS_TOOL_RESULT_INPUT_SHA+'。总监静态源码核对；不是独立第三方复核或实机PASS。</p></details>'
   '<p><a href="#o06-control-blobdataanalysiscontrol">返回BlobDataAnalysisControl原记录</a></p></section>')
 out=['<h5 id="o06-current-control-mapping">31类对象唯一主记录：按证据形态分组</h5>',
  '<p>每个对象在本表仅有一条权威记录，逐行合并我方工程事实、VM来源与职责边界、候选目标、已知消费者及迁移/退出门。D01–X01只交叉引用本表；原10项处置表仅作导航。分组按VM来源证据主形态，不按我方类名或继承形态：控件=来源明确出现的具体可实例化元素；模板=字段/类型到模板的映射；组合链=需跨参数、模板、宿主或窗口多层装配理解的职责；待核=范围内证据不足以判定对应或全局不存在；确认无源需有同职责全范围检索及停止边界。本轮31项无一达到确认无源门槛。目标项目位置均为候选，不表示批准或现行实施。VM反编译重建目录不能推定原始.csproj/solution组织。</p>',
  '<div id="o06-control-migration-gate" class="br-note"><b>工程事实与共同迁移门：</b>Plugin.SDK.csproj文字自称“零依赖纯契约层”，实际UseWPF=true并引用Tool.Kernels与Plugin.Runtime，且默认主题依赖ThemeInfo/Generic.xaml，因此现有SDK边界并非已证明的中立层。Plugin.Wpf真实存在，但目前只有PluginFoundation样例消费者，未进入主slnx且无src/UI/tools生产消费者；OutDir指向artifacts/validation/plugin-foundation-01/bin；ParameterEditorControl自称Minimal editor contract consumer，并非生产编辑器替代，当前没有本表31类实现或Generic.xaml/ThemeInfo。候选组织是Contracts放中立字段/输出且不依UI，Plugin.Wpf按Controls/Primitives、Fields、Regions、Presentation、Themes放有跨工具复用价值的局部视觉，不复制整个SDK/UI/Controls树；Blob.Runtime放算法/业务，Blob.Wpf仅专业布局/转换，UI宿主持有图像源目录、命令/缩略图服务、窗口/场景/调度。Plugin.Wpf正式生产接入、设计器/资源/输出迁移仍待批准。Plugin.Wpf本身目前只在PluginFoundation样例验证接入，不是生产控件层。每行迁移需核消费者、x:Class/namespace/assembly、DefaultStyleKey、ThemeInfo/Generic.xaml、UI AppResources、固定pack URI、设计时加载器、BAML、项目枚举、build输出和Release依赖；插件依赖/资源必须由构建发布规则部署。证据：'+link(org_path,'逐类工程组织delta（SHA256 '+digest+'）')+' · '+link(engineering,'engineering-v3候选与兼容门')+' · '+link(vm_inv,'VM程序集元数据清单')+' · <a href="'+E(vm_external_href)+'">VM 9候选范围复核（SHA256 '+vm_pack_sha+'）</a> · '+link(arch_path,'技术总监五组归类复核（SHA256 '+arch_sha+'）')+' · '+link(FOUR_DECISIONS,'最终四项架构推荐v2（SHA256 '+FOUR_DECISIONS_SHA+'）')+' · '+link(FOUR_EVIDENCE,'冻结证据快照')+'。VMControls.WPF.dll身份为1.0.0.0 / 4.4.1.1459276，SHA256 99FFA401C8784472913AD43813D68A8A5577890C00E149506E3E5664FA61C97D。最终31项分组为4控件/8模板/19组合链/0待核/0确认无源；四项归类以v2为准，局部未知留在对应行。具体项目事实为src/Plugin.SDK/SunEyeVision.Plugin.SDK.csproj、src/Plugin.Wpf/SunEyeVision.Plugin.Wpf.csproj与Directory.Build.props。</div>',
  '<div class="br-scroll"><table><thead><tr><th>对象与职责</th><th>我方当前工程/程序集/文件</th><th>VM程序集、命名空间、模板/组合链与对应边界</th><th>目标组织、迁移消费者/资源/构建发布与旧入口退出理由</th><th>关联事项与证据</th></tr></thead><tbody>']
 out.insert(3,render_cross_tool_migration_gate(cross_tool_result))
 for category,names in groups.items():
  if category=='确认无源':
   out.append('<tr class="br-group"><th colspan="5">确认无源（0）：本轮没有对象达到“同职责全量消费者/源码检索+停止边界”门槛；未定位到源不构成删除依据。</th></tr>')
   continue
  out.append('<tr class="br-group"><th colspan="5">'+E(category)+'（'+str(len(names))+'项）</th></tr>')
  for name in names:
   row=next(x for x in data if x['name']==name);mapping,decision=VM_CONTROL_MAP[name];item=org_by_name[name]
   files=[]
   for f in item.get('currentFiles',[]): files.append(link(ROOT/f,f))
   project=Path(item['currentProject'])
   project_link=link(project,item['currentAssembly']) if project.exists() else '<code>'+E(item['currentAssembly'])+'</code>'
   refs='、'.join(x for x in item.get('currentProjectReferences',[]) if x) or '无直接ProjectReference'
   markers='；'.join(str(m.get('line'))+': '+m.get('text','') for m in item.get('styleAndDesignerMarkers',[])) or '本轮未记录'
   consumers=item.get('knownConsumers') or []
   consumer_text='、'.join(consumers) if consumers else '静态闭包入口：'+'、'.join(item.get('closureVia',[]))+'；外部/动态消费者尚未闭合'
   dids=['<a href="#vm-control-disposition-'+d.lower()+'">'+d+'</a>' for d,objects in DECISION_OBJECTS.items() if name in objects]
   identity='o06-control-'+name.lower()
   v2row=four_by_name.get(name)
   vm_text=mapping
   if name=='ImagePreviewControl':
    vm_text='主界面图像源组合链（不是本方同名控件一对一证明）：VMControls.WPF.ModuleResultView.xaml:268→LocalImageListView；ModuleResultView.xaml.cs:439创建并注入ModuleDisplayDecorator；ModuleDisplayDecorator.cs:376解析当前图像/上游参数根与流程范围；LocalImageListView.xaml:45绑定自动切换/运行模式；ImageListPanel.xaml:190虚拟缩略图/删除；ImageListPanelBase.cs:1314选图、更新来源参数并触发流程。相似职责包括图像列表/缩略图、全部或当前模式、自动切换、加图/目录/清空/逐图删除。本方Input链位于ImageResultControl与ActiveInputSource，通过WorkflowExecutionRequested请求；ImageResultControl先发布运行时输入再RunSelectedImageAsync。差异：VM依附选中模块参数/来源路径；本方依附主结果区和ActiveInputSource。VM缩略图72×52；本方PreviewBitmapCache长边100并另有全图缓存。本方空计数显示“图像源”，VM模板显示当前数/总数。未验证运行等价、同构建截图、执行顺序或响应性。'
   elif name in ('DefaultDebugWindow','CustomDebugWindow'):
    vm_text='本方类路径：'+item['currentFiles'][0]+'。F03 ProcedureConfigControl.cs:808–834 从AppDomain已加载程序集或模块目录 ModuleTypeName+Control.dll 查找首个TabWinBase派生窗；未命中回退ParamsTabControlBase。F04 ProcedureConfigControl.cs:712–787 注入ParamsConfig/ROI、设置Owner、modeless Show并挂Closed。ParamsTabControlBase.cs:31–68转发参数并绑定窗口尺寸；paramstabcontrolbase.xaml:2,40；ParamsConfigControlBase.cs:37–53、151–185、206–268装配参数树/控件。Blob目录未见Control.dll不能排除已加载程序集；反编译树不证明原csproj；我方Default/CustomDebugWindow不是VM同名类型，也未证等价。'
   elif name=='ColorPicker':
    vm_text='VM ImageViewConfig XAML:2503–2556在具体局部样式编辑组合内实例化 ColorPickerControlView:2527–2528；仅证一个颜色编辑子控件，不证明与本方 ColorPicker CLR/完整交互同构。ColorConfig DataTemplate:2608另证字段模板。'
   elif name=='ShapeToolBar':
    vm_text='局部形状选择组合链：RoiSelectControl.xaml:23–79以形状ToggleButton绑定ValueChangedCommand/RoiType；与我方ShapeToolBar形状选择职责相关但非同名控件。具体全量命令路由/全部手势与运行行为仍待映射。'
   elif name=='DrawingParameterPanel':
    vm_text='VM局部组合链：ROISelecter XML→DataTemplate→RoiSelectControl.xaml:15–16合并RoiSelectDataTemplate，:101–121按形状选模板，:151经ContentControl使用ROIListStyle；paramtabcontrol/listview/roiselectdatatemplate.xaml:27–35绑定CurROIData/BoxROI→BoxROIStack、:76–132按类型选ContentTemplate；Apps.XmlUI.ParamTabControl.BoxROIStack.xaml:15–115编辑CenterX/Y/Width/Height/Angle并发ROIValueChangedCommand。对应本方DrawingParameterPanel.xaml:48–56、80–88、139–162的矩形数值编辑职责。其他形状参数/手势未证。'
   elif name=='ToolIcon':
    vm_text='VMControls.WPF图标资源和XAML图像用法可见；我方ToolIcon同职责CLR组件、资源键及消费链待核，故按架构复审核定归“待核”而非“模板”。'
   elif name=='ToolBarControl':
    vm_text='VM主界面工具栏及相关资源可见；当前没有证据闭合到本方ToolBarControl的CLR类型、命令路由或职责等价。'
   elif name in ('BlobDataAnalysisControl','JudgmentEditorControl'):
    vm_text=VM_CANDIDATE_SUMMARY[name]
   if v2row:
    vm_text=v2row['counterpart']+' 对照边界：'+v2row['unknown']
   elif name not in ('ImagePreviewControl','DefaultDebugWindow','CustomDebugWindow','ColorPicker','ShapeToolBar','DrawingParameterPanel','ToolIcon','ToolBarControl','BlobDataAnalysisControl','JudgmentEditorControl'):
    vm_text=origin(name,mapping)+'<br>'+mapping
   gate='目标依赖门：'+item.get('dependencyGate','')+'；迁移门：'+item.get('migrationGate','')
   candidate=item.get('candidateTarget','')
   if name=='ConfigSetting': candidate='R02职责拆分：中立字段/绑定合同归Contracts，所需局部编辑部件才评估Plugin.Wpf/Controls/Fields；delta中的ConfigSetting.cs仅是组织锚点，不整类迁移。'
   elif name=='ImagePreviewControl': candidate='继续归src/UI宿主图像源/结果功能区；拆分视图/VM、图像目录与缩略图服务、命令/流程协调；不得整体迁入Plugin.Wpf。'
   elif name in ('RoiEditorControl','RoiEditorManagerControl','RoiSubscribePanel','ShapeToolBar','DrawingParameterPanel'): candidate='职责拆分候选：Plugin.Wpf/Controls/Regions局部视觉；Contracts中立几何/字段；宿主持有交互scope、坐标及协调。原delta路径仅为逐类候选，不是直接搬迁决定。'
   elif name in ('GraphicsDisplayEditorControl','TextDisplayEditorControl','ResultRenderLayer','DispItemColorControl','DispTextColorControl'): candidate='Plugin.Wpf/Controls/Presentation局部呈现/样式候选；结果合同归Contracts，scene/owner由UI宿主负责。原delta路径仅为逐类候选。'
   elif name in ('BlobAnalysisToolDebugControl','BlobDataAnalysisControl'): candidate='tools/SunEyeVision.BlobAnalysis/Wpf/Views（可选Blob.Wpf候选）；运行算法留Blob.Runtime，视图不进入runtime。待消费者及发布边界核定。'
   elif name in ('ImageControl','LabeledControl','ToolIcon','ToggleSwitch','RangeInputControl','NumericUpDown','RangeSlider','ColorSelector','ColorPicker'): candidate='Plugin.Wpf按职责候选归Controls/Primitives或Fields；只抽局部视觉，不整类复制Plugin.SDK/UI/Controls。具体目录依资源/消费者迁移验证。'
   elif name in ('DefaultDebugWindow','CustomDebugWindow','ToolDebugControlBase'): candidate='窗口/执行装配由UI宿主承担；SDK仅保留迁移所需有限兼容入口。VM局部组合证据不证明原类直接搬迁。'
   elif name=='ToolBarControl': candidate='目标归属待确认；先映射实际消费者/命令与资源，不因主界面toolbar同名/可见就迁入Plugin.Wpf。'
   if v2row:
    candidate={
     'BlobDataAnalysisControl':'宿主通用当前结果表/摘要/稳定元素选择投影；不新增Blob.Wpf专用结果表。',
     'JudgmentEditorControl':'统一字段组与条件模板；Blob.Runtime继续负责判定规则和状态计算。',
     'ToolIcon':'宿主资源解析与标准Image/DataTemplate；保留可复用矢量素材并作为可部署资源。',
     'ToolBarControl':'由编辑会话驱动的局部类型选择模板；临时绘图模式与算法参数草稿分开。'
    }[name]
   arch_decision=next((x['decision'] for x in arch['specificDecisions'] if x['name']==name),None)
   disposition=('候选目标：'+candidate+'。'+item.get('dispositionBasis','')+'。当前处置建议：'+(arch_decision or decision))
   if v2row:
    disposition='最终架构推荐v2（待用户审核，非实施批准）：'+v2row['disposition']
   details=('已知消费者/闭包：'+consumer_text+'。样式标记：'+markers+'。'+gate+'。未知：'+item.get('unknown',''))
   current_role=self_by_name[name]['responsibility'] if v2row else row.get('role','')
   cell1='<code>'+E(name)+'</code><br><b>'+E(item.get('responsibilityLayer',''))+'</b><br>当前职责：'+E(current_role)
   cell2='<b>工程：</b>'+project_link+'<br><code>'+E(item['currentProject'])+'</code><br><b>namespace：</b><code>'+E(item.get('currentNamespace',''))+'</code><br><b>文件：</b>'+'<br>'.join(files)+'<br><b>ProjectReference：</b><code>'+E(refs)+'</code>'
   cell3='<b>程序集/命名空间边界：</b>'+E(origin(name,mapping))+'<br><b>来源与职责：</b>'+E(vm_text)
   if v2row:
    vm_sources=''.join('<li>'+E(src)+'</li>' for src in v2row['vmSourceEvidence'])
    visible_vm_sources='<br>'.join(E(src) for src in v2row['vmSourceEvidence'][:2])
    cell3='<b>VM对应职责：</b>'+E(v2row['counterpart'])+'<br><b>关键源码位置：</b>'+visible_vm_sources+'<details><summary>展开其余源码映射与程序集SHA</summary><ul>'+vm_sources+'</ul></details><p><b>边界：</b>'+E(v2row['unknown'])+'</p>'
   cell4='<b>候选路径（非批准）：</b><code>'+E(candidate)+'</code><br>'+E(disposition)+'<br><b>消费者迁移/资源/build与publish/旧入口退出：</b>'+E(details)
   if v2row:
    acceptance=''.join('<li>'+E(x)+'</li>' for x in v2row['acceptance'])
    engineering_items=''.join('<li>'+E(x)+'</li>' for x in v2row['engineering'])
    differences=E(v2row['businessDifferences'])
    decision_detail=('<details class="o06-final-v2"><summary>展开：差异、承接方式与验收门</summary>'
      '<p><b>为什么这样处置：</b>'+E(v2row['reason'])+'</p>'
      '<p><b>VM对应关系：</b>'+E(v2row['counterpart'])+'</p>'
      '<p><b>功能承接：</b>'+E(v2row['handoff'])+'</p>'
      '<p><b>业务差异：</b>'+differences+'</p>'
      '<p><b>工程落点候选：</b></p><ul>'+engineering_items+'</ul>'
      '<p><b>验收条件：</b></p><ul>'+acceptance+'</ul>'
      '<p><b>仍未知：</b>'+E(v2row['unknown'])+'</p>'
      '<p><b>依据：</b><a href="'+E(os.path.relpath(FOUR_DECISIONS,PAGE.parent).replace("\\","/"))+'">最终架构推荐v2</a>（SHA256 '+FOUR_DECISIONS_SHA+'） · '
      '<a href="'+E(os.path.relpath(FOUR_EVIDENCE,PAGE.parent).replace("\\","/"))+'">VM与自研证据快照</a>（SHA256 '+FOUR_EVIDENCE_SHA+'）'
      +(' · <a href="'+E(os.path.relpath(FOUR_SCREENSHOT,PAGE.parent).replace("\\","/"))+'">用户界面截图</a>（仅佐证位置和列；版本未对齐）' if name=='BlobDataAnalysisControl' else '')
      +'</p></details>')
    cell4+=decision_detail
   vmreview=vm_candidates.get(name)
   vmproof=(v2row['counterpart']+' 未知边界：'+v2row['unknown']) if v2row else VM_CANDIDATE_SUMMARY.get(name,'静态对应仅限本格所列职责，不证明运行时等价。')
   vmproof_link=(' · <a href="'+E(vm_external_href)+'">VM本轮逐候选证据</a> · <a href="'+E(arch_href)+'">五组架构复核</a> · SHA256 '+E(vm_pack_sha)+' / '+E(arch_sha)) if vmreview else ''
   if v2row:
    vmproof_link=' · <a href="'+E(os.path.relpath(FOUR_DECISIONS,PAGE.parent).replace("\\","/"))+'">最终推荐v2</a> · <a href="'+E(os.path.relpath(FOUR_EVIDENCE,PAGE.parent).replace("\\","/"))+'">冻结VM证据快照</a> · <a href="'+E(os.path.relpath(FOUR_SELF,PAGE.parent).replace("\\","/"))+'">自研职责快照</a> · SHA256 '+FOUR_DECISIONS_SHA+' / '+FOUR_EVIDENCE_SHA+' / '+FOUR_SELF_SHA
   if name in ('DefaultDebugWindow','CustomDebugWindow'):
    window_sources=[('VMControls.WPF.ProcedureConfigControl.cs','F03/F04 ProcedureConfigControl.cs:712–787,808–834','A1C233068DB35667F7DD49B05839701364CC2EBE9CA699F5F413317ABBE4DFB4'),('VMControls.WPF.ParamsTabControlBase.cs','ParamsTabControlBase.cs:31–68','D6A194572A76819CEB31048F2D0110A525974D3802F928FDCDF5C1F7B02380F1'),('wpf-resources/paramstabcontrolbase.xaml','paramstabcontrolbase.xaml:2,40','30AE71B37D6D0FD6DEC5CF584763E051B9FA61C6493B8BB9A82037251DC3621D'),('VMControls.WPF.ParamsConfigControlBase.cs','ParamsConfigControlBase.cs:37–53,151–185,206–268','C5C43AA489FF393A4F1810AD3157F90AC903245321529E8D32038BBA426F1A5B')]
    vm_source_root=vm_pack_path.parents[2]/'source'
    window_links=[]
    for filename,label,expected_sha in window_sources:
     source_file=vm_source_root/filename;source_raw=source_file.read_bytes();assert sha256(source_raw).hexdigest().upper()==expected_sha,(filename,'source drift')
     window_links.append('<a href="'+E(source_file.as_uri())+'">'+E(label)+'</a> · SHA256 '+expected_sha)
    vmproof_link+=' · '+' · '.join(window_links)
   cell5='<b>组：</b>'+E(category)+'<br><b>关联处置：</b>'+('、'.join(dids) if dids else '无')+'<br><a href="'+E(delta_href)+'">本行工程组织delta（含原始候选路径）</a> · VM清单/本行自研源码见上两格'+vmproof_link+'<br><b>证据状态：</b>'+E(vmproof)
   if name=='BlobDataAnalysisControl':
    shot_href=E(os.path.relpath(FOUR_SCREENSHOT,PAGE.parent).replace('\\','/'))
    cell5+='<br><a href="#o06-blob-result-responsibility-triptych">打开全宽责任图与跨工具边界</a> · <a href="#o06-capability-matrix">查看原能力映射与适用范围</a><br><a href="'+shot_href+'"><img src="'+shot_href+'" alt="用户提供的VM当前结果表截图" style="max-width:260px;height:auto;border:1px solid #557080;border-radius:4px"></a><br><small>用户截图只证明下方当前结果表的位置与可见列；版本未对齐，不能证明运行行为。</small>'
   out.append('<tr id="'+E(identity)+'" data-control-name="'+E(name)+'" data-control-group="'+E(category)+'"><td>'+cell1+'</td><td>'+cell2+'</td><td>'+cell3+'</td><td>'+cell4+'</td><td>'+cell5+'</td></tr>')
 out.append('</tbody></table></div>')
 out.append(render_blob_result_responsibility_triptych())
 return ''.join(out)

def apply():
 OUT.mkdir(parents=True,exist_ok=True)
 data=PAGE.read_bytes();page=data.decode('utf-8')
 a=page.index('<h3 id="br-blob-controls">');b=page.index('<h3 id="br-region-result-rendering">',a)
 old=page[a:b]
 heading,body=old.split('</h3>',1)
 new=heading+'</h3>'+render()+render_current_control_table()+'\n'
 prefix,suffix=page[:a],page[b:]
 assembled=prefix+new+suffix
 normalize=__import__('runpy').run_path(str(ROOT/'artifacts/analysis/vm-control-blob-document-merge-20261024/merge_controls_h4.py'))['repair_external_links']
 outside_unchanged=normalize(prefix)==prefix and normalize(suffix)==suffix
 assembled=normalize(assembled)
 assembled,final_review=render_blob_final_reconciliation(assembled)
 # Apply the current stage receipt after historical design rendering, so old status cannot replace it.
 stage_renderer=ROOT/'scripts/documentation/update_s1_stage_summary.py'
 if stage_renderer.exists():
  import runpy
  assembled=runpy.run_path(str(stage_renderer))['render_s1_stage'](assembled)
 PAGE.write_bytes(assembled.encode('utf-8'))
 report={'scope':'Blob complete final design review; O06 existing sections only; documentation/static QA; no product modification/build/run/release/test or implementation approval','selfTypesMapped':31,'capabilityRows':10,'pageBefore':sha256(data).hexdigest(),'pageAfter':sha256(PAGE.read_bytes()).hexdigest(),'approvedSectionUpdates':final_review['updatedSections'],'externalEvidenceLinksNormalized':True,'finalReviewInput':{'path':str(BLOB_FINAL_INPUT.relative_to(ROOT)).replace('\\','/'),'sha256':BLOB_FINAL_INPUT_SHA},'productImplementationApproved':final_review['implementationApproved'],'runtimeVerified':final_review['runtimeVerified'],'crossToolResultInput':{'path':str(CROSS_TOOL_RESULT_INPUT.relative_to(ROOT)).replace('\\','/'),'sha256':CROSS_TOOL_RESULT_INPUT_SHA},'supersededInput':{'path':str(CROSS_TOOL_RESULT_V2.relative_to(ROOT)).replace('\\','/'),'sha256':CROSS_TOOL_RESULT_V2_SHA},'priorInput':{'path':str(CROSS_TOOL_RESULT_V1.relative_to(ROOT)).replace('\\','/'),'sha256':CROSS_TOOL_RESULT_V1_SHA},'sources':[{'id':k,'path':str(p),'sha256':sha256(p.read_bytes()).hexdigest()} for k,p in SOURCES.items()]}
 (OUT/'review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k!='sources'},ensure_ascii=False))

if __name__=='__main__':apply()
