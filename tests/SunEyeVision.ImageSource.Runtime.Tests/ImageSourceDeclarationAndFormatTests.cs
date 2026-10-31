using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NSubstitute;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.ImageSource.Runtime;
using Xunit;

namespace SunEyeVision.ImageSource.Runtime.Tests;

/// <summary>Behavior retained from the retired image-load tests, expressed only in the current component and resource contracts.</summary>
public sealed class ImageSourceDeclarationAndFormatTests
{
    [Fact]
    public void Runtime_declares_catalog_stable_tool_identity_and_the_actual_component_role()
    {
        var tool = new ImageSourceTool();
        Assert.Equal("ImageLoad", tool.ToolId);
        Assert.Equal("图像源", tool.Catalog.DisplayName);
        Assert.Equal("采集", tool.Catalog.Category);
        Assert.Equal("image", tool.Catalog.IconKey);
        Assert.Equal(10, tool.Catalog.Order);
        Assert.False(string.IsNullOrWhiteSpace(tool.Catalog.Description));
        var component = Assert.Single(typeof(ImageSourceTool).Assembly.GetCustomAttributes<ToolComponentAttribute>());
        Assert.Equal("SunEyeVision.Tool.ImageSource.Runtime", component.Metadata.ComponentId);
        Assert.Equal(tool.ToolId, component.Metadata.ToolId);
        Assert.Equal(ToolComponentRole.Runtime, component.Metadata.Role);
        Assert.Equal(FoundationContract.Major, component.Metadata.ContractMajor);
        Assert.Equal(FoundationContract.Minor, component.Metadata.ContractMinor);
        Assert.Null(component.Metadata.CompanionToolId);
        Assert.Equal(typeof(ImageSourceTool).FullName, component.EntryType);
    }

    [Fact]
    public void Every_output_port_has_its_current_resource_or_scalar_type_and_no_legacy_result_wrapper()
    {
        var expected = new Dictionary<string, TypeRef>
        {
            [ImageSourceOutput.Image] = ImageSourceOutput.ImageType,
            [ImageSourceOutput.GrayImage] = ImageSourceOutput.GrayImageType,
            [ImageSourceOutput.Sequence] = TypeRef.ScalarOf(ScalarKind.Int64),
            [ImageSourceOutput.SourceKind] = TypeRef.ScalarOf(ScalarKind.Text),
            [ImageSourceOutput.DeviceId] = TypeRef.ScalarOf(ScalarKind.Text),
            [ImageSourceOutput.FileReference] = TypeRef.ScalarOf(ScalarKind.Text),
            [ImageSourceOutput.FileName] = TypeRef.ScalarOf(ScalarKind.Text),
            [ImageSourceOutput.FrameId] = TypeRef.ScalarOf(ScalarKind.Text),
            [ImageSourceOutput.Timestamp] = TypeRef.ScalarOf(ScalarKind.Text)
        };
        Assert.Equal(expected.Keys.OrderBy(x => x), ImageSourceOutput.Ports.Select(port => port.Id).OrderBy(x => x));
        foreach (var port in ImageSourceOutput.Ports)
        {
            Assert.Equal(expected[port.Id], port.Type);
            Assert.True(port.Required);
            Assert.False(port.Nullable);
        }
        var tool = new ImageSourceTool();
        Assert.Equal(ImageSourceOutput.Ports, tool.ResultSchema.Outputs);
        Assert.Equal(ImageSourceOutput.Ports, ImageSourceTool.Schema.Outputs);
        Assert.Null(ImageSourceTool.Schema.WorkflowImageInputId);
    }

