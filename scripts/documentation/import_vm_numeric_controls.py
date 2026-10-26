"""Reproduce the numeric migration from existing exported source; never synthesizes controls.

Run only in this worktree. No build, tests, package restoration or vendor execution.
External cross-group types/resources are explicit manifest dependencies, not replacement stubs.
"""
from pathlib import Path
import hashlib
import json
import re
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
EXPORT = Path('D:/MyWork/REA逆向测试/analysis/decompiled')
CURRENT = Path('D:/MyWork/REA逆向测试/analysis/selfdev-support/VM-CONTROL-BLOB-ASSEMBLY-20261024/root/current-xmlui')
CODE = ROOT / 'src/Plugin.Wpf/Controls/Native/Numeric'
THEME = ROOT / 'src/Plugin.Wpf/Themes/Native/Numeric'
ASSET = ROOT / 'src/Plugin.Wpf/Themes/Assets/Native/Numeric'
NS = 'SunEyeVision.Plugin.Wpf.Native.Numeric.'
PACK = '/SunEyeVision.Plugin.Wpf;component/'
numeric_names = {'ByteUpDown','SByteUpDown','ShortUpDown','UShortUpDown','IntegerUpDown','UIntegerUpDown',
                 'LongUpDown','ULongUpDown','SingleUpDown','DoubleUpDown','DecimalUpDown','NumericUpDown','CommonNumericUpDown'}
numeric_editors = {name+'Editor' for name in numeric_names} | {'PropertyGridEditor'+name for name in numeric_names} | {'UpDownEditor'}
entries = []
types = []
dependencies = set()
dependency_manifests = []
dependency_types = {}
dependency_resources = {}
for group, filename in [('Display','display-migration-manifest.json'),('Input','migration-manifest.json')]:
    relative=Path('src/Plugin.Wpf/Controls/Native')/group/filename
    candidates=[ROOT/relative,ROOT.parent/group.lower()/relative]
    selected=next((candidate for candidate in candidates if candidate.is_file()),None)
    if selected is None:
        raise FileNotFoundError('The actual '+group+' migration manifest is required: '+str(relative))
    data=json.loads(selected.read_text(encoding='utf-8-sig'))
    dependency_manifests.append({'group':group,'path':str(selected),'sha256':hashlib.sha256(selected.read_bytes()).hexdigest().upper()})
    mapping=data['type_map']
    if isinstance(mapping,dict):dependency_types.update(mapping)
    else:dependency_types.update({item['original_full_type']:item['target_full_type'] for item in mapping})
    dependency_resources.update({key.lower():value for key,value in data.get('component_uri_map',{}).items()})
    if group=='Input':
        for item in data['files']:
            if item.get('output') and item.get('original_relative'):
                dependency_resources[('/'+item['library']+';component/'+item['original_relative']).lower()]=PACK+item['output'].replace('src/Plugin.Wpf/','')
dependency_namespaces={original.rsplit('.',1)[0]:target.rsplit('.',1)[0] for original,target in dependency_types.items() if original.count('.')>0}
old_manifest = CODE/'numeric-migration-manifest.json'
previous_entries = json.loads(old_manifest.read_text(encoding='utf-8'))['entries'] if old_manifest.exists() else []

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def text(path): return path.read_text(encoding='utf-8-sig')
def record(source, target, kind, adaptations=()):
    entries.append({'source':str(source),'source_sha256':digest(source),'target':str(target.relative_to(ROOT)),
                    'target_sha256':digest(target),'kind':kind,'adaptations':list(adaptations)})

def map_namespace(namespace):
    if namespace in dependency_namespaces:return dependency_namespaces[namespace]
    if namespace.startswith('VM.Controls'):
        suffix = namespace[len('VM.Controls'):]
        return 'SunEyeVision.Plugin.Wpf.Native.Display.VMControls'+suffix
    if namespace.startswith('VM.Utility'):
        return namespace.replace('VM.Utility','SunEyeVision.Plugin.Wpf.Native.Display.VMUtility',1)
    if namespace.startswith('HikVision.Styles'):
        return namespace.replace('HikVision.Styles','SunEyeVision.Plugin.Wpf.Native.Display.HikVision.Styles',1)
    if namespace in {'HikPcUI.ListView','HikPcUI.Controls','Apps.XmlUI.ParamTabControl'}:
        return NS+namespace
    return namespace

