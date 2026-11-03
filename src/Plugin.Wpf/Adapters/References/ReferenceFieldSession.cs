using SunEyeVision.Plugin.Wpf.Interop.Windows;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

internal sealed record ReferenceTreeNode(string Name,IReadOnlyList<ReferenceTreeNode> EnumEntrysA,
    DeclaredReferenceCandidate? Candidate=null,bool Clear=false,bool VariableManager=false)
{
    public bool IsChecked {get;set;}
}

/// <summary>Private display, browsing and index-edit buffer over one borrowed accepted field state.</summary>
internal sealed class ReferenceFieldSession : INotifyPropertyChanged,IDisposable
{
    private readonly DeclaredFieldState _state;
    private readonly Func<ParameterSetting,bool> _accept;
    private readonly Func<bool>? _commitGroup;
    private readonly Action<ReferenceExpansionRequest>? _expand;
    private readonly Func<ReferenceExpansionPreference> _preference;
    private readonly Func<bool> _isExpanded;
    private ReferenceExpansionRequest? _expansion;
    private long _expansionGeneration;
    internal Port Target=>_state.Port;
    private readonly ReferenceFieldOptions _options;
    private readonly Action<VariableSelectionRequest>? _selectVariable;
    private VariableSelectionRequest? _variableRequest;
    private long _variableGeneration;
    private IReadOnlyList<VariableSelectionItem> _variables=Array.Empty<VariableSelectionItem>();
    private IReadOnlyList<VariableSelectionItem> _indexVariables=Array.Empty<VariableSelectionItem>();
    private readonly bool _allowConstantText;
    private bool CanBrowse=>!_disposed&&!_state.IsDisposed&&(_allowConstantText||_state.IsReferenceMode);
    private readonly Func<OutputReference,string>? _format;
    private IReadOnlyList<DeclaredReferenceCandidate> _candidates=Array.Empty<DeclaredReferenceCandidate>();
    private IReadOnlyList<DeclaredReferenceCandidate> _indices=Array.Empty<DeclaredReferenceCandidate>();
    private string? _pendingText;
    private sealed record PendingReference(ParameterSetting Setting,long Acceptance);
    private PendingReference? _pendingReference;
    private bool HasPendingReference=>_pendingReference is {} pending&&pending.Acceptance==_state.AcceptanceVersion;
    private ParameterSetting? DisplaySetting=>HasPendingReference?_pendingReference!.Setting:
        !_state.ReferenceCleared&&ReferenceSettingParts.IsReference(_state.Accepted)?_state.Accepted:null;
    private long _pendingVersion;
    private void SetPending(string? value){if(_pendingText==value)return;_pendingText=value;_pendingVersion++;}
    private bool _disposed,_committing,_sourceOpen,_indexOpen,_suspended;
    private long _uiGeneration;
    private long _popupGeneration;
    internal long PopupGeneration=>_popupGeneration;
    private long _candidateGeneration;
    private bool _refreshing,_candidatesValid;
    internal void SetSuspended(bool suspended){if(_suspended==suspended)return;_suspended=suspended;_uiGeneration++;if(suspended)InvalidateVariableSelection();}
    internal bool IsSuspended=>_suspended;
    private ReferenceTreeNode? _sourceNode,_indexNode;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? CloseRequested;
    public IReadOnlyList<ReferenceTreeNode> SourceNodes {get;private set;}=Array.Empty<ReferenceTreeNode>();
    public IReadOnlyList<ReferenceTreeNode> IndexNodes {get;private set;}=Array.Empty<ReferenceTreeNode>();
    public ReferenceTreeNode? SelectedSourceNode {get=>_sourceNode;set=>_sourceNode=value;}
    public ReferenceTreeNode? SelectedIndexNode {get=>_indexNode;set=>_indexNode=value;}
    public bool HasPendingText=>_pendingText is not null||HasPendingReference;
    public bool ReadOnlyA=>!_allowConstantText&&!_options.EditableIndex;
    public bool IsTextPopupVisible=>_expansion is null;
    public bool SubscribeInPopWindowEnableA=>_expand is not null;
    internal void ClearExpandedSelection(ReferenceExpansionRequest request)
    {
        if(_disposed||!ReferenceEquals(_expansion,request)||!request.IsValid)return;
        request.SetIndex(request.IsIndex,false);Notify(nameof(IsSelectedSubscribeParam));
    }
    public bool IsSelectedSubscribeParam=>_expansion is {IsValid:true,HasSelection:true};
    public bool IsDropDownOpen {get=>_sourceOpen;set{if(_sourceOpen==value)return;_sourceOpen=value;if(value)_popupGeneration++;Notify(nameof(IsDropDownOpen));}}
    public bool IsTextDropDownOpen {get=>_indexOpen;set{if(_indexOpen==value)return;_indexOpen=value;if(value)_popupGeneration++;Notify(nameof(IsTextDropDownOpen));}}
    public string CurValueA {get=>_pendingText??AcceptedText();set{ObserveText(value);if(!_suspended&&value.Length!=0&&_pendingText is not null)TryCommitPendingFromUi();}}
    public ICommand DropDownCommand {get;}
    public ICommand ClickCommand {get;}
    public ICommand IndexClickCommand {get;}
    public ICommand FocusedCommand {get;}
    public ICommand PopSubscribeWindowCommand {get;}

