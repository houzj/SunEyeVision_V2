using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>Numeric-leaf boundary only. The borrowed field state and all reference operations
/// retain their actual sequence declaration; no scalar field or second accepted value is created.</summary>
internal static class NativeNumericSequenceProjection
{
    internal static bool Applies(Port port)=>port.Type.Shape==ValueShape.Sequence&&port.Type.Element==TypeRef.ScalarOf(ScalarKind.Float64);
    internal static object Single(Port port,object value)
    {
        if(!Applies(port)||value is double)return value;
        var reader=new InputValues(new[]{port},new Dictionary<string,object?>{{port.Id,value}}).Read(port.Id);
        if(reader.Count!=1)throw new ArgumentException("此常量含零个或多个数值，不能作为单个数值编辑；请明确输入新常量或选择订阅。");
        return reader.GetElement(0).GetScalar<double>();
    }
    internal static object FreezeNumber(Port port,object value)=>ValueRules.Freeze(port,Applies(port)?new[]{Convert.ToDouble(value,System.Globalization.CultureInfo.InvariantCulture)}:value)!;
    internal static bool Same(Port port,object? left,object? right)
    {
        if(!Applies(port))return Equals(left,right);
        var a=new InputValues(new[]{port},new Dictionary<string,object?>{{port.Id,left}}).Read(port.Id);
        var b=new InputValues(new[]{port},new Dictionary<string,object?>{{port.Id,right}}).Read(port.Id);
        return a.Count==b.Count&&Enumerable.Range(0,a.Count).All(i=>a.GetElement(i).GetScalar<double>().Equals(b.GetElement(i).GetScalar<double>()));
    }
}
