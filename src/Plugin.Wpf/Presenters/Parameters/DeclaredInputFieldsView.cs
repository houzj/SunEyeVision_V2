using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using NativeHost=SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView;

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
    private readonly ReferenceWindowOwner _referenceWindowOwner;
    private readonly bool _ownsReferenceWindowOwner;
    public IReadOnlyList<DeclaredFieldEditor> Fields {get;}
    public DeclaredInputFieldsView(ToolSchema schema,Func<Port,ParameterSetting> read,
        Action<Port,ParameterSetting> write,ReferenceFieldOptions references,
        Func<OutputReference,string>? format=null,
        Func<Port,IReadOnlyList<DeclaredReferenceCandidate>>? referenceCandidates=null)
    {
        _ownsReferenceWindowOwner=references.WindowOwner is null;_referenceWindowOwner=references.WindowOwner??new ReferenceWindowOwner(this,()=>!_disposed,new(),false);references=references with{WindowOwner=_referenceWindowOwner};
        Fields=_fields.AsReadOnly();
        try
        {
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
