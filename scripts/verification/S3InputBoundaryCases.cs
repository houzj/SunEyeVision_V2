using System.IO;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Contracts;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try
        {
            var root=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();manager.LoadPlugins(Path.Combine(root,"plugins"));
            manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(x=>x.Id));
            var tool=ToolRegistry.CreateToolInstance("BlobAnalysis")!;
            using var image=HalImage.CreateFromBytes(Enumerable.Repeat((byte)200,16*16).ToArray(),16,16,1);
            // A bounded declared-input fixture, not a fake registered tool or a native algorithm mock.
            var inputs=new Inputs(image);inputs.SetSuccess();
            ImageFrameProvenance.RecordResult(inputs,null,ImageFramePolicy.NewFrame);
            var frame=ImageFrameProvenance.CaptureInput(image);
            ImageFrameProvenance.RecordResult(inputs,frame,ImageFramePolicy.PreserveCoordinates);
            ToolParameters Parameters(bool correction=true,string path="Matrices[0]")
            {
                var p=ToolRegistry.CreateParameters("BlobAnalysis");
                Set(p,"Threshold",100);Set(p,"IsAreaFilterEnabled",false);Set(p,"IsCircularityFilterEnabled",false);
                Set(p,"IsRectangularityFilterEnabled",false);Set(p,"IsBlobCountFilterEnabled",false);Set(p,"OutputContours",false);
                Set(p,"IsPositionCorrectionEnabled",correction);
                Set(p,"PositionCorrectionSource",new ImageSourceReference("fixture",path,"fixture",path));
                ((RoiCollection)Get(p,"InspectionRois")!).AddInput(new Roi{LocalGeometry=new RoiShape{Geometry=new Circle2d(4,4,1)}});
                return p;
            }
            ToolResults Run(ToolParameters p,OutputValueRequest[]? requests=null)=>new ToolExecutor(new ParameterResolver(),new CheckLogger()).Run(tool,
                new ToolExecutionRequest(p){NodeId="blob",ResolvedInputImage=image,NodeResults=new Dictionary<string,ToolResults>{{"fixture",inputs}},OutputRequests=requests??Array.Empty<OutputValueRequest>()});
            double[] X(ToolResults result)=>(double[])Get(result,"BlobCentroidXs")!;
            double[] Y(ToolResults result)=>(double[])Get(result,"BlobCentroidYs")!;
            inputs.Matrices=new[]{new AffineMatrix(1,0,0,1,3,1)};
            double rawX,rawY;
            using(var raw=new HRegion(4.0,4.0,1.0))
            {
                HOperatorSet.AreaCenter(raw,out var area,out var row,out var col);
                using(area)using(row)using(col){rawX=col.D;rawY=row.D;Console.WriteLine($"原生栅格化基准：面积={area.D} X={rawX} Y={rawY}");}
            }
            var translated=Run(Parameters());
            try{Require(translated.IsSuccess&&X(translated).Single()==rawX+3&&Y(translated).Single()==rawY+1,"SDK本地ROI平移坐标不符："+translated.ErrorMessage+" X="+string.Join(",",X(translated))+" Y="+string.Join(",",Y(translated)));}
            finally{translated.Resources?.Dispose();}
            inputs.Matrices=new[]{new AffineMatrix(0,1,-1,0,12,0)};
            var rotated=Run(Parameters());
            try{Require(rotated.IsSuccess&&X(rotated).Single()==12-rawY&&Y(rotated).Single()==rawX,"SDK本地ROI旋转的行列/像素中心不符："+rotated.ErrorMessage);}
            finally{rotated.Resources?.Dispose();}
            Console.WriteLine("通过：正式SDK执行器与真实HALCON的本地ROI平移/90度旋转，采用唯一中立仿射转换");
            var inherited=Parameters();((RoiCollection)Get(inherited,"InspectionRois")!).Rois.Clear();
            Set(inherited,"InspectionMode",SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedGeometry);
            inputs.Shapes=new[]{new RoiShape{Geometry=new Circle2d(4,4,1)}};
            ((RoiCollection)Get(inherited,"InspectionRois")!).AddInput(new Roi{Source=RoiGeometrySource.WholeRegionSub,
                Binding=new ParamSetting{BindingType=BindingType.Binding,SourceNodeId="fixture",SourceProperty="Shapes"}});
            var alreadyCurrent=Run(inherited);
            try{Require(alreadyCurrent.IsSuccess&&X(alreadyCurrent).Single()==rawX&&Y(alreadyCurrent).Single()==rawY,"继承几何被位置修正二次变换："+alreadyCurrent.ErrorMessage);}
            finally{alreadyCurrent.Resources?.Dispose();}
            Console.WriteLine("通过：已在当前图像坐标中的继承几何不会二次位置修正");
            void Rejected(ToolParameters p,string code,OutputValueRequest[]? requests=null)
            {
                var result=Run(p,requests);try{Require(!result.IsSuccess&&result.ErrorMessage?.Contains(code)==true,"应拒绝 "+code+"，实际 "+result.ErrorMessage);}finally{result.Resources?.Dispose();}
            }
            Rejected(Parameters(path:"Matrices"),"BLOB_POSE_SELECTION_REQUIRED");
            var disabled=Run(Parameters(false,"Missing"));
            try{Require(disabled.IsSuccess&&X(disabled).Single()==rawX,"禁用位置修正仍消费失效来源");}finally{disabled.Resources?.Dispose();}
            ImageFrameProvenance.RecordResult(inputs,Guid.NewGuid());
            Rejected(Parameters(),"不属于当前输入图像");
            ImageFrameProvenance.RecordResult(inputs,null);
            Rejected(Parameters(),"BLOB_POSE_FRAME_UNVERIFIED");
            Console.WriteLine("通过：矩阵数组须显式选择，禁用不消费；来源错帧和未证同帧均拒绝");
            ImageFrameProvenance.RecordResult(inputs,frame);
            inputs.Shapes=null;
            Rejected(inherited,"BLOB_ROI_SOURCE_INVALID");
            var missing=Parameters(false);((RoiCollection)Get(missing,"InspectionRois")!).Rois.Clear();
            Set(missing,"InspectionMode",SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedGeometry);
            ((RoiCollection)Get(missing,"InspectionRois")!).AddInput(new Roi{Source=RoiGeometrySource.WholeRegionSub,
                Binding=new ParamSetting{BindingType=BindingType.Binding,SourceNodeId="missing",SourceProperty="Shapes"}});
            Rejected(missing,"BLOB_ROI_SOURCE_INVALID");
            var composed=Parameters(false);((RoiCollection)Get(composed,"InspectionRois")!).Rois.Clear();
            Set(composed,"InspectionMode",SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedGeometry);
            ((RoiCollection)Get(composed,"InspectionRois")!).AddInput(new Roi{Source=RoiGeometrySource.GeometricSub,ComposeShapeType=ShapeType.Circle,
                ParameterBindings=new(){["Radius"]=new ParamSetting{BindingType=BindingType.Binding,SourceNodeId="missing",SourceProperty="Radius"}}});
            Rejected(composed,"BLOB_ROI_SOURCE_INVALID");
            Rejected(Parameters(false),"Unknown requested output:",new[]{new OutputValueRequest("unknown-output")});
            Rejected(Parameters(false),"not a record member",new[]{new OutputValueRequest(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSchema.Field(1),new[]{"unknown"})});
            Console.WriteLine("通过：null几何、缺失来源和拼装参数失效均拒绝，不退回空值或模板默认");
            Console.WriteLine("S3 SDK输入边界：4组通过。输入使用明确的测试声明与帧标记，不冒称VM取证或完整窗口验收。");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name)!.SetValue(target,value);
    private static object? Get(object target,string name)=>target.GetType().GetProperty(name)!.GetValue(target);
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private sealed class Inputs(IVisionImage image):ToolResults
    {
        public AffineMatrix[] Matrices {get;set;}=Array.Empty<AffineMatrix>();
        public RoiShape[]? Shapes {get;set;}
        public override Dictionary<string,IVisionImage> GetDisplayImages()=>new(){{"Image",image}};
        public override IReadOnlyList<OutputVariable> GetOutputVariables()=>new OutputVariable[]{
            new(){Name="Matrices",DisplayName="矩阵",DeclaredType=typeof(AffineMatrix[]),IsCollection=true,RuntimeValue=Matrices},
            new(){Name="Shapes",DisplayName="几何",DeclaredType=typeof(RoiShape[]),IsCollection=true,RuntimeValue=Shapes}};
    }
    private sealed class CheckLogger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){}
    }
}