    public ReferenceFieldSession(DeclaredFieldState state,ReferenceFieldOptions options,Func<OutputReference,string>? format,Func<ParameterSetting,bool>? accept=null,Func<bool>? commitGroup=null,Action<ReferenceExpansionRequest>? expand=null,Func<ReferenceExpansionPreference>? preference=null,bool allowConstantText=false,Action<VariableSelectionRequest>? selectVariable=null,Func<bool>? isExpanded=null)
    {
        ArgumentNullException.ThrowIfNull(options.ReadCandidates);
        _state=state;_options=options;_format=format;_accept=accept??state.TrySet;_commitGroup=commitGroup;_expand=options.Expand??expand;_allowConstantText=allowConstantText;_selectVariable=options.SelectVariable??selectVariable;
        var localPreference=new ReferenceExpansionPreference();_preference=preference??(()=>localPreference);_isExpanded=isExpanded??(()=>options.WindowOwner?.IsExpanded==true);
        DropDownCommand=new SessionCommand(_=>{if(_suspended||!CanBrowse)return;if(_isExpanded())Expand(false);else Refresh();});
        ClickCommand=new SessionCommand(value=>SelectSource(Unwrap(value)));
        IndexClickCommand=new SessionCommand(value=>SelectIndex(Unwrap(value)));
        FocusedCommand=new SessionCommand(FocusChanged);
        PopSubscribeWindowCommand=new SessionCommand(_=>Expand(false));
        _state.Changed+=OnStateChanged;
    }

