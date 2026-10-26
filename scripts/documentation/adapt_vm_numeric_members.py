"""Keep native numeric text consistent with the neutral invariant literal contract."""
from pathlib import Path
import hashlib
import json

ROOT=Path(__file__).resolve().parents[2]
NATIVE=ROOT/'src/Plugin.Wpf/Controls/Native'
MANIFEST=NATIVE/'Numeric/numeric-migration-manifest.json'
RECEIPT=NATIVE/'Display/visual-host-adaptations.json'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    manifest=json.loads(MANIFEST.read_text(encoding='utf-8'))
    receipt=json.loads(RECEIPT.read_text(encoding='utf-8'))
    integrated=json.loads((NATIVE/'source-integration-receipt.json').read_text(encoding='utf-8'))
    hashes={entry['path']:entry['sha256'] for entry in integrated['integrated_files']}
    protected={entry['target']:entry for entry in receipt['files']}
    changes=[]
    for name in ('NumericControl.xaml.cs','RangeRule.cs'):
        entry=next(row for row in manifest['entries'] if row['target'].replace('\\','/').endswith('PcUi/'+name))
        target=entry['target'].replace('\\','/');path=ROOT/target;source=Path(entry['source'])
        assert sha(source)==entry['source_sha256'].lower()
        if target in protected:
            assert sha(path)==protected[target]['adapted_sha256']
            continue
        assert sha(path)==hashes[target], 'Review changed native numeric source before adapting'
        text=path.read_text(encoding='utf-8')
        if name=='NumericControl.xaml.cs':
            old='double.TryParse(Text, out var result)';assert text.count(old)==1
            text=text.replace(old,'double.TryParse(Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)')
            old='_Value.ToString();';assert text.count(old)==1
            text=text.replace(old,'_Value.ToString(System.Globalization.CultureInfo.InvariantCulture);')
        else:
            old='double.Parse((string)value)';assert text.count(old)==1
            text=text.replace(old,'double.Parse((string)value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)')
        changes.append((entry,path,source,target,text))
    for entry,path,source,target,text in changes:
        path.write_text(text,encoding='utf-8',newline='\r\n')
        entry['target_sha256']=sha(path).upper()
        entry['adaptations'].append('Invariant literal parsing/formatting for the neutral host; original native numeric UI/templates retained.')
        receipt['files'].append({'target':target,'original_source':str(source),
            'original_source_sha256':sha(source),'adapted_sha256':sha(path),
            'numeric_member_adapter':'invariant-literal-v1'})
    if changes:
        receipt['changes'].append('原PCUI NumericControl/RangeRule按本方中立数值合同使用InvariantCulture，避免de-DE将点号按千位分隔误读；原模板、箭头和事件保留，不宣称VM区域设置行为相同。')
        MANIFEST.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        RECEIPT.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'adapted_files':len(changes),'runtime_verified':False}))


if __name__=='__main__':main()
