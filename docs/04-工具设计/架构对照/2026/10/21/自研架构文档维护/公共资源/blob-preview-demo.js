/* Documentation-only synthetic example. No product/VM API, pixel segmentation or performance claim. */
(()=>{'use strict';
const copy=v=>JSON.parse(JSON.stringify(v));
const objects=[{id:1,x:150,y:135,rx:32,ry:26,area:2600},{id:2,x:310,y:120,rx:22,ry:18,area:1240,judgmentPassed:false},{id:3,x:455,y:160,rx:39,ry:24,area:2940},{id:4,x:210,y:285,rx:18,ry:17,area:960},{id:5,x:395,y:285,rx:29,ry:35,area:3190}];
function local(p,r){const a=-r.angle*Math.PI/180,dx=p.x-r.x,dy=p.y-r.y;return{x:dx*Math.cos(a)-dy*Math.sin(a),y:dx*Math.sin(a)+dy*Math.cos(a)};}
function world(p,r){const a=r.angle*Math.PI/180;return{x:r.x+p.x*Math.cos(a)-p.y*Math.sin(a),y:r.y+p.x*Math.sin(a)+p.y*Math.cos(a)};}
function contains(p,r){const q=local(p,r);return Math.abs(q.x)<=r.w/2&&Math.abs(q.y)<=r.h/2;}
function resizeRectangle(original,p,index,axis='corner'){
 const q=local(p,original);let left=-original.w/2,right=original.w/2,top=-original.h/2,bottom=original.h/2;
 if(axis==='corner'){if(index===0||index===3)left=Math.min(q.x,right-8);else right=Math.max(q.x,left+8);if(index===0||index===1)top=Math.min(q.y,bottom-8);else bottom=Math.max(q.y,top+8);}
 else if(index===0)top=Math.min(q.y,bottom-8);else if(index===1)right=Math.max(q.x,left+8);else if(index===2)bottom=Math.max(q.y,top+8);else left=Math.min(q.x,right-8);
 const c=world({x:(left+right)/2,y:(top+bottom)/2},original);return{...original,x:c.x,y:c.y,w:right-left,h:bottom-top};}
function imageToViewport(p,zoom,pan){return{x:320+(p.x-320)*zoom+pan.x,y:210+(p.y-210)*zoom+pan.y};}
function viewportToImage(p,zoom,pan){return{x:(p.x-pan.x-320)/zoom+320,y:(p.y-pan.y-210)/zoom+210};}
function rectangleTriangle(r,side,zoom){const a=r.angle*Math.PI/180,A=6/zoom,anchor=world({x:side*r.w/2,y:r.h*.1},r);return[[A,-A],[0,0],[-A,-A]].map(([u,v])=>({x:anchor.x+u*Math.cos(a)-v*Math.sin(a),y:anchor.y+u*Math.sin(a)+v*Math.cos(a)}));}
function evaluate(rois,settings){const inspect=rois.filter(r=>r.kind==='inspect'),mask=rois.filter(r=>r.kind==='mask');return objects.map(o=>{const eligible=(!inspect.length||inspect.some(r=>contains(o,r)))&&!mask.some(r=>contains(o,r));const passArea=!settings.filter||(o.area>=settings.min&&o.area<=settings.max);return{...o,roiEligible:eligible,eligible:eligible&&passArea,status:!eligible?'ROI/屏蔽排除':!passArea?'面积过滤':o.judgmentPassed===false?'NG':'OK'};});}
const fieldDeclarations={inspect:{id:'InspectionRois',label:'检测区域',allowed:['circle','rotated-rectangle','polygon']},mask:{id:'MaskRois',label:'屏蔽区域',allowed:['circle','rotated-rectangle','polygon']}};
const bindingCatalog={'upstream-detection':[{id:'ref-detection-1',name:'上游检测区域',kind:'inspect',x:300,y:200,w:360,h:240,angle:15}],'upstream-mask':[{id:'ref-mask-1',name:'上游屏蔽区域',kind:'mask',x:455,y:160,w:100,h:90,angle:30}]};
function resolveRois(rois,settings){return['inspect','mask'].flatMap(kind=>settings.fieldModes?.[kind]==='binding'?(bindingCatalog[settings.bindings?.[kind]]||[]).map(r=>({...r,kind,editable:false,reference:settings.bindings[kind]})):rois.filter(r=>r.kind===kind).map(r=>({...r,editable:true})));}
const initial=[{id:'inspect-1',name:'检测1',kind:'inspect',x:310,y:205,w:490,h:290,angle:0},{id:'mask-1',name:'屏蔽1',kind:'mask',x:455,y:160,w:110,h:80,angle:30}];
class Model{
 constructor(){this.committed=copy(initial);this.draft=copy(initial);this.settings={threshold:128,type:'Binary',filter:true,min:1000,max:3000,inputImageSource:'synthetic-camera',fieldModes:{inspect:'drawing',mask:'drawing'},bindings:{inspect:'upstream-detection',mask:'upstream-mask'}};this.committedSettings=copy(this.settings);this.edit=true;this.selected='inspect-1';this.selectedResult=null;this.tool='select';this.kind='inspect';this.zoom=1;this.pan={x:0,y:0};this.source='OverlayImage';this.showContours=true;this.showNumbers=true;this.showAreas=true;this.scene=evaluate(resolveRois(this.committed,this.committedSettings),this.committedSettings);this.pending=null;this.generation=1;this.log='已加载合成场景。先选择ROI或绘制；参数仅提交后生效。';}
 previewRois(){return resolveRois(this.draft,this.settings);}
 activateField(kind){if(!fieldDeclarations[kind])return false;this.kind=kind;this.selected=null;this.tool='select';this.log='公共绘制已切到'+fieldDeclarations[kind].label+'字段；另一字段不同时接收手势。';return true;}
 beginFieldDraw(kind){this.activateField(kind);if(!this.edit||this.settings.fieldModes[kind]!=='drawing'||!this.settings.inputImageSource){this.log='当前字段不可绘制：请打开编辑、选择输入图像并使用绘制模式。';return false;}this.tool='draw';this.log='请在中央预览拖画旋转矩形，回填'+fieldDeclarations[kind].label+'。';return true;}
 setFieldMode(kind,mode){this.activateField(kind);this.settings.fieldModes[kind]=mode;this.log=mode==='binding'?'订阅参数几何来源：保存引用，预览只读，不转为本地常量。':'恢复本地绘制字段；本地几何列表保留。';}
 dirty(){return JSON.stringify(this.draft)!==JSON.stringify(this.committed)||JSON.stringify(this.settings)!==JSON.stringify(this.committedSettings);}
 commit(){if(!this.settings.inputImageSource||Object.keys(this.settings.fieldModes).some(k=>this.settings.fieldModes[k]==='binding'&&!bindingCatalog[this.settings.bindings[k]])){this.log='参数不可提交：输入或订阅来源缺失。';return false;}this.committed=copy(this.draft);this.committedSettings=copy(this.settings);this.log='已提交：参数列表已替换；可作为一次撤销的原子动作（本演示不提供完整历史栈）。';this.releasePending();return true;}
 cancel(){this.draft=copy(this.committed);this.settings=copy(this.committedSettings);this.log='已取消：恢复编辑前已提交参数。';this.releasePending();}
 close(){this.cancel();this.edit=false;this.selected=null;this.log='已结束本工具编辑：隐藏操作点，保留只读结果。';}
 open(){this.edit=true;this.draft=copy(this.committed);this.settings=copy(this.committedSettings);this.selected=this.draft[0]?.id||null;this.log='打开自己的编辑会话；结果层没有被清空。';}
 run(forceHandoff=false){const next=evaluate(resolveRois(this.committed,this.committedSettings),this.committedSettings);if(this.edit&&(this.dirty()||forceHandoff)){this.pending=next;this.log='新结果已缓存：编辑基准保持；提交或取消后才交接显示。';}else{this.scene=next;this.generation++;this.log='模拟运行完成：仅中心点ROI资格+面积过滤，不是阈值像素分割。';}}
 releasePending(){if(this.pending){this.scene=this.pending;this.pending=null;this.generation++;this.log+=' 已交接缓存的新结果。';}}
}
const api={Model,contains,local,world,resizeRectangle,rectangleTriangle,fieldDeclarations,bindingCatalog,resolveRois,imageToViewport,viewportToImage,evaluate,objects};if(typeof module!=='undefined'&&module.exports)module.exports=api;
if(typeof document==='undefined')return;
const root=document.getElementById('blob-preview-demo');if(!root)return;const S='http://www.w3.org/2000/svg',m=new Model();
const el=id=>root.querySelector('#bpd-'+id),svg=el('canvas');let gesture=null,nextId=3;
function node(tag,attrs={},text,parent){const n=document.createElementNS(S,tag);for(const[k,v]of Object.entries(attrs))n.setAttribute(k,String(v));if(text!==undefined)n.textContent=text;if(parent)parent.appendChild(n);return n;}
function eventPoint(e){const b=svg.getBoundingClientRect(),scale=Math.min(b.width/640,b.height/420),ox=(b.width-640*scale)/2,oy=(b.height-420*scale)/2;return{x:(e.clientX-b.left-ox)/scale,y:(e.clientY-b.top-oy)/scale};}
function imagePoint(e){return viewportToImage(eventPoint(e),m.zoom,m.pan);}
function finishGesture(rollback=false){const old=gesture;gesture=null;if(old&&rollback){if(old.original){const i=m.draft.findIndex(r=>r.id===old.id);m.draft[i]=old.original;}else if(old.action==='draw')m.draft=m.draft.filter(r=>r.id!==old.id);}if(old&&old.pointerId!==undefined&&svg.hasPointerCapture(old.pointerId))svg.releasePointerCapture(old.pointerId);}
function showParameters(){return m.draft.map(r=>`${r.name}: 中心(${r.x.toFixed(1)},${r.y.toFixed(1)}) 宽${r.w.toFixed(1)} 高${r.h.toFixed(1)} 角${r.angle.toFixed(1)}°`).join('\n');}
function paint(){
 svg.replaceChildren();const defs=node('defs',{},undefined,svg),clip=node('clipPath',{id:'bpd-clip'},undefined,defs);node('rect',{width:640,height:420},undefined,clip);
 const all=node('g',{'clip-path':'url(#bpd-clip)'},undefined,svg),g=node('g',{transform:`translate(${m.pan.x} ${m.pan.y}) translate(320 210) scale(${m.zoom}) translate(-320 -210)`},undefined,all);
 const image=node('g',{'data-layer':'image'},undefined,g),results=node('g',{'data-layer':'read-only-results'},undefined,g),interaction=node('g',{'data-layer':'owner-edit'},undefined,g);
 const binary=m.source==='BinaryMask';node('rect',{width:640,height:420,fill:binary?'#14191c':'#3d4951'},undefined,image);node('rect',{x:60,y:50,width:520,height:310,rx:24,fill:binary?'#080b0d':'#89959a',stroke:binary?'#30383d':'#c0c9cd','stroke-width':2/m.zoom},undefined,image);
 for(const o of objects)node('ellipse',{cx:o.x,cy:o.y,rx:o.rx,ry:o.ry,fill:binary?'#ffffff':m.source==='GrayImage'?'#cbd0d3':'#afbfc5',stroke:binary?'none':'#53636c','stroke-width':1/m.zoom},undefined,image);
 if(m.showContours)for(const o of m.scene.filter(o=>o.eligible)){const selected=m.selectedResult===o.id;node('ellipse',{cx:o.x,cy:o.y,rx:o.rx+3,ry:o.ry+3,fill:'none',stroke:selected?'#fff266':o.status==='NG'?'#ff6269':'#40d58d','stroke-width':(selected?3:1.6)/m.zoom,'data-result':o.id,tabindex:0},undefined,results);}
 if(m.showNumbers||m.showAreas)for(const o of m.scene.filter(o=>o.eligible))node('text',{x:o.x-o.rx,y:o.y-o.ry-8/m.zoom,fill:'#f8faff','font-size':12/m.zoom,'paint-order':'stroke',stroke:'#20303b','stroke-width':3/m.zoom,'data-result':o.id},[m.showNumbers?`#${o.id}`:'',m.showAreas?`面积${o.area}`:''].filter(Boolean).join('\n'),results);
 // Label anchors above each synthetic blob are proposed display policy, not current product behavior.
 if(m.edit)for(const r of m.previewRois()){const selected=r.id===m.selected&&r.editable!==false,a=r.angle*Math.PI/180,col='#0f6af3';
 const pts=[[-r.w/2,-r.h/2],[r.w/2,-r.h/2],[r.w/2,r.h/2],[-r.w/2,r.h/2]].map(p=>world({x:p[0],y:p[1]},r));
 node('polygon',{points:pts.map(p=>`${p.x},${p.y}`).join('\n'),fill:'#00000001',stroke:col,'stroke-width':(selected?2:1)/m.zoom,'data-roi':r.id,'data-action':r.editable===false?'readonly':'move',tabindex:0},undefined,interaction);
 for(const side of [-1,1]){const tri=rectangleTriangle(r,side,m.zoom);node('polygon',{points:tri.map(p=>`${p.x},${p.y}`).join('\n'),fill:col,'pointer-events':'none'},undefined,interaction);} if(selected){const hs=8/m.zoom;for(let i=0;i<4;i++){const p=pts[i];node('rect',{x:p.x-hs/2,y:p.y-hs/2,width:hs,height:hs,fill:'#f76900',stroke:'#f76900','stroke-width':1/m.zoom,'data-roi':r.id,'data-action':'resize','data-index':i,transform:`rotate(${r.angle} ${p.x} ${p.y})`},undefined,interaction);const mid={x:(p.x+pts[(i+1)%4].x)/2,y:(p.y+pts[(i+1)%4].y)/2};node('rect',{x:mid.x-hs/2,y:mid.y-hs/2,width:hs,height:hs,fill:'transparent',stroke:'none','data-roi':r.id,'data-action':'resize','data-index':i,'data-axis':'edge',transform:`rotate(${r.angle} ${mid.x} ${mid.y})`},undefined,interaction);if(i===1)node('path',{d:'M-7,-14 A12,12 0 0 1 10,-5',fill:'none',stroke:'#f76900','stroke-width':2,transform:`translate(${p.x} ${p.y}) rotate(${r.angle}) scale(${1/m.zoom})`,'pointer-events':'none'},undefined,interaction);const dx=p.x-r.x,dy=p.y-r.y,len=Math.hypot(dx,dy)||1,rot={x:p.x+dx/len*18/m.zoom,y:p.y+dy/len*18/m.zoom};node('circle',{cx:rot.x,cy:rot.y,r:7/m.zoom,fill:'transparent',stroke:'none','stroke-width':1/m.zoom,'data-roi':r.id,'data-action':'rotate'},undefined,interaction);}
}
 }
 for(const kind of ['inspect','mask']){const list=el(kind+'-rois');list.replaceChildren();const bound=m.settings.fieldModes[kind]==='binding';el(kind+'-mode-drawing').checked=!bound;el(kind+'-mode-binding').checked=bound;el(kind+'-binding-panel').hidden=!bound;el(kind+'-drawing-panel').hidden=bound;el(kind+'-binding').value=m.settings.bindings[kind];el(kind+'-rect').disabled=!m.edit||bound||!m.settings.inputImageSource;
 const rows=m.previewRois().filter(r=>r.kind===kind);for(const r of rows){const b=document.createElement('button');b.type='button';b.textContent=`${r.name} · ${r.angle.toFixed(0)}°${bound?'（来源只读）':''}`;b.className=r.id===m.selected?'active':'';b.disabled=!m.edit||bound;b.addEventListener('click',()=>{m.activateField(kind);m.selected=r.id;paint();});list.appendChild(b);}
 el(kind+'-values').textContent=rows.map(r=>`中心 ${r.x.toFixed(1)}, ${r.y.toFixed(1)}
宽 ${r.w.toFixed(1)} / 高 ${r.h.toFixed(1)} / 角 ${r.angle.toFixed(1)}°`).join('\n');el(kind+'-field').classList.toggle('bpd-field-active',m.kind===kind&&m.edit);}
 el('service').textContent=`公共绘制 → ${fieldDeclarations[m.kind].label} → ${m.settings.fieldModes[m.kind]==='binding'?'订阅来源只读':'本字段参数回填'}（检测/屏蔽互斥）`;
 el('input-source').value=m.settings.inputImageSource;
 el('results').replaceChildren();for(const o of m.scene){const b=document.createElement('button');b.type='button';b.textContent=`${o.eligible?'检测结果':'排除统计'} #${o.id} · ${o.status} · 面积 ${o.area}`;b.className=m.selectedResult===o.id?'active':'';b.addEventListener('click',()=>{m.selectedResult=o.id;paint();});el('results').appendChild(b);}
 el('status').textContent=`显示源 ${{GrayImage:'原始灰度图',BinaryMask:'预制二值图',OverlayImage:'合成叠加图'}[m.source]} / 场景${m.generation} / ${m.edit?'编辑打开':'编辑已关闭'} / ${m.pending?'新结果等待交接':'显示已同步'} · ${m.log}`;
 el('draft').textContent=showParameters();el('committed').textContent=m.committed.map(r=>`${r.name}: (${r.x.toFixed(1)},${r.y.toFixed(1)}) ${r.w.toFixed(1)}×${r.h.toFixed(1)} ${r.angle.toFixed(1)}°`).join('\n');
 el('zoom-label').textContent=`${Math.round(m.zoom*100)}%`;el('threshold').value=m.settings.threshold;el('min').value=m.settings.min;el('max').value=m.settings.max;el('filter').checked=m.settings.filter;
 for(const id of ['select','remove','commit','cancel','confirm'])el(id).disabled=!m.edit;
 }
svg.addEventListener('pointerdown',e=>{if(e.button!==0&&e.button!==1)return;const roiId=e.target.getAttribute?.('data-roi'),result=e.target.getAttribute?.('data-result');if(result&&!roiId){m.selectedResult=+result;paint();return;}const p=imagePoint(e);
 if(m.tool==='pan'||e.button===1){gesture={action:'pan',p:eventPoint(e),pan:{...m.pan}};}
 else if(m.edit&&m.tool==='draw'&&m.settings.inputImageSource&&m.settings.fieldModes[m.kind]==='drawing'){const r={id:`roi-${nextId}`,name:`${m.kind==='mask'?'屏蔽':'检测'}${nextId++}`,kind:m.kind,x:p.x,y:p.y,w:1,h:1,angle:0};m.draft.push(r);m.selected=r.id;gesture={action:'draw',id:r.id,start:p};}
 else if(m.edit&&roiId){if(e.target.getAttribute('data-action')==='readonly'){m.log='该区域来自订阅引用，只读预览；不转成本地几何。';paint();return;}const r=m.draft.find(r=>r.id===roiId);if(!r||m.settings.fieldModes[r.kind]!=='drawing')return;m.kind=r.kind;m.selected=roiId;gesture={action:e.target.getAttribute('data-action')||'move',id:roiId,start:p,original:copy(r),index:+e.target.getAttribute('data-index'),axis:e.target.getAttribute('data-axis')||'corner',startTheta:Math.atan2(p.y-r.y,p.x-r.x)};}
 if(gesture){gesture.pointerId=e.pointerId;svg.setPointerCapture(e.pointerId);e.preventDefault();paint();}});
svg.addEventListener('pointermove',e=>{if(!gesture)return;const p=imagePoint(e),r=m.draft.find(r=>r.id===gesture.id);
 if(gesture.action==='pan'){const q=eventPoint(e);m.pan={x:gesture.pan.x+q.x-gesture.p.x,y:gesture.pan.y+q.y-gesture.p.y};}
 else if(gesture.action==='draw'){r.x=(gesture.start.x+p.x)/2;r.y=(gesture.start.y+p.y)/2;r.w=Math.max(8,Math.abs(p.x-gesture.start.x));r.h=Math.max(8,Math.abs(p.y-gesture.start.y));}
 else if(gesture.action==='move'){r.x=gesture.original.x+p.x-gesture.start.x;r.y=gesture.original.y+p.y-gesture.start.y;}
 else if(gesture.action==='resize')Object.assign(r,resizeRectangle(gesture.original,p,gesture.index,gesture.axis));
 else if(gesture.action==='rotate')r.angle=gesture.original.angle+(Math.atan2(p.y-r.y,p.x-r.x)-gesture.startTheta)*180/Math.PI;
 m.log='参数草稿改变，已提交值未改。';paint();});
svg.addEventListener('pointerup',e=>{if(!gesture)return;finishGesture(false);if(m.tool==='draw')m.tool='select';paint();});
svg.addEventListener('pointercancel',()=>{finishGesture(true);m.log='手势取消：恢复拖动前几何并释放捕获。';paint();});
svg.addEventListener('lostpointercapture',()=>{if(gesture){if(gesture.original){const i=m.draft.findIndex(r=>r.id===gesture.id);m.draft[i]=gesture.original;}else if(gesture.action==='draw')m.draft=m.draft.filter(r=>r.id!==gesture.id);gesture=null;m.log='捕获丢失：撤回本次手势草稿。';paint();}});
svg.addEventListener('wheel',e=>{e.preventDefault();m.zoom=Math.min(4,Math.max(.25,m.zoom*(e.deltaY<0?1.15:1/1.15)));paint();},{passive:false});
root.addEventListener('keydown',e=>{if(e.key==='Escape'&&m.edit){finishGesture(true);m.cancel();paint();}});
const on=(id,fn)=>el(id).addEventListener('click',()=>{finishGesture(id==='cancel'||id==='close');fn();paint();});
on('select',()=>{m.tool='select';});on('pan',()=>{m.tool='pan';});on('remove',()=>{m.draft=m.draft.filter(r=>r.id!==m.selected);m.selected=null;});on('commit',()=>m.commit());on('cancel',()=>m.cancel());on('confirm',()=>{if(m.commit())m.close();});on('close',()=>m.close());on('open',()=>m.open());on('run',()=>{if(m.commit())m.run();});on('handoff',()=>m.run(true));on('reset-view',()=>{m.zoom=1;m.pan={x:0,y:0};});
for(const id of ['source'])el(id).addEventListener('change',()=>{m[id]=el(id).value;paint();});
for(const id of ['threshold','min','max','filter'])el(id).addEventListener('change',()=>{m.settings[id]=id==='filter'?el(id).checked:+el(id).value;m.log='参数仅改草稿；Threshold在此不是像素分割计算。';paint();});
el('contours').addEventListener('change',()=>{m.showContours=el('contours').checked;paint();});el('numbers').addEventListener('change',()=>{m.showNumbers=el('numbers').checked;paint();});el('areas').addEventListener('change',()=>{m.showAreas=el('areas').checked;paint();});
for(const kind of ['inspect','mask']){on(kind+'-rect',()=>m.beginFieldDraw(kind));for(const mode of ['drawing','binding'])el(kind+'-mode-'+mode).addEventListener('change',()=>{finishGesture(true);m.setFieldMode(kind,mode);paint();});el(kind+'-binding').addEventListener('change',()=>{finishGesture(true);m.settings.bindings[kind]=el(kind+'-binding').value;m.activateField(kind);paint();});}
el('input-source').addEventListener('change',()=>{finishGesture(true);m.settings.inputImageSource=el('input-source').value;paint();});
for(const b of root.querySelectorAll('[data-bpd-tab]'))b.addEventListener('click',()=>{const page=b.getAttribute('data-bpd-tab');for(const p of root.querySelectorAll('[data-bpd-page]'))p.hidden=p.getAttribute('data-bpd-page')!==page;for(const k of root.querySelectorAll('[data-bpd-tab]'))k.setAttribute('aria-selected',k===b?'true':'false');});
paint();root.dataset.ready='true';
})();
