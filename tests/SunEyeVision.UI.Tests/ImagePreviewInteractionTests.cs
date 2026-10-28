using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using NSubstitute;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls.Panels;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed partial class SolutionManagerUiTests
{
    private static void PreviewCall(ImageSourceStrip control, string method, params object[] args) =>
        typeof(ImageSourceStrip).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, args);

    private static Task SourceImport(ImageSourceStripSession session,string method,params object[] arguments)
        =>(Task)typeof(ImageSourceStripSession).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,arguments)!;

    private static void PumpPreviewTask(ImageSourceStrip control, Task task)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        _ = task.ContinueWith(_ => control.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        timer.Stop();
        Assert.True(task.IsCompleted, "图像导入超时");
        task.GetAwaiter().GetResult();
    }

    private sealed class NativeSourceFixture : IDisposable
    {
        public ImageInputSource Source {get;}
        public ImageSourceStripSession Session {get;}
        public ImageSourceStrip Control {get;}
        private readonly Window _window;
        public NativeSourceFixture(ImageInputSource source,Func<bool>? current=null,Func<ImageInfo,int,long,Task>? activate=null)
        {
            Source=source;Session=new(source,current??(()=>true),async(image,index,version)=>
            {
                Assert.Equal(version,source.EditVersion);source.SelectImage(index);
                if(activate is not null)await activate(image,index,version);
            });
            Control=new ImageSourceStrip{Session=Session};
            _window=new Window{Content=Control,Width=600,Height=180,Left=-10000,ShowActivated=false};
            _window.Show();_window.UpdateLayout();
            Control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        }
        public Task Request(ImageSourceStripRequestKind kind,string? itemId=null,IEnumerable<string>? paths=null)
            =>Session.RequestAsync(new(kind,Session.Version,itemId,paths:paths),CancellationToken.None);
        public void Dispose(){_window.Close();Control.Session=null;Session.Dispose();}
    }

    [Fact]
    public void Unchecked_activation_in_selection_mode_preserves_start_history_and_execution()
    {
        Sta(()=>
        {
            var source=new ImageInputSource("node");
            var selected=new ImageInfo{FilePath="selected.png"};var uncheckedImage=new ImageInfo{FilePath="unchecked.png"};
            source.AddImages(new[]{selected,uncheckedImage},selectFirstAdded:true);
            source.SetRunSelection(new[]{selected.Id});source.RunMode=ImageRunMode.运行选择;
            var version=source.EditVersion;var writes=0;var executions=0;
            source.ConfigurationWriter=_=>writes++;
            using var fixture=new NativeSourceFixture(source,activate:(_,_,_)=>{executions++;return Task.CompletedTask;});
            PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.Activate,uncheckedImage.Id));
            PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.SelectStart,uncheckedImage.Id));
            Assert.Throws<InvalidOperationException>(()=>source.SelectImage(1));
            Assert.Equal(0,writes);Assert.Equal(0,executions);Assert.Equal(version,source.EditVersion);
            Assert.Equal(0,source.CapturePolicy().CurrentIndex);Assert.Same(selected,source.CurrentImage);
            Assert.False(uncheckedImage.IsForRun);Assert.Equal(0,((ListBox)fixture.Control.FindName("lst")).SelectedIndex);
            Assert.Contains("已勾选",fixture.Session.Status);
            PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.Activate,selected.Id));
            Assert.Equal(1,executions);
        });
    }

    [Fact]
    public void Selection_and_removal_preserve_current_image_identity_and_one_way_display()
    {
        Sta(()=>
        {
            var files=new[]{Path.GetTempFileName(),Path.GetTempFileName(),Path.GetTempFileName()};
            try
            {
                var source=new ImageInputSource("node");foreach(var file in files)source.AddImage(new ImageInfo{FilePath=file});
                using var fixture=new NativeSourceFixture(source);
                source.SelectImage(2);
                PumpPreviewTask(fixture.Control,Task.CompletedTask);
                var list=(ListBox)fixture.Control.FindName("lst");
                Assert.Equal(2,list.SelectedIndex);var selected=source.CurrentImage;
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.Remove,source.Images[0].Id));
                Assert.Equal(1,source.CurrentIndex);Assert.Equal(1,list.SelectedIndex);Assert.Same(selected,source.CurrentImage);
                Assert.Equal(files[2],source.CurrentImage!.FilePath);Assert.All(files,file=>Assert.True(File.Exists(file)));
                // Programmatic list selection is presentation only and cannot write formal source selection.
                list.SelectedIndex=0;Assert.Equal(1,source.CurrentIndex);
            }
            finally{foreach(var file in files)File.Delete(file);}
        });
    }

    [Fact]
    public void Restoring_images_preserves_missing_references_selection_and_run_flags_without_rewriting_configuration()
    {
        Sta(() =>
        {
            var root = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? throw new InvalidOperationException("Missing validation root"), "restore-images", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var a = Path.Combine(root, "a.png"); var b = Path.Combine(root, "b.png");
                File.WriteAllBytes(a, new byte[] { 1 }); File.WriteAllBytes(b, new byte[] { 2 });
                var config = new ImageSourcePolicy { FilePaths = new() { Path.Combine(root, "missing.png"), a, Path.Combine(root, "lost.png"), b }, CurrentIndex = 3, ForRunPaths = new() { b } };
                var source = new ImageInputSource("node");
                var resolver = new SunEyeVision.Workflow.Persistence.SolutionResourceResolver(root);
                source.LoadFromPolicy(config, resolver);
                Assert.Equal(4, source.Count); Assert.Equal(3, source.CurrentIndex);
                Assert.Equal(b, source.CurrentImage!.FilePath); Assert.True(source.CurrentImage.IsForRun);
                Assert.Equal(config.FilePaths, source.CapturePolicy().FilePaths);
                Assert.Equal(config.ForRunPaths, source.CapturePolicy().ForRunPaths);
                config.CurrentIndex = 0; source.LoadFromPolicy(config, resolver); Assert.Equal(0, source.CurrentIndex);
                Assert.Equal(config.FilePaths[0], source.CurrentImage!.SourceReference);
                Assert.False(File.Exists(source.CurrentImage.FilePath));
                source.ClearImages(); Assert.True(File.Exists(a)); Assert.True(File.Exists(b));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void Unresolved_relative_image_keeps_reference_and_can_be_resolved_without_current_directory_fallback()
    {
        Sta(() =>
        {
            var root = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")
                ?? throw new InvalidOperationException("Missing validation root"), "relative-image", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, "sample.png"); File.WriteAllBytes(file, new byte[] { 1 });
            try
            {
                var config = new ImageSourcePolicy { FilePaths = new() { "sample.png" }, ForRunPaths = new() { "sample.png" }, CurrentIndex = 0 };
                var source = new ImageInputSource("relative");
                source.LoadFromPolicy(config, new SunEyeVision.Workflow.Persistence.SolutionResourceResolver(null));
                var unresolved = Assert.Single(source.Images);
                Assert.Equal("sample.png", unresolved.SourceReference); Assert.Empty(unresolved.FilePath);
                Assert.False(string.IsNullOrWhiteSpace(unresolved.ResourceError)); Assert.True(unresolved.IsForRun);
                Assert.Equal(config.FilePaths, source.CapturePolicy().FilePaths);
                source.LoadFromPolicy(config, new SunEyeVision.Workflow.Persistence.SolutionResourceResolver(root));
                var resolved = Assert.Single(source.Images);
                Assert.Equal(file, resolved.FilePath); Assert.Null(resolved.ResourceError); Assert.True(resolved.IsForRun);
                Assert.Equal("sample.png", Assert.Single(source.CapturePolicy().FilePaths));
                source.ClearImages(); Assert.True(File.Exists(file));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void Right_click_and_programmatic_selection_do_not_execute_left_and_keys_execute_once()
    {
        Sta(()=>
        {
            var source=new ImageInputSource("node");source.AddImages(new[]{new ImageInfo{FilePath="a.png"},new ImageInfo{FilePath="b.png",IsForRun=true}});
            var executions=0;
            using var fixture=new NativeSourceFixture(source,activate:(_,_,_)=>{executions++;return Task.CompletedTask;});
            var list=(ListBox)fixture.Control.FindName("lst");list.SelectedIndex=1;Assert.Equal(0,executions);
            var row=new Border{DataContext=fixture.Session.Items[0]};
            PreviewCall(fixture.Control,"Grid_MouseDown",row,new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Right));
            Assert.Equal(0,executions);
            PreviewCall(fixture.Control,"Grid_MouseDown",row,new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left));
            Assert.Equal(1,executions);Assert.Equal(0,source.CurrentIndex);
            using var hwnd=new HwndSource(new HwndSourceParameters("source-key-test"){Width=1,Height=1,WindowStyle=unchecked((int)0x80000000)});
            var key=new KeyEventArgs(Keyboard.PrimaryDevice,hwnd,0,Key.Right){RoutedEvent=Keyboard.PreviewKeyDownEvent};
            PreviewCall(fixture.Control,"ListKeyDown",list,key);Assert.True(key.Handled);Assert.Equal(2,executions);Assert.Equal(1,source.CurrentIndex);
            source.RunMode=ImageRunMode.运行选择;
            row.DataContext=fixture.Session.Items[0];
            // Original plain click activates even in selected-run mode; Ctrl is the run-selection gesture.
            PreviewCall(fixture.Control,"Grid_MouseDown",row,new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left));
            Assert.Equal(3,executions);
            source.CurrentIndex=1;Assert.Equal(3,executions);
            PreviewCall(fixture.Control,"FirstImage_Click",fixture.Control,new RoutedEventArgs());
            Assert.Equal(3,executions); // First eligible establishes a start; it does not execute.
        });
    }

    [Fact]
    public void Adding_missing_or_duplicate_images_does_not_change_current_selection()
    {
        Sta(() =>
        {
            var file = Path.GetTempFileName();
            try
            {
                var source = new ImageInputSource("node"); source.AddImage(new ImageInfo { FilePath = file });
                using var fixture=new NativeSourceFixture(source);
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.ImportFiles,paths:new[]{file,file+".missing"}));
                Assert.Single(source.Images);Assert.Equal(0,source.CurrentIndex);
            }
            finally { File.Delete(file); }
        });
    }

    [Fact]
    public void Import_cancel_before_acceptance_does_not_publish_or_change_selection()
    {
        Sta(() =>
        {
            var source = new ImageInputSource("source");
            source.AddImages(new[] { new ImageInfo { FilePath = "existing.png" } }, selectFirstAdded: true);
            using var fixture=new NativeSourceFixture(source);
            var files = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var writes = 0; source.ConfigurationWriter = _ => writes++;
            var task=SourceImport(fixture.Session,"ImportAsync",(Func<CancellationToken,Task<string[]>>)(_=>files.Task));
            fixture.Request(ImageSourceStripRequestKind.CancelImport).GetAwaiter().GetResult();
            files.SetResult(new[] { "late.png" });
            PumpPreviewTask(fixture.Control,task);
            Assert.Equal(0, writes); Assert.Single(source.Images);
            Assert.Equal("existing.png", source.CurrentImage!.FilePath);
            Assert.False(fixture.Session.IsImporting);
        });
    }

    [Fact]
    public void Import_cancel_after_atomic_acceptance_keeps_the_complete_committed_batch()
    {
        Sta(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "SunEyeVision-ImportCancel", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var paths = Enumerable.Range(0, 600).Select(index => Path.Combine(directory, index.ToString("D4") + ".png")).ToArray();
                foreach (var path in paths) File.WriteAllBytes(path, new byte[] { 1 });
                var source = new ImageInputSource("import");
                using var fixture=new NativeSourceFixture(source);
                source.CollectionChanged+=(_,_)=>fixture.Request(ImageSourceStripRequestKind.CancelImport).GetAwaiter().GetResult();
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.ImportDirectory,paths:new[]{directory}));
                Assert.Equal(600, source.Count); Assert.Equal(600, source.CapturePolicy().FilePaths.Count);
                Assert.False(fixture.Session.IsImporting);Assert.Contains("取消",fixture.Session.Status);
                Assert.All(paths, path => Assert.True(File.Exists(path)));
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    [Fact]
    public void Import_source_switch_during_commit_cannot_change_new_source_selection()
    {
        Sta(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "SunEyeVision-ImportSwitch", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var paths = Enumerable.Range(0, 600).Select(index => Path.Combine(directory, index.ToString("D4") + ".png")).ToArray();
                foreach (var path in paths) File.WriteAllBytes(path, new byte[] { 1 });
                var old = new ImageInputSource("old"); var next = new ImageInputSource("next");
                next.AddImages(paths.Take(2).Select(path => new ImageInfo { FilePath = path })); next.CurrentIndex = 1;
                ImageInputSource current=old;
                using var fixture=new NativeSourceFixture(old,()=>ReferenceEquals(current,old));
                using var nextSession=new ImageSourceStripSession(next,()=>ReferenceEquals(current,next));
                old.CollectionChanged += (_, _) =>
                {
                    current=next;fixture.Control.Session=nextSession;
                };
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.ImportFiles,paths:paths));
                Assert.Equal(600, old.Count); Assert.Equal(2, next.Count);
                Assert.Equal(1,nextSession.DisplayIndex);Assert.Equal(1,next.CurrentIndex);
                Assert.False(fixture.Session.IsImporting);
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    [Fact]
    public void Manual_execution_does_not_advance_but_default_successful_execution_does()
    {
        Sta(() =>
        {
            using var fixture=new LocalImageExecutionFixture();var source=fixture.AddSource("source",2);
            var completed=0;fixture.Manager.WorkflowExecutionCompleted+=(_,_)=>completed++;
            fixture.Manager.RunSingleAsync(fixture.Tab,advanceLocalImage:false).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);Assert.Equal(1,completed);Assert.Equal(0,source.CurrentIndex);
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);Assert.Equal(2,completed);Assert.Equal(1,source.CurrentIndex);
            // Existing file with invalid bytes passes path readiness and fails in the actual Runtime decoder.
            File.WriteAllBytes(source.CurrentImage!.FilePath,new byte[]{1});
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.NotEmpty(fixture.Errors);Assert.Equal(1,source.CurrentIndex);
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            fixture.Manager.RunSingleAsync(fixture.Tab,cancel.Token).GetAwaiter().GetResult();
            Assert.Equal(1,source.CurrentIndex);
        });
    }
    // This execution test needs a temporary manager, not the retired manager window/card fixture.
    private sealed class PreviewSolutionFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "SunEyeVision-PreviewPolicy", Guid.NewGuid().ToString("N"));
        public SolutionManager Manager { get; }
        public string CreateImage(string name)
        {
            var path = Path.Combine(_root, name);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
                2,1,96,96,System.Windows.Media.PixelFormats.Gray8,null,new byte[]{20,90},2)));
            using(var file=File.Create(path))encoder.Save(file);
            return path;
        }
        public PreviewSolutionFixture()
        {
            Directory.CreateDirectory(_root);
            Manager = new SolutionManager(new ApplicationDataLocationService(Path.Combine(_root, "locator.json"))
                .Prepare(Path.Combine(_root, "data")));
        }
        public void Dispose() => Directory.Delete(_root, true);
    }
}
