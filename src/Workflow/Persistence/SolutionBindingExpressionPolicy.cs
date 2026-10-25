using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Reads the actual ParamSettingContainer.Settings list, never an invented property-to-setting dictionary.</summary>
internal static class SolutionBindingExpressionPolicy
{
    public static void Validate(JsonNode? container, string path, bool deferLegacyEnvelopeToOwner = false)
    {
        if (container is null) return;
        if (container is not JsonObject settings) throw new JsonException("Expected parameter settings object.", path, null, null);
        foreach (var property in settings)
            if (property.Key is not ("NodeId" or "ToolName" or "Count" or "ParameterNames" or "Settings"))
                throw new JsonException("Unknown parameter settings member.", path + "." + property.Key, null, null);
        if (settings["Settings"] is null) return;
        if (settings["Settings"] is not JsonArray parameters) throw new JsonException("Expected parameter settings list.", path + ".Settings", null, null);
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i] as JsonObject ?? throw new JsonException("Null or invalid parameter setting.", path + ".Settings[" + i + "]", null, null);
            if (parameter["ParameterName"] is not JsonValue nameValue || !nameValue.TryGetValue<string>(out var name)
                || string.IsNullOrWhiteSpace(name) || !names.Add(name))
                throw new JsonException("Parameter names must be nonempty and unique.", path + ".Settings[" + i + "].ParameterName", null, null);
            foreach (var property in parameter)
                if (property.Key is not ("SourceWorkflowId" or "SourceFieldId" or "ReferenceExpression" or "Conversion"
                    or "ParameterName" or "BindingType" or "ConstantValue" or "SourceNodeId" or "SourceProperty"
                    or "SourceNodeName" or "SourceDisplayName" or "IsValid"))
                    throw new JsonException("Unknown parameter setting member.", path + ".Settings[" + i + "]." + property.Key, null, null);
            var binding = parameter["BindingType"];
            var dynamicBinding = false;
            if (binding is not null)
            {
                if (binding is JsonValue bindingValue && bindingValue.TryGetValue<int>(out var bindingCode) && bindingCode is >= 0 and <= 2)
                    dynamicBinding = bindingCode != 0;
                else if (binding is JsonValue bindingText && bindingText.TryGetValue<string>(out var bindingName)
                    && bindingName is "Constant" or "Binding" or "NodeOutput")
                    dynamicBinding = bindingName != "Constant";
                else throw new JsonException("Unknown binding kind.", path + ".Settings[" + i + "].BindingType", null, null);
            }
            if (parameter["ReferenceExpression"] is null)
            {
                if (dynamicBinding) throw new JsonException("Dynamic bindings require a typed reference or an explicit approved legacy mapping.", path + ".Settings[" + i + "].ReferenceExpression", null, null);
                continue;
            }
            var bindingPath = path + ".Settings[" + i + "].ReferenceExpression";
            try
            {
                if (deferLegacyEnvelopeToOwner)
                {
                    using var envelope = JsonDocument.Parse(parameter["ReferenceExpression"]!.GetValue<string>());
                    if (envelope.RootElement.TryGetProperty("ContractMajor", out var major)
                        && major.TryGetInt32(out var number) && number == 4)
                    {
                        // This is not acceptance or version conversion. Repository must run the candidate
                        // owner migration before it can return this document; that path performs strict legacy parsing.
                        continue;
                    }
                }
                // Unknown old identities cannot be promoted by changing the document's format number.
                _ = BindingReferenceSerializer.Deserialize(parameter["ReferenceExpression"]!.GetValue<string>());
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                throw new JsonException("Binding requires a current declaration or an explicit approved legacy mapping.", bindingPath, null, null, exception);
            }
        }
    }
}
