namespace SunEyeVision.Plugin.Contracts;

/// <summary>Read/write intent of a declared external resource reference.</summary>
public enum ResourcePurpose { FileRead, FileWrite, DirectoryRead }
public sealed record ResourcePath(string Reference, string Address, bool Exists);

/// <summary>Resolves references against one frozen document base; no UI or legacy SDK dependency.</summary>
public interface IResourcePathResolver
{
    ResourcePath Resolve(string fieldName, string reference, ResourcePurpose purpose);
}