def image_uri(match, source):
    assembly, component = match.group(1), match.group(2)
    filename = component.rsplit('/',1)[-1]
    lookup = {'Apps.XmlUI':CURRENT,'VM.PcUI':EXPORT/'VM.PcUI','Apps.Style':EXPORT/'Apps.Style','Apps.PcUI':EXPORT/'VM.PcUI'}
    origin_root = lookup.get(assembly)
    options = list(origin_root.rglob(filename)) if origin_root else []
    if not options:
        # Exporters retain lower-case resource filenames on some assemblies.
        options = [p for p in origin_root.rglob('*') if p.is_file() and p.name.lower()==filename.lower()] if origin_root else []
    if not options:
        dependencies.add('asset:'+assembly+'/'+component)
        return match.group(0)
    origin = options[0]; target = ASSET/assembly/origin.name
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(origin,target)
    if not any(e['target']==str(target.relative_to(ROOT)) for e in entries):record(origin,target,'original_asset')
    return PACK+str(target.relative_to(ROOT/'src/Plugin.Wpf')).replace('\\','/')

def transform(value, source, original_namespace, target_namespace):
    # Only finite assembly/namespace/resource adaptations; all handlers and algorithms remain original.
    if original_namespace.startswith('VM.Controls'):
        value = re.sub(r'^namespace '+re.escape(original_namespace)+r';','namespace '+target_namespace+';',value,flags=re.M)
    else:
        value = value.replace(original_namespace,target_namespace)
    for old in ('VM.Controls.Core.Utilities','VM.Controls.Core.Converters','VM.Controls.Primitives','VM.Controls.PropertyGrid.Editors','VM.Controls.PropertyGrid','VM.Controls.Themes','VM.Utility.Convert'):
        value = re.sub(r'(?<![\w.])'+re.escape(old)+r'\b',map_namespace(old),value)
    value = re.sub(r'/([\w.]+);component/([^"\s<>]+\.(?:png|jpg|bmp|ico|cur))',lambda m:image_uri(m,source),value,flags=re.I)
    # Decompiled attached-property short forms are expanded without changing their values.
    if source.suffix=='.xaml':
        value = re.sub(r'(?<=\s)(Column|Row|ColumnSpan|RowSpan)=',lambda m:'Grid.'+m.group(1)+'=',value)
        value = re.sub(r'/(?:Apps.PcUI|VM.PcUI);component/Controls/CommomDictionary.xaml',PACK+'Themes/Native/Numeric/Shared/CommomDictionary.xaml',value,flags=re.I)
        value = re.sub(r'/(Apps.XmlUI|VM.PcUI);component/(?:ParamTabControl/ListView|ListView)/SliderArrangeDictionary.xaml',
                       PACK+'Themes/Native/Numeric/'+('AppsXmlUi' if original_namespace.startswith('Apps.') else 'PcUi')+'/SliderArrangeDictionary.xaml',value)
        value = value.replace('/Apps.Style;component/ButtonStyle/ToggleButtonStyle.xaml',PACK+'Themes/VmFieldStyles.xaml')
        value = re.sub(r'(clr-namespace:SunEyeVision\.Plugin\.Wpf\.Native\.Numeric\.[^";]+);assembly=[\w.]+',r'\1',value)
        value = re.sub(r'clr-namespace:([\w.]+)(?:;assembly=[\w.]+)?',
                       lambda m:'clr-namespace:'+map_namespace(m.group(1)) if m.group(1) in dependency_namespaces and not m.group(1).startswith('SunEyeVision.') else m.group(0),value)
    value=re.sub(r'/[\w.]+;component/[^"\s<>]+',lambda m:dependency_resources.get(m.group(0).lower(),m.group(0)),value)
    return value

