using System;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Tool.BlobAnalysis;

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
                ResolvedInputImage = image, Threshold = 100, MaxValue = 255,
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
            Console.WriteLine("S3算法局部检查：3通过。工程拆分/中立运行/完整轮廓与装配仍未核收。");
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
