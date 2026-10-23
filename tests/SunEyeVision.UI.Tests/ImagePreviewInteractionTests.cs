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
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed partial class SolutionManagerUiTests
{
    public sealed class PreviewPolicyParameters : SunEyeVision.Plugin.SDK.Execution.Parameters.ToolParameters
    {
        public SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind SourceKind { get; set; }
        public SunEyeVision.Plugin.SDK.Vision.Contracts.ImageOutputFormat OutputFormat { get; set; }
        public long SequenceNumber { get; set; } = 1;
        public string FilePath { get; set; } = "";
        public bool Fail { get; set; }
    }

    public sealed class PreviewPolicyResults : SunEyeVision.Plugin.SDK.Execution.Results.ToolResults { }

    [SunEyeVision.Plugin.SDK.Core.Tool("PreviewPolicyImageLoad", "图像推进测试工具")]
    public sealed class PreviewPolicyTool : SunEyeVision.Plugin.SDK.Core.IToolPlugin<PreviewPolicyParameters, PreviewPolicyResults>
    {
        public static Action<PreviewPolicyParameters>? OnRun { get; set; }
        public PreviewPolicyResults Run(PreviewPolicyParameters parameters)
        {
            OnRun?.Invoke(parameters);
            if (parameters.Fail) throw new InvalidOperationException("expected test failure");
            return new PreviewPolicyResults { Status = SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Success };
        }
    }

    private static void PreviewCall(ImagePreviewControl control, string method, params object[] args) =>
        typeof(ImagePreviewControl).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, args);

    private static void PumpPreviewTask(ImagePreviewControl control, Task task)
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

    [Fact]
    public void Selection_and_removal_preserve_two_way_binding_and_current_image_identity()
    {
        Sta(() =>
        {
            var files = new[] { Path.GetTempFileName(), Path.GetTempFileName(), Path.GetTempFileName() };
            try
            {
                var source = new ImageInputSource("node");
                foreach (var file in files) source.AddImage(new ImageInfo { FilePath = file });
                var control = new ImagePreviewControl { ActiveInputSource = source, ImageCollection = source.Images };
                System.Windows.Data.BindingOperations.SetBinding(control, ImagePreviewControl.CurrentImageIndexProperty,
                    new System.Windows.Data.Binding(nameof(source.CurrentIndex)) { Source = source, Mode = System.Windows.Data.BindingMode.TwoWay });
                control.CurrentImageIndex = 2;
                var binding = System.Windows.Data.BindingOperations.GetBindingExpression(control, ImagePreviewControl.CurrentImageIndexProperty);
                Assert.NotNull(binding); binding!.UpdateSource(); Assert.Equal(2, source.CurrentIndex);
                PreviewCall(control, "ExecuteDeleteSingleImage", source.Images[0]);
                Assert.Equal(1, source.CurrentIndex); Assert.Equal(1, control.CurrentImageIndex);
                Assert.Equal(files[2], source.CurrentImage!.FilePath);
                Assert.NotNull(System.Windows.Data.BindingOperations.GetBindingExpression(control, ImagePreviewControl.CurrentImageIndexProperty));
                Assert.All(files, file => Assert.True(File.Exists(file)));
            }
            finally { foreach (var file in files) File.Delete(file); }
        });
    }

    [Fact]
    public void Restoring_images_filters_missing_files_and_remaps_selection_by_path()
    {
        Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var a = Path.Combine(root, "a.png"); var b = Path.Combine(root, "b.png");
                File.WriteAllBytes(a, new byte[] { 1 }); File.WriteAllBytes(b, new byte[] { 2 });
                var config = new ImageSourceConfig { FilePaths = new() { Path.Combine(root, "missing.png"), a, Path.Combine(root, "lost.png"), b }, CurrentIndex = 3, ForRunPaths = new() { b } };
                var source = new ImageInputSource("node"); source.LoadFromConfig(config);
                Assert.Equal(2, source.Count); Assert.Equal(1, source.CurrentIndex);
                Assert.Equal(b, source.CurrentImage!.FilePath); Assert.True(source.CurrentImage.IsForRun);
                Assert.Equal(4, config.FilePaths.Count); Assert.Equal(2, source.ToConfig().FilePaths.Count);
                config.CurrentIndex = 0; source.LoadFromConfig(config); Assert.Equal(-1, source.CurrentIndex);
                source.ClearImages(); Assert.True(File.Exists(a)); Assert.True(File.Exists(b));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void Right_click_and_programmatic_selection_do_not_execute_left_and_keys_execute_once()
    {
        Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var a = Path.Combine(root, "a.png"); var b = Path.Combine(root, "b.png");
                File.WriteAllBytes(a, new byte[] { 1 }); File.WriteAllBytes(b, new byte[] { 2 });
                var source = new ImageInputSource("node");
                source.AddImages(new[] { new ImageInfo { FilePath = a }, new ImageInfo { FilePath = b, IsForRun = true } });
                var control = new ImagePreviewControl { ActiveInputSource = source, ImageCollection = source.Images };
                var list = (ListBox)control.FindName("ThumbnailListBox"); list.ItemsSource = source.Images;
                typeof(ImagePreviewControl).GetField("_thumbnailListBox", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(control, list);
                var requests = 0; control.WorkflowExecutionRequested += (_, _) => requests++;
                list.SelectedItem = source.Images[1]; Assert.Equal(0, requests);
                var border = new Border { Tag = source.Images[0] };
                PreviewCall(control, "OnThumbnailClick", border, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right));
                Assert.Equal(0, requests);
                PreviewCall(control, "OnThumbnailClick", border, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
                Assert.Equal(1, requests); Assert.Equal(0, control.CurrentImageIndex);
                using var hwnd = new HwndSource(new HwndSourceParameters("preview-key-test") { Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000) });
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, hwnd, 0, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                PreviewCall(control, "OnThumbnailPreviewKeyDown", list, key);
                Assert.True(key.Handled); Assert.Equal(2, requests); Assert.Equal(1, control.CurrentImageIndex);
                control.ImageRunMode = ImageRunMode.运行选择;
                PreviewCall(control, "OnThumbnailClick", border, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
                Assert.Equal(2, requests);
                control.CurrentImageIndex = 0; Assert.Equal(2, requests);
            }
            finally { Directory.Delete(root, true); }
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
                var control = new ImagePreviewControl { ActiveInputSource = source, ImageCollection = source.Images, CurrentImageIndex = 0 };
                PumpPreviewTask(control, control.ImportFilePathsAsync(new[] { file, file + ".missing" }));
                Assert.Single(source.Images); Assert.Equal(0, control.CurrentImageIndex);
            }
            finally { File.Delete(file); }
        });
    }

    [Fact]
    public void Import_cancel_after_first_batch_keeps_committed_references_and_stops_late_appends()
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
                var control = new ImagePreviewControl { ActiveInputSource = source, ImageCollection = source.Images };
                source.CollectionChanged += (_, _) => { if (source.Count >= 256) control.CancelImportCommand.Execute(null); };
                PumpPreviewTask(control, control.ImportDirectoryAsync(directory));
                Assert.Equal(256, source.Count); Assert.Equal(256, source.ToConfig().FilePaths.Count);
                Assert.False(control.IsImporting); Assert.Contains("取消", control.ImportStatus);
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
                var control = new ImagePreviewControl { ActiveInputSource = old, ImageCollection = old.Images };
                old.CollectionChanged += (_, _) =>
                {
                    if (old.Count < 256) return;
                    control.ActiveInputSource = next; control.ImageCollection = next.Images; control.CurrentImageIndex = 1;
                };
                PumpPreviewTask(control, control.ImportFilePathsAsync(paths));
                Assert.Equal(256, old.Count); Assert.Equal(2, next.Count);
                Assert.Equal(1, control.CurrentImageIndex); Assert.Equal(1, next.CurrentIndex);
                Assert.False(control.IsImporting);
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    [Fact]
    public void Manual_execution_does_not_advance_but_default_successful_execution_does()
    {
        Sta(() =>
        {
            using var fixture = new PreviewSolutionFixture();
            var serviceField = typeof(SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer)
                .GetField("_solutionManager", BindingFlags.NonPublic | BindingFlags.Static)!;
            var previous = serviceField.GetValue(null);
            if (previous == null) serviceField.SetValue(null, fixture.Manager);
            SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(typeof(PreviewPolicyTool));
            try
            {
            var engine = new WorkflowEngine(Substitute.For<ILogger>());
            var root = engine.CreateWorkflow(Guid.NewGuid().ToString(), "手动自动推进测试");
            root.AddNode(new WorkflowNodeBase("disabled", "source", "source", "PreviewPolicyImageLoad")
            { Parameters = new PreviewPolicyParameters() });
            var tab = new WorkflowTabViewModel { Id = root.Id, Name = root.Name }; tab.SetNodes(root.Nodes); tab.SetConnections(root.Connections);
            var source = new ImageInputSource("disabled");
            source.AddImages(new[] { new ImageInfo { FilePath = fixture.CreateImage("one.png") },
                new ImageInfo { FilePath = fixture.CreateImage("two.png") } });
            source.CurrentIndex = 0;
            root.Nodes[0].InputSource = source;
            var snapshots = new RuntimeInputSnapshotService();
            var input = Substitute.For<IInputProvider>(); input.GetInputImageAsync().Returns(Task.FromResult<object?>(null));
            var manager = new WorkflowExecutionManager(input, engine, snapshots);
            var completed = 0; string? error = null;
            manager.WorkflowExecutionCompleted += (_, _) => completed++;
            manager.WorkflowExecutionError += (_, e) => error = e.ErrorMessage;
            manager.RunSingleAsync(tab, advanceLocalImage: false).GetAwaiter().GetResult();
            Assert.Null(error); Assert.Equal(1, completed); Assert.Equal(0, source.CurrentIndex);
            manager.RunSingleAsync(tab).GetAwaiter().GetResult();
            Assert.Null(error); Assert.Equal(2, completed); Assert.Equal(1, source.CurrentIndex);
            ((PreviewPolicyParameters)root.Nodes[0].Parameters).Fail = true;
            manager.RunSingleAsync(tab).GetAwaiter().GetResult();
            Assert.NotNull(error); Assert.Equal(1, source.CurrentIndex);
            using var cancel = new System.Threading.CancellationTokenSource(); cancel.Cancel();
            manager.RunSingleAsync(tab, cancel.Token).GetAwaiter().GetResult();
            Assert.Equal(1, source.CurrentIndex);
            }
            finally
            {
                SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.UnregisterTool("PreviewPolicyImageLoad");
                if (ReferenceEquals(serviceField.GetValue(null), fixture.Manager)) serviceField.SetValue(null, previous);
            }
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
            File.WriteAllBytes(path, new byte[] { 1 }); // Policy tool records paths; it does not decode pixels.
            return path;
        }
        public PreviewSolutionFixture()
        {
            Directory.CreateDirectory(_root);
            SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.MarkAsLoaded();
            Manager = new SolutionManager(new ApplicationDataLocationService(Path.Combine(_root, "locator.json"))
                .Prepare(Path.Combine(_root, "data")));
        }
        public void Dispose() => Directory.Delete(_root, true);
    }
}
