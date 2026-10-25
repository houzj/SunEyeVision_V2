using System;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;

namespace SunEyeVision.UI.Services.Solutions;

/// <summary>Application entry points for the editable document; no controls or workflow scheduling.</summary>
public sealed class SolutionApplicationService
{
    private readonly SolutionManager _manager;
    public SolutionApplicationService(SolutionManager manager) => _manager = manager;
    public Solution CreateNew() => _manager.CreateUntitledSolution();
    public SolutionSaveResult Save(string? target, Action commitEditors)
    {
        try { commitEditors(); return _manager.SaveCurrentDocument(target); }
        catch (Exception ex) when (ex is not SolutionSaveRecoveryRequiredException) { return new SolutionSaveResult(false, ex.Message); }
    }
}
