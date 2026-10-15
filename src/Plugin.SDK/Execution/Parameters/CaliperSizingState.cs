using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>Persisted initialization ownership, independent for each caliper.</summary>
public sealed class CaliperSizingState
{
    /// <summary>Old projects without this metadata keep their saved pixel dimensions.</summary>
    public bool PreserveExisting { get; set; }
    public HashSet<int> InitializedElements { get; set; } = new();

    public bool NeedsInitialization(int element) => !PreserveExisting && !InitializedElements.Contains(element);
    public void Preserve(int element) => InitializedElements.Add(element);
}