    [Theory]
    [InlineData("original")]
    [InlineData("mono8")]
    [InlineData("rgb24")]
    public async Task Camera_color_input_ignores_local_format_without_changing_metadata_or_domain(string format)
    {
        using var resources = new FrozenOutputResources();
        var sourceFrame = new ImageFrameAddress(Guid.NewGuid(), "camera:color-fixture");
        var domain = resources.CreateRegion(new CoordinateFrame("pixels", CoordinateDomain.Image, CoordinateUnit.Pixel), sourceFrame,
            new[] { new RegionRun(0, 0, 1), new RegionRun(1, 1, 1) });
        // The public pixel contract is BGR, including padding; these asymmetric colors expose red/blue swaps.
        // The two color samples have exact standard grayscale values 47 and 208; the third is neutral 80.
        byte[] original = { 150, 0, 100, 105, 255, 155, 20, 20, 20, 201, 202, 203,
                            40, 40, 40, 80, 80, 80, 60, 60, 60, 211, 212, 213 };
        var borrowed = resources.CreateImage(new ImageLayout(3, 2, 12, PixelLayout.Bgr24), sourceFrame, domain, original);
        var context = Context(resources);
        var input = CameraInput(borrowed, 37L);
        await using var session = new ImageSourceTool().CreateSession();
        var outcome = await session.ExecuteAsync(new(new ImageSourceSettings { SourceKind = "camera", CameraDeviceId = "device", OutputFormat = format }, input, context));
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(x => x.Message)));
        using var output = outcome.Output!;
        var values = output.ToValues();
        var image = Assert.IsAssignableFrom<IReadOnlyImage>(((RecordValue)values[ImageSourceOutput.Image]!).Get<IReadOnlyImage>(ImageSourceOutput.ImageData));
        var gray = Assert.IsAssignableFrom<IReadOnlyImage>(((RecordValue)values[ImageSourceOutput.GrayImage]!).Get<IReadOnlyImage>(ImageSourceOutput.GrayData));
        Assert.Equal(PixelLayout.Bgr24, image.Layout.Format);
        Assert.Equal(PixelLayout.Gray8, gray.Layout.Format);
        using var imagePixels = image.AcquirePixels();
        using var grayPixels = gray.AcquirePixels();
        Assert.Equal(new byte[] {
            (byte)((76*100+150*0+30*150)>>8),
            (byte)((76*155+150*255+30*105)>>8)
        }, grayPixels.GetRow(0)[..2].ToArray());
        Assert.Equal((byte)80, grayPixels.GetRow(1)[1]);
        Assert.NotSame(image, gray);
        Assert.Equal(new byte[] { 150, 0, 100, 105, 255, 155 }, imagePixels.GetRow(0)[..6].ToArray());
        Assert.Equal(new byte[] { 80, 80, 80 }, imagePixels.GetRow(1).Slice(3, 3).ToArray());
        AssertImageIdentityAndDomain(image, context.Identity, ImageSourceOutput.Image);
        AssertImageIdentityAndDomain(gray, context.Identity, ImageSourceOutput.GrayImage);
        Assert.NotEqual(sourceFrame, image.Frame);
        Assert.Equal(original, borrowed.CopyPixels());
        Assert.Equal(sourceFrame, borrowed.Frame);
        Assert.Equal("camera", Assert.IsType<string>(values[ImageSourceOutput.SourceKind]));
        Assert.Equal("device", Assert.IsType<string>(values[ImageSourceOutput.DeviceId]));
        Assert.Equal("frame-123", Assert.IsType<string>(values[ImageSourceOutput.FrameId]));
        Assert.Equal("2026-10-28T01:02:03.4560000+00:00", Assert.IsType<string>(values[ImageSourceOutput.Timestamp]));
        Assert.Equal(string.Empty, Assert.IsType<string>(values[ImageSourceOutput.FileReference]));
        Assert.Equal(37L, Assert.IsType<long>(values[ImageSourceOutput.Sequence]));
        Assert.Equal(3L, Assert.IsType<long>(((RecordValue)values[ImageSourceOutput.Image]!).Get<long>(ImageSourceOutput.ImageWidth)));
        Assert.Equal(2L, Assert.IsType<long>(((RecordValue)values[ImageSourceOutput.Image]!).Get<long>(ImageSourceOutput.ImageHeight)));
        Assert.Equal(image.Layout.Format.ToString(), Assert.IsType<string>(((RecordValue)values[ImageSourceOutput.Image]!).Get<string>(ImageSourceOutput.ImageFormat)));
    }

    [Theory]
    [InlineData("original")]
    [InlineData("mono8")]
    [InlineData("rgb24")]
    public async Task Camera_gray_input_ignores_local_format_and_preserves_actual_domain(string format)
    {
        using var resources = new FrozenOutputResources();
        var sourceFrame = new ImageFrameAddress(Guid.NewGuid(), "camera:gray-fixture");
        var domain = resources.CreateRegion(new CoordinateFrame("pixels", CoordinateDomain.Image, CoordinateUnit.Pixel), sourceFrame,
            new[] { new RegionRun(0, 0, 1), new RegionRun(1, 1, 1) });
        var borrowed = resources.CreateImage(new ImageLayout(3, 2, 4, PixelLayout.Gray8), sourceFrame, domain,
            new byte[] { 10, 30, 99, 201, 77, 80, 88, 202 });
        var context = Context(resources);
        await using var session = new ImageSourceTool().CreateSession();
        var outcome = await session.ExecuteAsync(new(new ImageSourceSettings { SourceKind = "camera", CameraDeviceId = "device", OutputFormat = format }, CameraInput(borrowed, 9L), context));
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(x => x.Message)));
        using var output = outcome.Output!;
        var values = output.ToValues();
        var image = Assert.IsAssignableFrom<IReadOnlyImage>(((RecordValue)values[ImageSourceOutput.Image]!).Get<IReadOnlyImage>(ImageSourceOutput.ImageData));
        var gray = Assert.IsAssignableFrom<IReadOnlyImage>(((RecordValue)values[ImageSourceOutput.GrayImage]!).Get<IReadOnlyImage>(ImageSourceOutput.GrayData));
        Assert.Equal(PixelLayout.Gray8, image.Layout.Format);
        using var pixels = image.AcquirePixels();
        using var grayPixels = gray.AcquirePixels();
        Assert.Equal(new byte[] { 10, 30 }, grayPixels.GetRow(0)[..2].ToArray());
        Assert.Equal((byte)80, grayPixels.GetRow(1)[1]);
        Assert.Same(image, gray);
        Assert.Equal(new byte[] { 10, 30 }, pixels.GetRow(0)[..2].ToArray());
        AssertImageIdentityAndDomain(image, context.Identity, ImageSourceOutput.Image);
        AssertImageIdentityAndDomain(gray, context.Identity, ImageSourceOutput.Image);
        Assert.Equal(new byte[] { 10, 30, 99, 201, 77, 80, 88, 202 }, borrowed.CopyPixels());
        Assert.Equal("frame-123", values[ImageSourceOutput.FrameId]);
        Assert.Equal("2026-10-28T01:02:03.4560000+00:00", values[ImageSourceOutput.Timestamp]);
    }

    [Fact]
    public async Task Camera_rgb_gray_conversion_matches_the_observed_vm_integer_formula()
    {
            const int width=256;
            var bgr=new byte[width*3];var expected=new byte[width];
            for(var i=0;i<width;i++)
            {
                var red=(byte)i;var green=(byte)((i*37+19)&255);var blue=(byte)((i*79+53)&255);
                bgr[3*i]=blue;bgr[3*i+1]=green;bgr[3*i+2]=red;
                expected[i]=(byte)((76*red+150*green+30*blue)>>8);
            }
            using var resources=new FrozenOutputResources();var context=Context(resources);
            var sourceFrame=new ImageFrameAddress(Guid.NewGuid(),"camera:formula-fixture");
            var domain=resources.CreateRegion(new CoordinateFrame("pixels",CoordinateDomain.Image,CoordinateUnit.Pixel),sourceFrame,
                new[]{new RegionRun(0,0,width-1)});
            var camera=resources.CreateImage(new ImageLayout(width,1,width*3,PixelLayout.Bgr24),sourceFrame,domain,bgr);
            await using var session=new ImageSourceTool().CreateSession();
            var outcome=await session.ExecuteAsync(new(new ImageSourceSettings {SourceKind="camera",CameraDeviceId="device"},CameraInput(camera,1L),context));
            Assert.True(outcome.Success,string.Join(";",outcome.Diagnostics.Select(item=>item.Message)));
            using var output=outcome.Output!;
            var gray=Assert.IsAssignableFrom<IReadOnlyImage>(((RecordValue)output.ToValues()[ImageSourceOutput.GrayImage]!).Get<IReadOnlyImage>(ImageSourceOutput.GrayData));
            Assert.Equal(expected,gray.CopyPixels());
    }

    [Theory]
    [InlineData(true,"original")]
    [InlineData(true,"mono8")]
    [InlineData(true,"rgb24")]
    [InlineData(false,"original")]
    [InlineData(false,"mono8")]
    [InlineData(false,"rgb24")]
    public async Task Local_file_formats_preserve_pixels_frame_and_decoded_domain(bool grayInput,string format)
    {
        var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")
            ??throw new InvalidOperationException("Missing validation root"),"local-formats",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path=Path.Combine(directory,"source.png");
            var encoder=new PngBitmapEncoder();
            var pixels=grayInput?new byte[]{20,90}:new byte[]{20,20,20,90,90,90};
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,1,96,96,
                grayInput?PixelFormats.Gray8:PixelFormats.Bgr24,null,pixels,grayInput?2:6)));
            using(var file=File.Create(path))encoder.Save(file);
            using var resources=new FrozenOutputResources();
            var context=Context(resources,path);
            var input=new InputValues(ImageSourceTool.Schema.Inputs,new Dictionary<string,object?> {
                [ImageSourceTool.LocalAddress]=path,[ImageSourceTool.Sequence]=7L });
            await using var session=new ImageSourceTool().CreateSession();
            var outcome=await session.ExecuteAsync(new(new ImageSourceSettings {OutputFormat=format},input,context));
            Assert.True(outcome.Success,string.Join(";",outcome.Diagnostics.Select(item=>item.Message)));
            using var output=outcome.Output!;
            var values=output.ToValues();
            var image=Assert.IsAssignableFrom<IReadOnlyImage>(((RecordValue)values[ImageSourceOutput.Image]!).Get<IReadOnlyImage>(ImageSourceOutput.ImageData));
            var gray=Assert.IsAssignableFrom<IReadOnlyImage>(((RecordValue)values[ImageSourceOutput.GrayImage]!).Get<IReadOnlyImage>(ImageSourceOutput.GrayData));
            var expectGray=format=="mono8"||(format=="original"&&grayInput);
            Assert.Equal(expectGray?PixelLayout.Gray8:PixelLayout.Bgr24,image.Layout.Format);
            Assert.Equal(expectGray?new byte[]{20,90}:new byte[]{20,20,20,90,90,90},image.CopyPixels());
            Assert.Equal(new byte[]{20,90},gray.CopyPixels());
            Assert.Equal(context.Identity,image.Frame.ProducerRun);
            Assert.Equal(context.Identity,gray.Frame.ProducerRun);
            Assert.Equal(ImageSourceOutput.Image,image.Frame.SourcePortId);
            Assert.Equal(expectGray?ImageSourceOutput.Image:ImageSourceOutput.GrayImage,gray.Frame.SourcePortId);
            Assert.Equal(expectGray,ReferenceEquals(image,gray));
            foreach(var result in new[]{image,gray})
            {
                Assert.Equal(result.Frame,result.EffectiveDomain.SourceFrame);
                using var domain=result.EffectiveDomain.AcquireReader();
                Assert.Equal(result.Frame,domain.SourceFrame);
                Assert.Equal(new[]{new RegionRun(0,0,1)},domain.ReadRuns(0,4096).ToArray());
            }
        }
        finally{Directory.Delete(directory,true);}
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Local_output_declares_empty_device_fields_even_if_unrelated_camera_metadata_is_present(bool staleMetadata)
    {
        var directory = Path.Combine(SunEyeVision.TestShared.ProductRuntimeTestHost.ProductRoot, "test-data", "image-source", "declaration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "local.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 17, 29 }, 2)));
            using (var file = File.Create(path)) encoder.Save(file);
            using var resources = new FrozenOutputResources();
            var context = Context(resources,path);
            var arguments = new Dictionary<string, object?> { [ImageSourceTool.LocalAddress] = path, [ImageSourceTool.Sequence] = 21L };
            if (staleMetadata)
            {
                arguments[ImageSourceTool.DeviceId] = "unrelated-camera";
                arguments[ImageSourceTool.DeviceFrameId] = "old-camera-frame";
                arguments[ImageSourceTool.DeviceTimestamp] = "old-camera-time";
            }
            await using var session = new ImageSourceTool().CreateSession();
            var outcome = await session.ExecuteAsync(new(new ImageSourceSettings { CameraDeviceId = "configured-but-local" }, new InputValues(ImageSourceTool.Schema.Inputs, arguments), context));
            Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(x => x.Message)));
            using var output = outcome.Output!;
            var values = output.ToValues();
            Assert.Equal("local", Assert.IsType<string>(values[ImageSourceOutput.SourceKind]));
            Assert.Equal(path, Assert.IsType<string>(values[ImageSourceOutput.FileReference]));
            Assert.Equal(Path.GetFileName(path), Assert.IsType<string>(values[ImageSourceOutput.FileName]));
            foreach (var id in new[] { ImageSourceOutput.DeviceId, ImageSourceOutput.FrameId, ImageSourceOutput.Timestamp })
                Assert.Equal(string.Empty, Assert.IsType<string>(values[id]));
            Assert.Equal(2L, Assert.IsType<long>(((RecordValue)values[ImageSourceOutput.Image]!).Get<long>(ImageSourceOutput.ImageWidth)));
            Assert.Equal(1L, Assert.IsType<long>(((RecordValue)values[ImageSourceOutput.Image]!).Get<long>(ImageSourceOutput.ImageHeight)));
            Assert.Equal(21L, Assert.IsType<long>(values[ImageSourceOutput.Sequence]));
            var image = Assert.IsAssignableFrom<IReadOnlyImage>(((RecordValue)values[ImageSourceOutput.Image]!).Get<IReadOnlyImage>(ImageSourceOutput.ImageData));
            Assert.Equal(new byte[] { 17, 29 }, image.CopyPixels());
            Assert.Same(image, ((RecordValue)values[ImageSourceOutput.GrayImage]!).Get<IReadOnlyImage>(ImageSourceOutput.GrayData));
            Assert.Equal(PixelLayout.Gray8.ToString(), Assert.IsType<string>(((RecordValue)values[ImageSourceOutput.Image]!).Get<string>(ImageSourceOutput.ImageFormat)));
            Assert.Equal(context.Identity, image.Frame.ProducerRun);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static IToolExecutionContext Context(FrozenOutputResources resources,string? localAddress=null)
    {
        var context = Substitute.For<IToolExecutionContext, ILocalImageReadContext>();
        ((ILocalImageReadContext)context).AcceptLocalImage().Returns(_=>localAddress??throw new InvalidOperationException("测试缺少读取接纳地址。"));
        context.Identity.Returns(new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "source", Guid.NewGuid()));
        context.Cancellation.Returns(CancellationToken.None); context.Resources.Returns(resources);
        return context;
    }
    private static InputValues CameraInput(IReadOnlyImage image, long sequence) => new(ImageSourceTool.Schema.Inputs,
        new Dictionary<string, object?>
        {
            [ImageSourceTool.Sequence] = sequence, [ImageSourceTool.CameraImage] = image,
            [ImageSourceTool.DeviceId] = "device", [ImageSourceTool.DeviceFrameId] = "frame-123",
            [ImageSourceTool.DeviceTimestamp] = "2026-10-28T01:02:03.4560000+00:00"
        });
    private static void AssertImageIdentityAndDomain(IReadOnlyImage image, RunIdentity run, string port)
    {
        Assert.Equal(run, image.Frame.ProducerRun);
        Assert.Equal(port, image.Frame.SourcePortId);
        Assert.Equal(image.Frame, image.EffectiveDomain.SourceFrame);
        using var reader = image.EffectiveDomain.AcquireReader();
        Assert.Equal(image.Frame, reader.SourceFrame);
        Assert.Equal(new[] { new RegionRun(0, 0, 1), new RegionRun(1, 1, 1) }, reader.ReadRuns(0, 4096).ToArray());
    }
}
