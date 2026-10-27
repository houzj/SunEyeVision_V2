using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Independent snapshots for pure group validation. Resources remain borrowed, never resolved here.</summary>
public sealed class FieldGroupCandidate
{
    public IReadOnlyDictionary<string,ParameterSetting> Current {get;}
    public IReadOnlyDictionary<string,ParameterSetting> Changes {get;}
    public IReadOnlyDictionary<string,ParameterSetting> Proposed {get;}
    internal FieldGroupCandidate(IReadOnlyDictionary<string,ParameterSetting> current,IReadOnlyDictionary<string,ParameterSetting> changes)
    {
        Current=Copy(current);Changes=Copy(changes);
        var proposed=current.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
        foreach(var item in changes)proposed[item.Key]=item.Value;
        Proposed=Copy(proposed);
    }
    private static IReadOnlyDictionary<string,ParameterSetting> Copy(IEnumerable<KeyValuePair<string,ParameterSetting>> values)
        =>new ReadOnlyDictionary<string,ParameterSetting>(values.ToDictionary(x=>x.Key,x=>DeclaredFieldState.CopySetting(x.Value),StringComparer.Ordinal));
}
