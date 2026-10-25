namespace SunEyeVision.Plugin.Contracts;

/// <summary>Finite presentation values using existing record/scalar constraints, without platform Color or brush objects.</summary>
public static class PresentationDeclarations
{
    public static TypeRef Rgba{get;}=TypeRef.RecordOf("presentation.rgba",new[]{
        new Port("r",DataType.Integer,Storage:NumericStorage.UInt8),new Port("g",DataType.Integer,Storage:NumericStorage.UInt8),
        new Port("b",DataType.Integer,Storage:NumericStorage.UInt8),new Port("a",DataType.Integer,Storage:NumericStorage.UInt8)});
    public static RecordValue Color(byte red,byte green,byte blue,byte alpha=255)=>new(Rgba,new Dictionary<string,object?>{
        ["r"]=(long)red,["g"]=(long)green,["b"]=(long)blue,["a"]=(long)alpha});
}
