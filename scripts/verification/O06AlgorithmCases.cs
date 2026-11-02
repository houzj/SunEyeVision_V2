using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.Reflection;
using System.IO;
using SunEyeVision.Core.Services.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls.Primitives;
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
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;
using Mode=SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSegmentationMode;

internal static class S2DocumentEditingCases
{
    private static readonly BlobSettings Basic=new(){IsAreaFilterEnabled=false,BlobCountLimit=20000,OutputGrayImage=false};
    private static int passed;
    [STAThread] private static int Main()
    {
        try
        {
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var plugins=new PluginManager();plugins.LoadPlugins();PluginLoader.MarkAsLoaded();
            Case("单/双阈值闭区间端点、等阈值、反向和实际值",()=>{
                using var resources=new FrozenOutputResources();using var input=Gray(new byte[]{0,99,100,101,199,200,201,255});
                using var single=new BlobSession().Execute(Basic with{Threshold=100},input,resources);
                Require(single.TotalArea==6&&single.ThresholdMinUsed==100&&single.ThresholdMaxUsed==255,"单阈值端点错误");
                using var range=new BlobSession().Execute(Basic with{SegmentationMode=Mode.DoubleThreshold,Threshold=100,UpperThreshold=200},input,resources);
                Require(range.TotalArea==4&&range.ThresholdMaxUsed==200,"双阈值端点错误");
                using var equal=new BlobSession().Execute(Basic with{SegmentationMode=Mode.DoubleThreshold,Threshold=100,UpperThreshold=100},input,resources);
                Require(equal.TotalArea==1,"等阈值应选择恰好一个灰度");
                using var inverse=new BlobSession().Execute(Basic with{SegmentationMode=Mode.DoubleThreshold,Threshold=100,UpperThreshold=200,Type=BlobThresholdMode.BinaryInv},input,resources);
                Require(inverse.TotalArea==4,"区间反向错误");
                Throws(()=>new BlobSession().Execute(Basic with{SegmentationMode=Mode.DoubleThreshold,Threshold=200,UpperThreshold=100},input,resources),"双阈值");
            });
            Case("不二值化原始Gray8、Domain内严格0/255和可空输出",()=>{
                using var resources=new FrozenOutputResources();using var input=Gray(new byte[]{0,255,0,255});
                var settings=Basic with{SegmentationMode=Mode.None,Threshold=-1,UpperThreshold=-1,Type=BlobThresholdMode.BinaryInv};
                using var output=new BlobSession().Execute(settings,input,resources);
                Require(output.TotalArea==2&&output.ThresholdMinUsed is null&&output.ThresholdMaxUsed is null&&output.TypeUsed=="NotApplied","None仍使用阈值或极性");
                var values=new InputValues(BlobSchema.Outputs,output.ToValues());Require(values.Get<long?>(BlobSchema.Field(8)) is null&&values.Get<string>(BlobSchema.Field(102))=="None","可空声明不一致");
                foreach(var bad in new byte[]{1,127,254}){using var invalid=Gray(new byte[]{255,bad});Throws(()=>new BlobSession().Execute(settings,invalid,resources),"BINARY_INPUT_VALUES");}
                using var color=new FrozenImageResource(new(1,1,3,PixelLayout.Bgr24),new(Guid.NewGuid(),"color"),BlobSchema.ImageFrame,new byte[]{255,255,255},new[]{new RegionRun(0,0,0)});
                Throws(()=>new BlobSession().Execute(settings,color,resources),"BINARY_INPUT_FORMAT");
                using var outside=Gray(new byte[]{255,17},new[]{new RegionRun(0,0,0)});using var ok=new BlobSession().Execute(settings,outside,resources);Require(ok.TotalArea==1,"Domain外非法值不应参与准入");
                using var domainBad=Gray(new byte[]{255,17});using var roi=new FrozenRegionResource(BlobSchema.ImageFrame,domainBad.Frame,new[]{new RegionRun(0,0,0)});
                Throws(()=>new BlobSession().Execute(settings,domainBad,resources,roi),"BINARY_INPUT_VALUES");
                var frame=Guid.NewGuid();var document=Guid.NewGuid();var workflow=Guid.NewGuid();
                using var prior=new FrozenImageResource(new(2,1,2,PixelLayout.Gray8),new(frame,"same-source",new(document,workflow,Guid.NewGuid(),"source",Guid.NewGuid())),BlobSchema.ImageFrame,new byte[]{0,255},new[]{new RegionRun(0,0,1)});
                using var current=new FrozenImageResource(new(2,1,2,PixelLayout.Gray8),new(frame,"same-source",new(document,workflow,Guid.NewGuid(),"source",Guid.NewGuid())),BlobSchema.ImageFrame,new byte[]{0,255},new[]{new RegionRun(0,0,1)});
                using var staleRegion=new FrozenRegionResource(BlobSchema.ImageFrame,prior.Frame,new[]{new RegionRun(0,1,1)});
                Throws(()=>new BlobSession().Execute(settings,current,resources,staleRegion),"frame");
            });
            Case("Runtime规范值可空阈值和最终集合统计",()=>{
                using var image=HalImage.CreateFromBytes(new byte[]{0,255,255,0},4,1,1);
                using var invocation=ExecuteBlob(image,new BlobSettings{SegmentationMode=Mode.None,IsAreaFilterEnabled=false,OutputGrayImage=true});
                var result=(DeclaredOutputSnapshot)invocation.NodeResults["blob"].Result!;var output=(BlobOutput)result.Output!;
                Require(invocation.Success&&output.ThresholdMinUsed is null&&output.ThresholdMaxUsed is null,"None阈值不应伪造");
                foreach(var id in new[]{BlobSchema.Field(8),BlobSchema.Field(9)})
                    Require(result.GetOutputVariables().Single(v=>v.PortId==id).AllowsNull&&result.Values[id] is null,"可空正式声明丢失");
                Require(result.Values[BlobSchema.Field(5)] is IReadOnlyImage,"规范图像类型改变");
                var many=new byte[16*16];many[2*16+2]=255;for(var y=8;y<10;y++)for(var x=8;x<10;x++)many[y*16+x]=255;
                using var multiple=HalImage.CreateFromBytes(many,16,16,1);
                using var final=ExecuteBlob(multiple,new BlobSettings{SegmentationMode=Mode.None,IsAreaFilterEnabled=false,BlobCountLimit=1,SortMode=BlobSortMode.SortModeDecend});
                var limited=(BlobOutput)((DeclaredOutputSnapshot)final.NodeResults["blob"].Result!).Output!;
                Require(final.Success&&limited.Count==1&&limited.MinimumArea==4&&limited.MaximumArea==4,"统计未来自最终限数集合");
            });
            Case("拒绝缺失Runtime配置和SDK双模型，不补默认配置",()=>{
                var flow=new Workflow("strict-runtime","严格配置");flow.Nodes.Add(new WorkflowNodeBase("blob","blob","blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id)});
                var json=JsonSerializer.SerializeToNode(flow,WorkflowSerializationOptions.Default)!;
                json["Nodes"]![0]!.AsObject().Remove("RuntimeConfiguration");
                Throws(()=>json.Deserialize<Workflow>(WorkflowSerializationOptions.Default),"RuntimeConfiguration");
                json=JsonSerializer.SerializeToNode(flow,WorkflowSerializationOptions.Default)!;json["Nodes"]![0]!["Parameters"]=new JsonObject();
                Throws(()=>json.Deserialize<Workflow>(WorkflowSerializationOptions.Default),"SDK Parameters");
                Require(WorkflowNodeFactory.CreateAlgorithmNode(BlobTool.Id,"bad","bad","bad",new GenericToolParameters()) is null,"Runtime接受了SDK参数");
            });
            Case("三种分割模式正式保存重开，缺字段拒绝且源文件不改写",()=>{
                foreach(var mode in Enum.GetValues<Mode>()){
                    var settings=new BlobSettings{SegmentationMode=mode,Threshold=100,UpperThreshold=200};
                    var flow=new Workflow("flow","分割");flow.Nodes.Add(new WorkflowNodeBase("blob","blob","blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(settings)});
                    var solution=new Solution();solution.Workflows.Add(flow);
                    var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"modes",mode.ToString());Directory.CreateDirectory(folder);
                    var path=Path.Combine(folder,"new.solution");var repository=new SolutionRepository();Require(repository.Save(solution,path),repository.LastError??"保存失败");
                    var loaded=repository.LoadCandidate(path).Candidate??throw new Exception("重开失败");
                    Require(((BlobSettings)loaded.Workflows[0].Nodes[0].RuntimeConfiguration!.CaptureSettings()).SegmentationMode==mode,"分割模式丢失");
                    var missing=JsonNode.Parse(File.ReadAllText(path))!;missing["Workflows"]![0]!["Nodes"]![0]!["RuntimeConfiguration"]!["Settings"]!.AsObject().Remove("Threshold");
                    var invalid=Path.Combine(folder,"invalid.solution");File.WriteAllText(invalid,missing.ToJsonString());var bytes=File.ReadAllBytes(invalid);
                    Require(repository.LoadCandidate(invalid).Candidate is null&&bytes.SequenceEqual(File.ReadAllBytes(invalid)),"缺字段被默认值掩盖或源文件被修改");
                }
            });
            Case("严格形态学拒绝隐式128、非byte和彩色，保全部操作及Circle",()=>{
                using var bad=HalImage.CreateFromBytes(new byte[]{0,200,255,0},4,1,1);var rejected=Run("Morphology",bad);try{Require(!rejected.IsSuccess&&rejected.ErrorMessage!.Contains("BINARY_INPUT_VALUES"),"形态学仍按128接受灰度");}finally{rejected.Dispose();}
                using var raw16=new HImage();raw16.GenImageConst("uint2",4,4);Throws(()=>HalconImageAdmission.RequireBinary(raw16),"BINARY_INPUT_FORMAT");
                using var color=HalImage.CreateFromBytes(Enumerable.Repeat((byte)255,48).ToArray(),4,4,3);var rgb=Run("Morphology",color);try{Require(!rgb.IsSuccess&&rgb.ErrorMessage!.Contains("BINARY_INPUT_FORMAT"),"形态学先转灰接受彩色");}finally{rgb.Dispose();}
                using var thresholdInput=HalImage.CreateFromBytes(new byte[]{0,200,255,0},4,1,1);
                var thresholdResult=Run("ImageThreshold",thresholdInput,p=>{Set(p,"Method","Binary");Set(p,"MaxValue",200);});
                try{
                    var rendered=(HalImage)thresholdResult.GetType().GetProperty("OutputImage")!.GetValue(thresholdResult)!;
                    var mask=(HalImage)thresholdResult.GetType().GetProperty("BinaryMask")!.GetValue(thresholdResult)!;
                    Throws(()=>HalconImageAdmission.RequireBinary(rendered.Unwrap()),"BINARY_INPUT_VALUES");HalconImageAdmission.RequireBinary(mask.Unwrap());
                }finally{thresholdResult.Dispose();}
                var pixels=new byte[32*32];for(int y=4;y<28;y++)for(int x=4;x<28;x++)pixels[y*32+x]=255;
                using var binary=HalImage.CreateFromBytes(pixels,32,32,1);
                foreach(var operation in new[]{"Erode","Dilate","Open","Close","Gradient","TopHat","BlackHat"}){
                    var result=Run("Morphology",binary,p=>{Set(p,"Operation",operation);Set(p,"KernelShape","Circle");});try{Require(result.IsSuccess,result.ErrorMessage??operation);var output=(IVisionImage)result.GetType().GetProperty("BinaryMask")!.GetValue(result)!;using var snap=HalImageSnapshotSource.Capture(((HalImage)output).Unwrap(),new(Guid.NewGuid(),"morph"));HalconImageAdmission.RequireBinary(snap);}finally{result.Dispose();}
                }
                var seed=new byte[256*256];seed[128*256+128]=255;using var point=HalImage.CreateFromBytes(seed,256,256,1);
                foreach(var (width,height) in new[]{(2,2),(100,2),(101,2),(2,100),(2,101),(100,101),(101,100)}){
                    var result=Run("Morphology",point,p=>{Set(p,"Operation","Dilate");Set(p,"KernelShape","Rect");Set(p,"KernelWidth",width);Set(p,"KernelHeight",height);});
                    try{Require(result.IsSuccess,result.ErrorMessage??"尺寸被错误拒绝");Require(Convert.ToInt64(result.GetType().GetProperty("TotalArea")!.GetValue(result))==width*height,"偶轴被取整或尺寸被截断");}finally{result.Dispose();}
                }
                var evenCircle=Run("Morphology",point,p=>{Set(p,"Operation","Dilate");Set(p,"KernelShape","Circle");Set(p,"KernelWidth",2);Set(p,"KernelHeight",2);});
                try{
                    HOperatorSet.GenCircle(out var originalCircle,1.0,1.0,1.0);using(originalCircle)using(var expectedCircle=new HRegion(originalCircle))using(var expectedArea=expectedCircle.AreaCenter(out HTuple row,out HTuple column))using(row)using(column){
                        Console.WriteLine("HALCON原Circle半径1实际离散面积="+expectedArea.D);
                        Require(evenCircle.IsSuccess&&Convert.ToInt64(evenCircle.GetType().GetProperty("TotalArea")!.GetValue(evenCircle))==expectedArea.D,"Circle偶数尺寸未按原native半径1语义承接");
                    }
                }finally{evenCircle.Dispose();}
            });
            Case("ImageFilter均衡化有效域统计与其他滤波承接",()=>{
                var bytes=Enumerable.Range(0,64).Select(i=>(byte)(i*4)).ToArray();using var image=HalImage.CreateFromBytes(bytes,8,8,1);
                foreach(var filter in new[]{"Blur","Gaussian","MedianBlur","HistogramEqualization"}){
                    var result=Run("ImageFilter",image,p=>Set(p,"FilterTypeValue",filter));try{Require(result.IsSuccess,result.ErrorMessage??filter);Require(result.GetType().GetProperty("OutputImage")!.GetValue(result) is IVisionImage,"滤波无图像");}finally{result.Dispose();}
                }
                var scoped=Run("ImageFilter",image,p=>{Set(p,"FilterTypeValue","HistogramEqualization");((RoiParameters)p).InspectionRois.AddInput(Roi.CreateLocal("small",new RotatedRect2d(3.5,3.5,3,3,0)));});
                try{
                    Require(scoped.IsSuccess,scoped.ErrorMessage??"有效域均衡失败");var output=(HalImage)scoped.GetType().GetProperty("OutputImage")!.GetValue(scoped)!;
                    using var native=output.Unwrap().Clone();using var domain=native.GetDomain();using var area=domain.AreaCenter(out HTuple row,out HTuple column);using(row)using(column)Require(area.D==16,"均衡化输出丢失ROI Domain");
                    HOperatorSet.EquHistoImage(image.Unwrap(),out var fullObj);using(fullObj)using(var full=new HImage(fullObj)){
                        using var actual=native.GetGrayval(2,2);using var unrestricted=full.GetGrayval(2,2);Require(actual.D!=unrestricted.D,"均衡化错误地使用全图直方图");
                    }
                }finally{scoped.Dispose();}
            });
            Case("有限不等条件、三模式字段显示与模式常量约束",()=>{
                if(Application.Current is null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                var provider=(IToolSettingsFields)new BlobTool();var schema=provider.SettingsSchema;
                using var settingsView=ToolRegistry.CreateSettingsView(BlobTool.Id,new BlobSettings(),new SunEyeVision.Plugin.Wpf.Presenters.Parameters.ToolSettingsViewOptions(ReferenceOptions:ReferenceFieldOptions.Empty));
                var view=Logical(settingsView.View).OfType<DeclaredFieldsView>().Single();
                DeclaredFieldAccess Field(string path)=>view.Fields.Single(f=>f.FieldId==schema.Presentation!.Fields.Single(a=>a.DisplayName==BlobParameterDefinition.Instance.Label(path)).FieldId);
                var mode=Field("SegmentationMode");var lower=Field("Threshold");var upper=Field("UpperThreshold");var type=Field("Type");
                Require(lower.View.Visibility==Visibility.Visible&&upper.View.Visibility==Visibility.Collapsed,"Single字段显示错误");
                mode.TrySetConstant("DoubleThreshold");Require(lower.View.Visibility==Visibility.Visible&&upper.View.Visibility==Visibility.Visible&&type.View.Visibility==Visibility.Visible,"Double隐藏有效字段");
                mode.TrySetConstant("None");Require(lower.View.Visibility==Visibility.Collapsed&&upper.View.Visibility==Visibility.Collapsed&&type.View.Visibility==Visibility.Collapsed,"None仍显示阈值");
                mode.TrySetConstant("DoubleThreshold");
                var native=Logical(upper.View).OfType<SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged.SliderArrangeBase>().Single();
                var combo=(System.Windows.Controls.ComboBox)native.FindName("CBox");combo.ApplyTemplate();
                var toggle=Visuals(combo).OfType<ToggleButton>().Single(button=>button.GetBindingExpression(ToggleButton.IsCheckedProperty)?.ParentBinding.Path?.Path=="IsSubscribeLeft");
                toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Require(upper.HasInvalidText&&!settingsView.TryCapture(out _,out _),"活动Upper没有来源仍可提交");
                mode.TrySetConstant("SingleThreshold");Require(upper.View.Visibility==Visibility.Collapsed&&upper.HasInvalidText&&settingsView.TryCapture(out _,out _),"隐藏Upper仍阻断或丢失待选草稿");
                mode.TrySetConstant("DoubleThreshold");Require(!settingsView.TryCapture(out _,out _),"恢复Double未恢复待选错误");
                toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Require(settingsView.TryCapture(out _,out _)&&schema.Inputs.Single(p=>p.Id==mode.FieldId).EditModes==FieldEditModes.Constant,"模式常量或恢复值错误");
                var controlling=new Port("controller",DataType.Text,EditModes:FieldEditModes.Constant|FieldEditModes.Reference,Choices:new TextChoiceConstraint(new[]{"a","b"}));
                var dependent=new Port("dependent",DataType.Integer);var source=new ReferenceSetting(new OutputReference("node","out",DataType.Text));
                var referenced=new DeclaredFieldsView(new[]{(controlling,new FieldPresentation("controller","mode","g",0,FieldEditorKind.Choice,new[]{"a","b"}),(ParameterSetting)source),(dependent,new FieldPresentation("dependent","value","g",1,FieldEditorKind.Scalar){Layout=new("p","s",new("controller","a",true))},(ParameterSetting)new ConstantSetting(1L))},_=>{});
                Require(referenced.Fields[1].View.Visibility==Visibility.Visible,"未解析引用条件应显式可见");
                referenced.Fields[1].TrySetConstant("invalid");Require(!referenced.TryValidate(out _)&&referenced.HasInvalidActiveFields,"未解析引用条件错误未参与校验");
                var hiddenByContainer=new DeclaredFieldsView(new[]{(new Port("advanced",DataType.Integer),new FieldPresentation("advanced","advanced","g",0,FieldEditorKind.Scalar){Layout=new("other-tab","advanced",Advanced:true)},(ParameterSetting)new ConstantSetting(1L)),(new Port("normal",DataType.Integer),new FieldPresentation("normal","normal","g",1,FieldEditorKind.Scalar){Layout=new("selected-tab","normal")},(ParameterSetting)new ConstantSetting(2L))},_=>{});
                hiddenByContainer.Fields[0].TrySetConstant("invalid");Require(!hiddenByContainer.TryValidate(out _)&&hiddenByContainer.HasInvalidActiveFields,"其他Tab/收起Advanced中的活动错误被忽略");
            });
            Case("真实Workflow滤波→范围BinaryMask→Circle形态学→Blob None",()=>WorkflowChain(plugins));
            Console.WriteLine($"O06新算法与正式链：{passed}组通过；不代表整体UI或人工验收。");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void WorkflowChain(PluginManager plugins)
    {
        var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow("o06-algorithm","算法链");
        WorkflowNodeBase Node(string id,string tool)=>new(id,id,id,tool){Parameters=ToolRegistry.CreateParameters(tool)};
        var filter=Node("filter","ImageFilter");Set(filter.Parameters,"FilterTypeValue","HistogramEqualization");flow.Nodes.Add(filter);
        var threshold=Node("threshold","ImageThreshold");Set(threshold.Parameters,"Method","InRange");Set(threshold.Parameters,"MinThreshold",100);Set(threshold.Parameters,"MaxThreshold",255);Set(threshold.Parameters,"MaxValue",200);Source(threshold,"filter","OutputImage");flow.Nodes.Add(threshold);
        var morphology=Node("morphology","Morphology");Set(morphology.Parameters,"Operation","Open");Set(morphology.Parameters,"KernelShape","Circle");Source(morphology,"threshold","BinaryMask");flow.Nodes.Add(morphology);
        var blob=new WorkflowNodeBase("blob","blob","blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings{SegmentationMode=Mode.None,IsAreaFilterEnabled=false,OutputBinaryImage=true})};
        var sourcePort=BindingClrAdapter.DescribeOutput(new OutputBindingChangeService().ReadContract(morphology)!.Single(p=>p.Name=="BinaryMask"));
        blob.ParamSettings.SetSetting(new ParamSetting{ParameterName=BlobTool.ImageInput,BindingType=BindingType.Binding,ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference(morphology.Id,sourcePort.Id,sourcePort.Type,scopeId:flow.Id))});flow.Nodes.Add(blob);
        var bytes=new byte[32*32];for(int y=6;y<26;y++)for(int x=6;x<26;x++)bytes[y*32+x]=200;
        using var input=HalImage.CreateFromBytes(bytes,32,32,1);var executor=new WorkflowExecutionEngine(engine,plugins,new CheckLogger());
        using var run=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,input)).GetAwaiter().GetResult();
        Require(run.Success,string.Join(";",run.Errors??new()));var result=run.NodeResults["blob"].Result!;
        Require(((DeclaredOutputSnapshot)result).Output is BlobOutput{Count:1,ThresholdMinUsed:null,SegmentationModeUsed:"None"},"真实链未消费严格二值");
        using var read=run.DeclaredOutputs.Single(o=>o.Identity.NodeId=="blob").AcquireReader();
        Require(read.Values!.Get<long?>(BlobSchema.Field(8)) is null,"Workflow nullable阈值丢失");
        var image=read.Values.Get<IReadOnlyImage>(BlobSchema.Field(6));var region=read.Values.Get<IReadOnlyRegion>(BlobSchema.RegionPort);Require(image.Frame==region.SourceFrame,"二值图/Region错帧");
        var required=new WorkflowNodeBase("required-threshold","required-threshold","required-threshold",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id)};flow.Nodes.Add(required);
        required.ParamSettings.SetSetting(new ParamSetting{ParameterName=((IToolSettingsFields)new BlobTool()).SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName==BlobParameterDefinition.Instance.Label("Threshold")).FieldId,BindingType=BindingType.Binding,SourceNodeId="blob",SourceWorkflowId=flow.Id,SourceProperty="ThresholdMinUsed",
            ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference("blob",BlobSchema.Field(8),DataType.Integer,scopeId:flow.Id,storage:NumericStorage.Int64))});
        using var rejected=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,input)).GetAwaiter().GetResult();
        Require(!rejected.Success,"nullable阈值null被下游必需参数默认为0");
        Require(new[]{"filter","threshold","morphology","blob"}.All(id=>rejected.NodeResults.TryGetValue(id,out var prior)&&prior.Success),"nullable拒绝前四个节点出现了其他失败");
        Require(rejected.NodeResults.TryGetValue("required-threshold",out var missing)&&!missing.Success,"缺少目标Threshold失败记录");
        var reason=string.Join(";",missing!.ErrorMessages)+";"+missing.Result?.ErrorMessage;
        Console.WriteLine("nullable目标绑定实际拒绝："+reason);
        Require(reason.Contains("null",StringComparison.OrdinalIgnoreCase),"拒绝没有证明来自Threshold/null绑定："+reason);
    }
    private static ExecutionResult ExecuteBlob(IVisionImage image,BlobSettings settings)
    {
        var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow(Guid.NewGuid().ToString(),"Runtime算法");
        flow.Nodes.Add(new WorkflowNodeBase("blob","blob","blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(settings)});
        return new WorkflowExecutionEngine(engine,new PluginManager(),new CheckLogger()).ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image)).GetAwaiter().GetResult();
    }
    private static void Source(WorkflowNodeBase target,string node,string property)=>target.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(target.Parameters,new ImageSourceReference(node,property,node,property));
    private static void Set(object p,string key,object value){var property=p.GetType().GetProperty(key)!;property.SetValue(p,property.PropertyType.IsEnum?Enum.Parse(property.PropertyType,(string)value):value);}
    private static ToolResults Run(string id,IVisionImage image,Action<ToolParameters>? configure=null){var p=ToolRegistry.CreateParameters(id);p.ResolvedInputImage=image;configure?.Invoke(p);return (ToolResults)ToolRegistry.CreateToolInstance(id)!.GetType().GetMethod("Run")!.Invoke(ToolRegistry.CreateToolInstance(id),new object[]{p})!;}
    private static FrozenImageResource Gray(byte[] bytes,RegionRun[]? domain=null)=>new(new(bytes.Length,1,bytes.Length,PixelLayout.Gray8),new(Guid.NewGuid(),"gray"),BlobSchema.ImageFrame,bytes,domain??new[]{new RegionRun(0,0,bytes.Length-1)});
    private static void Case(string name,Action action){action();passed++;Console.WriteLine("通过："+name);}
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        yield return root;
        for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in Visuals(System.Windows.Media.VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Throws(Action action,string message){try{action();}catch(Exception e){var actual=e is TargetInvocationException?e.InnerException!:e;if(actual.Message.Contains(message))return;throw;}throw new Exception("未拒绝："+message);}
    private sealed class CheckLogger:ILogger{public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(message);}}
}
