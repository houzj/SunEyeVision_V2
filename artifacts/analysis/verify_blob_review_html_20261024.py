from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import unquote, urlsplit
import hashlib, json

ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护'
page=DOC/'优化方案/optimization-o06.html'
class Scan(HTMLParser):
    def __init__(self): super().__init__(); self.ids=[]; self.links=[]; self.rows=0
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if 'id' in a:self.ids.append(a['id'])
        if 'href' in a:self.links.append(a['href'])
        if 'data-control-row' in a:self.rows+=1
text=page.read_text(encoding='utf-8')
block=text.split('<!-- BLOB-FIVE-LAYER-REVIEW:BEGIN -->')[1].split('<!-- BLOB-FIVE-LAYER-REVIEW:END -->')[0]
whole=Scan();whole.feed(text)
new=Scan();new.feed(block)
bad=[]
for raw in new.links:
    u=urlsplit(unquote(raw))
    if u.scheme or u.netloc:continue
    target=(page.parent/u.path).resolve() if u.path else page
    if not target.is_file():bad.append({'href':raw,'reason':'missing file'});continue
    if u.fragment:
        other=whole if target==page else Scan()
        if target!=page:other.feed(target.read_text(encoding='utf-8-sig'))
        if u.fragment not in other.ids:bad.append({'href':raw,'reason':'missing fragment'})
inventory=json.loads((ROOT/'artifacts/analysis/blob-shared-controls-inventory-20261024.json').read_text(encoding='utf-8'))
duplicates=[x for x in new.ids if whole.ids.count(x)!=1]
home=(DOC/'index.html').read_text(encoding='utf-8')
report={'kind':'documentation-static-verification-only','controls':inventory['count'],'controlRows':new.rows,
        'chapterAnchors':len(new.ids),'newLinks':len(new.links),'badLinks':bad,'duplicateNewIds':duplicates,
        'homeEntry':home.count('BLOB-REVIEW-ENTRY:BEGIN')==1,'chapterEntry':text.count('BLOB-FIVE-LAYER-REVIEW:BEGIN')==1,
        'productRun':False,'productCodeChangedByThisTask':False,
        'pageSHA256':hashlib.sha256(page.read_bytes()).hexdigest(),
        'homeSHA256':hashlib.sha256((DOC/'index.html').read_bytes()).hexdigest()}
report['pass']=not bad and not duplicates and report['homeEntry'] and report['chapterEntry'] and new.rows==inventory['count']
out=ROOT/'artifacts/analysis/blob-review-static-verification-20261024.json'
out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
raise SystemExit(0 if report['pass'] else 1)
