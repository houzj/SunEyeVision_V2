namespace SunEyeVision.Plugin.Contracts;

/// <summary>Physical numeric constraints on a semantic Int64/Float64 field; never a separate type catalog.</summary>
public enum NumericStorage { Int8, UInt8, Int16, UInt16, Int32, UInt32, Int64, Float32, Float64 }

public static class NumericStorageRules
{
    public static NumericStorage? Effective(TypeRef type,NumericStorage? declared)
    {
        var element=type.Shape==ValueShape.Sequence?type.Element!:type;
        return declared??(element.Scalar is ScalarKind.Int64 or ScalarKind.Float64?Default(element.Scalar.Value):null);
    }
    public static bool IsInteger(NumericStorage storage) => storage is not NumericStorage.Float32 and not NumericStorage.Float64;
    public static NumericStorage Default(ScalarKind kind) => kind switch
    { ScalarKind.Int64 => NumericStorage.Int64, ScalarKind.Float64 => NumericStorage.Float64, _ => throw new ArgumentException("A numeric scalar is required.") };
    public static (long Minimum,long Maximum) IntegerRange(NumericStorage storage) => storage switch
    {
        NumericStorage.Int8 => (sbyte.MinValue,sbyte.MaxValue), NumericStorage.UInt8 => (byte.MinValue,byte.MaxValue),
        NumericStorage.Int16 => (short.MinValue,short.MaxValue), NumericStorage.UInt16 => (ushort.MinValue,ushort.MaxValue),
        NumericStorage.Int32 => (int.MinValue,int.MaxValue), NumericStorage.UInt32 => (uint.MinValue,uint.MaxValue),
        NumericStorage.Int64 => (long.MinValue,long.MaxValue), _ => throw new ArgumentException("Integer storage is required.")
    };
    public static void ValidateDeclaration(Port port)
    {
        if(port.Storage is not NumericStorage storage)return;
        var element=port.Type.Shape==ValueShape.Sequence?port.Type.Element!:port.Type;
        if(!Enum.IsDefined(storage) || element.Shape!=ValueShape.Scalar ||
            element.Scalar!=(IsInteger(storage)?ScalarKind.Int64:ScalarKind.Float64))
            throw new ArgumentException($"Numeric storage does not match the semantic declaration for {port.Id}.");
    }
    public static void ValidateValue(Port port,object? value)
    {
        ValidateDeclaration(port);
        if(port.Storage is not NumericStorage storage || value is null)return;
        if(port.Type.Shape==ValueShape.Sequence)
        {
            IEnumerable<object?> items=value switch { SequenceValue sequence=>sequence, Array array when array.Rank==1=>array.Cast<object?>(), _=>throw new ArgumentException("A declared sequence is required.") };
            foreach(var item in items)EnsureFits(item,storage);
        }
        else EnsureFits(value,storage);
    }
    public static object EnsureFits(object? value,NumericStorage storage)
    {
        if(!Enum.IsDefined(storage))throw new ArgumentOutOfRangeException(nameof(storage));
        if(IsInteger(storage))
        {
            var (minimum,maximum)=IntegerRange(storage);
            if(value is not long integer || integer<minimum || integer>maximum)
                throw new OverflowException($"Value is outside the declared {storage} range.");
        }
        else if(value is not double number || !double.IsFinite(number) ||
            storage==NumericStorage.Float32 && (!float.IsFinite((float)number) || (double)(float)number!=number))
            throw new OverflowException($"Value cannot be represented exactly as {storage}.");
        return value!;
    }
    public static bool IsWidening(ScalarKind from,NumericStorage? source,ScalarKind to,NumericStorage? target)
    {
        var a=source??Default(from);var b=target??Default(to);
        if(IsInteger(a) && IsInteger(b))
        {var (amin,amax)=IntegerRange(a);var (bmin,bmax)=IntegerRange(b);return amin>=bmin&&amax<=bmax;}
        if(!IsInteger(a)&&!IsInteger(b))return a==b||b==NumericStorage.Float64;
        if(IsInteger(a)&&!IsInteger(b))
        {var (minimum,maximum)=IntegerRange(a);long exact=b==NumericStorage.Float32?16777216L:9007199254740992L;return minimum>=-exact&&maximum<=exact;}
        return false;
    }
    public static object ConvertNumber(object value,ScalarKind from,ScalarKind to,BindingConversion conversion,NumericStorage? target=null)
    {
        if(!Enum.IsDefined(conversion))throw new ArgumentOutOfRangeException(nameof(conversion),"Unknown conversion operation.");
        object converted=value;
        if(from==ScalarKind.Int64&&to==ScalarKind.Float64&&value is long integer)
        {
            double number=integer;
            if(number>=9223372036854775808d || checked((long)number)!=integer)
                throw new OverflowException("Int64 value cannot be represented exactly as Float64.");
            converted=number;
        }
        else if(from==ScalarKind.Float64&&to==ScalarKind.Int64&&value is double number&&conversion==BindingConversion.Float64ToInt64Checked)
        {
            if(!double.IsFinite(number)||Math.Truncate(number)!=number||number< -9223372036854775808d||number>=9223372036854775808d)
                throw new OverflowException("Explicit Float64-to-Int64 conversion requires a finite integral value within Int64 range.");
            converted=checked((long)number);
        }
        else if(from!=to)throw new ArgumentException("No declared numeric conversion operation applies.");
        return EnsureFits(converted,target??Default(to));
    }
}
