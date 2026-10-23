const fs=require('fs'),assert=require('assert/strict'),crypto=require('crypto');
const d=JSON.parse(fs.readFileSync('artifacts/validation/sdk-engineering-migration.json'));
const byName=new Map(d.projects.map(p=>[p[0],p]));
function visit(n,stack=[]){assert(!stack.includes(n),'dependency cycle '+stack.concat(n));for(const dep of byName.get(n)[4]){assert(byName.has(dep),dep);visit(dep,stack.concat(n));}}
for(const [n] of d.projects)visit(n);
assert.deepEqual(byName.get('Runtime.SDK')[4],['Contracts']);assert.deepEqual(byName.get('Contracts')[4],[]);
assert(!byName.get('Tool.SDK')[4].some(n=>/Wpf|Engine|Runtime.SDK/.test(n)));
const seen=new Set();for(const x of d.inventory){assert(!seen.has(x.source));seen.add(x.source);assert(byName.has(x.project));assert(x.target.startsWith('src/'+x.project+'/'));assert(x.action&&x.reason&&x.review);assert.equal(crypto.createHash('sha256').update(fs.readFileSync(x.source)).digest('hex'),x.sha256);}
function walk(p){return fs.readdirSync(p,{withFileTypes:true}).flatMap(x=>x.isDirectory()?(['bin','obj'].includes(x.name)?[]:walk(p+'/'+x.name)):[p+'/'+x.name]);}
assert.equal(d.inventory.length,walk('src/Plugin.SDK').filter(p=>/\.(cs|xaml)$/.test(p)).length);
for(const e of d.evidence)assert.equal(crypto.createHash('sha256').update(fs.readFileSync(e.path)).digest('hex'),e.sha256);
assert.equal(d.stages.length,6);assert.equal(d.chains.length,4);assert.equal(d.productModified,false);
const out={targetProjects:d.projects.length,mappedSourceFiles:d.inventory.length,keyRewriteGroups:d.rewrites.length,actualChains:d.chains.length,migrationStages:d.stages.length,sourceHashes:true,targetDagAcyclic:true,allSdkSourceFilesCovered:true,abiCompatibilityTested:false,productModified:false};
fs.writeFileSync('artifacts/validation/sdk-engineering-validation.json',JSON.stringify(out,null,2));console.log(JSON.stringify(out));
