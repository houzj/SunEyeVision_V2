using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Execution;

/// <summary>One invocation batch's pure variable slots; no document or image ownership is retained.</summary>
internal sealed class ExecutionVariableValues : IDisposable
{
    private readonly object _gate = new();
    private readonly IReadOnlyDictionary<string, BindingVariableSource> _sources;
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Diagnostic> _issues = new(StringComparer.Ordinal);
    private bool _disposed;
    internal ExecutionVariableValues(string solutionId, IEnumerable<BindingVariableSource> sources,
        IEnumerable<KeyValuePair<string, object?>> initialValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionId);
        ArgumentNullException.ThrowIfNull(sources); ArgumentNullException.ThrowIfNull(initialValues);
        SolutionId = solutionId;
        var captured = sources.ToArray();
        if (captured.Any(source => source.Reference.SolutionId != solutionId ||
            source.Reference.Selection is not BindingSelection.AllItems))
            throw new ArgumentException("变量声明不属于本方案或包含选择表达式。", nameof(sources));
        _sources = new ReadOnlyDictionary<string, BindingVariableSource>(captured.ToDictionary(
            source => source.Reference.VariableId, StringComparer.Ordinal));
        foreach (var (id, value) in initialValues)
        {
            if (!_sources.ContainsKey(id)) throw new ArgumentException("初始变量不属于本次冻结声明：" + id, nameof(initialValues));
            _values.Add(id, Copy(value));
        }
    }
    internal string SolutionId { get; }
    internal IReadOnlyList<BindingVariableSource> Sources => Array.AsReadOnly(_sources.Values.ToArray());

    internal IReadOnlyList<BindingVariableValue> Capture(RunIdentity consumer)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Array.AsReadOnly(_sources.Select(pair =>
            {
                var source = pair.Value;
                if (_issues.TryGetValue(pair.Key, out var issue)) return new BindingVariableValue(source, consumer, null, issue);
                if (!_values.TryGetValue(pair.Key, out var raw)) return new BindingVariableValue(source, consumer, null,
                    new Diagnostic("variable-unavailable", "变量在本批运行尚未产生有效值。", pair.Key));
                try
                {
                    var port = new Port(pair.Key, source.Reference.Type, Storage: source.Reference.Storage);
                    return new BindingVariableValue(source, consumer, Normalize(port, raw));
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
                { return new BindingVariableValue(source, consumer, null, new Diagnostic("variable-value", error.Message, pair.Key)); }
            }).ToArray());
        }
    }
    internal void Publish(IReadOnlyDictionary<string, (object? Value, Diagnostic? Issue)> values, CancellationToken cancellation)
    {
        var prepared = values.ToDictionary(pair => pair.Key,
            pair => (Value: Copy(pair.Value.Value), pair.Value.Issue), StringComparer.Ordinal);
        if (prepared.Keys.Any(id => !_sources.ContainsKey(id))) throw new ArgumentException("生产变量不属于本批声明。", nameof(values));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this); cancellation.ThrowIfCancellationRequested();
            foreach (var (id, entry) in prepared)
            {
                _values.Remove(id); _issues.Remove(id);
                if (entry.Issue is { } issue) _issues.Add(id, issue); else _values.Add(id, entry.Value);
            }
        }
    }
    private static bool IsScalar(Type type) => type == typeof(string) || type == typeof(bool) || type == typeof(sbyte)
        || type == typeof(byte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int)
        || type == typeof(uint) || type == typeof(long) || type == typeof(float) || type == typeof(double);
    private static object? Copy(object? value)
    {
        if (value is null || IsScalar(value.GetType())) return value;
        if (value is Array array && array.GetType().IsSZArray && IsScalar(array.GetType().GetElementType()!)) return array.Clone();
        throw new ArgumentException("运行变量槽仅接纳纯标量及一维纯标量数组。", nameof(value));
    }
    private static object? Normalize(Port port, object? value)
    {
        if (value is null || value is SequenceValue) return ValueRules.Freeze(port, value);
        if (port.Type.Shape != ValueShape.Sequence && value is Array wrapper)
        {
            if (wrapper.Rank != 1 || wrapper.Length != 1) throw new ArgumentException("标量传输数组必须恰有一项。");
            return Normalize(port, wrapper.GetValue(0));
        }
        if (port.Type.Shape == ValueShape.Sequence)
        {
            if (value is not Array { Rank: 1 } array) throw new ArgumentException("序列声明要求一维数组。");
            var element = new Port(port.Id, port.Type.Element!, Storage: port.Storage);
            var items = array.Cast<object?>().Select(item => Normalize(element, item)).ToArray();
            return ValueRules.Freeze(port, port.Type.Element == TypeRef.ScalarOf(ScalarKind.Float64)
                ? items.Cast<double>().ToArray() : new SequenceValue(port.Type, items));
        }
        if (port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64)
        {
            var storage = value switch
            {
                sbyte => NumericStorage.Int8, byte => NumericStorage.UInt8, short => NumericStorage.Int16,
                ushort => NumericStorage.UInt16, int => NumericStorage.Int32, uint => NumericStorage.UInt32,
                long => NumericStorage.Int64, float => NumericStorage.Float32, double => NumericStorage.Float64,
                _ => (NumericStorage?)null
            };
            if (storage is null || storage != (port.Storage ?? NumericStorageRules.Default(port.Type.Scalar.Value))
                || NumericStorageRules.IsInteger(storage.Value) != (port.Type.Scalar == ScalarKind.Int64))
                throw new ArgumentException("运行数值的物理表示与正式声明不一致。");
            value = port.Type.Scalar == ScalarKind.Int64
                ? (object)Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture)
                : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        return ValueRules.Freeze(port, value);
    }
    public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _values.Clear(); _issues.Clear(); } }
}
