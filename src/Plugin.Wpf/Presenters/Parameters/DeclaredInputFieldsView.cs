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
    private bool _disposed;
    public IReadOnlyList<DeclaredFieldEditor> Fields {get;}
    public DeclaredInputFieldsView(ToolSchema schema,Func<Port,ParameterSetting> read,
        Action<Port,ParameterSetting> write,ReferenceFieldOptions references,
        Func<OutputReference,string>? format=null,
        Func<Port,IReadOnlyList<DeclaredReferenceCandidate>>? referenceCandidates=null)
    {
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
                        ()=>{if(!_disposed&&Window.GetWindow(this) is {} owner)ReferenceExpansionCoordinator.ClearSelectionFor(owner);});
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
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        var cleanup=new List<Action>();
        foreach(var (field,handler) in _handlers)cleanup.Add(()=>field.Changed-=handler);
        _handlers.Clear();
        cleanup.Add(()=>SetRows(null));cleanup.Add(_page.Dispose);
        cleanup.AddRange(_fields.Select(field=>(Action)field.Dispose));
        cleanup.Add(()=>{BindingOperations.ClearBinding(this,ChildLeftWidthProperty);BindingOperations.ClearBinding(this,ChildMarginProperty);BindingOperations.ClearBinding(this,ChildHeaderMarginProperty);});
        ParameterCleanup.Run(cleanup);
    }
}
