using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Text;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using NativeHost=SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView;
using NativeRow=SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

public sealed record NativeDisplayUiDraftEntry(string Label,bool ShowStyleButton,bool InitiallyVisible=true);

/// <summary>Original graphic rows using transient UI state until the host owns display settings.</summary>
public sealed class NativeDisplayUiDraftRows : NativeHost,IDisposable,IParameterVisibility
{
    private readonly ObservableCollection<NativeParameterRow> _rows=[];
    private readonly List<IDisposable> _contents=[];
    private readonly List<DeclaredFieldState> _states=[];
    private bool _disposed;
    private readonly Dictionary<DeclaredFieldState,ParameterSetting> _baseline=[];
    private readonly Dictionary<DeclaredFieldEditor,ParameterSetting> _fieldBaseline=[];
    private readonly Dictionary<NativeColorPresenter,Color> _colorBaseline=[];
    public event Action? Changed;
    public bool HasPendingEdits=>!_disposed&&(_fieldBaseline.Keys.Any(editor=>editor.HasPendingEdits)||_contents.OfType<NativeGraphicStylePresenter>().Any(view=>_baseline.Keys.Any(state=>view.HasPendingEdits(state.Port.Id))));
    public bool HasInvalidInput=>!_disposed&&(_states.Any(state=>state.HasInvalidInput)||_fieldBaseline.Keys.Any(editor=>editor.HasInvalidText));
    public void RetireInteractions()
    {if(_disposed)return;ParameterCleanup.Run(new Action[]{()=>((IParameterVisibility)this).PrepareForVisibility(false),()=>{if(!_disposed&&IsVisible)((IParameterVisibility)this).PrepareForVisibility(true);}});}
    void IParameterVisibility.PrepareForVisibility(bool visible)
    {ParameterCleanup.Run(_contents.OfType<IParameterVisibility>().Select(view=>(Action)(()=>view.PrepareForVisibility(visible&&(view as FrameworkElement)?.Visibility==Visibility.Visible))).Concat(_contents.OfType<NativeColorPresenter>().Select(color=>(Action)(()=>{if(!visible)color.ClosePalette();}))));}
    private void OnRawChanged(object sender,TextChangedEventArgs args)=>OnUiChanged();
    public bool HasUiChanges=>!_disposed&&(_baseline.Any(pair=>pair.Key.HasInvalidInput||!DeclaredFieldState.SameSetting(pair.Key.Accepted,pair.Value))
        ||_fieldBaseline.Any(pair=>pair.Key.HasPendingEdits||!DeclaredFieldState.SameSetting(pair.Key.Setting,pair.Value))
        ||_colorBaseline.Any(pair=>pair.Key.SelectedColor!=pair.Value)
        ||_contents.OfType<NativeGraphicStylePresenter>().Any(view=>_baseline.Keys.Any(state=>view.HasPendingEdits(state.Port.Id))));
    private void OnUiChanged(){if(!_disposed)Changed?.Invoke();}
    private void OnColorChanged(object sender,RoutedPropertyChangedEventArgs<Color> args)=>OnUiChanged();
    protected override Size MeasureOverride(Size availableSize)
    {
        var measured=base.MeasureOverride(availableSize);
        var changed=false;
        void BindRows(DependencyObject parent)
        {
            if(parent is NativeRow row)
            {
                var margin=BindingOperations.GetBinding(row,MarginProperty);
                if(!ReferenceEquals(margin?.Source,this))
                {
                    BindingOperations.SetBinding(row,MarginProperty,new Binding(nameof(ChildMargin)){Source=this});
                    BindingOperations.SetBinding(row,NativeRow.ChildLeftWidthProperty,new Binding(nameof(ChildLeftWidth)){Source=this});
                    changed=true;
                }
            }
            for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)BindRows(VisualTreeHelper.GetChild(parent,i));
        }
        BindRows(this);
        return changed?base.MeasureOverride(availableSize):measured;
    }
    public void BindEntryVisibility(int index,FrameworkElement source)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(source);
        if(index<0||index>=_rows.Count)throw new ArgumentOutOfRangeException(nameof(index));
        BindingOperations.SetBinding(_rows[index].View!,VisibilityProperty,new Binding(nameof(Visibility)){Source=source,Mode=BindingMode.OneWay});
    }
    /// <summary>Borrow the original row views into the caller's existing group; this instance still owns their transient states.</summary>
    public IReadOnlyList<FrameworkElement> ExtractEntryViews()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var rows=_rows.ToArray();var views=rows.Select(row=>row.View!).ToArray();_rows.Clear();
        ParameterCleanup.Run(rows.Select(row=>(Action)row.Dispose));return Array.AsReadOnly(views);
    }
    public NativeDisplayUiDraftRows(string section,IEnumerable<NativeDisplayUiDraftEntry> entries,bool includeTextSettings=false)
    {
        try
        {
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Display/GraphicStyle.xaml",UriKind.Relative)});
        Style=(Style)FindResource("ParamListViewStyleNoScro");
        ChildMargin=new Thickness(32,1,32,0);ChildLeftWidth=100;
        var group=new NativeParameterGroup(section);
        foreach(var entry in entries)
        {
            var groupId="ui-only-graphic-"+_rows.Count;
            var ids=new[]{groupId+".visible",groupId+".ok",groupId+".ng",groupId+".opacity",groupId+".width"};
            var types=new[]{(TypeRef)DataType.Boolean,PresentationDeclarations.Rgba,PresentationDeclarations.Rgba,(TypeRef)DataType.Number,(TypeRef)DataType.Number};
            var values=new object[]{true,PresentationDeclarations.Color(102,255,0),PresentationDeclarations.Color(255,0,0),1d,2d};
            var states=new Dictionary<string,DeclaredFieldState>(StringComparer.Ordinal);
            var presentations=new Dictionary<string,FieldPresentation>(StringComparer.Ordinal);
            for(var i=0;i<ids.Length;i++)
            {
                var state=new DeclaredFieldState(new Port(ids[i],types[i]),new ConstantSetting(values[i]),_=>{});
                _states.Add(state);states.Add(ids[i],state);
                presentations.Add(ids[i],new FieldPresentation(ids[i],entry.Label,section,i,FieldEditorKind.Scalar)
                {Layout=new("结果显示",section,DialogGroup:groupId,DialogTitle:entry.Label,DialogPrimary:i==0,
                    Minimum:i==3?0:i==4?1:null,Maximum:i==3?1:i==4?10:null,Increment:i==3?.01:1)});
            }
            var roles=new NativeGraphicDisplayOptions(groupId,ids[0],ids[1],ids[2],ids[3],ids[4],entry.ShowStyleButton);
            var view=new NativeGraphicStylePresenter(roles,states,presentations,()=>!_disposed);
            view.Visibility=entry.InitiallyVisible?Visibility.Visible:Visibility.Collapsed;
            _contents.Add(view);view.PendingChanged+=OnUiChanged;
            _rows.Add(new NativeParameterRow(group,view));
        }
        if(includeTextSettings)AddTextSettings();
        SetRows(_rows);
        foreach(var state in _states){_baseline.Add(state,state.Accepted);state.Changed+=OnUiChanged;}
        foreach(var editor in _contents.OfType<DeclaredFieldEditor>()){_fieldBaseline.Add(editor,editor.Setting);editor.Changed+=OnUiChanged;editor.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRawChanged));}
        foreach(var color in _contents.OfType<NativeColorPresenter>()){_colorBaseline.Add(color,color.SelectedColor);color.SelectedColorChanged+=OnColorChanged;}
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    private void AddTextSettings()
    {
        var group=new NativeParameterGroup("文本显示");
        void Add(string id,string label,TypeRef type,ParameterSetting initial,FieldLayout layout,FieldEditModes mode=FieldEditModes.Constant,bool text=false,bool inline=false)
        {
            var port=new Port("unavailable.text."+id,type,Required:mode==FieldEditModes.Constant,EditModes:mode);
            var presentation=new FieldPresentation(port.Id,label,"文本显示",_rows.Count,FieldEditorKind.Scalar){Layout=layout};
            DeclaredFieldEditor editor;
            if(inline)
            {
                var state=new DeclaredFieldState(port,initial,_=>{},allowUnboundReference:true);
                _states.Add(state);
                editor=new DeclaredFieldEditor(state,label,ReferenceFieldOptions.Empty,presentation:presentation,
                    allowUnboundReference:true,inlineConstantReference:true);
            }
            else editor=new DeclaredFieldEditor(port,label,initial,_=>{},
                ReferenceFieldOptions.Empty,presentation:presentation,allowUnboundReference:mode==FieldEditModes.Reference);
            _contents.Add(editor);
            // VM's StringConfig, FloatConfig and ButtonSelecter rows have a 32-DIP host.
            ((FrameworkElement)editor.EditorBody).Height=32;
            if(text)editor.UseTextExpressions(new(()=>new(Array.Empty<DeclaredReferenceCandidate>(),_=>throw new InvalidOperationException("当前没有可用来源。"),()=>!_disposed)));
            _rows.Add(new NativeParameterRow(group,editor));
        }
        var layout=new FieldLayout("结果显示","文本显示");
        Add("content","内容",DataType.Text,new ConstantSetting(""),layout,text:true);
        void ColorRow(string label,Color color)
        {
            var picker=new NativeColorPresenter{SelectedColor=color};_contents.Add(picker);
            var row=new NativeRow{Height=32,LeftContent=new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center},RightContent=picker};
            _rows.Add(new NativeParameterRow(group,row));
        }
        ColorRow("OK颜色",Color.FromRgb(102,255,0));ColorRow("NG颜色",Colors.Red);
        Add("size","字号",DataType.Number,new ConstantSetting(10d),layout with {Minimum=6,Maximum=72,Increment=1});
        Add("opacity","透明度",DataType.Number,new ConstantSetting(1d),layout with {Minimum=0,Maximum=1,Increment=.1});
        Add("x","位置X",DataType.Number,new ConstantSetting(0d),layout,FieldEditModes.Constant|FieldEditModes.Reference,inline:true);
        Add("y","位置Y",DataType.Number,new ConstantSetting(0d),layout,FieldEditModes.Constant|FieldEditModes.Reference,inline:true);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var state in _baseline.Keys)state.Changed-=OnUiChanged;
        foreach(var editor in _fieldBaseline.Keys){editor.Changed-=OnUiChanged;editor.RemoveHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRawChanged));}
        foreach(var view in _contents.OfType<NativeGraphicStylePresenter>())view.PendingChanged-=OnUiChanged;
        foreach(var color in _colorBaseline.Keys)color.SelectedColorChanged-=OnColorChanged;Changed=null;
        ParameterCleanup.Run(new Action[]{()=>SetRows(null)}
            .Concat(_rows.Select(row=>(Action)(()=>{if(row.View is {} view)BindingOperations.ClearBinding(view,VisibilityProperty);})))
            .Concat(_rows.Select(row=>(Action)row.Dispose))
            .Concat(_contents.Select(content=>(Action)content.Dispose)).Concat(_states.Select(state=>(Action)state.Dispose))
            .Append(()=>{_rows.Clear();_contents.Clear();_states.Clear();}));
    }
}
