namespace SunEyeVision.Plugin.Contracts;

/// <summary>Single semantic declaration for variable values supported by the formal scalar binding chain.</summary>
public static class VariableValueDeclarations
{
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

