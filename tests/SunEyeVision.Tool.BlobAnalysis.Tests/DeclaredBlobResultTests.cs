using NSubstitute;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

public sealed class DeclaredBlobResultTests
{
    private static WorkflowExecutionEngine Engine(BlobSettings settings,out SunEyeVision.Workflow.Workflow flow,out WorkflowNodeBase node)
    {
        var directory=SunEyeVision.TestShared.ProductRuntimeTestHost.CreateRuntimeDirectory();
        var logger=Substitute.For<ILogger>();var workflows=new WorkflowEngine(logger,directory);
        flow=workflows.CreateWorkflow("blob-"+Guid.NewGuid().ToString("N"),"blob");
        node=new WorkflowNodeBase("blob","blob","blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(directory,BlobTool.Id).WithSettings(settings)};
        flow.AddNode(node);return new(workflows,logger);
    }
    [Fact]
    public async Task Sorted_filtered_boundary_records_keep_native_element_identity()
    {
        var pixels=new byte[64*64];
        foreach(var box in new[]{(X:2,Y:2,W:3,H:3),(X:22,Y:7,W:4,H:5),(X:8,Y:28,W:6,H:7)})
            for(var y=0;y<box.H;y++)for(var x=0;x<box.W;x++)pixels[(box.Y+y)*64+box.X+x]=255;
        using var input=HalImage.CreateFromBytes(pixels,64,64,1);
        var executor=Engine(new BlobSettings{IsAreaFilterEnabled=true,FilterAreaMin=10,FilterAreaMax=100,SortMode=BlobSortMode.SortModeDecend,OutputBoundary=true},out var flow,out _);
        using var run=await executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,input)).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(run.Success,string.Join(";",run.Errors));
        var output=(BlobOutput)((DeclaredOutputSnapshot)run.NodeResults["blob"].Result!).Output!;
        using var reader=run.AcquireDeclaredOutput(Assert.Single(run.DeclaredOutputs).Identity);
        var records=reader.Values!.Read(BlobSchema.BlobsPort);
        Assert.Equal(new[]{42d,20d},Enumerable.Range(0,records.Count).Select(i=>records.GetElement(i).GetField("area").GetScalar<double>()));
        var ids=output.Measurements.Select(m=>m.ElementId).ToArray();Assert.NotEqual(ids.OrderBy(i=>i).ToArray(),ids);
        for(var i=0;i<output.Count;i++)
        {
            var measurement=output.Measurements[i];var record=records.GetElement(i);
            Assert.Equal(measurement.ElementId,record.GetField("element-id").GetScalar<long>());
            Assert.Equal(measurement.Area==42?10.5:23.5,record.GetField("centroid-x").GetScalar<double>(),6);
            Assert.Equal(measurement.Area==42?31:9,record.GetField("centroid-y").GetScalar<double>(),6);
            Assert.NotEmpty(measurement.Boundary);Assert.Equal(4,measurement.RotatedBounds.Count);
            var points=measurement.Boundary.SelectMany(path=>path.Points).ToArray();
            Assert.InRange(Math.Abs((points.Min(p=>p.X)+points.Max(p=>p.X))/2-measurement.CentroidX),0,1);
            Assert.InRange(Math.Abs((points.Min(p=>p.Y)+points.Max(p=>p.Y))/2-measurement.CentroidY),0,1);
        }
    }
    [Fact]
    public async Task Explicit_missing_image_is_not_replaced_by_root_input()
    {
        using var input=HalImage.CreateFromBytes(new byte[32*32],32,32,1);
        var executor=Engine(new BlobSettings(),out var flow,out var node);
        var reference=new OutputReference("missing","missing-image",TypeRef.ResourceOf(ResourceKind.Image),scopeId:flow.Id);
        node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobTool.ImageInput,new ReferenceSetting(reference)}});
        using var run=await executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,input)).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(run.Success);Assert.Equal(reference,Assert.IsType<ReferenceSetting>(node.RuntimeConfiguration.Fields.Read(BlobTool.ImageInput)).Reference);
        Assert.Equal(1024,input.GetPixelData().Length);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public async Task Native_workflow_publishes_records_and_independent_reader_leases(int count)
    {
        var pixels=new byte[32*32];
        for(var i=0;i<count;i++)for(var y=0;y<4;y++)for(var x=0;x<4;x++)pixels[(2+(i==2?16:0)+y)*32+2+i*10+x]=255;
        using var input=HalImage.CreateFromBytes(pixels,32,32,1);
        var executor=Engine(new BlobSettings{IsAreaFilterEnabled=false,OutputGrayImage=true,OutputBinaryImage=true,OutputBoundary=true},out var flow,out var node);
        using var run=await executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,input)).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(run.Success,string.Join(";",run.Errors));
        Assert.Equal(100,((BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold);
        var publication=Assert.Single(run.DeclaredOutputs);Assert.Equal(BlobSchema.Outputs.Count,publication.Schema.Outputs.Count);
        var first=run.AcquireDeclaredOutput(publication.Identity);var second=run.AcquireDeclaredOutput(publication.Identity);
        var records=second.Values!.Read(BlobSchema.BlobsPort);Assert.Equal(count,records.Count);
        var ids=new HashSet<long>();
        for(var i=0;i<count;i++)
        {
            var record=records.GetElement(i);Assert.Equal(16,record.GetField("area").GetScalar<double>());
            Assert.True(ids.Add(record.GetField("element-id").GetScalar<long>()));Assert.Equal(4,record.GetField("rotated-bounds").Count);
        }
        var borrowed=second.Values.Get<IReadOnlyImage>(BlobSchema.Field(5));
        run.Dispose();first.Dispose();Assert.Equal(1024,borrowed.CopyPixels().Length);
        second.Dispose();Assert.Throws<ObjectDisposedException>(()=>{borrowed.CopyPixels();});Assert.Equal(1024,input.GetPixelData().Length);
    }
}
