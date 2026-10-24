namespace SunEyeVision.Plugin.Contracts;

/// <summary>Read-only navigation over already frozen values. No raw array or borrowed resource escapes.</summary>
public sealed class DeclaredValueReader
{
    private readonly object? _value;
    internal DeclaredValueReader(TypeRef declaration,object? value,bool missing=false)
    {Declaration=declaration;_value=value;IsMissing=missing;}
    public TypeRef Declaration {get;}
    public bool IsMissing {get;}
    public bool IsNull => !IsMissing && _value is null;
    private void RequireValue()
    {
        if(IsMissing)throw new InvalidOperationException("The declared field has no value.");
        if(IsNull)throw new InvalidOperationException("The declared field is null.");
    }
    public int Count
    {
        get
        {
            RequireValue();
            if(Declaration.Shape!=ValueShape.Sequence)throw new InvalidOperationException("A sequence declaration is required.");
            return _value switch {SequenceValue sequence=>sequence.Count,double[] array=>array.Length,
                _=>throw new InvalidOperationException("Invalid frozen sequence.")};
        }
    }
    public DeclaredValueReader GetElement(int index)
    {
        var count=Count;
        if(index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));
        return _value switch {SequenceValue sequence=>sequence.Read(index),double[] array=>new(Declaration.Element!,array[index]),
            _=>throw new InvalidOperationException("Invalid frozen sequence.")};
    }
    public DeclaredValueReader GetField(string fieldId)
    {
        RequireValue();
        if(Declaration.Shape!=ValueShape.Record||_value is not RecordValue record)
            throw new InvalidOperationException("A record declaration is required.");
        return record.Read(fieldId);
    }
    public T GetScalar<T>()
    {
        RequireValue();
        if(Declaration.Shape!=ValueShape.Scalar || typeof(T)!=typeof(bool)&&typeof(T)!=typeof(long)&&typeof(T)!=typeof(double)&&typeof(T)!=typeof(string))
            throw new InvalidOperationException("Only immutable canonical scalars can be read directly.");
        return _value is T scalar?scalar:throw new InvalidOperationException("Scalar representation does not match the declaration.");
    }
    /// <summary>Geometry values are closed immutable data; no resource handle or mutable container escapes.</summary>
    public GeometryValue GetGeometry()
    {
        RequireValue();
        return Declaration.Shape==ValueShape.Geometry && _value is GeometryValue geometry
            ?geometry:throw new InvalidOperationException("A geometry declaration is required.");
    }
}
