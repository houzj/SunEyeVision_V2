/* Browser translation of NumericControl/SliderArrange, not a WPF equivalence claim.
 * Evidence: NumericControl.Text uses LostFocus; ForceUpdateSource on popup close.
 * SliderArrange uses Slider.IncreaseLarge/DecreaseLarge with LargeChange=tick,
 * Thumb_MouseLeftButtonUp commits, and ClipSilder divides hit regions at midpoint.
 */
(function () {
  'use strict';
  const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));
  function snap(v, min, max, step) {
    return clamp(Number((min + Math.round((v - min) / step) * step).toPrecision(14)), min, max);
  }
  function endpoint(x, low, high) { return x <= (low + high) / 2 ? 0 : 1; }
  function dragValue(x, startX, startValue, width, min, max, step) {
    return snap(startValue + (x - startX) / Math.max(1, width - 10) * (max - min), min, max, step);
  }
  const api = {clamp, snap, endpoint, dragValue};
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof document === 'undefined') return;
  const groups = [], byInput = new Map();
  let active = null, gesture = null, repeat = null;
  function finishRepeat() { clearTimeout(repeat); repeat = null; }
  function format(input, value) {
    const decimals = Number(input.dataset.vmDecimals || 0);
    return decimals ? value.toFixed(decimals) : String(Math.round(value));
  }
  function bounds(input, group, index) {
    return [index === 1 ? group.values[0] : group.min,
      group.inputs.length === 2 && index === 0 ? group.values[1] : group.max];
  }
  function emit(input) { input.dispatchEvent(new Event('change', {bubbles:true})); }
  function paint(group, values = group.values) {
    group.sliders.forEach((slider, i) => {
      slider.value = values[i];
      slider.title = format(group.inputs[i], values[i]);
    });
    const percent = values.map(v => 100 * (v - group.min) / (group.max - group.min));
    group.popup.style.setProperty('--vm-low', (group.inputs.length === 2 ? percent[0] : 0) + '%');
    group.popup.style.setProperty('--vm-high', (group.inputs.length === 2 ? percent[1] : percent[0]) + '%');
    if (group.inputs.length === 2) {
      const middle = (percent[0] + percent[1]) / 2;
      group.sliders[0].style.clipPath = `inset(0 ${100-middle}% 0 0)`;
      group.sliders[1].style.clipPath = `inset(0 0 0 ${middle}%)`;
    }
  }
  function commit(input, group, index) {
    const value = Number(input.value), [min, max] = bounds(input, group, index);
    const valid = input.value !== '' && Number.isFinite(value) && value >= min && value <= max;
    input.setCustomValidity(valid ? '' : `请输入 ${min} 到 ${max} 之间的数值`);
    input.setAttribute('aria-invalid', String(!valid));
    if (!valid) return false; // RangeRule retains invalid text rather than silently clamping it.
    group.values[index] = value;
    paint(group); emit(input); return true;
  }
  function close(group) {
    if (!group) return;
    const draft = validateDraft(group, false);
    if(draft){group.values=draft;paint(group);group.inputs.forEach(emit);}
    group.popup.hidden = true;
    if (active === group) active = null;
    finishRepeat();
  }
  function closeAll() { groups.filter(group=>!group.popup.hidden || active===group).forEach(close); }
  function validateDraft(group, report) {
    const draft=group.inputs.map(input=>Number(input.value));let firstInvalid=null;
    group.inputs.forEach((input,index)=>{
      const lo=index===1?draft[0]:group.min;
      const hi=group.inputs.length===2&&index===0?draft[1]:group.max;
      const valid=input.value!==''&&Number.isFinite(draft[index])&&draft[index]>=lo&&draft[index]<=hi;
      input.setCustomValidity(valid?'':`请输入 ${lo} 到 ${hi} 之间的数值，下限不能超过上限`);
      input.setAttribute('aria-invalid',String(!valid));if(!valid&&!firstInvalid)firstInvalid=input;
    });
    if(firstInvalid){if(report)firstInvalid.reportValidity();return null;}return draft;
  }
  function subscribed(group) {
    return group.inputs.some(input => document.getElementById(input.id + 'Subscribed')?.checked);
  }
  function open(group) {
    if (subscribed(group)) return;
    if (active && active !== group) close(active);
    const draft=validateDraft(group,false);if(draft)group.values=draft;
    group.popup.hidden = false; active = group; paint(group);
  }
  function set(group, index, value, notify) {
    const [min, max] = bounds(group.inputs[index], group, index);
    group.values[index] = clamp(value, min, max);
    const input = group.inputs[index]; input.value = format(input, group.values[index]);
    input.setCustomValidity(''); input.setAttribute('aria-invalid', 'false');
    input.dispatchEvent(new Event('input', {bubbles:true}));
    paint(group); if (notify) emit(input);
  }
  function move(group, index, direction, notify = true) {
    set(group, index, group.values[index] + direction * group.step, notify);
  }
  function mount(root=document){
  for (const popup of root.querySelectorAll('.value-slider')) {
    if(popup.dataset.vmNumericMounted)continue;
    popup.dataset.vmNumericMounted='true';
    const shell = popup.closest('.range-pair') || popup.closest('[data-scalar]');
    const inputs = [...shell.querySelectorAll('.numeric input')];
    const sliders = [...popup.querySelectorAll('input[type=range]')];
    const group = {shell, popup, inputs, sliders, values:inputs.map(x => Number(x.value)),
      min:Number(sliders[0].min), max:Number(sliders[0].max), step:Number(sliders[0].step) || 1};
    groups.push(group);
    inputs.forEach((input, index) => {
      byInput.set(input, {group,index});
      input.step = group.step;
      input.dataset.vmDecimals = String((String(group.step).split('.')[1] || '').length);
      input.addEventListener('focus', () => open(group));
      input.addEventListener('click', () => open(group));
      input.addEventListener('blur', () => commit(input, group, index));
      input.addEventListener('input', () => {
        input.setCustomValidity(''); input.removeAttribute('aria-invalid');
        // Keep the visible text/thumbs in sync without issuing a committed change.
        const draft = inputs.map(field => Number(field.value));
        if(inputs.every(field=>field.value!=='') && draft.every(v=>Number.isFinite(v)&&v>=group.min&&v<=group.max)
          && (draft.length===1 || draft[0]<=draft[1])) paint(group,draft);
      });
      input.addEventListener('wheel', event => {
        if (document.activeElement !== input || input.readOnly) return;
        event.preventDefault(); commit(input, group, index);
        move(group,index,event.deltaY < 0 ? 1 : -1,false);
      }, {passive:false});
      input.addEventListener('keydown', event => { if([' ','ArrowUp','ArrowDown'].includes(event.key)) event.preventDefault(); });
    });
    shell.addEventListener('focusout', () => queueMicrotask(() => {
      if (!shell.contains(document.activeElement) && !gesture) close(group);
    }));
    popup.addEventListener('pointerdown', event => {
      if (event.button !== 0 || gesture || subscribed(group)) return;
      if (!inputs.every((input,index) => commit(input,group,index))) return;
      event.preventDefault();
      const rect = sliders[0].getBoundingClientRect(), left = rect.left + 5, width = rect.width - 10;
      const positions = group.values.map(v => left + (v-group.min)/(group.max-group.min)*width);
      const index = inputs.length === 1 ? 0 : endpoint(event.clientX, positions[0], positions[1]);
      const thumb = Math.abs(event.clientX - positions[index]) <= 7;
      sliders[index].focus({preventScroll:true});
      gesture = {group,index,startX:event.clientX,startValue:group.values[index],width:rect.width,thumb};
      popup.setPointerCapture(event.pointerId);
      if (!thumb) {
        const direction = event.clientX > positions[index] ? 1 : -1;
        move(group,index,direction);
        const held = () => {
          if(!gesture || gesture.thumb) return;
          const position = left + (group.values[index]-group.min)/(group.max-group.min)*width;
          if((event.clientX-position)*direction <= 0) return;
          move(group,index,direction); repeat=setTimeout(held,80);
        };
        repeat = setTimeout(held,400);
      }
    });
    popup.addEventListener('pointermove', event => {
      if (!gesture || gesture.group !== group || !gesture.thumb) return;
      set(group,gesture.index,dragValue(event.clientX,gesture.startX,gesture.startValue,gesture.width,group.min,group.max,group.step),false);
    });
    const finish = event => {
      if (!gesture || gesture.group !== group) return;
      emit(group.inputs[gesture.index]); gesture = null; finishRepeat();
      if (popup.hasPointerCapture(event.pointerId)) popup.releasePointerCapture(event.pointerId);
    };
    popup.addEventListener('pointerup',finish); popup.addEventListener('pointercancel',finish);
    popup.addEventListener('lostpointercapture', () => {
      if(gesture?.group === group){emit(group.inputs[gesture.index]);gesture=null;finishRepeat();}
    });
    sliders.forEach((slider,index) => {
      slider.oninput = null;
      slider.addEventListener('keydown', event => {
        const dir = ['ArrowRight','ArrowUp','PageUp'].includes(event.key) ? 1 : ['ArrowLeft','ArrowDown','PageDown'].includes(event.key) ? -1 : 0;
        if(dir){event.preventDefault();move(group,index,dir);}
        else if(event.key==='Home'||event.key==='End'){event.preventDefault();set(group,index,event.key==='Home'?group.min:group.max,true);}
      });
    });
    paint(group);
  }
  root.querySelectorAll('[data-step]').forEach(button => {
    if(button.dataset.vmNumericMounted)return;button.dataset.vmNumericMounted='true';
    const input = document.getElementById(button.dataset.for), item = byInput.get(input);
    if(!item) return;
    button.onclick = null;
    button.addEventListener('pointerdown', event => {
      event.preventDefault(); if(input.disabled || input.readOnly) return;
      const act = () => {commit(input,item.group,item.index);move(item.group,item.index,Number(button.dataset.step));};
      act(); const held=()=>{act();repeat=setTimeout(held,80);};repeat=setTimeout(held,400);
      button.setPointerCapture(event.pointerId);
    });
    button.addEventListener('pointerup',finishRepeat);button.addEventListener('pointercancel',finishRepeat);button.addEventListener('lostpointercapture',finishRepeat);
    button.addEventListener('click', event => {if(event.detail===0)move(item.group,item.index,Number(button.dataset.step));});
  });
  root.querySelectorAll('.numeric input').forEach(input=>{
    if(input.dataset.vmNumericKeyboard)return;input.dataset.vmNumericKeyboard='true';
    input.addEventListener('keydown',event=>{
      if(event.isComposing || input.disabled || input.readOnly)return;
      const item=byInput.get(input);
      if((event.key==='ArrowUp'||event.key==='ArrowDown')&&item){
        event.preventDefault();commit(input,item.group,item.index);
        move(item.group,item.index,event.key==='ArrowUp'?1:-1);return;
      }
      if(event.key!=='Enter')return;
      event.preventDefault();event.stopPropagation();
      if(item){if(!validateDraft(item.group,true))return;close(item.group);}
      else{
        if(input.value===''||!Number.isFinite(Number(input.value))){input.setCustomValidity('请输入有效数值');}
        else input.setCustomValidity('');
        if(!input.checkValidity()){input.reportValidity();return;}emit(input);
      }
      const draftLabel=document.getElementById('draft');if(draftLabel)draftLabel.textContent='数值草稿已确认';
      input.blur();
    });
    if(!byInput.has(input))input.addEventListener('input',()=>input.setCustomValidity(''));
  });
  }
  function dispose(root){
    for(let i=groups.length-1;i>=0;i--)if(root.contains(groups[i].shell)){
      const group=groups[i];if(!group.popup.hidden)close(group);
      group.inputs.forEach(input=>byInput.delete(input));groups.splice(i,1);
    }
  }
  mount();
  document.addEventListener('pointerdown', event => {if(active && !active.shell.contains(event.target))close(active);},true);
  document.addEventListener('keydown', event => {if(event.key==='Escape')closeAll();});
  document.querySelectorAll('[data-tab],.feature-enable input,[data-mode]').forEach(el => el.addEventListener('click',closeAll));
  window.vmNumericInteraction = {closeAll,mount,dispose};
})();
