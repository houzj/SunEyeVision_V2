using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>CLR declarations and values adapt to the single semantic contract; checked rules live in Contracts.</summary>
public static class BindingClrAdapter
{
    public static string OutputPortId(OutputVariable output)=>output.FieldId is Guid id?"field:"+id.ToString("N"):"name:"+output.Name;
    public static Port DescribeOutput(OutputVariable output)
    {
        if(output.Kind!=OutputVariableKind.Data)throw new ArgumentException("Only formal data outputs can be bound.");
        if(output.ValueDeclaration is { } explicitType)
        {
            NumericStorage? storage=null;
            if(output.DeclaredType is { } clr&&TryDescribe(clr,out var from,out var physical)&&
                (from.Shape==ValueShape.Sequence?from.Element:from)==(explicitType.Shape==ValueShape.Sequence?explicitType.Element:explicitType))storage=physical;
            return new(OutputPortId(output),explicitType,Nullable:output.AllowsNull,Storage:storage);
        }
        var declared=output.DeclaredType??SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToSystemType(output.VariableType);
        if(declared==typeof(RoiShape[]))return new(OutputPortId(output),RoiShapeValueAdapter.SequenceDeclaration);
        if(declared is null||declared==typeof(object))throw new ArgumentException("Output type is not formally declared.");
        return DescribePort(OutputPortId(output),declared,output.FixedCount,output.IsCollection);
    }
    public static bool TryDescribe(Type clrType,out TypeRef type,out NumericStorage? storage)
    {
        var plain=Nullable.GetUnderlyingType(clrType)??clrType;
        if(plain.IsArray&&plain.GetArrayRank()==1)
        {
            if(TryDescribe(plain.GetElementType()!,out var element,out storage)&&element.Shape==ValueShape.Scalar)
            {type=TypeRef.SequenceOf(element);return true;}
            type=null!;storage=null;return false;
        }
        storage=plain==typeof(sbyte)?NumericStorage.Int8:plain==typeof(byte)?NumericStorage.UInt8:
            plain==typeof(short)?NumericStorage.Int16:plain==typeof(ushort)?NumericStorage.UInt16:
            plain==typeof(int)?NumericStorage.Int32:plain==typeof(uint)?NumericStorage.UInt32:
            plain==typeof(long)?NumericStorage.Int64:plain==typeof(float)?NumericStorage.Float32:
            plain==typeof(double)?NumericStorage.Float64:null;
        type=storage is NumericStorage numeric?TypeRef.ScalarOf(NumericStorageRules.IsInteger(numeric)?ScalarKind.Int64:ScalarKind.Float64):
            plain==typeof(bool)?TypeRef.ScalarOf(ScalarKind.Boolean):plain==typeof(string)?TypeRef.ScalarOf(ScalarKind.Text):null!;
        return type is not null;
    }
    public static Port DescribePort(string id,Type clrType,int? fixedCount=null,bool? collection=null,bool required=true)
    {
        if(!TryDescribe(clrType,out var type,out var storage))throw new ArgumentException($"No declared contract adapter for {clrType.Name}.");
        var many=collection??type.Shape==ValueShape.Sequence;
        var element=type.Shape==ValueShape.Sequence?type.Element!:type;
        if(many)type=TypeRef.SequenceOf(element,fixedCount.HasValue?CountConstraint.Exactly(fixedCount.Value):CountConstraint.Any);
        else type=element;
        return new(id,type,required,Nullable.GetUnderlyingType(clrType)!=null,storage);
    }
    public static object ConvertNumeric(object value,Type targetType,BindingConversion conversion=BindingConversion.Automatic)
    {
        var target=Nullable.GetUnderlyingType(targetType)??targetType;
        if(!TryDescribe(value.GetType(),out var from,out var sourceStorage)||sourceStorage is null||from.Shape!=ValueShape.Scalar||
            !TryDescribe(target,out var to,out var targetStorage)||targetStorage is null||to.Shape!=ValueShape.Scalar)
            throw new ArgumentException("Unsupported numeric representation; a declared mapping is required.");
        var canonical=NumericStorageRules.IsInteger(sourceStorage.Value)
            ?(object)Convert.ToInt64(value,System.Globalization.CultureInfo.InvariantCulture)
            :Convert.ToDouble(value,System.Globalization.CultureInfo.InvariantCulture);
        NumericStorageRules.EnsureFits(canonical,sourceStorage.Value);
        return MaterializeScalar(NumericStorageRules.ConvertNumber(canonical,from.Scalar!.Value,to.Scalar!.Value,conversion,targetStorage),target);
    }
    public static object? NormalizeValue(Port port,object? value)
    {
        if(value is null)return ValueRules.Freeze(port,null);
        if(port.Type==RoiShapeValueAdapter.Declaration&&value is RoiShape shape)return RoiShapeValueAdapter.Freeze(shape);
        if(value is RecordValue or SequenceValue or GeometryValue)return ValueRules.Freeze(port,value);
        if(port.Type.Shape==ValueShape.Resource&&port.Type.Resource==ResourceKind.Image&&value is SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage image)
            return ValueRules.Freeze(port,new BorrowedImage(image));
        // Existing SDK scalar outputs have a one-item transport wrapper. It does not change their declaration.
        if(port.Type.Shape!=ValueShape.Sequence&&value is Array transport)
        {
            if(transport.Rank!=1||transport.Length!=1)throw new ArgumentException("A scalar output requires exactly one transport item.");
            return NormalizeValue(port,transport.GetValue(0));
        }
        if(port.Type.Shape==ValueShape.Sequence)
        {
            if(value is not Array { Rank:1 } array)throw new ArgumentException("The formal sequence requires a one-dimensional array.");
            var element=new Port(port.Id,port.Type.Element!,Storage:port.Storage);
            var items=array.Cast<object?>().Select(item=>NormalizeValue(element,item)).ToArray();
            return ValueRules.Freeze(port,port.Type.Element==TypeRef.ScalarOf(ScalarKind.Float64)?items.Cast<double>().ToArray():new SequenceValue(port.Type,items));
        }
        if(port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64)
        {
            if(!TryDescribe(value.GetType(),out var actual,out var actualStorage)||actual.Shape!=ValueShape.Scalar||actual.Scalar!=port.Type.Scalar||
                actualStorage!=(port.Storage??NumericStorageRules.Default(port.Type.Scalar.Value)))
                throw new ArgumentException("Runtime numeric representation differs from the formal declaration.");
            value=port.Type.Scalar==ScalarKind.Int64?(object)Convert.ToInt64(value,System.Globalization.CultureInfo.InvariantCulture):Convert.ToDouble(value,System.Globalization.CultureInfo.InvariantCulture);
        }
        return ValueRules.Freeze(port,value);
    }
    private sealed class BorrowedImage(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage image):IReadOnlyImage
    {
        public int Width=>image.Width;
        public int Height=>image.Height;
        public byte[] CopyPixels()=>image.GetPixelData().ToArray();
    }
    public static object? Materialize(Port port,Type targetType,object? canonical)
    {
        canonical=ValueRules.Freeze(port,canonical);
        if(canonical is null)return null;
        var target=Nullable.GetUnderlyingType(targetType)??targetType;
        if(target.IsArray&&target.GetArrayRank()==1)
        {
            IEnumerable<object?> items=canonical switch { SequenceValue sequence=>sequence,Array array when array.Rank==1=>array.Cast<object?>(),_=>throw new ArgumentException("Canonical sequence required.") };
            var values=items.Select(item=>MaterializeScalar(item!,target.GetElementType()!)).ToArray();
            var result=Array.CreateInstance(target.GetElementType()!,values.Length);
            for(int i=0;i<values.Length;i++)result.SetValue(values[i],i);
            return result;
        }
        return MaterializeScalar(canonical,target);
    }
    private static object MaterializeScalar(object value,Type target)
    {
        if(!TryDescribe(target,out var type,out var storage)||type.Shape!=ValueShape.Scalar)throw new ArgumentException("Scalar target required.");
        if(storage is NumericStorage numeric)NumericStorageRules.EnsureFits(value,numeric);
        checked
        {
            if(target==typeof(sbyte))return (sbyte)(long)value;if(target==typeof(byte))return (byte)(long)value;
            if(target==typeof(short))return (short)(long)value;if(target==typeof(ushort))return (ushort)(long)value;
            if(target==typeof(int))return (int)(long)value;if(target==typeof(uint))return (uint)(long)value;
            if(target==typeof(long))return (long)value;if(target==typeof(float))return (float)(double)value;
            if(target==typeof(double))return (double)value;
        }
        return value;
    }
}
