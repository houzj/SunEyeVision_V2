namespace SunEyeVision.Plugin.Contracts;

/// <summary>Single semantic declaration for variable values supported by the formal scalar binding chain.</summary>
public static partial class VariableValueDeclarations
{
    /// <summary>Validates the actual CLR value accepted by the text editor, including its established array forms.</summary>
    public static void ValidateEditableValue(string type, object? value)
    {
        var valid = type switch
        {
            "Integer" => value is int or long or int[] or long[],
            "Double" => value is float f ? float.IsFinite(f) : value is double d ? double.IsFinite(d)
                : value is float[] fs ? fs.All(float.IsFinite) : value is double[] ds && ds.All(double.IsFinite),
            "String" => value is string or string[],
            "Boolean" => value is bool or bool[],
            "Integer[]" => value is long[],
            "Double[]" => value is double[] sequence && sequence.All(double.IsFinite),
            "String[]" => value is string[] texts && texts.All(text => text is not null),
            "Boolean[]" => value is bool[],
            _ => false
        };
        if (!valid) throw new InvalidOperationException("当前值与变量类型不匹配，或该类型不支持文本值编辑。");
    }

    public static bool TryGetConsumerPort(string vocabulary, out Port port)
    {
        if (vocabulary == "Image")
        {
            port = new Port("value", TypeRef.ResourceOf(ResourceKind.Image));
            return true;
        }
        return TryGetPort(vocabulary, out port);
    }

    public static bool TryGetPort(string vocabulary, out Port port)
    {
        port = null!;
        if (string.IsNullOrWhiteSpace(vocabulary)) return false;
        var sequence = vocabulary.EndsWith("[]", StringComparison.Ordinal);
        var name = sequence ? vocabulary[..^2] : vocabulary;
        if (sequence && name.Equals("Boolean", StringComparison.OrdinalIgnoreCase) && vocabulary != "Boolean[]") return false;
        ScalarKind kind; NumericStorage? storage;
        if (name.Equals("Integer", StringComparison.OrdinalIgnoreCase)) { kind = ScalarKind.Int64; storage = sequence && vocabulary != "Integer[]" ? NumericStorage.Int32 : NumericStorage.Int64; }
        else if (name.Equals("Double", StringComparison.OrdinalIgnoreCase)) { kind = ScalarKind.Float64; storage = NumericStorage.Float64; }
        else if (name.Equals("Float", StringComparison.OrdinalIgnoreCase)) { kind = ScalarKind.Float64; storage = NumericStorage.Float32; }
        else if (name.Equals("String", StringComparison.OrdinalIgnoreCase)) { kind = ScalarKind.Text; storage = null; }
        else if (name.Equals("Boolean", StringComparison.OrdinalIgnoreCase)) { kind = ScalarKind.Boolean; storage = null; }
        else return false;
        var scalar = TypeRef.ScalarOf(kind);
        port = new Port("value", sequence ? TypeRef.SequenceOf(scalar) : scalar, Storage: storage);
        return true;
    }
}

