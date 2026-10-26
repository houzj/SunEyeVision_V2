/* VM ColorPickerControlView: anchored Default/More palette, not a HEX modal.
 * ImageViewConfig_subContent: OK, NG, opacity, optional stroke thickness.
 * SourceAdvancedColorPicker: original image pixels + linear-scRGB brightness.
 * All changes below belong to the browser draft; no HALCON execution.
 */
(function(){
 'use strict';
 const srgbToLinear=v=>{v/=255;return v<=.04045?v/12.92:Math.pow((v+.055)/1.055,2.4);};
 const linearToSrgb=v=>Math.round(255*(v<=.0031308?12.92*v:1.055*Math.pow(v,1/2.4)-.055));
 function brightness(rgb,value){return rgb.map(v=>{const c=srgbToLinear(v);return linearToSrgb(value>=.5?c+(1-c)*(value-.5)*2:c*value*2);});}
 function parseRgb(hex){return [1,3,5].map(i=>parseInt(hex.slice(i,i+2),16));}
 const toHex=rgb=>'#'+rgb.map(v=>v.toString(16).padStart(2,'0')).join('');
 function position(anchor,w,h,screenW,screenH){return {left:Math.max(8,Math.min(anchor.left,screenW-w-8)),top:Math.max(8,anchor.bottom+h<=screenH-8?anchor.bottom:anchor.top-h)};}
 if(typeof module!=='undefined'&&module.exports)module.exports={brightness,parseRgb,toHex,position};
 if(typeof document==='undefined')return;
 const data=window.VMColorEvidence;
 if(!data)throw new Error('Missing migrated VM palette/pixel evidence');
 const $=id=>document.getElementById(id);
 const stylePanel=document.createElement('div');stylePanel.className='vm-style-popup';stylePanel.hidden=true;stylePanel.setAttribute('role','dialog');stylePanel.setAttribute('aria-label','图形显示样式');
 const palette=document.createElement('div');palette.className='vm-color-popup';palette.hidden=true;palette.setAttribute('role','dialog');palette.setAttribute('aria-label','颜色选择');
 document.body.append(stylePanel,palette);
 let styleTarget=null,colorAnchor=null,styleAnchor=null,currentHex='#000000',selected=null;
 let chartBase=[128,128,128],chartPoint=null;
 const pixels=Uint8Array.from(atob(data.rgbBase64),c=>c.charCodeAt(0));
 const textTargets=new Set(['okColor','ngColor']);
 function draftChanged(){ $('draft').textContent='样式草稿已修改';paintVisuals(); }
 function styleFor(name){const s=visualStyles[name];return {color:s.color,ngColor:s.ngColor||'#ff0000',opacity:s.opacity??1,width:s.width>=1?s.width:1};}
 function closeColor(){palette.hidden=true;colorAnchor?.setAttribute('aria-expanded','false');selected=null;}
 function closeAll(){closeColor();window.vmNumericInteraction?.dispose(stylePanel);stylePanel.hidden=true;styleAnchor?.setAttribute('aria-expanded','false');}
 function place(panel,anchor,w,h,offset=0){const r=anchor.getBoundingClientRect();const p=position({...r,left:r.left+offset,top:r.top,bottom:r.bottom},w,h,innerWidth,innerHeight);panel.style.left=p.left+'px';panel.style.top=p.top+'px';}
 function valueButton(hex,label){const b=document.createElement('button');b.type='button';b.className='vm-color-value';b.setAttribute('aria-label',label);b.setAttribute('aria-haspopup','dialog');b.setAttribute('aria-expanded','false');const chip=document.createElement('i');const rgb=document.createElement('span');b.append(chip,rgb);setButton(b,hex);return b;}
 function setButton(b,hex){b.querySelector('i').style.backgroundColor=hex;b.querySelector('span').textContent=parseRgb(hex).join(', ');}
 function selectColor(hex,close){currentHex=hex;if(selected)selected(hex);palette.querySelectorAll('[data-vm-color]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.vmColor.toLowerCase()===hex.toLowerCase())));if(close)closeColor();}
 function paletteGrid(colors,first){const grid=document.createElement('div');grid.className='vm-palette-grid'+(first?' vm-palette-top':'');for(const color of colors){const b=document.createElement('button');b.type='button';b.dataset.vmColor=color;b.style.backgroundColor=color;b.title=parseRgb(color).join(', ');b.setAttribute('aria-label','颜色 '+b.title);b.setAttribute('aria-pressed',String(currentHex.toLowerCase()===color.toLowerCase()));b.onclick=()=>selectColor(color,true);grid.append(b);}return grid;}
 function showPaletteTab(tab){
  palette.querySelectorAll('[data-vm-color-tab]').forEach(b=>b.setAttribute('aria-selected',String(b.dataset.vmColorTab===tab)));
  palette.querySelector('.vm-default-colors').hidden=tab!=='default';palette.querySelector('.vm-more-colors').hidden=tab!=='more';
  if(colorAnchor)place(palette,colorAnchor,206,tab==='default'?232:204);
 }
 function buildPalette(){
  palette.replaceChildren();const tabs=document.createElement('div');tabs.className='vm-color-tabs';tabs.setAttribute('role','tablist');
  for(const [key,label] of [['default','默认'],['more','更多']]){const b=document.createElement('button');b.type='button';b.dataset.vmColorTab=key;b.textContent=label;b.setAttribute('role','tab');b.onclick=()=>showPaletteTab(key);tabs.append(b);}
  palette.append(tabs);
  const standard=document.createElement('div');standard.className='vm-default-colors';
  for(const [label,rows] of [['主题颜色',data.rows.slice(0,-1)],['标准颜色',data.rows.slice(-1)]]){const h=document.createElement('div');h.className='vm-palette-heading';h.textContent=label;standard.append(h);rows.forEach((row,index)=>standard.append(paletteGrid(row,index===0)));}
  const more=document.createElement('div');more.className='vm-more-colors';more.hidden=true;
  const chart=document.createElement('div');chart.className='vm-color-chart';chart.tabIndex=0;chart.setAttribute('role','slider');chart.setAttribute('aria-label','色谱，左右调整色相，上下调整饱和度');
  const img=document.createElement('img');img.src=data.chartUrl;img.alt='迁入的VM原色谱';img.draggable=false;
  const marker=document.createElement('i');marker.className='vm-color-marker';marker.hidden=true;chart.append(img,marker);
  const bright=document.createElement('input');bright.className='vm-color-brightness';bright.type='range';bright.min='0';bright.max='1';bright.step='.01';bright.value='.5';bright.setAttribute('aria-label','亮度');
  function setBase(x,y){x=Math.max(0,Math.min(137,Math.round(x)));y=Math.max(0,Math.min(137,Math.round(y)));const offset=(y*data.width+x)*3;chartBase=[pixels[offset],pixels[offset+1],pixels[offset+2]];chartPoint={x,y};marker.hidden=false;marker.style.left=x+'px';marker.style.top=y+'px';bright.value='.5';bright.style.setProperty('--vm-chart-base',toHex(chartBase));selectColor(toHex(chartBase),false);}
  function point(event){const r=chart.getBoundingClientRect();setBase((event.clientX-r.left)*138/r.width,(event.clientY-r.top)*138/r.height);}
  chart.addEventListener('pointerdown',event=>{if(event.button!==0)return;event.preventDefault();chart.focus({preventScroll:true});chart.setPointerCapture(event.pointerId);point(event);});
  chart.addEventListener('pointermove',event=>{if(chart.hasPointerCapture(event.pointerId))point(event);});
  chart.addEventListener('pointerup',event=>{if(chart.hasPointerCapture(event.pointerId))chart.releasePointerCapture(event.pointerId);});
  chart.addEventListener('keydown',event=>{const dx=event.key==='ArrowRight'?1:event.key==='ArrowLeft'?-1:0,dy=event.key==='ArrowDown'?1:event.key==='ArrowUp'?-1:0;if(dx||dy){event.preventDefault();const p=chartPoint||{x:69,y:69};setBase(p.x+dx,p.y+dy);}});
  bright.oninput=()=>selectColor(toHex(brightness(chartBase,Number(bright.value))),false);
  more.append(chart,bright);palette.append(standard,more);
 }
 buildPalette();
 function openColor(anchor,hex,onChange){if(!palette.hidden&&colorAnchor===anchor){closeColor();return;}closeColor();colorAnchor=anchor;currentHex=hex;selected=onChange;chartBase=parseRgb(hex);chartPoint=null;palette.querySelector('.vm-color-marker').hidden=true;const b=palette.querySelector('.vm-color-brightness');b.value='.5';b.style.setProperty('--vm-chart-base',hex);palette.hidden=false;anchor.setAttribute('aria-expanded','true');showPaletteTab('default');palette.querySelectorAll('[data-vm-color]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.vmColor.toLowerCase()===hex.toLowerCase())));}
 function updateStyle(name,key,value){visualStyles[name]={...styleFor(name),[key]:value};draftChanged();}
 function numericRow(label,value,min,max,step,key){
  const row=document.createElement('div');row.className='vm-style-row';const caption=document.createElement('span');caption.textContent=label;
  const shell=document.createElement('span');shell.className='scalar-setting';shell.dataset.scalar='vmStyle'+key;
  const numeric=document.createElement('span');numeric.className='numeric';
  const input=document.createElement('input');input.id='vmStyle'+key;input.type='number';input.min=min;input.max=max;input.step=step;input.value=Number(value).toFixed(2);input.setAttribute('aria-label',label);
  const steppers=document.createElement('span');steppers.className='steppers';for(const [direction,symbol]of [[1,'▴'],[-1,'▾']]){const b=document.createElement('button');b.type='button';b.dataset.step=String(direction);b.dataset.for=input.id;b.textContent=symbol;b.setAttribute('aria-label',(direction===1?'增加':'减少')+label);steppers.append(b);}numeric.append(input,steppers);
  const popup=document.createElement('span');popup.className='value-slider';popup.hidden=true;const slider=document.createElement('input');slider.type='range';slider.min=min;slider.max=max;slider.step=step;slider.value=value;slider.dataset.slider=input.id;slider.setAttribute('aria-label',label+'滑条');popup.append(slider);shell.append(numeric,popup);
  const apply=report=>{const v=Number(input.value);input.setCustomValidity(input.value!==''&&Number.isFinite(v)&&v>=min&&v<=max?'':`${label}须在${min}—${max}之间`);if(input.checkValidity()){updateStyle(styleTarget,key,v);return true;}if(report)input.reportValidity();return false;};
  input.oninput=()=>apply(false);input.onchange=()=>apply(false);input.onblur=()=>{if(apply(false))input.value=Number(input.value).toFixed(2);};
  row.append(caption,shell);return row;
 }
 function openStyle(target,anchor){if(!stylePanel.hidden&&styleAnchor===anchor){closeAll();return;}closeAll();window.vmNumericInteraction?.dispose(stylePanel);styleTarget=target;styleAnchor=anchor;stylePanel.replaceChildren();const state=styleFor(target);for(const [label,key]of [['OK颜色','color'],['NG颜色','ngColor']]){const row=document.createElement('div');row.className='vm-style-row';const text=document.createElement('span');text.textContent=label;const b=valueButton(state[key],label);b.onclick=()=>openColor(b,styleFor(target)[key],hex=>{setButton(b,hex);updateStyle(target,key,hex);});row.append(text,b);stylePanel.append(row);}stylePanel.append(numericRow('透明度',state.opacity,0,1,.01,'opacity'),numericRow('粗细',state.width,1,10,1,'width'));window.vmNumericInteraction?.mount(stylePanel);stylePanel.hidden=false;anchor.setAttribute('aria-expanded','true');place(stylePanel,anchor,260,150,-90);}
 document.querySelectorAll('.window .colorbox').forEach(box=>{const input=box.querySelector('input[type=color]');if(!input)return;const chip=document.createElement('i');chip.className='vm-text-color-chip';chip.style.backgroundColor=input.value;box.insertBefore(chip,input);box.tabIndex=0;box.setAttribute('role','button');box.setAttribute('aria-label',input.id==='okColor'?'OK颜色':'NG颜色');box.setAttribute('aria-haspopup','dialog');const show=()=>{stylePanel.hidden=true;openColor(box,input.value,hex=>{input.value=hex;chip.style.backgroundColor=hex;input.nextElementSibling.textContent=parseRgb(hex).join(', ');draftChanged();});};box.onclick=event=>{event.preventDefault();event.stopPropagation();show();};input.onclick=event=>{event.preventDefault();event.stopPropagation();show();};box.onkeydown=event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();show();}};});
 openVisual=function(target){if(textTargets.has(target)){$(target).parentElement.click();return;}const anchor=document.querySelector('[data-style="'+target+'"]');if(anchor)openStyle(target,anchor);};
 const originalPaint=paintVisuals;
 paintVisuals=function(){originalPaint();document.querySelectorAll('.window .colorbox').forEach(box=>{const input=box.querySelector('input[type=color]'),chip=box.querySelector('.vm-text-color-chip');if(input&&chip)chip.style.backgroundColor=input.value;});for(const[name,id]of Object.entries({Blob:'highlight',检测区域:'roi',屏蔽区域:'maskShape',质心:'centroidShape'})){const state=styleFor(name);$(id).style.opacity=String(state.opacity);$(id).querySelectorAll('rect,circle,ellipse,path,polygon,line,polyline').forEach(shape=>{shape.setAttribute('vector-effect','non-scaling-stroke');shape.setAttribute('stroke-width',state.width);});}};
 const originalClose=closeEditor;closeEditor=function(discard){closeAll();originalClose(discard);};
 document.addEventListener('pointerdown',event=>{if(palette.hidden&&stylePanel.hidden)return;if(palette.contains(event.target)||colorAnchor?.contains(event.target)||styleAnchor?.contains(event.target))return;if(stylePanel.contains(event.target)){closeColor();return;}closeAll();},true);
 document.addEventListener('keydown',event=>{if(event.key!=='Escape')return;if(!palette.hidden){event.preventDefault();closeColor();}else if(!stylePanel.hidden){event.preventDefault();closeAll();}},true);
 document.querySelectorAll('[data-tab]').forEach(button=>button.addEventListener('click',closeAll));
 window.addEventListener('resize',closeAll);document.getElementById('pages').addEventListener('scroll',closeAll);
 paintVisuals();
})();