def import_source(source, family, original_namespace, target_namespace=None):
    target_namespace = target_namespace or NS+original_namespace
    is_dictionary = source.suffix=='.xaml' and ET.fromstring(text(source)).tag.rsplit('}',1)[-1]=='ResourceDictionary'
    destination = (THEME if is_dictionary else CODE)/family/source.name
    destination.parent.mkdir(parents=True,exist_ok=True)
    value=transform(text(source),source,original_namespace,target_namespace)
    if source.suffix=='.cs':
        if source.name=='SliderArrangeBase.cs':
            # Both original exported bases contain private handlers referenced by derived XAML.
            # BAML had already captured those delegates; rebuilding XAML needs inherited access.
            # Keep the exact handler bodies and the original commit/drag notification semantics.
            for handler in ['T1_OnNumericClick','T2_OnNumericClick']:
                value=value.replace('private void '+handler+'(', 'protected void '+handler+'(')
        # Numeric declarations move out of the display group's support namespace.
        if original_namespace.startswith('VM.Controls'):
            value = re.sub(r'^namespace [\w.]+;', 'namespace '+target_namespace+';',value,flags=re.M)
            value = ('using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;\n'
                     'using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;\n'
                     'using '+NS+'VM.Controls;\n'+value)
            if 'PropertyGrid.Editors' in original_namespace:
                # These names originally resolved through the enclosing VM.Controls.PropertyGrid
                # namespace. Moving only editors loses that parent scope; use the actual owner map.
                property_item_namespace=dependency_types['VM.Controls.PropertyGrid.PropertyItem'].rsplit('.',1)[0]
                value='using '+property_item_namespace+';\n'+value
        nesting=[]
        for found in re.finditer(r'^(\s*)(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly)\s+)+(?:class|enum|struct)\s+(\w+)(\s*<[^\n>{}]+>)?',text(source),re.M):
            indent=len(found.group(1).rsplit('\n',1)[-1].expandtabs(4))
            while nesting and nesting[-1][0]>=indent:nesting.pop()
            name=found.group(2)+(found.group(3) or '').strip()
            qualified='.'.join([item[1] for item in nesting]+[name])
            types.append({'source_type':original_namespace+'.'+qualified,'target_type':target_namespace+'.'+qualified,'target':str(destination.relative_to(ROOT))})
            nesting.append((indent,name))
    destination.write_text(value,encoding='utf-8')
    record(source,destination,'xaml_dictionary' if is_dictionary else 'xaml_control' if source.suffix=='.xaml' else 'csharp',
           ['namespace/pack URI only','restore attached property qualification where exported shorthand'])

def import_parameter_family(base, family, namespace, point=False):
    names=['NumericControl.xaml','NumericControl.xaml.cs','NumbericTextBox.cs','SliderArrange.xaml','SliderArrange.xaml.cs',
           'SliderArrangeFloat.xaml','SliderArrangeFloat.xaml.cs','SliderArrangeBase.cs','SliderArrangeDictionary.xaml',
           'SliderArrangeDictionary.xaml.cs','RangeRule.cs','SymbolType.cs','SliderArrangeType.cs']
    if point:names+=['SliderArrangePoint.xaml','SliderArrangePoint.xaml.cs']
    for name in names:import_source(base/name,family,namespace)

import_parameter_family(CURRENT/'Apps.XmlUI.ParamTabControl','AppsXmlUi','Apps.XmlUI.ParamTabControl',True)
import_source(CURRENT/'Apps.XmlUI.ParamTabControl/PopupWithTopmost.cs','AppsXmlUi','Apps.XmlUI.ParamTabControl')
import_parameter_family(EXPORT/'VM.PcUI/HikPcUI.ListView','PcUi','HikPcUI.ListView')
for filename in ['NumericUpDown.xaml','NumericUpDown.xaml.cs','SliderArrangeType.cs','BtnHeightConverter.cs','BtnShowConverter.cs','BtnShowGridConverter.cs']:
    import_source(EXPORT/'VM.PcUI/HikPcUI.Controls'/filename,'PcUiControls','HikPcUI.Controls')

# ArrowButtonStyle is an existing original dictionary, not a new approximate arrow button.
original=EXPORT/'VM.PcUI/controls/commomdictionary.xaml';target=THEME/'Shared/CommomDictionary.xaml'
target.parent.mkdir(parents=True,exist_ok=True)
value=transform(text(original),original,'HikPcUI.Controls',NS+'HikPcUI.Controls');target.write_text(value,encoding='utf-8');record(original,target,'original_resource_dictionary')

# The existing runnable reconstruction has its own dictionary identity. It is not
# silently deduplicated with either of the two original assembly dictionaries.
local_slider=Path('D:/MyWork/REA逆向测试/src/VisionUi/SourceSliderDictionary.xaml')
local_slider_target=THEME/'VisionUi/SourceSliderDictionary.xaml'
local_slider_target.parent.mkdir(parents=True,exist_ok=True)
local_slider_target.write_text(transform(text(local_slider),local_slider,'VisionUi',NS+'VisionUi'),encoding='utf-8')
record(local_slider,local_slider_target,'original_resource_dictionary',['complete existing local reconstruction dictionary; separate identity; original slider image pack URI resolved to migrated original assets'])

