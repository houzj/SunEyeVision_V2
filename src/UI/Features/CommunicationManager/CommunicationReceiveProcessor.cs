using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Opc.Ua;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.SDK.Models.Communication;

namespace SunEyeVision.UI.Features.CommunicationManager;

internal sealed record CommunicationReceiveOutcome(bool IsSuccess, string? FailureReason);

/// <summary>
/// Processes one complete received message. Parsing is prepared before any field is updated,
/// so an invalid field cannot leave a partially accepted message in the UI.
/// </summary>
internal static class CommunicationReceiveProcessor
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static CommunicationReceiveOutcome Process(
        CommunicationReceiveEventViewModel definition, byte[] data, DateTimeOffset timestamp)
    {
        if (definition.ProcessingMethod == CommunicationEventProcessingMethod.Script)
            return Reject(definition, data, timestamp, "脚本接收事件尚未接入运行时");
        if (definition.ProcessingMethod == CommunicationEventProcessingMethod.ByteMatch)
            return ProcessByteRules(definition, data, timestamp);

        var types = definition.IsProtocolCompare
            ? definition.CompareInputs.Select(field => field.DataType).ToArray()
            : definition.Outputs.Select(field => field.DataType).ToArray();
        if (types.Length == 0)
            return Reject(definition, data, timestamp, "未配置解析字段");

        if (!TryDecode(definition, data, types, out var values, out var reason))
            return Reject(definition, data, timestamp, reason);

        return ProcessDecodedFields(definition, data, timestamp, types, values, recordHistory: true);
    }

    private static CommunicationReceiveOutcome ProcessDecodedFields(
        CommunicationReceiveEventViewModel definition, byte[] data, DateTimeOffset timestamp,
        IReadOnlyList<string> types, IReadOnlyList<string> values, bool recordHistory, bool allowTextConversion = true)
    {
        var parsed = new (object Value, string Display)[types.Count];
        for (var index = 0; index < types.Count; index++)
        {
            // Native OPC UA String fields retain strict type checking; raw text packets may convert.
            if ((!allowTextConversion || types[index].ToLowerInvariant() is "bool" or "boolean")
                && !string.Equals(types[index], "string", StringComparison.OrdinalIgnoreCase))
                return Reject(definition, data, timestamp,
                    $"第 {index + 1} 个字段类型不匹配：文本数据为 string，配置为 {types[index]}", recordHistory);
            if (!TryNormalize(values[index], types[index], out parsed[index]))
                return Reject(definition, data, timestamp,
                    $"第 {index + 1} 个字段无法转换为 {types[index]}：{values[index]}", recordHistory);
        }

        var statuses = new string[types.Count];
        for (var index = 0; index < types.Count; index++)
            statuses[index] = EvaluateSample(definition,
                definition.IsProtocolCompare ? definition.CompareInputs[index].FieldId : definition.Outputs[index].FieldId,
                definition.IsProtocolCompare ? definition.CompareInputs[index].CompareRule : definition.Outputs[index].CompareRule,
                parsed[index].Value, recordHistory, definition.IsProtocolCompare
                    ? IsCompareMatch(definition.CompareInputs[index], parsed[index])
                    : IsOutputMatch(definition.Outputs[index], parsed[index]))
                ? "OK" : "NG";

        if (definition.IsProtocolCompare)
        {
            for (var index = 0; index < parsed.Length; index++)
            {
                var field = definition.CompareInputs[index];
                field.Value = parsed[index].Value;
                field.Result = parsed[index].Display;
                field.Status = statuses[index];
            }
        }
        else
        {
            for (var index = 0; index < parsed.Length; index++)
            {
                var field = definition.Outputs[index];
                field.Value = parsed[index].Value;
                field.Result = parsed[index].Display;
                field.Status = statuses[index];
            }
        }

        var rejectedFields = definition.IsProtocolCompare
            ? definition.CompareInputs.Where((_, index) => statuses[index] == "NG").Select(field => field.Name).ToArray()
            : definition.Outputs.Where((_, index) => statuses[index] == "NG").Select(field => field.Name).ToArray();
        var success = rejectedFields.Length == 0;
        var failureReason = success ? null : $"比较未通过：{string.Join("、", rejectedFields)}";
        if (success)
            ApplyAssemblyResults(definition);
        else
            ClearAssemblyResults(definition);

        if (recordHistory)
            definition.AddParseHistory(timestamp, data, string.Join(",", parsed.Select(value => value.Display)),
                success, failureReason);
        return new CommunicationReceiveOutcome(success, failureReason);
    }

    /// <summary>Decode FC03 data registers without framing.</summary>
    public static CommunicationReceiveOutcome ProcessModbus(CommunicationReceiveEventViewModel definition,
        ushort[] registers, DateTimeOffset timestamp)
    {
        var data = new byte[registers.Length * 2];
        for (var i = 0; i < registers.Length; i++)
        { data[i * 2] = (byte)(registers[i] >> 8); data[i * 2 + 1] = (byte)registers[i]; }
        if (definition.ProcessingMethod != CommunicationEventProcessingMethod.ByteMatch)
            return Reject(definition, data, timestamp, "Modbus 地址块目前支持字节匹配－协议比对");
        if (definition.AsciiData)
            return Reject(definition, data, timestamp, "Modbus 寄存器已是原始字节，不适用事件 ASCII 输入转换");
        return ProcessByteRules(definition, data, timestamp);
    }

    private static CommunicationReceiveOutcome ProcessByteRules(CommunicationReceiveEventViewModel definition,
        byte[] data, DateTimeOffset timestamp)
    {
        if (!definition.IsProtocolCompare) return Reject(definition, data, timestamp, "字节匹配目前支持协议比对事件");
        if (definition.ByteLengthCompare && data.Length != definition.ByteLength)
            return Reject(definition, data, timestamp, $"字节长度不符：期望 {definition.ByteLength}，实际 {data.Length}");
        if (definition.CompareInputs.Count == 0) return Reject(definition, data, timestamp, "未配置字节比对规则");
        var parsed = new (object Value, string Display)[definition.CompareInputs.Count];
        try
        {
            // Event ASCII validates character bytes; unlike the module it does not parse numeric characters.
            if (definition.AsciiData) ProtocolValueCodec.EncodingFor("ASCII").GetString(data);
            for (var i = 0; i < parsed.Length; i++)
            {
                var f = definition.CompareInputs[i];
                var value = ProtocolValueCodec.Decode(data, f.ByteStart, f.ByteEnd, f.DataType, f.ByteOrder);
                parsed[i] = (value, value is BinaryPayload b ? b.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
            }
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException or OverflowException)
        { return Reject(definition, data, timestamp, ex.Message); }
        var matched = new bool[parsed.Length];
        for (var i = 0; i < parsed.Length; i++)
        {
            var f = definition.CompareInputs[i];
            matched[i] = IsByteRuleMatch(f, parsed[i], f.Value);
            f.Value = parsed[i].Value; f.Result = parsed[i].Display; f.Status = matched[i] ? "OK" : "NG";
        }
        var success = definition.ByteMatchLogic == "或" ? matched.Any(v => v) : matched.All(v => v);
        var reason = success ? null : "字节规则比较未通过";
        if (success) ApplyAssemblyResults(definition); else ClearAssemblyResults(definition);
        var details = string.Join(", ", definition.CompareInputs.Select((field, index) =>
            $"{field.ByteStart}-{field.ByteEnd}[{Convert.ToHexString(ProtocolValueCodec.Slice(data, field.ByteStart, field.ByteEnd))}]={parsed[index].Display}"));
        definition.AddParseHistory(timestamp, data, details, success, reason);
        return new(success, reason);
    }

    private static bool IsByteRuleMatch(CommunicationCompareInputViewModel field, (object Value, string Display) actual, object? previous)
    {
        if (actual.Value is int n) return IsModbusIntMatch(field, n, previous is int old ? old : null);
        if (actual.Value is BinaryPayload b)
        {
            if (field.CompareRule == "不比较") return true;
            if (field.CompareRule is not ("等于" or "不等于")) return false;
            try
            {
                var equal = b.ToArray().SequenceEqual(ProtocolValueCodec.ParseHex(field.CompareValue));
                return field.CompareRule == "等于" ? equal : !equal;
            }
            catch (FormatException) { return false; }
        }
        if (actual.Value is float f && field.CompareRule is "等于" or "不等于")
        {
            if (!float.TryParse(field.CompareValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var expected) || !float.IsFinite(expected)) return false;
            return field.CompareRule == "等于" ? f == expected : f != expected;
        }
        return IsCompareMatch(field, actual);
    }

    /// <summary>Consumes one native scalar value from a bound OPC UA data point.</summary>
    public static CommunicationReceiveOutcome ProcessOpcUa(
        CommunicationReceiveEventViewModel definition, OpcUaPointValueEventArgs update, bool recordHistory = true)
    {
        var raw = Convert.ToString(update.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        var data = Encoding.UTF8.GetBytes(raw);
        if (definition.ProcessingMethod != CommunicationEventProcessingMethod.Text)
            return Reject(definition, data, update.Timestamp, "OPC UA 数据点仅支持原生标量解析或比对", recordHistory);
        if (!string.IsNullOrEmpty(update.ErrorMessage) || !StatusCode.IsGood(update.StatusCode) ||
            !update.IsSupportedScalar || update.Value == null)
            return Reject(definition, data, update.Timestamp,
                $"OPC UA 数据点质量或类型无效：{update.ErrorMessage ?? update.StatusCode.ToString()}", recordHistory);

        if (update.BuiltInType == BuiltInType.String && update.Value is string text)
        {
            if (definition.IsBinaryMode)
                return Reject(definition, data, update.Timestamp,
                    "OPC UA String 数据点应使用文本模式", recordHistory);
            var types = definition.IsProtocolCompare
                ? definition.CompareInputs.Select(field => field.DataType).ToArray()
                : definition.Outputs.Select(field => field.DataType).ToArray();
            if (types.Length == 0)
                return Reject(definition, data, update.Timestamp, "未配置解析字段", recordHistory);
            if (!TrySplitText(definition, text, types.Length, out var values, out var splitReason))
                return Reject(definition, data, update.Timestamp, splitReason, recordHistory);
            // Split the native String directly; UTF-8 bytes above are only for receive history.
            return ProcessDecodedFields(definition, data, update.Timestamp, types, values, recordHistory, allowTextConversion: false);
        }

        var fieldCount = definition.IsProtocolCompare ? definition.CompareInputs.Count : definition.Outputs.Count;
        if (fieldCount != 1)
            return Reject(definition, data, update.Timestamp, "OPC UA 单个数据点必须配置恰好一条解析规则", recordHistory);
        var dataType = definition.IsProtocolCompare ? definition.CompareInputs[0].DataType : definition.Outputs[0].DataType;
        if (!TryReadOpcUaScalar(update, dataType, out var parsed))
            return Reject(definition, data, update.Timestamp,
                $"OPC UA 类型不匹配：数据点为 {update.BuiltInType}，规则为 {dataType}", recordHistory);

        bool success;
        if (definition.IsProtocolCompare)
        {
            var field = definition.CompareInputs[0];
            success = EvaluateSample(definition, field.FieldId, field.CompareRule, parsed.Value, recordHistory, IsCompareMatch(field, parsed));
            field.Value = parsed.Value;
            field.Result = parsed.Display;
            field.Status = success ? "OK" : "NG";
        }
        else
        {
            var field = definition.Outputs[0];
            success = EvaluateSample(definition, field.FieldId, field.CompareRule, parsed.Value, recordHistory, IsOutputMatch(field, parsed));
            field.Value = parsed.Value;
            field.Result = parsed.Display;
            field.Status = success ? "OK" : "NG";
        }
        if (success) ApplyAssemblyResults(definition);
        else ClearAssemblyResults(definition);
        var reason = success ? null : "OPC UA 数据点比较未通过";
        if (recordHistory)
            definition.AddParseHistory(update.Timestamp, data, parsed.Display, success, reason);
        return new CommunicationReceiveOutcome(success, reason);
    }

    private static bool TryReadOpcUaScalar(OpcUaPointValueEventArgs update, string dataType,
        out (object Value, string Display) parsed)
    {
        parsed = default;
        var typeMatches = dataType.ToLowerInvariant() switch
        {
            "boolean" or "bool" => update.BuiltInType == BuiltInType.Boolean && update.Value is bool,
            "string" => update.BuiltInType == BuiltInType.String && update.Value is string,
            "int16" => update.BuiltInType == BuiltInType.Int16 && update.Value is short,
            "uint16" => update.BuiltInType == BuiltInType.UInt16 && update.Value is ushort,
            "int" or "int32" => update.BuiltInType == BuiltInType.Int32 && update.Value is int,
            "uint32" => update.BuiltInType == BuiltInType.UInt32 && update.Value is uint,
            "float" or "float32" => update.BuiltInType == BuiltInType.Float && update.Value is float,
            "double" => update.BuiltInType == BuiltInType.Double && update.Value is double,
            _ => false
        };
        if (!typeMatches) return false;
        if (update.Value is float f && !float.IsFinite(f) || update.Value is double d && !double.IsFinite(d)) return false;
        parsed = (update.Value!, Convert.ToString(update.Value, CultureInfo.InvariantCulture) ?? string.Empty);
        return true;
    }

    private static bool EvaluateSample(CommunicationReceiveEventViewModel definition, Guid fieldId,
        string rule, object value, bool realSample, bool levelMatch)
    {
        if (rule is not ("上升沿" or "下降沿")) return levelMatch;
        if (value is not bool current || !realSample) return false;
        var signature = definition.SelectedDeviceId + "|" + definition.SelectedAddress + "|" + rule;
        var known = definition.BooleanEdgeHistory.TryGetValue(fieldId, out var old) && old.Signature == signature;
        definition.BooleanEdgeHistory[fieldId] = (signature, current);
        return known && (rule == "上升沿" ? !old.Value && current : old.Value && !current);
    }

    public static void ResetHistory(CommunicationReceiveEventViewModel definition)
    {
        definition.BooleanEdgeHistory.Clear();
        foreach (var field in definition.Outputs) field.Value = null;
        foreach (var field in definition.CompareInputs) field.Value = null;
    }


    private static bool IsModbusIntMatch(CommunicationCompareInputViewModel field, int actual, int? previous) =>
        field.CompareRule switch
        {
            "不比较" => true,
            "范围内" => field.Minimum <= field.Maximum && actual >= field.Minimum && actual <= field.Maximum,
            "范围外" => field.Minimum <= field.Maximum && (actual < field.Minimum || actual > field.Maximum),
            "等于" => int.TryParse(field.CompareValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expected) && actual == expected,
            "上升沿" => previous.HasValue && previous.Value == 0 && actual != 0,
            "下降沿" => previous.HasValue && previous.Value != 0 && actual == 0,
            _ => false
        };

    private static CommunicationReceiveOutcome Reject(
        CommunicationReceiveEventViewModel definition, byte[] data, DateTimeOffset timestamp, string reason,
        bool recordHistory = true)
    {
        definition.BooleanEdgeHistory.Clear();
        foreach (var field in definition.Outputs)
        {
            field.Value = null;
            field.Result = string.Empty;
            field.Status = "NG";
        }
        foreach (var field in definition.CompareInputs)
        {
            field.Value = null;
            field.Result = string.Empty;
            field.Status = "NG";
        }
        ClearAssemblyResults(definition);
        if (recordHistory)
        {
            string raw;
            try { raw = StrictUtf8.GetString(data).Replace("\r", "\\r").Replace("\n", "\\n"); }
            catch (DecoderFallbackException) { raw = Convert.ToHexString(data); }
            definition.AddParseHistory(timestamp, data, raw, false, reason);
        }
        return new CommunicationReceiveOutcome(false, reason);
    }

    private static bool TryDecode(CommunicationReceiveEventViewModel definition, byte[] data,
        IReadOnlyList<string> types, out string[] values, out string reason)
    {
        values = Array.Empty<string>();
        reason = string.Empty;
        if (definition.IsBinaryMode)
            return TryDecodeBinary(definition, data, types, out values, out reason);

        string text;
        try { text = StrictUtf8.GetString(data); }
        catch (DecoderFallbackException)
        {
            reason = "报文不是有效的 UTF-8 文本";
            return false;
        }

        return TrySplitText(definition, text, types.Count, out values, out reason);
    }

    private static bool TrySplitText(CommunicationReceiveEventViewModel definition, string text,
        int fieldCount, out string[] values, out string reason)
    {
        values = Array.Empty<string>();
        reason = string.Empty;
        // This is a whole-message check, before splitting; the separator is part of the length.
        var characterCount = 0;
        foreach (var _ in text.EnumerateRunes()) characterCount++;
        if (definition.CharacterLengthCompare && characterCount != definition.CharacterLength)
        {
            reason = $"字符长度不符：期望 {definition.CharacterLength}，实际 {characterCount}";
            return false;
        }

        values = string.IsNullOrEmpty(definition.Separator)
            ? new[] { text }
            : text.Split(definition.Separator, StringSplitOptions.None);
        // Output rules consume fields in order; extra received fields are not output rules.
        if (values.Length < fieldCount)
        {
            reason = $"字段不足：需要 {fieldCount}，实际 {values.Length}";
            return false;
        }
        return true;
    }

    private static bool TryDecodeBinary(CommunicationReceiveEventViewModel definition, byte[] data,
        IReadOnlyList<string> types, out string[] values, out string reason)
    {
        values = new string[types.Count];
        reason = string.Empty;
        var offset = definition.ByteOffset;
        if (offset < 0 || offset > data.Length)
        {
            reason = "字节起始位置超出报文范围";
            return false;
        }
        var littleEndian = string.Equals(definition.ByteOrder, "LittleEndian", StringComparison.OrdinalIgnoreCase);
        for (var index = 0; index < types.Count; index++)
        {
            var type = types[index].ToLowerInvariant();
            var width = type is "int16" or "uint16" ? 2 : type is "int" or "int32" or "uint32" or "float" or "float32" ? 4 : 0;
            if (width == 0)
            {
                reason = $"第 {index + 1} 个字段不支持二进制类型 {types[index]}";
                return false;
            }
            if (offset + width > data.Length)
            {
                reason = $"第 {index + 1} 个字段所需字节不足";
                return false;
            }
            var bytes = data.AsSpan(offset, width);
            values[index] = type switch
            {
                "int16" => (littleEndian ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : BinaryPrimitives.ReadInt16BigEndian(bytes)).ToString(CultureInfo.InvariantCulture),
                "uint16" => (littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes)).ToString(CultureInfo.InvariantCulture),
                "uint32" => (littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes)).ToString(CultureInfo.InvariantCulture),
                "float" or "float32" => BitConverter.Int32BitsToSingle(littleEndian ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : BinaryPrimitives.ReadInt32BigEndian(bytes)).ToString(CultureInfo.InvariantCulture),
                _ => (littleEndian ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : BinaryPrimitives.ReadInt32BigEndian(bytes)).ToString(CultureInfo.InvariantCulture)
            };
            offset += width;
        }
        return true;
    }

    private static bool TryNormalize(string text, string typeName, out (object Value, string Display) value)
    {
        value = default;
        if (typeName.ToLowerInvariant() is "int" or "int32" or "float" or "float32" or "string")
        {
            try
            {
                var parsed = ProtocolValueCodec.ConvertText(text, typeName);
                value = (parsed, Convert.ToString(parsed, CultureInfo.InvariantCulture) ?? string.Empty);
                return true;
            }
            catch (FormatException) { return false; }
        }
        var culture = CultureInfo.InvariantCulture;
        switch (typeName.ToLowerInvariant())
        {
            case "bool" or "boolean" when bool.TryParse(text, out var boolean):
                value = (boolean, boolean.ToString()); return true;
            case "string": value = (text, text); return true;
            case "int" or "int32" when int.TryParse(text, NumberStyles.Integer, culture, out var number):
                value = (number, number.ToString(culture)); return true;
            case "int16" when short.TryParse(text, NumberStyles.Integer, culture, out var shortNumber):
                value = (shortNumber, shortNumber.ToString(culture)); return true;
            case "uint16" when ushort.TryParse(text, NumberStyles.Integer, culture, out var ushortNumber):
                value = (ushortNumber, ushortNumber.ToString(culture)); return true;
            case "uint32" when uint.TryParse(text, NumberStyles.Integer, culture, out var uintNumber):
                value = (uintNumber, uintNumber.ToString(culture)); return true;
            case "double" when double.TryParse(text, NumberStyles.Float, culture, out var doubleNumber) && double.IsFinite(doubleNumber):
                value = (doubleNumber, doubleNumber.ToString(culture)); return true;
            case "float" or "float32" when float.TryParse(text, NumberStyles.Float, culture, out var floatNumber) && float.IsFinite(floatNumber):
                value = (floatNumber, floatNumber.ToString(culture)); return true;
            default: return false;
        }
    }

    private static bool IsCompareMatch(CommunicationCompareInputViewModel field, (object Value, string Display) actual)
    {
        return IsRuleMatch(field.CompareRule, field.IsString, field.CompareValue,
            field.Minimum, field.Maximum, actual);
    }

    private static bool IsOutputMatch(CommunicationEventOutputViewModel field, (object Value, string Display) actual) =>
        IsRuleMatch(field.CompareRule, string.Equals(field.DataType, "string", StringComparison.OrdinalIgnoreCase),
            field.CompareValue, field.Minimum, field.Maximum, actual);

    private static bool IsRuleMatch(string? ruleText, bool isString, string compareValue,
        int minimum, int maximum, (object Value, string Display) actual)
    {
        var rule = ruleText?.Trim() ?? string.Empty;
        if (rule == "不比较") return true;
        if (actual.Value is bool boolean)
            return bool.TryParse(compareValue, out var expectedBoolean) && (rule switch
            {
                "等于" => boolean == expectedBoolean,
                "不等于" => boolean != expectedBoolean,
                _ => false
            });
        if (isString)
            return rule switch
            {
                "等于" => string.Equals(actual.Display, compareValue, StringComparison.Ordinal),
                "不等于" => !string.Equals(actual.Display, compareValue, StringComparison.Ordinal),
                _ => false
            };
        if (minimum > maximum) return false;
        var number = Convert.ToDouble(actual.Value, CultureInfo.InvariantCulture);
        return rule switch
        {
            "范围内" => number >= minimum && number <= maximum,
            "范围外" => number < minimum || number > maximum,
            _ => false
        };
    }

    internal static string GetOutputStatus(CommunicationEventOutputViewModel field) =>
        field.Value != null && IsOutputMatch(field, (field.Value, field.Result)) ? "OK" : "NG";

    internal static string GetCompareStatus(CommunicationCompareInputViewModel field) =>
        field.Value != null && (field.IsByteRule
            ? IsByteRuleMatch(field, (field.Value, field.Result), null)
            : IsCompareMatch(field, (field.Value, field.Result))) ? "OK" : "NG";

    private static void ApplyAssemblyResults(CommunicationReceiveEventViewModel definition)
    {
        foreach (var output in definition.AssemblyOutputs)
        {
            if (!output.SourceFieldId.HasValue)
                output.ResolveBindingIdentity();
            if (!output.SourceFieldId.HasValue)
            {
                output.Value = null;
                output.Result = string.Empty;
                continue;
            }
            var input = definition.CompareInputs.FirstOrDefault(field => field.FieldId == output.SourceFieldId.Value);
            output.Value = input?.Value;
            output.Result = input?.Result ?? string.Empty;
        }
    }

    private static void ClearAssemblyResults(CommunicationReceiveEventViewModel definition)
    {
        foreach (var output in definition.AssemblyOutputs)
        {
            output.Value = null;
            output.Result = string.Empty;
        }
    }
}
