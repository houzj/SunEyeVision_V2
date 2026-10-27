using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Common settings view over Runtime metadata and an independent private model.
/// It does not execute, commit the document, resolve references or keep resources.</summary>
public sealed class DeclaredSettingsView : ContentControl,IToolSettingsView
{
    private Dictionary<string,ReferenceSetting> _references=new(StringComparer.Ordinal);
    private DeclaredFieldsView? _fields;
    private FrameworkElement? _inputContent;
    private long _generation;
    private bool _binding;
    private bool _disposed;
    public static readonly DependencyProperty DraftProperty=DependencyProperty.Register(nameof(Draft),typeof(IToolSettingsDraft),typeof(DeclaredSettingsView),new PropertyMetadata(null,Rebind));
    public static readonly DependencyProperty OptionsProperty=DependencyProperty.Register(nameof(Options),typeof(ToolSettingsViewOptions),typeof(DeclaredSettingsView),new PropertyMetadata(null,Rebind));
    public static readonly DependencyProperty SelectedPageIndexProperty=DependencyProperty.Register(nameof(SelectedPageIndex),typeof(int),typeof(DeclaredSettingsView),
        new FrameworkPropertyMetadata(0,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,SelectPage),value=>(int)value>=0);
    public int SelectedPageIndex {get=>(int)GetValue(SelectedPageIndexProperty);set=>SetValue(SelectedPageIndexProperty,value);}
    public IToolSettingsDraft? Draft {get=>(IToolSettingsDraft?)GetValue(DraftProperty);set=>SetValue(DraftProperty,value);}
    public ToolSettingsViewOptions? Options {get=>(ToolSettingsViewOptions?)GetValue(OptionsProperty);set=>SetValue(OptionsProperty,value);}
    public FrameworkElement View=>this;
    public bool HasInvalidInput=>!_disposed&&_fields?.HasInvalidActiveFields==true;
    public event Action? Changed;
    public DeclaredSettingsView() { }
    public DeclaredSettingsView(IToolSettingsDraft draft,ToolSettingsViewOptions? options=null)=>Bind(draft,options);
    public void Bind(IToolSettingsDraft draft,ToolSettingsViewOptions? options=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(draft);
        _binding=true;
        try {SetCurrentValue(DraftProperty,draft);SetCurrentValue(OptionsProperty,options);}
        finally {_binding=false;}
        Rebuild();
    }
    private static void Rebind(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {var view=(DeclaredSettingsView)sender;if(!view._binding)view.Rebuild();}
    private void Detach()
    {
        _generation++;
        if(_fields is not null)
        {foreach(var field in _fields.Fields)field.Changed-=OnChanged;_fields.PageChanged-=OnPageChanged;}
        if(_inputContent?.Parent is Panel parent)parent.Children.Remove(_inputContent);
        _inputContent=null;_fields=null;Content=null;_references.Clear();
    }
    private void Rebuild()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);Detach();
        if(Draft is not {} draft)return;
        var generation=_generation;var options=Options??new();
        var schema=draft.Schema;var values=draft.ReadFields();
        if(schema.Presentation is null)throw new ArgumentException("Runtime settings require explicit presentation metadata.");
        _references=options.References?.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
        foreach(var reference in _references)
            if(!schema.Inputs.Any(p=>p.Id==reference.Key&&(p.EditModes&FieldEditModes.Reference)!=0))
                throw new ArgumentException("Unknown or non-reference settings field: "+reference.Key);
        _fields=new(schema.Presentation.Fields.Select(presentation=>
        {
            var port=schema.Inputs.Single(p=>p.Id==presentation.FieldId);
            ParameterSetting initial=_references.TryGetValue(port.Id,out var reference)?reference:new ConstantSetting(values.Get<object?>(port.Id));
            return(port,presentation,initial);
        }),(id,value)=>
        {
            if(_disposed||generation!=_generation)throw new InvalidOperationException("The settings view has been rebound or disposed.");
            Write(id,value);
        },options.SelectReference,id=>new ConstantSetting(draft.ReadFields().Get<object?>(id)),options.FormatReference);
        foreach(var field in _fields.Fields)field.Changed+=OnChanged;
        if(options.InputContent is not null)
        {
            _inputContent=options.InputContent;
            _fields.AddToGroup(options.InputSection??throw new ArgumentException("An input content section is required."),_inputContent,0);
        }
        Content=_fields;_fields.SelectedPageIndex=SelectedPageIndex;_fields.PageChanged+=OnPageChanged;
    }
    private static void SelectPage(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {var view=(DeclaredSettingsView)sender;if(view._fields is not null)view._fields.SelectedPageIndex=(int)args.NewValue;}
    private void OnPageChanged()=>SetCurrentValue(SelectedPageIndexProperty,_fields!.SelectedPageIndex);
    private void Write(string id,ParameterSetting value)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(value is ConstantSetting constant)
        {Draft!.ApplyFields(new Dictionary<string,object?>{{id,constant.Value}});_references.Remove(id);}
        else if(value is ReferenceSetting reference)
        {
            var port=Draft!.Schema.Inputs.Single(p=>p.Id==id);
            if((port.EditModes&FieldEditModes.Reference)==0)throw new ArgumentException("Settings field does not allow references.");
            _references[id]=reference;
        }
        else throw new ArgumentException("Settings require a current value or an explicit reference.");
    }
    private void OnChanged()=>Changed?.Invoke();
    public bool TryCapture(out object settings,out IReadOnlyDictionary<string,ReferenceSetting> references,out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(Draft is null||_fields is null)throw new InvalidOperationException("A settings draft is required.");
        settings=Draft.CaptureSettings();references=new ReadOnlyDictionary<string,ReferenceSetting>(new Dictionary<string,ReferenceSetting>(_references,StringComparer.Ordinal));
        return _fields.TryValidate(out error);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        Detach();Changed=null;
        _binding=true;
        try {ClearValue(DraftProperty);ClearValue(OptionsProperty);}
        finally {_binding=false;}
    }
}
