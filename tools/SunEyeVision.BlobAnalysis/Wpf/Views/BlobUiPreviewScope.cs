namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>One editor-window preview boundary. No workflow/model/persistence ownership.</summary>
internal sealed class BlobUiPreviewScope : IDisposable
{
    private readonly Func<bool> _current;
    private readonly List<Func<bool>> _changes=[];
    private readonly List<Func<bool>> _unsupported=[];
    private readonly List<Action> _detach=[],_retire=[];
    private bool _disposed;
    internal event Action? Changed;
    internal bool IsCurrent=>!_disposed&&_current();
    internal bool HasUiChanges=>IsCurrent&&(_changes.Any(read=>read())||_unsupported.Any(read=>read()));
    internal BlobUiPreviewScope(Func<bool> current)=>_current=current;
    internal IDisposable Observe(Func<bool> changes,Action<Action> subscribe,Action<Action> unsubscribe,
        Action? retire=null,Func<bool>? unsupported=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);_changes.Add(changes);if(unsupported is not null)_unsupported.Add(unsupported);
        subscribe(Notify);Action detach=null!;detach=()=>
        {
            unsubscribe(Notify);_changes.Remove(changes);if(unsupported is not null)_unsupported.Remove(unsupported);
            if(retire is not null)_retire.Remove(retire);_detach.Remove(detach);
        };
        _detach.Add(detach);if(retire is not null)_retire.Add(retire);return new Subscription(detach);
    }
    internal string? Validate()
    {
        if(!IsCurrent)return "参数窗口已失效，请重新打开。";
        if(_unsupported.Any(read=>read()))return "当前阈值或排序仅为界面预览，尚未接入执行；请恢复已支持选择。";
        return _changes.Any(read=>read())?"尚未接入的界面设置已修改，请恢复原状态后再确认。":null;
    }
    internal void Notify(){if(IsCurrent)Changed?.Invoke();}
    internal void RetireInteractions()
    {
        if(_disposed)return;
        var errors=new List<Exception>();foreach(var retire in _retire)try{retire();}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("预览弹层退役失败。",errors);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;
        var errors=new List<Exception>();foreach(var detach in _detach.ToArray())try{detach();}catch(Exception error){errors.Add(error);}
        foreach(var retire in _retire.ToArray())try{retire();}catch(Exception error){errors.Add(error);}
        _changes.Clear();_unsupported.Clear();_detach.Clear();_retire.Clear();
        if(errors.Count!=0)throw new AggregateException("预览作用域清理失败。",errors);
    }
    private sealed class Subscription(Action detach):IDisposable
    {
        private Action? _detach=detach;
        public void Dispose()=>Interlocked.Exchange(ref _detach,null)?.Invoke();
    }
}
