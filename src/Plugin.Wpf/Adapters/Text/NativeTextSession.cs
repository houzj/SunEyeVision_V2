using SunEyeVision.Plugin.Wpf.Interop.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WindowUtilities.Extern;

namespace SunEyeVision.Plugin.Wpf.Adapters.Text;

/// <summary>Raw text and one caret-scoped browse lease over a borrowed formal field state.</summary>
internal sealed class NativeTextSession : INotifyPropertyChanged,IDisposable
{
    private readonly DeclaredFieldState _state;
    private readonly TextExpressionOptions? _options;
    private readonly Func<bool> _isCurrent;
    private TextBox? _text;
    private string? _pending;
    private string? _ownedSourceError;
    private bool _disposed,_suspended,_committing,_restoring,_opening,_open;
    private long _rawVersion,_treeVersion,_attachVersion,_selectionVersion;
    private TextExpressionSnapshot? _snapshot;
    private Dictionary<DeclaredReferenceCandidate,DeclaredReferenceCandidate> _originalCandidates=new(ReferenceEqualityComparer.Instance);
    private BrowseLease? _browse;
    private ReferenceTreeNode? _selected;
    private sealed record BrowseLease(long RawVersion,long TreeVersion,long Acceptance,TextBox Text,BindingExpression Binding,
        string Raw,int Caret,int Selection,int Start,int End);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<Action,string,int,Func<bool>>? ProjectInsertionRequested;
    internal bool HasPendingEdits=>!_disposed&&_pending is not null;
    public string RawText
    {
        get=>_pending??AcceptedText();
        set
        {
            if(!Current()||_suspended||value==RawText)return;
            _pending=value??"";_rawVersion++;InvalidateTree();ClearSourceError();
        }
    }
    public bool ReadOnly=>false;
    public IReadOnlyList<ReferenceTreeNode> SourceNodes {get;private set;}=Array.Empty<ReferenceTreeNode>();
    public ReferenceTreeNode? SelectedSourceNode {get=>_selected;set=>_selected=value;}
    public bool IsTextDropDownOpen
    {
        get=>_open;
        set{if(_open==value)return;_open=value;Notify(nameof(IsTextDropDownOpen));}
    }
    public ICommand FocusedCommand {get;}
    public ICommand IndexClickCommand {get;}
    public NativeTextSession(DeclaredFieldState state,TextExpressionOptions? options,Func<bool> isCurrent)
    {
        _state=state;_options=options;_isCurrent=isCurrent;options?.Validate(state.Port);
        FocusedCommand=new TextCommand(FocusChanged);
        IndexClickCommand=new TextCommand(SelectNode);
        _state.ExplicitlyAccepted+=OnAccepted;
    }
    internal void AttachText(TextBox? text)
    {
        if(_disposed||ReferenceEquals(text,_text))return;
        var version=++_attachVersion;
        if(_text is {} oldText)oldText.SelectionChanged-=OnSelectionChanged;
        if(_text is {} previous&&previous.GetBindingExpression(TextBox.TextProperty) is {DataItem:var item}
            &&ReferenceEquals(item,this))BindingOperations.ClearBinding(previous,TextBox.TextProperty);
        if(_disposed||version!=_attachVersion)return;
        _text=text;if(text is not null)text.SelectionChanged+=OnSelectionChanged;InvalidateTree();
        if(text is not null&&Current())Notify(nameof(RawText));
    }
    private void OnSelectionChanged(object sender,System.Windows.RoutedEventArgs args)
    {if(ReferenceEquals(sender,_text))_selectionVersion++;}
    private string AcceptedText()=>_state.Accepted switch {ConstantSetting {Value:string text}=>text,DisplayExpressionSetting expression=>expression.Text,_=>""};
    private bool Current()
    {
        if(_disposed||_state.IsDisposed)return false;
        var current=_isCurrent();
        return current&&!_disposed&&!_state.IsDisposed;
    }
    private bool SameRaw(long version,string raw)=>Current()&&version==_rawVersion&&raw==RawText;
    private bool LeaseCurrent(BrowseLease lease,bool checkReceipt=true)
    {
        if(_suspended||!SameRaw(lease.RawVersion,lease.Raw)||lease.TreeVersion!=_treeVersion
            ||lease.Acceptance!=_state.AcceptanceVersion||!ReferenceEquals(_text,lease.Text)
            ||!ReferenceEquals(lease.Binding,lease.Text.GetBindingExpression(TextBox.TextProperty))
            ||lease.Text.Text!=lease.Raw||lease.Text.SelectionStart!=lease.Caret||lease.Text.SelectionLength!=lease.Selection)return false;
        if(checkReceipt)
        {
            var snapshot=_snapshot;
            if(snapshot is null)return false;
            bool valid=false;Exception? failure=null;
            try
            {
                valid=snapshot.IsCurrent()&&_originalCandidates.All(pair=>pair.Key.MemberPath.SequenceEqual(pair.Value.MemberPath)
                    &&snapshot.Candidates.Any(candidate=>ReferenceEquals(candidate,pair.Value)));
            }
            catch(Exception error){failure=error;}
            if(!ReferenceEquals(snapshot,_snapshot)||!LeaseCurrent(lease,false))return false;
            if(!valid)
            {
                InvalidateTree();
                if(failure is not null&&SameRaw(lease.RawVersion,lease.Raw))SetSourceError(failure.Message);
                return false;
            }
            return true;
        }
        return true;
    }
    private void FocusChanged(object? value)
    {
        if(_opening||_restoring||_suspended||_options is null||!Current()
            ||value is not TextBoxFocusEventArgs {IsFocus:true} args||_text is not {} text
            ||args.Text!=text.Text||args.SelectionStart!=text.SelectionStart||!text.IsKeyboardFocused)return;
        var raw=RawText;if(raw!=text.Text)return;var caret=text.SelectionStart;
        var start=caret>0?raw.LastIndexOf('{',Math.Min(caret-1,raw.Length-1)):-1;
        var end=start<0?-1:raw.IndexOf('}',start+1);
        if(start<0||end<caret||end<start+1||raw.AsSpan(start+1,end-start-1).Contains('{'))
        {InvalidateTree();return;}
        _opening=true;
        try
        {
            InvalidateTree();
            var binding=text.GetBindingExpression(TextBox.TextProperty);
            if(binding is null||!ReferenceEquals(binding.DataItem,this)||binding.ParentBinding.Path?.Path!=nameof(RawText))return;
            var lease=new BrowseLease(_rawVersion,_treeVersion,_state.AcceptanceVersion,text,binding,raw,caret,text.SelectionLength,start,end);
            if(!LeaseCurrent(lease,false))return;
            TextExpressionSnapshot snapshot;
            ReferenceTreeNode[] nodes;
            var originals=new Dictionary<DeclaredReferenceCandidate,DeclaredReferenceCandidate>(ReferenceEqualityComparer.Instance);
            try
            {
                snapshot=_options.ReadCandidates()??throw new ArgumentException("文本来源快照不能为空。");
                if(!LeaseCurrent(lease,false))return;
                ArgumentNullException.ThrowIfNull(snapshot.FormatExpression);ArgumentNullException.ThrowIfNull(snapshot.IsCurrent);
                ArgumentNullException.ThrowIfNull(snapshot.Candidates);
                if(!snapshot.IsCurrent()||!LeaseCurrent(lease,false))return;
                var candidates=snapshot.Candidates.Select(candidate=>
                {
                    if(candidate is null)throw new ArgumentException("文本来源不能为空。");
                    var frozen=candidate.Freeze();
                    originals.Add(frozen,candidate);return frozen;
                }).ToArray();
                // The host supplies front/self/internal scope. No C07 Clear node or guessed evaluator.
                foreach(var candidate in candidates)
                {
                    var type=candidate.SelectedType;
                    while(type.Shape==ValueShape.Sequence)type=type.Element!;
                    if(type.Shape!=ValueShape.Scalar||type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64 or ScalarKind.Text))
                        throw new ArgumentException("文本来源只支持已声明的整数、浮点或字符串。");
                }
                nodes=DeclaredReferenceTree.Build(candidates,false).ToArray();
            }
            catch(Exception error)
            {if(LeaseCurrent(lease,false))SetSourceError(error.Message);return;}
            if(!LeaseCurrent(lease,false))return;
            _snapshot=snapshot;_originalCandidates=originals;
            if(!LeaseCurrent(lease))return;
            _browse=lease;SourceNodes=Array.AsReadOnly(nodes);_selected=null;
            ClearSourceError();
            if(!LeaseCurrent(lease))return;
            Notify(nameof(SourceNodes));Notify(nameof(SelectedSourceNode));
            if(LeaseCurrent(lease))IsTextDropDownOpen=nodes.Length!=0;
        }
        finally{_opening=false;}
    }
    private void SelectNode(object? value)
    {
        var node=ReferenceFieldSession.Unwrap(value);var lease=_browse;var snapshot=_snapshot;
        if(node is not {Candidate:{} candidate,Clear:false}||lease is null||snapshot is null
            ||!Contains(SourceNodes,node)||!LeaseCurrent(lease))return;
        string proposed;string? error;string? selectedToken=null;
        try
        {
            var token=snapshot.FormatExpression(_originalCandidates[candidate]);selectedToken=token;
            if(!LeaseCurrent(lease))return;
            if(string.IsNullOrEmpty(token)||token.Contains('{')||token.Contains('}'))throw new ArgumentException("文本来源标记无效。");
            proposed=lease.Raw[..(lease.Start+1)]+token+lease.Raw[lease.End..];
            error=_options?.ValidateText?.Invoke(proposed);
            if(!LeaseCurrent(lease))return;
        }
        catch(Exception failure)
        {if(LeaseCurrent(lease,false))SetSourceError(failure.Message);return;}
        if(!string.IsNullOrEmpty(error)){_state.SetEditError(error);return;}
        if(!LeaseCurrent(lease))return;
        ClearSourceError();
        if(!LeaseCurrent(lease))return;
        // Host acceptance/notification is deliberately outside the preparation catch.
        var initialSelectionVersion=_selectionVersion;var selectionChangedDuringAccept=false;
        Accept(proposed,lease.RawVersion,lease.Raw,
            ()=>selectionChangedDuringAccept=initialSelectionVersion!=_selectionVersion,
            project=>
            {
                if(selectionChangedDuringAccept){project();return;}
                var rawVersion=_rawVersion;var acceptance=_state.AcceptanceVersion;var attachment=_attachVersion;
                bool OwnsCaret()=>Current()&&!_suspended&&_pending is null&&_rawVersion==rawVersion
                    &&_state.AcceptanceVersion==acceptance&&_attachVersion==attachment
                    &&ReferenceEquals(_text,lease.Text)&&AcceptedText()==proposed
                    &&ReferenceEquals(lease.Binding,lease.Text.GetBindingExpression(TextBox.TextProperty))
                    &&_selectionVersion==initialSelectionVersion&&_browse is null&&!_open;
                var handler=ProjectInsertionRequested;
                if(handler is null||!OwnsCaret()){project();return;}
                _restoring=true;
                try{handler(project,proposed,proposed.Length-(lease.Raw.Length-lease.End),OwnsCaret);}
                finally{_restoring=false;}
            },new(proposed,_state.Accepted,candidate,selectedToken,lease.Start,lease.End),()=>LeaseCurrent(lease));
    }
    private static bool Contains(IEnumerable<ReferenceTreeNode> nodes,ReferenceTreeNode target)
        =>nodes.Any(node=>ReferenceEquals(node,target)||Contains(node.EnumEntrysA,target));
    private void SetSourceError(string error)
    {if(!Current())return;_ownedSourceError=error;_state.SetSourceError(error);}
    private void ClearSourceError()
    {
        var owned=_ownedSourceError;_ownedSourceError=null;
        if(owned is not null&&Current()&&_state.SourceError==owned)_state.SetSourceError("");
    }
    internal bool TrySetConstant(object value)
    {
        if(value is not string text)throw new ArgumentException("字符串字段必须提供文本。");
        return TryCommit(text,_rawVersion,RawText);
    }
    internal bool TryCommitPending()
        =>!Current()?false:_pending is null?!_state.HasInvalidInput:TryCommit(_pending,_rawVersion,_pending);
    private bool TryCommit(string proposed,long version,string original)
    {
        if(_committing||!SameRaw(version,original))return false;
        string? error;
        try{error=_options?.ValidateText?.Invoke(proposed);}
        catch(Exception failure){if(SameRaw(version,original))_state.SetEditError(failure.Message);return false;}
        if(!SameRaw(version,original))return false;
        if(!string.IsNullOrEmpty(error)){_state.SetEditError(error);return false;}
        return Accept(proposed,version,original);
    }
    private bool Accept(string proposed,long version,string original,Action? afterWrite=null,Action<Action>? projection=null,TextExpressionProposal? expressionProposal=null,Func<bool>? proposalCurrent=null)
    {
        if(_committing||!SameRaw(version,original))return false;
        var acceptedVersion=_state.AcceptanceVersion;
        ParameterSetting prepared;
        try{prepared=_options?.CreateSetting?.Invoke(expressionProposal??new(proposed,_state.Accepted))??new ConstantSetting(proposed);}
        catch(Exception failure)when(failure is ArgumentException or InvalidOperationException)
        {if(SameRaw(version,original)&&acceptedVersion==_state.AcceptanceVersion)_state.SetEditError(failure.Message);return false;}
        if(!SameRaw(version,original)||acceptedVersion!=_state.AcceptanceVersion||proposalCurrent?.Invoke()==false
            ||!SameRaw(version,original)||acceptedVersion!=_state.AcceptanceVersion)return false;
        _committing=true;bool accepted=false;
        try{accepted=_state.TrySet(prepared);afterWrite?.Invoke();}
        finally
        {
            _committing=false;
            if(Current()&&_state.AcceptanceVersion!=acceptedVersion&&version==_rawVersion)OnAccepted(accepted?projection:null);
        }
        return accepted&&Current()&&!_state.HasInvalidInput;
    }
    private void OnAccepted()=>OnAccepted(null);
    private void OnAccepted(Action<Action>? projection)
    {
        if(!Current())return;
        var hadPending=_pending is not null;var acceptanceVersion=_state.AcceptanceVersion;
        _pending=null;var version=++_rawVersion;
        ParameterCleanup.Run(new Action[]{ClearSourceError,
            ()=>{if(Current()&&version==_rawVersion)InvalidateTree();},
            ()=>{if(Current()&&version==_rawVersion)
            {
                void Project(){if(Current()&&version==_rawVersion)Notify(nameof(RawText));}
                if(projection is null)Project();else projection(Project);
            }},
            ()=>{if(hadPending&&Current()&&version==_rawVersion&&_pending is null&&acceptanceVersion==_state.AcceptanceVersion)_state.NotifyEditSettled();}});
    }
    internal void SetSuspended(bool suspended)
    {
        if(_disposed||_suspended==suspended)return;
        _suspended=suspended;
        if(suspended)Close();
        else if(Current())Notify(nameof(RawText));
    }
    internal void Close()=>InvalidateTree();
    private void InvalidateTree()
    {
        _treeVersion++;_snapshot=null;_originalCandidates.Clear();_browse=null;_selected=null;SourceNodes=Array.Empty<ReferenceTreeNode>();
        IsTextDropDownOpen=false;Notify(nameof(SourceNodes));Notify(nameof(SelectedSourceNode));
    }
    private void Notify(string name)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_rawVersion++;_attachVersion++;_state.ExplicitlyAccepted-=OnAccepted;
        var text=_text;var sourceError=_ownedSourceError;_ownedSourceError=null;
        ParameterCleanup.Run(new Action[]{()=>{if(text is not null)text.SelectionChanged-=OnSelectionChanged;},
            ()=>{if(sourceError is not null&&!_state.IsDisposed&&_state.SourceError==sourceError)_state.SetSourceError("");},InvalidateTree,
            ()=>{if(text?.GetBindingExpression(TextBox.TextProperty) is {DataItem:var item}&&ReferenceEquals(item,this))
                BindingOperations.ClearBinding(text,TextBox.TextProperty);},
            ()=>{PropertyChanged=null;ProjectInsertionRequested=null;_text=null;_pending=null;}});
    }
    private sealed class TextCommand(Action<object?> action) : ICommand
    {
        public event EventHandler? CanExecuteChanged {add{}remove{}}
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>action(parameter);
    }
}
