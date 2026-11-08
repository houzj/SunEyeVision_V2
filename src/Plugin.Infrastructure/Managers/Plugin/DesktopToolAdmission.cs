using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Runtime;
namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

/// <summary>Desktop projection bound to one release-admitted runtime directory.</summary>
public sealed class DesktopToolAdmission
{
    internal DesktopToolAdmission(ToolDirectoryCatalog directory,RuntimeDirectorySnapshot runtime)
    {Directory=directory;Runtime=runtime;}
    public ToolDirectoryCatalog Directory { get; }
    public RuntimeDirectorySnapshot Runtime { get; }
    public bool IsCurrent => PluginManager.IsCurrent(this);
    public IReadOnlyList<SunEyeVision.Plugin.Contracts.ToolComponentMetadata> GetComponentMetadata()
    {
        if (!IsCurrent) throw new InvalidOperationException("桌面工具目录已退役。");
        return Array.AsReadOnly(Runtime.ComponentMetadata.ToArray());
    }
}
