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
            HOperatorSet.SetSystem("max_connection",1);var rejected=false;
            try{using var output=Run(input,settings);}catch(InvalidOperationException e)when(e.Message.Contains("BLOB_HALCON_MAX_CONNECTION")){rejected=true;}
            Require(rejected,"允许HALCON全局截断候选");HOperatorSet.GetSystem("max_connection",out var unchanged);using(unchanged)Require(unchanged.I==1,"擅自修改全局限制");
            Console.WriteLine("通过：拒绝全局截断且不修改全局限制");HOperatorSet.SetSystem("max_connection",0);
            var separate=new byte[256];separate[34]=200;for(var y=8;y<10;y++)for(var x=8;x<10;x++)separate[y*16+x]=200;
            using var multiple=HalImage.CreateFromBytes(separate,16,16,1);
            using(var output=Run(multiple,settings with{SortMode=BlobSort.AreaDesc,IsBlobCountFilterEnabled=true,BlobCountLimit=1}))Require(output.Count==1&&output.TotalArea==4,"没有先排序再限数");
            Console.WriteLine("通过：完整候选筛选排序后才执行数量限制");
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
            Console.WriteLine("通过：空结果执行完成且业务判定NG");Console.WriteLine("S3 Runtime算法检查：5组通过；不代表完整宿主、VM视觉或人工验收。");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{HOperatorSet.SetSystem("max_connection",limit);HOperatorSet.SetSystem("tsp_neighborhood",previous);limit.Dispose();previous.Dispose();}
    }
    private static void Require(bool value,string error){if(!value)throw new Exception(error);}
}
