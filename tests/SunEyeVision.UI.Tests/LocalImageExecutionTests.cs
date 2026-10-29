using System.Reflection;
using NSubstitute;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
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
    private sealed class LocalImageExecutionFixture : IDisposable
    {
        private readonly PreviewSolutionFixture _files = new();
        private readonly FieldInfo _serviceField = typeof(SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer)
            .GetField("_solutionManager", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previous;
        public WorkflowEngine Engine { get; } = new(Substitute.For<ILogger>());
        public SunEyeVision.Workflow.Workflow Root { get; }
        public WorkflowTabViewModel Tab { get; }
        public IInputProvider Input { get; } = Substitute.For<IInputProvider>();
        public RuntimeInputSnapshotService Snapshots { get; } = new();
        public WorkflowExecutionManager Manager { get; }
        public List<string> Errors { get; } = new();
        public Action<string>? OnResult {get;set;}
        public LocalImageExecutionFixture()
        {
            _previous = _serviceField.GetValue(null);
            if (_previous == null) _serviceField.SetValue(null, _files.Manager);
            _ = ImageSourceTestRuntime.Configuration();
            Root = Engine.CreateWorkflow(Guid.NewGuid().ToString(), "运行序列执行测试");
            Tab = new WorkflowTabViewModel { Id = Root.Id, Name = Root.Name };
            Tab.SetNodes(Root.Nodes); Tab.SetConnections(Root.Connections);
            Input.GetInputImageAsync().Returns(Task.FromResult<object?>(null));
            Manager = new WorkflowExecutionManager(Input, Engine, Snapshots) { AllowUnhostedRunForTests = true };
            Manager.WorkflowExecutionError += (_, e) => Errors.Add(e.ErrorMessage);
            Manager.NodeExecutionCompleted += (_, e) =>
            {
                using var reader=e.Result.AcquireReader();
                if(reader.Values is {} values)OnResult?.Invoke(values.Get<string>("output.file"));
            };
        }
        public ImageInputSource AddSource(string id, int images, SunEyeVision.Workflow.Workflow? owner = null)
        {
            var source = new ImageInputSource(id);
            source.AddImages(Enumerable.Range(0, images).Select(index => new ImageInfo
            {
                FilePath = _files.CreateImage(id + index + ".png")
            }));
            source.CurrentIndex = images > 0 ? 0 : -1;
            var node = new SunEyeVision.UI.Shared.WorkflowNode(id,id,id,"ImageLoad")
            {RuntimeConfiguration=ImageSourceTestRuntime.Configuration(),InputSource=source};
            source.CapturePolicy().ApplyTo(node);
            (owner ?? Root).AddNode(node);
            // This unhosted fixture explicitly publishes its configuration before runs.
            // Production uses NodeConfigurationEditHost rather than this fixture callback.
            source.ConfigurationWriter = configuration =>
            {
                ImageSourceTestRuntime.Edit(node,policy=>policy.UpdateLocalInput(configuration.Configuration));
                if(configuration.SelectStart)Manager.ResetLocalImageCursor((owner ?? Root).Id, id);
            };
            return source;
        }
        public void Dispose()
        {
            OnResult = null;
            if (ReferenceEquals(_serviceField.GetValue(null), _files.Manager)) _serviceField.SetValue(null, _previous);
            _files.Dispose();
        }
    }

    [Fact]
    public void Continuous_sources_stop_at_last_valid_selected_entry_without_advancing_other_sources()
    {
        Sta(() =>
        {
            using var fixture = new LocalImageExecutionFixture();
            var a = fixture.AddSource("a", 2); var b = fixture.AddSource("b", 3);
            a.RunMode = ImageRunMode.运行选择;
            foreach (var image in a.Images) image.IsForRun = true;
            var missing = a.Images[0].FilePath + ".missing";
            a.AddImage(new ImageInfo { FilePath = missing, IsForRun = true });
            ImageSourceTestRuntime.Edit(fixture.Root.Nodes[0],policy=>policy.StopAtLastImage=true);
            var paths = new System.Collections.Concurrent.ConcurrentQueue<string>();
            fixture.OnResult = path => paths.Enqueue(path);
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Manager.WorkflowExecutionStopped += (_, _) => stopped.TrySetResult();
            Assert.True(fixture.Manager.StartContinuousRun(fixture.Tab));
            try { stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
            finally { fixture.Manager.StopAndWaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
            Assert.Empty(fixture.Errors);
            Assert.Equal(4, paths.Count);
            Assert.DoesNotContain(missing, paths);
            Assert.Equal(1, a.CurrentIndex); Assert.Equal(1, b.CurrentIndex);
            Assert.Contains(missing, a.CapturePolicy().FilePaths);
        });
    }

    [Fact]
    public void Mode_edits_preserve_display_and_execution_cursor_but_explicit_choice_resets_it()
    {
        Sta(()=>
        {
            using var fixture=new LocalImageExecutionFixture();
            var a=fixture.AddSource("a",3);var b=fixture.AddSource("b",3);
            var seen=new List<string>();fixture.OnResult=path=>seen.Add(path);
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Equal(1,a.CurrentIndex);Assert.Equal(0,a.CapturePolicy().CurrentIndex);
            foreach(var image in a.Images)image.IsForRun=true;
            a.AutoSwitchEnabled=false;a.RunMode=ImageRunMode.运行选择;
            Assert.Equal(1,a.CurrentIndex);Assert.Equal(0,a.CapturePolicy().CurrentIndex);
            seen.Clear();fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);Assert.Equal(new[]{a.Images[1].FilePath,b.Images[1].FilePath},seen);
            a.SelectImage(0);
            seen.Clear();fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);Assert.Equal(new[]{a.Images[0].FilePath,b.Images[2].FilePath},seen);
        });
    }

    [Fact]
    public void Same_start_reset_during_a_round_restarts_only_that_source_on_the_next_round()
    {
        Sta(()=>
        {
            using var fixture=new LocalImageExecutionFixture();
            var a=fixture.AddSource("a",3);var b=fixture.AddSource("b",3);
            ImageSourceTestRuntime.Edit(fixture.Root.Nodes[1],policy=>policy.StopAtLastImage=true);
            var seen=new System.Collections.Concurrent.ConcurrentQueue<string>();
            var reset=0;
            fixture.OnResult=path=>
            {
                seen.Enqueue(path);
                if(path==a.Images[0].FilePath&&Interlocked.Exchange(ref reset,1)==0)
                    fixture.Manager.ResetLocalImageCursor(fixture.Root.Id,"a");
            };
            var stopped=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Manager.WorkflowExecutionStopped+=(_,_)=>stopped.TrySetResult();
            Assert.True(fixture.Manager.StartContinuousRun(fixture.Tab));
            try{stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();}
            finally{fixture.Manager.StopAndWaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();}
            Assert.Empty(fixture.Errors);
            Assert.Equal(new[]{a.Images[0].FilePath,b.Images[0].FilePath,a.Images[0].FilePath,b.Images[1].FilePath,
                a.Images[1].FilePath,b.Images[2].FilePath},seen);
            Assert.Equal(0,ImageSourceTestRuntime.Read(fixture.Root.Nodes[0]).CurrentIndex);
            Assert.Equal(1,a.CurrentIndex);Assert.Equal(2,b.CurrentIndex);
        });
    }

    [Fact]
    public void Repeated_single_runs_keep_execution_cursor_without_a_preview_and_explicit_choice_resets_only_its_source()
    {
        Sta(() =>
        {
            using var fixture = new LocalImageExecutionFixture();
            var a = fixture.AddSource("a", 3); var b = fixture.AddSource("b", 3);
            var seen = new List<string>();
            fixture.OnResult = path => seen.Add(path);
            fixture.Root.Nodes[0].InputSource = null;
            fixture.Root.Nodes[1].InputSource = null;
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);
            Assert.Equal(new[] { a.Images[0].FilePath, b.Images[0].FilePath, a.Images[1].FilePath, b.Images[1].FilePath }, seen);
            Assert.Equal(0, ImageSourceTestRuntime.Read(fixture.Root.Nodes[0]).CurrentIndex);
            fixture.Manager.ResetLocalImageCursor(fixture.Root.Id, "a");
            seen.Clear();
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);
            Assert.Equal(new[] { a.Images[0].FilePath, b.Images[2].FilePath }, seen);
        });
    }

    [Fact]
    public void Stale_preview_list_cannot_replace_a_published_source_or_receive_its_cursor()
    {
        Sta(() =>
        {
            using var fixture = new LocalImageExecutionFixture();
            var original = fixture.AddSource("source", 2);
            var stale = new ImageInputSource("source");
            stale.AddImages(new[] { new ImageInfo { FilePath = "unpublished.png" } }, selectFirstAdded: true);
            fixture.Root.Nodes[0].InputSource = stale;
            string? seen = null;
            fixture.OnResult = path => seen = path;
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);
            Assert.Equal(original.Images[0].FilePath, seen);
            Assert.Equal(0, stale.CurrentIndex);
            Assert.Equal("unpublished.png", stale.CurrentImage!.FilePath);
        });
    }

    [Fact]
    public void Cancellation_while_frozen_input_is_in_flight_never_advances_a_source()
    {
        Sta(() =>
        {
            using var fixture = new LocalImageExecutionFixture();
            var source = fixture.AddSource("a", 2);
            using var release = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Input.GetInputImageAsync().Returns(_ => Task.Run<object?>(() =>
            { entered.TrySetResult(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); return null; }));
            var run = fixture.Manager.RunSingleAsync(fixture.Tab, cancellation.Token);
            try
            {
                entered.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                cancellation.Cancel();
            }
            finally { release.Set(); }
            run.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            Assert.Equal(0, source.CurrentIndex);
            Assert.False(fixture.Manager.IsRunning(fixture.Root.Id));
        });
    }

    [Fact]
    public void Solution_execution_captures_each_workflow_source_and_does_not_auto_advance()
    {
        Sta(() =>
        {
            using var fixture = new LocalImageExecutionFixture();
            var a = fixture.AddSource("a", 2);
            var other = fixture.Engine.CreateWorkflow(Guid.NewGuid().ToString(), "other");
            var b = fixture.AddSource("b", 2, other); b.CurrentIndex = 1;
            var tab = new WorkflowTabViewModel { Id = other.Id, Name = other.Name };
            tab.SetNodes(other.Nodes); tab.SetConnections(other.Connections);
            var solution = new Solution(); solution.Workflows.Add(fixture.Root); solution.Workflows.Add(other);
            var paths = new System.Collections.Concurrent.ConcurrentQueue<string>();
            fixture.OnResult = path => paths.Enqueue(path);
            fixture.Snapshots.Publish(other.Id, "wrong-preview.png", 99);
            fixture.Manager.RunSolutionAsync(new[] { fixture.Tab, tab }, solution).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);
            Assert.Equal(new[] { a.Images[0].FilePath, b.Images[1].FilePath }, paths.ToArray());
            Assert.Equal(0, a.CurrentIndex); Assert.Equal(1, b.CurrentIndex);
        });
    }

    [Fact]
    public void Frozen_execution_ignores_other_preview_and_does_not_overwrite_a_new_user_selection()
    {
        Sta(() =>
        {
            using var fixture = new LocalImageExecutionFixture();
            var source = fixture.AddSource("source", 3);
            var expectedPath = source.Images[0].FilePath;
            fixture.Input.GetInputImageAsync().Returns(_ =>
            {
                source.SelectImage(2); // A new explicit user choice is a formal edit after this run captured its input.
                fixture.Snapshots.Publish("another-workflow", "unrelated.png", 9);
                return Task.FromResult<object?>(null);
            });
            string? observedPath = null;
            fixture.OnResult = path => observedPath = path;
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);
            Assert.Equal(expectedPath, observedPath);
            Assert.Equal(2, source.CurrentIndex);
            Assert.Equal(2,ImageSourceTestRuntime.Read(fixture.Root.Nodes[0]).CurrentIndex); // The frozen old invocation must not overwrite the newer accepted formal choice.
        });
    }

    [Fact]
    public void Success_advances_each_source_and_missing_or_empty_selection_is_rejected_before_dispatch()
    {
        Sta(() =>
        {
            using var fixture = new LocalImageExecutionFixture();
            var a = fixture.AddSource("a", 2); var b = fixture.AddSource("b", 3);
            var paths = new List<string>();
            fixture.OnResult = path => paths.Add(path);
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors); Assert.Equal(2, paths.Count);
            Assert.Contains(a.Images[0].FilePath, paths); Assert.Contains(b.Images[0].FilePath, paths);
            Assert.Equal(1, a.CurrentIndex); Assert.Equal(1, b.CurrentIndex);
            a.ReplaceImage(a.CurrentImage!, a.CurrentImage!.FilePath + ".missing");
            paths.Clear();
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(paths); Assert.Contains(fixture.Errors, error => error.Contains("已选图片"));
            var uncheckedImage = a.Images[0];
            a.ClearImages(); a.AddImage(uncheckedImage); fixture.Errors.Clear();
            a.RunMode = ImageRunMode.运行选择;
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(paths); Assert.Contains(fixture.Errors, error => error.Contains("没有可运行"));
            Assert.Equal(1, b.CurrentIndex);
        });
    }
}
