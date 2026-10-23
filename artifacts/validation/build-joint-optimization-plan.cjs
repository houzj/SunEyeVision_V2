const fs=require('fs');
module.exports=function(){
 const p='artifacts/validation/joint-optimization-plan.json',d=JSON.parse(fs.readFileSync(p));
 d.roles[0][0]='自研产品线（自研主程序）';d.roles[0][2]='现状复审、设计取舍、公共合同、适配实现与产品回归；交付变更事实给专职文档维护者';
 d.documentation={owner:'自研架构文档维护',scope:'架构对照HTML、对应生成器/离线验证、DOC_SYNC_STATUS.json',policy:'明确交付后增量同步；不轮询；产品线不并发写文档文件'};
 fs.writeFileSync(p,JSON.stringify(d,null,2));
 const E=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
 const labels=['角色','工作区','主责','边界','交付'];const rows=d.roles.map(r=>'<tr>'+r.map((v,i)=>'<td data-label="'+labels[i]+'">'+E(v)+'</td>').join('')+'</tr>').join('');
 let html=fs.readFileSync('artifacts/validation/joint-optimization-plan.template.html','utf8');
 html=html.replace(/(<section id="roles">[\s\S]*?<tbody>)[\s\S]*?(<\/tbody>)/,(_,before,after)=>before+rows+after);
 html=html.replace('</h1>','</h1><p class="notice"><a href="index.html">文档索引</a> · <a href="architecture-target.html">2026-10-22最新12项目标决策</a>。文档由专职聊天唯一维护，产品开发交付变更事实。J00已有有限静态核对，样本、构建和运行验收未完成；旧批次表保留路线规划状态，不表示产品已实施。</p>');
 fs.writeFileSync('docs/04-工具设计/架构对照/2026/10/21/joint-optimization-plan.html',html);
};
if(require.main===module)module.exports();
