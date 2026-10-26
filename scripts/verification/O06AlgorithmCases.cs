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
using SunEyeVision.Tool.BlobAnalysis;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;
using Mode=SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSegmentationMode;

internal static class S2DocumentEditingCases
{
    private static readonly BlobSettings Basic=new(){IsAreaFilterEnabled=false,IsBlobCountFilterEnabled=false,OutputGrayImage=false};
    private static int passed;
    [STAThread] private static int Main()
    {
        try
        {
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var plugins=new PluginManager();plugins.LoadPlugins(Path.Combine(product,"plugins"));PluginLoader.MarkAsLoaded();
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
            Case("Facade nullable实际阈值与正式声明、canonical图像来源保留",()=>{
                using var image=HalImage.CreateFromBytes(new byte[]{0,255,255,0},4,1,1);
                var p=new BlobAnalysisParameters{ResolvedInputImage=image,SegmentationMode=SunEyeVision.Tool.BlobAnalysis.BlobSegmentationMode.None,IsAreaFilterEnabled=false,OutputBinaryImage=true};
                var result=new BlobAnalysisTool().Run(p);try{
                    Require(result.IsSuccess&&result.ThresholdMinUsed is null&&result.ThresholdMaxUsed is null,result.ErrorMessage??"None门面失败");
                    foreach(var name in new[]{"ThresholdMinUsed","ThresholdMaxUsed"}){var field=result.GetOutputVariables().Single(f=>f.Name==name);Require(field.AllowsNull&&BindingClrAdapter.DescribeOutput(field).Nullable&&field.RuntimeValue is null,"Nullable正式声明丢失");}
                    Require(result.GetOutputVariables().Single(f=>f.Name=="GrayImage").RuntimeValue is IVisionImage,"SDK图像类型被替换");
                    Require(result.TryGetDeclaredOutputValue(BlobSchema.Field(5),out var canonical)&&canonical is IReadOnlyImage,"canonical override丢失");
                }finally{result.Resources.Dispose();}
                var many=new byte[16*16];many[2*16+2]=255;for(int y=8;y<10;y++)for(int x=8;x<10;x++)many[y*16+x]=255;
                using var multiple=HalImage.CreateFromBytes(many,16,16,1);
                var limited=new BlobAnalysisParameters{ResolvedInputImage=multiple,SegmentationMode=SunEyeVision.Tool.BlobAnalysis.BlobSegmentationMode.None,IsAreaFilterEnabled=false,IsBlobCountFilterEnabled=true,BlobCountLimit=1,SortMode=BlobSortMode.AreaDesc};
                var final=new BlobAnalysisTool().Run(limited);try{
                    Require(final.IsSuccess&&final.FilteredBlobCount==1&&final.MinimumArea==4&&final.MaximumArea==4,"Facade min/max没有来自限数后的最终集合");
                    var runtime=typeof(BlobAnalysisResults).GetProperty("RuntimeOutput",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(final)!;
                    Require((double)runtime.GetType().GetProperty("MinimumArea")!.GetValue(runtime)! ==final.MinimumArea&&(double)runtime.GetType().GetProperty("MaximumArea")!.GetValue(runtime)! ==final.MaximumArea,"Facade/Runtime min/max不一致");
                    foreach(var name in new[]{"MinimumArea","MaximumArea"}){var declared=final.GetOutputVariables().Single(v=>v.Name==name);Require(Convert.ToDouble(((Array)declared.RuntimeValue!).GetValue(0))==4,"正式SDK声明min/max与最终集合不一致");}
                }finally{final.Resources.Dispose();}
            });
            Case("退出字段/绑定明确拒绝，合法来源同名不误拒绝",()=>{
                var policy=typeof(SolutionRepository).Assembly.GetType("SunEyeVision.Workflow.Persistence.SolutionCompatibilityAdapter")!;
                var prepare=policy.GetMethod("Prepare",BindingFlags.Static|BindingFlags.Public)!;
                var diagnosticsType=prepare.GetParameters()[1].ParameterType;
                JsonObject Document()=>JsonNode.Parse("{\"Id\":\"doc\",\"SchemaVersion\":2,\"Workflows\":[{\"Id\":\"flow\",\"Name\":\"flow\",\"Nodes\":[{\"Id\":\"blob\",\"ToolType\":\"BlobAnalysis\",\"Parameters\":{},\"ParamSettings\":{\"Settings\":[]}}],\"Connections\":[]}]}")!.AsObject();
                void Check(JsonObject doc)=>prepare.Invoke(null,new object[]{doc,Activator.CreateInstance(diagnosticsType)!,false});
                var old=Document();old["Workflows"]![0]!["Nodes"]![0]!["Parameters"]!["IsMorphologyEnabled"]=true;Throws(()=>Check(old),"旧分割/预处理/形态学");
                var oldBinding=Document();oldBinding["Workflows"]![0]!["Nodes"]![0]!["ParamSettings"]!["Settings"]!.AsArray().Add(new JsonObject{["ParameterName"]="MaxValue",["BindingType"]=0});Throws(()=>Check(oldBinding),"旧字段绑定");
                var valid=Document();valid["Workflows"]![0]!["Nodes"]![0]!["ParamSettings"]!["Settings"]!.AsArray().Add(new JsonObject{["ParameterName"]="Threshold",["BindingType"]=0,["SourceProperty"]="MaxValue"});Check(valid);
            });
            Case("单/双/None保存重开且旧参数不再写出",()=>{
                foreach(var mode in Enum.GetValues<SunEyeVision.Tool.BlobAnalysis.BlobSegmentationMode>()){
                    var p=ToolRegistry.CreateParameters("BlobAnalysis");Set(p,"SegmentationMode",mode.ToString());Set(p,"Threshold",100);Set(p,"UpperThreshold",200);
                    var json=JsonSerializer.Serialize<ToolParameters>(p,ParameterTypeRegistry.SerializationOptions);
                    Require(!json.Contains("IsMorphologyEnabled")&&!json.Contains("Preprocess")&&!json.Contains("\"MaxValue\""),"旧参数仍保存");
                    var restored=JsonSerializer.Deserialize<ToolParameters>(json,ParameterTypeRegistry.SerializationOptions)!;
                    Require(restored.GetType().GetProperty("SegmentationMode")!.GetValue(restored)!.ToString()==mode.ToString()&&(int)restored.GetType().GetProperty("UpperThreshold")!.GetValue(restored)! ==200&&restored.Validate().IsValid,"新分割配置不能保存重开");
                    var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"save-"+mode);Directory.CreateDirectory(folder);
                    var repository=new SolutionRepository(Path.Combine(folder,"recovery"));var solution=new Solution{Id="new-"+mode,SchemaVersion=2};var flow=new Workflow("flow","flow");
                    flow.Nodes.Add(new WorkflowNodeBase("blob","blob","blob","BlobAnalysis"){Parameters=p});solution.Workflows.Add(flow);
                    var path=Path.Combine(folder,"new.solution");Require(repository.Save(solution,path),repository.LastError??"新配置不能保存");
                    Require(repository.LoadCandidate(path).Candidate is not null,repository.LastError??"新配置不能真实重开");
                    var legacy=JsonNode.Parse(File.ReadAllText(path))!;legacy["Workflows"]![0]!["Nodes"]![0]!["Parameters"]!["IsPreprocessingEnabled"]=true;
                    var legacyPath=Path.Combine(folder,"old.solution");File.WriteAllText(legacyPath,legacy.ToJsonString());var before=File.ReadAllBytes(legacyPath);
                    Require(repository.LoadCandidate(legacyPath).Candidate is null&&before.SequenceEqual(File.ReadAllBytes(legacyPath)),"真实仓库未拒绝旧已启用设置或改写源文件");
                }
            });
            Case("严格形态学拒绝隐式128、非byte和彩色，保全部操作及Circle",()=>{
                using var bad=HalImage.CreateFromBytes(new byte[]{0,200,255,0},4,1,1);var rejected=Run("Morphology",bad);try{Require(!rejected.IsSuccess&&rejected.ErrorMessage!.Contains("BINARY_INPUT_VALUES"),"形态学仍按128接受灰度");}finally{rejected.Resources?.Dispose();}
                using var raw16=new HImage();raw16.GenImageConst("uint2",4,4);Throws(()=>HalconImageAdmission.RequireBinary(raw16),"BINARY_INPUT_FORMAT");
                using var color=HalImage.CreateFromBytes(Enumerable.Repeat((byte)255,48).ToArray(),4,4,3);var rgb=Run("Morphology",color);try{Require(!rgb.IsSuccess&&rgb.ErrorMessage!.Contains("BINARY_INPUT_FORMAT"),"形态学先转灰接受彩色");}finally{rgb.Resources?.Dispose();}
                using var thresholdInput=HalImage.CreateFromBytes(new byte[]{0,200,255,0},4,1,1);
                var thresholdResult=Run("ImageThreshold",thresholdInput,p=>{Set(p,"Method","Binary");Set(p,"MaxValue",200);});
                try{
                    var rendered=(HalImage)thresholdResult.GetType().GetProperty("OutputImage")!.GetValue(thresholdResult)!;
                    var mask=(HalImage)thresholdResult.GetType().GetProperty("BinaryMask")!.GetValue(thresholdResult)!;
                    Throws(()=>HalconImageAdmission.RequireBinary(rendered.Unwrap()),"BINARY_INPUT_VALUES");HalconImageAdmission.RequireBinary(mask.Unwrap());
                }finally{thresholdResult.Resources?.Dispose();}
                var pixels=new byte[32*32];for(int y=4;y<28;y++)for(int x=4;x<28;x++)pixels[y*32+x]=255;
                using var binary=HalImage.CreateFromBytes(pixels,32,32,1);
                foreach(var operation in new[]{"Erode","Dilate","Open","Close","Gradient","TopHat","BlackHat"}){
                    var result=Run("Morphology",binary,p=>{Set(p,"Operation",operation);Set(p,"KernelShape","Circle");});try{Require(result.IsSuccess,result.ErrorMessage??operation);var output=(IVisionImage)result.GetType().GetProperty("BinaryMask")!.GetValue(result)!;using var snap=HalImageSnapshotSource.Capture(((HalImage)output).Unwrap(),new(Guid.NewGuid(),"morph"));HalconImageAdmission.RequireBinary(snap);}finally{result.Resources?.Dispose();}
                }
                var seed=new byte[256*256];seed[128*256+128]=255;using var point=HalImage.CreateFromBytes(seed,256,256,1);
                foreach(var (width,height) in new[]{(2,2),(100,2),(101,2),(2,100),(2,101),(100,101),(101,100)}){
                    var result=Run("Morphology",point,p=>{Set(p,"Operation","Dilate");Set(p,"KernelShape","Rect");Set(p,"KernelWidth",width);Set(p,"KernelHeight",height);});
                    try{Require(result.IsSuccess,result.ErrorMessage??"尺寸被错误拒绝");Require(Convert.ToInt64(result.GetType().GetProperty("TotalArea")!.GetValue(result))==width*height,"偶轴被取整或尺寸被截断");}finally{result.Resources?.Dispose();}
                }
                var evenCircle=Run("Morphology",point,p=>{Set(p,"Operation","Dilate");Set(p,"KernelShape","Circle");Set(p,"KernelWidth",2);Set(p,"KernelHeight",2);});
                try{
                    HOperatorSet.GenCircle(out var originalCircle,1.0,1.0,1.0);using(originalCircle)using(var expectedCircle=new HRegion(originalCircle))using(var expectedArea=expectedCircle.AreaCenter(out HTuple row,out HTuple column))using(row)using(column){
                        Console.WriteLine("HALCON原Circle半径1实际离散面积="+expectedArea.D);
                        Require(evenCircle.IsSuccess&&Convert.ToInt64(evenCircle.GetType().GetProperty("TotalArea")!.GetValue(evenCircle))==expectedArea.D,"Circle偶数尺寸未按原native半径1语义承接");
                    }
                }finally{evenCircle.Resources?.Dispose();}
            });
            Case("ImageFilter均衡化有效域统计与其他滤波承接",()=>{
                var bytes=Enumerable.Range(0,64).Select(i=>(byte)(i*4)).ToArray();using var image=HalImage.CreateFromBytes(bytes,8,8,1);
                foreach(var filter in new[]{"Blur","Gaussian","MedianBlur","HistogramEqualization"}){
                    var result=Run("ImageFilter",image,p=>Set(p,"FilterTypeValue",filter));try{Require(result.IsSuccess,result.ErrorMessage??filter);Require(result.GetType().GetProperty("OutputImage")!.GetValue(result) is IVisionImage,"滤波无图像");}finally{result.Resources?.Dispose();}
                }
                var scoped=Run("ImageFilter",image,p=>{Set(p,"FilterTypeValue","HistogramEqualization");((RoiParameters)p).InspectionRois.AddInput(Roi.CreateLocal("small",new RotatedRect2d(3.5,3.5,3,3,0)));});
                try{
                    Require(scoped.IsSuccess,scoped.ErrorMessage??"有效域均衡失败");var output=(HalImage)scoped.GetType().GetProperty("OutputImage")!.GetValue(scoped)!;
                    using var native=output.Unwrap().Clone();using var domain=native.GetDomain();using var area=domain.AreaCenter(out HTuple row,out HTuple column);using(row)using(column)Require(area.D==16,"均衡化输出丢失ROI Domain");
                    HOperatorSet.EquHistoImage(image.Unwrap(),out var fullObj);using(fullObj)using(var full=new HImage(fullObj)){
                        using var actual=native.GetGrayval(2,2);using var unrestricted=full.GetGrayval(2,2);Require(actual.D!=unrestricted.D,"均衡化错误地使用全图直方图");
                    }
                }finally{scoped.Resources?.Dispose();}
            });
            Case("有限不等条件、三模式字段显示与模式常量约束",()=>{
                if(Application.Current is null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                var p=new BlobAnalysisParameters();var accessors=p.GetConfigurationFields();
                var view=new DeclaredFieldsView(accessors.Select(f=>(f.Port,f.Presentation,(ParameterSetting)new ConstantSetting(f.Read()))),(_,_)=>{},_=>null);
                DeclaredFieldEditor Field(string path)=>view.Fields.Single(f=>f.FieldId==accessors.Single(a=>a.StoragePath==path).Port.Id);
                var mode=Field("SegmentationMode");var lower=Field("Threshold");var upper=Field("UpperThreshold");var type=Field("Type");
                Require(lower.Visibility==Visibility.Visible&&upper.Visibility==Visibility.Collapsed,"Single字段显示错误");
                mode.TrySetConstant("DoubleThreshold");Require(lower.Visibility==Visibility.Visible&&upper.Visibility==Visibility.Visible&&type.Visibility==Visibility.Visible,"Double隐藏下限或极性");
                mode.TrySetConstant("None");Require(lower.Visibility==Visibility.Collapsed&&upper.Visibility==Visibility.Collapsed&&type.Visibility==Visibility.Collapsed,"None仍显示阈值");
                Require(accessors.Single(a=>a.StoragePath=="SortMode").Presentation.ChoiceLabels["None"]=="无","Sort.None被误标成不二值化");
                mode.TrySetConstant("DoubleThreshold");
                Logical(upper).OfType<ToggleButton>().Single(button=>Equals(button.ToolTip,"切换为订阅")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Require(upper.HasInvalidText&&!view.TryValidate(out _)&&view.HasInvalidActiveFields,"活动Upper未选订阅来源不阻塞");
                mode.TrySetConstant("SingleThreshold");Require(upper.Visibility==Visibility.Collapsed&&upper.HasInvalidText&&view.TryValidate(out _)&&!view.HasInvalidActiveFields,"Single被隐藏Upper pending阻塞或清掉草稿");
                mode.TrySetConstant("None");Require(view.TryValidate(out _)&&upper.Setting is ConstantSetting{Value:255L},"None隐字段pending改写了常量或阻塞");
                mode.TrySetConstant("DoubleThreshold");Require(!view.TryValidate(out _)&&view.HasInvalidActiveFields,"切回Double没有恢复pending阻塞");
                Logical(upper).OfType<ToggleButton>().Single(button=>Equals(button.ToolTip,"切换为数值编辑")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Require(view.TryValidate(out _),"恢复数值后仍阻塞");
                Require(BlobInputContract.Fields.Single(f=>f.LegacyPath=="SegmentationMode").Port.EditModes==FieldEditModes.Constant,"模式允许订阅");
                Require(p.ValidateStoredConfiguration(new[]{"SegmentationMode"}).Count>0,"模式订阅未被存储拒绝");
                var controlling=new Port("controller",DataType.Text,EditModes:FieldEditModes.Constant|FieldEditModes.Reference,Choices:new TextChoiceConstraint(new[]{"a","b"}));
                var dependent=new Port("dependent",DataType.Integer);var source=new ReferenceSetting(new OutputReference("node","out",DataType.Text));
                var referenced=new DeclaredFieldsView(new[]{(controlling,new FieldPresentation("controller","mode","g",0,FieldEditorKind.Choice,new[]{"a","b"}),(ParameterSetting)source),(dependent,new FieldPresentation("dependent","value","g",1,FieldEditorKind.Scalar){Layout=new("p","s",new("controller","a",true))},(ParameterSetting)new ConstantSetting(1L))},(_,_)=>{});
                Require(referenced.Fields[1].Visibility==Visibility.Visible,"未解析引用条件应显式可见");
                referenced.Fields[1].TrySetConstant("invalid");Require(!referenced.TryValidate(out _)&&referenced.HasInvalidActiveFields,"未解析引用条件错误未参与校验");
                var hiddenByContainer=new DeclaredFieldsView(new[]{(new Port("advanced",DataType.Integer),new FieldPresentation("advanced","advanced","g",0,FieldEditorKind.Scalar){Layout=new("other-tab","advanced",Advanced:true)},(ParameterSetting)new ConstantSetting(1L)),(new Port("normal",DataType.Integer),new FieldPresentation("normal","normal","g",1,FieldEditorKind.Scalar){Layout=new("selected-tab","normal")},(ParameterSetting)new ConstantSetting(2L))},(_,_)=>{});
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
        var blob=Node("blob","BlobAnalysis");Set(blob.Parameters,"SegmentationMode","None");Set(blob.Parameters,"IsAreaFilterEnabled",false);Set(blob.Parameters,"OutputBinaryImage",true);Source(blob,"morphology","BinaryMask");flow.Nodes.Add(blob);
        var bytes=new byte[32*32];for(int y=6;y<26;y++)for(int x=6;x<26;x++)bytes[y*32+x]=200;
        using var input=HalImage.CreateFromBytes(bytes,32,32,1);var executor=new WorkflowExecutionEngine(engine,plugins,new CheckLogger());
        using var run=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,input)).GetAwaiter().GetResult();
        Require(run.Success,string.Join(";",run.Errors??new()));var result=run.NodeResults["blob"].Result!;
        Require((int)result.GetType().GetProperty("FilteredBlobCount")!.GetValue(result)! ==1&&result.GetType().GetProperty("ThresholdMinUsed")!.GetValue(result) is null&&(string)result.GetType().GetProperty("SegmentationModeUsed")!.GetValue(result)! =="None","真实链未消费严格二值");
        using var read=run.DeclaredOutputs.Single(o=>o.Identity.NodeId=="blob").AcquireReader();
        Require(read.Values!.Get<long?>(BlobSchema.Field(8)) is null,"Workflow nullable阈值丢失");
        var image=read.Values.Get<IReadOnlyImage>(BlobSchema.Field(6));var region=read.Values.Get<IReadOnlyRegion>(BlobSchema.RegionPort);Require(image.Frame==region.SourceFrame,"二值图/Region错帧");
        var required=Node("required-threshold","BlobAnalysis");flow.Nodes.Add(required);
        required.ParamSettings.SetSetting(new ParamSetting{ParameterName="Threshold",BindingType=BindingType.Binding,SourceNodeId="blob",SourceWorkflowId=flow.Id,SourceProperty="ThresholdMinUsed",
            ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference("blob",BlobSchema.Field(8),DataType.Integer,scopeId:flow.Id,storage:NumericStorage.Int32))});
        using var rejected=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,input)).GetAwaiter().GetResult();
        Require(!rejected.Success,"nullable阈值null被下游必需参数默认为0");
        Require(new[]{"filter","threshold","morphology","blob"}.All(id=>rejected.NodeResults.TryGetValue(id,out var prior)&&prior.Success),"nullable拒绝前四个节点出现了其他失败");
        Require(rejected.NodeResults.TryGetValue("required-threshold",out var missing)&&!missing.Success,"缺少目标Threshold失败记录");
        var reason=string.Join(";",missing!.ErrorMessages)+";"+missing.Result?.ErrorMessage;
        Console.WriteLine("nullable目标绑定实际拒绝："+reason);
        Require(reason.Contains("null",StringComparison.OrdinalIgnoreCase)&&(reason.Contains("Threshold",StringComparison.Ordinal)||reason.Contains("field:43bda03261df55b88f963bd18f2ba4d9",StringComparison.Ordinal)),"拒绝没有证明来自Threshold/null绑定："+reason);
    }
    private static void Source(WorkflowNodeBase target,string node,string property)=>target.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(target.Parameters,new ImageSourceReference(node,property,node,property));
    private static void Set(object p,string key,object value){var property=p.GetType().GetProperty(key)!;property.SetValue(p,property.PropertyType.IsEnum?Enum.Parse(property.PropertyType,(string)value):value);}
    private static ToolResults Run(string id,IVisionImage image,Action<ToolParameters>? configure=null){var p=ToolRegistry.CreateParameters(id);p.ResolvedInputImage=image;configure?.Invoke(p);return (ToolResults)ToolRegistry.CreateToolInstance(id)!.GetType().GetMethod("Run")!.Invoke(ToolRegistry.CreateToolInstance(id),new object[]{p})!;}
    private static FrozenImageResource Gray(byte[] bytes,RegionRun[]? domain=null)=>new(new(bytes.Length,1,bytes.Length,PixelLayout.Gray8),new(Guid.NewGuid(),"gray"),BlobSchema.ImageFrame,bytes,domain??new[]{new RegionRun(0,0,bytes.Length-1)});
    private static void Case(string name,Action action){action();passed++;Console.WriteLine("通过："+name);}
    private static IEnumerable<DependencyObject> Logical(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Throws(Action action,string message){try{action();}catch(Exception e){var actual=e is TargetInvocationException?e.InnerException!:e;if(actual.Message.Contains(message))return;throw;}throw new Exception("未拒绝："+message);}
    private sealed class CheckLogger:ILogger{public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(message);}}
}
