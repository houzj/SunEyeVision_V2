using System.Windows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Logical access to one declared field; paired fields borrow the same actual composition.</summary>
public sealed class DeclaredFieldAccess
{
    private readonly Func<ParameterSetting> _read;
    private readonly Func<bool> _alive;
    private readonly Func<bool> _invalid;
    private readonly Func<string> _error;
    private readonly Func<FrameworkElement?> _input;
    private readonly Func<object,bool> _set;
    private readonly Func<bool> _commit;
    private readonly Action<Action> _add,_remove;
    public string FieldId {get;}
    public FrameworkElement View {get;}
    public ParameterSetting Setting=>_read();
    public bool HasInvalidText=>!_alive()||_invalid();
    public string Error=>_alive()?_error():"字段编辑范围已释放。";
    public FrameworkElement? InputElement=>_alive()?_input():null;
    public bool TrySetConstant(object value)=>_alive()&&_set(value);
    public bool TryCommitPending()=>_alive()&&_commit();
    public event Action Changed {add{if(_alive())_add(value);}remove=>_remove(value);}
    internal DeclaredFieldAccess(string id,FrameworkElement view,Func<ParameterSetting> read,Func<bool> invalid,Func<string> error,
        Func<FrameworkElement?> input,Func<object,bool> set,Func<bool> commit,Action<Action> add,Action<Action> remove,Func<bool> alive)
    {FieldId=id;View=view;_read=read;_invalid=invalid;_error=error;_input=input;_set=set;_commit=commit;_add=add;_remove=remove;_alive=alive;}
    internal DeclaredFieldAccess(DeclaredFieldEditor editor):this(editor.FieldId,editor,()=>editor.Setting,()=>editor.HasInvalidText,()=>editor.Error,
        ()=>editor.InputElement,editor.TrySetConstant,editor.TryCommitPending,handler=>editor.Changed+=handler,handler=>editor.Changed-=handler,()=>!editor.IsDisposed){}
}
