using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Workflow;

// Runtime regression: references the official Release through the existing SafeEntry.
// No substitute producer, mock parameter context or copied product payload.
internal static class S2DocumentEditingCases
{
    private static readonly List<object> Outcomes=new();
    private static PluginManager Plugins=null!;
    private static string Root=null!,ImagePath=null!;
    [STAThread] private static int Main()
    {
        Root=Environment.GetEnvironmentVariable("S2_CASE_ROOT")!;
        Directory.CreateDirectory(Root);
        try
        {
            Plugins=new PluginManager();Plugins.LoadPlugins(Path.Combine(Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!,"plugins"));PluginLoader.MarkAsLoaded();
            ImagePath=Path.Combine(Root,"roi-source.bmp");
            using(var image=HalImage.CreateFromBytes(Enumerable.Repeat((byte)200,64*48).ToArray(),64,48,1))
                HOperatorSet.WriteImage(image.Unwrap(),"bmp",0,ImagePath);
            Check("真实矩形集合继承与声明冻结",()=>Whole(false));
            Check("真实圆集合继承与同帧",()=>Whole(true));
            Check("参数标量广播与序列逐项及显式索引",Parameters);
            Check("参数缺字段及失效成员明确拒绝",Missing);
            Check("不同ImageLoad物理来源即使文件相同仍拒绝",WrongFrame);
            Check("正式计划来源核帧不受重复显示字段误导",MismatchedDisplaySource);
            Check("非活动非法参数草稿保留且切回明确拒绝",InactiveDraft);
            Check("整区域record拒绝非正尺寸",InvalidWholeGeometry);
            Check("检测与屏蔽继承同时实际执行并保留非活动分支",MaskInheritance);
            Check("本run正式产物与上run值拒绝",RunIdentityCheck);
            Check("ROI真实保存重开后继续typed执行",Persistence);
            Check("CircleDetect可空Circle正式声明及中立冻结",CircleDeclaration);
        }
        catch(Exception error){Outcomes.Add(new{name="fixture初始化",passed=false,error=error.ToString()});}
        File.WriteAllText(Path.Combine(Root,"roi-typed-workflow-results.json"),JsonSerializer.Serialize(Outcomes,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(Outcomes));
        return Outcomes.Any(o=>o.GetType().GetProperty("passed")!.GetValue(o) is false)?1:0;
    }
    private sealed class Chain
    {
        public readonly WorkflowEngine Engine=new(new CheckLogger());
        public readonly Workflow Flow;
        public readonly WorkflowNodeBase Load,Threshold,Blob;
        public Chain()
        {
            Flow=Engine.CreateWorkflow("roi-"+Guid.NewGuid().ToString("N"),"真实ROI链");
            Load=Node("load","ImageLoad");Set(Load.Parameters,"FilePath",ImagePath);Set(Load.Parameters,"SourceKind","Local");
            Threshold=Node("threshold","ImageThreshold");Set(Threshold.Parameters,"Threshold",3);Source(Threshold,"load","OutputImage");
            Blob=Node("blob","BlobAnalysis");Set(Blob.Parameters,"Threshold",128);Set(Blob.Parameters,"IsAreaFilterEnabled",false);Set(Blob.Parameters,"IsBlobCountFilterEnabled",false);
            Set(Blob.Parameters,"InspectionMode","InheritedGeometry");Source(Blob,"load","OutputImage");
            Flow.Nodes.Add(Load);Flow.Nodes.Add(Threshold);Flow.Nodes.Add(Blob);
        }
        public RoiParameters Producer=>(RoiParameters)Threshold.Parameters;
        public RoiParameters Consumer=>(RoiParameters)Blob.Parameters;
        public ExecutionResult Run()=>new WorkflowExecutionEngine(Engine,Plugins,new CheckLogger()).ExecuteWorkflowAsync(new WorkflowRunRequest(Flow.Id)).GetAwaiter().GetResult();
        public void Rectangles()
        {
            Producer.InspectionRois.Rois.Clear();
            Producer.InspectionRois.Rois.Add(Roi.CreateLocal("rect1",new RotatedRect2d(16,16,6,8,0)));
            Producer.InspectionRois.Rois.Add(Roi.CreateLocal("rect2",new RotatedRect2d(38,22,12,10,0)));
        }
    }
    private static void Whole(bool circle)
    {
        var c=new Chain();
        Set(c.Blob.Parameters,"InspectionMode",circle?"InheritedCircle":"InheritedRectangle");
        if(circle)c.Producer.InspectionRois.Rois.Add(Roi.CreateLocal("circle",new Circle2d(24,20,6)));else c.Rectangles();
        var roi=Roi.CreateSubscribed("继承","threshold","InspectionGeometries");roi.InheritedShapeType=circle?ShapeType.Circle:ShapeType.RotatedRectangle;c.Consumer.InspectionRois.Rois.Add(roi);
        using var run=c.Run();Success(run);
        var shapes=FrozenGeometries(run,"threshold");Require(shapes.Count==(circle?1:2),"生产者未冻结实际本地几何集合");
        var consumed=FrozenGeometries(run,"blob");Require(consumed.Count==shapes.Count,"下游未消费动态计划缓存");
        Require(Count(run)==(circle?1:2),"继承后实际区域/连通分量不符");
        Require(!roi.HasRuntimeBinding(null),"正式设计对象被写入runtime cache");
    }
    private static Roi Composed(Chain c,bool indexed=false)
    {
        Set(c.Blob.Parameters,"InspectionMode","InheritedRectangleParameters");
        var entry=Bind(c,"InspectionGeometries",new[]{"width"},indexed?0:null);
        var fields=new Dictionary<string,ParamSetting>{["CenterX"]=entry,["CenterY"]=Constant("CenterY",32d),["Width"]=Constant("Width",2d),["Height"]=Constant("Height",2d),["Angle"]=Constant("Angle",0d)};
        var roi=Roi.CreateComposed("参数矩形",ShapeType.RotatedRectangle,fields);c.Consumer.InspectionRois.Rois.Add(roi);return roi;
    }
    private static void Parameters()
    {
        var c=new Chain();c.Rectangles();Composed(c);
        using(var run=c.Run()){Success(run);Require(Count(run)==2&&FrozenGeometries(run,"blob").Count==2,"序列应按两项生成，常量标量广播");}
        c.Consumer.InspectionRois.Rois.Clear();Composed(c,true);
        using(var run=c.Run()){Success(run);Require(Count(run)==1,"显式[0]未缩为单个实例");}
        c.Consumer.InspectionRois.Rois.Clear();
        Set(c.Blob.Parameters,"InspectionMode","InheritedCircleParameters");
        var fields=new Dictionary<string,ParamSetting>{["CenterX"]=Constant("CenterX",24d),["CenterY"]=Constant("CenterY",20d),["Radius"]=Bind(c,"ThresholdUsed")};
        c.Consumer.InspectionRois.Rois.Add(Roi.CreateComposed("标量圆",ShapeType.Circle,fields));
        using var scalar=c.Run();Success(scalar);var record=(RecordValue)FrozenGeometries(scalar,"blob")[0]!;Require(record.Get<double>("radius")==3,"正式数值输出未转换为可广播标量");
    }
    private static void Missing()
    {
        var c=new Chain();c.Rectangles();var roi=Composed(c);roi.ParameterBindings!.Remove("Height");
        using(var failed=c.Run())Rejected(failed,"Height");
        c.Consumer.InspectionRois.Rois.Clear();roi=Composed(c);roi.ParameterBindings!["CenterX"]=Bind(c,"InspectionGeometries",new[]{"not-a-declared-member"});
        using var badPath=c.Run();Rejected(badPath,"member");
    }
    private static void WrongFrame()
    {
        var c=new Chain();c.Rectangles();c.Consumer.InspectionRois.Rois.Add(Roi.CreateSubscribed("继承","threshold","InspectionGeometries"));
        var second=Node("load-other","ImageLoad");Set(second.Parameters,"FilePath",ImagePath);Set(second.Parameters,"SourceKind","Local");c.Flow.Nodes.Insert(0,second);Source(c.Blob,"load-other","OutputImage");
        using var run=c.Run();Rejected(run,"BLOB_ROI_FRAME_MISMATCH");Require(run.NodeResults["threshold"].Success,"来源工具先失败，不能算错帧拒绝");
    }
    private static void MismatchedDisplaySource()
    {
        var c=new Chain();c.Rectangles();
        var second=Node("load-other","ImageLoad");Set(second.Parameters,"FilePath",ImagePath);Set(second.Parameters,"SourceKind","Local");c.Flow.Nodes.Insert(0,second);
        Source(c.Threshold,"load-other","OutputImage");
        var binding=Bind(c,"InspectionGeometries");binding.SourceNodeId="load";
        var roi=Roi.CreateSubscribed("错误显示来源","load","InspectionGeometries");roi.Binding=binding;c.Consumer.InspectionRois.Rois.Add(roi);
        using var run=c.Run();Rejected(run,"BLOB_ROI_FRAME_MISMATCH");
        Require(run.NodeResults["threshold"].Success,"必须由实际异帧几何触发检查");
    }
    private static void InactiveDraft()
    {
        var c=new Chain();c.Rectangles();Set(c.Blob.Parameters,"InspectionMode","LocalGeometry");
        c.Consumer.InspectionRois.Rois.Add(Roi.CreateComposed("未配置旧草稿",ShapeType.None,new Dictionary<string,ParamSetting>()));
        Require(RoiBindingInputs.Describe(c.Blob.Parameters).Count==0,"非活动草稿进入正式动态绑定目录");
        using(var run=c.Run())Success(run);
        Set(c.Blob.Parameters,"InspectionMode","InheritedGeometry");
        try{RoiBindingInputs.Describe(c.Blob.Parameters);throw new Exception("活动非法形状未被拒绝");}
        catch(ArgumentException error){Require(error.Message.Contains("ROI_COMPOSE_SHAPE"),"错误分类不符");}
    }
    private static void InvalidWholeGeometry()
    {
        foreach(var shape in new[]{new RoiShape{Geometry=new Circle2d(2,3,0)},new RoiShape{Geometry=new RotatedRect2d(2,3,0,4,0)}})
        {
            var record=RoiShapeValueAdapter.Freeze(shape);
            try{RoiGeometryValueAdapter.Materialize(record);throw new Exception("非法整区域尺寸未被拒绝");}
            catch(ArgumentException error){Require(error.Message.StartsWith("ROI_SOURCE_SIZE"),"错误分类不符："+error.Message);}
        }
    }
    private static void MaskInheritance()
    {
        var c=new Chain();c.Rectangles();Set(c.Blob.Parameters,"InspectionMode","InheritedRectangle");
        var inspection=Roi.CreateSubscribed("检测矩形","threshold","InspectionGeometries");inspection.InheritedShapeType=ShapeType.RotatedRectangle;c.Consumer.InspectionRois.Rois.Add(inspection);
        double Area(ExecutionResult run)=>Convert.ToDouble(run.NodeResults["blob"].Result!.GetType().GetProperty("FilteredTotalArea")!.GetValue(run.NodeResults["blob"].Result));
        double initial;using(var run=c.Run()){Success(run);initial=Area(run);Require(Count(run)==2,"检测继承基线应有两颗");}
        Set(c.Blob.Parameters,"MaskMode","InheritedCircleParameters");
        var mask=Roi.CreateComposed("屏蔽圆",ShapeType.Circle,new Dictionary<string,ParamSetting>{["CenterX"]=Constant("CenterX",16d),["CenterY"]=Constant("CenterY",16d),["Radius"]=Constant("Radius",2d)});c.Consumer.MaskRois.Rois.Add(mask);
        using(var run=c.Run()){Success(run);Require(Area(run)>0&&Area(run)<initial,"参数屏蔽圆没有从检测面积实际扣除");}
        var whole=Roi.CreateSubscribed("屏蔽矩形","threshold","InspectionGeometries");whole.InheritedShapeType=ShapeType.RotatedRectangle;c.Consumer.MaskRois.Rois.Add(whole);
        Set(c.Blob.Parameters,"MaskMode","InheritedRectangle");
        using(var run=c.Run()){Success(run);Require(Count(run)==0&&Area(run)==0,"整矩形屏蔽未扣除全部检测区域");}
        Set(c.Blob.Parameters,"MaskMode","InheritedCircleParameters");
        using(var run=c.Run()){Success(run);Require(Area(run)>0&&Area(run)<initial,"切回参数屏蔽未恢复原分支");}
        Require(c.Consumer.MaskRois.Rois.Count==2&&!mask.HasRuntimeBinding("Radius")&&!whole.HasRuntimeBinding(null),"模式切换丢草稿或污染正式缓存");
    }
    private static void RunIdentityCheck()
    {
        var c=new Chain();c.Rectangles();c.Consumer.InspectionRois.Rois.Add(Roi.CreateSubscribed("继承","threshold","InspectionGeometries"));
        using var first=c.Run();Success(first);using var second=c.Run();Success(second);
        var old=first.DeclaredOutputs.Single(s=>s.Identity.NodeId=="threshold");var current=second.DeclaredOutputs.Single(s=>s.Identity.NodeId=="threshold");var consumer=second.DeclaredOutputs.Single(s=>s.Identity.NodeId=="blob");
        using var reader=old.AcquireReader();
        var output=new OutputBindingChangeService().ReadContract(c.Threshold)!.Single(v=>v.Name=="InspectionGeometries");var port=BindingClrAdapter.DescribeOutput(output);
        var sourceNode=new BindingNodeId(c.Flow.Id,"threshold");var targetNode=new BindingNodeId(c.Flow.Id,"blob");
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{port});var source=new BindingSource(sourceNode,current.Identity.ScopeId,schema,0);
        var scope=new BindingScopeSnapshot(targetNode,consumer.Identity.DocumentSessionId,0,new[]{source},new[]{sourceNode},Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
        var service=new ParameterBindingService();var reference=new OutputReference("threshold",port.Id,port.Type,scopeId:c.Flow.Id,storage:port.Storage);
        var plan=service.CompileBinding(reference,new Port("roi",port.Type),scope).Plan??throw new Exception("真实声明未编译");
        var stale=new BindingNodeResult(sourceNode,old.Identity,0,schema.Fingerprint,true,reader.Values!);
        var snapshot=new BindingRunSnapshot(targetNode,consumer.Identity,0,new[]{stale},new Dictionary<BindingNodeId,RunIdentity>{{sourceNode,current.Identity}});
        var rejected=service.ResolveBinding(plan,snapshot);Require(!rejected.Success&&rejected.Diagnostic?.Code=="run-mismatch","真实上一run冻结几何未被正式typed身份检查拒绝");
    }
    private static void Persistence()
    {
        var c=new Chain();c.Rectangles();var roi=Composed(c,true);
        var folder=Path.Combine(Root,"save-reopen");Directory.CreateDirectory(folder);var path=Path.Combine(folder,"roi.solution");
        var solution=new Solution{Id="roi-fixture",SchemaVersion=2};solution.Workflows.Add(c.Flow);var repository=new SolutionRepository(Path.Combine(folder,"recovery"));
        Require(repository.Save(solution,path),repository.LastError??"保存失败");var loaded=repository.LoadCandidate(path).Candidate??throw new Exception(repository.LastError??"重开失败");
        var flow=loaded.Workflows.Single();var restored=(RoiParameters)flow.Nodes.Single(n=>n.Id=="blob").Parameters;var saved=restored.InspectionRois.Rois.Single();
        Require(saved.Id==roi.Id&&saved.ParameterBindings!["CenterX"].ReferenceExpression==roi.ParameterBindings!["CenterX"].ReferenceExpression&&!saved.HasRuntimeBinding("CenterX"),"重开丢索引/绑定或错误持久化缓存");
        var engine=new WorkflowEngine(new CheckLogger());engine.RegisterWorkflow(flow);using var run=new WorkflowExecutionEngine(engine,Plugins,new CheckLogger()).ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id){ResourcePaths=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(loaded)}).GetAwaiter().GetResult();Success(run);Require(Count(run)==1,"重开后未真实执行typed索引");
    }
    private static void CircleDeclaration()
    {
        var node=Node("circle","CircleDetect");var declared=new OutputBindingChangeService().ReadContract(node)!.Single(v=>v.Name=="Circle");
        var port=BindingClrAdapter.DescribeOutput(declared);Require(port.Nullable,"CircleDetect Circle[]?正式静态声明丢nullable");
        Require(port.Type.Shape==ValueShape.Sequence&&port.Type.Element!.Geometry==GeometryKind.Circle,"圆集合未有真实中立声明");
        Require(BindingClrAdapter.NormalizeValue(port,null) is null,"Arc模式Circle=null不能正式冻结");
        var tool=ToolRegistry.CreateToolInstance("CircleDetect")!;var result=(ToolResults)Activator.CreateInstance(tool.ResultType)!;
        var actual=result.GetOutputVariables().Single(v=>v.Name=="Circle");Require(actual.AllowsNull&&BindingClrAdapter.DescribeOutput(actual).Type==port.Type,"静态与运行期圆声明不一致");
        Console.WriteLine("未运行：CircleDetect原生拟合；此项只验证实际注册声明和null冻结，不冒称拟合通过。");
    }
    private static SequenceValue FrozenGeometries(ExecutionResult run,string node)
    {
        var snapshot=run.DeclaredOutputs.Single(s=>s.Identity.NodeId==node);using var reader=snapshot.AcquireReader();
        var field=snapshot.Fields.Single(p=>p.Value.Name=="InspectionGeometries");return reader.Values!.Get<SequenceValue>(field.Key);
    }
    private static ParamSetting Bind(Chain c,string name,string[]? fields=null,int? index=null)
    {
        var output=new OutputBindingChangeService().ReadContract(c.Threshold)!.Single(v=>v.Name==name);var port=BindingClrAdapter.DescribeOutput(output);
        return new ParamSetting{ParameterName=name,BindingType=BindingType.Binding,SourceNodeId="threshold",SourceWorkflowId=c.Flow.Id,SourceFieldId=output.FieldId,SourceProperty=name,
            ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference("threshold",port.Id,port.Type,fields,index is int i?new BindingSelection.AtConstant(i):BindingSelection.All,c.Flow.Id,port.Storage))};
    }
    private static ParamSetting Constant(string name,double value)=>ParamSetting.CreateConstant(name,value);
    private static int Count(ExecutionResult run)=>Convert.ToInt32(run.NodeResults["blob"].Result!.GetType().GetProperty("FilteredBlobCount")!.GetValue(run.NodeResults["blob"].Result));
    private static WorkflowNodeBase Node(string id,string tool)=>new(id,id,id,tool){Parameters=ToolRegistry.CreateParameters(tool)};
    private static void Source(WorkflowNodeBase node,string producer,string output)=>node.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(node.Parameters,new ImageSourceReference(producer,output,producer,output));
    private static void Set(object target,string name,object value){var p=target.GetType().GetProperty(name)!;p.SetValue(target,p.PropertyType.IsEnum?Enum.Parse(p.PropertyType,(string)value):value);}
    private static void Success(ExecutionResult result)=>Require(result.Success,string.Join(";",result.Errors));
    private static void Rejected(ExecutionResult result,string fragment)
    {var text=string.Join(";",result.Errors)+";"+string.Join(";",result.NodeResults.Values.SelectMany(n=>n.ErrorMessages));Require(!result.Success&&text.Contains(fragment,StringComparison.OrdinalIgnoreCase),"未因指定ROI问题拒绝: "+text);}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Check(string name,Action test){try{test();Outcomes.Add(new{name,passed=true});Console.WriteLine("通过："+name);}catch(Exception error){Outcomes.Add(new{name,passed=false,error=error.ToString()});Console.WriteLine("失败："+name+" "+error.Message);}}
    private sealed class CheckLogger:ILogger{public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(message);}}
}
