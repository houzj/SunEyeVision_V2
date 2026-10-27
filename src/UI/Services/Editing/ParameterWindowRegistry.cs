using System.Windows;
namespace SunEyeVision.UI.Services.Editing;
/// <summary>宿主拥有的独立参数窗口；创建到关闭均占编辑范围，取消关闭时继续持有。</summary>
public sealed class ParameterWindowRegistry
{
    private readonly Dictionary<Window,Registration> _windows=new();
    public int Count=>_windows.Count;
    public event EventHandler? Changed;
    public IDisposable Track(Window window)
    {
        window.Dispatcher.VerifyAccess();
        if(_windows.ContainsKey(window))throw new InvalidOperationException("参数窗口已登记。");
        var registration=new Registration(this,window);_windows.Add(window,registration);Changed?.Invoke(this,EventArgs.Empty);return registration;
    }
    public bool TryCloseAll()
    {
        foreach(var registration in _windows.Values.ToArray())
        {
            registration.Window.Close();
            if(_windows.ContainsKey(registration.Window))return false; // Closing取消，不能继续切换文档。
        }
        return true;
    }
    private sealed class Registration : IDisposable
    {
        private ParameterWindowRegistry? _owner;
        public Window Window {get;}
        public Registration(ParameterWindowRegistry owner,Window window){_owner=owner;Window=window;Window.Closed+=Closed;}
        private void Closed(object? sender,EventArgs e)=>Dispose();
        public void Dispose()
        {
            Window.Dispatcher.VerifyAccess();var owner=_owner;if(owner==null)return;_owner=null;
            Window.Closed-=Closed;owner._windows.Remove(Window);owner.Changed?.Invoke(owner,EventArgs.Empty);
        }
    }
}