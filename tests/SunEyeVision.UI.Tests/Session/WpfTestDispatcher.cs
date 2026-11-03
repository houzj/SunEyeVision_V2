using System.Windows;
using System.Windows.Threading;

namespace SunEyeVision.TestShared;

/// <summary>All real WPF views in one test process use the application's owning dispatcher.</summary>
internal static class WpfTestDispatcher
{
    private static readonly Lazy<Dispatcher> Owned=new(()=>
    {
        Dispatcher? dispatcher=null;Exception? failure=null;
        using var ready=new ManualResetEventSlim();
        var thread=new Thread(()=>
        {
            try
            {
                var app=new System.Windows.Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                dispatcher=app.Dispatcher;
                foreach(var path in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+path,UriKind.Relative)});
            }
            catch(Exception error){failure=error;}
            finally{ready.Set();}
            if(failure is null)Dispatcher.Run();
        }){IsBackground=true,Name="正式WPF测试宿主"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        if(!ready.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("正式WPF测试Dispatcher未就绪。");
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return dispatcher!;
    });
    internal static void Invoke(Action action)
    {
        var dispatcher=System.Windows.Application.Current?.Dispatcher??Owned.Value;
        if(dispatcher.CheckAccess())action();
        else dispatcher.Invoke(action);
    }
}
