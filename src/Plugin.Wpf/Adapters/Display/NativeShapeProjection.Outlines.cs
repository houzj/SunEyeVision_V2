using System.Buffers.Binary;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Presenters.Display;

namespace SunEyeVision.Plugin.Wpf.Adapters.Display;

internal static partial class NativeShapeProjection
{
    // HALCON-to-original-point-record host adaptation, not algorithm equivalence.
    internal static PreparedNativeShapes Outlines(NativeResultFrameReceipt receipt,IReadOnlyList<NativeOutlineOverlay> overlays,
        int maximum,int maximumPoints,Func<bool> current)
    {
        if(overlays.Count>maximum)throw new ArgumentException("shape-budget: 轮廓对象超过预算。");
        var items=new List<object>();var count=0;
        foreach(var overlay in overlays.ToArray())
        {
            NativeResultRaster.CheckCurrent(current);
            if(overlay is null||overlay.SourceFrame!=receipt.Frame||overlay.Coordinates!=receipt.Coordinates
                ||overlay.Coordinates.Domain!=CoordinateDomain.Image||overlay.Coordinates.Unit!=CoordinateUnit.Pixel||overlay.ResultIndex<0)
                throw new ArgumentException("轮廓与当前物理帧、像素坐标或结果序号不符。");
            var points=overlay.Points.ToArray();count=checked(count+points.Length);
            if(count>maximumPoints)throw new ArgumentException("shape-budget: 轮廓点超过预算。");
            var scores=overlay.Scores;
            if(scores is not null&&scores.Count!=points.Length)
                throw new ArgumentException("轮廓点分数与点数不一致。");
            var bytes=new byte[checked(points.Length*16)];
            for(var i=0;i<points.Length;i++)
            {
                if ((i & 255) == 0) NativeResultRaster.CheckCurrent(current);
                var x=(float)points[i].X;var y=(float)points[i].Y;
                if(!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentException("轮廓坐标超出原float点集范围。");
                var record=bytes.AsSpan(i*16,16);
                BinaryPrimitives.WriteSingleLittleEndian(record,x);BinaryPrimitives.WriteSingleLittleEndian(record[4..],y);
                // 原件 MultiPointShape：逐点分数 0→OK色、1→黄色、其余→NG色。
                var marker=scores is null?(overlay.Passed?0f:2f):scores[i];
                if(!float.IsFinite(marker))throw new ArgumentException("轮廓点分数不是有限值。");
                BinaryPrimitives.WriteSingleLittleEndian(record[8..],marker);
                BinaryPrimitives.WriteInt32LittleEndian(record[12..],overlay.ResultIndex);
            }
            if(points.Length!=0)
                items.Add(new OutlinePoint(bytes,overlay.Ok.ToString(),overlay.Ng.ToString())
                {
                    // 原件 Display.xml 的 outlinepoint 带 opacity/linewidth（如 #66ff00 配 1 / 2）；
                    // 渲染基类 ShapePrimitive 已提供这两个属性，此前未按声明写入。
                    Opacity=overlay.Opacity,
                    StrokeThickness=overlay.Width
                });
        }
        NativeResultRaster.CheckCurrent(current);
        return new(items.ToArray(),items.Count,count);
    }
    // The original control writes one pointsImage. Aggregate owners without mutating
    // their prepared data, so each native paint consumes the complete active set.
    internal static object[] MergePointBatches(object[] items)
    {
        var batches=items.OfType<IMultiPoint>().ToArray();
        if(batches.Length<2)return items;
        var merged=new MultiPoint();
        foreach(var batch in batches)
        {
            foreach(var point in batch.OutlinePoints)merged.OutlinePoints.Add(point);
            foreach(var point in batch.MicroPoints)merged.MicroPoints.Add(point);
            foreach(var rectangle in batch.Rectangles)merged.Rectangles.Add(rectangle);
        }
        var result=new List<object>();var inserted=false;
        foreach(var item in items)
        {if(item is not IMultiPoint)result.Add(item);else if(!inserted){result.Add(merged);inserted=true;}}
        return result.ToArray();
    }
}
