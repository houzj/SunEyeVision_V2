using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Validate a runtime component before it becomes visible to any host.</summary>
public static class RuntimeComponentAdmission
{
    public static void Validate(ToolComponentRegistration component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var type=component.EntryType;
        if(component.Metadata.Role!=ToolComponentRole.Runtime || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
            throw new InvalidOperationException("A concrete parameterless runtime entry is required.");
        if(type.Assembly.GetReferencedAssemblies().Any(a=>a.Name is "SunEyeVision.UI" or "SunEyeVision.Plugin.SDK" or "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Xaml"))
            throw new InvalidOperationException("Neutral runtime component cannot depend on UI/SDK/WPF.");
        if(Activator.CreateInstance(type) is not IToolDefinition definition)
            throw new InvalidOperationException("Runtime must implement the neutral tool definition.");
        if(definition is not IToolCatalogProvider catalog || catalog.Catalog is null)
            throw new InvalidOperationException("Runtime must supply its own neutral catalog information.");
        if(definition.ToolId!=component.Metadata.ToolId)throw new InvalidOperationException("Runtime entry ToolId differs from its component declaration.");
        var settings=definition.CreateSettings();
        if(settings.GetType()!=definition.SettingsType || !typeof(IToolOutput).IsAssignableFrom(definition.OutputType))
            throw new InvalidOperationException("Runtime settings/output identities differ from their declaration.");
        if(definition.Validate(settings).Count!=0)throw new InvalidOperationException("Runtime default settings are invalid.");
        _=definition.Describe();
        _=ToolHostDeclarationRules.ValidateAndFingerprint(definition);
        if(definition is IToolSettingsFields settingsFields)
        {
            var defaults=settingsFields.ReadSettings(settings).CopyValues().ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal);
            if(definition is IToolResultDisplayProvider display)
                foreach(var pair in display.CreateDisplaySettings().CopyValues())defaults.Add(pair.Key,new ConstantSetting(pair.Value));
            _=new ToolSettingsEditSession(definition,new FieldSettings(ResultDisplayFields.EditorSchema(definition).Inputs,defaults)).CaptureValidatedFields();
        }
    }
}
