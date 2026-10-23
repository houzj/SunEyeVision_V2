(() => {
  const form = document.getElementById('document-search-form');
  const input = document.getElementById('document-search-input');
  const clear = document.getElementById('document-search-clear');
  const results = document.getElementById('document-search-results');
  const status = document.getElementById('document-search-status');
  if (!form || !input || !clear || !results || !status) return;

  const documents = window.DOCUMENT_SEARCH_INDEX || [];
  const normalize = value => String(value || '').normalize('NFKC').toLocaleLowerCase().replace(/\s+/g, ' ').trim();
  const fields = item => [item.title, item.topic, item.pageTitle, item.heading, item.excerpt].map(normalize);

  function rank(group, query) {
    const queryTerms = query.split(' ').filter(Boolean);
    const combined = group.records.flatMap(fields).join(' ');
    const exact = queryTerms.every(term => combined.includes(term));
    let overlap = 0;
    if (!exact && queryTerms.length === 1) {
      const term = queryTerms[0];
      if (term.length <= 2) return 0;
      const grams = new Set(Array.from({length: term.length - 1}, (_, i) => term.slice(i, i + 2)));
      overlap = [...grams].filter(gram => combined.includes(gram)).length / grams.size;
      if (overlap < 0.75) return 0;
    } else if (!exact) return 0;

    const best = group.records.map(item => {
      const f = fields(item);
      let score = exact ? 10 : Math.round(overlap * 6);
      let covered = 0;
      for (const term of queryTerms) {
        if (f.some(value => value.includes(term))) covered++;
        if (f[0].includes(term)) score += 30;
        if (f[1].includes(term)) score += 14;
        if (f[2].includes(term)) score += 10;
        if (f[3].includes(term)) score += 16;
        if (f[4].includes(term)) score += 5;
      }
      return {item, score, covered};
    }).sort((a, b) => b.covered - a.covered || b.score - a.score)[0];
    const matchingRecords=[],missing=new Set(queryTerms);
    while(missing.size&&matchingRecords.length<3){
      const next=group.records.map(item=>({item,terms:[...missing].filter(term=>fields(item).some(value=>value.includes(term)))})).sort((a,b)=>b.terms.length-a.terms.length)[0];
      if(!next||!next.terms.length)break;
      matchingRecords.push(next.item);next.terms.forEach(term=>missing.delete(term));
    }
    if (!matchingRecords.length && queryTerms.length === 1) {
      const term=queryTerms[0],grams=new Set(Array.from({length:term.length-1},(_,i)=>term.slice(i,i+2)));
      matchingRecords.push(...group.records.filter(item=>{const value=fields(item).join(' ');return [...grams].filter(gram=>value.includes(gram)).length/grams.size>=0.75;}).slice(0,3));
    }
    const matching = matchingRecords
      .sort((a, b) => fields(b).filter(value => queryTerms.some(term => value.includes(term))).length - fields(a).filter(value => queryTerms.some(term => value.includes(term))).length)
      .slice(0, 3).map(item => item.excerpt).filter(Boolean);
    return {...best, excerpt: matching.join(' · ').slice(0, 520)};
  }

  function runSearch() {
    const query = normalize(input.value);
    results.replaceChildren();
    if (!query) {
      status.textContent = '输入名称或关键词查找专题；多个词用空格分开。';
      return [];
    }

    const groups = new Map();
    for (const item of documents) {
      const key = item.path;
      if (!groups.has(key)) groups.set(key, {path: key, records: []});
      groups.get(key).records.push(item);
    }
    const matches = [...groups.values()].map(group => ({group, result: rank(group, query)}))
      .filter(x => x.result)
      .sort((a, b) => b.result.score - a.result.score || a.result.item.title.localeCompare(b.result.item.title, 'zh-CN'))
      .slice(0, 12);

    if (!matches.length) {
      status.textContent = '没有找到匹配内容。试试更短的词，或用空格分开关键词。';
      return [];
    }

    status.textContent = '找到 ' + matches.length + ' 个专题';
    for (const {result} of matches) {
      const item = result.item;
      const li = document.createElement('li');
      const link = document.createElement('a');
      const meta = document.createElement('small');
      const excerpt = document.createElement('p');
      link.href = item.href;
      link.textContent = item.heading && item.heading !== item.pageTitle ? item.heading : item.pageTitle;
      meta.textContent = item.topic + ' · ' + item.pageTitle;
      excerpt.textContent = result.excerpt || item.excerpt;
      li.append(link, meta, excerpt);
      results.append(li);
    }
    return matches;
  }

  form.addEventListener('submit', event => { event.preventDefault(); runSearch(); });
  input.addEventListener('input', runSearch);
  clear.addEventListener('click', () => { input.value = ''; runSearch(); input.focus(); });
  input.addEventListener('keydown', event => {
    if (event.key === 'ArrowDown') {
      const first = results.querySelector('a');
      if (first) { event.preventDefault(); first.focus(); }
    } else if (event.key === 'Escape') {
      input.value = '';
      runSearch();
    }
  });
  document.addEventListener('keydown', event => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLocaleLowerCase() === 'k') {
      event.preventDefault();
      input.focus();
      input.select();
    }
  });
})();
