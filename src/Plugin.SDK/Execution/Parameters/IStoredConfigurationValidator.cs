using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>Pure declared settings validation, including inactive persisted backups. No source readiness, native execution or UI access.</summary>
public interface IStoredConfigurationValidator
{
    IReadOnlyList<Diagnostic> ValidateStoredConfiguration(IReadOnlyCollection<string>? boundParameterNames=null);
}
