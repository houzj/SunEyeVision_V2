using System.IO;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;

// Release regression for the strict composition contract.
// Pure composer checks only: these do not claim typed workflow resolution or UI acceptance.
internal static class S2DocumentEditingCases
{
    private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    private static Dictionary<string,object?> Circle()=>new(){{"CenterX",12.125},{"CenterY",23.25},{"Radius",4.5}};
    private static void Reject(Action action,string prefix)
    {
        try{action();}catch(ArgumentException error){Require(error.Message.StartsWith(prefix,StringComparison.Ordinal),"错误分类不符: "+error);return;}
        throw new InvalidOperationException("缺少预期拒绝: "+prefix);
    }
    private static int Main()
    {
        var results=new List<object>();var failures=0;
        void Check(string name,Action action)
        {try{action();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception error){failures++;results.Add(new{name,passed=false,error=error.ToString()});Console.WriteLine("FAIL "+name+" "+error);}}
        Check("scalar rectangle/circle retain exact values",()=>{
            var circle=(Circle2d)RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,Circle()).Single()!;
            Require(circle.Radius==4.5,"圆半径丢失");
            var rectangle=(RotatedRect2d)RoiComposeResolver.ComposeAllByTemplate(ShapeType.RotatedRectangle,new Dictionary<string,object?>{{"CenterX",1.25},{"CenterY",2.5},{"Width",3.125},{"Height",4.25},{"Angle",.125}}).Single()!;
            Require(rectangle.Width==3.125&&rectangle.Height==4.25&&rectangle.Angle==.125,"矩形精度/角度被取整");
        });
        Check("only scalar broadcasts; equal arrays preserve order",()=>{
            var values=Circle();values["CenterX"]=new[]{2d,9d};values["Radius"]=new[]{3d,7d};
            var geometries=RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,values).Cast<Circle2d>().ToArray();
            Require(geometries.Length==2&&geometries[0].Radius==3&&geometries[1].Radius==7,"向量长度/顺序错误");
        });
        Check("one-element arrays do not broadcast",()=>{
            var values=Circle();values["CenterX"]=new[]{2d,9d};values["Radius"]=new[]{3d};
            Reject(()=>RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,values),"ROI_COMPOSE_COUNT");
        });
        Check("empty valid vectors differ from missing and mismatched",()=>{
            var empty=Circle();empty["CenterX"]=Array.Empty<double>();Require(RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,empty).Count==0,"空成功集合造出默认几何");
            empty["Radius"]=new[]{4d};Reject(()=>RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,empty),"ROI_COMPOSE_COUNT");
            var missing=Circle();missing.Remove("Radius");Reject(()=>RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,missing),"ROI_COMPOSE_REQUIRED");
        });
        Check("null text NaN and infinity rejected",()=>{
            foreach(var value in new object?[]{null,"4.5",double.NaN,double.PositiveInfinity}){
                var values=Circle();values["Radius"]=value;
                Reject(()=>RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,values),value is null?"ROI_COMPOSE_REQUIRED":value is string?"ROI_COMPOSE_TYPE":"ROI_COMPOSE_FINITE");
            }
        });
        Check("nonpositive sizes and malformed array ranks rejected",()=>{
            foreach(var value in new[]{0d,-1d}){var values=Circle();values["Radius"]=value;Reject(()=>RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,values),"ROI_COMPOSE_SIZE");}
            var rank=Circle();rank["CenterX"]=new double[1,1];Reject(()=>RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,rank),"ROI_COMPOSE_ARRAY");
            rank["CenterX"]=Array.CreateInstance(typeof(double),new[]{1},new[]{1});Reject(()=>RoiComposeResolver.ComposeAllByTemplate(ShapeType.Circle,rank),"ROI_COMPOSE_ARRAY");
        });
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"roi-composition-independent.json"),JsonSerializer.Serialize(new{scope="pure-composer-only",passed=results.Count-failures,failed=failures,results},new JsonSerializerOptions{WriteIndented=true}));
        return failures==0?0:1;
    }
}
