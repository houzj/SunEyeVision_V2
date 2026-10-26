using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>Finite geometry composition. Missing or failed inputs never create substitute geometry.</summary>
public static class RoiComposeResolver
{
    public static IReadOnlyList<string> RequiredParameters(ShapeType shape) => shape switch
    {
        ShapeType.Point => new[]{"CenterX","CenterY"},
        ShapeType.Line => new[]{"StartX","StartY","EndX","EndY"},
        ShapeType.Circle => new[]{"CenterX","CenterY","Radius"},
        ShapeType.RotatedRectangle => new[]{"CenterX","CenterY","Width","Height","Angle"},
        ShapeType.Annulus => new[]{"CenterX","CenterY","InnerRadius","OuterRadius","StartAngle","EndAngle"},
        ShapeType.Arc => new[]{"CenterX","CenterY","Radius","StartAngle","EndAngle"},
        _ => throw new ArgumentException("ROI_COMPOSE_SHAPE: 此形状没有参数拼装合同。",nameof(shape))
    };

    public static IReadOnlyList<object?> ResolveComposedGeometries(Roi roi, IDictionary<string, ToolResults> nodeResults)
    {
        ArgumentNullException.ThrowIfNull(roi);
        if(!roi.IsComposed)return Array.Empty<object?>();
        var values=new Dictionary<string,object?>();
        foreach(var key in RequiredParameters(roi.DeclaredShapeType))
        {
            if(roi.ParameterBindings?.TryGetValue(key,out var setting)!=true||setting is null)
                throw Missing(key);
            values[key]=setting.BindingType switch
            {
                BindingType.Constant => setting.ConstantValue,
                BindingType.Binding when !string.IsNullOrWhiteSpace(setting.SourceNodeId)&&!string.IsNullOrWhiteSpace(setting.SourceProperty)
                    => OutputVariableResolver.Resolve(nodeResults,setting.SourceNodeId!,setting.SourceProperty!),
                _ => throw Missing(key)
            };
        }
        return ComposeAllByTemplate(roi.DeclaredShapeType,values);
    }

    /// <summary>Scalars broadcast. Arrays, including empty and one-element arrays, must have identical lengths.</summary>
    public static IReadOnlyList<object?> ComposeAllByTemplate(ShapeType shapeType,IReadOnlyDictionary<string,object?> rawValues)
    {
        ArgumentNullException.ThrowIfNull(rawValues);
        var required=RequiredParameters(shapeType);
        // Persisted object-valued settings deserialize as JSON. Only numeric JSON is a numeric literal.
        var values=required.ToDictionary(key=>key,key=>rawValues.TryGetValue(key,out var raw)?ReadLiteral(raw):throw Missing(key));
        int? count=null;
        foreach(var key in required)
        {
            var value=values[key];if(value is null)throw Missing(key);
            if(value is Array array)
            {
                if(array.Rank!=1||array.GetLowerBound(0)!=0)throw new ArgumentException("ROI_COMPOSE_ARRAY: 参数数组必须是一维零起点数组。");
                if(count.HasValue&&count.Value!=array.Length)
                    throw new ArgumentException($"ROI_COMPOSE_COUNT: 参数 {key} 的数组长度 {array.Length} 与 {count.Value} 不一致；只有标量可广播。");
                count=array.Length;
                foreach(var item in array)Number(item,key);
            }
            else Number(value,key);
        }
        var result=new List<object?>(count??1);
        for(var index=0;index<(count??1);index++)
            result.Add(ComposeByTemplate(shapeType,key=>Number(values[key] is Array items?items.GetValue(index):values[key],key)));
        return result;
    }

    private static object? ReadLiteral(object? value)=>value is not JsonElement json?value:json.ValueKind switch
    {
        JsonValueKind.Number=>json.GetDouble(),
        JsonValueKind.Array=>json.EnumerateArray().Select(item=>item.ValueKind==JsonValueKind.Number
            ?(object)item.GetDouble():throw new ArgumentException("ROI_COMPOSE_TYPE: 保存的参数数组只允许数值元素。")).ToArray(),
        JsonValueKind.Null=>null,
        _=>throw new ArgumentException("ROI_COMPOSE_TYPE: 保存的参数只允许数值，不能把文本转换为数值。")
    };

    private static double Number(object? value,string key)
    {
        var number=value switch {double d=>d,float f=>f,int i=>i,long l=>l,_=>throw new ArgumentException($"ROI_COMPOSE_TYPE: 参数 {key} 必须是声明数值，不能用空值或文本代替。")};
        if(!double.IsFinite(number))throw new ArgumentException($"ROI_COMPOSE_FINITE: 参数 {key} 必须是有限数值。");
        return number;
    }
    private static ArgumentException Missing(string key)=>new($"ROI_COMPOSE_REQUIRED: 缺少必需参数 {key}，不能使用默认几何。");

    public static object? ComposeByTemplate(ShapeType shapeType,Func<string,double?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var values=RequiredParameters(shapeType).ToDictionary(key=>key,key=>Number(resolve(key),key));
        double Get(string key)=>values[key];
        double Positive(string key){var value=Get(key);if(value<=0)throw new ArgumentException($"ROI_COMPOSE_SIZE: 参数 {key} 必须大于0。");return value;}
        switch(shapeType)
        {
            case ShapeType.Point:return new Point2d(Get("CenterX"),Get("CenterY"));
            case ShapeType.Line:
                var start=new Point(Get("StartX"),Get("StartY"));var end=new Point(Get("EndX"),Get("EndY"));
                if((end-start).LengthSquared<1e-20)throw new ArgumentException("ROI_COMPOSE_LINE: 直线两点不能重合。");
                return Line2d.FromTwoPoints(start,end);
            case ShapeType.Circle:return new Circle2d(Get("CenterX"),Get("CenterY"),Positive("Radius"));
            case ShapeType.RotatedRectangle:return new RotatedRect2d(Get("CenterX"),Get("CenterY"),Positive("Width"),Positive("Height"),Get("Angle"));
            case ShapeType.Arc:return new Arc2d(Get("CenterX"),Get("CenterY"),Positive("Radius"),Get("StartAngle"),Get("EndAngle"));
            case ShapeType.Annulus:
                var inner=Get("InnerRadius");var outer=Positive("OuterRadius");
                if(inner<0||inner>=outer)throw new ArgumentException("ROI_COMPOSE_RADII: 内半径须非负且小于外半径。");
                return new Annulus2d(Get("CenterX"),Get("CenterY"),inner,outer,Get("StartAngle"),Get("EndAngle"));
            default:throw new ArgumentException("ROI_COMPOSE_SHAPE: 此形状没有参数拼装合同。");
        }
    }
}
