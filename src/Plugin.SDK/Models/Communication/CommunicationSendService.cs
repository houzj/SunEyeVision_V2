using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

public sealed record CommunicationSendTarget(string Kind, string Id, string DeviceId, string Name,
    string Protocol, string DataType, IReadOnlyList<CommunicationSendParameter>? Parameters = null, string DeviceName = "");
public sealed record CommunicationSendParameter(Guid FieldId, string Name, string DataType);
public sealed record CommunicationSendInput(Guid FieldId, string TargetId, object Value, string DataType);

/// <summary>Explicit wire types; normalized workflow scalar types stay unchanged.</summary>
public static class CommunicationSendTypes
{
    public static IReadOnlyList<string> Choices { get; } = Array.AsReadOnly(new[] { "String", "Integer", "Int16", "Int32", "Int64", "UInt16", "UInt32", "Float", "Double", "Boolean", "Binary" });
    public static string Canonical(string type) => type.ToLowerInvariant() switch
    {
        "string" => "String", "integer" => "Integer", "int" or "int32" => "Int32", "int16" => "Int16", "int64" => "Int64",
        "uint16" => "UInt16", "uint32" => "UInt32", "float" or "float32" => "Float", "double" => "Double",
        "bool" or "boolean" => "Boolean", "binary" => "Binary", _ => throw new FormatException($"不支持发送类型 {type}。")
    };
    public static Type BindingType(string type) => Canonical(type) switch
    { "String" => typeof(string), "Boolean" => typeof(bool), "Binary" => typeof(BinaryPayload), "Float" or "Double" => typeof(double), _ => typeof(long) };
    public static string WorkflowType(string type) => BindingType(type) == typeof(long) ? "Integer"
        : BindingType(type) == typeof(double) ? "Double" : Canonical(type);
    public static object ParseLiteral(string text, string type)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        object value = Canonical(type) switch
        {
            "String" => text,
            "Integer" or "Int16" or "Int32" or "Int64" or "UInt16" or "UInt32" when long.TryParse(text, System.Globalization.NumberStyles.Integer, culture, out var n) => n,
            "Float" or "Double" when double.TryParse(text, System.Globalization.NumberStyles.Float, culture, out var n) && double.IsFinite(n) => n,
            "Boolean" when bool.TryParse(text, out var b) => b,
            "Binary" => new BinaryPayload(ProtocolValueCodec.ParseHex(text)),
            _ => throw new FormatException("常量与发送声明类型不兼容。")
        };
        return Validate(value, type);
    }
    public static object Validate(object value, string type)
    {
        if (value is double d && !double.IsFinite(d) || value is float f && !float.IsFinite(f))
            throw new FormatException($"输入值超出发送类型 {type} 的有效范围。");
        var name = Canonical(type); var normalized = CommunicationReceiveService.Normalize(value);
        if (!BindingType(type).IsInstanceOfType(normalized)) throw new FormatException($"发送类型 {name} 与输入类型 {normalized.GetType().Name} 不一致。");
        _ = name switch
        {
            "Int16" => checked((short)(long)normalized), "Int32" => checked((int)(long)normalized),
            "UInt16" => checked((ushort)(long)normalized), "UInt32" => checked((uint)(long)normalized), _ => 0L
        };
        if (normalized is double number && (!double.IsFinite(number) || name == "Float" && Math.Abs(number) > float.MaxValue))
            throw new FormatException($"输入值超出发送类型 {name} 的有效范围。");
        return normalized;
    }
    public static void RequireTarget(string sending, string target, string name)
    {
        if (Canonical(sending) != Canonical(target)) throw new FormatException($"目标 {name}：发送类型 {sending}，目标要求 {target}，类型不一致。");
    }
}
public sealed record CommunicationSendOptions(bool Hex = false, string Separator = "", string Terminator = "");
public sealed record CommunicationSendRequest(string Kind, string DeviceId, string EventId,
    IReadOnlyList<CommunicationSendInput> Inputs, CommunicationSendOptions Options, string? NodeId = null, string? WorkflowId = null);
public sealed record SendVariablePublication(string VariableId, string OutputKey, string DataType);

/// <summary>A validated single-use submission. Preparation performs no writes.</summary>
public sealed class PreparedCommunicationSend(string name, object value, Func<CancellationToken, Task<int>> submit)
{
    private int _submitted;
    public string Name { get; } = name;
    public object Value { get; } = value;
    public Task<int> SubmitAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _submitted, 1) != 0) throw new InvalidOperationException("发送计划不能重复提交。");
        return submit(token);
    }
}

public interface ICommunicationSendService
{
    IReadOnlyList<CommunicationSendTarget> GetTargets();
    Task<PreparedCommunicationSend> PrepareAsync(CommunicationSendRequest request, CancellationToken token = default);
    // Explicit design-time binding command; never called by execution or result display.
    void ConfigureVariablePublications(string nodeId, IReadOnlyList<SendVariablePublication> mappings);
    void ConfigureVariablePublications(string workflowId, string nodeId, IReadOnlyList<SendVariablePublication> mappings)
        => ConfigureVariablePublications(nodeId, mappings);
}

/// <summary>Nested expression bindings declared by tools with editable row collections.</summary>
public interface ICommunicationBindingInputProvider
{
    IReadOnlyList<string> GetInputExpressions();
}
