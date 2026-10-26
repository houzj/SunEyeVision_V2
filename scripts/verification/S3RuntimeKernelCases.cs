using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    private static readonly BlobSettings Basic = new() { IsAreaFilterEnabled = false, OutputGrayImage = false, IsBlobCountFilterEnabled = false };
    private static int Main()
    {
        try
        {
            Case("XML设置编辑操作唯一强类型模型，非法整批提案不污染草稿",()=>{
                var definition=new BlobTool();var provider=(IToolSettingsFields)definition;
                var original=new BlobSettings();var draft=new ToolSettingsEditSession(definition,original);
                Require(provider.SettingsSchema.Inputs.Count==54&&provider.SettingsSchema.Outputs.Count==0,"设置字段数或职责错误");
                string Field(string label)=>provider.SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName==label).FieldId;
                var threshold=Field(BlobParameterDefinition.Instance.Label(nameof(BlobSettings.Threshold)));
                var upper=Field(BlobParameterDefinition.Instance.Label(nameof(BlobSettings.UpperThreshold)));
                var mode=Field(BlobParameterDefinition.Instance.Label(nameof(BlobSettings.SegmentationMode)));
                draft.ApplyFields(new Dictionary<string,object?>{{threshold,73L},{upper,211L},{mode,nameof(BlobSegmentationMode.DoubleThreshold)}});
                Require(((BlobSettings)draft.CaptureSettings()).Threshold==73&&original.Threshold==128
                    &&draft.ReadFields().Get<long>(threshold)==73,"字段编辑产生第二模型或污染原配置");
                Throws<ArgumentException>(()=>draft.ApplyFields(new Dictionary<string,object?>{{threshold,220L},{upper,210L}}));
                Throws<ArgumentException>(()=>draft.ApplyFields(new Dictionary<string,object?>{{threshold,300L}}));
                Throws<ArgumentException>(()=>draft.ApplyFields(new Dictionary<string,object?>{{mode,"undefined"}}));
                Throws<ArgumentException>(()=>draft.ApplyFields(new Dictionary<string,object?>{{"unknown",1L}}));
                Require(((BlobSettings)draft.CaptureSettings()).Threshold==73&&((BlobSettings)draft.CaptureSettings()).UpperThreshold==211,"拒绝提案污染私有状态");
                var rule=provider.SettingsSchema.Presentation!.Fields.Single(f=>f.Layout?.Page=="结果显示"&&f.Layout.PairWith is not null&&f.DisplayName=="Blob数量下限");
                draft.ApplyFields(new Dictionary<string,object?>{{rule.FieldId,19d}});
                Require(draft.ReadFields().Get<double>(rule.FieldId)==19&&original.Judgments.SequenceEqual(new BlobSettings().Judgments),"判定编辑修改原记录");
            });
            Case("宿主中立入口保留模型身份、冻结参数并在无UI回调时清理",()=>{
                using var resources=new FrozenOutputResources();
                foreach(var asynchronous in new[]{false,true})
                {
                    var implementation=new RunnerDefinition(asynchronous);IToolDefinition definition=implementation;
                    Require(definition.SettingsType==typeof(RunnerSettings)&&definition.OutputType==typeof(RunnerOutput)
                        &&definition.CreateSettings() is RunnerSettings,"宿主入口未公开实际强类型身份");
                    var settings=new RunnerSettings{Values=new[]{7}};var inputs=new InputValues(Array.Empty<Port>(),new Dictionary<string,object?>());
                    var previous=SynchronizationContext.Current;var blocked=new NoPostContext();SynchronizationContext.SetSynchronizationContext(blocked);
                    ToolOutcome outcome;
                    try
                    {
                        var task=ToolRuntimeExecutor.ExecuteAsync(definition,settings,inputs,new RunnerContext(resources)).AsTask();
                        settings.Values[0]=99;
                        Require(task.Wait(TimeSpan.FromSeconds(3)),"宿主入口或异步session释放等待UI回调");
                        outcome=task.GetAwaiter().GetResult();
                    }
                    finally{SynchronizationContext.SetSynchronizationContext(previous);}
                    Require(blocked.Posts==0&&outcome.Success&&outcome.Output is RunnerOutput {Value:7}
                        &&ReferenceEquals(outcome.Output,implementation.Session!.Output)&&implementation.Session.Disposed,
                        "入口转换了结果模型、未冻结参数或未清理会话");
                }
            });
            Case("宿主入口拒绝错型/非法参数/未知输入输出与取消，执行和清理异常均保留",()=>{
                using var resources=new FrozenOutputResources();var definition=new RunnerDefinition(false);
                var inputs=new InputValues(Array.Empty<Port>(),new Dictionary<string,object?>());var context=new RunnerContext(resources);
                Throws<ArgumentException>(()=>ToolRuntimeExecutor.ExecuteAsync(definition,new object(),inputs,context).GetAwaiter().GetResult());
                var invalid=ToolRuntimeExecutor.ExecuteAsync(definition,new RunnerSettings{Values=Array.Empty<int>()},inputs,context).GetAwaiter().GetResult();
                Require(!invalid.Success&&definition.Session is null,"非法参数仍创建算法会话");
                Throws<ArgumentException>(()=>ToolRuntimeExecutor.ExecuteAsync(definition,new RunnerSettings(),inputs,context,new[]{new OutputValueRequest("unknown")}).GetAwaiter().GetResult());
                var extra=new InputValues(new[]{new Port("unknown",DataType.Integer)},new Dictionary<string,object?>{{"unknown",1L}});
                Throws<ArgumentException>(()=>ToolRuntimeExecutor.ExecuteAsync(definition,new RunnerSettings(),extra,context).GetAwaiter().GetResult());
                using var cancelled=new CancellationTokenSource();cancelled.Cancel();
                var stopped=ToolRuntimeExecutor.ExecuteAsync(definition,new RunnerSettings(),inputs,new RunnerContext(resources,cancelled.Token)).GetAwaiter().GetResult();
                Require(!stopped.Success&&definition.Session is null,"已取消调用仍创建会话");
                var broken=new RunnerDefinition(false,true);
                try{ToolRuntimeExecutor.ExecuteAsync(broken,new RunnerSettings(),inputs,context).GetAwaiter().GetResult();throw new Exception("异常未传播");}
                catch(AggregateException errors){Require(errors.InnerExceptions.Count==2&&broken.Session!.Disposed,"执行/清理异常丢失或会话未释放");}
            });
            Case("工作流图像默认源必须显式指向图像输入，并参与冻结声明身份",()=>{
                var ports=new[]{new Port("image",DataType.Image),new Port("number",DataType.Integer)};
                var direct=new ToolSchema(ports,Array.Empty<Port>());
                var fallback=new ToolSchema(ports,Array.Empty<Port>(),workflowImageInputId:"image");
                Require(direct.Fingerprint!=fallback.Fingerprint&&new BlobTool().Describe(Basic).WorkflowImageInputId==BlobTool.ImageInput,
                    "默认图像源未由Runtime声明或未参与身份冻结");
                Throws<ArgumentException>(()=>new ToolSchema(ports,Array.Empty<Port>(),workflowImageInputId:"number"));
                Throws<ArgumentException>(()=>new ToolSchema(ports,Array.Empty<Port>(),workflowImageInputId:"unknown"));
            });
            Case("XML唯一默认、用户设置重开及强类型读取",()=>{
                var settings=new BlobSettings();
                Require(settings.Threshold==128&&settings.UpperThreshold==255&&settings.FilterAreaMin==10&&settings.FilterAreaMax==999999
                    &&settings.BlobCountLimit==1000&&settings.OutputGrayImage&&!settings.OutputBinaryImage&&settings.Judgments.Count==12,"XML默认值未生效");
                var changed=settings with{Threshold=73,UpperThreshold=211,FilterAreaMax=9999999,BlobCountLimit=993,
                    Judgments=new[]{new BlobJudgment("per-blob-area",true,true,19,750)}};
                var restored=JsonSerializer.Deserialize<BlobSettings>(JsonSerializer.Serialize(changed))!;
                Require(restored.Threshold==73&&restored.UpperThreshold==211&&restored.FilterAreaMax==9999999&&restored.BlobCountLimit==993
                    &&restored.Judgments.SequenceEqual(changed.Judgments),"XML默认覆盖已保存的用户值");
                Throws<InvalidDataException>(()=>BlobParameterDefinition.Instance.Default<double>(nameof(BlobSettings.Threshold)));
            });
            Case("Runtime直接调用按XML拒绝越界与伪造判定，不依赖SDK界面校验",()=>{
                IToolDefinition definition=new BlobTool();
                var invalid=new[] {
                    new BlobSettings { BlobCountLimit=20001 },
                    new BlobSettings { FilterAreaMax=10000000 },
                    new BlobSettings { SegmentationMode=BlobSegmentationMode.DoubleThreshold,Threshold=200,UpperThreshold=199 },
                    new BlobSettings { Judgments=new[]{new BlobJudgment("white-ratio",false,true,0,101)} },
                    new BlobSettings { Judgments=new[]{new BlobJudgment("average-circularity",false,true,-0.01,1)} },
                    new BlobSettings { Judgments=new[]{new BlobJudgment("per-blob-area",true,false,0,1,BlobJudgmentKind.Boolean)} },
                    new BlobSettings { Judgments=new[]{new BlobJudgment("output-not-empty",true,false,0,0,BlobJudgmentKind.Boolean)} },
                    new BlobSettings { Judgments=new[]{new BlobJudgment("unknown",false,false,0,1)} }
                };
                using var resources=new FrozenOutputResources();
                var inputs=new InputValues(Array.Empty<Port>(),new Dictionary<string,object?>());
                foreach(var settings in invalid)
                {
                    Require(settings.Validate().Count>0,"Runtime接受了XML边界外的参数");
                    var result=ToolRuntimeExecutor.ExecuteAsync(definition,settings,inputs,new RunnerContext(resources)).GetAwaiter().GetResult();
                    Require(!result.Success&&result.Output is null&&result.Diagnostics.All(d=>d.Code=="BLOB_SETTINGS_INVALID"),
                        "非法设置未在输入准入及算法执行前拒绝");
                }
                Require((new BlobSettings{BlobCountLimit=20000,FilterAreaMax=9999999}).Validate().Count==0,"XML合法边界被拒绝");
                Require((new BlobSettings{IsBlobCountFilterEnabled=false,BlobCountLimit=0}).Validate().Count==0,"关闭的筛选仍参与执行校验");
                var absent=new BlobSettings{Judgments=null!};
                Require(absent.Validate().Count>0,"空判定集合未报告字段错误");
                Throws<ArgumentException>(()=>absent.Freeze());
                Throws<ArgumentException>(()=>(new BlobSettings{Judgments=new BlobJudgment[]{null!}}).Freeze());
            });
            Case("参数XML完整性与非法元数据在加载阶段拒绝",()=>{
                string Xml(){using var s=typeof(BlobTool).Assembly.GetManifestResourceStream("BlobAnalysis.BlobParameters.xml")!;return new StreamReader(s).ReadToEnd();}
                void Reject(Action<XElement> mutate)
                {
                    var root=XElement.Parse(Xml());mutate(root);
                    using var stream=new MemoryStream(Encoding.UTF8.GetBytes(root.ToString()));
                    Throws<InvalidDataException>(()=>BlobParameterDefinition.Read(stream));
                }
                XElement Field(XElement root,string id)=>root.Elements("Group").Elements("Parameter").Single(x=>(string?)x.Attribute("id")==id);
                Reject(r=>Field(r,"Threshold").Remove());
                Reject(r=>Field(r,"Threshold").SetAttributeValue("id","Unknown"));
                Reject(r=>Field(r,"Threshold").Parent!.Add(new XElement(Field(r,"Threshold"))));
                Reject(r=>Field(r,"Threshold").SetAttributeValue("type","Number"));
                Reject(r=>Field(r,"Threshold").SetAttributeValue("default","256"));
                Reject(r=>Field(r,"Threshold").SetAttributeValue("default","oops"));
                Reject(r=>Field(r,"Threshold").SetAttributeValue("minimum","NaN"));
                Reject(r=>Field(r,"Threshold").SetAttributeValue("minimum","oops"));
                Reject(r=>Field(r,"Threshold").SetAttributeValue("decimals","0.5"));
                Reject(r=>Field(r,"Threshold").SetAttributeValue("increment","0"));
                Reject(r=>Field(r,"SortMode").SetAttributeValue("default","Missing"));
                Reject(r=>r.Add(new XElement("Unknown")));
                Reject(r=>r.Attribute("workflowImageInputId")!.Remove());
                Reject(r=>r.SetAttributeValue("workflowImageInputId","input.unknown"));
                Reject(r=>r.Element("Judgments")!.Elements().First().Remove());
                Reject(r=>r.Element("Judgments")!.Elements().First().SetAttributeValue("perItem","true"));
                Reject(r=>r.Element("Judgments")!.Elements().First().SetAttributeValue("minimum","Infinity"));
                Reject(r=>r.Element("Judgments")!.Elements().First().SetAttributeValue("unknown","1"));
                using var dtd=new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY y 'x'>]><BlobParameters schemaVersion='1'/>"));
                Throws<InvalidDataException>(()=>BlobParameterDefinition.Read(dtd));
            });
            Case("显示XML声明源校验、样式与表格投影",()=>{
                var definition=BlobDisplayDefinition.Instance;var schema=new BlobTool().Describe(Basic);
                Require(definition.Graphics.Count==5&&definition.Texts.Count==1&&definition.Table.Columns.Count==8
                    &&definition.Table.Validate(schema).Count==0,"显示定义未加载或字段映射无效");
                Require(definition.Graphics.Single(x=>x.Key=="inspection-roi").InputSource
                    &&definition.Graphics.Single(x=>x.Key=="blobs").Source.PortId==BlobSchema.BlobsPort
                    &&definition.Texts.Single().Source.MemberFieldPath.SequenceEqual(new[]{"area"}),"输入/输出显示来源混淆");
                string Xml(){using var s=typeof(BlobTool).Assembly.GetManifestResourceStream("BlobAnalysis.BlobDisplay.xml")!;return new StreamReader(s).ReadToEnd();}
                void Reject(Action<XElement> mutate)
                {
                    var root=XElement.Parse(Xml());mutate(root);
                    using var stream=new MemoryStream(Encoding.UTF8.GetBytes(root.ToString()));
                    Throws<InvalidDataException>(()=>BlobDisplayDefinition.Read(stream,schema));
                }
                Reject(r=>r.Element("Graphics")!.Elements().First().SetAttributeValue("member","unknown"));
                Reject(r=>r.Element("Graphics")!.Elements().First().SetAttributeValue("okColor","#GG0000"));
                Reject(r=>r.Element("Graphics")!.Elements().First().SetAttributeValue("opacity","1.1"));
                Reject(r=>r.Element("Graphics")!.Add(new XElement(r.Element("Graphics")!.Elements().First())));
                Reject(r=>r.Element("Texts")!.Elements().First().SetAttributeValue("port","unknown"));
                Reject(r=>r.Element("Table")!.SetAttributeValue("identity","missing"));
                Reject(r=>r.Element("Table")!.Elements().First().SetAttributeValue("member","unknown"));
                Reject(r=>r.Element("Table")!.Elements().First().SetAttributeValue("decimals","16"));
            });
            Case("最终集合、双孔边界、NG保留和请求输出", () => {
                using var factory = new FrozenOutputResources();
                using var input = Image(32,24,(r,c) => r>=2&&r<=16&&c>=2&&c<=16 && !(r>=5&&r<=7&&c>=5&&c<=7) && !(r>=10&&r<=12&&c>=10&&c<=12) || r>=20&&r<=21&&c>=25&&c<=26);
                using var result = new BlobSession().Execute(Basic with { Judgments = new[] {new BlobJudgment("per-blob-area", true, true, 10, 9999)} }, input, factory, requested: BlobOutputRequest.GrayImage|BlobOutputRequest.BinaryImage|BlobOutputRequest.Boundary);
                Require(result.Count==2 && result.TotalArea==211 && result.Measurements[0].Area==207 && !result.IsPass, "最终集合或NG被过滤");
                var ring=result.Measurements[0];
                Require(ring.Boundary.Count==3 && ring.Boundary.Count(p=>p.Role=="hole"&&p.ParentId is not null)==2, "双孔边界拓扑丢失");
                var values=result.ToValues();
                Require(values.ContainsKey(BlobSchema.BlobsPort)&&result.GrayImage is not null&&result.BinaryImage is not null, "请求资源被开关抑制");
                Require((double)values[BlobSchema.Field(30)]! ==4&&(double)values[BlobSchema.Field(31)]! ==207,"公开最小/最大面积没有来自同一最终集合");
                var definition=new BlobTool();var declaration=definition.Describe(Basic);
                var session=definition.CreateSession();
                var outcome=session.ExecuteAsync(new(Basic,
                    new InputValues(declaration.Inputs,new Dictionary<string,object?>{{BlobTool.ImageInput,input}}),
                    new Context(factory),new[]{new OutputValueRequest(BlobSchema.BlobsPort,new[]{"contour-lossy"})})).GetAwaiter().GetResult();
                session.DisposeAsync().GetAwaiter().GetResult();
                using(var projected=outcome.Output)
                {
                    Require(outcome.Success&&projected is not null&&outcome.Diagnostics.Any(d=>d.Code=="BLOB_CONTOUR_LOSSY"),"多孔投影缺少明确诊断");
                    var records=(SequenceValue)projected!.ToValues()[BlobSchema.BlobsPort]!;
                    Require(((RecordValue)records[0]!).Get<bool>("contour-lossy")&&((RecordValue)records[0]!).Contains("boundary"),"请求投影状态未生成完整边界或有损标记");
                }
                using var limited = new BlobSession().Execute(Basic with {IsBlobCountFilterEnabled=true,BlobCountLimit=1},input,factory);
                Require(limited.Count==1 && limited.TotalArea==207 && Math.Abs(limited.WhitePixelRatio-211.0/768*100)<1e-9, "排序限数、最终统计或分割白像素语义错误");
            });
            Case("空ROI不回退满图、实际Domain内反阈值", () => {
                using var factory = new FrozenOutputResources();
                using var input=Image(8,8,(r,c)=>r>=2&&r<=3&&c>=2&&c<=3,Enumerable.Range(2,4).Select(r=>new RegionRun(r,2,5)).ToArray());
                using var empty=new FrozenRegionResource(BlobSchema.ImageFrame,input.Frame,Array.Empty<RegionRun>());
                using var zero=new BlobSession().Execute(Basic,input,factory,empty);
                Require(zero.Count==0 && zero.WhitePixelRatio==0,"有效空ROI被当成Unset");
                using var inv=new BlobSession().Execute(Basic with { Type=BlobThresholdMode.BinaryInv },input,factory);
                Require(inv.TotalArea==12 && inv.WhitePixelRatio==18.75,"反阈值超出实际Domain或分母改变");
            });
            Case("平移和旋转遵守列X行Y、像素中心", () => {
                using var factory = new FrozenOutputResources(); using var input=Image(16,16,(_,_)=>true);
                using var roi=new FrozenRegionResource(BlobSchema.ImageFrame,input.Frame,new[]{new RegionRun(2,3,3)});
                using var translated=new BlobSession().Execute(Basic with {IsPositionCorrectionEnabled=true},input,factory,roi,pose:Pose(1,0,0,1,4,1));
                Require(translated.TotalArea==1&&translated.Measurements[0].CentroidX==7&&translated.Measurements[0].CentroidY==3,"平移行列错误");
                using var rotated=new BlobSession().Execute(Basic with {IsPositionCorrectionEnabled=true},input,factory,roi,pose:Pose(0,1,-1,0,10,0));
                Require(rotated.TotalArea==1&&rotated.Measurements[0].CentroidX==8&&rotated.Measurements[0].CentroidY==3,"旋转像素中心错误");
            });
            Case("同源准入、取消、输出scope独立释放", () => {
                using var factory=new FrozenOutputResources(); using var input=Image(8,8,(_,_)=>true);
                using var other=Image(8,8,(_,_)=>true);
                using var wrong=new FrozenRegionResource(BlobSchema.ImageFrame,other.Frame,new[]{new RegionRun(0,0,0)});
                Throws<InvalidDataException>(()=>new BlobSession().Execute(Basic,input,factory,wrong));
                using var cancelled=new CancellationTokenSource();cancelled.Cancel();
                Throws<OperationCanceledException>(()=>new BlobSession().Execute(Basic,input,factory,cancellation:cancelled.Token));
                var result=new BlobSession().Execute(Basic,input,factory,requested:BlobOutputRequest.GrayImage);
                using var region=result.Region.AcquireReader();using var pixels=result.GrayImage!.AcquirePixels();
                result.Dispose();factory.Dispose();input.Dispose();
                Require(region.RunCount==8&&pixels.GetRow(0)[0]==200,"独立reader被scope释放影响");
                Throws<ObjectDisposedException>(()=>result.Region.AcquireReader());
                region.Dispose();pixels.Dispose();
                Throws<ObjectDisposedException>(()=>region.ReadRuns(0,1));
                Throws<ObjectDisposedException>(()=>pixels.GetRow(0));
            });
            Case("奇宽灰度、BGR和BGRA往返保留有效像素及实际Domain",()=> {
                foreach(var format in new[]{PixelLayout.Gray8,PixelLayout.Bgr24,PixelLayout.Bgra32})
                {
                    int channels=format==PixelLayout.Gray8?1:format==PixelLayout.Bgr24?3:4;
                    var layout=new ImageLayout(3,2,3*channels+3,format);
                    var bytes=Enumerable.Repeat((byte)238,layout.BufferLength).ToArray();
                    for(int row=0;row<2;row++)for(int i=0;i<3*channels;i++)bytes[row*layout.RowStrideBytes+i]=(byte)(row*40+i*7);
                    var frame=new ImageFrameAddress(Guid.NewGuid(),"input");
                    using var source=new FrozenImageResource(layout,frame,BlobSchema.ImageFrame,bytes,new[]{new RegionRun(1,1,2)});
                    using var factory=new FrozenOutputResources();using var native=HalconResourceBridge.CopyImage(source);
                    var output=HalImageSnapshotSource.Capture(native,frame,factory);
                    using var reader=output.AcquirePixels();using var domain=reader.AcquireDomainReader();
                    Require(reader.Layout.Format==format&&domain.RunCount==1&&domain.ReadRuns(0,1).Span[0]==new RegionRun(1,1,2),"颜色或Domain丢失");
                    for(int row=0;row<2;row++)Require(reader.GetRow(row).Slice(0,3*channels).SequenceEqual(bytes.AsSpan(row*layout.RowStrideBytes,3*channels)),"有效颜色像素或透明度改变");
                }
            });
            Case("资源scope互不清理、冻结输入保留共享像素",()=> {
                using var root=new FrozenOutputResources();using var first=root.CreateScope();using var second=root.CreateScope();
                using var source=Image(4,4,(_,_)=>true);
                var a=first.RetainInput(source);var b=second.RetainInput(source);
                using var original=source.AcquirePixels();using var ar=a.AcquirePixels();using var br=b.AcquirePixels();
                Require(original.GetRow(0).Overlaps(ar.GetRow(0))&&ar.GetRow(0).Overlaps(br.GetRow(0)),"冻结输入被不必要地复制");
                first.Dispose();source.Dispose();Throws<ObjectDisposedException>(()=>a.AcquirePixels());
                Require(b.CopyPixels()[0]==200&&ar.GetRow(0)[0]==200,"一个scope清理另一个scope或既有reader");
            });
            Case("声明执行与下游资源请求覆盖隐藏输出、无效路径明确拒绝",()=> {
                using var factory=new FrozenOutputResources(); using var image=Image(8,8,(_,_)=>true);
                var definition=new BlobTool();var schema=definition.Describe(Basic);
                var inputs=new InputValues(schema.Inputs,new Dictionary<string,object?>{[BlobTool.ImageInput]=image});
                var context=new Context(factory);
                var requests=new[]{new OutputValueRequest(BlobSchema.Field(5)),new OutputValueRequest(BlobSchema.Field(6)),new OutputValueRequest(BlobSchema.BlobsPort,new[]{"boundary"})};
                var session=definition.CreateSession();
                try
                {
                    var outcome=session.ExecuteAsync(new(Basic,inputs,context,requests)).GetAwaiter().GetResult();
                    Require(outcome.Success&&outcome.Output is not null,"声明执行失败："+string.Join(";",outcome.Diagnostics.Select(x=>x.Message)));
                    using var result=outcome.Output!;
                    Require(result.GrayImage is not null&&result.BinaryImage is not null&&result.Measurements[0].Boundary.Count>0,"下游请求被显示开关抑制");
                    var values=new InputValues(schema.Outputs,result.ToValues());
                    Require(values.Get<long>(BlobSchema.Field(11))==1&&values.Get<double[]>(BlobSchema.Field(19))[0]==64,"声明/统计/数组不一致");
                    var invalid=session.ExecuteAsync(new(Basic,inputs,context,new[]{new OutputValueRequest(BlobSchema.BlobsPort,new[]{"unknown"})})).GetAwaiter().GetResult();
                    Require(!invalid.Success&&invalid.Output is null,"未知消费字段静默回退");
                    session.DisposeAsync().GetAwaiter().GetResult();
                    var closed=session.ExecuteAsync(new(Basic,inputs,context)).GetAwaiter().GetResult();
                    Require(!closed.Success,"关闭的会话继续执行");
                }
                finally { session.DisposeAsync().GetAwaiter().GetResult(); }
            });
            Case("运行层直接引用无资源实现库、进程无SDK和WPF",()=> {
                var refs=typeof(BlobSession).Assembly.GetReferencedAssemblies().Select(x=>x.Name!).ToArray();
                Require(!refs.Contains("SunEyeVision.Plugin.Runtime"),"Blob直接依赖资源实现库");
                Require(!AppDomain.CurrentDomain.GetAssemblies().Any(x=>Forbidden(x.GetName().Name!)),"纯算法执行加载SDK/WPF");
            });
            Console.WriteLine($"S3真实内核：{_passed}组通过；仅证明运行层，不是生产装配或S3整体完成。");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static FrozenImageResource Image(int w,int h,Func<int,int,bool> bright,RegionRun[]? domain=null)
    {
        var bytes=new byte[w*h];for(int r=0;r<h;r++)for(int c=0;c<w;c++)bytes[r*w+c]=bright(r,c)?(byte)200:(byte)0;
        return new(new(w,h,w,PixelLayout.Gray8),new(Guid.NewGuid(),"input"),BlobSchema.ImageFrame,bytes,domain??Enumerable.Range(0,h).Select(r=>new RegionRun(r,0,w-1)).ToArray());
    }
    private static RecordValue Pose(double a,double b,double c,double d,double x,double y)=>new(SpatialDeclarations.Affine2D,new Dictionary<string,object?>{
        ["m11"]=a,["m12"]=b,["m21"]=c,["m22"]=d,["offset-x"]=x,["offset-y"]=y,["from"]=BlobSchema.ImageFrame.Id,["to"]=BlobSchema.ImageFrame.Id});
    private sealed class Context(IOutputResources resources):IToolExecutionContext
    {
        public RunIdentity Identity { get; }=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"blob",Guid.NewGuid());
        public CancellationToken Cancellation=>default;
        public IOutputResources Resources=>resources;
        public void Log(string message) { }
    }
    private sealed record RunnerSettings:IToolSettings<RunnerSettings>
    {
        public RunnerSettings(){}public int[] Values{get;init;}=new[]{7};
        public RunnerSettings Freeze()=>this with{Values=Values.ToArray()};
    }
    private sealed record RunnerOutput(int Value):IToolOutput
    {public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>{{"value",(long)Value}};}
    private sealed class RunnerDefinition(bool asynchronous,bool fail=false):IToolDefinition<RunnerSettings,RunnerOutput>
    {
        public RunnerSession? Session;public string ToolId=>"runner";
        public ToolSchema Describe(RunnerSettings settings)=>new(Array.Empty<Port>(),new[]{new Port("value",DataType.Integer)});
        public IReadOnlyList<Diagnostic> Validate(RunnerSettings settings)=>settings.Values.Length==1?Array.Empty<Diagnostic>():new[]{new Diagnostic("invalid","one value required")};
        public ToolCapabilities Capabilities(RunnerSettings settings)=>ToolCapabilities.None;
        public IToolSession<RunnerSettings,RunnerOutput> CreateSession()=>Session=new(asynchronous,fail);
    }
    private sealed class RunnerSession(bool asynchronous,bool fail):IToolSession<RunnerSettings,RunnerOutput>
    {
        public bool Disposed;public RunnerOutput? Output;
        public ValueTask<ToolOutcome<RunnerOutput>> ExecuteAsync(ToolInvocation<RunnerSettings> invocation)
        {
            if(fail)throw new InvalidOperationException("execute");
            return asynchronous?CompleteAsync(invocation):ValueTask.FromResult(ToolOutcome<RunnerOutput>.Completed(Output=new(invocation.Settings.Values[0])));
        }
        private async ValueTask<ToolOutcome<RunnerOutput>> CompleteAsync(ToolInvocation<RunnerSettings> invocation)
        {await Task.Delay(15).ConfigureAwait(false);return ToolOutcome<RunnerOutput>.Completed(Output=new(invocation.Settings.Values[0]));}
        public async ValueTask DisposeAsync(){await Task.Delay(15).ConfigureAwait(false);Disposed=true;if(fail)throw new InvalidOperationException("cleanup");}
    }
    private sealed class RunnerContext(IOutputResources resources,CancellationToken cancellation=default):IToolExecutionContext
    {
        public RunIdentity Identity{get;}=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"runner",Guid.NewGuid());
        public CancellationToken Cancellation=>cancellation;public IOutputResources Resources=>resources;public void Log(string message){}
    }
    private sealed class NoPostContext:SynchronizationContext
    {public int Posts;public override void Post(SendOrPostCallback callback,object? state)=>Interlocked.Increment(ref Posts);}
    private static bool Forbidden(string name)=>name is "SunEyeVision.Plugin.SDK" or "SunEyeVision.UI" or "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Xaml";
    private static void Case(string name,Action run){run();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private static void Throws<T>(Action run) where T:Exception {try{run();}catch(T){return;}throw new Exception("缺少预期拒绝："+typeof(T).Name);}
}
