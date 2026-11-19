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
public sealed class DeclaredInputFieldsView : NativeHost, INativeInputComposition
{
    private readonly NativeParameterPage _page=new();
    private readonly List<DeclaredFieldRow> _fields=new();
    private readonly List<NativeInputField> _inputFields=new();
    private readonly List<(DeclaredFieldRow Field,Action Handler)> _handlers=new();
    private readonly Dictionary<string,NativeParameterRow> _fieldRows=new(StringComparer.Ordinal);
    private readonly Dictionary<string,DeclaredFieldRow> _movedFields=new(StringComparer.Ordinal);
    private readonly HashSet<string> _externallyValidatedFields=new(StringComparer.Ordinal);
    private readonly HashSet<string> _referenceOnlyFields=new(StringComparer.Ordinal);
    private readonly List<IDisposable> _additionalContents=new();
    private bool _disposed;
    private readonly ToolSchema _schema;
    private readonly Func<Port,ParameterSetting> _read;
    private readonly Action<Port,ParameterSetting> _write;
    private readonly ReferenceFieldOptions _references;
    private readonly Func<OutputReference,string>? _formatReference;
    private readonly ReferenceWindowOwner _referenceWindowOwner;
    private readonly bool _ownsReferenceWindowOwner;
    public IReadOnlyList<DeclaredFieldRow> Fields {get;}
    public IReadOnlyList<NativeInputField> InputFields=>_inputFields.AsReadOnly();
    // VM's CustomVisible branches only participate in validation while their
    // projected row is active. Assist/ROI compositions own the hidden branches
    // and expose the active branch through their own commit contract.
    private bool IsActive(DeclaredFieldRow field)
        => !_movedFields.ContainsKey(field.FieldId) && !_externallyValidatedFields.Contains(field.FieldId)
            && field.Visibility == Visibility.Visible && field.IsEnabled;
    /// <summary>Transfers validation ownership to an original composite such as Blob Assist.</summary>
    public void MarkExternallyValidated(IEnumerable<string> fieldIds)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        foreach(var id in fieldIds) _externallyValidatedFields.Add(id);
    }
    public bool HasInvalidInput=>_fields.Any(field=>IsActive(field)&&field.HasInvalidText);
    public string? InvalidInputDiagnostic=>_fields.FirstOrDefault(field=>IsActive(field)&&field.HasInvalidText)?.Error;
    public string InvalidInputFieldIds=>string.Join(",",_fields.Where(field=>IsActive(field)&&field.HasInvalidText).Select(field=>field.FieldId));
    public bool HasPendingEdits=>_fields.Any(field=>IsActive(field)&&field.HasPendingEdits);
    public event Action? Changed;
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId,bool pageKnown,bool currentPage,Func<bool> current,
        IReadOnlyDictionary<string,ConstantSetting?>? currentValues=null)
    {
        var candidates=_fields.Where(field=>IsActive(field)&&(scope==NumericModelReadScope.Field?field.FieldId==fieldId:field.HasNumericModel)).ToArray();
        if(scope==NumericModelReadScope.CurrentPage&&!currentPage)
            return pageKnown?Array.Empty<NumericModelReadResult>():Array.AsReadOnly(candidates.Select(field=>new NumericModelReadResult(field.FieldId,NumericModelReadStatus.NotApplicable,"输入组合未声明可证明的页面归属。" )).ToArray());
        return Array.AsReadOnly(candidates.Select(field=>field.ReadNumericModel(()=>!_disposed&&current(),
            currentValues is not null&&currentValues.TryGetValue(field.FieldId,out var value)?()=>value:null)).ToArray());
    }
    public DeclaredInputFieldsView(ToolSchema schema,Func<Port,ParameterSetting> read,
        Action<Port,ParameterSetting> write,ReferenceFieldOptions references,
        Func<OutputReference,string>? format=null)
    {
        _schema=schema;_read=read;_write=write;
        _ownsReferenceWindowOwner=references.WindowOwner is null;_referenceWindowOwner=references.WindowOwner??new ReferenceWindowOwner(this,()=>!_disposed,new(),false);references=references with{WindowOwner=_referenceWindowOwner};
        _references=references;_formatReference=format;
        Fields=_fields.AsReadOnly();
        try
        {
            Resources.MergedDictionaries.Add(SunEyeVision.Plugin.Wpf.EditorResources.Shared);
            // 未声明输入内容呈现的工具（例如无图像输入、输入仅走参数引用的条件分支）没有可渲染的输入区，
            // 这里视为「无输入内容」而不是抛异常——否则整张参数页组装失败。
            if (schema.Presentation is not { } presentation)
            {
                Style=(Style)FindResource("ParamListViewStyleNoScro");
                return;
            }
            var declarations=presentation.Fields
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
                    var editor=new DeclaredFieldRow(port with{EditModes=FieldEditModes.Reference},
                        field.DisplayName,read(port),setting=>write(port,setting),references,formatReference:format,
                        presentation:field,allowUnboundReference:!port.Required||schema.WorkflowImageInputId==port.Id);
                    _fields.Add(editor);
                    _inputFields.Add(new NativeInputField(field.FieldId, field.Layout?.Section ?? throw new ArgumentException("Input section is required."), field.Order, port, field, editor));
                    if(port.Id==schema.WorkflowImageInputId)
                        editor.UseImageSourceSelector(()=>references.ReadCandidates(port));
                    var row=new NativeParameterRow(metadata,editor,()=>editor.NativeItemState,
                        ()=>{if(!_disposed)ReferenceExpansionCoordinator.ClearSelectionFor(_referenceWindowOwner);});
                    _fieldRows.Add(port.Id,row);
                    _page.Rows.Add(row);
                    Action handler=()=>{row.Refresh();Changed?.Invoke();};_handlers.Add((editor,handler));editor.Changed+=handler;
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
    /// <summary>Project a field changed by an accepted owner edit without proposing a second edit.</summary>
    public void RefreshInputProjection(string id,ConstantSetting? defaultWhenUnset=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var row = _fields.Single(field => field.FieldId == id);
        var state = row.StateForHost;
        ParameterSetting ReadAccepted()
        {
            var value=ReadInputProjection(id);
            return value is UnsetSetting&&defaultWhenUnset is not null?defaultWhenUnset:value;
        }
        var accepted = ReadAccepted();
        if (_referenceOnlyFields.Contains(id) && state.Accepted is UnsetSetting && accepted is ConstantSetting)
            return;
        if (DeclaredFieldState.SameSetting(state.Accepted, accepted)) return;
        row.RetireInteractions();
        if (!_disposed) state.ProjectAcceptedInput(ReadAccepted());
    }
    /// <summary>Creates a real declared input editor for a tool's original composite layout.
    /// The view retains the editor and the same host write/reference callbacks; no second field owner is created.</summary>
    public DeclaredFieldRow ComposeInputEditor(string id,string label,bool numericSequence=false,bool imageSource=false,ConstantSetting? defaultWhenUnset=null,
        bool referenceOnly=false,NativeIntegerRadioOptions? integerRadioOptions=null, bool showClearButton=true)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var port=_schema.Inputs.SingleOrDefault(port=>port.Id==id)??throw new ArgumentException("输入未声明。");
        // 对齐原厂"节点唯一"语义：原厂节点树（NameValueItem）按 id 唯一存在，
        // 切页只是重新呈现节点，从不重建节点。因此同一字段的编辑器实例也必须唯一——
        // 重复请求返回已有实例，而不是抛异常（否则页面重建必然失败）。
        var existing=_fields.FirstOrDefault(field=>field.FieldId==id);
        if(existing is not null)
        {
            if(_fieldRows.ContainsKey(id)) return TakeField(id);
            return existing;
        }
        if(_schema.Presentation!.Fields.Any(field=>field.FieldId==id))
            throw new ArgumentException("输入已有编辑器。");
        var value = _read(port);
        ObjectDisposedException.ThrowIf(_disposed,this);
        if (value is UnsetSetting && defaultWhenUnset is not null) value = defaultWhenUnset;
        if (referenceOnly) _referenceOnlyFields.Add(id);
        // An optional host input with an explicit presentation default is a selected
        // constant in the original radio control. Keep the formal input optional;
        // only its editor projection has no unset branch.
        var editorPort = referenceOnly ? port with { EditModes=FieldEditModes.Reference } : port;
        if (integerRadioOptions is not null && defaultWhenUnset is not null)
            editorPort = editorPort with { Required=true };
        var editor=new DeclaredFieldRow(editorPort,label,value,setting=>_write(port,setting),_references,
            formatReference:_formatReference,allowUnboundReference:!port.Required,numericSequence:numericSequence,
            integerRadioOptions:integerRadioOptions, showClearButton:showClearButton);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            if(imageSource)editor.UseImageSourceSelector(()=>_references.ReadCandidates(port));
            ObjectDisposedException.ThrowIf(_disposed,this);
            if(_fields.Any(field=>field.FieldId==id))throw new InvalidOperationException("输入编辑器在创建期间已被安装。");
            _fields.Add(editor);
            return editor;
        }
        catch{editor.Dispose();throw;}
    }
    public ParameterSetting ReadInputProjection(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        return _read(_schema.Inputs.Single(port=>port.Id==id));
    }
    public string DescribeInputSource(ParameterSetting setting) => setting switch
    {
        ReferenceSetting value => _formatReference?.Invoke(value.Reference) ?? value.Reference.PortId,
        VariableSetting value => _references.FormatVariable?.Invoke(value.Reference) ?? value.Reference.VariableId,
        ConstantSetting value => Convert.ToString(value.Value, System.Globalization.CultureInfo.CurrentCulture) ?? "",
        _ => ""
    };
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
    public DeclaredFieldRow TakeField(string fieldId)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_movedFields.TryGetValue(fieldId,out var moved))return moved;
        if(!_fieldRows.Remove(fieldId,out var row))throw new ArgumentException("输入字段不存在或已移出。",nameof(fieldId));
        var editor=_fields.Single(field=>field.FieldId==fieldId);
        var handler=_handlers.Single(item=>ReferenceEquals(item.Field,editor));
        editor.Changed-=handler.Handler;_handlers.Remove(handler);
        _page.Rows.Remove(row);row.Dispose();
        _movedFields.Add(fieldId,editor);
        return editor;
    }
    public bool TryValidate(out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        foreach(var field in _fields.Where(IsActive))
        {
            if(field.TryCommitPending())continue;
            error=field.Error.Length==0?"输入参数未能提交。":field.Error;
            return false;
        }
        error=_fields.FirstOrDefault(field=>IsActive(field)&&field.HasInvalidText)?.Error??"";
        return error.Length==0;
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
        cleanup.Add(()=>Changed=null);
        _inputFields.Clear();_movedFields.Clear();_externallyValidatedFields.Clear();_referenceOnlyFields.Clear();
        cleanup.Add(()=>{BindingOperations.ClearBinding(this,ChildLeftWidthProperty);BindingOperations.ClearBinding(this,ChildMarginProperty);BindingOperations.ClearBinding(this,ChildHeaderMarginProperty);});
        if(_ownsReferenceWindowOwner)cleanup.Add(_referenceWindowOwner.Dispose);
        ParameterCleanup.Run(cleanup);
    }
}
