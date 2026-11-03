// Fixed product-only mapping from BLOB_PARAMETER_NEUTRAL_STRUCTURE_CONTRACT_20261103.md.
// Usage: node scripts/analysis/Invoke-ParameterNeutralStructure.mjs apply|check|refresh-catalog
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const mode=process.argv[2]??'check';
const base='8962f6bdbc74e88637fecd3d8bd01677849134b1';
const product='src/Plugin.Wpf/';
const catalogPath=product+'Catalog/controls.json';
const reportPath='Release/_validation/parameter-neutral-public-20261103/structure-map.json';
const git=(...args)=>execFileSync('git',args,{cwd:root,maxBuffer:64*1024*1024});
const sha=bytes=>crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const abs=p=>path.join(root,p);
const sortLongest=pairs=>pairs.sort((a,b)=>b[0].length-a[0].length);
const familyPairs=[
 ['Composition/Apps.XmlUI/Apps.XmlUI','Composition/Parameters/Core'],
 ['Composition/Apps.XmlUI/Apps.XmlUI.ParamTab','Composition/Parameters/Rows'],
 ['Composition/Apps.XmlUI/Apps.XmlUI.ParamTabControl','Composition/Parameters/Editors'],
 ['Composition/Apps.XmlUI/resourcedictionaries/globalresources.xaml','Composition/Parameters/ParameterResources.xaml'],
 ['Composition/Apps.XmlUI/paramtabcontrol/paramtabui/subui/clearbuttontextbox/clearbuttontextboxstyle.xaml','Composition/Parameters/Rows/ClearButtonTextBoxStyle.xaml'],
 ['Numeric/AppsXmlUi','Numeric/Coordinates'],
 ['Numeric/VMControls/UpDown','Numeric/UpDown'],
 ['Numeric/VMControls/Editors','Numeric/UpDown/PropertyGrid/Editors'],
 ['Dependencies/Apps.BaseData','Dependencies/ValueConverters'],
 ['Dependencies/Apps.XmlParser','Dependencies/Regions'],
 ['Dependencies/Apps.UIHelper','Dependencies/VisualElements'],
 ['Input/VM.PcUI/HikPcUI','Input/FieldPrimitives'],
 ['Input/VM.PcUI/HikPcUI.ListView','Input/FieldPrimitives/Editors'],
 ['Input/VM.PcUI/HikPcUI.Commom','Input/FieldPrimitives/Shared'],
 ['Input/VM.PcUI/HikPcUI.Controls','Input/FieldPrimitives/Controls'],
 ['Input/VM.PcUI/HikPcUI.Converts','Input/FieldPrimitives/Converters'],
 ['Input/VM.PcUI/HikPcUI.ImageView','Input/FieldPrimitives/Imaging'],
 ['Input/VM.PcUI/HikPcUI.Models','Input/FieldPrimitives/Models'],
 ['Composition/Apps.Style','Composition/Styles'],
];
const pathPairs=sortLongest([
 ...['Controls/Native/','Themes/Native/'].flatMap(prefix=>familyPairs.map(([a,b])=>[product+prefix+a,product+prefix+b])),
 ...[
  ['Composition/Apps.Style/Resources','Composition/Icons'],
  ['Composition/Apps.XmlUI','Composition/EditorAssets'],
  ['Numeric/Apps.XmlUI','Numeric/Coordinates'],
  ['Input/VM.PcUI','Input/FieldPrimitives'],
 ].map(([a,b])=>[product+'Themes/Assets/Native/'+a,product+'Themes/Assets/Native/'+b]),
 [product+'Themes/VmFieldStyles.xaml',product+'Themes/FieldStyles.xaml'],
 [product+'Themes/VmFieldStyles.provenance.json',product+'Themes/FieldStyles.provenance.json'],
 // The UpDown family has two actual theme pages under VMControls/controls.
 [product+'Themes/Native/Numeric/VMControls',product+'Themes/Native/Numeric/UpDown'],
]);
const nsPairs=sortLongest([
 ['Composition.Apps.XmlUI.ParamTabControl','Composition.Parameters.Editors'],
 ['Composition.Apps.XmlUI.ParamTab','Composition.Parameters.Rows'],
 ['Composition.Apps.XmlUI','Composition.Parameters.Core'],
 ['Numeric.Apps.XmlUI.ParamTabControl','Numeric.Coordinates'],
 ['Numeric.VM.Controls.PropertyGrid.Editors','Numeric.UpDown.PropertyGrid.Editors'],
 ['Numeric.VM.Controls','Numeric.UpDown'],
 ['Dependencies.Apps.BaseData','Dependencies.ValueConverters'],
 ['Dependencies.Apps.XmlParser.ParamTab','Dependencies.Regions'],
 ['Dependencies.Apps.UIHelper.Extern','Dependencies.VisualElements'],
 ['Input.HikPcUI.ListView','Input.FieldPrimitives.Editors'],
 ['Input.HikPcUI.Commom','Input.FieldPrimitives.Shared'],
 ['Input.HikPcUI.Controls','Input.FieldPrimitives.Controls'],
 ['Input.HikPcUI.Converts','Input.FieldPrimitives.Converters'],
 ['Input.HikPcUI.ImageView','Input.FieldPrimitives.Imaging'],
 ['Input.HikPcUI.Models','Input.FieldPrimitives.Models'],
 ['Input.HikPcUI','Input.FieldPrimitives'],
].map(([a,b])=>['SunEyeVision.Plugin.Wpf.Native.'+a,'SunEyeVision.Plugin.Wpf.Native.'+b]));
const textPairs=sortLongest([
 ...pathPairs.flatMap(([a,b])=>[[a,b],[a.slice(product.length),b.slice(product.length)]]),
]);
const typePairs=[['HikCheckBox','FieldCheckBox'],['HikTextBox','FieldTextBox'],
 ['HikCoverStyleConverter','OverlayStyleConverter'],['HikCoverCircleStyleConverter','CircularOverlayStyleConverter']];
