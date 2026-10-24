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
        public LocalImageExecutionFixture()
        {
            _previous = _serviceField.GetValue(null);
            if (_previous == null) _serviceField.SetValue(null, _files.Manager);
            ToolRegistry.RegisterTool(typeof(PreviewPolicyTool));
            Root = Engine.CreateWorkflow(Guid.NewGuid().ToString(), "运行序列执行测试");
            Tab = new WorkflowTabViewModel { Id = Root.Id, Name = Root.Name };
            Tab.SetNodes(Root.Nodes); Tab.SetConnections(Root.Connections);
            Input.GetInputImageAsync().Returns(Task.FromResult<object?>(null));
            Manager = new WorkflowExecutionManager(Input, Engine, Snapshots) { AllowLegacyEditorCommitForTests = true };
            Manager.WorkflowExecutionError += (_, e) => Errors.Add(e.ErrorMessage);
        }
        public ImageInputSource AddSource(string id, int images, SunEyeVision.Workflow.Workflow? owner = null)
        {
            var source = new ImageInputSource(id);
            source.AddImages(Enumerable.Range(0, images).Select(index => new ImageInfo
            {
                FilePath = _files.CreateImage(id + index + ".png"), SourceReference = "assets/" + id + index
            }));
            source.CurrentIndex = images > 0 ? 0 : -1;
            (owner ?? Root).AddNode(new WorkflowNodeBase(id, id, id, "PreviewPolicyImageLoad")
            {
                Parameters = new PreviewPolicyParameters(), InputSource = source,
                ImageSourceConfig = new ImageSourceConfig { NodeId = id }
            });
            return source;
        }
        public void Dispose()
        {
            PreviewPolicyTool.OnRun = null;
            ToolRegistry.UnregisterTool("PreviewPolicyImageLoad");
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
            fixture.Root.Nodes[0].ImageSourceConfig!.StopAtLastImage = true;
            var paths = new System.Collections.Concurrent.ConcurrentQueue<string>();
            PreviewPolicyTool.OnRun = parameters => paths.Enqueue(parameters.FilePath);
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Manager.WorkflowExecutionStopped += (_, _) => stopped.TrySetResult();
            Assert.True(fixture.Manager.StartContinuousRun(fixture.Tab));
            try { stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
            finally { fixture.Manager.StopAndWaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
            Assert.Empty(fixture.Errors);
            Assert.Equal(4, paths.Count);
            Assert.DoesNotContain(missing, paths);
            Assert.Equal(1, a.CurrentIndex); Assert.Equal(1, b.CurrentIndex);
            Assert.Contains(missing, a.ToConfig().FilePaths);
        });
    }

    [Fact]
    public void Cancellation_while_tool_is_in_flight_never_advances_a_source()
    {
        Sta(() =>
        {
            using var fixture = new LocalImageExecutionFixture();
            var source = fixture.AddSource("a", 2);
            using var release = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            PreviewPolicyTool.OnRun = _ => { entered.TrySetResult(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); };
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
            PreviewPolicyTool.OnRun = parameters => paths.Enqueue(parameters.FilePath);
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
                source.CurrentIndex = 2; // Simulate a UI edit after this run captured its input.
                fixture.Snapshots.Publish("another-workflow", "unrelated.png", 9);
                return Task.FromResult<object?>(null);
            });
            string? observedPath = null;
            PreviewPolicyTool.OnRun = parameters => observedPath = parameters.FilePath;
            fixture.Manager.RunSingleAsync(fixture.Tab).GetAwaiter().GetResult();
            Assert.Empty(fixture.Errors);
            Assert.Equal(expectedPath, observedPath);
            Assert.Equal(2, source.CurrentIndex);
            Assert.Equal(string.Empty, ((PreviewPolicyParameters)fixture.Root.Nodes[0].Parameters).FilePath);
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
            PreviewPolicyTool.OnRun = parameters => paths.Add(parameters.FilePath);
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
