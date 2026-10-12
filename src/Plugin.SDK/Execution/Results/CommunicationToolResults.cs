using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Communication;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

/// <summary>One execution's schema, atomic business outputs and non-bindable analysis.</summary>
public class CommunicationToolResults : ToolResults
{
    private readonly CommunicationOutputField[] _fields;
    private Dictionary<Guid, object?> _values = new();
    private CommunicationAnalysisRow[] _analysis = Array.Empty<CommunicationAnalysisRow>();

    public CommunicationToolResults() : this(Array.Empty<CommunicationOutputField>()) { }

    public CommunicationToolResults(IEnumerable<CommunicationOutputField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = fields.ToArray();
        if (_fields.Select(f => f.FieldId).Distinct().Count() != _fields.Length ||
            _fields.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _fields.Length)
            throw new ArgumentException("输出字段身份或名称重复。", nameof(fields));
        foreach (var field in _fields) field.CreateContract();
    }

    [IgnoreBind]
    public IReadOnlyList<CommunicationAnalysisRow> Analysis => Array.AsReadOnly(_analysis);

    public void SetAnalysis(IEnumerable<CommunicationAnalysisRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _analysis = rows.ToArray();
    }

    /// <summary>Validation failures clear old values and never publish a partial dictionary.</summary>
    public void PublishOutputs(IReadOnlyDictionary<Guid, object?> values)
    {
        _values = new();
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != _fields.Length)
            throw new ArgumentException("输出必须恰好覆盖配置字段。", nameof(values));
        var prepared = new Dictionary<Guid, object?>();
        foreach (var field in _fields)
        {
            if (!values.TryGetValue(field.FieldId, out var value) || value == null)
                throw new ArgumentException($"输出字段 {field.Name} 缺少有效值。", nameof(values));
            var type = SdkTypeVocabulary.ToSystemType(field.VariableType)!;
            if (!type.IsInstanceOfType(value) &&
                !(value is Array array && array.GetType().GetElementType() == type))
                throw new ArgumentException($"输出字段 {field.Name} 需要 {field.VariableType}，实际 {value.GetType().Name}。", nameof(values));
            if (value is double scalar && !double.IsFinite(scalar) ||
                value is double[] numbers && numbers.Any(n => !double.IsFinite(n)))
                throw new ArgumentException($"输出字段 {field.Name} 包含非有限数值。", nameof(values));
            prepared[field.FieldId] = CopyValue(value);
        }
        _values = prepared;
    }

    public void ClearOutputs() => _values = new();

    public override IReadOnlyList<OutputVariable> GetOutputVariables()
    {
        var result = base.GetOutputVariables().ToList();
        foreach (var field in _fields)
        {
            _values.TryGetValue(field.FieldId, out var value);
            result.Add(field.CreateContract(IsSuccess ? CopyValue(value) : null));
        }
        return result;
    }

    private static object? CopyValue(object? value) => value is Array array ? array.Clone() : value;
}

public sealed record CommunicationAnalysisRow(
    Guid? FieldId, string Name, string Source, string Rule, string Detail, bool IsValid);