    internal static ReferenceTreeNode? Unwrap(object? value)=>(value is ObjectEventArgs args?args.Param:value) as ReferenceTreeNode;
    private string Label(OutputReference source)
    {
        var candidate=_candidates.FirstOrDefault(item=>DeclaredFieldState.SameSource(item.Reference(BindingSelection.All),source));
        var all=new OutputReference(source.NodeId,source.PortId,source.Type,source.MemberFieldPath,BindingSelection.All,source.ScopeId,source.Storage);
        return candidate?.Label??_format?.Invoke(all)??source.NodeId+" · "+source.PortId;
    }
    private string Label(ParameterSetting source)=>source switch {
        ReferenceSetting module=>Label(module.Reference),
        VariableSetting variable=>_variables.FirstOrDefault(item=>ReferenceSettingParts.SameSource(item.Reference,variable.Reference)) is {} item
            ?AppLocalizationService.GetLang("Lang_GlobalVariable","全局变量")+"."+item.Name:_options.FormatVariable?.Invoke(variable.Reference)??"已选择变量",
        _=>""};
    private string AcceptedText()
    {
        if(!HasPendingReference&&!_state.ReferenceCleared&&_allowConstantText&&_state.Accepted is ConstantSetting number)
            return number.Value switch{long integer=>integer.ToString(CultureInfo.InvariantCulture),double real when NumericStorageRules.Effective(_state.Port.Type,_state.Port.Storage)==NumericStorage.Float32=>((float)real).ToString("R",CultureInfo.InvariantCulture),double real=>real.ToString("R",CultureInfo.InvariantCulture),_=>""};
        if(DisplaySetting is not {} source)return "";
        var label=Label(source);var selection=ReferenceSettingParts.Selection(source);
        return label+"["+(selection switch
        {
            BindingSelection.AtConstant index=>index.Index.ToString(CultureInfo.InvariantCulture),
            BindingSelection.AtField index=>_format?.Invoke(index.Reference)??index.Reference.NodeId+" · "+index.Reference.PortId,
            BindingSelection.AtVariable index=>_indexVariables.FirstOrDefault(item=>ReferenceSettingParts.SameSource(item.Reference,index.Reference)) is {} item
                ?AppLocalizationService.GetLang("Lang_GlobalVariable","全局变量")+"."+item.Name:_options.FormatVariable?.Invoke(index.Reference)??"已选择索引变量",
            _=>""
        })+"]";
    }
    private static TypeRef? ProjectedType(ParameterSetting reference)
    {
        try{return ReferenceSettingParts.Type(reference);}
        catch(Exception error)when(error is ArgumentException or InvalidOperationException){return null;}
    }
    private bool HasSequenceReference=>DisplaySetting is {} reference&&ProjectedType(reference)?.Shape==ValueShape.Sequence;
    private bool NeedsSelectionRepair(ParameterSetting reference)=>ProjectedType(reference) is {} type
        &&DeclaredReferenceTree.RequiresSelectionRepair(type,_state.Port,ReferenceSettingParts.Selection(reference),ReferenceSettingParts.Conversion(reference),_state.ReferenceBehavior);
    public void Refresh()
    {
        if(_disposed||_state.IsDisposed||_refreshing)return;
        InvalidateVariableSelection();var generation=++_candidateGeneration;var uiGeneration=_uiGeneration;var acceptance=_state.AcceptanceVersion;
        bool Current()=>!_disposed&&!_state.IsDisposed&&generation==_candidateGeneration&&uiGeneration==_uiGeneration&&acceptance==_state.AcceptanceVersion;
        _candidatesValid=false;_refreshing=true;
        try
        {
            IsTextDropDownOpen=false;if(!Current())return;
            ReferenceCandidates snapshot;
            IReadOnlyList<VariableSelectionItem> variables;
            IReadOnlyList<VariableSelectionItem> indexVariables;
            IReadOnlyList<ReferenceTreeNode> sources,indices;
            try
            {
                snapshot=_options.ReadCandidates(_state.Port)??throw new ArgumentException("来源目录返回了空快照。");
                if(!Current())return;
                sources=DeclaredReferenceTree.Build(snapshot.Sources.Where(candidate=>DeclaredReferenceTree.Matches(candidate,
                    _state.Port,ReferenceSettingParts.Conversion(_state.Accepted),true,_state.ReferenceBehavior)),true);
                var eligibleIndices=Array.AsReadOnly(snapshot.Indices.Where(candidate=>ParameterReferenceRules.CanUseIndexSource(candidate.SelectedType,_state.ReferenceBehavior)).ToArray());
                indices=DeclaredReferenceTree.Build(eligibleIndices,true);
                indexVariables=Array.AsReadOnly(snapshot.Variables.Where(item=>ParameterReferenceRules.CanUseIndexSource(item.Reference.Type,_state.ReferenceBehavior)&&item.Reference.Selection is BindingSelection.AllItems).ToArray());
                variables=Array.AsReadOnly(snapshot.Variables.Where(item=>DeclaredReferenceTree.Matches(item.Reference,_state.Port,ReferenceSettingParts.Conversion(_state.Accepted),true,_state.ReferenceBehavior)).ToArray());
                if(_selectVariable is not null&&variables.Count!=0)sources=Array.AsReadOnly(sources.Append(new ReferenceTreeNode(AppLocalizationService.GetLang("Lang_VariableManager","变量管理"),Array.Empty<ReferenceTreeNode>(),VariableManager:true)).ToArray());
                if(_selectVariable is not null&&indexVariables.Count!=0)indices=Array.AsReadOnly(indices.Append(new ReferenceTreeNode(AppLocalizationService.GetLang("Lang_VariableManager","变量管理"),Array.Empty<ReferenceTreeNode>(),VariableManager:true)).ToArray());
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException)
            {if(Current())_state.SetSourceError(error.Message);return;}
            if(!Current())return;
            _candidates=Array.AsReadOnly(DeclaredReferenceTree.Descendants(sources).Where(node=>node.Candidate is not null).Select(node=>node.Candidate!).ToArray());_indices=Array.AsReadOnly(snapshot.Indices.Where(candidate=>ParameterReferenceRules.CanUseIndexSource(candidate.SelectedType,_state.ReferenceBehavior)).ToArray());_variables=variables;_indexVariables=indexVariables;SourceNodes=sources;IndexNodes=indices;_candidatesValid=true;
            RestoreSelection(false);
            ParameterCleanup.Run(new Action[]{()=>{if(Current())_state.SetSourceError(DisplaySetting is {} displayed&&!SourceAvailable(displayed)?"来源或索引已删除、不可订阅或声明已改变，请重新选择。":"");}}
                .Concat(new[]{nameof(SelectedSourceNode),nameof(SelectedIndexNode),nameof(SourceNodes),nameof(IndexNodes),nameof(CurValueA)}
                    .Select(property=>(Action)(()=>{if(Current())Notify(property);}))));
        }
        finally{_refreshing=false;}
    }

