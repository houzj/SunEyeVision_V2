using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Explicit CLR slots on a private node draft, using the existing semantic value rules.</summary>
public sealed class EditorDraftAdapter
{
    private readonly ToolParameters _parameters;
    private readonly (Port Port, PropertyInfo Property)[] _slots;
    public EditorDraftAdapter(ToolParameters parameters, IReadOnlyDictionary<string, Type> slots)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _slots = slots.Select(slot =>
        {
            var property = parameters.GetType().GetProperty(slot.Key, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || property.PropertyType != slot.Value || !property.CanRead || !property.CanWrite)
                throw new ArgumentException($"参数槽 {slot.Key} 与明确的 CLR 声明不一致。");
            return (BindingClrAdapter.DescribePort(slot.Key, slot.Value), property);
        }).ToArray();
    }
    public void Validate()
    {
        var values = _slots.ToDictionary(slot => slot.Port.Id,
            slot => (SunEyeVision.Plugin.Contracts.ParameterSetting)new ConstantSetting(BindingClrAdapter.NormalizeValue(slot.Port, slot.Property.GetValue(_parameters))), StringComparer.Ordinal);
        var session = new ParameterEditSession(_slots.Select(slot => slot.Port), values);
        if (!session.TryCommit()) throw new InvalidOperationException("参数草稿不符合声明。");
    }
    // InputText is intentionally absent: its existing binding descriptor accepts String/Binary.
    // Outputs are validated by the existing output contract and binding graph, never a fabricated union.
    public static EditorDraftAdapter ForProtocolParse(ToolParameters parameters)
    {
        if (parameters.GetType().FullName != "SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters")
            throw new ArgumentException("此适配器仅接受 ProtocolParse 的明确参数声明。");
        return new(parameters, new Dictionary<string, Type>(StringComparer.Ordinal)
        { ["Mode"] = typeof(string), ["Separator"] = typeof(string), ["InputFormat"] = typeof(string),
          ["StringEncoding"] = typeof(string), ["AsciiData"] = typeof(bool) });
    }
}