for source in sorted((EXPORT/'VM.Controls').rglob('*.cs')):
    if source.stem in numeric_names or source.stem in numeric_editors:
        original=re.search(r'^namespace ([\w.]+);',text(source),re.M).group(1)
        import_source(source,'VMControls/'+('Editors' if 'Editors' in original else 'UpDown'),original)

for filename in ['generic.xaml','aero2.normalcolor.xaml']:
    source=EXPORT/'VM.Controls/controls/numericupdown/themes'/filename
    target=THEME/'VMControls/controls/numericupdown/themes'/filename;target.parent.mkdir(parents=True,exist_ok=True)
    value=text(source)
    value=value.replace('clr-namespace:VM.Controls"','clr-namespace:'+NS+'VM.Controls"')
    value=value.replace('xmlns:local=', 'xmlns:display="clr-namespace:SunEyeVision.Plugin.Wpf.Native.Display.VMControls" xmlns:local=',1)
    for support in ['ButtonSpinner','WatermarkTextBox']:
        value=value.replace('local:'+support,'display:'+support)
    for namespace in ['VM.Controls.Primitives','VM.Controls.Core.Converters','VM.Controls.Chromes','VM.Controls.Themes']:
        value=value.replace(namespace,map_namespace(namespace))
    value=re.sub(r'(Source=")[.][.]/[.][.]/Themes/([^\"]+)',
                 lambda m:m.group(1)+PACK+'Themes/Native/Display/VMControls/controls/themes/'+m.group(2).lower(),value)
    # Explicit external support declarations are resolved by the display group's complete type map.
    target.write_text(value,encoding='utf-8');record(source,target,'numeric_updown_theme',['numeric namespace; display support namespace'])

dependencies.update([
 'display:SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives.UpDownBase<T>',
 'display:SunEyeVision.Plugin.Wpf.Native.Display.VMControls.ButtonSpinner/Spinner/WatermarkTextBox',
 'display:SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives.InputBase',
 'display:SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors.TypeEditor<T>',
 'display:controls/themes/{Common,Generic/Glyphs,Aero2/Common,Aero2/Glyphs}.xaml',
 'input:HikPcUI.ListView.BooleanControl/PopupWithTopmost (numeric XAML source references must use input map)',
 'root:Themes/VmFieldStyles.xaml#EditLinkButton and original theme resource keys',
 'framework:Microsoft.Windows.Themes.ListBoxChrome from PresentationFramework.Aero (VM.PcUI NumericUpDown original template; root resolves modern WPF theme assembly)',
])
blocked=[{'source_library':'VMControls.WPF.Release','source_files':['VMControls.WPF.Release.Front/NumbericParamControl.xaml','VMControls.WPF.Release.Front/NumbericParamControl.xaml.cs'],
          'status':'pending_real_dependency_not_compiled','dependency':'FrontendUI.Design.Controls.NumbericParamControl; source export absent',
          'reason':'Wrapper forwards actual Font/behavior API to vendor control; no empty substitute introduced.'}]
for filename in ['NumbericParamControl.xaml','NumbericParamControl.xaml.cs']:
    source=EXPORT/'VMControls.WPF.Release/VMControls.WPF.Release.Front'/filename
    target=CODE/'WpfRelease'/(filename+'.pending');target.parent.mkdir(parents=True,exist_ok=True)
    value=text(source).replace('VMControls.WPF.Release.Front',NS+'VMControls.WPF.Release.Front')
    binding_converter_namespace=dependency_types['VMControls.WPF.Release.Convert.BindingPropertyConvert'].rsplit('.',1)[0]
    value=value.replace('VMControls.WPF.Release.Convert',binding_converter_namespace)
    target.write_text(value,encoding='utf-8');record(source,target,'pending_vendor_wrapper',['namespace only; original vendor dependency retained explicitly'])
    blocked[0].setdefault('pending_targets',[]).append(str(target.relative_to(ROOT)))
