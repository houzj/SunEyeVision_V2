using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using NativeHost=SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamListView;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Original grouped input rows, without an inner scroll owner.
/// The accepting settings view owns this composition; field values remain host-owned.</summary>
public sealed class DeclaredInputFieldsView : NativeHost,IDisposable
{
    private readonly NativeParameterPage _page=new();
    private readonly List<DeclaredFieldEditor> _fields=new();
    private readonly List<(DeclaredFieldEditor Field,Action Handler)> _handlers=new();
    private readonly Dictionary<string,NativeParameterRow> _fieldRows=new(StringComparer.Ordinal);
    private readonly List<IDisposable> _additionalContents=new();
    private bool _disposed;
    private readonly ToolSchema _schema;
    private readonly Func<Port,ParameterSetting> _read;
    private readonly Action<Port,ParameterSetting> _write;
    private readonly ReferenceFieldOptions _references;
    private readonly Func<OutputReference,string>? _formatReference;
    private readonly ReferenceWindowOwner _referenceWindowOwner;
    private readonly bool _ownsReferenceWindowOwner;
    public IReadOnlyList<DeclaredFieldEditor> Fields {get;}
    internal IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId,bool pageKnown,bool currentPage,Func<bool> current,
        IReadOnlyDictionary<string,ConstantSetting?>? currentValues=null)
    {
        var candidates=_fields.Where(field=>scope==NumericModelReadScope.Field?field.FieldId==fieldId:field.HasNumericModel).ToArray();
        if(scope==NumericModelReadScope.CurrentPage&&!currentPage)
            return pageKnown?Array.Empty<NumericModelReadResult>():Array.AsReadOnly(candidates.Select(field=>new NumericModelReadResult(field.FieldId,NumericModelReadStatus.NotApplicable,"输入组合未声明可证明的页面归属。" )).ToArray());
        return Array.AsReadOnly(candidates.Select(field=>field.ReadNumericModel(()=>!_disposed&&current(),
            currentValues is not null&&currentValues.TryGetValue(field.FieldId,out var value)?()=>value:null)).ToArray());
    }
    public DeclaredInputFieldsView(ToolSchema schema,Func<Port,ParameterSetting> read,
        Action<Port,ParameterSetting> write,ReferenceFieldOptions references,
        Func<OutputReference,string>? format=null,
        Func<Port,IReadOnlyList<DeclaredReferenceCandidate>>? referenceCandidates=null)
    {
        _schema=schema;_read=read;_write=write;
        _ownsReferenceWindowOwner=references.WindowOwner is null;_referenceWindowOwner=references.WindowOwner??new ReferenceWindowOwner(this,()=>!_disposed,new(),false);references=references with{WindowOwner=_referenceWindowOwner};
        _references=references;_formatReference=format;
        Fields=_fields.AsReadOnly();
        using var sharedResourceScope=SunEyeVision.Plugin.Wpf.EditorResources.BeginSharedResourceScope();
        try
        {
            Resources.MergedDictionaries.Add(SunEyeVision.Plugin.Wpf.EditorResources.Create());
            var declarations=(schema.Presentation??throw new ArgumentException("Input presentation is required.")).Fields
                .Select(field=>(Field:field,Port:schema.Inputs.Single(port=>port.Id==field.FieldId),
                    Section:field.Layout?.Section??throw new ArgumentException("Input section is required."))).ToArray();
            if(declarations.Select(item=>item.Port.Id).Distinct(StringComparer.Ordinal).Count()!=declarations.Length)
                throw new ArgumentException("Duplicate input presentation.");
            Style=(Style)FindResource("ParamListViewStyleNoScro");
            foreach(var property in new[]{ChildLeftWidthProperty,ChildMarginProperty,ChildHeaderMarginProperty})
                SetBinding(property,new Binding(property.Name){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(NativeHost),1)});
            foreach(var group in declarations.GroupBy(item=>item.Section))
            {
                var metadata=new NativeParameterGroup(group.Key);
                foreach(var (field,port,_) in group)
                {
                    var editor=new DeclaredFieldEditor(port with{EditModes=FieldEditModes.Reference},
                        field.DisplayName,read(port),setting=>write(port,setting),references,formatReference:format,
                        presentation:field,allowUnboundReference:!port.Required||schema.WorkflowImageInputId==port.Id);
                    _fields.Add(editor);
                    if(port.Id==schema.WorkflowImageInputId)
                        editor.UseImageSourceSelector(()=>referenceCandidates?.Invoke(port)??Array.Empty<DeclaredReferenceCandidate>());
                    var row=new NativeParameterRow(metadata,editor,()=>editor.NativeItemState,
                        ()=>{if(!_disposed)ReferenceExpansionCoordinator.ClearSelectionFor(_referenceWindowOwner);});
                    _fieldRows.Add(port.Id,row);
                    _page.Rows.Add(row);
                    Action handler=row.Refresh;_handlers.Add((editor,handler));editor.Changed+=handler;
                }
            }
            SetRows(_page.Rows);
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;
        }
    }
    /// <summary>A tool's nonvisual pure-value input uses the same formal owner callbacks; no extra field state is retained.</summary>
    public ParameterSetting ReadUnpresentedInput(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var port=Unpresented(id);return _read(port);
    }
    /// <summary>Creates a real declared input editor for a tool's original composite layout.
    /// The view retains the editor and the same host write/reference callbacks; no second field owner is created.</summary>
    public DeclaredFieldEditor ComposeInputEditor(string id,string label,bool numericSequence=false)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var port=_schema.Inputs.SingleOrDefault(port=>port.Id==id)??throw new ArgumentException("输入未声明。");
        if(_fields.Any(field=>field.FieldId==id)||_schema.Presentation!.Fields.Any(field=>field.FieldId==id))
            throw new ArgumentException("输入已有编辑器。");
        var editor=new DeclaredFieldEditor(port,label,_read(port),setting=>_write(port,setting),_references,
            formatReference:_formatReference,allowUnboundReference:!port.Required,numericSequence:numericSequence);
        _fields.Add(editor);return editor;
    }
    public void WriteUnpresentedInput(string id,ParameterSetting setting)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);var port=Unpresented(id);
        if(setting is not UnsetSetting)_=FieldSettings.Freeze(port,setting);
        else if(port.Required)throw new ArgumentException("必填输入不能清除。");
        _write(port,setting);
    }
    private Port Unpresented(string id)
    {
        var port=_schema.Inputs.SingleOrDefault(port=>port.Id==id)??throw new ArgumentException("输入未声明。");
        if(_schema.Presentation!.Fields.Any(field=>field.FieldId==id)||port.Type.Shape==ValueShape.Resource
            ||port.EditModes!=FieldEditModes.Constant)throw new ArgumentException("此入口仅供未呈现的纯值常量输入。");
        return port;
    }
    /// <summary>Transfers ownership of a real extra composition into the specified input section.</summary>
    public void AddAfterField(string fieldId,FrameworkElement content,string? section=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(content);
        if(!_fieldRows.TryGetValue(fieldId,out var anchor))throw new ArgumentException("输入字段不存在。",nameof(fieldId));
        if(content.Parent is not null)throw new ArgumentException("输入组合已有父元素。",nameof(content));
        var row=new NativeParameterRow(section is null?anchor.GroupName:new NativeParameterGroup(section),content);
        try
        {
            _page.Rows.Insert(_page.Rows.IndexOf(anchor)+1,row);
            if(content is IDisposable disposable)_additionalContents.Add(disposable);
        }
        catch {row.Dispose();throw;}
    }
    /// <summary>Move an input visual to its associated settings row; this host retains its field ownership.</summary>
    public DeclaredFieldEditor TakeField(string fieldId)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!_fieldRows.Remove(fieldId,out var row))throw new ArgumentException("输入字段不存在或已移出。",nameof(fieldId));
        var editor=_fields.Single(field=>field.FieldId==fieldId);
        var handler=_handlers.Single(item=>ReferenceEquals(item.Field,editor));
        editor.Changed-=handler.Handler;_handlers.Remove(handler);
        _page.Rows.Remove(row);row.Dispose();
        return editor;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        var cleanup=new List<Action>();
        foreach(var (field,handler) in _handlers)cleanup.Add(()=>field.Changed-=handler);
        _handlers.Clear();
        cleanup.Add(()=>SetRows(null));cleanup.Add(_page.Dispose);
        cleanup.AddRange(_additionalContents.Select(content=>(Action)content.Dispose));_additionalContents.Clear();_fieldRows.Clear();
        cleanup.AddRange(_fields.Select(field=>(Action)field.Dispose));
        cleanup.Add(()=>{BindingOperations.ClearBinding(this,ChildLeftWidthProperty);BindingOperations.ClearBinding(this,ChildMarginProperty);BindingOperations.ClearBinding(this,ChildHeaderMarginProperty);});
        if(_ownsReferenceWindowOwner)cleanup.Add(_referenceWindowOwner.Dispose);
        ParameterCleanup.Run(cleanup);
    }
}
