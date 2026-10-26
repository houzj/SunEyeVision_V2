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
specs.extend([(local, 'SliderStyle1')])
bottom = SOURCE / 'src/VisionUi/SourceBottomButtons.xaml'
specs.append((bottom, 'Orange_ButtonStyle'))
tabs = SOURCE / 'analysis/decompiled/Apps.Style/itemscontrolstyle/itemscontrolstyle.xaml'
specs.extend((tabs, key) for key in ('TopTabItemStyle', 'TabControlStyle'))
target = ROOT / 'src/Plugin.Wpf/Themes'
dictionary = ET.Element(f'{{{P}}}ResourceDictionary')
manifest = []
merged = ET.SubElement(dictionary, f'{{{P}}}ResourceDictionary.MergedDictionaries')
ET.SubElement(merged, f'{{{P}}}ResourceDictionary', {'Source': '/SunEyeVision.Plugin.Wpf;component/Themes/VmSliderDictionary.xaml'})

def copy_asset(origin, name):
    shutil.copyfile(origin, target / 'Assets' / name)
    manifest.append({'asset': name, 'source': str(origin), 'sha256': hashlib.sha256(origin.read_bytes()).hexdigest()})

# The local reconstruction already carries the complete original slider dictionary.
slider_source = SOURCE / 'src/VisionUi/SourceSliderDictionary.xaml'
slider = slider_source.read_text(encoding='utf-8-sig').replace('/Apps.XmlUI;component/Resources/', '/SunEyeVision.Plugin.Wpf;component/Themes/Assets/')
(target / 'VmSliderDictionary.xaml').write_text(slider, encoding='utf-8')
manifest.append({'dictionary': 'VmSliderDictionary.xaml', 'source': str(slider_source), 'sha256': hashlib.sha256(slider_source.read_bytes()).hexdigest()})
for name in set(re.findall(r'Themes/Assets/([^"<]+\.png)', slider)):
    copy_asset(SOURCE / 'analysis/selfdev-support/VM-CONTROL-BLOB-ASSEMBLY-20261024/root/current-xmlui/resources' / name, name)

# Theme brushes are source resources, not a dependency on VM's theme singleton.
for path in (local, bottom):
    for brush in ET.parse(path).getroot():
        key = brush.get(f'{{{X}}}Key')
        if brush.tag.endswith('Brush') and key and not any(node.get(f'{{{X}}}Key') == key for node in dictionary):
            dictionary.append(brush)
for path, key in specs:
    source = ET.parse(path).getroot()
    element = next(item for item in source if item.get(f'{{{X}}}Key') == key)
    text = ET.tostring(element, encoding='unicode')
    # Native ToggleButton is two-state; its IsChecked/IsPressed/IsMouseOver booleans
    # bind directly instead of introducing a dependency on VM.Utility.CheckedConvert.
    text = text.replace(', Converter={StaticResource CheckedConvert}', '')
    if key == 'TopTabItemStyle':
        # Our host already supplies the localized header and selection; it has no VM tab-model object.
        element = ET.fromstring(text)
        for setter in list(element):
            if setter.tag == f'{{{P}}}Setter' and setter.get('Property') == 'TabItem.IsSelected':
                element.remove(setter)
        for node in element.iter():
            if node.tag == f'{{{P}}}Border' and node.get('Name') == 'bd':
                node.attrib.pop('Width', None)
                node.set('Padding', '14,0')
            if node.tag == f'{{{P}}}ContentControl' and node.get('Name') == 'Content':
                for child in list(node):
                    if child.tag == f'{{{P}}}ContentControl.Content':
                        node.remove(child)
                node.set('Content', '{TemplateBinding HeaderedContentControl.Header}')
        text = ET.tostring(element, encoding='unicode')
    for prefix in ('/VisionUi;component/SourceAssets/', '/Apps.Style;component/Resources/'):
        text = text.replace(prefix, '/SunEyeVision.Plugin.Wpf;component/Themes/Assets/')
    dictionary.append(ET.fromstring(text))
    manifest.append({'key': key, 'source': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    for name in set(re.findall(r'Themes/Assets/([^"<]+\.png)', text)):
        origin = SOURCE / ('src/VisionUi/SourceAssets' if path == local else 'analysis/decompiled/Apps.Style/resources') / name
        copy_asset(origin, name)
ET.indent(dictionary)
ET.ElementTree(dictionary).write(target / 'VmFieldStyles.xaml', encoding='utf-8', xml_declaration=True)
(target / 'VmFieldStyles.provenance.json').write_text(json.dumps({'adaptation': 'pack URI and direct two-state Boolean bindings only', 'sources': manifest}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
generic = target / 'Generic.xaml'
text = generic.read_text(encoding='utf-8-sig')
text = re.sub(r'<Style x:Key="O06.RegionSourceRadioStyle".*?</Style>\s*', '', text, flags=re.S)
text = re.sub(r'<Style x:Key="SliderStyle".*?</Style>\s*', '', text, flags=re.S)
text = text.replace('Style="{StaticResource SliderStyle}"', 'Style="{StaticResource SliderStyle1}"')
text = text.replace('Source="VmFieldStyles.xaml"', 'Source="/SunEyeVision.Plugin.Wpf;component/Themes/VmFieldStyles.xaml"')
if 'component/Themes/VmFieldStyles.xaml' not in text:
    text = text.replace('</ResourceDictionary.MergedDictionaries>', '<ResourceDictionary Source="/SunEyeVision.Plugin.Wpf;component/Themes/VmFieldStyles.xaml" /></ResourceDictionary.MergedDictionaries>', 1)
generic.write_text(text, encoding='utf-8')

# Keep the existing reconstructed BooleanControl implementation, changing only its namespace/asset URI.
control_dir = ROOT / 'src/Plugin.Wpf/Controls/Native/Common'
control_dir.mkdir(parents=True, exist_ok=True)
for name in ('SourceBooleanToggle.xaml', 'SourceBooleanToggle.xaml.cs'):
    origin = SOURCE / 'src/VisionUi' / name
    content = origin.read_text(encoding='utf-8-sig').replace('VisionUi.SourceBooleanToggle', 'SunEyeVision.Plugin.Wpf.Native.Common.SourceBooleanToggle')
    content = content.replace('namespace VisionUi;', 'namespace SunEyeVision.Plugin.Wpf.Native.Common;')
    content = content.replace('/VisionUi;component/SourceAssets/', '/SunEyeVision.Plugin.Wpf;component/Themes/Assets/')
    (control_dir / name).write_text(content, encoding='utf-8')
    manifest.append({'control': name, 'source': str(origin), 'sha256': hashlib.sha256(origin.read_bytes()).hexdigest()})
for name in ('swith_off.png', 'swith_on.png'):
    copy_asset(SOURCE / 'src/VisionUi/SourceAssets' / name, name)
(target / 'VmFieldStyles.provenance.json').write_text(json.dumps({'adaptation': 'namespace/pack URI, direct two-state Boolean binding, host-localized tab header/selection', 'sources': manifest}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Imported {len(specs)} original styles and {sum("asset" in item for item in manifest)} original PNGs.')
