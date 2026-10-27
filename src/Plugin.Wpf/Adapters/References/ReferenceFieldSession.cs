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
    private bool _disposed,_committing,_sourceOpen,_indexOpen,_suspended;
    private long _uiGeneration;
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
    public bool HasPendingText=>_pendingText is not null;
    public bool ReadOnlyA=>!_allowConstantText&&!_options.EditableIndex;
    public bool IsTextPopupVisible=>_expansion is null;
    public bool SubscribeInPopWindowEnableA=>_expand is not null;
    internal void ClearExpandedSelection(ReferenceExpansionRequest request)
    {
        if(_disposed||!ReferenceEquals(_expansion,request)||!request.IsValid)return;
        request.SetIndex(request.IsIndex,false);Notify(nameof(IsSelectedSubscribeParam));
    }
    public bool IsSelectedSubscribeParam=>_expansion is {IsValid:true,HasSelection:true};
    public bool IsDropDownOpen {get=>_sourceOpen;set{if(_sourceOpen==value)return;_sourceOpen=value;Notify(nameof(IsDropDownOpen));}}
    public bool IsTextDropDownOpen {get=>_indexOpen;set{if(_indexOpen==value)return;_indexOpen=value;Notify(nameof(IsTextDropDownOpen));}}
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
        if(!_state.ReferenceCleared&&_allowConstantText&&_state.Accepted is ConstantSetting number)
            return number.Value switch{long integer=>integer.ToString(CultureInfo.InvariantCulture),double real when NumericStorageRules.Effective(_state.Port.Type,_state.Port.Storage)==NumericStorage.Float32=>((float)real).ToString("R",CultureInfo.InvariantCulture),double real=>real.ToString("R",CultureInfo.InvariantCulture),_=>""};
        if(_state.ReferenceCleared||_state.Accepted is not ReferenceSetting accepted)return "";
        var source=accepted.Reference;var label=Label(source);
        return source.Type.Shape!=ValueShape.Sequence?label:label+" ["+(source.Selection switch
        {
            BindingSelection.AtConstant index=>index.Index.ToString(CultureInfo.InvariantCulture),
            BindingSelection.AtField index=>_format?.Invoke(index.Reference)??index.Reference.NodeId+" · "+index.Reference.PortId,
            _=>""
        })+"]";
    }

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
                sources=Build(snapshot.Sources);indices=Build(snapshot.Indices);
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

    private static IReadOnlyList<ReferenceTreeNode> Build(IEnumerable<DeclaredReferenceCandidate> candidates)
    {
        var roots=new List<ReferenceTreeNode>{new("清除来源",Array.Empty<ReferenceTreeNode>(),Clear:true)};
        foreach(var group in candidates.GroupBy(candidate=>(candidate.ScopeId,candidate.NodeId)))
            roots.Add(new(group.Key.NodeId,Array.AsReadOnly(group.Select(candidate=>new ReferenceTreeNode(
                candidate.Label,Array.Empty<ReferenceTreeNode>(),candidate)).ToArray())));
        return roots.AsReadOnly();
    }
    private void RestoreSelection(bool notify=true)
    {
        _sourceNode=null;_indexNode=null;
        if(!_state.ReferenceCleared&&_state.Accepted is ReferenceSetting accepted)
        {
            _sourceNode=SourceNodes.SelectMany(node=>node.EnumEntrysA).FirstOrDefault(node=>node.Candidate is {} candidate
                &&DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),accepted.Reference));
            if(accepted.Reference.Selection is BindingSelection.AtField index)
                _indexNode=IndexNodes.SelectMany(node=>node.EnumEntrysA).FirstOrDefault(node=>node.Candidate is {} candidate
                    &&DeclaredFieldState.SameSource(candidate.Reference(BindingSelection.All),index.Reference));
        }
        if(notify)ParameterCleanup.Run(new Action[]{()=>Notify(nameof(SelectedSourceNode)),()=>Notify(nameof(SelectedIndexNode))});
    }

    private void SelectSource(ReferenceTreeNode? node)
    {
        if(_suspended||!CanBrowse||!ContainsSourceNode(node))return;
        if(node.Clear){ClearSource();return;}
        if(node.Candidate is not {} candidate)return;
        var selection=!_state.ReferenceCleared&&_state.Accepted is ReferenceSetting old&&candidate.Output.Type.Shape==ValueShape.Sequence
            ?old.Reference.Selection:BindingSelection.All;
        Submit(candidate.Reference(selection));
    }
    private void SelectIndex(ReferenceTreeNode? node)
    {
        if(_suspended||_disposed||_state.IsDisposed||!_state.IsReferenceMode||!ContainsIndexNode(node)||_state.ReferenceCleared||_state.Accepted is not ReferenceSetting accepted
            ||accepted.Reference.Type.Shape!=ValueShape.Sequence||node is null)return;
        var selection=node.Clear?BindingSelection.All:node.Candidate is {} candidate
            ?new BindingSelection.AtField(candidate.Reference(BindingSelection.All)):null;
        if(selection is null)return;
        var source=accepted.Reference;
        Submit(new(source.NodeId,source.PortId,source.Type,source.MemberFieldPath,selection,source.ScopeId,source.Storage));
    }
    private bool Submit(OutputReference reference,bool explicitCommit=false)
        =>AcceptSetting(new ReferenceSetting(reference),explicitCommit);
    private bool AcceptSetting(ParameterSetting proposal,bool explicitCommit=false)
    {
        if(_suspended&&!explicitCommit||_disposed||_state.IsDisposed)return false;
        var generation=_uiGeneration;var version=_state.AcceptanceVersion;bool accepted;
        try{accepted=_accept(proposal);}
        catch(Exception failure)when(_state.AcceptanceVersion!=version)
        {
            _pendingText=null;
            if(!_disposed&&!_state.IsDisposed&&generation==_uiGeneration)
                try{OnStateChanged();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
        if(_disposed||_state.IsDisposed)return accepted;
        if(accepted)
        {
            _pendingText=null;
            if(proposal is ConstantSetting)_state.SetSourceError("");
            if(_disposed||_state.IsDisposed||generation!=_uiGeneration)return true;
            RequestClose();if(!_disposed&&!_state.IsDisposed&&generation==_uiGeneration)OnStateChanged();return true;
        }
        if(generation==_uiGeneration){RestoreSelection();if(!_disposed)Notify(nameof(CurValueA));}return false;
    }

    public void ObserveText(string text)
    {
        if(_disposed||text==CurValueA)return;
        if(!_suspended&&text.Length==0){ClearSource();return;}
        _pendingText=text;
    }
    private void ClearSource()
    {
        if(!_state.Port.Required){AcceptSetting(new UnsetSetting());return;}
        var generation=_uiGeneration;
        if(!ClearReference()){RestoreSelection();Notify(nameof(CurValueA));return;}
        if(_disposed||_state.IsDisposed||generation!=_uiGeneration)return;
        _pendingText=null;OnStateChanged();RequestClose();
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
            if(_state.ReferenceCleared||_state.MissingReference)throw new ArgumentException("请先选择来源，再编辑索引。");
            proposal=_state.Accepted;
            if(_pendingText is null||ReadOnlyA)return true;
            if(!_options.EditableIndex)throw new ArgumentException("请输入有效数值，或选择来源。");
            if(proposal is not ReferenceSetting accepted)throw new ArgumentException("请先选择来源，再编辑索引。");
            var source=accepted.Reference;var prefix=Label(source)+" [";
            if(source.Type.Shape!=ValueShape.Sequence||!_pendingText.StartsWith(prefix,StringComparison.Ordinal)
                ||!_pendingText.EndsWith("]",StringComparison.Ordinal))throw new ArgumentException("只能编辑当前来源括号中的索引。");
            var content=_pendingText[prefix.Length..^1];
            BindingSelection selection=content.Length==0?BindingSelection.All
                :new BindingSelection.AtConstant(int.Parse(content,NumberStyles.None,CultureInfo.InvariantCulture));
            if(selection is BindingSelection.AtConstant index&&source.Type.Count.Maximum is int maximum&&index.Index>=maximum)
                throw new ArgumentException("序号超出来源声明的最大数量。");
            proposal=new ReferenceSetting(new(source.NodeId,source.PortId,source.Type,source.MemberFieldPath,selection,source.ScopeId,source.Storage));
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
        if(_pendingText is null&&_allowConstantText&&_state.Accepted is ConstantSetting)
        {_state.SetSourceError("");if(_disposed||_state.IsDisposed)return false;}
        if(_committing||_pendingText is null)return !_state.HasInvalidInput;
        if(ReadOnlyA){_pendingText=null;Notify(nameof(CurValueA));return !_state.HasInvalidInput;}
        _committing=true;
        try
        {
            if(!TryPreparePending(out var proposal))return false;
            if(uiGeneration is {} generation&&(_suspended||generation!=_uiGeneration))return false;
            if(proposal is ReferenceSetting reference)return Submit(reference.Reference,true);
            return AcceptSetting(proposal,true);
        }
        finally{_committing=false;}
    }
    public void ProjectAccepted(){_pendingText=null;OnStateChanged();}

    private void FocusChanged(object? value)
    {
        if(_suspended||!CanBrowse||value is not TextBoxFocusEventArgs {Text:{} text} args)return;
        if(!args.IsFocus){IsTextDropDownOpen=false;return;}
        if(ReadOnlyA||args.SelectionStart<1||args.SelectionStart>text.Length)return;
        var before=text[..args.SelectionStart];var after=text[args.SelectionStart..];
        var inside=_options.EditableIndex&&_state.Accepted is ReferenceSetting{Reference.Type.Shape:ValueShape.Sequence}
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
        &&(!request.IsIndex||_state.Accepted is ReferenceSetting{Reference.Type.Shape:ValueShape.Sequence})
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
        if(index&&(_state.ReferenceCleared||_state.Accepted is not ReferenceSetting {Reference.Type.Shape:ValueShape.Sequence}))return;
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
        if(!CanBrowse||_expansion?.IsIndex==true&&(_state.ReferenceCleared||_state.Accepted is not ReferenceSetting {Reference.Type.Shape:ValueShape.Sequence}))InvalidateExpansion();
        RestoreSelection();Notify(nameof(CurValueA));
    }
    public void Close(){IsDropDownOpen=false;IsTextDropDownOpen=false;}
    private void RequestClose(){Close();if(!_disposed&&!_state.IsDisposed)CloseRequested?.Invoke();}
    private void Notify(string name){if(!_disposed)PropertyChanged?.Invoke(this,new(name));}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_candidateGeneration++;_candidatesValid=false;
        try{InvalidateExpansion();}
        finally{Close();_state.Changed-=OnStateChanged;PropertyChanged=null;CloseRequested=null;}
    }
    private sealed class SessionCommand(Action<object?> action) : ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>action(parameter);
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
}
