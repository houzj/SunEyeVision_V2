using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Tool.BlobAnalysis;

/// <summary>Finite projection of the explicit Blob field map onto current SDK model slots.</summary>
internal static class BlobConfigurationFields
{
    internal static IReadOnlyList<ConfigurationFieldAccessor> Describe(BlobAnalysisParameters parameters)
        => BlobInputContract.Fields.Where(field=>field.HasStoredLiteral).Select(field=>
        {
            var path=field.LogicalKey switch {
                "input.inspection.mode"=>nameof(parameters.InspectionMode),
                "input.mask.mode"=>nameof(parameters.MaskMode), _=>field.LegacyPath};
            var (owner,property)=FindSlot(parameters,path);
            var descriptor=new FieldPresentation(field.Port.Id,Label(field,parameters),Group(field),field.Presentation.Order,
                field.Presentation.Editor,field.Presentation.ChoiceKeys,field.Presentation.ChoiceKeys.ToDictionary(key=>key,ChoiceLabel));
            return new ConfigurationFieldAccessor(field.Port,descriptor,path,()=>field.ReadStored(parameters),value=>
            {
                var frozen=ValueRules.Freeze(field.Port,value);
                object? converted=property.PropertyType==typeof(Color)&&frozen is RecordValue rgba
                    ?Color.FromArgb((byte)rgba.Get<long>("a"),(byte)rgba.Get<long>("r"),(byte)rgba.Get<long>("g"),(byte)rgba.Get<long>("b"))
                    :property.PropertyType.IsEnum?Enum.Parse(property.PropertyType,(string)frozen!)
                    :BindingClrAdapter.Materialize(field.Port,property.PropertyType,frozen);
                try{property.SetValue(owner,converted);}
                catch(TargetInvocationException exception){throw new InvalidOperationException(exception.InnerException?.Message??exception.Message,exception.InnerException);}
            });
        }).ToArray();

    // Only exact paths from the tool's finite field map enter here. The UI cannot submit a CLR
    // path, infer a field from a live value, or address arbitrary properties/collections.
    private static (object Owner,PropertyInfo Property) FindSlot(object parameters,string path)
    {
        object current=parameters;var parts=path.Split('.');
        for(var index=0;index<parts.Length;index++)
        {
            var part=Regex.Match(parts[index],@"^(\w+)(?:\[Key=([^\]]+)\])?$");
            if(!part.Success)throw new InvalidOperationException("不支持的已声明字段路径："+path);
            var property=current.GetType().GetProperty(part.Groups[1].Value)??throw new InvalidOperationException("字段映射已失效："+path);
            if(index==parts.Length-1){if(!property.CanWrite)throw new InvalidOperationException("字段不可写："+path);return(current,property);}
            current=property.GetValue(current)??throw new InvalidOperationException("字段模型缺失："+path);
            if(part.Groups[2].Success)
            {
                var key=part.Groups[2].Value;
                current=((IEnumerable)current).Cast<object>().Single(item=>Equals(item.GetType().GetProperty("Key")?.GetValue(item),key));
            }
        }
        throw new InvalidOperationException("字段路径为空。");
    }
    private static string Group(BlobInputField field)=>field.LogicalKey.StartsWith("presentation.judgments.")?"结果判定":
        field.LogicalKey.StartsWith("presentation.graphics.")?"图形样式":field.LogicalKey.StartsWith("presentation.texts.")?"文本样式":
        field.LogicalKey.StartsWith("input.")||field.LogicalKey=="setting.pose-enabled"?"基本参数":"运行参数";
    private static string Label(BlobInputField field,BlobAnalysisParameters parameters)
    {
        if(parameters.GetRuntimeParameters().FirstOrDefault(slot=>slot.PropertyName==field.LegacyPath) is { } runtime)return runtime.DisplayName;
        if(field.LogicalKey=="setting.pose-enabled")return "启用位置修正";
        if(field.LogicalKey=="input.inspection.mode")return "检测区域来源";
        if(field.LogicalKey=="input.mask.mode")return "屏蔽区域来源";
        var parts=field.LogicalKey.Split('.');
        return parts[0]=="presentation"?Name(parts[2])+" · "+Name(parts[^1]):field.Presentation.DisplayName;
    }
    private static string Name(string key)=>key switch {
        "output-not-empty"=>"输出不为空","white-ratio"=>"白像素占全图比例","blob-count"=>"Blob数量",
        "total-area"=>"总面积","average-area"=>"平均面积","max-area"=>"最大面积","min-area"=>"最小面积",
        "average-circularity"=>"平均圆度","average-rectangularity"=>"平均矩形度","per-blob-area"=>"各Blob面积",
        "per-blob-circularity"=>"各Blob圆度","per-blob-rectangularity"=>"各Blob矩形度","blobs"=>"Blob",
        "centroids"=>"质心","contours"=>"轮廓","inspection-roi"=>"检测区域","mask-roi"=>"屏蔽区域","area"=>"面积文字",
        "Enabled"=>"启用","Min"=>"下限","Max"=>"上限","IsVisible"=>"显示","OkColor"=>"OK颜色","NgColor"=>"NG颜色",
        "Opacity"=>"透明度","LineWidth"=>"线宽","CrossSize"=>"十字大小","LineDashStyle"=>"线型","FontSize"=>"字号",
        "IsBold"=>"加粗","PositionX"=>"位置X","PositionY"=>"位置Y",_=>key};
    private static string ChoiceLabel(string key)=>key switch {
        "LocalGeometry"=>"本地绘制 / 数值编辑","InheritedGeometry"=>"继承上游几何","InheritedRegion"=>"继承上游像素区域",
        "None"=>"无","Binary"=>"正向阈值","BinaryInv"=>"反向阈值","Mean"=>"均值滤波","Gaussian"=>"高斯滤波",
        "Median"=>"中值滤波","HistogramEqualization"=>"直方图均衡","Erosion"=>"腐蚀","Dilation"=>"膨胀",
        "Opening"=>"开运算","Closing"=>"闭运算","TopHat"=>"顶帽","BottomHat"=>"底帽","Rectangle"=>"矩形","Circle"=>"圆形",
        "AreaAsc"=>"面积升序","AreaDesc"=>"面积降序","CircularityAsc"=>"圆度升序","CircularityDesc"=>"圆度降序",
        "RectangularityAsc"=>"矩形度升序","RectangularityDesc"=>"矩形度降序","XAsc"=>"质心X升序","XDesc"=>"质心X降序",
        "YAsc"=>"质心Y升序","YDesc"=>"质心Y降序","Solid"=>"实线","Dash"=>"虚线","Dot"=>"点线","DashDot"=>"点划线",_=>key};
}
