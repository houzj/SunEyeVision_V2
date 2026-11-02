using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Metadata;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Tool;

public static partial class ToolRegistry
{



    public static IToolEditorFactory? CreateEditorFactory(IToolPlugin? tool)
    {
        if(tool is null)return null;
        var id=tool.GetType().GetCustomAttribute<ToolAttribute>()?.Id;
        ToolComponentRegistration? component;
        lock(_lock)component=id is null?null:_admission?.Runtime.Components.SingleOrDefault(c=>c.Metadata.Role==ToolComponentRole.Editor&&c.Metadata.ToolId==id);
        return component is null?tool as IToolEditorFactory:Activator.CreateInstance(component.EntryType) as IToolEditorFactory;
    }

    public static IReadOnlyList<ToolComponentMetadata> GetComponentMetadata()
    {
        lock(_lock)return _admission?.Runtime.Components.Select(x=>x.Metadata).ToArray()??Array.Empty<ToolComponentMetadata>();
    }

    internal static IReadOnlyList<SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryTool> GetRuntimeDirectoryTools()
    {
        lock (_lock) return (_admission?.Runtime.Components.Where(c=>c.Metadata.Role==ToolComponentRole.Runtime)??Array.Empty<ToolComponentRegistration>()).Select(item =>
        {
            var metadata = _metadataCache[item.Metadata.ToolId];
            var entry = item.EntryType;
            return new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryTool(item.Metadata.ToolId, metadata.DisplayName,
                metadata.Category, metadata.Order, metadata.Icon, metadata.Description, entry.FullName!, entry.Assembly.Location,
                true, SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryToolKind.Runtime);
        }).ToArray();
    }

private static bool PrepareComponents(IReadOnlyList<ToolComponentRegistration>? components,
        Dictionary<string,ToolComponentRegistration> runtimes,Dictionary<string,ToolComponentRegistration> editors,
        Dictionary<string,ToolMetadata> metadata,Dictionary<string,Type> sdkTypes)
    {
        if(components is null||components.Count==0)return false;
        _=ToolComponentCatalog.Prepare(components.Select(c=>c.Metadata));
        var changed=false;
        foreach(var component in components)
        {
            var target=component.Metadata.Role==ToolComponentRole.Runtime?runtimes:editors;
            if(target.TryGetValue(component.Metadata.ToolId,out var existing))
            {
                if(existing!=component)throw new InvalidOperationException("Tool component identity is already owned: "+component.Metadata.ToolId);
                continue;
            }
            ValidateComponentEntry(component);
            if(component.Metadata.Role==ToolComponentRole.Runtime)
            {
                if(sdkTypes.TryGetValue(component.Metadata.ToolId,out var sdk)
                    &&sdk.GetCustomAttribute<ToolAttribute>()?.RuntimeComponentId!=component.Metadata.ComponentId)
                    throw new InvalidOperationException("Runtime ToolId conflicts with an independent SDK producer.");
                var catalog=((IToolCatalogProvider)Activator.CreateInstance(component.EntryType)!).Catalog;
                metadata[component.Metadata.ToolId]=new ToolMetadata{Id=component.Metadata.ToolId,DisplayName=catalog.DisplayName,
                    Description=catalog.Description,Icon=catalog.IconKey,Category=catalog.Category,ToolType=component.EntryType,RuntimeCatalog=catalog};
            }
            target.Add(component.Metadata.ToolId,component);changed=true;
        }
        return changed;
    }

    private static void ValidateComponentEntry(ToolComponentRegistration component)
    {
        var type=component.EntryType;
        if(type.IsAbstract||type.GetConstructor(Type.EmptyTypes) is null)throw new InvalidOperationException("A concrete parameterless component entry is required.");
        if(type.Assembly.GetReferencedAssemblies().Any(a=>a.Name=="SunEyeVision.UI"))throw new InvalidOperationException("Tool components cannot reference the UI host.");
        if(component.Metadata.Role==ToolComponentRole.Editor)
        {
            if((!typeof(IToolEditorFactory).IsAssignableFrom(type)&&!typeof(IToolPresentationFactory).IsAssignableFrom(type))||typeof(IToolPlugin).IsAssignableFrom(type))
                throw new InvalidOperationException("Editor must be a factory, never an algorithm producer.");
        }
        else RuntimeComponentAdmission.Validate(component);
    }
}
