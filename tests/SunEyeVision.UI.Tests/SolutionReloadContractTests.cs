using System;
using System.IO;
using System.Threading.Tasks;
using SunEyeVision.UI.Services.Solutions;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class SolutionReloadContractTests
{
    private static SolutionManager Manager()
    {
        var root = Path.Combine(Path.GetTempPath(), "SunEye-B1-session", Guid.NewGuid().ToString("N"));
        return new SolutionManager(Path.Combine(root, "solutions"), Path.Combine(root, "settings.json"), Path.Combine(root, "recovery"), root);
    }

    [Fact]
    public async Task Candidate_error_preserves_document_and_path_without_transition()
    {
        var manager = Manager(); var old = manager.CreateUntitledSolution();
        var prepared = false;
        var result = await new WorkspaceOpenCoordinator().OpenAsync(manager, () => throw new IOException("broken file"),
            () => { prepared = true; return Task.FromResult(true); }, _ => Task.CompletedTask);
        Assert.Equal(WorkspaceOpenStatus.RejectedBeforeCommit, result.Status);
        Assert.False(prepared); Assert.Same(old, manager.CurrentSolution); Assert.Null(manager.CurrentFilePath);
    }

    [Fact]
    public async Task Cancel_preserves_current_session()
    {
        var manager = Manager(); var old = manager.CreateUntitledSolution(); var candidate = Solution.Create();
        var result = await new WorkspaceOpenCoordinator().OpenAsync(manager, () => candidate, () => Task.FromResult(false), _ => Task.CompletedTask);
        Assert.False(result.Committed); Assert.Same(old, manager.CurrentSolution);
    }

    [Fact]
    public async Task Activation_failure_is_committed_degraded_never_claims_old_workspace()
    {
        var manager = Manager(); manager.CreateUntitledSolution(); var candidate = Solution.Create();
        var result = await new WorkspaceOpenCoordinator().OpenAsync(manager, () => candidate, () => Task.FromResult(true),
            _ => throw new InvalidOperationException("tab assembly failed"));
        Assert.Equal(WorkspaceOpenStatus.CommittedDegraded, result.Status); Assert.True(result.Committed);
        Assert.Same(candidate, manager.CurrentSolution);
    }

    [Fact]
    public void First_save_binds_path_only_on_success_and_later_edits_remain_dirty()
    {
        var manager = Manager(); var document = manager.CreateUntitledSolution();
        document.Description = "content before save";
        Assert.False(manager.SaveCurrentDocument().FileWritten); Assert.Null(manager.CurrentFilePath);
        var path = Path.Combine(manager.DataRoot, "sample.solution");
        Assert.True(manager.SaveCurrentDocument(path).FileWritten); Assert.Equal(Path.GetFullPath(path), manager.CurrentFilePath);
        Assert.False(manager.HasUnsavedChanges);
        document.Description = "later edit"; Assert.True(manager.HasUnsavedChanges);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.False(manager.SaveCurrentDocument().FileWritten);
        Assert.Equal("later edit", document.Description); Assert.True(manager.HasUnsavedChanges);
    }

    [Fact]
    public void Legacy_close_cannot_discard_an_untitled_dirty_document_when_save_is_requested()
    {
        var manager = Manager(); var document = manager.CreateUntitledSolution(); document.Description = "unsaved";
        Assert.Throws<IOException>(() => manager.CloseSolution(saveChanges: true));
        Assert.Same(document, manager.CurrentSolution); Assert.Equal("unsaved", document.Description);
    }
}
