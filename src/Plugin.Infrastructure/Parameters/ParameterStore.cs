using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Infrastructure.Parameters;

/// <summary>按节点持有唯一一份参数值；声明、校验与变更通知都在这里收口。</summary>
public sealed class ParameterStore : IParameterStore
{
    private sealed class NodeState
    {
        internal required IReadOnlyDictionary<string, Port> Ports { get; init; }
        internal required IReadOnlyDictionary<string, FieldPresentation> Fields { get; init; }
        internal Dictionary<string, ParameterSetting> Values { get; } = new(StringComparer.Ordinal);
    }

    private readonly Dictionary<Guid, NodeState> _nodes = new();
    private readonly object _gate = new();

    public event Action<Guid, string>? Changed;

    public void Declare(Guid nodeId, ToolSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var ports = schema.Inputs.ToDictionary(port => port.Id, StringComparer.Ordinal);
        var fields = (schema.Presentation?.Fields ?? Array.Empty<FieldPresentation>())
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        lock (_gate)
        {
            if (_nodes.TryGetValue(nodeId, out var existing))
            {
                var declared = existing.Ports.Keys.OrderBy(id => id, StringComparer.Ordinal);
                var incoming = ports.Keys.OrderBy(id => id, StringComparer.Ordinal);
                if (!declared.SequenceEqual(incoming, StringComparer.Ordinal))
                    throw new InvalidOperationException("Node is already declared with a different field set: " + nodeId);
                return;
            }
            _nodes.Add(nodeId, new NodeState { Ports = ports, Fields = fields });
        }
    }

    public ParameterSetting Read(Guid nodeId, string fieldId)
    {
        lock (_gate)
        {
            var node = Require(nodeId);
            RequireField(node, fieldId);
            return node.Values.TryGetValue(fieldId, out var value) ? value : new UnsetSetting();
        }
    }

    public void Write(Guid nodeId, string fieldId, ParameterSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        if (setting is UnsetSetting)
            throw new ArgumentException("Unset is an edit proposal, not a stored value: " + fieldId, nameof(setting));
        lock (_gate)
        {
            var node = Require(nodeId);
            RequireField(node, fieldId);
            var frozen = FieldSettings.Freeze(node.Ports[fieldId], setting);
            if (frozen is ConstantSetting constant && node.Fields.TryGetValue(fieldId, out var field))
                ValidateRange(field, constant);
            if (node.Values.TryGetValue(fieldId, out var current) && Equals(current, frozen)) return;
            node.Values[fieldId] = frozen;
        }
        Changed?.Invoke(nodeId, fieldId);
    }

    public FieldSettings Snapshot(Guid nodeId)
    {
        lock (_gate)
        {
            var node = Require(nodeId);
            return new FieldSettings(node.Ports.Values,
                new Dictionary<string, ParameterSetting>(node.Values, StringComparer.Ordinal));
        }
    }

    public void Restore(Guid nodeId, FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<string>? touched = null;
        lock (_gate)
        {
            var node = Require(nodeId);
            var incoming = settings.Capture();
            foreach (var id in incoming.Keys) RequireField(node, id);
            var changed = node.Values.Keys.Union(incoming.Keys, StringComparer.Ordinal)
                .Where(id => !node.Values.TryGetValue(id, out var before)
                    || !incoming.TryGetValue(id, out var after)
                    || !Equals(before, after))
                .ToArray();
            node.Values.Clear();
            foreach (var (id, value) in incoming) node.Values[id] = value;
            if (changed.Length != 0) touched = changed.ToList();
        }
        if (touched is null) return;
        foreach (var id in touched) Changed?.Invoke(nodeId, id);
    }

    private NodeState Require(Guid nodeId) => _nodes.TryGetValue(nodeId, out var node)
        ? node
        : throw new InvalidOperationException("Node parameters have not been declared: " + nodeId);

    private static void RequireField(NodeState node, string fieldId)
    {
        if (!node.Ports.ContainsKey(fieldId))
            throw new ArgumentException("Unknown node parameter: " + fieldId, nameof(fieldId));
    }

    private static void ValidateRange(FieldPresentation field, ConstantSetting constant)
    {
        if (field.Layout is not { } layout) return;
        if (constant.Value is not (long or int or double)) return;
        var number = Convert.ToDouble(constant.Value, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number))
            throw new ArgumentOutOfRangeException(nameof(constant), "Parameter value must be finite: " + field.FieldId);
        if (layout.Minimum is { } minimum && number < minimum)
            throw new ArgumentOutOfRangeException(nameof(constant),
                "Parameter value " + number.ToString(CultureInfo.InvariantCulture) + " is below the declared minimum "
                + minimum.ToString(CultureInfo.InvariantCulture) + ": " + field.FieldId);
        if (layout.Maximum is { } maximum && number > maximum)
            throw new ArgumentOutOfRangeException(nameof(constant),
                "Parameter value " + number.ToString(CultureInfo.InvariantCulture) + " is above the declared maximum "
                + maximum.ToString(CultureInfo.InvariantCulture) + ": " + field.FieldId);
    }
}
