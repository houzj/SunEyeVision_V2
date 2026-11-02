using SunEyeVision.Plugin.Runtime;
namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

/// <summary>Desktop projection bound to one release-admitted runtime directory.</summary>
public sealed class DesktopToolAdmission
{
    internal DesktopToolAdmission(ToolDirectoryCatalog directory,RuntimeDirectorySnapshot runtime)
    {Directory=directory;Runtime=runtime;}
    public ToolDirectoryCatalog Directory { get; }
    public RuntimeDirectorySnapshot Runtime { get; }
}
