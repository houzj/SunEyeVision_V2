using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Text;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Common settings view over Runtime metadata and an independent private model.
/// It does not execute, commit the document, resolve references or keep resources.</summary>
public sealed class DeclaredSettingsView : ContentControl,IToolSettingsView
{
    private DeclaredFieldsView? _fields;
    private FrameworkElement? _inputContent;
    private IReadOnlyList<FrameworkElement> _additionalContents=Array.Empty<FrameworkElement>();
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
    public bool HasPendingEdits=>!_disposed&&_fields?.HasPendingActiveFields==true;
    public long AcceptedRevision=>Draft?.Revision??throw new InvalidOperationException("A settings draft is required.");
    public IReadOnlyList<string> AcceptedFieldOrder {get;private set;}=Array.Empty<string>();
    public FieldSettings CaptureAcceptedFields()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var draft=Draft??throw new InvalidOperationException("A settings draft is required.");
        var generation=_generation;var revision=draft.Revision;var fields=draft.CaptureFields();
        if(_disposed||generation!=_generation||!ReferenceEquals(Draft,draft)||revision!=draft.Revision)
            throw new InvalidOperationException("参数草稿捕获期间已改变或重新绑定。");
        return fields;
    }
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId=null)
    {
        VerifyAccess();
        if(!Enum.IsDefined(scope))throw new ArgumentOutOfRangeException(nameof(scope));
        if(scope==NumericModelReadScope.Field&&string.IsNullOrWhiteSpace(fieldId))throw new ArgumentException("单项回读必须指定字段。",nameof(fieldId));
        var fields=_fields;var generation=_generation;var draft=Draft;long? batchRevision=null;
        bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(fields,_fields)&&ReferenceEquals(draft,Draft)&&(!batchRevision.HasValue||draft?.Revision==batchRevision.Value);
        if(!Current()||fields is null)return new[]{new NumericModelReadResult(fieldId??"",_disposed?NumericModelReadStatus.Retired:NumericModelReadStatus.Unavailable)};
        var input=_inputContent as DeclaredInputFieldsView;var options=Options??new();
        bool OwnsInput()=>Current()&&ReferenceEquals(input,_inputContent);
        IReadOnlyDictionary<string,ConstantSetting?>? currentValues=null;
        if(scope==NumericModelReadScope.All&&(fields.HasNumericModels||input?.Fields.Any(field=>field.HasNumericModel)==true)
            &&options.ReferenceOptions?.ReadCurrentValues is {} readCurrentValues)
        {
            // A batch optimization must not change the established contract that a failed
            // host read is reported by the individual numeric models while the page remains open.
            batchRevision=draft?.Revision;
            try{currentValues=readCurrentValues();}catch{currentValues=null;}
        }
        if(scope==NumericModelReadScope.Field&&input?.Fields.Any(field=>field.FieldId==fieldId)==true)
            return input.ReadNumericModels(scope,fieldId,true,true,OwnsInput);
        var result=fields.ReadNumericModels(scope,fieldId,Current,currentValues);
        if(input is null||scope==NumericModelReadScope.Field)return result;
        var extra=input.ReadNumericModels(scope,null,fields.HasNumericInputPage(options.InputAfterField,options.InputSection),fields.NumericInputPage(options.InputAfterField,options.InputSection),OwnsInput,currentValues);
        return Array.AsReadOnly(result.Concat(extra).ToArray());
    }
    public event Action? Changed;
    public DeclaredSettingsView() { }
    public DeclaredSettingsView(IToolSettingsDraft draft,ToolSettingsViewOptions? options=null)=>Bind(draft,options);
    public void Bind(IToolSettingsDraft draft,ToolSettingsViewOptions? options=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(draft);
        AcceptedFieldOrder=Array.Empty<string>();
        _binding=true;
        try {SetCurrentValue(DraftProperty,draft);SetCurrentValue(OptionsProperty,options);}
        finally {_binding=false;}
        Rebuild();
    }
    private static void Rebind(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {var view=(DeclaredSettingsView)sender;if(!view._binding)view.Rebuild();}
    private void Detach(FrameworkElement? retainInput=null,IReadOnlySet<FrameworkElement>? retainAdditional=null)
    {
        _generation++;
        var fields=_fields;var input=_inputContent;
        var additional=_additionalContents;
        _additionalContents=additional.Where(item=>retainAdditional?.Contains(item)==true).ToArray();
        _fields=null;_inputContent=ReferenceEquals(input,retainInput)?input:null;
        var cleanup=new List<Action>{()=>Content=null};
        if(fields is not null)
        {
            foreach(var field in fields.Fields)field.Changed-=OnChanged;
            fields.PageChanged-=OnPageChanged;cleanup.Add(fields.Dispose);
        }
        if(input is IDisposable disposable&&!ReferenceEquals(input,retainInput))cleanup.Add(disposable.Dispose);
        cleanup.AddRange(additional.Where(item=>retainAdditional?.Contains(item)!=true).OfType<IDisposable>().Select(item=>(Action)item.Dispose));
        ParameterCleanup.Run(cleanup);
    }
    private void Rebuild()
    {
        AcceptedFieldOrder=Array.Empty<string>();
        ObjectDisposedException.ThrowIf(_disposed,this);
        var draft=Draft;var options=Options??new();var optionsIdentity=Options;
        var nextInput=draft is null?null:options.InputContent;
        var inputFactory=draft is null?null:options.InputContentFactory;
        FrameworkElement? createdInput=nextInput;
        if(nextInput is not null&&inputFactory is not null)throw new ArgumentException("输入内容不能同时声明实例和按页工厂。");
        var additions=draft is null?Array.Empty<KeyValuePair<string,FrameworkElement>>():(options.AdditionalPageContents?.ToArray()??[]);
        var anchored=draft is null?Array.Empty<AnchoredSettingsContent>():(options.AdditionalAnchoredContents?.ToArray()??[]);
        if(anchored.Any(entry=>entry is null||string.IsNullOrWhiteSpace(entry.FieldId)||entry.Content is null))
            throw new ArgumentException("附加内容必须有有效锚点和视图实例。");
        var owned=additions.Select(entry=>entry.Value).Concat(anchored.Select(entry=>entry.Content)).ToArray();
        if(additions.Any(entry=>string.IsNullOrWhiteSpace(entry.Key)||entry.Value is null)
            ||owned.Distinct(ReferenceEqualityComparer.Instance).Count()!=owned.Length
            ||owned.Any(entry=>ReferenceEquals(entry,nextInput)||ReferenceEquals(entry,options.AdditionalAfterContent)))
            throw new ArgumentException("附加内容必须有有效锚点和唯一视图实例。");
        var retained=owned.ToHashSet<FrameworkElement>(ReferenceEqualityComparer.Instance);
        var generation=_generation+1;
        bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(Draft,draft)&&ReferenceEquals(Options,optionsIdentity);
        DeclaredFieldsView? candidate=null;
        try
        {
            Detach(nextInput,retained);if(!Current()||draft is null)return;
        _additionalContents=owned;
        var schema=draft.Schema;var values=draft.CaptureFields();
        if(schema.Presentation is null)throw new ArgumentException("Runtime settings require explicit presentation metadata.");
        var inputPage=options.InputAfterField is {} inputAnchor
            ?schema.Presentation.Fields.FirstOrDefault(field=>field.FieldId==inputAnchor) is {} anchorField?anchorField.Layout?.Page??anchorField.Group:null
            :options.InputSection is {} inputSection
                ?schema.Presentation.Fields.FirstOrDefault(field=>field.Group==inputSection) is {} sectionField?sectionField.Layout?.Page??sectionField.Group
                    :schema.Presentation.Fields.Any(field=>StringComparer.Ordinal.Equals(field.Layout?.Page??field.Group,inputSection))?inputSection:null
                :schema.Presentation.Fields.FirstOrDefault()?.Layout?.Page;
        var images=options.ImageSources?.ToDictionary(pair=>pair.Key,pair=>pair.Value??throw new ArgumentException("图像来源函数不能为空。"),StringComparer.Ordinal);
        T Observe<T>(Func<T> callback)
        {
            if(!Current())throw new InvalidOperationException("文本编辑视图已重绑或释放。");
            var result=callback();
            if(!Current())throw new InvalidOperationException("文本回调期间视图已重绑或释放。");
            return result;
        }
        var texts=options.TextExpressions?.ToDictionary(pair=>pair.Key,pair=>
        {
            var option=pair.Value??throw new ArgumentException("文本表达式配置不能为空。");
            ArgumentNullException.ThrowIfNull(option.ReadCandidates);
            return new TextExpressionOptions(()=>
            {
                var snapshot=Observe(option.ReadCandidates)??throw new ArgumentException("文本来源快照不能为空。");
                ArgumentNullException.ThrowIfNull(snapshot.FormatExpression);ArgumentNullException.ThrowIfNull(snapshot.IsCurrent);
                return snapshot with {FormatExpression=candidate=>Observe(()=>snapshot.FormatExpression(candidate)),
                    IsCurrent=()=>Current()&&snapshot.IsCurrent()&&Current()};
            },option.ValidateText is null?null:text=>Observe(()=>option.ValidateText(text)),
                option.CreateSetting is null?null:proposal=>Observe(()=>option.CreateSetting(proposal)));
        },StringComparer.Ordinal);
        candidate=new(schema.Presentation.Fields.Select(presentation=>
        {
            var port=schema.Inputs.Single(p=>p.Id==presentation.FieldId);
            ParameterSetting initial=values.Read(port.Id);
            return(port,presentation,initial);
        }),proposals=>
        {
            if(_disposed||generation!=_generation)throw new InvalidOperationException("The settings view has been rebound or disposed.");
            WriteGroup(proposals);
        },options.ReferenceOptions is {} referenceOptions?referenceOptions with {ValidateProposal=null}:null,
            id=>draft.CaptureFields().Read(id) as ConstantSetting,options.FormatReference,options.IntegerRadios,options.ValidateComposition,Current,
            images?.ToDictionary(pair=>pair.Key,pair=>(Func<IReadOnlyList<SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate>>)(()=>
            {
                if(!Current())throw new InvalidOperationException("图像来源视图已重绑或释放。");
                var result=pair.Value();
                if(!Current())throw new InvalidOperationException("图像来源读取期间视图已重绑或释放。");
                return result;
            }),StringComparer.Ordinal),options.ParentConditions,options.InlineConstants,texts,options.GraphicDisplays?.ToArray(),options.TextDisplays?.ToArray(),
            options.AssistCompositions?.ToArray(),options.HeaderlessSectionAnchors,options.PageLabelWidths,options.ChoiceDisplays,options.VisibilityDisplays,
            inputFactory is null&&options.PageBuilt is null?null:(page,built)=>
            {
                if(!Current())throw new InvalidOperationException("参数页工厂所属会话已退役。");
                if(inputFactory is not null&&createdInput is null&&StringComparer.Ordinal.Equals(page,inputPage))
                {
                    var content=inputFactory(built)??throw new InvalidOperationException("输入页工厂返回空内容。");
                    createdInput=content;
                    if(options.InputAfterField is {} anchor)built.AddAfterField(anchor,content,options.InputCondition);
                    else built.AddToGroup(options.InputSection??throw new ArgumentException("An input content section is required."),content,0,options.InputCondition);
                }
                var pageInput=StringComparer.Ordinal.Equals(page,inputPage)?createdInput:null;
                options.PageBuilt?.Invoke(page,built,pageInput);
            },SelectedPageIndex,eagerlyComposePages:options.EagerlyComposePages);

            if(!Current()){candidate.Dispose();return;}
            if(nextInput is not null)
            {
                if(options.InputAfterField is { } anchor)
                    candidate.AddAfterField(anchor,nextInput,options.InputCondition);
                else
                    candidate.AddToGroup(options.InputSection??throw new ArgumentException("An input content section is required."),nextInput,0,options.InputCondition);
            }
            if(options.AdditionalAfterContent is {} after)
                candidate.AddAfterField(options.AdditionalAfterField??throw new ArgumentException("附加输入需要关联字段。"),after,options.AdditionalAfterCondition);
            else if(options.AdditionalAfterField is not null||options.AdditionalAfterCondition is not null)
                throw new ArgumentException("附加输入内容缺失。");
            foreach(var addition in anchored.Where(item=>!item.InsertBefore).Reverse().Concat(anchored.Where(item=>item.InsertBefore)))
            {
                if(!Current()){candidate.Dispose();return;}
                candidate.AddAfterField(addition.FieldId,addition.Content,addition.Condition,addition.FollowAnchorVisibility,addition.InsertBefore);
            }
            foreach(var addition in additions)
            {
                if(!Current()){candidate.Dispose();return;}
                candidate.AddToGroup(addition.Key,addition.Value);
            }
            if(!Current()){candidate.Dispose();return;}
            foreach(var source in options.FieldVisibilitySources??new Dictionary<string,FrameworkElement>())candidate.BindFieldVisibilityTo(source.Key,source.Value);
            if(options.SuppressedFieldRows is {} suppressed)candidate.SuppressFieldRows(suppressed);
            candidate.SelectedPageIndex=SelectedPageIndex;
            foreach(var field in candidate.Fields)field.Changed+=OnChanged;
            candidate.PageChanged+=OnPageChanged;
            _fields=candidate;_inputContent=createdInput;
            Content=candidate;
            if(Current()&&ReferenceEquals(_fields,candidate))
            {
                // Synchronizing every numeric model, including hidden pages, is not
                // part of composing the visible settings view. Defer that host read
                // until the dispatcher is idle so opening and first-page layout can
                // complete first. The generation guard preserves rebind/dispose
                // semantics, while explicit callers can still request an immediate
                // All read through the public contract.
                var scheduledGeneration=generation;
                Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, new Action(() =>
                {
                    if(!_disposed&&scheduledGeneration==_generation&&ReferenceEquals(_fields,candidate))
                        ReadNumericModels(NumericModelReadScope.All);
                }));
            }
        }
        catch(Exception failure)
        {
            try
            {
                ParameterCleanup.Run(new Action[]{()=>{if(Current())Detach();},()=>candidate?.Dispose(),()=>{if(createdInput is IDisposable disposable&&!ReferenceEquals(createdInput,_inputContent))disposable.Dispose();}});
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
        var order=Array.AsReadOnly(proposals.Keys.ToArray());
        draft.ApplyFields(proposals,candidate=>
        {
            if(!Current())throw new InvalidOperationException("The settings view has been rebound or disposed.");
            options?.AcceptCandidate?.Invoke(candidate);
            if(!Current())throw new InvalidOperationException("The settings view has been rebound or disposed.");
        });
        if(!Current())throw new InvalidOperationException("The settings view has been rebound or disposed.");
        AcceptedFieldOrder=order;
    }
    private void OnChanged()=>Changed?.Invoke();
    public bool TryCapture(out FieldSettings fields,out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(Draft is null||_fields is null)throw new InvalidOperationException("A settings draft is required.");
        fields=null!;
        if(_capturing){error="参数捕获正在进行，请等待当前提交完成。";return false;}
        _capturing=true;
        try
        {
            var draft=Draft;var view=_fields;var generation=_generation;
            bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(draft,Draft)&&ReferenceEquals(view,_fields);
            var uiError=Options?.ValidateUiState?.Invoke();
            if(!Current()){error="界面校验期间参数视图已重新绑定或释放。";return false;}
            if(!string.IsNullOrEmpty(uiError)){error=uiError;return false;}
            if(!view.TryValidate(out error))return false;
            if(!Current()){error="参数视图已经重新绑定。";return false;}
            var revision=draft.Revision;
            try{fields=draft.CaptureValidatedFields();}
            catch(Exception failure)when(failure is ArgumentException or InvalidOperationException or FormatException or OverflowException)
            {error=failure.Message;return false;}
            if(!Current()||revision!=draft.Revision||view.HasPendingActiveFields)
            {fields=null!;error="参数捕获期间视图已重新绑定或发生新的编辑。";return false;}
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
