using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Display names for this tool's declared outputs, not a second output/value model.</summary>
internal static class BlobOutputDisplay
{
    internal static IReadOnlyList<OutputDisplayNode> Create()
    {
        var roots=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var (id,name) in new (int,string)[]{(5,"Blob图像数据"),(6,"二值化图像数据"),
            (8,"低阈值"),(9,"高阈值"),(10,"极性"),(102,"阈值方式"),(11,"Blob个数"),(12,"总面积"),
            (13,"平均面积"),(14,"平均圆度"),(15,"平均矩形度"),(16,"白色像素比例"),(17,"灰度直方图"),
            (18,"通道均值"),(19,"面积"),(20,"周长"),(23,"圆度"),
            (24,"矩形度"),(27,"矩形尺寸"),(30,"最小面积"),(31,"最大面积")})
            roots.Add(BlobSchema.Field(id),name);
        roots.Add(BlobSchema.BlobsPort,"Blob明细");roots.Add(BlobSchema.RegionPort,"检测结果区域");
        roots.Add(BlobSchema.ModuleStatusPort,"模块状态");
        roots.Add(BlobSchema.ModuleRunTimePort,"耗时（ms）");
        roots.Add(BlobSchema.BlobStatusesPort,"单体状态");
        roots.Add(BlobSchema.MinGrayValuesPort,"最小灰度值");
        roots.Add(BlobSchema.MaxGrayValuesPort,"最大灰度值");
        roots.Add(BlobSchema.GrayContrastsPort,"灰度对比度");
        roots.Add(BlobSchema.PrincipalAnglesPort,"Blob主轴角度");
        roots.Add(BlobSchema.OuterRectanglesPort,"最小外接矩形");
        roots.Add(BlobSchema.LongAxesPort,"长轴");
        roots.Add(BlobSchema.ShortAxesPort,"短轴");
        roots.Add(BlobSchema.MaskImagePort,"输出掩膜");
        roots.Add(BlobSchema.MaskPolygonsPort,"屏蔽区多边形");
        roots.Add(BlobSchema.MaskPolygonStringsPort,"屏蔽区字符串");
        roots.Add(BlobSchema.InnerRectanglesPort,"最大内接矩形");
        roots.Add(BlobSchema.DetectionRectanglesPort,"检测矩形");
        roots.Add(BlobSchema.DetectionAnnuliPort,"扇环检测区域");
        roots.Add(BlobSchema.ContourPointsPort,"轮廓点");
        roots.Add(BlobSchema.CentroidPointsPort,"质心点");
        roots.Add(BlobSchema.AxisAlignedRectanglesPort,"Blob矩形框");
        roots.Add(BlobSchema.RectangleTopLeftXPort,"Blob矩形左上点X");
        roots.Add(BlobSchema.RectangleTopLeftYPort,"Blob矩形左上点Y");
        roots.Add(BlobSchema.RectangleBottomRightXPort,"Blob矩形右下点X");
        roots.Add(BlobSchema.RectangleBottomRightYPort,"Blob矩形右下点Y");
        var members=new Dictionary<string,string>(StringComparer.Ordinal){
            ["element-id"]="元素标识",["label"]="标签",["area"]="面积",["perimeter"]="周长",
            ["centroid-x"]="质心X",["centroid-y"]="质心Y",["centroid"]="质心点",
            ["circularity"]="圆度",["rectangularity"]="矩形度",["bounds"]="Blob矩形框",
            ["oriented-bounds"]="最小外接矩形",["center"]="中心点",["origin"]="左上点",["angle-degrees"]="角度",
            ["inner-radius"]="内半径",["outer-radius"]="外半径",["start-degrees"]="起始角度",["sweep-degrees"]="角度范围",
            ["long-axis"]="长轴",["short-axis"]="短轴",
            ["rotated-bounds"]="最小外接矩形顶点",["contour"]="外轮廓",["contour-lossy"]="轮廓投影有损",
            ["judgment-passed"]="单体状态",["boundary"]="完整边界",["region"]="区域",
            ["outline-points"]="轮廓显示点",["inner-rectangle"]="最大内接矩形",
            ["x"]="左上点X",["y"]="左上点Y",["width"]="宽度",["height"]="高度",
            ["paths"]="边界路径",["path-id"]="路径标识",["parent-path-id"]="父路径标识",
            ["role"]="路径角色",["closed"]="闭合",["points"]="顶点",
            ["info"]="轮廓信息",["count"]="轮廓点个数",["score"]="轮廓判定值",["blob-index"]="Blob索引"};
        OutputDisplayNode Build(Port port,bool root,string rootId,bool center=false)
        {
            var type=port.Type;while(type.Shape==ValueShape.Sequence)type=type.Element!;
            var imagePrefix=rootId==BlobSchema.Field(5)?"Blob图像":rootId==BlobSchema.Field(6)?"二值化图像":rootId==BlobSchema.MaskImagePort?"掩膜图像":null;
            var label=root?roots[port.Id]:imagePrefix is not null?imagePrefix+(port.Id switch{
                "data"=>"数据","width"=>"宽度","height"=>"高度","pixel-format"=>"像素格式",_=>throw new ArgumentException("未知图像成员。")
            }):rootId==BlobSchema.ContourPointsPort&&(port.Id is "x" or "y") ? port.Id.ToUpperInvariant()
                :rootId==BlobSchema.CentroidPointsPort ? (port.Id=="x"?"质心X":"质心Y")
                :center ? (port.Id=="x"?"中心X":"中心Y") : members[port.Id];
            return new(port.Id,label,type.Shape==ValueShape.Record
                ?type.Fields.Select(field=>Build(field,false,rootId,port.Id=="center")):Array.Empty<OutputDisplayNode>(),
                isAuxiliaryImage:root&&rootId==BlobSchema.MaskImagePort);
        }
        return OutputDisplayNode.Freeze(BlobSchema.Outputs.Select(port=>
            port.Id==BlobSchema.BlobsPort
                ?new OutputDisplayNode(port.Id,roots[port.Id],isAuxiliaryImage:false,displayInResultPanel:false)
                :port.Id==BlobSchema.ModuleRunTimePort
                    ?new OutputDisplayNode(port.Id,roots[port.Id],isAuxiliaryImage:false,displayInResultPanel:true,isModuleExecutionSummary:true)
                :port.Id==BlobSchema.ModuleStatusPort
                    ?new OutputDisplayNode(port.Id,roots[port.Id],isAuxiliaryImage:false,displayInResultPanel:true,isModuleExecutionSummary:true)
                    :Build(port,true,port.Id)));
    }
}
