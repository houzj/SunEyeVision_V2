"""Extract Blob UI facts from installed XML and hash-bound recovered BAML.

The source assembly is authenticated by REA static inspection. Reuse the
matching ILSpy export receipt; do not execute VM or infer runtime template hits.
"""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
INSTALL = Path('D:/Program Files/VisionMaster4.4.0/Applications')
MODULE = INSTALL / 'Module(sp)/x64/Location/IMVSBlobFindModu'
RECOVERY = Path('D:/MyWork/REA逆向测试/analysis/selfdev-support/VM-CONTROL-BLOB-ASSEMBLY-20261024/root')
OUTPUT = ROOT / 'docs/collaboration/evidence/VM_BLOB_UI_SOURCE_BASELINE_20261026.json'
ASSEMBLY_SHA = '639d0fb9dfca81e38df690e201b73874ce006d1361b689ddc6d5f41f7b0b04fd'
P = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
XKEY = '{http://schemas.microsoft.com/winfx/2006/xaml}Key'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def local(name):
    return name.split('}')[-1]


def identity(path):
    return {'path': str(path), 'sha256': sha(path), 'bytes': path.stat().st_size}


def main():
    assembly = INSTALL / 'myLibs/Apps.XmlUI.dll'
    assert sha(assembly) == ASSEMBLY_SHA, 'Reinspect changed Apps.XmlUI before extraction'
    provenance_path = RECOVERY / 'xmlui-provenance.json'
    provenance = json.loads(provenance_path.read_text(encoding='utf-8-sig'))
    assert provenance['inputSha256'].lower() == ASSEMBLY_SHA and provenance['exitCode'] == 0
    assert provenance['targetExecuted'] is False
    decompiler = Path(provenance['toolPath'])
    assert sha(decompiler) == provenance['toolSha256'].lower()
    source = RECOVERY / 'current-xmlui/styles/paramusercontroldatatemplate.xaml'
    recorded = next(item for item in provenance['outputs'] if Path(item['path']) == source)
    assert sha(source) == recorded['sha256'].lower(), 'Recovered dictionary differs from bound export'
    dictionary = ET.parse(source).getroot()
    templates = {item.get(XKEY): item for item in dictionary if item.tag == P + 'DataTemplate'}
    keys = ('Boolean_sub', 'Boolean', 'Integer_Sub', 'Integer', 'IntegerBettween_Sub',
            'IntegerBettween', 'Float_sub', 'Float', 'FloatBettween_sub', 'FloatBettween',
            'Enumeration', 'EnumerationG', 'ColorConfigEx', 'ColorConfig',
            'ImageViewConfig_subContent', 'ImageViewConfig', 'StringConfig_sub', 'StringConfig')
    snippets = ROOT / 'artifacts/analysis/vm-blob-ui-source-20261026/templates'
    snippets.mkdir(parents=True, exist_ok=True)
    selected = []
    for key in keys:
        if key not in templates:
            continue
        node = templates[key]
        path = snippets / (key + '.xaml')
        path.write_bytes(ET.tostring(node, encoding='utf-8', xml_declaration=True))
        selected.append({'key': key, 'source': identity(source), 'snippet': identity(path),
                         'elements': [{'type': local(child.tag), 'attributes': child.attrib}
                                      for child in node.iter()]})
    tabs_path = MODULE / 'IMVSBlobFindModuAlgorithmTab.xml'
    tabs = []
    properties = ('DisplayName', 'Description', 'AlgorithmIndex', 'AccessMode', 'CurValue',
                  'DefaultValue', 'MinValue', 'MaxValue', 'IncValue', 'CustomVisible',
                  'DisplayType', 'SubscribeVisible', 'NeedSlider', 'DecimalDigits')
    for tab in ET.parse(tabs_path).getroot().findall('./Tabs/Tab'):
        categories = []
        for category in tab.findall('./Categorys/Category'):
            fields = []
            for field in category.findall('./Items/*'):
                fields.append({'type': field.tag, 'attributes': field.attrib,
                               'properties': {key: field.findtext(key) for key in properties
                                              if field.find(key) is not None},
                               'definitionXml': ET.tostring(field, encoding='unicode')})
            categories.append({'attributes': category.attrib, 'fields': fields})
        tabs.append({'attributes': tab.attrib, 'categories': categories})
    display_path = MODULE / 'IMVSBlobFindModuDisplay.xml'
    objects = [node.attrib for node in ET.parse(display_path).getroot().findall('.//Object')]
    style = templates['ImageViewConfig_subContent']
    sliders = [{'type': local(node.tag), **node.attrib} for node in style.iter()
               if local(node.tag) == 'SliderArrangeFloat']
    assert [(s['Minimum'], s['Maximum'], s['SliderTickFrequency']) for s in sliders] == [
        ('0', '1', '0.01'), ('1', '10', '1')]
    result = {
        'status': 'STATIC_SOURCE_BASELINE_RUNTIME_SELECTION_UNOBSERVED',
        'productComparisonBaseline': 'ca247bdb (before this evidence-led increment correction)',
        'scope': 'Installed VM Blob XML plus exact-hash ILSpy BAML recovery; not a VM process observation or whole-library acceptance',
        'rea': {'artifactEvidence': 'ev_0b720b58c29ebc598cf14b4033d1cf52c913afcb9c9d066a53d694437e997b36',
                'assembly': identity(assembly), 'mvid': 'a00f8b40-7a51-4c6d-8aeb-cad134724f1f',
                'resource': {'name': 'Apps.XmlUI.g.resources', 'bytes': 779163,
                             'sha256': '93bb8aeb64c20e9eb12e1e8fb33db4a9367c08b69c13ae2dcd2ca42a6b77f3a1'},
                'memberEvidence': 'ev_704887f3d378d65afcfa43caa1d2135676c1162d67043b31ebb8b30f485faf64',
                'memberCoverage': 'unavailable: REA metadata representation exceeds 67108864-byte budget; no member-body success claimed'},
        'decompilation': {'receipt': identity(provenance_path), 'tool': identity(decompiler),
                          'version': provenance['decompiler'], 'targetExecuted': False,
                          'classification': 'Decompiler reconstruction, hash-bound to inspected assembly; not original authored source'},
        'installedXml': [identity(tabs_path), identity(display_path), identity(MODULE / 'IMVSBlobFindModu.xml')],
        'tabs': tabs, 'displayObjects': objects, 'templates': selected,
        'verifiedStyleLayout': {'width': 260, 'labelColumn': 102, 'rowHeight': 32,
                                'rows': ['OK颜色', 'NG颜色', '透明度', '粗细'],
                                'strokeRowConditional': 'BridgeObjA.StrokeThicknessConfigEnable',
                                'sliders': sliders, 'popupHorizontalOffset': -90},
        'migrationDeltas': [
            'At ca247bdb the declared numeric editor is an extracted SDK NumericUpDown, not the original SliderArrange/SliderArrangeFloat consumer; importing native source alone does not close this obligation.',
            'ImageViewConfig opacity tick is 0.01; stroke tick is 1. At ca247bdb FieldLayout had no increment and declared NumericUpDown defaulted to 1: explicit mismatch, now being corrected.',
            'Native integer/float and two-ended templates bind values, XML extrema, subscription state/data, and value/type-change commands; preserve the full combination when adapting to neutral contracts.',
            'Installed RangArea allows 1..999999999, while screenshot text 9999999 is a current value, not proof of maximum. Do not silently replace saved values or existing product limits.',
            'Installed FindNum permits 1..20000; at ca247bdb BlobCountLimit presentation maximum 1000000 disagreed with both VM XML and existing SDK validation 20000, now being corrected.',
        ],
        'remaining': ['VM process loaded version, chosen custom/fallback window and active template/theme are unobserved',
                      'Preserve user-supplied VM screenshots as same-state acceptance references',
                      'Native consumers, legacy value preservation, private draft/error/confirm/cancel behavior and all original31 exits still require implementation and Release verification'],
    }
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'output': str(OUTPUT.relative_to(ROOT)), 'tabs': len(tabs),
                      'templates': len(selected), 'displayObjects': len(objects),
                      'sha256': sha(OUTPUT), 'runtimeObserved': False}, ensure_ascii=False))


if __name__ == '__main__':
    main()
