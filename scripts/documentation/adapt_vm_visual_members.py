"""Reviewed framework/member adapters for already imported visual controls."""
from pathlib import Path
import hashlib,json,re

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'src/Plugin.Wpf/Controls/Native/Display'
MANIFEST=BASE/'display-migration-manifest.json'
RECEIPT=BASE/'visual-host-adaptations.json'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def adapt(name,text):
    if name in ('ListBoxBehavior.cs','TreeViewScrollToBottomBehavior.cs'):
        assert '((Behavior)this).OnAttached();' in text and '((Behavior)this).OnDetaching();' in text
        return text.replace('((Behavior)this).OnAttached();','base.OnAttached();').replace('((Behavior)this).OnDetaching();','base.OnDetaching();')
    if name=='EditRoundedWindow.xaml':
        assert 'Name="sliderArrangeRounded"' in text and 'x:Name="sliderArrangeRounded"' not in text
        return text.replace('Name="sliderArrangeRounded"','x:Name="sliderArrangeRounded"')
    if name=='EditableTextBlock.cs':
        for variable,owner in (('_editableTextBoxHeader','TextBox'),('_textBlockHeader','TextBlock')):
            old=variable+'.Binding(this, "Text", "Text");';assert old in text
            text=text.replace(old,variable+'.SetBinding('+owner+'.TextProperty, new System.Windows.Data.Binding("Text") { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });')
        marker='\t\tbase.OnApplyTemplate();'
        cleanup='''\t\tif (_editableTextBoxHeader != null)
\t\t{
\t\t\t_editableTextBoxHeader.IsVisibleChanged -= editableTextBoxHeader_IsVisibleChanged;
\t\t\t_editableTextBoxHeader.KeyDown -= editableTextBoxHeader_KeyDown;
\t\t\t_editableTextBoxHeader.LostFocus -= editableTextBoxHeader_LostFocus;
\t\t}
\t\tif (_textBlockHeader != null) _textBlockHeader.MouseLeftButtonDown -= textBlockHeader_MouseLeftButtonDown;
'''
        assert text.count(marker)==1
        return text.replace(marker,cleanup+marker)
    if name=='HikMessageBox.xaml.cs':
        methods=[]
        for button,result in (('Ok','Ok'),('Yes','Yes'),('No','No'),('Cancel','Cancel')):
            pattern=r'\t\t'+button+r'Btn.Click \+= \(object s, RoutedEventArgs e\) =>\s*\{\s*DialogResult = DialogResult\.'+result+r';\s*\};'
            text,count=re.subn(pattern,'\t\t'+button+'Btn.Click += On'+button+'Click;',text);assert count==1
            old=button+'Btn.RemoveEvents();';assert old in text
            text=text.replace(old,button+'Btn.Click -= On'+button+'Click;')
            methods.append('\tprivate void On'+button+'Click(object sender, RoutedEventArgs args) => DialogResult = DialogResult.'+result+';')
        assert '\t\tthis.RemoveEvents();\n' in text
        text=text.replace('\t\tthis.RemoveEvents();\n','')
        return text.replace('\tprivate void Initialize()','\n'.join(methods)+'\n\n\tprivate void Initialize()')
    if name=='ShapeAndRoiMarginCvt.cs':
        assert 'private static bool ImageROIOffsetEnable { get; set; }' in text
        text=text.replace('private static bool ImageROIOffsetEnable { get; set; }','public bool ImageROIOffsetEnable { get; set; }')
        pattern=r'\n\t\tif \(bool.TryParse\(ConfigurationHelper.GetConfig\("ImageROIOffsetEnable"\), out var result\)\)\s*\{\s*ImageROIOffsetEnable = result;\s*\}'
        text,count=re.subn(pattern,'',text);assert count==1
        return text
    if name=='MultiLineROI.cs':
        assert 'typeof(HikMultiLineRoiParameter)' in text
        text=text.replace('typeof(HikMultiLineRoiParameter)','typeof(IMultiLineROI)')
        old='HikMultiLineRoiParameter hikMultiLineRoiParameter = DataContext as HikMultiLineRoiParameter;';assert old in text
        text=text.replace(old,'var polygonPoints = PolygonPoints;\n\t\tif (polygonPoints == null || polygonPoints.Count < 2) return -1;')
        text=text.replace('hikMultiLineRoiParameter.PolygonPoints','polygonPoints')
        start=text.index('\tprivate int GetIndex(');end=text.index('\tprivate double PointToSegDist(',start)
        body=text[start:end].replace('Tuple<PolygonPoint, PolygonPoint>','Tuple<Point, Point>')
        body=body.replace('item.Value.Item1, item.Value.Item2, curPoint','item.Value.Item1, item.Value.Item2, new Point(curPoint.X, curPoint.Y)')
        text=text[:start]+body+text[end:]
        text=text.replace('PointToSegDist(PolygonPoint Point1, PolygonPoint Point2, PolygonPoint curPoint)','PointToSegDist(Point Point1, Point Point2, Point curPoint)')
        text=text.replace('GetMinDistance(PolygonPoint pt1, PolygonPoint pt2, PolygonPoint pt3)','GetMinDistance(Point pt1, Point pt2, Point pt3)')
        return text
    if name=='HelpDocumentUtil.cs':
        assert 'BrowserUtil.OpenModuleHelpUrl(' in text
        return text.replace('BrowserUtil.OpenModuleHelpUrl(', 'SunEyeVision.Plugin.Wpf.Native.Common.NativeHelpDocument.OpenModuleHelpUrl(')
    raise AssertionError(name)

