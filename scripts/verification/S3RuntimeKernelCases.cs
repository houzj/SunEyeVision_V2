using System.IO;
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
                var definition=new BlobDefinition();var declaration=definition.Describe(Basic);
                var session=definition.CreateSession();
                var outcome=session.ExecuteAsync(new(Basic,
                    new InputValues(declaration.Inputs,new Dictionary<string,object?>{{BlobDefinition.ImageInput,input}}),
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
                var definition=new BlobDefinition();var schema=definition.Describe(Basic);
                var inputs=new InputValues(schema.Inputs,new Dictionary<string,object?>{[BlobDefinition.ImageInput]=image});
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
    private static bool Forbidden(string name)=>name is "SunEyeVision.Plugin.SDK" or "SunEyeVision.UI" or "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Xaml";
    private static void Case(string name,Action run){run();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private static void Throws<T>(Action run) where T:Exception {try{run();}catch(T){return;}throw new Exception("缺少预期拒绝："+typeof(T).Name);}
}
