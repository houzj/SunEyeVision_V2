using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Display;

internal static partial class NativeRoiResultProjection
{
    internal static PreparedNativeShapes Prepare(NativeResultFrameReceipt receipt,IReadOnlyList<NativeRoiResultOverlay> rows,int maximum,int maximumVertices,Func<bool> current)
    {
        if(rows.Count>maximum)throw new ArgumentException("shape-budget: 完整区域图形超过预算。");
        var items=new List<object>();var vertices=0;
        foreach(var row in rows.ToArray())
        {
            NativeResultRaster.CheckCurrent(current);
            if(row.SourceFrame!=receipt.Frame||receipt.Coordinates!=RoiDeclarations.ImageFrame)throw new ArgumentException("区域图形与实际帧或坐标域不匹配。");
            RoiGeometryEditing.Validate(row.Shape);
            if(!double.IsFinite(row.Width)||row.Width<=0||!double.IsFinite(row.Opacity)||row.Opacity is <0 or >1)throw new ArgumentException("区域样式无效。");
            var shape=row.Shape;var kind=shape.Get<string>("kind");
            if(kind is "annulus" or "sector")
            {
                var center=RoiGeometryEditing.Center(shape);var start=shape.Get<double>("start-radians");var end=shape.Get<double>("end-radians");var sweep=end-start;
                items.Add(new Annular(new(center.X,center.Y),shape.Get<double>("inner-radius"),shape.Get<double>("outer-radius"),
                    (sweep<0?end:start)*180/Math.PI,Math.Abs(Math.Abs(sweep)-2*Math.PI)<=1e-9?360:Math.Abs(sweep)*180/Math.PI,
                    row.Stroke.ToString(),row.Fill.ToString(),row.Width,row.Opacity,row.IncludeMeasurements));
            }
            else
            {
                GeometryValue geometry=kind switch
                {
                    "rectangle"=>new OrientedRectangleGeometry(receipt.Coordinates,RoiGeometryEditing.Center(shape),shape.Get<double>("width"),shape.Get<double>("height"),shape.Get<double>("angle-degrees")*Math.PI/180),
                    "polygon"=>new PolygonGeometry(receipt.Coordinates,RoiGeometryEditing.Vertices(shape)),
                    _=>throw new ArgumentException("结果区域不支持该名义形状。")
                };
                var prepared=NativeShapeProjection.Geometry(receipt,new[]{new NativeGeometryOverlay(row.SourceFrame,geometry,row.Stroke,row.Width,row.Opacity,row.Fill,IncludeMeasurements:row.IncludeMeasurements)},maximum,maximumVertices,current);
                vertices=checked(vertices+prepared.Vertices);items.AddRange(prepared.Items);
            }
        }
        NativeResultRaster.CheckCurrent(current);return new(items.ToArray(),items.Count,vertices);
    }
    private sealed record Annular(Point CenterPoint,double InnerRadius,double OuterRadius,double StartAngle,double AngleRange,
        string Stroke,string Fill,double StrokeThickness,double Opacity,bool IncludeMeasurements):IAnnularSector
    {
        public string ToolTip=>IncludeMeasurements
            ? $"CenterX:{CenterPoint.X} CenterY:{CenterPoint.Y} OuterRadius:{OuterRadius} InnerRadius:{InnerRadius} StartAngle:{StartAngle} AngleRange:{AngleRange}" : "";
    }
}
