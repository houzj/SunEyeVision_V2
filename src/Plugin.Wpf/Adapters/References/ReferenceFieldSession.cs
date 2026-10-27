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
    private readonly Func<OutputReference,string>? _format;
    private IReadOnlyList<DeclaredReferenceCandidate> _candidates=Array.Empty<DeclaredReferenceCandidate>();
    private string? _pendingText;
    private bool _disposed,_committing,_sourceOpen,_indexOpen;
    private ReferenceTreeNode? _sourceNode,_indexNode;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? CloseRequested;
    public IReadOnlyList<ReferenceTreeNode> SourceNodes {get;private set;}=Array.Empty<ReferenceTreeNode>();
    public IReadOnlyList<ReferenceTreeNode> IndexNodes {get;private set;}=Array.Empty<ReferenceTreeNode>();
    public ReferenceTreeNode? SelectedSourceNode {get=>_sourceNode;set=>_sourceNode=value;}
    public ReferenceTreeNode? SelectedIndexNode {get=>_indexNode;set=>_indexNode=value;}
    public bool HasPendingText=>_pendingText is not null;
    public bool ReadOnlyA=>!_options.EditableIndex;
    public bool IsTextPopupVisible=>_expansion is null;
    public bool SubscribeInPopWindowEnableA=>_expand is not null;
    public bool IsSelectedSubscribeParam=>_expansion?.IsValid==true;
    public bool IsDropDownOpen {get=>_sourceOpen;set{if(_sourceOpen==value)return;_sourceOpen=value;Notify(nameof(IsDropDownOpen));}}
    public bool IsTextDropDownOpen {get=>_indexOpen;set{if(_indexOpen==value)return;_indexOpen=value;Notify(nameof(IsTextDropDownOpen));}}
    public string CurValueA {get=>_pendingText??AcceptedText();set{ObserveText(value);if(_pendingText is not null)TryCommitPending();}}
    public ICommand DropDownCommand {get;}
    public ICommand ClickCommand {get;}
    public ICommand IndexClickCommand {get;}
    public ICommand FocusedCommand {get;}
    public ICommand PopSubscribeWindowCommand {get;}

    public ReferenceFieldSession(DeclaredFieldState state,ReferenceFieldOptions options,Func<OutputReference,string>? format,Func<ParameterSetting,bool>? accept=null,Func<bool>? commitGroup=null,Action<ReferenceExpansionRequest>? expand=null,Func<ReferenceExpansionPreference>? preference=null)
    {
        _state=state;_options=options;_format=format;_accept=accept??state.TrySet;_commitGroup=commitGroup;_expand=options.Expand??expand;
        var localPreference=new ReferenceExpansionPreference();_preference=preference??(()=>localPreference);
        DropDownCommand=new SessionCommand(_=>{if(_preference().PreferExpanded)Expand(false);else Refresh();});
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
        if(_disposed||_state.IsDisposed)return;
        IsTextDropDownOpen=false;
        DeclaredReferenceCandidate[] snapshot;
        IReadOnlyList<ReferenceTreeNode> sources,indices;
        try
        {
            snapshot=_options.Candidates(_state.Port).Select(candidate=>
            {
                var frozen=candidate with{MemberPath=Array.AsReadOnly(candidate.MemberPath.ToArray())};
                _=frozen.SelectedType;
                return frozen;
            }).ToArray();
            if(_disposed||_state.IsDisposed)return;
            sources=Build(snapshot);
            indices=Build(snapshot.Where(candidate=>candidate.Output.Type.Shape!=ValueShape.Sequence
                &&candidate.SelectedType==TypeRef.ScalarOf(ScalarKind.Int64)));
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException)
        {_state.SetSourceError(error.Message);return;}
        if(_disposed||_state.IsDisposed)return;
        _candidates=Array.AsReadOnly(snapshot);SourceNodes=sources;IndexNodes=indices;
        RestoreSelection();_state.SetSourceError("");
        Notify(nameof(SourceNodes));Notify(nameof(IndexNodes));Notify(nameof(CurValueA));
    }

    private static IReadOnlyList<ReferenceTreeNode> Build(IEnumerable<DeclaredReferenceCandidate> candidates)
    {
        var roots=new List<ReferenceTreeNode>{new("清除来源",Array.Empty<ReferenceTreeNode>(),Clear:true)};
        foreach(var group in candidates.GroupBy(candidate=>(candidate.ScopeId,candidate.NodeId)))
            roots.Add(new(group.Key.NodeId,Array.AsReadOnly(group.Select(candidate=>new ReferenceTreeNode(
                candidate.Label,Array.Empty<ReferenceTreeNode>(),candidate)).ToArray())));
        return roots.AsReadOnly();
    }
    private void RestoreSelection()
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
        Notify(nameof(SelectedSourceNode));Notify(nameof(SelectedIndexNode));
    }

    private void SelectSource(ReferenceTreeNode? node)
    {
        if(_disposed||_state.IsDisposed||!_state.IsReferenceMode||!ContainsSourceNode(node))return;
        if(node.Clear){_pendingText=null;ClearReference();OnStateChanged();RequestClose();return;}
        if(node.Candidate is not {} candidate)return;
        var selection=!_state.ReferenceCleared&&_state.Accepted is ReferenceSetting old&&candidate.Output.Type.Shape==ValueShape.Sequence
            ?old.Reference.Selection:BindingSelection.All;
        Submit(candidate.Reference(selection));
    }
    private void SelectIndex(ReferenceTreeNode? node)
    {
        if(_disposed||_state.IsDisposed||!_state.IsReferenceMode||!ContainsIndexNode(node)||_state.ReferenceCleared||_state.Accepted is not ReferenceSetting accepted
            ||accepted.Reference.Type.Shape!=ValueShape.Sequence||node is null)return;
        var selection=node.Clear?BindingSelection.All:node.Candidate is {} candidate
            ?new BindingSelection.AtField(candidate.Reference(BindingSelection.All)):null;
        if(selection is null)return;
        var source=accepted.Reference;
        Submit(new(source.NodeId,source.PortId,source.Type,source.MemberFieldPath,selection,source.ScopeId,source.Storage));
    }
    private bool Submit(OutputReference reference)
    {
        if(_disposed||_state.IsDisposed)return false;
        if(_accept(new ReferenceSetting(reference)))
        {_pendingText=null;RequestClose();OnStateChanged();return true;}
        RestoreSelection();Notify(nameof(CurValueA));return false;
    }

    public void ObserveText(string text)
    {
        if(_disposed||text==CurValueA)return;
        if(text.Length==0){_pendingText=null;ClearReference();OnStateChanged();RequestClose();return;}
        _pendingText=text;
    }
    private void ClearReference()
    {
        if(_state.Port.Required)_state.ClearReference();
        else _accept(new UnsetSetting());
    }
    public bool TryPreparePending(out ParameterSetting proposal)
    {
        proposal=null!;
        if(_disposed||_state.IsDisposed||_state.SourceError.Length!=0)return false;
        try
        {
            if(_state.ReferenceCleared||_state.MissingReference)throw new ArgumentException("请先选择来源，再编辑索引。");
            proposal=_state.Accepted;
            if(_pendingText is null||ReadOnlyA)return true;
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
    public bool TryCommitPending()
    {
        if(_disposed||_state.IsDisposed)return false;
        if(_commitGroup is not null)return _commitGroup();
        if(_committing||_pendingText is null)return !_state.HasInvalidInput;
        if(ReadOnlyA){_pendingText=null;Notify(nameof(CurValueA));return !_state.HasInvalidInput;}
        _committing=true;
        try
        {
            if(!TryPreparePending(out var proposal))return false;
            if(proposal is ReferenceSetting reference)return Submit(reference.Reference);
            return _accept(proposal);
        }
        finally{_committing=false;}
    }
    public void ProjectAccepted(){_pendingText=null;OnStateChanged();}

    private void FocusChanged(object? value)
    {
        if(_disposed||_state.IsDisposed||!_state.IsReferenceMode||value is not TextBoxFocusEventArgs {Text:{} text} args)return;
        if(!args.IsFocus){IsTextDropDownOpen=false;return;}
        if(ReadOnlyA||args.SelectionStart<1||args.SelectionStart>text.Length)return;
        var before=text[..args.SelectionStart];var after=text[args.SelectionStart..];
        var inside=before.LastIndexOf('[')>before.LastIndexOf(']')&&after.IndexOf(']')>=0
            &&(after.IndexOf('[')<0||after.IndexOf(']')<after.IndexOf('['));
        if(_expansion?.IsValid==true){_expansion.SetIndex(inside,inside);RequestClose();return;}
        if(_preference().PreferExpanded){Expand(inside,inside);return;}
        if(inside){Refresh();IsDropDownOpen=false;}IsTextDropDownOpen=inside;
    }
    internal bool ContainsSourceNode(object? node)=>Contains(SourceNodes,node);
    internal bool ContainsIndexNode(object? node)=>Contains(IndexNodes,node);
    private static bool Contains(IReadOnlyList<ReferenceTreeNode> roots,object? node)
        =>node is not null&&roots.Any(item=>ReferenceEquals(item,node)||Contains(item.EnumEntrysA,node));
    internal bool IsExpansionCurrent(ReferenceExpansionRequest request,long generation)=>!_disposed&&!_state.IsDisposed&&_state.IsReferenceMode
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
        if(_disposed||_state.IsDisposed||!_state.IsReferenceMode||_expand is null)return;
        var generation=_expansionGeneration+1;InvalidateExpansion();
        if(generation!=_expansionGeneration)return;
        Refresh();
        if(_disposed||_state.IsDisposed||!_state.IsReferenceMode||generation!=_expansionGeneration)return;
        if(index&&(_state.ReferenceCleared||_state.Accepted is not ReferenceSetting {Reference.Type.Shape:ValueShape.Sequence}))return;
        RequestClose();
        if(_disposed||_state.IsDisposed||!_state.IsReferenceMode||generation!=_expansionGeneration)return;
        var preference=_preference();
        if(_disposed||_state.IsDisposed||!_state.IsReferenceMode||generation!=_expansionGeneration)return;
        var request=new ReferenceExpansionRequest(this,generation,preference,index);_expansion=request;request.SetIndex(index,hasSelection);
        Notify(nameof(IsSelectedSubscribeParam));Notify(nameof(IsTextPopupVisible));
        if(!request.IsValid)return;
        try{_expand(request);}
        catch{request.Dispose();throw;}
    }
    private void OnStateChanged()
    {
        if(_disposed)return;
        if(!_state.IsReferenceMode||_expansion?.IsIndex==true&&(_state.ReferenceCleared||_state.Accepted is not ReferenceSetting {Reference.Type.Shape:ValueShape.Sequence}))InvalidateExpansion();
        RestoreSelection();Notify(nameof(CurValueA));
    }
    public void Close(){IsDropDownOpen=false;IsTextDropDownOpen=false;}
    private void RequestClose(){Close();if(!_disposed&&!_state.IsDisposed)CloseRequested?.Invoke();}
    private void Notify(string name){if(!_disposed)PropertyChanged?.Invoke(this,new(name));}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
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
