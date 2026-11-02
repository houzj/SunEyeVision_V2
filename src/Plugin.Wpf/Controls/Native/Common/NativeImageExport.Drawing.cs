using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using CvPoint = OpenCvSharp.Point;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.Plugin.Wpf.Native.Common;

// Static drawing sequence from ImageSaveOpenCvHelper (954980DB…50257EE).
// OpenCvSharp 4.13 and checked buffer/codec/atomic-file boundaries are host adaptations;
// this does not assert equivalence to the vendor's native build or PutTextZh ABI.
internal static partial class NativeImageExport
{
    private static Mat ToMat(BitmapSource bitmap)
    {
        if (bitmap.Format == PixelFormats.Pbgra32)
            bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var format = bitmap.Format;
        MatType type;
        if (format == PixelFormats.Gray8) type = MatType.CV_8UC1;
        else if (format == PixelFormats.Gray16) type = MatType.CV_16UC1;
        else if (format == PixelFormats.Bgr24 || format == PixelFormats.Rgb24) type = MatType.CV_8UC3;
        else if (format == PixelFormats.Bgra32) type = MatType.CV_8UC4;
        else if (format == PixelFormats.Rgb48) type = MatType.CV_16UC3;
        else throw new NotSupportedException($"导出未支持像素格式 {format}，不会静默转换位深或透明度。");
        var stride = checked((bitmap.PixelWidth * format.BitsPerPixel + 7) / 8);
        var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        var mat = new Mat(bitmap.PixelHeight, bitmap.PixelWidth, type);
        try
        {
            Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
            if (format == PixelFormats.Rgb24 || format == PixelFormats.Rgb48)
                Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2BGR);
            return mat;
        }
        catch { mat.Dispose(); throw; }
    }

    private static BitmapSource FromMat(Mat image)
    {
        using var copy = image.Clone();
        PixelFormat format;
        if (copy.Type() == MatType.CV_8UC1) format = PixelFormats.Gray8;
        else if (copy.Type() == MatType.CV_16UC1) format = PixelFormats.Gray16;
        else if (copy.Type() == MatType.CV_8UC3) format = PixelFormats.Bgr24;
        else if (copy.Type() == MatType.CV_8UC4) format = PixelFormats.Bgra32;
        else if (copy.Type() == MatType.CV_16UC3)
        { Cv2.CvtColor(copy, copy, ColorConversionCodes.BGR2RGB); format = PixelFormats.Rgb48; }
        else throw new NotSupportedException("未支持的导出矩阵格式。");
        var stride = checked(copy.Width * format.BitsPerPixel / 8);
        var pixels = new byte[checked(stride * copy.Height)];
        Marshal.Copy(copy.Data, pixels, 0, pixels.Length);
        var bitmap = BitmapSource.Create(copy.Width, copy.Height, 96, 96, format, null, pixels, stride);
        bitmap.Freeze(); return bitmap;
    }

    private static void Number(double value)
    {
        // Keep OpenCV integer geometry and native allocations within a safe range.
        if (!double.IsFinite(value) || value < -100000000 || value > 100000000)
            throw new ArgumentException("导出图形坐标或尺寸无效。");
    }
    private static void Position(WpfPoint point) { Number(point.X); Number(point.Y); }
    private static CvPoint Point(WpfPoint point) { Position(point); return new CvPoint((int)point.X, (int)point.Y); }
    private static int Thickness(double thickness) { Number(thickness); return Math.Max(2, (int)thickness); }
    private static Scalar Color(string text)
    {
        var color = string.IsNullOrEmpty(text) ? System.Drawing.Color.Empty : System.Drawing.ColorTranslator.FromHtml(text);
        return new Scalar(color.B, color.G, color.R, 255);
    }
    private static void Line(Mat image, CvPoint a, CvPoint b, string stroke, double thickness) =>
        Cv2.Line(image, a, b, Color(stroke), Thickness(thickness), LineTypes.Link8);
    private static void Arrow(Mat image, WpfPoint a, WpfPoint b, string stroke, double thickness)
    {
        Position(a); Position(b);
        Cv2.ArrowedLine(image, new CvPoint((int)a.X, (int)a.Y), new CvPoint((int)b.X, (int)b.Y),
            Color(stroke), Thickness(thickness), LineTypes.Link8, 0, .1);
    }

    // Text guards share the actual drawing dispatch, so opacity, tuple and overload
    // exclusions cannot drift into a second, weaker preflight implementation.
    private static void GuardNativeText(string? content)
    {
        if (content is null) throw new ArgumentException("导出文字内容缺失。");
        if (content == (AppLocalizationService.GetLang("Lang_ModuleErr") ?? "模块执行异常") || string.IsNullOrEmpty(content)) return;
        throw new NotSupportedException("文字导出未实现：原PutTextZh原生字体接口尚未闭合；请关闭可见文字后导出图形。");
    }
    private static void DrawNativeShape(Mat image, IShape shape)
    {
        Number(shape.Opacity);
        if (shape.Opacity == 0 && shape is not IMultiPoint) return;
        if (shape is IMultiText texts)
        {
            foreach (var group in texts.Texts.GroupBy(text => text.Position))
                foreach (var text in group)
                {
                    Position(text.Position);
                    if (text.Position.X > image.Width || text.Position.Y > image.Height) continue;
                    GuardNativeText(text.Content);
                }
            return;
        }
        if (shape is ICircle circle)
        {
            Position(circle.CenterPoint); Number(circle.Radius);
            if (circle.Radius < 0) throw new ArgumentException("圆半径无效。");
            Cv2.Circle(image, new CvPoint((int)circle.CenterPoint.X, (int)circle.CenterPoint.Y), (int)circle.Radius,
                Color(circle.Stroke), Thickness(circle.StrokeThickness), LineTypes.Link8);
        }
        else if (shape is IEllipse ellipse)
        {
            Position(ellipse.CenterPoint); Number(ellipse.MajorRadius); Number(ellipse.MinorRadius); Number(ellipse.Angle);
            if (ellipse.MajorRadius < 0 || ellipse.MinorRadius < 0) throw new ArgumentException("椭圆半径无效。");
            Cv2.Ellipse(image, Point(ellipse.CenterPoint), new OpenCvSharp.Size((int)ellipse.MajorRadius, (int)ellipse.MinorRadius),
                ellipse.Angle, 0, 360, Color(ellipse.Stroke), Thickness(ellipse.StrokeThickness), LineTypes.Link8);
        }
        else if (shape is IAnnularSector arc) DrawNativeArc(image, arc);
        else if (shape is ILine line)
        {
            Position(line.StartPoint); Position(line.EndPoint);
            if (!line.IsThroughLine) Line(image, Point(line.StartPoint), Point(line.EndPoint), line.Stroke, line.StrokeThickness);
            else if (BorderPoints(line.StartPoint, line.EndPoint, image.Width, image.Height, out var a, out var b))
                Line(image, Point(a), Point(b), line.Stroke, line.StrokeThickness);
        }
        else if (shape is IRectangle rectangle) DrawNativeRectangle(image, rectangle);
        else if (shape is IPoint point)
        {
            Position(point.Point); var width = Thickness(point.StrokeThickness); var half = 3.0 * width;
            var x = (float)point.Point.X; var y = (float)point.Point.Y;
            Cv2.Line(image, new CvPoint((int)x, (int)(y-half)), new CvPoint((int)x, (int)(y+half)), Color(point.Stroke), width, LineTypes.AntiAlias);
            Cv2.Line(image, new CvPoint((int)(x-half), (int)y), new CvPoint((int)(x+half), (int)y), Color(point.Stroke), width, LineTypes.AntiAlias);
        }
        else if (shape is ICoordinateShape coordinate)
        {
            Position(coordinate.CenterPoint); Number(coordinate.VectorX.X); Number(coordinate.VectorX.Y);
            Number(coordinate.VectorY.X); Number(coordinate.VectorY.Y);
            static bool HasAxis(WpfPoint direction)
            {
                var vector = new System.Windows.Vector((float)direction.X,(float)direction.Y);
                vector.Normalize();
                return !double.IsNaN(vector.X) && !double.IsNaN(vector.Y);
            }
            if (HasAxis(coordinate.VectorX) || HasAxis(coordinate.VectorY)) GuardNativeText("X");
        }
        else if (shape is IArrowPath path)
        {
            for (int index=0; index+1 < path.Points.Count; index++) Arrow(image, path.Points[index], path.Points[index+1], path.Stroke, path.StrokeThickness);
        }
        else if (shape is IAngleShape angle)
        {
            // This branch calls the string overload even when Intersection is offscreen.
            GuardNativeText(angle.Angle.ToString("N1") + "°");
        }
        else if (shape is IPolygon polygon)
        {
            if (polygon.Points.Count <= 2) return;
            var points = polygon.Points.Select(Point).ToArray();
            Cv2.Polylines(image, new[]{points}, true, Color(polygon.Stroke), Thickness(polygon.StrokeThickness), LineTypes.Link8);
            if (!string.IsNullOrEmpty(polygon.Fill) && System.Drawing.ColorTranslator.FromHtml(polygon.Fill).A > 0)
                Cv2.FillPoly(image, new[]{points}, Color(polygon.Fill), LineTypes.Link8);
        }
        else if (shape is IPolyline polyline)
        {
            if (polyline.PolylinePointList.Count > 1)
                Cv2.Polylines(image, new[]{polyline.PolylinePointList.Select(Point).ToArray()}, false,
                    Color(polyline.Stroke), Thickness(polyline.StrokeThickness), LineTypes.Link8);
        }
        else if (shape is ICross cross)
        {
            Position(cross.CenterPoint);
            Line(image, new CvPoint(0,(int)cross.CenterPoint.Y), new CvPoint(image.Width,(int)cross.CenterPoint.Y), cross.Stroke,cross.StrokeThickness);
            Line(image, new CvPoint((int)cross.CenterPoint.X,0), new CvPoint((int)cross.CenterPoint.X,image.Height), cross.Stroke,cross.StrokeThickness);
        }
        else if (shape is IMultiPoint multi)
        {
            if (multi.MicroPoints != null)
                foreach (var micro in multi.MicroPoints)
                    if (micro.Opacity != 0) { Position(micro.Point); Cv2.Circle(image,new CvPoint((int)micro.Point.X,(int)micro.Point.Y),1,Color(micro.Stroke),1,LineTypes.Link8); }
            if (multi.OutlinePoints != null)
                foreach (var outline in multi.OutlinePoints)
                    if (outline.Opacity != 0 && outline.OutlineData != null) DrawNativeOutline(image,outline);
            // The original multi-point rectangle branch does not recheck nested opacity.
            if (multi.Rectangles != null) foreach (var memberRectangle in multi.Rectangles) DrawNativeRectangle(image,memberRectangle);
        }
        // An unsupported interface is ignored by the original dispatch (including standalone IText).
    }
    private static void DrawNativeRectangle(Mat image, IRectangle rectangle)
    {
        Position(rectangle.CenterPoint); Number(rectangle.Width); Number(rectangle.Height); Number(rectangle.Angle);
        if (rectangle.Width < 0 || rectangle.Height < 0) throw new ArgumentException("矩形宽高无效。");
        // Original 1.0 CIL: Size2f(double,double) truncates via int before float.
        // Points is managed; preserve single precision and operation order locally.
        float cx=(float)rectangle.CenterPoint.X, cy=(float)rectangle.CenterPoint.Y;
        float width=(float)(int)rectangle.Width, height=(float)(int)rectangle.Height;
        double radians=(float)rectangle.Angle*Math.PI/180.0;
        float a=(float)Math.Cos(radians)*0.5f, b=(float)Math.Sin(radians)*0.5f;
        var corners=new[]{new Point2f(cx-b*height-a*width,cy+a*height-b*width),
            new Point2f(cx+b*height-a*width,cy-a*height-b*width),new Point2f(),new Point2f()};
        corners[2]=new Point2f(2*cx-corners[0].X,2*cy-corners[0].Y);
        corners[3]=new Point2f(2*cx-corners[1].X,2*cy-corners[1].Y);
        for(int i=0;i<4;i++) Line(image,new CvPoint((int)corners[i].X,(int)corners[i].Y),
            new CvPoint((int)corners[(i+1)%4].X,(int)corners[(i+1)%4].Y),rectangle.Stroke,rectangle.StrokeThickness);
    }
    private static void DrawNativeArc(Mat image,IAnnularSector arc)
    {
        Position(arc.CenterPoint); Number(arc.InnerRadius); Number(arc.OuterRadius); Number(arc.StartAngle); Number(arc.AngleRange);
        if(arc.InnerRadius<0 || arc.OuterRadius<0) throw new ArgumentException("扇环半径无效。");
        var thickness=Thickness(arc.StrokeThickness);var color=Color(arc.Stroke);
        if(arc.AngleRange==360)
        {
            Cv2.Circle(image,new CvPoint((int)arc.CenterPoint.X,(int)arc.CenterPoint.Y),(int)arc.InnerRadius,color,thickness,LineTypes.Link8);
            Cv2.Circle(image,new CvPoint((int)arc.CenterPoint.X,(int)arc.CenterPoint.Y),(int)arc.OuterRadius,color,thickness,LineTypes.Link8);return;
        }
        WpfPoint Rotate(double radius,double angle)
        {
            var transform=new RotateTransform(angle,arc.CenterPoint.X,arc.CenterPoint.Y);
            return transform.Transform(new WpfPoint(arc.CenterPoint.X+radius,arc.CenterPoint.Y));
        }
        Line(image,Point(Rotate(arc.InnerRadius,arc.StartAngle)),Point(Rotate(arc.OuterRadius,arc.StartAngle)),arc.Stroke,thickness);
        Line(image,Point(Rotate(arc.InnerRadius,arc.StartAngle+arc.AngleRange)),Point(Rotate(arc.OuterRadius,arc.StartAngle+arc.AngleRange)),arc.Stroke,thickness);
        Cv2.Ellipse(image,Point(arc.CenterPoint),new OpenCvSharp.Size((int)arc.InnerRadius,(int)arc.InnerRadius),0,arc.StartAngle,arc.StartAngle+arc.AngleRange,color,thickness,LineTypes.Link8);
        Cv2.Ellipse(image,Point(arc.CenterPoint),new OpenCvSharp.Size((int)arc.OuterRadius,(int)arc.OuterRadius),0,arc.StartAngle,arc.StartAngle+arc.AngleRange,color,thickness,LineTypes.Link8);
    }
    private static void DrawNativeOutline(Mat image,IOutlinePoint outline)
    {
        // VM struct: float X, float Y, float Score, int Index = 16 bytes. Index is not consumed.
        var data=outline.OutlineData;
        for(int offset=0;offset+16<=data.Length;offset+=16)
        {
            var x=BitConverter.ToSingle(data,offset);var y=BitConverter.ToSingle(data,offset+4);var score=BitConverter.ToSingle(data,offset+8);
            Number(x);Number(y);Number(score);
            var ix=(int)x;var iy=(int)y;
            if(ix<0 || ix>image.Width || iy<0 || iy>image.Height)continue;
            var color=(int)score switch {0=>Color(outline.Stroke),1=>new Scalar(0,255,255,255),_=>new Scalar(0,0,255,255)};
            Cv2.Circle(image,new CvPoint(ix,iy),1,color,1,LineTypes.Link8);
        }
    }
    private static bool BorderPoints(WpfPoint a,WpfPoint b,int width,int height,out WpfPoint first,out WpfPoint second)
    {
        first=default;second=default;
        if(a==b)return false;
        if(a.X==b.X){first=new WpfPoint(a.X,0);second=new WpfPoint(a.X,height);return true;}
        if(a.Y==b.Y){first=new WpfPoint(0,a.Y);second=new WpfPoint(width,a.Y);return true;}
        var slope=(a.Y-b.Y)/(a.X-b.X);var intercept=b.Y-slope*b.X;
        var points=new[]{new WpfPoint(0,(int)intercept),new WpfPoint(width,(int)(slope*width+intercept)),
            new WpfPoint((int)(-intercept/slope),0),new WpfPoint((int)((height-intercept)/slope),height)};
        int count=0;
        foreach(var point in points)
            if(point.X>=0 && point.X<=width && point.Y>=0 && point.Y<=height)
            { if(count==0){first=point;count++;}else second=point; }
        return true;
    }
}
