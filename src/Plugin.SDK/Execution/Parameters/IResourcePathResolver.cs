namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public enum ResourcePurpose { FileRead, FileWrite, DirectoryRead }
public sealed record ResourcePath(string Reference, string Address, bool Exists);
/// <summary>Session scoped I/O addressing. Never changes persisted parameter strings.</summary>
public interface IResourcePathResolver
{
    ResourcePath Resolve(string fieldName, string reference, ResourcePurpose purpose);
}
