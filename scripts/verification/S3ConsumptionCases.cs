using System.IO;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try
        {
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();manager.LoadPlugins(Path.Combine(product,"plugins"));
            manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(t=>t.Id));
            var pixels=new byte[64];for(int y=2;y<6;y++)for(int x=2;x<6;x++)pixels[y*8+x]=200;
            using var image=HalImage.CreateFromBytes(pixels,8,8,1);
            ToolParameters Parameters()
            {
                var p=ToolRegistry.CreateParameters("BlobAnalysis");
                foreach(var pair in new Dictionary<string,object>{["Threshold"]=100,["IsAreaFilterEnabled"]=false,["IsCircularityFilterEnabled"]=false,
                    ["IsRectangularityFilterEnabled"]=false,["IsBlobCountFilterEnabled"]=false,["OutputBlobImage"]=false,["OutputBinaryImage"]=false,["OutputContours"]=false})
                    p.GetType().GetProperty(pair.Key)!.SetValue(p,pair.Value);
                return p;
            }
            WorkflowNodeBase Node(string id)=>new(id,id,id,"BlobAnalysis"){Parameters=Parameters()};
            var engine=new WorkflowEngine(new CheckLogger());var workflow=engine.CreateWorkflow("S3-consumption","真实消费需求");
            var source=Node("source");workflow.Nodes.Add(source);
            var executor=new WorkflowExecutionEngine(engine,manager,new CheckLogger());
            using(var baseline=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())
            {
                Require(baseline.Success,"无消费者基线失败");using var reader=baseline.DeclaredOutputs.Single().AcquireReader();
                Require(reader.Values!.Get<IReadOnlyImage?>(BlobSchema.Field(5)) is null&&reader.Values.Get<IReadOnlyImage?>(BlobSchema.Field(6)) is null,"无消费需求仍意外生成图像");
                var records=reader.Values.Get<SequenceValue>(BlobSchema.BlobsPort);
                Require(records.Count==1&&!((RecordValue)records[0]!).Contains("boundary"),"无需求仍生成轮廓");
            }
            Console.WriteLine("通过：无消费者时，关闭的图像/轮廓输出保持不生成");
            var catalog=new CommunicationReceiveService{VariableCatalog=()=>new[]{new ReceiveVariableSource("image-var","图像变量","","Image",true,"source","BinaryMask",workflow.Id)}};
            using(var published=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image,communicationInputs:catalog)).GetAwaiter().GetResult())
            {
                Require(published.Success,"变量生产者需求执行失败");using var reader=published.DeclaredOutputs.Single().AcquireReader();
                Require(reader.Values!.Get<IReadOnlyImage>(BlobSchema.Field(6)) is not null
                    &&reader.Values.Get<IReadOnlyImage?>(BlobSchema.Field(5)) is null,"变量输出需求未按冻结生产者目录生成，或意外生成无需求灰度图");
            }
            Console.WriteLine("通过：没有本地消费者时，冻结变量生产者目录仍驱动所需资源输出");
            var parent=engine.CreateWorkflow("S3-parent-return","子流程结果消费");
            parent.Nodes.Add(Node("parentSource"));
            var subroutine=(SubroutineParameters)ToolRegistry.CreateParameters("Subroutine");subroutine.SubroutineId=workflow.Id;
            subroutine.Inputs.Add(new SubroutineInputConfig{Name="Count",TypeName="Integer",BindingSource="parentSource.FilteredBlobCount"});
            subroutine.Outputs.Add(new SubroutineOutputConfig{Name="ForwardImage",TypeName="Image",BindingSource="source.BinaryMask"});
            parent.Nodes.Add(new WorkflowNodeBase("call","call","子流程","Subroutine"){SubroutineId=workflow.Id,Parameters=subroutine});
            using(var returned=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(parent.Id,image)).GetAwaiter().GetResult())
            {
                Require(returned.Success,"子流程输出需求执行失败："+string.Join(";",returned.Errors??new()));
                var forwarded=OutputVariableResolver.Resolve(returned.NodeResults["call"].Result!,"ForwardImage") as IVisionImage;
                Require(forwarded is not null&&!forwarded.IsEmpty,"子流程返回没有保留所需图像资源");
            }
            Console.WriteLine("通过：真实父/子流程返回链在无本地消费者时也生成并返回所需图像");
            // Geometry inheritance requires a proven physical frame. A raw caller
            // buffer intentionally has no provenance; use two actual ImageLoad
            // invocations so same-source success and cross-source rejection are real.
            var imagePath=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"consumption-source.png");
            HalconDotNet.HOperatorSet.WriteImage(image.Unwrap(),"png",0,imagePath);
            foreach(var loadId in new[]{"load","emptyLoad"})
            {
                var loadParameters=ToolRegistry.CreateParameters("ImageLoad");
                loadParameters.GetType().GetProperty("FilePath")!.SetValue(loadParameters,imagePath);
                loadParameters.GetType().GetProperty("SourceKind")!.SetValue(loadParameters,ImageSourceKind.Local);
                workflow.Nodes.Add(new WorkflowNodeBase(loadId,loadId,loadId,"ImageLoad"){Parameters=loadParameters});
            }
            source.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(source.Parameters,new ImageSourceReference("load","OutputImage","load","原图"));
            var binary=Node("binary");binary.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(binary.Parameters,new ImageSourceReference("source","BinaryMask","source","二值图"));workflow.Nodes.Add(binary);
            var gray=Node("gray");gray.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(gray.Parameters,new ImageSourceReference("source","GrayImage","source","灰度图"));
            gray.Parameters.GetType().GetProperty("Threshold")!.SetValue(gray.Parameters,210);workflow.Nodes.Add(gray);
            // This real consumer projects the single solid Blob's boundary path id into an integer
            // parameter. It must cause boundary generation although OutputContours remains false.
            var boundary=Node("boundary");workflow.Nodes.Add(boundary);
            boundary.ParamSettings.SetSetting(new ParamSetting{ParameterName="Threshold",BindingType=BindingType.Binding,
                SourceNodeId="source",SourceWorkflowId=workflow.Id,SourceProperty="FilteredBlobs",
                ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference("source",BlobSchema.BlobsPort,BlobSchema.BlobsType,
                    new[]{"boundary","paths","path-id"},new BindingSelection.AtConstant(0),workflow.Id))});
            void RegionBinding(WorkflowNodeBase target,string sourceId,bool mask)
            {
                var property=mask?"MaskRegionInput":"InspectionRegionInput";
                target.Parameters.GetType().GetProperty(mask?"MaskMode":"InspectionMode")!.SetValue(target.Parameters,RegionFieldMode.InheritedRegion);
                target.ParamSettings.SetSetting(new ParamSetting{ParameterName=property,BindingType=BindingType.Binding,
                    SourceNodeId=sourceId,SourceWorkflowId=workflow.Id,SourceProperty="Region",
                    ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference(sourceId,BlobSchema.RegionPort,TypeRef.ResourceOf(ResourceKind.Region),scopeId:workflow.Id))});
            }
            var inspection=Node("inspection");RegionBinding(inspection,"source",false);
            ((RoiCollection)inspection.Parameters.GetType().GetProperty("InspectionRois")!.GetValue(inspection.Parameters)!).AddInput(new Roi{
                Source=RoiGeometrySource.WholeRegionSub,Binding=new ParamSetting{BindingType=BindingType.Binding,SourceNodeId="inactive-missing",SourceProperty="Shapes"}});
            inspection.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(inspection.Parameters,new ImageSourceReference("source","GrayImage","source","灰度图"));workflow.Nodes.Add(inspection);
            var mask=Node("mask");RegionBinding(mask,"source",true);
            mask.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(mask.Parameters,new ImageSourceReference("source","GrayImage","source","灰度图"));workflow.Nodes.Add(mask);
            var emptySource=Node("emptySource");emptySource.Parameters.GetType().GetProperty("Threshold")!.SetValue(emptySource.Parameters,210);workflow.Nodes.Add(emptySource);
            emptySource.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(emptySource.Parameters,new ImageSourceReference("emptyLoad","OutputImage","emptyLoad","另一物理来源"));
            var emptyRegion=Node("emptyRegion");RegionBinding(emptyRegion,"emptySource",false);
            emptyRegion.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(emptyRegion.Parameters,new ImageSourceReference("emptySource","GrayImage","emptySource","灰度图"));workflow.Nodes.Add(emptyRegion);
            var emptyGeometry=Node("emptyGeometry");
            emptyGeometry.Parameters.GetType().GetProperty("InspectionMode")!.SetValue(emptyGeometry.Parameters,RegionFieldMode.InheritedGeometry);
            var roiCollection=(RoiCollection)emptyGeometry.Parameters.GetType().GetProperty("InspectionRois")!.GetValue(emptyGeometry.Parameters)!;
            roiCollection.AddInput(new Roi{Source=RoiGeometrySource.WholeRegionSub,Binding=new ParamSetting{BindingType=BindingType.Binding,SourceNodeId="source",SourceProperty="InspectionGeometries",SourceWorkflowId=workflow.Id}});
            emptyGeometry.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(emptyGeometry.Parameters,new ImageSourceReference("source","GrayImage","source","灰度图"));workflow.Nodes.Add(emptyGeometry);
            using var execution=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult();
            Require(execution.Success,"真实消费工作流失败："+string.Join(";",execution.Errors??new()));
            double Value(string node,string name)=>Convert.ToDouble(execution.NodeResults[node].Result!.GetType().GetProperty(name)!.GetValue(execution.NodeResults[node].Result));
            Require(Value("binary","FilteredTotalArea")==16&&Value("gray","FilteredBlobCount")==0,"灰度/二值来源语义或下游执行错误");
            Require(Value("boundary","ThresholdMinUsed")==1,"声明成员需求没有驱动完整边界生成和下游消费");
            Require(Value("inspection","FilteredTotalArea")==16&&Value("mask","FilteredBlobCount")==0
                &&Value("emptyRegion","FilteredBlobCount")==0&&Value("emptyGeometry","FilteredBlobCount")==0,
                "Inspection/Mask或有效空Region/几何被错误退成全图");
            Console.WriteLine("通过：关闭显示输出仍按实际图像源/声明成员需求生成灰度、二值和边界；下游真实算法消费成功");
            Console.WriteLine("通过：实际Region继承/屏蔽、有效空Region和空几何订阅均进入正确处理域，空值不退回全图");
            using var snapshot=execution.DeclaredOutputs.Single(o=>o.Identity.NodeId=="source").AcquireReader();
            var values=snapshot.Values!;var complete=values.Get<SequenceValue>(BlobSchema.BlobsPort);var record=(RecordValue)complete[0]!;
            Require(complete.Declaration==BlobSchema.BlobsType&&record.Contains("region")&&record.Contains("boundary"),"发布的记录不是唯一完整Runtime声明");
            var declaredGray=values.Get<IReadOnlyImage>(BlobSchema.Field(5));var declaredBinary=values.Get<IReadOnlyImage>(BlobSchema.Field(6));
            using var region=values.Get<IReadOnlyRegion>(BlobSchema.RegionPort).AcquireReader();
            Require(declaredGray.Frame==declaredBinary.Frame&&declaredGray.Frame==region.SourceFrame,"正式图像和区域被发布成不同来源帧");
            using var held=declaredGray.AcquirePixels();execution.Dispose();
            Require(held.Layout.Width==8&&region.RunCount>0,"执行结果退出破坏独立读者");
            Console.WriteLine("通过：正式发布同一完整记录/Region/图像来源，执行结束后独立读者仍有效");
            // A bound Region from another independently captured source must fail, never be relabelled.
            var mismatch=Node("mismatch");RegionBinding(mismatch,"emptySource",false);
            mismatch.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(mismatch.Parameters,new ImageSourceReference("source","GrayImage","source","灰度图"));workflow.Nodes.Add(mismatch);
            using(var rejected=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())
                Require(!rejected.Success&&rejected.NodeResults["mismatch"].Result?.ErrorMessage?.Contains("BLOB_REGION_FRAME_MISMATCH")==true,"跨帧Region没有明确拒绝");
            workflow.Nodes.Remove(mismatch);
            var absent=Node("absent");absent.Parameters.GetType().GetProperty("InspectionMode")!.SetValue(absent.Parameters,RegionFieldMode.InheritedRegion);workflow.Nodes.Add(absent);
            using(var rejected=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())
                Require(!rejected.Success&&rejected.NodeResults["absent"].Result?.ErrorMessage?.Contains("BLOB_REGION_SOURCE_INVALID")==true,"已选像素区域但来源未设置时退回了全图");
            workflow.Nodes.Remove(absent);
            var invalid=Node("invalid");
            ((RoiCollection)invalid.Parameters.GetType().GetProperty("InspectionRois")!.GetValue(invalid.Parameters)!).AddInput(new Roi());workflow.Nodes.Add(invalid);
            using(var rejected=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())
                Require(!rejected.Success&&rejected.NodeResults["invalid"].Result?.ErrorMessage?.Contains("BLOB_ROI_GEOMETRY_INVALID")==true,"启用却无形状的本地ROI没有明确拒绝");
            Console.WriteLine("通过：实际工作流拒绝跨帧Region、已选却无源的Region和无形状本地ROI");
            Console.WriteLine("S3真实消费链：7组通过；不代表完整S3已核收。");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private sealed class CheckLogger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All)
        {if(level==LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}
    }
}
