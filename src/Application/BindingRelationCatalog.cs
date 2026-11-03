using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Enumerates binding identities from the one admitted configuration and display expressions.</summary>
internal static class BindingRelationCatalog
{
    internal static IEnumerable<VariableReference> IndexVariables(BindingSelection selection)
    {
        if (selection is BindingSelection.AtVariable variable) yield return variable.Reference;
        if (selection is BindingSelection.AtField field)
            foreach (var index in IndexVariables(field.Reference.Selection)) yield return index;
    }
    internal static IEnumerable<(string FieldId, VariableReference Reference)> VariableReferences(
        AdmittedToolConfiguration admitted, bool executionOnly = false)
    {
        foreach (var (fieldId, setting) in admitted.Configuration.Fields.Capture())
        {
            if (executionOnly && !admitted.IsExecutionFieldActive(fieldId)) continue;
            if (setting is VariableSetting variable) yield return (fieldId, variable.Reference);
            var selection = setting switch { VariableSetting value => value.Reference.Selection, ReferenceSetting value => value.Reference.Selection, _ => null };
            if (selection is not null)
                foreach (var index in IndexVariables(selection)) yield return (fieldId, index);
            if (!executionOnly && setting is DisplayExpressionSetting expression)
                foreach (var slot in expression.Slots)
                    if (slot.Reference is { } source)
                        foreach (var index in IndexVariables(source.Selection)) yield return (fieldId, index);
        }
    }
    internal static IReadOnlyDictionary<string, ReferenceSetting> References(AdmittedToolConfiguration admitted, bool executionOnly = false)
    {
        var result = new Dictionary<string, ReferenceSetting>(StringComparer.Ordinal);
        foreach (var (id, setting) in admitted.Configuration.Fields.Capture())
        {
            if (setting is ReferenceSetting reference && (!executionOnly || admitted.IsExecutionFieldActive(id))) result.Add(id, reference);
            if (setting is DisplayExpressionSetting expression && !executionOnly)
                for (var index = 0; index < expression.Slots.Count; index++)
                    if (expression.Slots[index].Reference is { } source) result.Add(SlotKey(id, index), new ReferenceSetting(source));
        }
        return result;
    }
    internal static string SlotKey(string field, int index) => "display-slot:" + field.Length + ":" + field + ":" + index;
}
