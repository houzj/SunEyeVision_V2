using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Logical access to one declared field; paired fields borrow the same actual composition.</summary>
public sealed class DeclaredFieldAccess
{
    private readonly Func<ParameterSetting> _read;
    private readonly Func<bool> _alive;
    private readonly Func<bool> _invalid;
    private readonly Func<bool> _pending;
    private readonly Func<string> _error;
    private readonly Func<FrameworkElement?> _input;
    private readonly Func<object,bool> _set;
    private readonly Func<bool> _commit;
    private readonly Action<Action> _add,_remove;
    public string FieldId {get;}
    private readonly Func<FrameworkElement?>? _visual;
    private readonly FrameworkElement? _fixedView;
    private DeclaredFieldAccess? _attached;
    public FrameworkElement? View=>_attached?.View??_visual?.Invoke()??_fixedView;
    public ParameterSetting Setting=>_attached?.Setting??_read();
    public bool HasInvalidText=>!_alive()||(_attached?.HasInvalidText??_invalid());
    public bool HasPendingEdits=>_alive()&&(_attached?.HasPendingEdits??(_invalid()||_pending()));
    public string Error=>_alive()?(_attached?.Error??_error()):"字段编辑范围已释放。";
    public FrameworkElement? InputElement=>_alive()?(_attached?.InputElement??_input()):null;
    public bool TrySetConstant(object value)=>_alive()&&(_attached?.TrySetConstant(value)??_set(value));
    public bool TryCommitPending()=>_alive()&&(_attached?.TryCommitPending()??_commit());
    public event Action Changed {add{if(_alive())_add(value);}remove=>_remove(value);}
    internal DeclaredFieldAccess(string id,FrameworkElement view,Func<ParameterSetting> read,Func<bool> invalid,Func<bool> pending,Func<string> error,
        Func<FrameworkElement?> input,Func<object,bool> set,Func<bool> commit,Action<Action> add,Action<Action> remove,Func<bool> alive)
    {FieldId=id;_fixedView=view;_read=read;_invalid=invalid;_pending=pending;_error=error;_input=input;_set=set;_commit=commit;_add=add;_remove=remove;_alive=alive;}
    internal DeclaredFieldAccess(DeclaredFieldState state,Func<bool> alive)
    {
        FieldId=state.Port.Id;_read=()=>state.Accepted;_invalid=()=>state.HasInvalidInput;_pending=()=>false;_error=()=>state.Error;
        _input=()=>null;_set=value=>value is not null&&state.TrySet(new ConstantSetting(value));_commit=()=>true;
        _add=handler=>state.Changed+=handler;_remove=handler=>state.Changed-=handler;_alive=()=>!state.IsDisposed&&alive();
    }
    internal void AttachVisual(DeclaredFieldAccess visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if(visual.FieldId!=FieldId)throw new ArgumentException("视觉编辑器字段身份不匹配。",nameof(visual));
        if(_attached is not null&&!ReferenceEquals(_attached,visual))throw new InvalidOperationException("字段已绑定另一个会话视觉编辑器。");
        _attached=visual;
    }
    internal DeclaredFieldAccess(DeclaredFieldEditor editor):this(editor.FieldId,editor,()=>editor.Setting,()=>editor.HasInvalidText,()=>editor.HasPendingEdits,()=>editor.Error,
        ()=>editor.InputElement,editor.TrySetConstant,editor.TryCommitPending,handler=>editor.Changed+=handler,handler=>editor.Changed-=handler,()=>!editor.IsDisposed){}
}
