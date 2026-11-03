using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Display names for this tool's declared outputs, not a second output/value model.</summary>
internal static class BlobOutputDisplay
{
    internal static IReadOnlyList<OutputDisplayNode> Create()
    {
        var roots=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var (id,name) in new (int,string)[]{(4,"模块状态"),(5,"Blob图像数据"),(6,"二值化图像数据"),
            (8,"低阈值"),(9,"高阈值"),(10,"极性"),(102,"阈值方式"),(11,"Blob个数"),(12,"总面积"),
            (13,"平均面积"),(14,"平均圆度"),(15,"平均矩形度"),(16,"白色像素比例"),(17,"灰度直方图"),
            (18,"通道均值"),(19,"面积"),(20,"周长"),(21,"质心X"),(22,"质心Y"),(23,"圆度"),
            (24,"矩形度"),(25,"矩形宽度"),(26,"矩形高度"),(27,"矩形尺寸"),(30,"最小面积"),(31,"最大面积")})
            roots.Add(BlobSchema.Field(id),name);
        roots.Add(BlobSchema.BlobsPort,"Blob明细");roots.Add(BlobSchema.RegionPort,"检测结果区域");
        var members=new Dictionary<string,string>(StringComparer.Ordinal){
            ["element-id"]="元素标识",["label"]="标签",["area"]="面积",["perimeter"]="周长",
            ["centroid-x"]="质心X",["centroid-y"]="质心Y",["centroid"]="质心点",
            ["circularity"]="圆度",["rectangularity"]="矩形度",["bounds"]="Blob矩形框",
            ["oriented-bounds"]="最小外接矩形",["center-x"]="中心X",["center-y"]="中心Y",["angle-degrees"]="角度",
            ["rotated-bounds"]="最小外接矩形顶点",["contour"]="外轮廓",["contour-lossy"]="轮廓投影有损",
            ["judgment-passed"]="单体状态",["boundary"]="完整边界",["region"]="区域",
            ["x"]="左上点X",["y"]="左上点Y",["width"]="宽度",["height"]="高度",
            ["paths"]="边界路径",["path-id"]="路径标识",["parent-path-id"]="父路径标识",
            ["role"]="路径角色",["closed"]="闭合",["points"]="顶点"};
        OutputDisplayNode Build(Port port,bool root,string rootId)
        {
            var type=port.Type;while(type.Shape==ValueShape.Sequence)type=type.Element!;
            var imagePrefix=rootId==BlobSchema.Field(5)?"Blob图像":rootId==BlobSchema.Field(6)?"二值化图像":null;
            var label=root?roots[port.Id]:imagePrefix is not null?imagePrefix+(port.Id switch{
                "data"=>"数据","width"=>"宽度","height"=>"高度","pixel-format"=>"像素格式",_=>throw new ArgumentException("未知图像成员。")
            }):members[port.Id];
            return new(port.Id,label,type.Shape==ValueShape.Record
                ?type.Fields.Select(field=>Build(field,false,rootId)):Array.Empty<OutputDisplayNode>());
        }
        return OutputDisplayNode.Freeze(BlobSchema.Outputs.Select(port=>Build(port,true,port.Id)));
    }
}