    private void RestoreSelection(bool notify=true)
    {
        _sourceNode=null;_indexNode=null;
        if(DisplaySetting is {} source)
        {
            if(source is ReferenceSetting module)
                _sourceNode=DeclaredReferenceTree.Descendants(SourceNodes).FirstOrDefault(node=>node.Candidate is {} candidate
                    &&DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),module.Reference));
            else _sourceNode=SourceNodes.FirstOrDefault(node=>node.VariableManager);
            if(ReferenceSettingParts.Selection(source) is BindingSelection.AtField index)
                _indexNode=DeclaredReferenceTree.Descendants(IndexNodes).FirstOrDefault(node=>node.Candidate is {} candidate
                    &&DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),index.Reference));
            else if(ReferenceSettingParts.Selection(source) is BindingSelection.AtVariable)
                _indexNode=IndexNodes.FirstOrDefault(node=>node.VariableManager);
        }
        if(notify)ParameterCleanup.Run(new Action[]{()=>Notify(nameof(SelectedSourceNode)),()=>Notify(nameof(SelectedIndexNode))});
    }
    private void SelectSource(ReferenceTreeNode? node)
    {
        if(_suspended||!CanBrowse||!ContainsSourceNode(node)||node!.EnumEntrysA.Count!=0)return;
        if(node.Clear){ClearSource();return;}
        if(node.VariableManager){OpenVariableSelector();return;}
        if(node.Candidate is not {} candidate)return;
        SelectSourceSetting(selection=>new ReferenceSetting(candidate.Reference(selection),ReferenceSettingParts.Conversion(_state.Accepted)));
    }
    private bool SelectSourceSetting(Func<BindingSelection,ParameterSetting> create)
    {
        var old=DisplaySetting;var selection=old is null?BindingSelection.All:ReferenceSettingParts.Selection(old);
        var acceptance=_state.AcceptanceVersion;var generation=_uiGeneration;
        var referenceMode=_state.IsReferenceMode;var referenceCleared=_state.ReferenceCleared;
        bool UnacceptedCurrent()=>!_disposed&&!_state.IsDisposed&&acceptance==_state.AcceptanceVersion&&generation==_uiGeneration
            &&referenceMode==_state.IsReferenceMode&&referenceCleared==_state.ReferenceCleared;
        try
        {
            var proposal=create(selection);
            if(_pendingText is not {} raw)return Submit(proposal);
            // Source identity comes only from the selected declaration. Preserve the
            // original bracket suffix, including invalid pending text, across a source change.
            if(ReadOnlyA)throw new ArgumentException("来源文字只读；请取消原文或重新选择来源。");
            var bracket=raw.IndexOf('[');
            if(bracket<0)throw new ArgumentException("待编辑原文没有完整索引后缀，请取消或修正原文后再选择来源。");
            if(!_candidatesValid||!SourceAvailable(proposal))throw new ArgumentException("来源或索引已失效，请刷新后重新选择。");
            _pendingReference=new(proposal,_state.AcceptanceVersion);_pendingVersion++;
            SetPending(Label(proposal)+raw[bracket..]);
            if(!UnacceptedCurrent())return false;
            RestoreSelection();if(!UnacceptedCurrent())return false;
            Notify(nameof(CurValueA));if(!UnacceptedCurrent())return false;
            return TryCommitPendingFromUi();
        }
        catch(Exception error)when((error is ArgumentException or FormatException or OverflowException or InvalidOperationException)&&UnacceptedCurrent())
        {
            _state.SetEditError(error.Message);
            if(UnacceptedCurrent()){RestoreSelection();if(UnacceptedCurrent())Notify(nameof(CurValueA));}
            return false;
        }
    }
    internal static void ValidateSelectionShape(ParameterSetting reference)
    {
        if(ReferenceSettingParts.Selection(reference) is not BindingSelection.AllItems&&ProjectedType(reference) is not {Shape:ValueShape.Sequence})
            throw new ArgumentException("当前标量声明不支持索引；已保留原来源和索引，不会自动改为ALL。");
    }
    private bool TryStageReference(ParameterSetting reference)
    {
        if(_options.EditableIndex&&!_suspended&&CanBrowse&&NeedsSelectionRepair(reference))
        {
            _pendingReference=new(reference,_state.AcceptanceVersion);_pendingVersion++;SetPending(null);
            var version=_pendingVersion;var popupGeneration=_popupGeneration;
            _state.SetEditError("请选择有效的序列索引后再确认。");
            if(_disposed||version!=_pendingVersion||!HasPendingReference)return true;
            RestoreSelection();Notify(nameof(CurValueA));
            if(popupGeneration==_popupGeneration)RequestClose();
            return true;
        }
        return false;
    }
    // Candidate identities include declaration type/storage and member path. Captions never rebind a source.
    private bool SourceAvailable(ParameterSetting setting)=>(setting switch {
        ReferenceSetting module=>_candidates.Any(candidate=>DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),module.Reference)),
        VariableSetting variable=>_variables.Any(candidate=>ReferenceSettingParts.SameSource(candidate.Reference,variable.Reference)),_=>false})
        &&(ReferenceSettingParts.Selection(setting) switch {
            BindingSelection.AtField index=>_indices.Any(candidate=>DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),index.Reference)),
            BindingSelection.AtVariable index=>_indexVariables.Any(candidate=>ReferenceSettingParts.SameSource(candidate.Reference,index.Reference)),
            _=>true});
    private void OpenVariableSelector(bool index=false)
    {
        var items=index?_indexVariables:_variables;
        if(_selectVariable is null||_suspended||!CanBrowse||!_candidatesValid||items.Count==0||index&&(ReadOnlyA||!HasSequenceReference))return;
        InvalidateVariableSelection();var generation=_variableGeneration;var acceptance=_state.AcceptanceVersion;
        var uiGeneration=_uiGeneration;var candidates=_candidateGeneration;
        RequestClose();
        if(_suspended||!CanBrowse||generation!=_variableGeneration||acceptance!=_state.AcceptanceVersion||uiGeneration!=_uiGeneration||candidates!=_candidateGeneration)return;
        var request=new VariableSelectionRequest(this,generation,acceptance,uiGeneration,candidates,items,index);_variableRequest=request;
        try{_selectVariable(request);}
        catch(Exception failure)
        {
            try{request.Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    internal bool IsVariableRequestCurrent(VariableSelectionRequest request,long generation,long acceptance,long uiGeneration,long candidates)
        =>!_suspended&&CanBrowse&&_candidatesValid&&ReferenceEquals(_variableRequest,request)&&generation==_variableGeneration
            &&acceptance==_state.AcceptanceVersion&&uiGeneration==_uiGeneration&&candidates==_candidateGeneration&&(!request.IsIndex||!ReadOnlyA&&HasSequenceReference);
    internal bool SelectVariable(VariableSelectionRequest request,VariableSelectionItem item)
    {
        if(!request.IsValid||!(request.IsIndex?_indexVariables:_variables).Any(candidate=>ReferenceEquals(candidate,item)))return false;
        if(request.IsIndex)
        {
            if(DisplaySetting is not {} source)return false;
            return SubmitAndRetire(request,()=>Submit(ReferenceSettingParts.WithSelection(source,new BindingSelection.AtVariable(item.Reference,_state.ReferenceBehavior))));
        }
        return SubmitAndRetire(request,()=>SelectSourceSetting(selection=>new VariableSetting(
            new VariableReference(item.Reference.SolutionId,item.Reference.VariableId,item.Reference.Type,selection,item.Reference.Storage),
            ReferenceSettingParts.Conversion(_state.Accepted))));
    }
    private static bool SubmitAndRetire(VariableSelectionRequest request,Func<bool> submit)
    {
        bool accepted;
        try{accepted=submit();}
        catch(Exception failure)
        {
            try{request.Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
        request.Dispose();return accepted;
    }
    internal void ReleaseVariableRequest(VariableSelectionRequest request,long generation)
    {if(generation==_variableGeneration&&ReferenceEquals(_variableRequest,request))InvalidateVariableSelection();}
    internal void InvalidateVariableSelection()
    {_variableGeneration++;var prior=_variableRequest;_variableRequest=null;prior?.Invalidate();}
    private void SelectIndex(ReferenceTreeNode? node)
    {
        if(ReadOnlyA||_suspended||!CanBrowse||!ContainsIndexNode(node)||DisplaySetting is not {} source
            ||!HasSequenceReference||node is null||node.EnumEntrysA.Count!=0)return;
        if(node.VariableManager){OpenVariableSelector(true);return;}
        var selection=node.Clear?BindingSelection.All:node.Candidate is {} candidate
            ?new BindingSelection.AtField(candidate.Reference(BindingSelection.All),_state.ReferenceBehavior):null;
        if(selection is null)return;
        Submit(ReferenceSettingParts.WithSelection(source,selection));
    }
    private bool Submit(ParameterSetting reference,bool explicitCommit=false)
    {
        try{ValidateSelectionShape(reference);}
        catch(ArgumentException error){_state.SetEditError(error.Message);RestoreSelection();Notify(nameof(CurValueA));return false;}
        if(!_candidatesValid||!SourceAvailable(reference))
        {_state.SetSourceError("来源或索引已失效，请刷新后重新选择。");RestoreSelection();return false;}
        if(!explicitCommit&&TryStageReference(reference))return false;
        return AcceptSetting(reference,explicitCommit);
    }
    private bool AcceptSetting(ParameterSetting proposal,bool explicitCommit=false)
    {
        if(_suspended&&!explicitCommit||_disposed||_state.IsDisposed)return false;
        var generation=_uiGeneration;var version=_state.AcceptanceVersion;var pendingVersion=_pendingVersion;var popupGeneration=_popupGeneration;bool accepted;
        try{accepted=_accept(proposal);}
        catch(Exception failure)when(_state.AcceptanceVersion!=version)
        {
            if(pendingVersion==_pendingVersion)SetPending(null);
            if(!_disposed&&!_state.IsDisposed&&generation==_uiGeneration)
                try{OnStateChanged();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
        if(_disposed||_state.IsDisposed)return accepted;
        if(accepted)
        {
            if(pendingVersion==_pendingVersion)SetPending(null);
            if(_disposed||_state.IsDisposed||generation!=_uiGeneration)return true;
            ParameterCleanup.Run(new Action[]{()=>{if(popupGeneration==_popupGeneration)RequestClose();},
                ()=>{if(!_disposed&&!_state.IsDisposed&&generation==_uiGeneration)OnStateChanged();}});return true;
        }
        if(generation==_uiGeneration){RestoreSelection();if(!_disposed)Notify(nameof(CurValueA));}return false;
    }

    public void ObserveText(string text)
    {
        if(_disposed||text==CurValueA)return;
        if(!_suspended&&text.Length==0){ClearSource();return;}
        SetPending(text);
    }
    private void ClearSource()
    {
        if(_pendingReference is not null){_pendingReference=null;_pendingVersion++;}
        if(!_state.Port.Required){AcceptSetting(new UnsetSetting());return;}
        var generation=_uiGeneration;var pendingVersion=_pendingVersion;var popupGeneration=_popupGeneration;
        if(!ClearReference()){RestoreSelection();Notify(nameof(CurValueA));return;}
        if(_disposed||_state.IsDisposed||generation!=_uiGeneration)return;
        if(pendingVersion==_pendingVersion)SetPending(null);
        ParameterCleanup.Run(new Action[]{OnStateChanged,()=>{if(popupGeneration==_popupGeneration)RequestClose();}});
    }
    private bool ClearReference()
    {
        if(_state.Port.Required){_state.ClearReference();return !_disposed&&!_state.IsDisposed&&_state.ReferenceCleared;}
        return _accept(new UnsetSetting());
    }
    public bool TryPreparePending(out ParameterSetting proposal)
    {
        proposal=null!;
        if(_disposed||_state.IsDisposed)return false;
        try
        {
            if(!HasPendingReference&&_pendingText is null&&!_state.IsReferenceMode&&_state.Accepted is ConstantSetting constant)
            {proposal=constant;return true;}
            if(_allowConstantText&&_pendingText is {} text)
            {
                object? value=null;
                if(_state.Port.Type.Scalar==ScalarKind.Int64&&long.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var integer))value=integer;
                else if(_state.Port.Type.Scalar==ScalarKind.Float64)
                {
                    if(NumericStorageRules.Effective(_state.Port.Type,_state.Port.Storage)==NumericStorage.Float32)
                    {if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var single))value=(double)single;}
                    else if(double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var real))value=real;
                }
                if(value is not null){proposal=new ConstantSetting(ValueRules.Freeze(_state.Port,value));return !_disposed&&!_state.IsDisposed;}
            }
            if(_state.SourceError.Length!=0&&!HasPendingReference)return false;
            if(HasPendingReference&&(!_candidatesValid||!SourceAvailable(_pendingReference!.Setting)))
                throw new ArgumentException("待确认来源已失效，请重新选择。");
            if(DisplaySetting is not {} source)throw new ArgumentException("请先选择来源，再编辑索引。");
            var referenceSetting=source;
            proposal=referenceSetting;
            if(_pendingText is not null&&ReadOnlyA)throw new ArgumentException("来源文字只读；待编辑原文已保留，请取消或重新选择。");
            if(_pendingText is null)
            {
                ValidateSelectionShape(source);
                if(NeedsSelectionRepair(source))throw new ArgumentException("请选择有效的序列索引后再确认。");
                return true;
            }
            if(!_options.EditableIndex)throw new ArgumentException("请输入有效数值，或选择来源。");
            var prefix=Label(source)+"[";
            var projected=ProjectedType(source);
            if(projected is null||!_pendingText.StartsWith(prefix,StringComparison.Ordinal)
                ||!_pendingText.EndsWith("]",StringComparison.Ordinal))throw new ArgumentException("只能编辑当前来源括号中的索引。");
            var content=_pendingText[prefix.Length..^1];
            BindingSelection selection=content.Length==0?BindingSelection.All
                :new BindingSelection.AtConstant(int.Parse(content,NumberStyles.None,CultureInfo.InvariantCulture));
            if(selection is not BindingSelection.AllItems&&projected.Shape!=ValueShape.Sequence)throw new ArgumentException("当前标量声明不支持索引，请明确清空括号内容；不会自动改为ALL。");
            if(selection is BindingSelection.AtConstant index&&projected.Shape==ValueShape.Sequence&&projected.Count.Maximum is int maximum&&index.Index>=maximum)
                throw new ArgumentException("序号超出来源声明的最大数量。");
            var selected=ReferenceSettingParts.WithSelection(source,selection);
            if(NeedsSelectionRepair(selected))throw new ArgumentException("请选择有效的序列索引后再确认。");
            proposal=selected;
            return !_disposed&&!_state.IsDisposed;
        }
        catch(Exception error)when(error is ArgumentException or FormatException or OverflowException)
        {_state.SetEditError(error.Message);return false;}
    }
    public bool TryCommitPending()=>CommitPending(null);
    internal bool TryCommitPendingFromUi()=>!_suspended&&CommitPending(_uiGeneration);
    private bool CommitPending(long? uiGeneration)
    {
        // Explicit owner capture is allowed offscreen; only UI-triggered writes are suspended.
        if(_disposed||_state.IsDisposed)return false;
        if(_commitGroup is not null)return _commitGroup();
        if(_committing)return !_state.HasInvalidInput;
        if(_pendingText is null&&!HasPendingReference)
        {
            if(!_state.IsReferenceMode&&_state.Accepted is ConstantSetting constant&&_state.SourceError.Length!=0)
                return AcceptSetting(constant);
            return !_state.HasInvalidInput;
        }
        if(ReadOnlyA&&_pendingText is not null){_state.SetEditError("来源文字只读；待编辑原文已保留，请取消或重新选择。");return false;}
        _committing=true;
        try
        {
            if(!TryPreparePending(out var proposal))return false;
            if(uiGeneration is {} generation&&(_suspended||generation!=_uiGeneration))return false;
            if(proposal is ReferenceSetting)return AcceptSetting(proposal,true);
            return AcceptSetting(proposal,true);
        }
        finally{_committing=false;}
    }
    public void ProjectAccepted(){if(_pendingReference is not null){_pendingReference=null;_pendingVersion++;}SetPending(null);OnStateChanged();}

    private void FocusChanged(object? value)
    {
        if(_suspended||!CanBrowse||value is not TextBoxFocusEventArgs {Text:{} text} args)return;
        if(!args.IsFocus){IsTextDropDownOpen=false;return;}
        if(ReadOnlyA||args.SelectionStart<1||args.SelectionStart>text.Length)return;
        var before=text[..args.SelectionStart];var after=text[args.SelectionStart..];
        var inside=_options.EditableIndex&&HasSequenceReference
            &&before.LastIndexOf('[')>before.LastIndexOf(']')&&after.IndexOf(']')>=0
            &&(after.IndexOf('[')<0||after.IndexOf(']')<after.IndexOf('['));
        if(_expansion?.IsValid==true){_expansion.SetIndex(inside,inside);Notify(nameof(IsSelectedSubscribeParam));RequestClose();return;}
        if(_isExpanded()){Expand(inside,inside);return;}
        if(inside)
        {
            var generation=_uiGeneration;Refresh();
            if(_suspended||_disposed||_state.IsDisposed||generation!=_uiGeneration)return;
            IsDropDownOpen=false;
        }
        IsTextDropDownOpen=inside;
    }
    internal bool ContainsSourceNode(object? node)=>!_disposed&&!_refreshing&&_candidatesValid&&Contains(SourceNodes,node);
    internal bool ContainsIndexNode(object? node)=>!_disposed&&!_refreshing&&_candidatesValid&&Contains(IndexNodes,node);
    private static bool Contains(IReadOnlyList<ReferenceTreeNode> roots,object? node)
        =>node is not null&&roots.Any(item=>ReferenceEquals(item,node)||Contains(item.EnumEntrysA,node));
    internal bool IsExpansionCurrent(ReferenceExpansionRequest request,long generation)=>!_suspended&&CanBrowse
        &&(!request.IsIndex||!ReadOnlyA&&HasSequenceReference)
        &&generation==_expansionGeneration&&ReferenceEquals(_expansion,request);
    internal void ReleaseExpansion(ReferenceExpansionRequest request,long generation)
    {
        if(generation!=_expansionGeneration||!ReferenceEquals(_expansion,request))return;
        InvalidateExpansion();
    }
    public void InvalidateExpansion()
    {
        _expansionGeneration++;var prior=_expansion;_expansion=null;
        ParameterCleanup.Run(new Action[]{()=>prior?.Invalidate(),
            ()=>Notify(nameof(IsSelectedSubscribeParam)),()=>Notify(nameof(IsTextPopupVisible))});
    }
    private void Expand(bool index,bool hasSelection=true)
    {
        if(_suspended||!CanBrowse||_expand is null)return;
        var generation=_expansionGeneration+1;InvalidateExpansion();
        if(generation!=_expansionGeneration)return;
        Refresh();
        if(_suspended||!CanBrowse||generation!=_expansionGeneration)return;
        if(index&&(ReadOnlyA||!HasSequenceReference))return;
        RequestClose();
        if(_suspended||!CanBrowse||generation!=_expansionGeneration)return;
        var preference=_preference();
        if(_suspended||!CanBrowse||generation!=_expansionGeneration)return;
        var request=new ReferenceExpansionRequest(this,generation,preference,index);_expansion=request;request.SetIndex(index,hasSelection);
        try
        {
            ParameterCleanup.Run(new Action[]{()=>Notify(nameof(IsSelectedSubscribeParam)),()=>Notify(nameof(IsTextPopupVisible))});
            if(request.IsValid)_expand(request);
        }
        catch(Exception failure)
        {
            try{request.Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    private void OnStateChanged()
    {
        if(_disposed)return;
        if(_pendingReference is {} pending&&(pending.Acceptance!=_state.AcceptanceVersion||!CanBrowse))_pendingReference=null;
        if(_variableRequest is {} variableRequest&&!variableRequest.IsValid)InvalidateVariableSelection();
        if(!CanBrowse||_expansion?.IsIndex==true&&!HasSequenceReference)InvalidateExpansion();
        RestoreSelection();Notify(nameof(CurValueA));
    }
    public void Close()
    {
        var source=_sourceOpen;var index=_indexOpen;_sourceOpen=false;_indexOpen=false;
        ParameterCleanup.Run(new Action[]{()=>{if(source)Notify(nameof(IsDropDownOpen));},
            ()=>{if(index)Notify(nameof(IsTextDropDownOpen));}});
    }
    private void RequestClose()
    {
        var generation=_popupGeneration;
        ParameterCleanup.Run(new Action[]{Close,()=>{if(!_disposed&&!_state.IsDisposed&&generation==_popupGeneration)CloseRequested?.Invoke();}});
    }
    private void Notify(string name){if(!_disposed)PropertyChanged?.Invoke(this,new(name));}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_candidateGeneration++;_candidatesValid=false;_pendingReference=null;_pendingText=null;_pendingVersion++;
        ParameterCleanup.Run(new Action[]{InvalidateVariableSelection,InvalidateExpansion,Close,()=>_state.Changed-=OnStateChanged,
            ()=>{PropertyChanged=null;CloseRequested=null;}});
    }
    private sealed class SessionCommand(Action<object?> action) : ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>action(parameter);
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
}
