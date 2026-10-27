using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
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
    private bool _capturing;
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
    private void Detach(FrameworkElement? retainInput=null)
    {
        _generation++;
        var fields=_fields;var input=_inputContent;
        _fields=null;_inputContent=ReferenceEquals(input,retainInput)?input:null;
        _references=new(StringComparer.Ordinal);
        var cleanup=new List<Action>{()=>Content=null};
        if(fields is not null)
        {
            foreach(var field in fields.Fields)field.Changed-=OnChanged;
            fields.PageChanged-=OnPageChanged;cleanup.Add(fields.Dispose);
        }
        if(input is IDisposable disposable&&!ReferenceEquals(input,retainInput))cleanup.Add(disposable.Dispose);
        ParameterCleanup.Run(cleanup);
    }
    private void Rebuild()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var draft=Draft;var options=Options??new();var optionsIdentity=Options;
        var nextInput=draft is null?null:options.InputContent;
        var generation=_generation+1;
        bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(Draft,draft)&&ReferenceEquals(Options,optionsIdentity);
        DeclaredFieldsView? candidate=null;
        try
        {
            Detach(nextInput);if(!Current()||draft is null)return;
        var schema=draft.Schema;var values=draft.ReadFields();
        if(schema.Presentation is null)throw new ArgumentException("Runtime settings require explicit presentation metadata.");
        var references=options.References?.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
        foreach(var reference in references)
            if(!schema.Inputs.Any(p=>p.Id==reference.Key&&(p.EditModes&FieldEditModes.Reference)!=0))
                throw new ArgumentException("Unknown or non-reference settings field: "+reference.Key);
        candidate=new(schema.Presentation.Fields.Select(presentation=>
        {
            var port=schema.Inputs.Single(p=>p.Id==presentation.FieldId);
            ParameterSetting initial=references.TryGetValue(port.Id,out var reference)?reference:new ConstantSetting(values.Get<object?>(port.Id));
            return(port,presentation,initial);
        }),proposals=>
        {
            if(_disposed||generation!=_generation)throw new InvalidOperationException("The settings view has been rebound or disposed.");
            WriteGroup(proposals);
        },options.ReferenceOptions is {} referenceOptions?referenceOptions with {ValidateProposal=null}:null,
            id=>new ConstantSetting(draft.ReadFields().Get<object?>(id)),options.FormatReference,options.IntegerRadios,options.ValidateComposition,Current);

            if(!Current()){candidate.Dispose();return;}
            if(nextInput is not null)
                candidate.AddToGroup(options.InputSection??throw new ArgumentException("An input content section is required."),nextInput,0);
            if(!Current()){candidate.Dispose();return;}
            candidate.SelectedPageIndex=SelectedPageIndex;
            foreach(var field in candidate.Fields)field.Changed+=OnChanged;
            candidate.PageChanged+=OnPageChanged;
            _fields=candidate;_inputContent=nextInput;_references=references;
            Content=candidate;
        }
        catch(Exception failure)
        {
            try
            {
                ParameterCleanup.Run(new Action[]{()=>{if(Current())Detach();},()=>candidate?.Dispose()});
            }
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    private static void SelectPage(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {var view=(DeclaredSettingsView)sender;if(view._fields is not null)view._fields.SelectedPageIndex=(int)args.NewValue;}
    private void OnPageChanged()=>SetCurrentValue(SelectedPageIndexProperty,_fields!.SelectedPageIndex);
    public bool TrySetFields(IReadOnlyDictionary<string,ParameterSetting> proposals)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        return (_fields??throw new InvalidOperationException("A settings draft is required.")).TrySetFields(proposals);
    }
    private void WriteGroup(IReadOnlyDictionary<string,ParameterSetting> proposals)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var draft=Draft??throw new InvalidOperationException("A settings draft is required.");
        var generation=_generation;var options=Options;
        bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(Draft,draft)&&ReferenceEquals(Options,options);
        var constants=new Dictionary<string,object?>(StringComparer.Ordinal);
        var nextReferences=new Dictionary<string,ReferenceSetting>(_references,StringComparer.Ordinal);
        foreach(var (id,value) in proposals)
        {
            var port=draft.Schema.Inputs.SingleOrDefault(port=>port.Id==id)
                ??throw new ArgumentException("Unknown settings field: "+id);
            if(value is ConstantSetting constant)
            {constants.Add(id,ValueRules.Freeze(port,constant.Value));nextReferences.Remove(id);}
            else if(value is ReferenceSetting reference)
            {
                if((port.EditModes&FieldEditModes.Reference)==0)throw new ArgumentException("Settings field does not allow references.");
                nextReferences[id]=reference;
            }
            else throw new ArgumentException("Settings require a current value or an explicit reference.");
        }
        var frozenReferences=new ReadOnlyDictionary<string,ReferenceSetting>(nextReferences);
        draft.ApplyFields(constants,candidate=>
        {
            if(!Current())throw new InvalidOperationException("The settings view has been rebound or disposed.");
            options?.AcceptCandidate?.Invoke(candidate,frozenReferences);
            if(!Current())throw new InvalidOperationException("The settings view has been rebound or disposed.");
        });
        if(!Current())throw new InvalidOperationException("The settings view has been rebound or disposed.");
        _references=nextReferences;
    }
    private void OnChanged()=>Changed?.Invoke();
    public bool TryCapture(out object settings,out IReadOnlyDictionary<string,ReferenceSetting> references,out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(Draft is null||_fields is null)throw new InvalidOperationException("A settings draft is required.");
        settings=null!;references=new ReadOnlyDictionary<string,ReferenceSetting>(new Dictionary<string,ReferenceSetting>(StringComparer.Ordinal));
        if(_capturing){error="参数捕获正在进行，请等待当前提交完成。";return false;}
        _capturing=true;
        try
        {
            if(!_fields.TryValidate(out error))return false;
            settings=Draft.CaptureSettings();
            references=new ReadOnlyDictionary<string,ReferenceSetting>(new Dictionary<string,ReferenceSetting>(_references,StringComparer.Ordinal));
            return true;
        }
        finally{_capturing=false;}
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        try{Detach();}finally{Changed=null;
        _binding=true;
        try {ClearValue(DraftProperty);ClearValue(OptionsProperty);}
        finally {_binding=false;}}
    }
}
