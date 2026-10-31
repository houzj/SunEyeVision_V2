using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

internal sealed record ReferenceTreeNode(string Name,IReadOnlyList<ReferenceTreeNode> EnumEntrysA,
    DeclaredReferenceCandidate? Candidate=null,bool Clear=false)
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
    private ReferenceExpansionRequest? _expansion;
    private long _expansionGeneration;
    internal Port Target=>_state.Port;
    private readonly ReferenceFieldOptions _options;
    private readonly bool _allowConstantText;
    private bool CanBrowse=>!_disposed&&!_state.IsDisposed&&(_allowConstantText||_state.IsReferenceMode);
    private readonly Func<OutputReference,string>? _format;
    private IReadOnlyList<DeclaredReferenceCandidate> _candidates=Array.Empty<DeclaredReferenceCandidate>();
    private string? _pendingText;
    private sealed record PendingReference(OutputReference Reference,long Acceptance);
    private PendingReference? _pendingReference;
    private bool HasPendingReference=>_pendingReference is {} pending&&pending.Acceptance==_state.AcceptanceVersion;
    private OutputReference? DisplayReference=>HasPendingReference?_pendingReference!.Reference:
        !_state.ReferenceCleared&&_state.Accepted is ReferenceSetting accepted?accepted.Reference:null;
    private long _pendingVersion;
    private void SetPending(string? value){if(_pendingText==value)return;_pendingText=value;_pendingVersion++;}
    private bool _disposed,_committing,_sourceOpen,_indexOpen,_suspended;
    private long _uiGeneration;
    private long _popupGeneration;
    internal long PopupGeneration=>_popupGeneration;
    private long _candidateGeneration;
    private bool _refreshing,_candidatesValid;
    internal void SetSuspended(bool suspended){if(_suspended==suspended)return;_suspended=suspended;_uiGeneration++;}
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

    public ReferenceFieldSession(DeclaredFieldState state,ReferenceFieldOptions options,Func<OutputReference,string>? format,Func<ParameterSetting,bool>? accept=null,Func<bool>? commitGroup=null,Action<ReferenceExpansionRequest>? expand=null,Func<ReferenceExpansionPreference>? preference=null,bool allowConstantText=false)
    {
        ArgumentNullException.ThrowIfNull(options.ReadCandidates);
        _state=state;_options=options;_format=format;_accept=accept??state.TrySet;_commitGroup=commitGroup;_expand=options.Expand??expand;_allowConstantText=allowConstantText;
        var localPreference=new ReferenceExpansionPreference();_preference=preference??(()=>localPreference);
        DropDownCommand=new SessionCommand(_=>{if(_suspended||!CanBrowse)return;if(_preference().PreferExpanded)Expand(false);else Refresh();});
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
    private string AcceptedText()
    {
        if(!HasPendingReference&&!_state.ReferenceCleared&&_allowConstantText&&_state.Accepted is ConstantSetting number)
            return number.Value switch{long integer=>integer.ToString(CultureInfo.InvariantCulture),double real when NumericStorageRules.Effective(_state.Port.Type,_state.Port.Storage)==NumericStorage.Float32=>((float)real).ToString("R",CultureInfo.InvariantCulture),double real=>real.ToString("R",CultureInfo.InvariantCulture),_=>""};
        if(DisplayReference is not {} source)return "";
        var label=Label(source);
        return ProjectedType(source)?.Shape!=ValueShape.Sequence&&source.Selection is BindingSelection.AllItems?label:label+" ["+(source.Selection switch
        {
            BindingSelection.AtConstant index=>index.Index.ToString(CultureInfo.InvariantCulture),
            BindingSelection.AtField index=>_format?.Invoke(index.Reference)??index.Reference.NodeId+" · "+index.Reference.PortId,
            _=>""
        })+"]";
    }
    private static TypeRef? ProjectedType(OutputReference reference)
    {
        try{return DeclaredReferenceCandidate.ProjectType(reference.Type,reference.MemberFieldPath);}
        catch(Exception error)when(error is ArgumentException or InvalidOperationException){return null;}
    }
    private bool HasSequenceReference=>DisplayReference is {} reference&&ProjectedType(reference)?.Shape==ValueShape.Sequence;
    private bool NeedsSelectionRepair(OutputReference reference)=>ProjectedType(reference) is {} type
        &&DeclaredReferenceTree.RequiresSelectionRepair(type,_state.Port,reference.Selection,
            (_state.Accepted as ReferenceSetting)?.Conversion??BindingConversion.Automatic);

    public void Refresh()
    {
        if(_disposed||_state.IsDisposed||_refreshing)return;
        var generation=++_candidateGeneration;var uiGeneration=_uiGeneration;
        bool Current()=>!_disposed&&!_state.IsDisposed&&generation==_candidateGeneration&&uiGeneration==_uiGeneration;
        _candidatesValid=false;_refreshing=true;
        try
        {
            IsTextDropDownOpen=false;if(!Current())return;
            ReferenceCandidates snapshot;
            IReadOnlyList<ReferenceTreeNode> sources,indices;
            try
            {
                snapshot=_options.ReadCandidates(_state.Port)??throw new ArgumentException("来源目录返回了空快照。");
                if(!Current())return;
                sources=DeclaredReferenceTree.Build(snapshot.Sources.Where(candidate=>DeclaredReferenceTree.Matches(candidate,
                    _state.Port,(_state.Accepted as ReferenceSetting)?.Conversion??BindingConversion.Automatic,_options.EditableIndex)),true);
                indices=DeclaredReferenceTree.Build(snapshot.Indices,true);
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException)
            {if(Current())_state.SetSourceError(error.Message);return;}
            if(!Current())return;
            _candidates=snapshot.Sources;SourceNodes=sources;IndexNodes=indices;_candidatesValid=true;
            RestoreSelection(false);
            ParameterCleanup.Run(new Action[]{()=>{if(Current())_state.SetSourceError("");}}
                .Concat(new[]{nameof(SelectedSourceNode),nameof(SelectedIndexNode),nameof(SourceNodes),nameof(IndexNodes),nameof(CurValueA)}
                    .Select(property=>(Action)(()=>{if(Current())Notify(property);}))));
        }
        finally{_refreshing=false;}
    }

    private void RestoreSelection(bool notify=true)
    {
        _sourceNode=null;_indexNode=null;
        if(DisplayReference is {} reference)
        {
            _sourceNode=DeclaredReferenceTree.Descendants(SourceNodes).FirstOrDefault(node=>node.Candidate is {} candidate
                &&DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),reference));
            if(reference.Selection is BindingSelection.AtField index)
                _indexNode=DeclaredReferenceTree.Descendants(IndexNodes).FirstOrDefault(node=>node.Candidate is {} candidate
                    &&DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),index.Reference));
        }
        if(notify)ParameterCleanup.Run(new Action[]{()=>Notify(nameof(SelectedSourceNode)),()=>Notify(nameof(SelectedIndexNode))});
    }

    private void SelectSource(ReferenceTreeNode? node)
    {
        if(_suspended||!CanBrowse||!ContainsSourceNode(node)||node!.EnumEntrysA.Count!=0)return;
        if(node.Clear){ClearSource();return;}
        if(node.Candidate is not {} candidate)return;
        var selection=DisplayReference is {} old&&candidate.SelectedType.Shape==ValueShape.Sequence
            ?old.Selection:BindingSelection.All;
        Submit(candidate.Reference(selection));
    }
    private bool TryStageReference(OutputReference reference)
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
    private void SelectIndex(ReferenceTreeNode? node)
    {
        if(_suspended||!CanBrowse||!ContainsIndexNode(node)||DisplayReference is not {} source
            ||!HasSequenceReference||node is null||node.EnumEntrysA.Count!=0)return;
        var selection=node.Clear?BindingSelection.All:node.Candidate is {} candidate
            ?new BindingSelection.AtField(candidate.Reference(BindingSelection.All)):null;
        if(selection is null)return;
        Submit(new(source.NodeId,source.PortId,source.Type,source.MemberFieldPath,selection,source.ScopeId,source.Storage));
    }
    private bool Submit(OutputReference reference,bool explicitCommit=false)
    {
        if(!explicitCommit&&TryStageReference(reference))return false;
        return AcceptSetting(new ReferenceSetting(reference,(_state.Accepted as ReferenceSetting)?.Conversion??BindingConversion.Automatic),explicitCommit);
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
            if(_state.SourceError.Length!=0)return false;
            if(HasPendingReference&&(!_candidatesValid||!_candidates.Any(candidate=>
                DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),_pendingReference!.Reference))))
                throw new ArgumentException("待确认来源已失效，请重新选择。");
            if(DisplayReference is not {} source)throw new ArgumentException("请先选择来源，再编辑索引。");
            var referenceSetting=!HasPendingReference&&_state.Accepted is ReferenceSetting original?original:
                new ReferenceSetting(source,(_state.Accepted as ReferenceSetting)?.Conversion??BindingConversion.Automatic);
            proposal=referenceSetting;
            if(_pendingText is null||ReadOnlyA)
            {
                if(NeedsSelectionRepair(source))throw new ArgumentException("请选择有效的序列索引后再确认。");
                return true;
            }
            if(!_options.EditableIndex)throw new ArgumentException("请输入有效数值，或选择来源。");
            var prefix=Label(source)+" [";
            var projected=ProjectedType(source);
            if(projected is not {Shape:ValueShape.Sequence}||!_pendingText.StartsWith(prefix,StringComparison.Ordinal)
                ||!_pendingText.EndsWith("]",StringComparison.Ordinal))throw new ArgumentException("只能编辑当前来源括号中的索引。");
            var content=_pendingText[prefix.Length..^1];
            BindingSelection selection=content.Length==0?BindingSelection.All
                :new BindingSelection.AtConstant(int.Parse(content,NumberStyles.None,CultureInfo.InvariantCulture));
            if(selection is BindingSelection.AtConstant index&&projected.Count.Maximum is int maximum&&index.Index>=maximum)
                throw new ArgumentException("序号超出来源声明的最大数量。");
            var selected=new OutputReference(source.NodeId,source.PortId,source.Type,source.MemberFieldPath,selection,source.ScopeId,source.Storage);
            if(NeedsSelectionRepair(selected))throw new ArgumentException("请选择有效的序列索引后再确认。");
            proposal=referenceSetting with{Reference=selected};
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
        if(ReadOnlyA){SetPending(null);Notify(nameof(CurValueA));return !_state.HasInvalidInput;}
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
        if(_preference().PreferExpanded){Expand(inside,inside);return;}
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
        &&(!request.IsIndex||HasSequenceReference)
        &&generation==_expansionGeneration&&ReferenceEquals(_expansion,request);
    internal void ReleaseExpansion(ReferenceExpansionRequest request,long generation)
    {
        if(generation!=_expansionGeneration||!ReferenceEquals(_expansion,request))return;
        InvalidateExpansion();
    }
    public void InvalidateExpansion()
    {
        _expansionGeneration++;var prior=_expansion;_expansion=null;
        try{prior?.Invalidate();}
        finally{Notify(nameof(IsSelectedSubscribeParam));Notify(nameof(IsTextPopupVisible));}
    }
    private void Expand(bool index,bool hasSelection=true)
    {
        if(_suspended||!CanBrowse||_expand is null)return;
        var generation=_expansionGeneration+1;InvalidateExpansion();
        if(generation!=_expansionGeneration)return;
        Refresh();
        if(_suspended||!CanBrowse||generation!=_expansionGeneration)return;
        if(index&&!HasSequenceReference)return;
        RequestClose();
        if(_suspended||!CanBrowse||generation!=_expansionGeneration)return;
        var preference=_preference();
        if(_suspended||!CanBrowse||generation!=_expansionGeneration)return;
        var request=new ReferenceExpansionRequest(this,generation,preference,index);_expansion=request;request.SetIndex(index,hasSelection);
        Notify(nameof(IsSelectedSubscribeParam));Notify(nameof(IsTextPopupVisible));
        if(!request.IsValid)return;
        try{_expand(request);}
        catch{request.Dispose();throw;}
    }
    private void OnStateChanged()
    {
        if(_disposed)return;
        if(_pendingReference is {} pending&&(pending.Acceptance!=_state.AcceptanceVersion||!CanBrowse))_pendingReference=null;
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
        ParameterCleanup.Run(new Action[]{InvalidateExpansion,Close,()=>_state.Changed-=OnStateChanged,
            ()=>{PropertyChanged=null;CloseRequested=null;}});
    }
    private sealed class SessionCommand(Action<object?> action) : ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>action(parameter);
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
}
