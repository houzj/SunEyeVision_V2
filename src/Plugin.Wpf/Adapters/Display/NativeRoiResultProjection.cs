using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;
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
                var center = RoiGeometryEditing.Center(shape);
                var start = shape.Get<double>("start-radians");
                var end = shape.Get<double>("end-radians");
                var sweep = end - start;
                var inner = shape.Get<double>("inner-radius");
                var outer = shape.Get<double>("outer-radius");
                var startDegrees = start * 180d / Math.PI;
                var signedRange = Math.Clamp(sweep * 180d / Math.PI, -360d, 360d);
                var tip = row.ToolTip ?? (row.IncludeMeasurements
                    ? $"CenterX:{(float)center.X} CenterY:{(float)center.Y} OuterRadius:{(float)outer} InnerRadius:{(float)inner} StartAngle:{(float)startDegrees} AngleRange:{(float)signedRange}"
                    : "");
                items.Add(new Annular(new(center.X,center.Y),inner,outer,
                    sweep < 0 ? end * 180d / Math.PI : startDegrees,
                    Math.Abs(Math.Abs(sweep) - 2 * Math.PI) <= 1e-9 ? 360 : Math.Abs(signedRange),
                    row.Stroke.ToString(),row.Fill.ToString(),row.Width,row.Opacity,tip));
            }
            else
            {
                GeometryValue geometry=kind switch
                {
                    "point"=>shape.Get<PointGeometry>("center"),
                    "rectangle"=>new OrientedRectangleGeometry(receipt.Coordinates,RoiGeometryEditing.Center(shape),shape.Get<double>("width"),shape.Get<double>("height"),shape.Get<double>("angle-degrees")*Math.PI/180),
                    "polygon"=>new PolygonGeometry(receipt.Coordinates,RoiGeometryEditing.Vertices(shape)),
                    _=>throw new ArgumentException("结果区域不支持该名义形状。")
                };
                var prepared=NativeShapeProjection.Geometry(receipt,new[]{new NativeGeometryOverlay(row.SourceFrame,geometry,row.Stroke,row.Width,row.Opacity,row.Fill,
                    IncludeMeasurements:row.IncludeMeasurements,ToolTip:row.ToolTip)},maximum,maximumVertices,current);
                vertices=checked(vertices+prepared.Vertices);items.AddRange(prepared.Items);
            }
        }
        NativeResultRaster.CheckCurrent(current);return new(items.ToArray(),items.Count,vertices);
    }
    private sealed record Annular(Point CenterPoint,double InnerRadius,double OuterRadius,double StartAngle,double AngleRange,
        string Stroke,string Fill,double StrokeThickness,double Opacity,string ToolTip):IAnnularSector;
}
