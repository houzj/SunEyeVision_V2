using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Regions;

/// <summary>UI geometry edits use the existing neutral ROI record; this is not a second runtime geometry store.</summary>
public static class RoiGeometryEditing
{
    public static IReadOnlyList<string> Kinds {get;}=Array.AsReadOnly(new[]{"point","line","circle","rectangle","arc","annulus","polygon"});
    public static string Label(string kind)=>kind switch {"point"=>"点","line"=>"直线","circle"=>"圆","rectangle"=>"旋转矩形","arc"=>"圆弧","annulus"=>"圆环","polygon"=>"多边形",_=>kind};
    public static Dictionary<string,object?> Values(RecordValue shape)=>shape.Declaration.Fields.Where(field=>shape.Contains(field.Id)).ToDictionary(field=>field.Id,field=>shape.Get<object?>(field.Id));
    public static Point2 Center(RecordValue shape)
    {
        if(shape.Get<string>("kind")=="polygon")
        {var points=Vertices(shape);return new(points.Average(p=>p.X),points.Average(p=>p.Y));}
        return shape.Get<PointGeometry>("center").Point;
    }
    public static Point2[] Vertices(RecordValue shape)=>shape.Get<SequenceValue>("vertices").Cast<PointGeometry>().Select(point=>point.Point).ToArray();
    public static PointGeometry Point(Point2 point)=>new(RoiDeclarations.ImageFrame,point);
    public static double ArcSweep(RecordValue shape)
    {
        var delta=shape.Get<double>("end-radians")-shape.Get<double>("start-radians");
        delta=((delta+Math.PI)%(2*Math.PI)+2*Math.PI)%(2*Math.PI)-Math.PI;
        if(Math.Abs(delta)>=Math.PI-1e-9||!shape.Contains("major-arc")||!shape.Get<bool>("major-arc"))return delta;
        return (delta>=0?-1:1)*(2*Math.PI-Math.Abs(delta));
    }
    public static RecordValue Create(string kind,Point2 start,Point2 end,IReadOnlyList<Point2>? polygon=null)
    {
        var values=new Dictionary<string,object?>{{"kind",kind},{"center",Point(start)}};
        var dx=end.X-start.X;var dy=end.Y-start.Y;var distance=Math.Sqrt(dx*dx+dy*dy);
        switch(kind)
        {
            case "point":break;
            case "line":values["direction-x"]=dx/distance;values["direction-y"]=dy/distance;break;
            case "circle":values["radius"]=distance;break;
            case "rectangle":values["center"]=Point(new((start.X+end.X)/2,(start.Y+end.Y)/2));values["width"]=Math.Abs(dx);values["height"]=Math.Abs(dy);values["angle-degrees"]=0d;break;
            case "arc":values["radius"]=distance;values["start-radians"]=0d;values["end-radians"]=Math.PI;values["major-arc"]=false;break;
            case "annulus":values["inner-radius"]=distance/2;values["outer-radius"]=distance;values["start-radians"]=0d;values["end-radians"]=2*Math.PI;break;
            case "polygon":values.Remove("center");values["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(field=>field.Id=="vertices").Type,
                (polygon??Array.Empty<Point2>()).Select(point=>(object?)Point(point)));values["closed"]=true;break;
            default:throw new ArgumentException("未知区域形状。");
        }
        return Checked(values);
    }
    public static RecordValue Checked(IReadOnlyDictionary<string,object?> values)
    {
        var shape=new RecordValue(RoiDeclarations.Shape,values);Validate(shape);return shape;
    }
    public static void Validate(RecordValue shape)
    {
        if(shape.Declaration!=RoiDeclarations.Shape)throw new ArgumentException("区域值必须符合既有ROI声明。");
        var kind=shape.Get<string>("kind");if(!Kinds.Contains(kind))throw new ArgumentException("请选择已声明的区域形状。");
        double Number(string key)=>shape.Get<double>(key);
        void Positive(string key){if(!double.IsFinite(Number(key))||Number(key)<=0)throw new ArgumentException("尺寸必须为正数："+key);}
        if(kind=="polygon")
        {
            var points=Vertices(shape);if(points.Length<3||points.Distinct().Count()<3)throw new ArgumentException("多边形至少需要三个不同的顶点。");
            return;
        }
        _=Center(shape);
        if(kind=="line"&&Math.Abs(Number("direction-x"))+Math.Abs(Number("direction-y"))<1e-12)throw new ArgumentException("直线方向不能为零。");
        if(kind is "circle" or "arc")Positive("radius");
        if(kind=="rectangle"){Positive("width");Positive("height");}
        if(kind=="annulus")
        {Positive("outer-radius");if(Number("inner-radius")<0||Number("inner-radius")>=Number("outer-radius"))throw new ArgumentException("内半径必须不小于0且小于外半径。");}
        if(kind is "arc" or "annulus")
        {var sweep=kind=="arc"?ArcSweep(shape):Number("end-radians")-Number("start-radians");if(Math.Abs(sweep)<1e-12||Math.Abs(sweep)>2*Math.PI+1e-9)throw new ArgumentException("角度范围必须大于0且不超过整圆。");}
    }
    public static RecordValue Translate(RecordValue shape,double dx,double dy)
    {
        var values=Values(shape);
        if(shape.Get<string>("kind")=="polygon")values["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(field=>field.Id=="vertices").Type,
            Vertices(shape).Select(point=>(object?)Point(new(point.X+dx,point.Y+dy))));
        else{var point=Center(shape);values["center"]=Point(new(point.X+dx,point.Y+dy));}
        return Checked(values);
    }
    public static RecordValue Manipulate(RecordValue shape,RoiEditAction action,Point2 anchor,Point2 current)
    {
        if(action is RoiEditAction.Move or RoiEditAction.Select)return Translate(shape,current.X-anchor.X,current.Y-anchor.Y);
        var values=Values(shape);var kind=shape.Get<string>("kind");var center=Center(shape);
        var dx=current.X-center.X;var dy=current.Y-center.Y;var radius=Math.Sqrt(dx*dx+dy*dy);
        if(action==RoiEditAction.Rotate)
        {
            var delta=Math.Atan2(dy,dx)-Math.Atan2(anchor.Y-center.Y,anchor.X-center.X);
            if(kind=="rectangle")values["angle-degrees"]=shape.Get<double>("angle-degrees")+delta*180/Math.PI;
            else if(kind=="line")
            {var x=shape.Get<double>("direction-x");var y=shape.Get<double>("direction-y");values["direction-x"]=x*Math.Cos(delta)-y*Math.Sin(delta);values["direction-y"]=x*Math.Sin(delta)+y*Math.Cos(delta);}
            else if(kind is "arc" or "annulus"){values["start-radians"]=shape.Get<double>("start-radians")+delta;values["end-radians"]=shape.Get<double>("end-radians")+delta;}
            else if(kind=="polygon")values["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(field=>field.Id=="vertices").Type,
                Vertices(shape).Select(point=>(object?)Point(new(center.X+(point.X-center.X)*Math.Cos(delta)-(point.Y-center.Y)*Math.Sin(delta),center.Y+(point.X-center.X)*Math.Sin(delta)+(point.Y-center.Y)*Math.Cos(delta)))));
        }
        else if(kind is "circle" or "arc")values["radius"]=radius;
        else if(kind=="annulus")values["outer-radius"]=radius;
        else if(kind=="rectangle")
        {var angle=-shape.Get<double>("angle-degrees")*Math.PI/180;values["width"]=2*Math.Abs(dx*Math.Cos(angle)-dy*Math.Sin(angle));values["height"]=2*Math.Abs(dx*Math.Sin(angle)+dy*Math.Cos(angle));}
        else if(kind=="point")values["center"]=Point(current);
        else if(kind=="line"){values["direction-x"]=dx/radius;values["direction-y"]=dy/radius;}
        else if(kind=="polygon")
        {
            var points=Vertices(shape);var selected=Enumerable.Range(0,points.Length).MinBy(index=>Math.Pow(points[index].X-anchor.X,2)+Math.Pow(points[index].Y-anchor.Y,2));points[selected]=current;
            values["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(field=>field.Id=="vertices").Type,points.Select(point=>(object?)Point(point)));
        }
        return Checked(values);
    }
    public static IReadOnlyList<(Point2[] Points,bool Closed)> Outlines(RecordValue shape,double lineExtent=1000)
    {
        var kind=shape.Get<string>("kind");if(kind=="polygon")return new[]{(Vertices(shape),true)};
        var center=Center(shape);double N(string key)=>shape.Get<double>(key);
        if(kind=="point")return new[]{(new[]{new Point2(center.X-3,center.Y),new Point2(center.X+3,center.Y)},false),(new[]{new Point2(center.X,center.Y-3),new Point2(center.X,center.Y+3)},false)};
        if(kind=="line")return new[]{(new[]{new Point2(center.X-N("direction-x")*lineExtent,center.Y-N("direction-y")*lineExtent),new Point2(center.X+N("direction-x")*lineExtent,center.Y+N("direction-y")*lineExtent)},false)};
        if(kind=="rectangle")
        {
            var angle=N("angle-degrees")*Math.PI/180;var width=N("width")/2;var height=N("height")/2;
            return new[]{(new[]{(-width,-height),(width,-height),(width,height),(-width,height)}.Select(p=>new Point2(center.X+p.Item1*Math.Cos(angle)-p.Item2*Math.Sin(angle),center.Y+p.Item1*Math.Sin(angle)+p.Item2*Math.Cos(angle))).ToArray(),true)};
        }
        var start=kind=="circle"?0:N("start-radians");var end=kind=="circle"?2*Math.PI:kind=="arc"?start+ArcSweep(shape):N("end-radians");
        Point2[] Arc(double radius)=>Enumerable.Range(0,73).Select(i=>{var t=start+(end-start)*i/72;return new Point2(center.X+radius*Math.Cos(t),center.Y+radius*Math.Sin(t));}).ToArray();
        if(kind=="annulus")
        {var outer=Arc(N("outer-radius"));var inner=Arc(N("inner-radius")).Reverse().ToArray();return Math.Abs(end-start)>=2*Math.PI-1e-9?new[]{(outer,true),(inner,true)}:new[]{(outer.Concat(inner).ToArray(),true)};}
        return new[]{(Arc(N("radius")),kind=="circle")};
    }
}