manifest={'group':'numeric','base_commit':'b87fb685','phase':'source_migration_no_build_or_tests','source_roots':[str(EXPORT),str(CURRENT)],
 'dependency_manifests':dependency_manifests,
 'resolved_cross_group_types':{name:dependency_types[name] for name in ['VM.Controls.PropertyGrid.PropertyItem','VM.Controls.PropertyGrid.PropertyGridUtilities','VM.Controls.PropertyGrid.Editors.TypeEditor','VM.Controls.Primitives.InputBase','VM.Controls.Primitives.UpDownBase','VM.Controls.ButtonSpinner','VM.Controls.WatermarkTextBox','VMControls.WPF.Release.Convert.BindingPropertyConvert']},
 'interaction_chain':{
    'focus_popup':'NumericControl.ValueText.GotFocus -> IsFocusedP -> templated ComboBox.IsDropDownOpen; CBox_PreviewMouseDown opens original TextBoxView path',
    'text_commit':'ValueText.Text LostFocus binding; PART_Popup_Closed ForceUpdateSource -> DoStartValueChange/DoEndValueChange',
    'step':'UpButton/DownButton -> DoUp/DoDown -> OnNumericClick -> inherited T1/T2 handlers -> original value notifications',
    'wheel':'ValueText_MouseWheel changes only while ValueText.IsFocused and writable; original clamp/extremum behavior unchanged',
    'drag':'Thumb preview down/up -> DragingChangedCommand(true/false); original slider clipping and endpoint notifications retained',
    'source_port_adaptation':'T1_OnNumericClick/T2_OnNumericClick made protected for inherited generated-XAML delegate access, with original bodies unchanged',
    'local_reconstruction_reference':'RoiNumericField was read as an existing behavior reference, not used to replace the original single/double slider implementation',
    'verification':'source chain only; no build or execution in this migration phase'},
 'ownership':{'worktree':str(ROOT),'branch':'codex/vm-controls-numeric','shared_project_files_modified':False,'formal_release_modified':False,'handoff':'root integrates these exact types/resources before unified build; central CURRENT_TASK remains owned by root'},
 'entries':entries,'type_map':types,'cross_group_dependencies':sorted(dependencies),'blocked':blocked,
 'not_claimed':['compilation','runtime usability','visual acceptance','full-library completion'],
 'resource_uri_map':[{ 'source':e['source'],'target_uri':PACK+str(Path(e['target']).relative_to('src/Plugin.Wpf')).replace('\\','/') } for e in entries if e['kind'] in ['xaml_dictionary','xaml_control','original_resource_dictionary','numeric_updown_theme']],
 'library_coverage':{'Apps.XmlUI':'NumericControl/NumbericTextBox; scalar/between/point sliders and original support',
                     'VM.PcUI':'separate NumericControl/sliders plus NumericUpDown with three original converters',
                     'VM.Controls':'13 numeric UpDown declarations and numeric PropertyGrid editors; display supplies common control bases',
                     'VMBaseControls':'no Numeric/Number/Digit/Decimal/Slider/RangeRule/SymbolType declaration in existing export; nonnumeric controls belong to display',
                     'VMControls.WPF':'AdditionRangeResultSelector is workflow/business selection, input group owns; not a numeric editor',
                     'VMControls.WPF.Release':'complete NumbericParamControl wrapper retained .pending, blocked on genuine missing FrontendUI.Design implementation',
                     'Apps.Style':'root owns all original style dictionaries; numeric references original EditLinkButton via VmFieldStyles'},
 'generation_notes':['XAML Page generation must supply InitializeComponent/named fields/IComponentConnector/IStyleConnector; exported code does not duplicate generated methods.',
                     'Inherited numeric click handlers are protected so regenerated derived XAML can wire the original delegates.',
                     'Do not merge the two libraries solely by short class names: AppsXmlUi and PcUi variants retain separate target namespaces.',
                     'Shared csproj, Generic.xaml and Contracts intentionally untouched; root integrates resource build items and dictionaries.']}
out=CODE/'numeric-migration-manifest.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
# Remove only obsolete files from this generator's prior manifest, within its three exclusive directories.
current_targets={e['target'] for e in entries}
for entry in previous_entries:
    obsolete=(ROOT/entry['target']).resolve()
    if entry['target'] not in current_targets and any(obsolete.is_relative_to(folder.resolve()) for folder in (CODE,THEME,ASSET)) and obsolete.is_file():
        obsolete.unlink()
print(json.dumps({'files':len(entries),'types':len(types),'manifest':str(out),'blocked':blocked},ensure_ascii=False))
