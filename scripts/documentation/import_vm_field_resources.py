"""Import the existing VM templates and original PNGs; never redraw controls."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path('D:/MyWork/REA逆向测试')
P = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
X = 'http://schemas.microsoft.com/winfx/2006/xaml'
ET.register_namespace('', P)
ET.register_namespace('x', X)
local = SOURCE / 'src/VisionUi/SourceControlStyles.xaml'
original = SOURCE / 'analysis/decompiled/Apps.Style/buttonstyle/togglebuttonstyle.xaml'
specs = [(local, key) for key in ('NormalRadioButton', 'eyeToggleButton', 'NativeLinkButton', 'NativeDisplayPopupButton')]
specs.append((original, 'EditLinkButton'))
target = ROOT / 'src/Plugin.Wpf/Themes'
dictionary = ET.Element(f'{{{P}}}ResourceDictionary')
manifest = []
for path, key in specs:
    source = ET.parse(path).getroot()
    element = next(item for item in source if item.get(f'{{{X}}}Key') == key)
    text = ET.tostring(element, encoding='unicode')
    # Native ToggleButton is two-state; its IsChecked/IsPressed/IsMouseOver booleans
    # bind directly instead of introducing a dependency on VM.Utility.CheckedConvert.
    text = text.replace(', Converter={StaticResource CheckedConvert}', '')
    for prefix in ('/VisionUi;component/SourceAssets/', '/Apps.Style;component/Resources/'):
        text = text.replace(prefix, '/SunEyeVision.Plugin.Wpf;component/Themes/Assets/')
    dictionary.append(ET.fromstring(text))
    manifest.append({'key': key, 'source': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    for name in set(re.findall(r'Themes/Assets/([^"<]+\.png)', text)):
        origin = SOURCE / ('src/VisionUi/SourceAssets' if path == local else 'analysis/decompiled/Apps.Style/resources') / name
        destination = target / 'Assets' / name
        shutil.copyfile(origin, destination)
        manifest.append({'asset': name, 'source': str(origin), 'sha256': hashlib.sha256(origin.read_bytes()).hexdigest()})
ET.indent(dictionary)
ET.ElementTree(dictionary).write(target / 'VmFieldStyles.xaml', encoding='utf-8', xml_declaration=True)
(target / 'VmFieldStyles.provenance.json').write_text(json.dumps({'adaptation': 'pack URI and direct two-state Boolean bindings only', 'sources': manifest}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
generic = target / 'Generic.xaml'
text = generic.read_text(encoding='utf-8-sig')
text = re.sub(r'<Style x:Key="O06.RegionSourceRadioStyle".*?</Style>\s*', '', text, flags=re.S)
text = text.replace('Source="VmFieldStyles.xaml"', 'Source="/SunEyeVision.Plugin.Wpf;component/Themes/VmFieldStyles.xaml"')
if 'component/Themes/VmFieldStyles.xaml' not in text:
    text = text.replace('</ResourceDictionary.MergedDictionaries>', '<ResourceDictionary Source="/SunEyeVision.Plugin.Wpf;component/Themes/VmFieldStyles.xaml" /></ResourceDictionary.MergedDictionaries>', 1)
generic.write_text(text, encoding='utf-8')
print(f'Imported {len(specs)} original styles and {sum("asset" in item for item in manifest)} original PNGs.')