def main():
    manifest=json.loads(MANIFEST.read_text(encoding='utf-8'));receipt=json.loads(RECEIPT.read_text(encoding='utf-8'))
    protected={row['target']:row for row in receipt['files']};changes=[]
    names=('ListBoxBehavior.cs','TreeViewScrollToBottomBehavior.cs','EditRoundedWindow.xaml','EditableTextBlock.cs','HikMessageBox.xaml.cs','ShapeAndRoiMarginCvt.cs','MultiLineROI.cs','HelpDocumentUtil.cs')
    for name in names:
        entry=next(row for row in manifest['files'] if Path(row['target']).name==name)
        path=ROOT/entry['target'];source=Path(entry['source']);assert sha(source)==entry['sourceSha256'].lower()
        previous=protected.get(entry['target'])
        if previous:
            assert sha(path)==previous['adapted_sha256'], 'Review current adapted output before changing: '+str(path)
            if 'visual_member_adapter' in previous:continue
        changes.append((entry,path,source,adapt(name,path.read_text(encoding='utf-8')),previous))
    for entry,path,source,text,previous in changes:
        path.write_text(text,encoding='utf-8',newline='\r\n')
        entry['targetSha256']=sha(path);entry['gitNormalizedTargetSha256']=hashlib.sha256(text.encode()).hexdigest()
        record=previous or {'target':entry['target'],'original_source':str(source),'original_source_sha256':sha(source)}
        record.update({'adapted_sha256':sha(path),'visual_member_adapter':True})
        if not previous:receipt['files'].append(record)
        entry['adaptations'].append('Reviewed framework/member adapter; see adapt_vm_visual_members.py and visual-host-adaptations.json; not runtime acceptance.')
    if changes:
        receipt['changes'].extend([
            'Behavior的反编译显式基类cast改为真正base调用；圆角窗口使用x:Name生成原滑条字段。',
            'EditableTextBlock按已知Text依赖属性直接TwoWay绑定，换模板先解绑旧模板事件；不迁入反射BindingHelper。',
            'HikMessageBox只解绑自己注册的四个具名按钮事件；保留BaseWindow原关闭清理，不再反射清空所有事件。',
            'ShapeAndRoiMarginCvt半像素公式保留，开关改为实例选项，默认false；不读取厂商全局App.config，也不宣称当前默认已与VM安装配置一致。',
            'MultiLineROI使用原IMultiLineROI绘制角色及自身PolygonPoints依赖属性，距离计算保留double坐标；不绑定Hik XML参数模型。',
            '帮助读取原helpUrl/GuidTemp布局并定位本方Release的Help目录，显式点击时调用系统浏览器；未读取厂商安装路径、注册表或单实例服务，未补造VM帮助内容，尚未执行浏览器打开验收。'])
        MANIFEST.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
        RECEIPT.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps({'adapted_files':len(changes),'runtime_verified':False}))

if __name__=='__main__':main()
