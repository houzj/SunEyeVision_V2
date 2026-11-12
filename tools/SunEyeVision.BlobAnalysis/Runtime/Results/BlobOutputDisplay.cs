using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>
/// 原件「模块结果树」的显示声明：项名、顺序、分组与成员路径都来自这里，渲染代码不再起名。
/// 证据：docs/collaboration/evidence/Blob模块结果树原件结构-20261112.json（原厂截图 6 张）。
/// 原件把记录型结果按成员转置成列（每成员一行、值为全部 Blob 的该成员、分号拼接），
/// 因此「质心点」「最小外接矩形」「Blob矩形框」等组合项由成员行承载，不再展开元素索引。
/// </summary>
internal static class BlobOutputDisplay
{
    internal static IReadOnlyList<OutputDisplayNode> Create()
    {
        var rows=new List<OutputDisplayNode>
        {
            new(BlobSchema.ModuleRunTimePort,"耗时（ms）",isModuleExecutionSummary:true),
            new(BlobSchema.ModuleStatusPort,"模块状态",isModuleExecutionSummary:true),
            new(BlobSchema.BlobStatusesPort,"单体状态"),
            new(BlobSchema.Field(11),"Blob个数"),
            new(BlobSchema.Field(19),"面积"),
            new(BlobSchema.Field(12),"总面积"),
            new(BlobSchema.CentroidPointsPort,"质心点",new[]
            {
                new OutputDisplayNode("x","质心X"),
                new OutputDisplayNode("y","质心Y")
            }),
            new(BlobSchema.OuterRectanglesPort,"最小外接矩形",new[]
            {
                new OutputDisplayNode("center","中心点",new[]
                {
                    new OutputDisplayNode("x","中心X"),
                    new OutputDisplayNode("y","中心Y")
                }),
                new OutputDisplayNode("width","矩形宽度"),
                new OutputDisplayNode("height","矩形高度"),
                new OutputDisplayNode("angle-degrees","角度")
            }),
            new(BlobSchema.MinGrayValuesPort,"最小灰度值"),
            new(BlobSchema.MaxGrayValuesPort,"最大灰度值"),
            new(BlobSchema.GrayContrastsPort,"对比度"),
            new(BlobSchema.InnerRectanglesPort,"最大内接矩形",new[]
            {
                new OutputDisplayNode("center","最大内接矩形中心点",new[]
                {
                    new OutputDisplayNode("x","最大内接矩形中心X"),
                    new OutputDisplayNode("y","最大内接矩形中心Y")
                }),
                new OutputDisplayNode("width","最大内接矩形宽度"),
                new OutputDisplayNode("height","最大内接矩形高度"),
                new OutputDisplayNode("angle-degrees","最大内接矩形角度")
            }),
            // 原件「Blob矩形框」跨越四个输出端口：矩形框原点/宽高来自 bounds，
            // 左上/右下点来自独立数组端口，主轴角度来自角度端口，因此子行显式给出目标端口。
            new(BlobSchema.AxisAlignedRectanglesPort,"Blob矩形框",new[]
            {
                new OutputDisplayNode("origin","矩形框原点",new[]
                {
                    new OutputDisplayNode("x","Blob矩形框原点X"),
                    new OutputDisplayNode("y","Blob矩形框原点Y")
                }),
                new OutputDisplayNode("width","Blob矩形框宽度"),
                new OutputDisplayNode("height","Blob矩形框高度"),
                new OutputDisplayNode("top-left-x","blob矩形左上点X",portId:BlobSchema.RectangleTopLeftXPort),
                new OutputDisplayNode("top-left-y","blob矩形左上点Y",portId:BlobSchema.RectangleTopLeftYPort),
                new OutputDisplayNode("bottom-right-x","blob矩形右下点X",portId:BlobSchema.RectangleBottomRightXPort),
                new OutputDisplayNode("bottom-right-y","blob矩形右下点Y",portId:BlobSchema.RectangleBottomRightYPort),
                new OutputDisplayNode("principal-angle","主轴角度",portId:BlobSchema.PrincipalAnglesPort)
            }),
            new(BlobSchema.Field(20),"周长"),
            new(BlobSchema.LongAxesPort,"长轴"),
            new(BlobSchema.ShortAxesPort,"短轴"),
            new(BlobSchema.Field(23),"圆形度"),
            new(BlobSchema.Field(24),"矩形度"),
            // 原件此处还有「分数」，本方没有 Blob 级 score 数据来源（只有轮廓判定值 0/2），
            // 按禁止兜底回退原则不伪造该行；缺口登记在证据 JSON 与 CURRENT_TASK.md。
            new(BlobSchema.Field(6),"二值化图像数据",new[]
            {
                new OutputDisplayNode("data","二值化图像数据"),
                new OutputDisplayNode("width","二值化图像宽度"),
                new OutputDisplayNode("height","二值化图像高度"),
                new OutputDisplayNode("pixel-format","二值化图像像素格式")
            }),
            new(BlobSchema.Field(5),"Blob图像数据",new[]
            {
                new OutputDisplayNode("data","Blob图像数据"),
                new OutputDisplayNode("width","Blob图像宽度"),
                new OutputDisplayNode("height","Blob图像高度"),
                new OutputDisplayNode("pixel-format","Blob图像像素格式")
            }),
            new(BlobSchema.DetectionRectanglesPort,"检测区域",new[]
            {
                new OutputDisplayNode("center","检测区域中心点",new[]
                {
                    new OutputDisplayNode("x","检测区域中心点X"),
                    new OutputDisplayNode("y","检测区域中心点Y")
                }),
                new OutputDisplayNode("width","检测区域宽度"),
                new OutputDisplayNode("height","检测区域高度"),
                new OutputDisplayNode("angle-degrees","检测区域角度")
            }),
            new(BlobSchema.DetectionAnnuliPort,"ROI圆弧",new[]
            {
                new OutputDisplayNode("center","检测圆弧中心点",new[]
                {
                    new OutputDisplayNode("x","检测圆弧中心X"),
                    new OutputDisplayNode("y","检测圆弧中心Y")
                }),
                new OutputDisplayNode("inner-radius","检测圆弧内径"),
                new OutputDisplayNode("outer-radius","检测圆弧外径"),
                new OutputDisplayNode("start-degrees","检测圆弧起始角度"),
                new OutputDisplayNode("sweep-degrees","检测圆弧角度范围")
            }),
            new(BlobSchema.ContourPointsPort,"轮廓点集",new[]
            {
                new OutputDisplayNode("info","轮廓信息"),
                new OutputDisplayNode("count","轮廓点个数")
            }),
            new(BlobSchema.MaskPolygonsPort,"屏蔽区多边形",new[]
            {
                new OutputDisplayNode("count","多边形点数"),
                new OutputDisplayNode("vertex","多边形点集",new[]
                {
                    new OutputDisplayNode("x","多边形点X",portId:BlobSchema.MaskPolygonsPort,memberPath:new[]{"x"}),
                    new OutputDisplayNode("y","多边形点Y",portId:BlobSchema.MaskPolygonsPort,memberPath:new[]{"y"})
                },portId:BlobSchema.MaskPolygonsPort),
                new OutputDisplayNode("mask-strings","屏蔽区",portId:BlobSchema.MaskPolygonStringsPort)
            }),
            new(BlobSchema.MaskImagePort,"输出掩膜",new[]
            {
                new OutputDisplayNode("data","输出掩膜图像"),
                new OutputDisplayNode("width","输出掩膜宽度"),
                new OutputDisplayNode("height","输出掩膜高度"),
                new OutputDisplayNode("pixel-format","输出掩膜像素格式")
            },isAuxiliaryImage:true)
        };
        // 原件清单之外的端口按证据隐藏：端口与 schema 指纹保持不动，只是不进结果树。
        foreach(var (portId,name) in new (string,string)[]
        {
            (BlobSchema.Field(8),"低阈值"),(BlobSchema.Field(9),"高阈值"),(BlobSchema.Field(10),"极性"),(BlobSchema.Field(102),"阈值方式"),
            (BlobSchema.Field(13),"平均面积"),(BlobSchema.Field(14),"平均圆度"),(BlobSchema.Field(15),"平均矩形度"),(BlobSchema.Field(16),"白色像素比例"),
            (BlobSchema.Field(17),"灰度直方图"),(BlobSchema.Field(18),"通道均值"),(BlobSchema.Field(27),"矩形尺寸"),
            (BlobSchema.Field(30),"最小面积"),(BlobSchema.Field(31),"最大面积"),
            (BlobSchema.BlobsPort,"Blob明细"),(BlobSchema.RegionPort,"检测结果区域")
        })
            rows.Add(new OutputDisplayNode(portId,name,displayInResultPanel:false));
        return OutputDisplayNode.Freeze(rows);
    }
}
