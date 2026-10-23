(()=>{const root=document.getElementById('forward-first-pass'),select=document.getElementById('forward-package');if(!root||!select)return;
const groups=[...root.querySelectorAll('[data-forward-package]')];
function show(id){groups.forEach(g=>g.hidden=g.dataset.forwardPackage!==id);select.value=id;}
select.addEventListener('change',()=>{show(select.value);history.replaceState(null,'','#forward-first-pass');});
function route(){const target=document.getElementById(decodeURIComponent(location.hash.slice(1)));if(!target?.matches('[data-forward-step]'))return;show(target.closest('[data-forward-package]').dataset.forwardPackage);target.open=true;target.scrollIntoView({block:'start'});}
addEventListener('hashchange',route);route();let saved=[];
addEventListener('beforeprint',()=>{saved=[...groups,...root.querySelectorAll('details')].map(e=>[e,e.hidden,e.open]);groups.forEach(e=>e.hidden=false);root.querySelectorAll('details').forEach(e=>e.open=true);});
addEventListener('afterprint',()=>saved.forEach(([e,hidden,open])=>{e.hidden=hidden;if(e.tagName==='DETAILS')e.open=open;}));
})();
