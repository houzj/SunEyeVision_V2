using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Plugin.Runtime;
public sealed record ToolComponentRegistration(ToolComponentMetadata Metadata, Type EntryType);