const mapLeaf=p=>{
 for(const [a,b]of typePairs)p=p.replace(new RegExp('/'+a+'(?=\\.)','g'),'/'+b);
 return p;
};
const mapPath=p=>{
 if(p.endsWith('.pending'))return p;
 for(const [a,b] of pathPairs)if(p.toLowerCase()===a.toLowerCase()||p.toLowerCase().startsWith(a.toLowerCase()+'/'))return mapLeaf(b+p.slice(a.length));
 return mapLeaf(p);
};
const escaped=s=>s.replace(/[.*+?^${}()|[\]\\]/g,'\\$&');
// A single pass prevents shorter prefixes from modifying already-mapped names.
const replacements=new Map(textPairs.map(([a,b])=>[a.toLowerCase(),b]));
const textPattern=new RegExp(textPairs.map(([a])=>escaped(a)).join('|'),'gi');
const nsReplacements=new Map(nsPairs);
const nsPattern=new RegExp(nsPairs.map(([a])=>escaped(a)).join('|'),'g');
const typeReplacements=new Map(typePairs);
const typePattern=new RegExp('\\b(?:'+typePairs.map(([a])=>a).join('|')+')\\b','g');
const mapText=text=>text.replace(nsPattern,m=>nsReplacements.get(m))
 .replace(textPattern,m=>replacements.get(m.toLowerCase()))
 .replace(typePattern,m=>typeReplacements.get(m))
 .replaceAll('HikTextBoxFg','FieldTextBoxForeground').replaceAll('Col_Menubar_VMInfo_','Col_Menubar_ProductInfo_');
