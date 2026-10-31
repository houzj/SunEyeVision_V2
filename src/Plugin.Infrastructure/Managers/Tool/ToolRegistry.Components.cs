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
    private static Dictionary<string,ToolComponentRegistration> _runtimeComponents=new(StringComparer.Ordinal);
    private static Dictionary<string,ToolComponentRegistration> _editorComponents=new(StringComparer.Ordinal);

    public static IToolEditorFactory? CreateEditorFactory(IToolPlugin? tool)
    {
        if(tool is null)return null;
        var id=tool.GetType().GetCustomAttribute<ToolAttribute>()?.Id;
        ToolComponentRegistration? component;
        lock(_lock)component=id is not null&&_editorComponents.TryGetValue(id,out var value)?value:null;
        return component is null?tool as IToolEditorFactory:Activator.CreateInstance(component.EntryType) as IToolEditorFactory;
    }

    public static IToolResultView? CreateCurrentResultView(string toolId)
    {
        ToolComponentRegistration? runtime, editor;
        lock(_lock)
        {
            _runtimeComponents.TryGetValue(toolId,out runtime);
            _editorComponents.TryGetValue(toolId,out editor);
        }
        if(runtime is null||Activator.CreateInstance(runtime.EntryType) is not IToolResultPresentationProvider provider)return null;
        var schema=provider.ResultSchema;var projection=provider.ResultProjection;
        var errors=projection.Validate(schema);
        if(errors.Count!=0)throw new InvalidOperationException("Invalid runtime result presentation: "+string.Join("; ",errors.Select(e=>e.Message)));
        // The module output tree has its own schema-driven owner. A property-list
        // declaration must not be mounted as the separate current-result table.
        if(projection.Kind is ResultProjectionKind.None or ResultProjectionKind.PropertyList)return null;
        if(editor is not null)
        {
            var factory=Activator.CreateInstance(editor.EntryType) as IToolPresentationFactory
                ??throw new InvalidOperationException("A runtime result declaration requires a neutral WPF presentation factory.");
            return factory.CreateResultView(schema,projection)??throw new InvalidOperationException("Result view factory returned null.");
        }
        return projection.Kind switch
        {
            ResultProjectionKind.StandardTable=>new DeclaredResultTable(schema,projection),
            _=>throw new NotSupportedException("No result renderer is registered for: "+projection.RendererKey)
        };
    }

    public static IReadOnlyList<ToolComponentMetadata> GetComponentMetadata()
    {
        lock(_lock)return _runtimeComponents.Values.Concat(_editorComponents.Values).Select(x=>x.Metadata).ToArray();
    }

    public static IToolDefinition CreateRuntime(string toolId)
    {
        ToolComponentRegistration component;
        lock(_lock)component=_runtimeComponents.TryGetValue(toolId,out var found)?found:
            throw new InvalidOperationException("Required runtime is not registered: "+toolId);
        return (IToolDefinition)Activator.CreateInstance(component.EntryType)!;
    }

    internal static IReadOnlyList<SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryTool> GetRuntimeDirectoryTools()
    {
        lock (_lock) return _runtimeComponents.Select(item =>
        {
            var metadata = _metadataCache[item.Key];
            var entry = item.Value.EntryType;
            return new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryTool(item.Key, metadata.DisplayName,
                metadata.Category, metadata.Order, metadata.Icon, metadata.Description, entry.FullName!, entry.Assembly.Location,
                true, SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryToolKind.Runtime);
        }).ToArray();
    }

    public static bool HasRuntime(string toolId)
    {lock(_lock)return _runtimeComponents.ContainsKey(toolId);}

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

    public static IToolSettingsView CreateSettingsView(string toolId,FieldSettings fields,ToolSettingsViewOptions? options=null)
    {
        var draft=new ToolSettingsEditSession(CreateRuntime(toolId),fields);
        ToolComponentRegistration? editor;
        lock(_lock)_editorComponents.TryGetValue(toolId,out editor);
        options??=new();
        if(editor is null)return new DeclaredSettingsView(draft,options);
        var factory=Activator.CreateInstance(editor.EntryType) as IToolPresentationFactory
            ??throw new InvalidOperationException("Runtime settings require a neutral WPF presentation factory.");
        return factory.CreateSettingsView(draft,options)??throw new InvalidOperationException("Settings view factory returned null.");
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
        else
        {
            if(type.Assembly.GetReferencedAssemblies().Any(a=>a.Name is "SunEyeVision.Plugin.SDK" or "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Xaml"))
                throw new InvalidOperationException("Neutral runtime component cannot depend on SDK/WPF.");
            if(!typeof(IToolDefinition).IsAssignableFrom(type))
                throw new InvalidOperationException("Runtime must implement the neutral tool definition.");
            var definition=(IToolDefinition)Activator.CreateInstance(type)!;
            if(definition is not IToolCatalogProvider catalog||catalog.Catalog is null)
                throw new InvalidOperationException("Runtime must supply its own neutral catalog information.");
            if(definition.ToolId!=component.Metadata.ToolId)
                throw new InvalidOperationException("Runtime entry ToolId differs from its component declaration.");
            var settings=definition.CreateSettings();
            if(settings.GetType()!=definition.SettingsType||!typeof(IToolOutput).IsAssignableFrom(definition.OutputType))
                throw new InvalidOperationException("Runtime settings/output identities differ from their declaration.");
            if(definition.Validate(settings).Count!=0)throw new InvalidOperationException("Runtime default settings are invalid.");
            _=definition.Describe();
            if(definition is IToolSettingsFields)
                _=new ToolSettingsEditSession(definition,new FieldSettings(((IToolSettingsFields)definition).SettingsSchema.Inputs,((IToolSettingsFields)definition).ReadSettings(settings).CopyValues().ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal))).CaptureFields();
        }
    }
}
