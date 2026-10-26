using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

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

    public static IToolResultView? CreateResultView(string toolId)
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
        if(editor is not null)
        {
            var factory=Activator.CreateInstance(editor.EntryType) as IToolPresentationFactory
                ??throw new InvalidOperationException("A runtime result declaration requires a neutral WPF presentation factory.");
            return factory.CreateResultView(schema,projection)??throw new InvalidOperationException("Result view factory returned null.");
        }
        return new DeclaredResultTable(schema,projection);
    }

    public static IReadOnlyList<ToolComponentMetadata> GetComponentMetadata()
    {
        lock(_lock)return _runtimeComponents.Values.Concat(_editorComponents.Values).Select(x=>x.Metadata).ToArray();
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
            if(!type.GetInterfaces().Any(i=>i.IsGenericType&&i.GetGenericTypeDefinition()==typeof(IToolDefinition<,>)))
                throw new InvalidOperationException("Runtime must implement the neutral tool definition.");
            var definition=Activator.CreateInstance(type)!;
            if(type.GetProperty("ToolId")?.GetValue(definition) is not string id||id!=component.Metadata.ToolId)
                throw new InvalidOperationException("Runtime entry ToolId differs from its component declaration.");
        }
    }
}
