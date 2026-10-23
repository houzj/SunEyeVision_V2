const fs=require('fs'),assert=require('assert/strict');
const a=JSON.parse(fs.readFileSync('artifacts/validation/delivery-review.json','utf8')),j=JSON.parse(fs.readFileSync('artifacts/validation/comparison-work-packages.json','utf8'));
const s=fs.readFileSync('docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html','utf8');
assert.equal(a.packages.length,20);assert.equal(new Set(a.packages.map(x=>x.id)).size,20);assert.equal(a.criteria.length,5);
for(const p of a.packages){assert.equal(p.criteriaPassed.length,5);assert.equal(p.analysisAccepted,p.criteriaPassed.every(Boolean));for(const id of p.anchors)assert.ok(s.includes('id="'+id+'"'),id);assert.ok(p.basis&&p.remaining&&p.evidenceGap);}
const accepted=a.packages.filter(x=>x.analysisAccepted).length,closed=a.evidenceTasks.filter(x=>x.closed).length;
assert.equal(accepted,20);assert.deepEqual(a.packages.filter(x=>!x.analysisAccepted).map(x=>x.id),[]);assert.equal(a.evidenceTasks.length,7);assert.equal(closed,0);
assert.equal(j.progressPolicy.analysisCompletionPercent,accepted/20*100);assert.equal(j.progressPolicy.evidenceClosurePercent,closed/7*100);assert.equal(j.progressPolicy.totalCompletionPercent,j.progressPolicy.analysisCompletionPercent);
const current=s.match(/<article id="comparison-progress">[\s\S]*?<\/article>/)[0];assert.ok(current.includes('100%（20/20）'));assert.ok(current.includes('57.1%（24/42）'));assert.ok(!current.includes('0/7＝0%'));
const vm=s.match(/<article[^>]*id="vm-globals"[\s\S]*?<\/article>/)[0],ours=s.match(/<article[^>]*id="suneye-globals"[\s\S]*?<\/article>/)[0];assert.ok(vm.includes('不能以自研RunSolutionAsync'));assert.ok(ours.includes('先从runtimeGlobals移除'));assert.ok(!ours.includes('我们的生产者写回时机、跨流程可见性'));
const out={reviewed:20,analysisAccepted:accepted,analysisCompletionPercent:100,pending:[],evidenceVerified:24,evidenceTotal:42,evidenceVerificationPercent:57.1,categoriesComplete:closed,categoriesTotal:7,C17AttributionFixed:true,analysisDeltaPercentagePoints:0,newEvidenceBaseline:true};fs.writeFileSync('artifacts/validation/delivery-metrics-validation.json',JSON.stringify(out,null,2)+'\n');console.log(JSON.stringify(out));
