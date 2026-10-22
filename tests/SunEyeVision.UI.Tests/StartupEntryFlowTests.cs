using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Services.Startup;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;
public sealed class StartupEntryFlowTests
{
    private static string Root()
    {
        var path = Path.Combine(Path.GetTempPath(), "SunEyeVision.StartupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
    [Fact]
    public void Start_page_preference_and_path_history_roundtrip_without_registration()
    {
        var root = Root(); var file = Path.Combine(root, "start-page.json");
        var settings = StartPageSettings.Load(file);
        Assert.False(settings.HideStartPage);
        settings.HideStartPage = true;
        settings.RecordOpened(Path.Combine(root, "a.solution"), "A");
        settings.RecordOpened(Path.Combine(root, "b.solution"), "B");
        settings.RecordOpened(Path.Combine(root, "a.solution"), "A updated");
        settings.Save(file);
        var loaded = StartPageSettings.Load(file);
        Assert.True(loaded.HideStartPage); Assert.Equal(2, loaded.RecentFiles.Count);
        Assert.Equal("A updated", loaded.RecentFiles[0].Name);
        Assert.False(File.Exists(Path.Combine(root, "a.solution"))); // Recording history never creates a solution.
    }
    [Fact]
    public void Isolated_launch_requires_marker_and_rejects_external_paths_and_devices()
    {
        var root = Root();
        Assert.Throws<InvalidDataException>(() => StartupLaunchContext.Parse(new[] { "--startup-test-root", root }));
        File.WriteAllText(Path.Combine(root, ".suneye-startup-test"), "isolated");
        var context = StartupLaunchContext.Parse(new[] { "--startup-test-root", root, Path.Combine(root, "a.solution") });
        Assert.True(context.IsIsolated);
        Assert.Throws<InvalidDataException>(() => context.ValidatePath(Path.Combine(Path.GetTempPath(), "outside.solution")));
        var solution = Solution.Create(); context.ValidateCandidate(solution);
        solution.Devices.Add(new Device());
        Assert.Throws<InvalidDataException>(() => context.ValidateCandidate(solution));
    }
    [Fact]
    public void File_request_is_kept_even_when_start_page_is_hidden()
    {
        var request = StartupLaunchContext.Parse(new[] { @"D:\Samples\explicit.solution" });
        Assert.Equal(@"D:\Samples\explicit.solution", request.SolutionPath);
        Assert.False(request.IsIsolated);
        Assert.Throws<ArgumentException>(() => StartupLaunchContext.Parse(new[] { "a.solution", "b.solution" }));
    }
    [Fact]
    public void Direct_activation_does_not_require_or_replace_catalog_entry_with_same_document_id()
    {
        var root = Root(); var first = Path.Combine(root, "first.solution"); var second = Path.Combine(root, "second.solution");
        var id = Guid.NewGuid().ToString();
        File.WriteAllText(first, "{\"Id\":\"" + id + "\",\"Name\":\"First\",\"Workflows\":[],\"Devices\":[],\"Communications\":[]}");
        File.WriteAllText(second, "{\"Id\":\"" + id + "\",\"Name\":\"Second\",\"Workflows\":[],\"Devices\":[],\"Communications\":[]}");
        var manager = new SolutionManager(root);
        manager.RegisterKnownSolution(new SolutionRepository().LoadMetadata(first)!);
        var candidate = manager.LoadSolutionOnly(second);
        Assert.NotNull(candidate);
        SolutionMetadata? eventMetadata = null;
        manager.CurrentSolutionChanged += (_, e) => eventMetadata = e.Metadata;
        manager.SetCurrentSolution(candidate!, registerInCatalog: false);
        Assert.Same(candidate, manager.CurrentSolution);
        Assert.Equal(second, manager.CurrentFilePath);
        Assert.Equal(first, Assert.Single(manager.GetAllMetadata()).FilePath);
        Assert.Equal(second, eventMetadata!.FilePath);
        Assert.Equal("Second", eventMetadata.Name);
    }
    [Fact]
    public void Bad_candidate_load_does_not_change_current_solution()
    {
        var root = Root(); var manager = new SolutionManager(root);
        var current = Solution.Create(); current.Name = "Keep";
        manager.SetCurrentSolution(current, registerInCatalog: false);
        var bad = Path.Combine(root, "bad.solution"); File.WriteAllText(bad, "{not-json}");
        Assert.Null(manager.LoadSolutionOnly(bad));
        Assert.Same(current, manager.CurrentSolution);
    }
    [Fact]
    public void Start_page_constructs_and_preference_failure_reverts_checkbox()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var page = new StartPageWindow(false, Array.Empty<RecentSolutionFile>(), _ => throw new IOException("denied"));
                page.Measure(new Size(1020, 690)); page.Arrange(new Rect(0, 0, 1020, 690)); page.UpdateLayout();
                var checkbox = (CheckBox)page.FindName("HideStartPageBox");
                checkbox.IsChecked = true;
                Assert.False(checkbox.IsChecked);
                Assert.Contains("未保存", ((TextBlock)page.FindName("StatusText")).Text);
                Assert.NotNull(page.FindName("RecentFilesList"));
                page.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (failure != null) throw failure;
    }
}
