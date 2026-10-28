using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("VM canvas UI")]
public sealed class ImageSourceDesignCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Public_design_context_uses_actual_local_camera_composition_without_device_connection(bool camera) => Sta(() =>
    {
        WithFactory((assembly,factory)=>
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(),reference=>reference.Name is "SunEyeVision.UI" or "SunEyeVision.Workflow" or "SunEyeVision.DeviceDriver");
            var context=Context(assembly,camera);
            var view=factory.CreateSettingsView(context.Draft,context.Options);
            var window=Show(view.View);
            try
            {
                var input=Assert.Single(Visuals<ImageSourceInputView>(view.View));
                Assert.Equal(camera,input.IsCamera);
                Assert.Equal(camera?Visibility.Collapsed:Visibility.Visible,input.SourceStrip.Visibility);
                Assert.False(input.CameraPicker.IsEnabled);Assert.False(input.OpenCameraButton.IsEnabled);
                Assert.Equal("设计态：未连接设备",input.CameraStatus.Text);
                Assert.Single(input.CameraPicker.Items);Assert.Empty(input.SourceStrip.Session!.Items);
                Assert.True(input.SourceStrip.Session.IsReadOnly);
                Assert.Throws<InvalidOperationException>(()=>input.SourceStrip.Session.RequestAsync(
                    new ImageSourceStripRequest(ImageSourceStripRequestKind.Clear,input.SourceStrip.Session.Version),CancellationToken.None).GetAwaiter().GetResult());
                Assert.Equal(camera?"camera":"local",context.Draft.ReadFields().Get<string>("source.kind"));
                Assert.True(view.TryCapture(out _,out var references,out var error),error);Assert.Empty(references);
                view.Dispose();Assert.Null(input.SourceStrip.Session);
            }
            finally{window.Content=null;window.Close();view.Dispose();}
        });
    });

    [Fact]
    public void Actual_field_edit_changes_same_draft_and_refreshes_source_visibility_and_auto_switch() => Sta(() =>
    {
        WithFactory((assembly,factory)=>
        {
            var context=Context(assembly,false);using var view=factory.CreateSettingsView(context.Draft,context.Options);
            var window=Show(view.View);
            try
            {
                var changed=0;view.Changed+=()=>changed++;
                var input=Assert.Single(Visuals<ImageSourceInputView>(view.View));
                var oldSession=input.SourceStrip.Session;
                Assert.True(Field(view,"local.auto-switch").TrySetConstant(false));
                Assert.False(context.Draft.ReadFields().Get<bool>("local.auto-switch"));
                Assert.False(input.SourceStrip.Session!.AutoSwitchEnabled);Assert.NotSame(oldSession,input.SourceStrip.Session);
                Assert.True(Field(view,"source.kind").TrySetConstant("camera"));
                Assert.Equal("camera",context.Draft.ReadFields().Get<string>("source.kind"));Assert.True(input.IsCamera);
                Assert.Equal(Visibility.Collapsed,input.SourceStrip.Visibility);
                Assert.True(Field(view,"source.kind").TrySetConstant("local"));
                Assert.False(input.IsCamera);Assert.Equal(Visibility.Visible,input.SourceStrip.Visibility);
                Assert.False(input.SourceStrip.Session!.AutoSwitchEnabled);Assert.True(changed>=3);
                Assert.True(view.TryCapture(out _,out _,out var error),error);
            }
            finally{window.Content=null;window.Close();}
        });
    });

    [Fact]
    public void Public_rebind_rejects_old_source_callbacks_and_releases_each_attached_input_once() => Sta(() =>
    {
        WithFactory((assembly,factory)=>
        {
            var first=Context(assembly,false);var second=Context(assembly,true);
            // These contexts own disposable design input even when our callback probe replaces it.
            ((IDisposable)first.Options.InputContent!).Dispose();((IDisposable)second.Options.InputContent!).Dispose();
            var source=new CallbackSource();var next=new CallbackSource();var accepts=0;var nextAccepts=0;
            var view=factory.CreateSettingsView(first.Draft,new(InputContent:source,AcceptCandidate:(_,_)=>accepts++));
            var window=Show(view.View);
            try
            {
                var oldPrepare=source.Prepare!;var oldAccept=source.Accept!;
                oldPrepare();oldAccept(first.Draft.CaptureSettings());Assert.Equal(1,accepts);
                var changed=0;view.Changed+=()=>changed++;
                source.Notify();Assert.Equal(1,changed);Assert.Equal(0,source.DisposeCount);
                Bind(view,second.Draft,new(InputContent:next,AcceptCandidate:(_,_)=>nextAccepts++));window.UpdateLayout();
                Assert.Equal(1,source.DisposeCount);Assert.Equal(0,next.DisposeCount);
                Assert.Throws<InvalidOperationException>(oldPrepare);
                Assert.Throws<InvalidOperationException>(()=>oldAccept(first.Draft.CaptureSettings()));
                Assert.Equal(1,accepts);Assert.Equal(0,nextAccepts);
                source.Notify();Assert.Equal(1,changed); // previous Changed subscription was removed.
                Assert.Equal("camera",second.Draft.ReadFields().Get<string>("source.kind"));
                var currentPrepare=next.Prepare!;var currentAccept=next.Accept!;
                currentPrepare();currentAccept(second.Draft.CaptureSettings());Assert.Equal(1,nextAccepts);
                view.Dispose();view.Dispose();Assert.Equal(1,next.DisposeCount);Assert.Equal(1,source.DisposeCount);
                Assert.Throws<InvalidOperationException>(currentPrepare);
                Assert.Throws<InvalidOperationException>(()=>currentAccept(second.Draft.CaptureSettings()));
                Assert.Throws<ObjectDisposedException>(()=>Bind(view,second.Draft,new()));
                Assert.Equal(1,nextAccepts);
            }
            finally{window.Content=null;window.Close();view.Dispose();}
        });
    });

    private static DeclaredFieldEditor Field(IToolSettingsView view,string id)=>Visuals<DeclaredFieldEditor>(view.View).Single(field=>field.FieldId==id);
    private static (IToolSettingsDraft Draft,ToolSettingsViewOptions Options) Context(Assembly assembly,bool camera)
        =>((IToolSettingsDraft,ToolSettingsViewOptions))assembly.GetType("SunEyeVision.Tool.ImageSource.Wpf.Design.ImageSourceDesignData",true)!
            .GetMethod("CreateParameterContext",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,new object[]{camera})!;
    private static void Bind(IToolSettingsView view,IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {
        try{view.GetType().GetMethod("Bind",BindingFlags.Public|BindingFlags.Instance)!.Invoke(view,new object[]{draft,options});}
        catch(TargetInvocationException error)when(error.InnerException is not null){ExceptionDispatchInfo.Capture(error.InnerException).Throw();}
    }
    private static void WithFactory(Action<Assembly,IToolPresentationFactory> action)
    {
        var host=typeof(ImageSourceDesignCompositionTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item=>item.Key=="HostDebugRoot").Value!;
        var prior=AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
        try
        {
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.TrimEndingDirectorySeparator(Path.GetFullPath(host))+Path.DirectorySeparatorChar);
            Assert.True(PluginLoader.LoadPlugins(Path.Combine(host,"plugins")));
            var runtime=ToolRegistry.CreateRuntime("ImageLoad");
            using var registered=ToolRegistry.CreateSettingsView("ImageLoad",runtime.CreateSettings());
            Assert.Equal("SunEyeVision.Tool.ImageSource.Wpf.Views.ImageSourceParameterEditor",registered.GetType().FullName);
            var assembly=registered.GetType().Assembly;
            var factory=Assert.IsAssignableFrom<IToolPresentationFactory>(Activator.CreateInstance(assembly.GetType("SunEyeVision.Tool.ImageSource.Wpf.ImageSourceEditorFactory",true)!));
            action(assembly,factory);
        }
        finally{AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",prior);}
    }
    private static Window Show(FrameworkElement view)
    {var window=new Window{Content=view,Width=650,Height=760,Left=-10000,ShowActivated=false};window.Show();window.UpdateLayout();return window;}
    private static IEnumerable<T> Visuals<T>(DependencyObject root)where T:DependencyObject
    {
        if(root is T match)yield return match;
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)
            foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(root,index)))yield return child;
    }
    private static void Sta(Action action)
    {
        Exception? failure=null;var thread=new Thread(()=>{try{action();}catch(Exception error){failure=error;}});
        thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)),"设计组合验证超时");
        if(failure is not null)ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private sealed class CallbackSource:UserControl,IImageSourceSettingsContent,IDisposable
    {
        public Action? Prepare{get;private set;}public Action<object>? Accept{get;private set;}
        public int DisposeCount{get;private set;}
        public event Action? Changed;
        public void BindSourceDraft(IToolSettingsDraft draft,Action prepareEdit,Action<object> acceptCandidate){Prepare=prepareEdit;Accept=acceptCandidate;}
        public void RefreshSourceDraft(){}
        public void Notify()=>Changed?.Invoke();
        public void Dispose(){DisposeCount++;}
    }
}