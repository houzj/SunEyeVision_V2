using HalconDotNet;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>One invocation-local rasterization of the persistent input-slot geometry. No resource escapes.</summary>
internal static class BlobLocalRoiRasterizer
{
    internal static HRegion Build(RecordValue definition,string group,HRegion inputDomain,CancellationToken cancellation)
    {
        BlobLocalRoi.Validate(definition);
        var coverage=BlobLocalRoi.Coverage(definition,group);
        if(coverage==BlobLocalRoi.FullDomain)return inputDomain.Clone();
        HOperatorSet.GenEmptyRegion(out var empty);
        var union=new HRegion(empty);empty.Dispose();
        try
        {
            if(coverage==BlobLocalRoi.None)return union.Clone();
            foreach(var item in BlobLocalRoi.Items(definition,group))
            {
                cancellation.ThrowIfCancellationRequested();if(!item.Get<bool>("enabled"))continue;
                using var region=Shape(item.Get<RecordValue>("shape"));using var next=union.Union2(region);
                var replacement=next.Clone();union.Dispose();union=replacement;
            }
            return union.Intersection(inputDomain);
        }
        finally{union.Dispose();}
    }
    private static HRegion Shape(RecordValue shape)
    {
        var kind=shape.Get<string>("kind");
        if(kind=="polygon")
        {
            var points=shape.Get<SequenceValue>("vertices").Cast<RecordValue>().ToArray();
            using var rows=new HTuple(points.Select(p=>p.Get<double>("y")).ToArray());using var cols=new HTuple(points.Select(p=>p.Get<double>("x")).ToArray());
            HOperatorSet.GenRegionPolygonFilled(out var polygon,rows,cols);using(polygon)return new HRegion(polygon);
        }
        var center=shape.Get<RecordValue>("center");var y=center.Get<double>("y");var x=center.Get<double>("x");
        if(kind=="rectangle")
        {
            HOperatorSet.GenRectangle2(out var rectangle,y,x,-BlobLocalRoi.NormalizeDegrees(shape.Get<double>("angle-degrees"))*Math.PI/180,shape.Get<double>("width")/2,shape.Get<double>("height")/2);
            using(rectangle)return new HRegion(rectangle);
        }
        var start=shape.Get<double>("start-radians");var end=shape.Get<double>("end-radians");var sweep=end-start;
        // A negative screen sweep occupies [end,start]. HALCON's positive mathematical interval reverses screen Y.
        if(sweep<0)(start,end)=(end,start);
        using var outer=Math.Abs(Math.Abs(sweep)-2*Math.PI)<=1e-9
            ?new HRegion(y,x,shape.Get<double>("outer-radius"))
            :new HRegion(y,x,shape.Get<double>("outer-radius"),Normalize(-end),Normalize(-start));
        var innerRadius=shape.Get<double>("inner-radius");
        if(innerRadius==0)return outer.Clone();
        using var inner=new HRegion(y,x,innerRadius);return outer.Difference(inner);
    }
    private static double Normalize(double value)=>(value%(2*Math.PI)+2*Math.PI)%(2*Math.PI);
}
