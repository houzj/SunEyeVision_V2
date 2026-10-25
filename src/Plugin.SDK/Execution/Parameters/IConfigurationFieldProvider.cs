using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>Current SDK model adapter to declared fields. Accessors edit only the supplied private draft.</summary>
public interface IConfigurationFieldProvider
{
    IReadOnlyList<ConfigurationFieldAccessor> GetConfigurationFields();
    ToolSchema? ConfigurationSchema=>null;
    IReadOnlyDictionary<string,string> RegionInputStorage=>new Dictionary<string,string>();
}

public sealed record ConfigurationFieldAccessor(Port Port, FieldPresentation Presentation, string StoragePath,
    Func<object?> Read, Action<object?> Write);
