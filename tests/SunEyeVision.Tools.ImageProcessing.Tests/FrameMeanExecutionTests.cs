using NSubstitute;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.FrameMean.Runtime;
using Xunit;

namespace SunEyeVision.Tools.ImageProcessing.Tests;

/// <summary>帧均值执行语义：正常输出、边界/空输入、参数校验失败与会话累计行为。</summary>
public sealed class FrameMeanExecutionTests
{
    private const int Size = 4;

    private static byte[] Uniform(byte value)=>Enumerable.Repeat(value,Size*Size).ToArray();

    private static InputValues ImageInput(IReadOnlyImage image)=>new(FrameMeanTool.Schema.Inputs,
        new Dictionary<string,object?> {[FrameMeanTool.ImageInput]=image});

    private static IToolExecutionContext Context(FrozenOutputResources resources,string nodeId="frame-mean-node")
    {
        var context=Substitute.For<IToolExecutionContext>();
        context.Identity.Returns(new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),nodeId,Guid.NewGuid()));
        context.Cancellation.Returns(CancellationToken.None);
        context.Resources.Returns(resources);
        return context;
    }

    private static async Task<FrameMeanOutput> ExecuteOnce(IToolSession<FrameMeanSettings,FrameMeanOutput> session,
        FrameMeanSettings settings,byte value,FrozenOutputResources resources)
    {
        var image=resources.CreateGray8(Size,Size,Uniform(value));
        var outcome=await session.ExecuteAsync(new(settings,ImageInput(image),Context(resources)));
        Assert.True(outcome.Success,string.Join(";",outcome.Diagnostics.Select(item=>item.Message)));
        return outcome.Output!;
    }

    private static IReadOnlyImage OutputImage(FrameMeanOutput output)
        =>((RecordValue)output.ToValues()[FrameMeanOutput.Image]!).Get<IReadOnlyImage>(FrameMeanOutput.ImageData);

    [Fact]
    public async Task Record_mode_accumulates_frames_and_publishes_the_mean_image()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new FrameMeanTool().CreateSession();
        using var first=await ExecuteOnce(session,new FrameMeanSettings(),10,resources);
        using var second=await ExecuteOnce(session,new FrameMeanSettings(),30,resources);
        // 均值 20 逐像素成立；统计数量随记录递增。
        Assert.Equal(Uniform(20),OutputImage(second).CopyPixels());
        var values=second.ToValues();
        Assert.Equal(2L,Assert.IsType<long>(values[FrameMeanOutput.ImageCount]));
        Assert.Equal(1L,Assert.IsType<long>(values[FrameMeanOutput.ModuleStatus]));
        var time=Assert.IsType<double>(values[FrameMeanOutput.ModuleRunTime]);
        Assert.True(double.IsFinite(time)&&time>=0);
        Assert.Equal((double)(float)time,time);
        var mean=OutputImage(second);
        Assert.Equal(PixelLayout.Gray8,mean.Layout.Format);
        Assert.Equal(Size,mean.Width);
        Assert.Equal(Size,mean.Height);
        // 产出图像帧归属本次执行与输出端口。
        var identity=(RecordValue)second.ToValues()[FrameMeanOutput.Image];
        Assert.Equal(FrameMeanOutput.Image,mean.Frame.SourcePortId);
        Assert.NotNull(mean.Frame.ProducerRun);
        var recordValues=identity.Get<IReadOnlyImage>(FrameMeanOutput.ImageData);
        Assert.Same(mean,recordValues);
        Assert.Equal(Size,identity.Get<long>(FrameMeanOutput.ImageWidth));
        Assert.Equal(Size,identity.Get<long>(FrameMeanOutput.ImageHeight));
        Assert.Equal(17301505L,identity.Get<long>(FrameMeanOutput.ImageFormat));
        // 第一个均值图像是单帧均值，等于输入本身。
        Assert.Equal(Uniform(10),OutputImage(first).CopyPixels());
    }

    [Fact]
    public async Task NoRecord_mode_reuses_accumulated_mean_without_consuming_the_input()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new FrameMeanTool().CreateSession();
        await ExecuteOnce(session,new FrameMeanSettings(),10,resources);
        // 不统计当前图像：输入 255 不参与均值，统计数量不变。
        var outcome=await session.ExecuteAsync(new(new FrameMeanSettings{ProcStep="2"},
            ImageInput(resources.CreateGray8(Size,Size,Uniform(255))),Context(resources)));
        Assert.True(outcome.Success,string.Join(";",outcome.Diagnostics.Select(item=>item.Message)));
        using var output=outcome.Output!;
        Assert.Equal(Uniform(10),OutputImage(output).CopyPixels());
        Assert.Equal(1L,Assert.IsType<long>(output.ToValues()[FrameMeanOutput.ImageCount]));
    }

    [Fact]
    public async Task NoRecord_mode_without_any_recorded_frame_fails_explicitly()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new FrameMeanTool().CreateSession();
        var outcome=await session.ExecuteAsync(new(new FrameMeanSettings{ProcStep="2"},
            ImageInput(resources.CreateGray8(Size,Size,Uniform(7))),Context(resources)));
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        var diagnostic=Assert.Single(outcome.Diagnostics);
        Assert.Equal("FRAME_MEAN_NO_FRAMES",diagnostic.Code);
    }

    [Fact]
    public async Task Recording_is_capped_at_the_original_limit_of_100_frames()
    {
        // 原厂证据：ImgCountA >= 100 且统计当前图像时拒绝运行（统计图像上限 :100）。
        using var resources=new FrozenOutputResources();
        await using var session=new FrameMeanTool().CreateSession();
        for(var index=0;index<100;index++)
            await ExecuteOnce(session,new FrameMeanSettings(),1,resources);
        var overflow=await session.ExecuteAsync(new(new FrameMeanSettings(),
            ImageInput(resources.CreateGray8(Size,Size,Uniform(1))),Context(resources)));
        Assert.False(overflow.Success);
        var diagnostic=Assert.Single(overflow.Diagnostics);
        Assert.Equal("FRAME_MEAN_COUNT_LIMIT",diagnostic.Code);
        Assert.Contains("100",diagnostic.Message);
    }

    [Fact]
    public async Task Clear_mode_resets_the_accumulator_and_reports_no_mean_image()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new FrameMeanTool().CreateSession();
        await ExecuteOnce(session,new FrameMeanSettings(),10,resources);
        await ExecuteOnce(session,new FrameMeanSettings(),30,resources);
        var cleared=await session.ExecuteAsync(new(new FrameMeanSettings{ProcStep="3"},
            ImageInput(resources.CreateGray8(Size,Size,Uniform(200))),Context(resources)));
        Assert.True(cleared.Success,string.Join(";",cleared.Diagnostics.Select(item=>item.Message)));
        using var output=cleared.Output!;
        var values=output.ToValues();
        Assert.Null(values[FrameMeanOutput.Image]);
        Assert.Equal(0L,Assert.IsType<long>(values[FrameMeanOutput.ImageCount]));
        Assert.Equal(1L,Assert.IsType<long>(values[FrameMeanOutput.ModuleStatus]));
        // 清空后重新统计，均值从新序列开始。
        using var restart=await ExecuteOnce(session,new FrameMeanSettings(),50,resources);
        Assert.Equal(Uniform(50),OutputImage(restart).CopyPixels());
    }

    [Fact]
    public async Task Frame_geometry_mismatch_fails_instead_of_silently_shrinking()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new FrameMeanTool().CreateSession();
        await ExecuteOnce(session,new FrameMeanSettings(),10,resources);
        // 宽度不同的同高度图像：像素缓冲按其真实尺寸构造。
        var wider=new byte[(Size+2)*Size];
        Array.Fill(wider,(byte)10);
        var mismatch=await session.ExecuteAsync(new(new FrameMeanSettings(),
            ImageInput(resources.CreateGray8(Size+2,Size,wider)),Context(resources)));
        Assert.False(mismatch.Success);
        Assert.Equal("FRAME_MEAN_IMAGE_MISMATCH",Assert.Single(mismatch.Diagnostics).Code);
    }

    [Fact]
    public async Task Invalid_settings_are_explicit_failures()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new FrameMeanTool().CreateSession();
        var invalidSettings=await session.ExecuteAsync(new(new FrameMeanSettings{ProcStep="9"},
            ImageInput(resources.CreateGray8(Size,Size,Uniform(1))),Context(resources)));
        Assert.False(invalidSettings.Success);
        Assert.Null(invalidSettings.Output);
        Assert.Equal("FRAME_MEAN_SETTINGS",Assert.Single(invalidSettings.Diagnostics).Code);
    }

    [Fact]
    public async Task Sessions_do_not_share_accumulators_across_nodes()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new FrameMeanTool().CreateSession();
        // 池化会话可能被不同节点复用：累计状态按执行节点隔离，不得互相污染。
        var nodeA=Context(resources,"node-a");
        var outcomeA=await session.ExecuteAsync(new(new FrameMeanSettings(),
            ImageInput(resources.CreateGray8(Size,Size,Uniform(10))),nodeA));
        Assert.True(outcomeA.Success,string.Join(";",outcomeA.Diagnostics.Select(item=>item.Message)));
        var nodeB=Context(resources,"node-b");
        var withoutB=await session.ExecuteAsync(new(new FrameMeanSettings{ProcStep="2"},
            ImageInput(resources.CreateGray8(Size,Size,Uniform(10))),nodeB));
        Assert.False(withoutB.Success);
        Assert.Equal("FRAME_MEAN_NO_FRAMES",Assert.Single(withoutB.Diagnostics).Code);
    }

    [Fact]
    public void Mean_record_declares_width_height_and_format_members()
    {
        foreach(var port in FrameMeanOutput.ImageType.Fields)
        {
            Assert.Equal(port.Id==FrameMeanOutput.ImageFormat,port.Nullable);
            if(port.Type==DataType.Integer)Assert.Equal(NumericStorage.Int32,port.Storage);
        }
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image),
            FrameMeanOutput.ImageType.Fields.Single(port=>port.Id==FrameMeanOutput.ImageData).Type);
    }
}
