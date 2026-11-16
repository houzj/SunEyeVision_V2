using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Runtime;
namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

/// <summary>Desktop projection bound to one release-admitted runtime directory.</summary>
public sealed class DesktopToolAdmission
{
    internal DesktopToolAdmission(ToolDirectoryCatalog directory,RuntimeDirectorySnapshot runtime,IReadOnlyDictionary<string,long>? loadSubstageMs=null)
    {Directory=directory;Runtime=runtime;LastLoadSubstageMs=loadSubstageMs??new Dictionary<string,long>();}
    public ToolDirectoryCatalog Directory { get; }
    public RuntimeDirectorySnapshot Runtime { get; }
    public IReadOnlyDictionary<string,long> LastLoadSubstageMs { get; }
    public bool IsCurrent => PluginManager.IsCurrent(this);
    public IReadOnlyList<SunEyeVision.Plugin.Contracts.ToolComponentMetadata> GetComponentMetadata()
    {
        if (!IsCurrent) throw new InvalidOperationException("桌面工具目录已退役。");
        return Array.AsReadOnly(Runtime.ComponentMetadata.ToArray());
    }
}
