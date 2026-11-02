namespace SunEyeVision.Plugin.Contracts;

/// <summary>Tool-declared parameter keys, separate from stable field IDs and translated labels.
/// Every referenced settings field needs an explicit key for binding record ordering.</summary>
public interface IToolParameterBindingNames
{
    IReadOnlyDictionary<string,string> ParameterBindingNames {get;}
}
