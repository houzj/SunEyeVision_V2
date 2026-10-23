// Requirement-by-requirement completion audit; no product or external-workspace writes.
const fs=require('fs'),assert=require('assert/strict'),crypto=require('crypto');
const base='docs/04-工具设计/架构对照/2026/10/21/';
const read=p=>JSON.parse(fs.readFileSync(p));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const E=s=>String(s??'').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const m=read('artifacts/validation/architecture-doc-site-manifest.json');
const h=read('docs/collaboration/ARCH_TARGET_VM_REVIEW_20261022.json');
const sync=read('docs/collaboration/DOC_SYNC_STATUS.json');
const v=read('artifacts/validation/architecture-doc-site-validation.json');
const contracts=require('./architecture-target-contracts.cjs');
const plans=require('./architecture-target-test-plans.cjs');
const requirements=[];
function check(id,requirement,evidence,verify){verify();requirements.push({id,requirement,evidence,status:'proven'});}
check('R01','index导航与独立HTML维护',['index.html','architecture-doc-site-manifest.json'],()=>{
 const index=fs.readFileSync(base+'index.html','utf8');assert(index.includes('id="maintenance-goals"'));
 assert.equal(m.frameworkPages.length,20);assert.equal(m.decisionPages.length,12);
 for(const p of [...m.frameworkPages,...m.decisionPages])assert(fs.existsSync(base+p.file));
});
check('R02','12项双方事实、目标取舍、代价和拟议合同',['architecture-a01.html … architecture-a12.html','architecture-target-contracts.cjs'],()=>{
 assert.equal(h.decisions.length,12);assert.equal(contracts.length,12);
 for(const d of h.decisions){const html=fs.readFileSync(base+'architecture-'+d.id.toLowerCase()+'.html','utf8'),c=contracts.find(c=>c.id===d.id);assert(c);
  for(const text of [d.vm,d.route,d.target,d.tradeoff,c.ours,c.api,c.state,c.owner,c.failure,c.version,c.alternative])assert(html.includes(E(text)),d.id+' missing contract content');
  assert(html.includes('proposed-not-implemented'));assert(html.includes('independent-implementation'));assert(html.includes('未知'));
  for(const e of d.vmEvidence){assert(html.includes(E(e.id)));assert(html.includes(E(e.classification)));assert(html.includes(E(e.behavior)));for(const r of e.refs)assert(html.includes(r.sha256));}
 }
});
check('R03','24项具操作和判定的未运行验收',['architecture-acceptance.html','architecture-target-test-plans.cjs'],()=>{
 const html=fs.readFileSync(base+'architecture-acceptance.html','utf8');let count=0;
 for(const d of h.decisions){assert.equal(plans[d.id].length,2);for(const [i,t] of d.acceptance.entries()){count++;assert.equal(t.status,'planned-not-run');assert(html.includes('id="'+t.id+'"'));for(const text of plans[d.id][i])assert(html.includes(E(text)));}}
 assert.equal(count,24);assert.equal(v.executedAcceptance,0);
});
check('R04','交接与53来源SHA未漂移',['ARCH_TARGET_VM_REVIEW_20261022.json','architecture-doc-site-manifest.json'],()=>{
 assert.equal(sha('docs/collaboration/ARCH_TARGET_VM_REVIEW_20261022.json'),m.handoffSha256);
 assert.equal(m.sourceChecks.length,38);assert.equal(m.officialSources.length,15);
 for(const s of [...m.sourceChecks,...m.officialSources])assert.equal(sha(s.path),s.sha256,s.path);
});
check('R05','生命周期合同无Completion/Acquire循环且可异步排空',['architecture-a01.html','architecture-a03.html','architecture-a06.html'],()=>{
 const a01=contracts.find(x=>x.id==='A01'),a03=contracts.find(x=>x.id==='A03'),a06=contracts.find(x=>x.id==='A06');
 assert(a01.owner.includes('已有ResultLease独立存活'));assert(a01.owner.includes('Completion不等待未来Acquire'));
 assert(a03.state.includes('ResourceRetired只表示本轮临时资源退役'));assert(a03.state.includes('ResultRetired'));
 for(const token of ['同步Dispose','DrainAsync','DisposeAsync','SelfDrainNotAllowed','ACK/显式失败'])assert(a06.failure.includes(token));
});
check('R06','旧锚点、本地链接、手机与打印验证',['architecture-doc-site-validation.json'],()=>{
 assert.equal(v.pages,36);assert.equal(v.legacyRoutes,Object.keys(m.anchorMap).length);assert.equal(v.legacyRoutes,74);assert.equal(v.localLinks,798);
 assert.deepEqual(v.widths,[390,768,1440]);assert(v.printRestore);assert.deepEqual(v.pageErrors,[]);assert.deepEqual(v.remoteRequests,[]);
 for(const p of ['architecture-index-desktop.png','architecture-a07-desktop.png','architecture-a06-mobile.png','architecture-a06-print.png'])assert(fs.statSync('artifacts/validation/'+p).size>0);
});
check('R07','保留原框架/SDK/迁移回归及产品验收边界',['framework-comparison-validation.json','sdk-development-validation.json','sdk-engineering-validation.json'],()=>{
 const f=read('artifacts/validation/framework-comparison-validation.json'),s=read('artifacts/validation/sdk-development-validation.json'),e=read('artifacts/validation/sdk-engineering-validation.json');
 assert.equal(f.comparisonPoints,13);assert.equal(f.sourceIdentities,39);assert.equal(s.sdkSubtopics,12);assert.equal(s.publicOperations,13);assert.equal(e.mappedSourceFiles,320);assert(e.targetDagAcyclic);assert.equal(e.abiCompatibilityTested,false);
 assert.equal(sync.validation.productAcceptanceExecuted,0);assert.equal(sync.validation.checkpointUpgrades,0);
});
check('R08','可见同步水位与事件驱动维护规则',['DOC_SYNC_STATUS.json','index.html#maintenance-goals'],()=>{
 assert.equal(sync.ownerThreadId,'01a160f7-9c99-72c1-b8bf-d689bf19f1ad');assert.equal(sync.handoff.sha256,m.handoffSha256);
 assert.equal(sync.policy.backgroundPolling,false);assert.equal(sync.handoff.wholeRepositoryVerified,false);
 assert.equal(sync.milestones.length,6);assert(sync.milestones.every(x=>x.status==='完成'));
 assert(sync.baselineKind.includes('not a stable accepted product release'));
 assert(sync.policy.forbidden.includes('shared CURRENT_TASK'));
});
check('R09','生成链包含分页及完整导出，不要求全站重生成',['build-new-entry-report.cjs','build-architecture-doc-site.cjs','build-secure-export.cjs'],()=>{
 const canonical=fs.readFileSync('artifacts/validation/build-new-entry-report.cjs','utf8');
 assert(canonical.indexOf("require('./build-architecture-doc-site.cjs')")>canonical.indexOf("require('./build-reader-navigation.cjs')"));
 const exporter=fs.readFileSync('artifacts/validation/build-secure-export.cjs','utf8');assert(exporter.includes('site.frameworkPages'));assert(exporter.includes('site.decisionPages'));
 assert(read('artifacts/validation/secure-export-validation.json').correctPasswordUnlocksZip);
});
const out={batchId:m.batchId,scope:'Current delivered maintenance milestone and event-driven upkeep mechanism; future deliveries remain conditional tasks',requirements,proven:requirements.length,implementationExecuted:false,acceptanceExecuted:0,checkpointUpgrades:0,remainingCurrentMilestoneWork:[],deferred:sync.deferred};
fs.writeFileSync('artifacts/validation/architecture-maintenance-completion-audit.json',JSON.stringify(out,null,2));
console.log(JSON.stringify({proven:requirements.length,currentMilestoneComplete:true,acceptanceExecuted:0,checkpointUpgrades:0}));
