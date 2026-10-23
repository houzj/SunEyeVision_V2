using System.Collections;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>Immutable declared sequence. Mutable leaf arrays are copied on every read.</summary>
public sealed class SequenceValue : IReadOnlyList<object?>
{
    private readonly object?[] _items;
    public SequenceValue(TypeRef declaration,IEnumerable<object?> items)
    {
        if(declaration.Shape!=ValueShape.Sequence)throw new ArgumentException("A sequence declaration is required.");
        Declaration=declaration;
        var source=items.ToArray();
        if(!declaration.Count.Accepts(source.Length))throw new ArgumentException("Sequence count violates its declaration.");
        var element=new Port("element",declaration.Element!);
        _items=source.Select(item=>ValueRules.Freeze(element,item)).ToArray();
    }
    public TypeRef Declaration { get; }
    public int Count=>_items.Length;
    public object? this[int index]=>ValueRules.Copy(_items[index]);
    public IEnumerator<object?> GetEnumerator(){for(int i=0;i<Count;i++)yield return this[i];}
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
}

/// <summary>Only declared fields are traversed; arbitrary object graphs are never scanned.</summary>
public sealed class RecordValue
{
    private readonly InputValues _fields;
    public RecordValue(TypeRef declaration,IReadOnlyDictionary<string,object?> fields)
    {
        if(declaration.Shape!=ValueShape.Record)throw new ArgumentException("A record declaration is required.");
        Declaration=declaration; _fields=new(declaration.Fields,fields);
    }
    public TypeRef Declaration { get; }
    public bool Contains(string fieldId)=>_fields.Contains(fieldId);
    public T Get<T>(string fieldId)=>_fields.Get<T>(fieldId);
    public IReadOnlyDictionary<string,object?> CopyFields()=>_fields.CopyValues();
    internal IEnumerable<IReadOnlyImage> Images()=>_fields.Images();
}