const packPattern=/(?:pack:\/\/application:,,,)?\/SunEyeVision\.Plugin\.Wpf;component\/([^"'<>\s)]+)/g;
const productText=p=>/\.(cs|xaml|props|csproj|resx)$/.test(p)&&!p.includes('ExportAlgorithm/ExportMVDCode.cs');
const protectedKey=k=>/^original/i.test(k)||/history|receipt|report/i.test(k)||['assembly','assemblyMvid','assemblySha256','sourceAssemblies','sourceBaseline','effectiveSourceBaseline','candidateRawSha256'].includes(k);
function identities(node,prefix='',result=[]){
 if(Array.isArray(node))node.forEach((v,i)=>identities(v,prefix+'/'+i,result));
 else if(node&&typeof node==='object')for(const [k,v]of Object.entries(node)){
  if((prefix===''&&k!=='entries'&&!k.startsWith('current'))||protectedKey(k))result.push([prefix+'/'+k,v]);else identities(v,prefix+'/'+k,result);
 }
 return result;
}
function refreshCatalog(text){
 // Only entries/current* describe the current product. Entire history/receipt
 // subtrees, pending snapshots and original source identity are immutable.
 const document=JSON.parse(text),before=identities(document);
 function current(node){
  if(Array.isArray(node))return node.map(current);
  if(typeof node==='string')return mapText(node);
  if(!node||typeof node!=='object')return node;
  const next={};
  for(const [k,v]of Object.entries(node))next[k]=protectedKey(k)?v:current(v);
  const pk=Object.hasOwn(next,'localPath')?'localPath':Object.hasOwn(next,'path')?'path':null;
  if(pk&&typeof next[pk]==='string'){
   const p=next[pk].replaceAll('\\','/');
   if(/^(src|tools)\//.test(p)&&fs.existsSync(abs(p))){
    const hk=pk==='localPath'?'localSha256':'sha256';
    if(Object.hasOwn(next,hk))next[hk]=sha(fs.readFileSync(abs(p)));
    if(Object.hasOwn(next,'gitBlob'))next.gitBlob=git('hash-object','--path='+p,p).toString().trim();
   }
  }
  return next;
 }
 for(const k of Object.keys(document))if(k==='entries'||k.startsWith('current'))document[k]=current(document[k]);
 if(JSON.stringify(before)!==JSON.stringify(identities(document)))throw Error('Original identity/history changed');
 const newline=text.includes('\r\n')?'\r\n':'\n';
 return (JSON.stringify(document,null,2)+'\n').replaceAll('\n',newline);
}
function productFiles(){
 const found=[];
 function visit(dir){for(const e of fs.readdirSync(abs(dir),{withFileTypes:true})){
  const p=dir+e.name;
  if(e.isDirectory()){if(!['obj','bin','.git'].includes(e.name))visit(p+'/');}
  else found.push(p);
 }}visit(product);return found;
}
if(mode==='apply'){
 const files=git('ls-files','-z',product).toString().split('\0').filter(Boolean);
 const moves=files.filter(p=>mapPath(p)!==p).map(p=>[p,mapPath(p)]);
 const targets=new Set();
 for(const [from,to]of moves){
  if(targets.has(to.toLowerCase())||fs.existsSync(abs(to)))throw Error('Target collision: '+to);
  targets.add(to.toLowerCase());
 }
 const records=[];
 const existingMissingUris=[];
 // Capture every old missing/dynamic URI before moving any resource. They are
 // explicit baseline limitations, never silently accepted as new regressions.
 for(const from of files.filter(productText)){
  const source=fs.readFileSync(abs(from),'utf8');
  for(const match of source.matchAll(packPattern)){
   const target=decodeURIComponent(match[1]);
   if(!fs.existsSync(abs(product+target)))existingMissingUris.push({
    oldSource:from,oldLine:source.slice(0,match.index).split('\n').length,oldTarget:target,
    source:mapPath(from),target:mapText(target),dynamic:target.includes('{'),
    classification:target.includes('{')?'existing-dynamic-uri':fs.existsSync(abs(product+target+'.pending'))?'existing-pending-resource':'existing-missing-resource',
   });
  }
 }
 for(const from of files){
  if(from===catalogPath)continue;
  const to=mapPath(from),before=fs.readFileSync(abs(from));
  const after=productText(from)?Buffer.from(mapText(before.toString('utf8'))):before;
  if(from!==to||!before.equals(after)){
   fs.mkdirSync(path.dirname(abs(to)),{recursive:true});
   fs.writeFileSync(abs(to),after);
   if(from!==to)fs.unlinkSync(abs(from));
   records.push({from,to,beforeSha256:sha(before),afterSha256:sha(after),bytePreserved:before.equals(after)});
  }
 }
 const beforeCatalog=fs.readFileSync(abs(catalogPath),'utf8');
 fs.writeFileSync(abs(catalogPath),refreshCatalog(beforeCatalog));
 const report={baseline:base,mode,scope:'product-only; historical identities/pending retained',moves:moves.length,
  changed:records.length,identitiesSha256:sha(JSON.stringify(identities(JSON.parse(beforeCatalog)))),records,existingMissingUris};
 fs.mkdirSync(path.dirname(abs(reportPath)),{recursive:true});
 fs.writeFileSync(abs(reportPath),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({moves:report.moves,changed:report.changed,report:reportPath}));
}else if(mode==='refresh-catalog'){
 const before=fs.readFileSync(abs(catalogPath),'utf8');
 fs.writeFileSync(abs(catalogPath),refreshCatalog(before));
 console.log('Current Catalog paths/SHA refreshed; original identities protected.');
}else if(mode!=='check')throw Error('Expected apply, check or refresh-catalog');

const issues=[];
const report=fs.existsSync(abs(reportPath))?JSON.parse(fs.readFileSync(abs(reportPath),'utf8')):null;
for(const record of report?.records??[]){
 if(!fs.existsSync(abs(record.to)))issues.push('Missing mapped file: '+record.to);
 else if(sha(fs.readFileSync(abs(record.to)))!==record.afterSha256)issues.push('Unexpected content change: '+record.to);
 if(record.from!==record.to&&fs.existsSync(abs(record.from)))issues.push('Old product file remains: '+record.from);
}
let pages=0,uris=0;
const knownMissing=new Set((report?.existingMissingUris??[]).map(item=>(item.source+'::'+item.target).toLowerCase()));
for(const p of productFiles()){
 if(!productText(p))continue;
 const text=fs.readFileSync(abs(p),'utf8');
 // Native exports and historical source JSON/pending are outside this product mapping.
 for(const [old]of nsPairs)if(text.includes(old))issues.push('Old product namespace: '+p+' '+old);
 for(const [old]of pathPairs){
  if(text.includes(old.slice(product.length)))issues.push('Old mapped resource path: '+p+' '+old);
 }
 if(/HikTextBoxFg|Col_Menubar_VMInfo_/.test(text))issues.push('Old product resource key: '+p);
 if(p.endsWith('.xaml'))pages++;
 for(const match of text.matchAll(packPattern)){
  uris++;
  const target=decodeURIComponent(match[1]);
  if(!fs.existsSync(abs(product+target))&&!knownMissing.has((p+'::'+target).toLowerCase()))issues.push('New missing pack URI: '+p+' -> '+target);
 }
}
const currentIdentities=sha(JSON.stringify(identities(JSON.parse(fs.readFileSync(abs(catalogPath),'utf8')))));
if(report&&report.identitiesSha256!==currentIdentities)issues.push('Catalog identity/history fingerprint changed');
let catalogFiles=0;
const baselineCatalog=JSON.parse(git('show',base+':'+catalogPath).toString('utf8'));
const baselineTree=new Set(git('ls-tree','-r','--name-only','-z',base,'--','src','tools').toString().split('\0').map(p=>p.toLowerCase()));
const preexistingCatalogMissingFiles=[];
const knownCatalogMissing=new Set();
function collectBaselineCatalogMissing(node){
 if(Array.isArray(node)){node.forEach(collectBaselineCatalogMissing);return;}
 if(!node||typeof node!=='object')return;
 const p=(node.localPath??node.path)?.replaceAll?.('\\','/');
 if(p&&/^(src|tools)\//.test(p)&&!baselineTree.has(p.toLowerCase())){
  const mapped=mapText(p);knownCatalogMissing.add(mapped.toLowerCase());
  preexistingCatalogMissingFiles.push({baseline:base,oldPath:p,currentPath:mapped});
 }
 for(const [k,v]of Object.entries(node))if(!protectedKey(k))collectBaselineCatalogMissing(v);
}
for(const [k,v]of Object.entries(baselineCatalog))if(k==='entries'||k.startsWith('current'))collectBaselineCatalogMissing(v);
function checkCurrentCatalog(node){
 if(Array.isArray(node)){node.forEach(checkCurrentCatalog);return;}
 if(!node||typeof node!=='object')return;
 const pk=Object.hasOwn(node,'localPath')?'localPath':Object.hasOwn(node,'path')?'path':null;
 if(pk&&typeof node[pk]==='string'){
  const p=node[pk].replaceAll('\\','/'),hk=pk==='localPath'?'localSha256':'sha256';
  if(/^(src|tools)\//.test(p)){
   if(!fs.existsSync(abs(p))&&!knownCatalogMissing.has(p.toLowerCase()))issues.push('New missing current Catalog file: '+p);
   else if(Object.hasOwn(node,hk)){
    catalogFiles++;
    if(node[hk].toUpperCase()!==sha(fs.readFileSync(abs(p))))issues.push('Current Catalog SHA mismatch: '+p);
   }
  }
 }
 for(const [k,v]of Object.entries(node))if(!protectedKey(k))checkCurrentCatalog(v);
}
const catalog=JSON.parse(fs.readFileSync(abs(catalogPath),'utf8'));
for(const [k,v]of Object.entries(catalog))if(k==='entries'||k.startsWith('current'))checkCurrentCatalog(v);
if(JSON.stringify(identities(baselineCatalog))!==JSON.stringify(identities(catalog)))issues.push('Catalog baseline original/history snapshots differ');
console.log(JSON.stringify({mode,pages,uris,catalogFiles,preexistingCatalogMissingFiles,existingMissingUris:report?.existingMissingUris??[],identityUnchanged:!report||report.identitiesSha256===currentIdentities,issues},null,2));
if(issues.length)process.exitCode=1;
