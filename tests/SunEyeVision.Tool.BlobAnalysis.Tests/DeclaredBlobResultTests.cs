using NSubstitute;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

public sealed class DeclaredBlobResultTests
{
    [Fact]
    public async Task Sorted_filtered_native_contours_resolve_the_exact_record_by_element_identity()
    {
        var pixels=new byte[64*64];
        foreach(var box in new[]{(X:2,Y:2,W:3,H:3),(X:22,Y:7,W:4,H:5),(X:8,Y:28,W:6,H:7)})
            for(int y=0;y<box.H;y++)for(int x=0;x<box.W;x++)pixels[(box.Y+y)*64+box.X+x]=255;
        using var input=HalImage.CreateFromBytes(pixels,64,64,1);
        ToolRegistry.RegisterTool(typeof(BlobAnalysisTool));
        var logger=Substitute.For<ILogger>();var workflows=new WorkflowEngine(logger);
        var flow=workflows.CreateWorkflow("blob-address-"+Guid.NewGuid().ToString("N"),"blob");
        var parameters=new BlobAnalysisParameters{
            IsAreaFilterEnabled=true,FilterAreaMin=10,FilterAreaMax=100,IsCircularityFilterEnabled=false,
            IsRectangularityFilterEnabled=false,IsBlobCountFilterEnabled=false,SortMode=BlobSortMode.AreaDesc,
            Threshold=128,UpperThreshold=255,OutputContours=true};
        flow.AddNode(new WorkflowNodeBase("blob","blob","blob","BlobAnalysis"){Parameters=parameters});
        var runner=new WorkflowExecutionEngine(workflows,Substitute.For<IPluginManager>(),logger);
        using var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,inputImage:input)).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(run.Success,string.Join(";",run.Errors));
        using var reader=run.AcquireDeclaredOutput(Assert.Single(run.DeclaredOutputs).Identity);
        var records=reader.Values!.Read(BlobOutputContract.BlobsPortId);
        Assert.Equal(2,records.Count);
        var byId=Enumerable.Range(0,records.Count).ToDictionary(i=>records.GetElement(i).GetField("element-id").GetScalar<long>(),i=>records.GetElement(i));
        Assert.Equal(new[]{42d,20d},Enumerable.Range(0,2).Select(i=>records.GetElement(i).GetField("area").GetScalar<double>()));
        Assert.NotEqual(byId.Keys.OrderBy(id=>id).ToArray(),byId.Keys.ToArray()); // Sorting actually changed native order.
        var raw=(BlobAnalysisResults)run.NodeResults["blob"].Result!;
        var batches=raw.Display.Items.OfType<SunEyeVision.Plugin.SDK.Models.Visualization.GraphicsDisplayItem>()
            .SelectMany(item=>item.Geometries).OfType<SunEyeVision.Plugin.SDK.Models.Visualization.BatchedContourGeometry>().ToArray();
        var visits=new HashSet<(long,string)>();
        foreach(var batch in batches)for(int i=0;i<batch.Contours.Count;i++)
        {
            var address=batch.SourceElements![i];var member=Assert.Single(address.MemberFieldPath);
            Assert.Equal(BlobOutputContract.BlobsPortId,address.FieldId);Assert.True(visits.Add((address.ElementId,member)));
            var record=byId[address.ElementId];var points=batch.Contours[i];
            var cx=record.GetField("centroid-x").GetScalar<double>();var cy=record.GetField("centroid-y").GetScalar<double>();
            Assert.InRange(Math.Abs((points.Min(p=>p.X)+points.Max(p=>p.X))/2-cx),0,1);
            Assert.InRange(Math.Abs((points.Min(p=>p.Y)+points.Max(p=>p.Y))/2-cy),0,1);
            Assert.Equal(record.GetField("area").GetScalar<double>()==42?10.5:23.5,cx,6);
            Assert.Equal(record.GetField("area").GetScalar<double>()==42?31:9,cy,6);
        }
        Assert.Equal(4,visits.Count);
    }
    [Fact]
    public async Task Explicit_missing_image_source_is_not_replaced_by_the_root_input()
    {
        using var input=HalImage.CreateFromBytes(new byte[32*32],32,32,1);
        ToolRegistry.RegisterTool(typeof(BlobAnalysisTool));
        var logger=Substitute.For<ILogger>();var workflows=new WorkflowEngine(logger);
        var flow=workflows.CreateWorkflow("blob-missing-"+Guid.NewGuid().ToString("N"),"blob");
        var reference=new SunEyeVision.Plugin.SDK.Execution.Parameters.ImageSourceReference("missing","Image","missing","Image");
        var parameters=new BlobAnalysisParameters{ImageSourceRef=reference};
        flow.AddNode(new WorkflowNodeBase("blob","blob","blob","BlobAnalysis"){Parameters=parameters});
        var runner=new WorkflowExecutionEngine(workflows,Substitute.For<IPluginManager>(),logger);
        using var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,inputImage:input)).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(run.Success);Assert.Same(reference,parameters.ImageSourceRef);Assert.Null(parameters.ResolvedInputImage);
        Assert.Equal(1024,input.GetPixelData().Length);
        using var reader=run.AcquireDeclaredOutput(Assert.Single(run.DeclaredOutputs).Identity);
        Assert.Null(reader.Values);
        Assert.True(reader.Snapshot.IsComplete);
        Assert.Equal("GrayImage",reader.Snapshot.Fields["field:"+parameters.GetOutputContract().Single(p=>p.Name=="GrayImage").FieldId!.Value.ToString("N")].Name);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Actual_native_workflow_publishes_declared_records_and_two_reader_leases(int count)
    {
        var pixels=new byte[32*32];
        for(int i=0;i<count;i++)for(int y=0;y<4;y++)for(int x=0;x<4;x++)pixels[(2+(i==2?16:0)+y)*32+2+i*10+x]=255;
        using var input=HalImage.CreateFromBytes(pixels,32,32,1);
        ToolRegistry.RegisterTool(typeof(BlobAnalysisTool));
        var logger=Substitute.For<ILogger>();var workflows=new WorkflowEngine(logger);
        var flow=workflows.CreateWorkflow("blob-declared-"+Guid.NewGuid().ToString("N"),"blob");
        var parameters=new BlobAnalysisParameters{IsAreaFilterEnabled=false,
            IsCircularityFilterEnabled=false,IsRectangularityFilterEnabled=false,IsBlobCountFilterEnabled=false,
            Threshold=128,UpperThreshold=255,OutputBlobImage=true,OutputBinaryImage=true,OutputContours=true};
        flow.AddNode(new WorkflowNodeBase("blob","blob","blob","BlobAnalysis"){Parameters=parameters});
        var runner=new WorkflowExecutionEngine(workflows,Substitute.For<IPluginManager>(),logger);
        using var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,inputImage:input)).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(run.Success,string.Join(";",run.Errors));
        Assert.Null(parameters.ResolvedInputImage);Assert.Null(parameters.ImageSourceRef);
        Assert.True(parameters.OutputBlobImage);Assert.True(parameters.OutputBinaryImage);Assert.Equal(128,parameters.Threshold);
        var output=Assert.Single(run.DeclaredOutputs);
        Assert.Equal(parameters.GetOutputContract().Count,output.Schema.Outputs.Count);
        var first=run.AcquireDeclaredOutput(output.Identity);var second=run.AcquireDeclaredOutput(output.Identity);
        var records=second.Values!.Read(BlobOutputContract.BlobsPortId);
        Assert.Equal(count,records.Count);
        var identities=new HashSet<long>();
        for(int i=0;i<count;i++)
        {
            var record=records.GetElement(i);
            Assert.Equal(16,record.GetField("area").GetScalar<double>());
            Assert.True(identities.Add(record.GetField("element-id").GetScalar<long>()));
            Assert.Equal(4,record.GetField("rotated-bounds").Count);
        }
        var raw=(BlobAnalysisResults)run.NodeResults["blob"].Result!;
        var batches=raw.Display.Items.OfType<SunEyeVision.Plugin.SDK.Models.Visualization.GraphicsDisplayItem>()
            .SelectMany(item=>item.Geometries).OfType<SunEyeVision.Plugin.SDK.Models.Visualization.BatchedContourGeometry>().ToArray();
        Assert.Equal(count*2,batches.Sum(batch=>batch.Contours.Count));
        foreach(var batch in batches)
        {
            Assert.NotNull(batch.SourceElements);Assert.Equal(batch.Contours.Count,batch.SourceElements.Count);
            foreach(var address in batch.SourceElements)
            {
                Assert.Equal(BlobOutputContract.BlobsPortId,address.FieldId);
                Assert.Contains(address.ElementId,identities);
                Assert.Contains(Assert.Single(address.MemberFieldPath),new[]{"contour","rotated-bounds"});
            }
        }
        var grayPort=parameters.GetOutputContract().Single(p=>p.Name=="GrayImage").FieldId!.Value;
        var borrowed=second.Values.Get<IReadOnlyImage>("field:"+grayPort.ToString("N"));
        run.Dispose();first.Dispose();first.Dispose();
        Assert.Equal(1024,borrowed.CopyPixels().Length);
        second.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>raw.GrayImage!.GetPixelData());
        Assert.Equal(1024,input.GetPixelData().Length);
    }
}
