using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        HOperatorSet.GetSystem("tsp_neighborhood",out var previous);HOperatorSet.GetSystem("max_connection",out var limit);
        try
        {
            using var resources=new FrozenOutputResources();
            BlobOutput Run(HalImage image,BlobSettings settings){using var snapshot=image.CaptureSnapshot(new(Guid.NewGuid(),"native-input"));return new BlobSession().Execute(settings,snapshot,resources);}
            var settings=new BlobSettings{Threshold=100,IsAreaFilterEnabled=false};
            HOperatorSet.SetSystem("max_connection",0);HOperatorSet.SetSystem("tsp_neighborhood",4);
            var bytes=new byte[64];bytes[18]=bytes[27]=200;using var input=HalImage.CreateFromBytes(bytes,8,8,1);
            using(var output=Run(input,settings))Require(output.Count==1&&output.TotalArea==2,"不是8邻域连接");
            HOperatorSet.GetSystem("tsp_neighborhood",out var restored);using(restored)Require(restored.I==4,"没有恢复调用线程设置");
            Console.WriteLine("通过：Runtime真实HALCON固定8邻域并恢复原线程设置");
            Require(new BlobSettings().Connectivity==BlobConnectivity.Connected_8,"原默认8连通未保留");
            foreach(var caller in new[]{4,8})foreach(var mode in new[]{BlobConnectivity.Connected_4,BlobConnectivity.Connected_8})
            {
                HOperatorSet.SetSystem("tsp_neighborhood",caller);
                using(var output=Run(input,settings with{Connectivity=mode}))Require(output.Count==(mode==BlobConnectivity.Connected_4?2:1)&&output.TotalArea==2&&output.Measurements.All(item=>item.Area==(mode==BlobConnectivity.Connected_4?1:2)),"4/8连通实际区域不符");
                HOperatorSet.GetSystem("tsp_neighborhood",out var after);using(after)Require(after.I==caller,"4/8连通没有恢复调用者线程");
            }
            HOperatorSet.SetSystem("tsp_neighborhood",4);
            var invalidConnectivity=settings with{Connectivity=(BlobConnectivity)6};
            var invalidRejected=false;try{using var output=Run(input,invalidConnectivity);}catch(ArgumentException){invalidRejected=true;}Require(invalidRejected,"非法连通值被执行");
            using(var canceled=new CancellationTokenSource())
            {canceled.Cancel();using var source=input.CaptureSnapshot(new(Guid.NewGuid(),"canceled"));try{using var output=new BlobSession().Execute(settings with{Connectivity=BlobConnectivity.Connected_8},source,resources,cancellation:canceled.Token);throw new Exception("取消未拒绝");}catch(OperationCanceledException){}}
            // 在作用域已安装后故意传入错误pose，证明异常finally恢复，而非只测预检拒绝。
            using(var source=input.CaptureSnapshot(new(Guid.NewGuid(),"pose-failure")))
            {try{using var output=new BlobSession().Execute(settings with{IsPositionCorrectionEnabled=true,Connectivity=BlobConnectivity.Connected_8},source,resources);throw new Exception("缺失pose未拒绝");}catch(ArgumentException){}}
            HOperatorSet.GetSystem("tsp_neighborhood",out var errorRestored);using(errorRestored)Require(errorRestored.I==4,"失败/取消污染调用者线程");
            Console.WriteLine("通过：显式4/8对角连通、默认8及非法/取消/安装后异常线程恢复");
            HOperatorSet.SetSystem("max_connection",1);var rejected=false;
            try{using var output=Run(input,settings);}catch(InvalidOperationException e)when(e.Message.Contains("BLOB_HALCON_MAX_CONNECTION")){rejected=true;}
            Require(rejected,"允许HALCON全局截断候选");HOperatorSet.GetSystem("max_connection",out var unchanged);using(unchanged)Require(unchanged.I==1,"擅自修改全局限制");
            Console.WriteLine("通过：拒绝全局截断且不修改全局限制");HOperatorSet.SetSystem("max_connection",0);
            var separate=new byte[256];separate[34]=200;for(var y=8;y<10;y++)for(var x=8;x<10;x++)separate[y*16+x]=200;
            using var multiple=HalImage.CreateFromBytes(separate,16,16,1);
            using(var output=Run(multiple,settings with{SortMode=BlobSortMode.SortModeDecend,BlobCountLimit=1}))Require(output.Count==1&&output.TotalArea==4,"没有先排序再限数");
            Console.WriteLine("通过：完整候选筛选排序后才执行数量限制");
            foreach(var feature in Enum.GetValues<BlobSortFeature>())foreach(var mode in Enum.GetValues<BlobSortMode>())
            {
                using var output=Run(multiple,settings with{SortFeature=feature,SortMode=mode});
                double Key(BlobMeasurement value)=>feature switch{BlobSortFeature.SortFeatureArea=>value.Area,BlobSortFeature.SortFeaturePerimeter=>value.Perimeter,BlobSortFeature.SortFeatureCircularity=>value.Circularity,BlobSortFeature.SortFeatureRect=>value.Rectangularity,BlobSortFeature.SortFeatureCentroidX=>value.CentroidX,_=>value.CentroidY};
                var values=output.Measurements.Select(Key).ToArray();Require(output.Count==2,"排序丢失候选");
                if(mode!=BlobSortMode.SortModeNotSort)Require(values.SequenceEqual(mode==BlobSortMode.SortModeAscend?values.OrderBy(value=>value):values.OrderByDescending(value=>value)),"正式特征/方向排序失败："+feature+"/"+mode);
                using var limited=Run(multiple,settings with{SortFeature=feature,SortMode=mode,BlobCountLimit=1});Require(limited.Measurements.Single().ElementId==output.Measurements[0].ElementId,"排序/限数不保元素身份");
                Require(output.Measurements.Select(value=>value.Label).SequenceEqual(new[]{1,2}),"排序后标签不对应最终序列");
            }
            using(var tied=Run(multiple,settings with{SortFeature=BlobSortFeature.SortFeatureRect,SortMode=BlobSortMode.SortModeNotSort}))
            using(var sorted=Run(multiple,settings with{SortFeature=BlobSortFeature.SortFeatureRect,SortMode=BlobSortMode.SortModeDecend}))
                Require(tied.Measurements.Select(value=>value.ElementId).SequenceEqual(sorted.Measurements.Select(value=>value.ElementId)),"等值排序不稳定");
            Console.WriteLine("通过：六真实特征×三方向、稳定同值、排序后限数及最终行身份");

            var ring=new byte[32*24];for(var y=2;y<=16;y++)for(var x=2;x<=16;x++)if(!(y>=5&&y<=7&&x>=5&&x<=7)&&!(y>=10&&y<=12&&x>=10&&x<=12))ring[y*32+x]=200;
            for(var y=20;y<=21;y++)for(var x=25;x<=26;x++)ring[y*32+x]=200;using var rings=HalImage.CreateFromBytes(ring,32,24,1);
            var judgments=settings.Judgments.Select(r=>r.Key=="per-blob-area"?r with{Enabled=true,Minimum=10,Maximum=9999}:r).ToArray();
            using(var output=Run(rings,settings with{OutputBoundary=true,Judgments=judgments}))
            {
                Require(output.Count==2&&output.TotalArea==211&&!output.IsPass&&output.Measurements[0].Boundary.Count==3,"多孔/判定丢失");
                using var reader=output.Region.AcquireReader();output.Dispose();Require(reader.RunCount>0,"输出退出破坏独立读者");
            }
            Console.WriteLine("通过：单一Runtime输出保留双孔边界/NG及独立Region读者");
            using var dark=HalImage.CreateFromBytes(new byte[64],8,8,1);
            using(var output=Run(dark,settings with{Judgments=settings.Judgments.Select(r=>r.Key=="output-not-empty"?r with{Enabled=true}:r).ToArray()}))Require(output.Count==0&&!output.IsPass,"空结果应为执行成功、业务NG");
            using(var frozen=input.CaptureSnapshot(new(Guid.NewGuid(),"formal-runtime-input")))
            {
                Require(SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.LoadPlugins(),"正式插件未接纳");
                var definition=new BlobTool();var inputs=new SunEyeVision.Plugin.Contracts.InputValues(definition.Describe().Inputs,new Dictionary<string,object?>{{BlobTool.ImageInput,frozen}});
                foreach(var invalid in new[]{settings with{FilterAreaMin=0},settings with{FilterAreaMax=1000000000},settings with{FilterCircularityMin=double.NaN},settings with{FilterCircularityMax=1.1},settings with{FilterRectangularityMin=-.1},settings with{FilterRectangularityMax=double.PositiveInfinity},settings with{IsAreaFilterEnabled=true,FilterAreaMin=30,FilterAreaMax=10},settings with{IsCircularityFilterEnabled=true,FilterCircularityMin=.8,FilterCircularityMax=.2},settings with{IsRectangularityFilterEnabled=true,FilterRectangularityMin=.9,FilterRectangularityMax=.1}})
                {
                    var hostRejected=false;try{SunEyeVision.Workflow.RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(invalid);}catch(ArgumentException){hostRejected=true;}Require(hostRejected,"正式配置入口没有拒绝同源非法常量");
                    using var outputResources=new FrozenOutputResources();var task=ToolRuntimeExecutor.ExecuteAsync(definition,invalid,inputs,new RuntimeContext(outputResources)).AsTask();Require(task.Wait(TimeSpan.FromSeconds(20)),"正式Runtime验证未结束");var outcome=task.GetAwaiter().GetResult();using var owned=outcome.Output as IDisposable;
                    Require(!outcome.Success&&outcome.Diagnostics.Any(item=>item.Code=="BLOB_SETTINGS_INVALID"),"正式Runtime入口接纳禁用筛选的非法常量："+invalid);
                }
                var retained=settings with{FilterAreaMin=30,FilterAreaMax=10,FilterCircularityMin=.8,FilterCircularityMax=.2,FilterRectangularityMin=.9,FilterRectangularityMax=.1};
                var retainedSettings=(BlobSettings)SunEyeVision.Workflow.RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(retained).CaptureSettings();Require(retainedSettings.FilterAreaMin==30&&retainedSettings.FilterAreaMax==10&&retainedSettings.FilterCircularityMin==.8&&retainedSettings.FilterCircularityMax==.2,"宿主未保留禁用合法倒序值");
                using var retainedResources=new FrozenOutputResources();var accepted=ToolRuntimeExecutor.ExecuteAsync(definition,retained,inputs,new RuntimeContext(retainedResources)).AsTask().GetAwaiter().GetResult();using var retainedOutput=accepted.Output as IDisposable;
                Require(accepted.Success,"禁用筛选合法端点倒序仍应保留，不提前套启用顺序");
                Require(retained.FilterAreaMin==30&&retained.FilterAreaMax==10&&retained.FilterCircularityMin==.8&&retained.FilterCircularityMax==.2,"运行改写禁用保存值");
            }
            Console.WriteLine("通过：正式Runtime入口核禁用筛选全部常量值域并保留合法倒序值");
            var maskEvidence=new List<object>();
            using(var pixels=HalImage.CreateFromBytes(new byte[]{0,1,99,100,101,254,255},7,1,1))
            using(var frozen=pixels.CaptureSnapshot(new(Guid.NewGuid(),"single-threshold-pixel-probe")))
            {
                var definition=new BlobTool();var inputs=new SunEyeVision.Plugin.Contracts.InputValues(definition.Describe().Inputs,new Dictionary<string,object?>{{BlobTool.ImageInput,frozen}});
                var cases=new[]{
                    (Threshold:0,Type:BlobThresholdMode.Binary,Expected:new byte[]{255,255,255,255,255,255,255}),
                    (Threshold:0,Type:BlobThresholdMode.BinaryInv,Expected:new byte[]{0,0,0,0,0,0,0}),
                    (Threshold:100,Type:BlobThresholdMode.Binary,Expected:new byte[]{0,0,0,255,255,255,255}),
                    (Threshold:100,Type:BlobThresholdMode.BinaryInv,Expected:new byte[]{255,255,255,0,0,0,0}),
                    (Threshold:255,Type:BlobThresholdMode.Binary,Expected:new byte[]{0,0,0,0,0,0,255}),
                    (Threshold:255,Type:BlobThresholdMode.BinaryInv,Expected:new byte[]{255,255,255,255,255,255,0})};
                foreach(var item in cases)
                {
                    using var resultResources=new FrozenOutputResources();var requested=settings with{Threshold=item.Threshold,Type=item.Type,OutputBinaryImage=true};
                    var outcome=ToolRuntimeExecutor.ExecuteAsync(definition,requested,inputs,new RuntimeContext(resultResources)).AsTask().GetAwaiter().GetResult();using var output=outcome.Output as IDisposable;
                    Require(outcome.Success&&outcome.Output is BlobOutput,"单阈值像素探针正式Runtime执行失败");var result=(BlobOutput)outcome.Output!;var actual=result.BinaryImage!.CopyPixels();
                    Require(actual.SequenceEqual(item.Expected),"本方单阈值掩膜不符：T="+item.Threshold+"；type="+item.Type+"；actual="+string.Join(",",actual));
                    Require(result.BinaryImage.Frame==frozen.Frame&&result.BinaryImage.EffectiveDomain.Frame==BlobSchema.ImageFrame&&result.ThresholdMinUsed==item.Threshold&&result.ThresholdMaxUsed==255&&result.TypeUsed==item.Type.ToString(),"像素掩膜与实际使用值或Frame不一致");
                    Require(result.TotalArea==item.Expected.Count(value=>value==255),"掩膜白像素与正式Region面积不一致");
                    maskEvidence.Add(new{threshold=item.Threshold,polarity=item.Type.ToString(),input=new byte[]{0,1,99,100,101,254,255},mask=actual,area=result.TotalArea,frame=frozen.Frame,coordinates=result.BinaryImage.EffectiveDomain.Frame});
                }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"single-threshold-pixel-masks.json"),System.Text.Json.JsonSerializer.Serialize(new{scope="本方SingleThreshold实测与自研比较合同；原VM方向、等号及0/255未证，不称等价",cases=maskEvidence},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("通过：正式Runtime单阈值T−1/T/T+1及0/255双极性六掩膜；仅本方合同不代VM等价");
            Console.WriteLine("通过：空结果执行完成且业务判定NG");Console.WriteLine("S3 Runtime算法检查：9组通过；不代表完整宿主、VM视觉或人工验收。");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{HOperatorSet.SetSystem("max_connection",limit);HOperatorSet.SetSystem("tsp_neighborhood",previous);limit.Dispose();previous.Dispose();}
    }
    private sealed class RuntimeContext(SunEyeVision.Plugin.Contracts.IOutputResources resources):SunEyeVision.Plugin.Contracts.IToolExecutionContext
    {
        public SunEyeVision.Plugin.Contracts.RunIdentity Identity{get;}=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"blob",Guid.NewGuid());
        public CancellationToken Cancellation=>default;public SunEyeVision.Plugin.Contracts.IOutputResources Resources=>resources;public void Log(string message){}
    }
    private static void Require(bool value,string error){if(!value)throw new Exception(error);}
}
