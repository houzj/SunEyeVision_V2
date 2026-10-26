using System;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Tool.BlobAnalysis;
using System.Reflection;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

// Reuse the guarded official-Release harness entry; this is S3, not an S2 pass claim.
internal static class S2DocumentEditingCases
{
    [STAThread]
    static int Main()
    {
        HOperatorSet.GetSystem("tsp_neighborhood", out var originalNeighborhood);
        HOperatorSet.GetSystem("max_connection", out var originalLimit);
        try
        {
            HOperatorSet.SetSystem("max_connection", 0);
            var pixels = new byte[8 * 8]; pixels[2 * 8 + 2] = pixels[3 * 8 + 3] = 200;
            using var image = HalImage.CreateFromBytes(pixels, 8, 8, 1);
            BlobAnalysisParameters Settings() => new() {
                ResolvedInputImage = image, Threshold = 100, UpperThreshold = 255,
                IsAreaFilterEnabled = false, IsCircularityFilterEnabled = false,
                IsRectangularityFilterEnabled = false, IsBlobCountFilterEnabled = false, OutputContours = false
            };
            HOperatorSet.SetSystem("tsp_neighborhood", 4);
            var joined = new BlobAnalysisTool().Run(Settings());
            try { Require(joined.IsSuccess && joined.FilteredBlobCount == 1 && joined.FilteredBlobs[0].Area == 2,
                "Blob必须固定8邻域连接对角像素：" + joined.ErrorMessage); }
            finally { joined.Resources.Dispose(); }
            HOperatorSet.GetSystem("tsp_neighborhood", out var restored);
            using (restored) Require(restored.I == 4, "成功后没有恢复调用线程原4邻域");
            Console.WriteLine("通过：实际HALCON以8邻域连通，退出恢复调用线程4邻域");

            HOperatorSet.SetSystem("max_connection", 1);
            var rejected = new BlobAnalysisTool().Run(Settings());
            try { Require(!rejected.IsSuccess && rejected.ErrorMessage?.Contains("BLOB_HALCON_MAX_CONNECTION") == true,
                "全局连通上限不能截断候选后冒称成功"); }
            finally { rejected.Resources.Dispose(); }
            HOperatorSet.GetSystem("max_connection", out var unchangedLimit);
            using (unchangedLimit) Require(unchangedLimit.I == 1, "算法擅自改了HALCON全局上限");
            HOperatorSet.GetSystem("tsp_neighborhood", out var unchangedNeighborhood);
            using (unchangedNeighborhood) Require(unchangedNeighborhood.I == 4, "拒绝执行污染线程设置");
            Console.WriteLine("通过：连通全局上限异常明确拒绝，不擅改全局或线程设置");

            HOperatorSet.SetSystem("max_connection", 0);
            var separate = new byte[16 * 16];
            separate[2 * 16 + 2] = 200;
            for(int y = 8; y < 10; y++) for(int x = 8; x < 10; x++) separate[y * 16 + x] = 200;
            using var secondImage = HalImage.CreateFromBytes(separate, 16, 16, 1);
            var sorted = Settings(); sorted.ResolvedInputImage = secondImage;
            sorted.SortMode = BlobSortMode.AreaDesc; sorted.IsBlobCountFilterEnabled = true; sorted.BlobCountLimit = 1;
            var final = new BlobAnalysisTool().Run(sorted);
            try { Require(final.IsSuccess && final.FilteredBlobCount == 1 && final.FilteredBlobs[0].Area == 4,
                "应先生成全部候选、按面积降序，再取数量上限，而非提前取到小区域：" + final.ErrorMessage); }
            finally { final.Resources.Dispose(); }
            Console.WriteLine("通过：候选完整生成，筛选排序后才执行数量限制");
            var ringPixels=new byte[32*24];
            for(int r=2;r<=16;r++)for(int c=2;c<=16;c++)
                if(!(r>=5&&r<=7&&c>=5&&c<=7)&&!(r>=10&&r<=12&&c>=10&&c<=12))ringPixels[r*32+c]=200;
            for(int r=20;r<=21;r++)for(int c=25;c<=26;c++)ringPixels[r*32+c]=200;
            using var rings=HalImage.CreateFromBytes(ringPixels,32,24,1);
            var delegated=Settings();delegated.ResolvedInputImage=rings;delegated.OutputContours=true;delegated.OutputBinaryImage=true;
            var areaRule=delegated.ResultPresentation.Judgments.Single(x=>x.Key=="per-blob-area");
            areaRule.Enabled=true;areaRule.Min=10;areaRule.Max=9999;
            var actual=new BlobAnalysisTool().Run(delegated);
            try
            {
                Require(actual.IsSuccess&&actual.FilteredBlobCount==2&&actual.FilteredTotalArea==211&&!actual.IsPass,"兼容入口丢失最终集合/NG："+actual.ErrorMessage);
                var core=(BlobOutput?)typeof(BlobAnalysisResults).GetProperty("RuntimeOutput",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(actual);
                Require(core is not null&&core.Measurements[0].Boundary.Count==3&&core.Measurements[0].Area==207,"入口未持有真实Runtime完整边界");
                Require(actual.BlobAreas.SequenceEqual(core!.Measurements.Select(x=>x.Area))&&actual.BlobRegions!.PixelCount==211,"兼容输出数组/区域与内核不一致");
                using var retained=core.Region.AcquireReader();actual.Resources.Dispose();
                Require(retained.RunCount>0,"旧结果释放破坏已有Runtime读者");
            }
            finally{actual.Resources.Dispose();}
            Console.WriteLine("通过：旧入口委派真实内核，双孔/NG/数组/区域一致，旧结果释放由scope承接");
            using var dark=HalImage.CreateFromBytes(new byte[64],8,8,1);
            var empty=Settings();empty.ResolvedInputImage=dark;
            empty.ResultPresentation.Judgments.Single(x=>x.Key=="output-not-empty").Enabled=true;
            var noBlob=new BlobAnalysisTool().Run(empty);
            try{Require(noBlob.IsSuccess&&!noBlob.IsPass&&noBlob.FilteredBlobCount==0,"布尔非空判定被错当成范围判定");}
            finally{noBlob.Resources.Dispose();}
            Console.WriteLine("通过：空结果保持执行成功/业务NG，兼容布尔判断语义");
            Console.WriteLine("S3算法/兼容入口检查：5组通过；可选编辑器、正式发现装配及完整阶段仍未核收。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            HOperatorSet.SetSystem("max_connection", originalLimit);
            HOperatorSet.SetSystem("tsp_neighborhood", originalNeighborhood);
            originalLimit.Dispose(); originalNeighborhood.Dispose();
        }
    }
    static void Require(bool condition, string error) { if(!condition) throw new Exception(error); }
}
