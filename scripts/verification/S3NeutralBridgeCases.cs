using System;
using System.Linq;
using System.Runtime.InteropServices;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

internal static class S2DocumentEditingCases
{
    static int Main()
    {
        try
        {
            var frame = new ImageFrameAddress(Guid.NewGuid(), "S3-neutral-bridge");
            var pixels = Enumerable.Repeat((byte)173, 5 * 4).ToArray();
            var pointer = Marshal.AllocHGlobal(pixels.Length);
            IImagePixelReadLease reader;
            try
            {
                Marshal.Copy(pixels, 0, pointer, pixels.Length);
                using var image = new HImage(); image.GenImage1("byte", 5, 4, pointer);
                using var domain = new HRegion(1.0, 1.0, 2.0, 3.0);
                using var reduced = image.ReduceDomain(domain);
                using var snapshot = HalImageSnapshotSource.Capture(reduced, frame);
                reader = snapshot.AcquirePixels();
            }
            finally { Marshal.FreeHGlobal(pointer); }
            using (reader)
            {
                Require(reader.Frame == frame && reader.Layout.Width == 5 && reader.Layout.Height == 4 && reader.Layout.Format == PixelLayout.Gray8,
                    "像素布局或来源身份丢失");
                Require(reader.GetRow(2).ToArray().All(v => v == 173), "源HALCON或snapshot退出后reader像素失效");
                using var domainReader = reader.AcquireDomainReader();
                var runs = domainReader.ReadRuns(0, 4096).ToArray();
                Require(domainReader.SourceFrame == frame && runs.Length == 2 && runs.All(x => x.FirstColumn == 1 && x.LastColumnInclusive == 3)
                    && runs[0].Row == 1 && runs[1].Row == 2, "实际非满图Domain被填成全幅或身份丢失");
            }
            try { reader.GetRow(0); throw new Exception("已释放reader仍可读"); }
            catch (ObjectDisposedException) { }
            Console.WriteLine("通过：真实HALCON非满域像素/Domain同源，源与快照释放后独立reader有效，最后释放后拒绝读取");
            var references = typeof(HalImageSnapshotSource).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
            Require(references.Contains("SunEyeVision.Plugin.Contracts") && references.Contains("SunEyeVision.Plugin.Runtime")
                && !references.Any(Forbidden), "运行桥引用了SDK或WPF");
            Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a => Forbidden(a.GetName().Name!)),
                "无界面桥验证进程实际加载了SDK/WPF");
            Console.WriteLine("通过：物理运行桥真实执行，直接依赖与进程实际加载均无SDK/WPF");
            Console.WriteLine("S3中立桥：2通过。不是Blob整体无UI运行/装配通过。");
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static bool Forbidden(string name) => name == "SunEyeVision.Plugin.SDK" || name == "PresentationFramework" || name == "PresentationCore"
        || name == "WindowsBase" || name == "System.Xaml" || name == "SunEyeVision.UI";
    static void Require(bool condition, string error) { if (!condition) throw new Exception(error); }
}
