using System.IO;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

public sealed class BlobSessionChainRegressionTests
{
    private sealed class DirectContext(IOutputResources resources) : IToolExecutionContext
    {
        public RunIdentity Identity { get; } = new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"direct",Guid.NewGuid());
        public CancellationToken Cancellation => default; public IOutputResources Resources => resources; public void Log(string message) { }
    }
    private sealed class Host : IDisposable
    {
        internal readonly DocumentSessionManager Manager;
        internal readonly DocumentSession Session;
        internal readonly WorkflowDefinition Flow;
        internal readonly NodeDefinition Node;
        private readonly FrozenOutputResources _inputs = new();
        private readonly IReadOnlyImage _image;
        internal IReadOnlyImage Input => _image;
        internal Host(BlobSettings settings, byte[] pixels, int width, int height)
        {
            Manager = new DocumentSessionManager(SunEyeVision.TestShared.ProductRuntimeTestHost.CreateRuntimeDirectory());
            Session = Manager.CreateNew("Blob正式会话");
            Flow = Session.GetWorkflows().Single();
            var graph = Session.PrepareGraphEdit(Flow.Id);
            Node = Session.CreateNode(graph, BlobTool.Id, new CanvasPoint(0, 0));
            Session.CommitGraphEdit(graph, "添加 Blob");
            var draft = Session.Editing.Host.BeginEdit(Flow.Id, Node.Id);
            var fields = new BlobTool().ReadSettings(settings).CopyValues()
                .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
            draft.ApplyFields(fields);
            Session.CommitEdit(DocumentEditProposal.Prepare(Session.Editing.Host, nodes: new[] { draft }));
            _image = _inputs.CreateGray8(width, height, pixels);
        }
        internal (DocumentRunBatch Batch, DocumentRunResult Result, ExecutionResult Run) Execute()
        {
            var batch = Session.CaptureRunBatch(new[] { Flow.Id }, workflowImages: new Dictionary<string, WorkflowImageInput>
            { [Flow.Id] = new WorkflowImageInput(_image, _image.AcquirePixels()) });
            try
            {
                var result = batch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                return (batch, result, result.Workflows[Flow.Id]);
            }
            catch { batch.Dispose(); throw; }
        }
        public void Dispose() { Manager.Dispose(); _inputs.Dispose(); }
    }

    private static BlobSettings Base(BlobSegmentationMode mode) => new()
    { SegmentationMode = mode, IsAreaFilterEnabled = false, OutputBlobImage = true,
      OutputBinaryImage = true, BlobCountLimit = 20000 };

    public static IEnumerable<object[]> Modes()
    {
        yield return new object[] { Base(BlobSegmentationMode.SingleThreshold) with { Threshold = 100 }, new byte[] {0,99,100,150,200,255}, new byte[] {0,0,255,255,255,255} };
        yield return new object[] { Base(BlobSegmentationMode.DoubleThreshold) with { Threshold = 150, UpperThreshold = 100 }, new byte[] {0,99,100,150,200,255}, new byte[] {255,255,0,0,255,255} };
        yield return new object[] { Base(BlobSegmentationMode.None), new byte[] {0,1,127,255}, new byte[] {0,1,127,255} };
        yield return new object[] { Base(BlobSegmentationMode.AutoThreshold), new byte[] {0,255}, new byte[] {255,255} };
        yield return new object[] { Base(BlobSegmentationMode.FixedSoftThreshold) with { FixedSoftLowerThreshold=150, FixedSoftUpperThreshold=100, ThresholdSoftness=254, Type=BlobThresholdMode.BinaryInv }, new byte[] {99,100,150,151,255}, new byte[] {255,255,255,0,0} };
        yield return new object[] { Base(BlobSegmentationMode.RelativeSoftThreshold) with { RelativeLeftRatio=0, RelativeRightRatio=0, RelativeLowerRatio=40, RelativeUpperRatio=60, Type=BlobThresholdMode.BinaryInv }, Enumerable.Range(0,256).Select(i=>(byte)i).ToArray(), Array.Empty<byte>() };
    }

    [Theory, MemberData(nameof(Modes))]
    public void Six_modes_run_through_admitted_document_session(BlobSettings settings, byte[] input, byte[] expected)
    {
        using var host = new Host(settings, input, input.Length, 1);
        var execution = host.Execute(); using var batch = execution.Batch; using var result = execution.Result;
        Assert.True(execution.Run.Success, string.Join(";", execution.Run.Errors));
        using var reader = execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        var binary = reader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data");
        var actual = binary.CopyPixels();
        if (settings.SegmentationMode == BlobSegmentationMode.RelativeSoftThreshold)
        {
            Assert.Equal((byte)255, actual[0]); Assert.Equal((byte)255, actual[101]);
            Assert.Equal((byte)0, actual[152]); Assert.Equal((byte)0, actual[255]);
        }
        else Assert.Equal(expected, actual);
        foreach (var field in new[] { 5, 6 })
        {
            var imageRecord = reader.Values.Get<RecordValue>(BlobSchema.Field(field));
            var image = imageRecord.Get<IReadOnlyImage>("data");
            Assert.Equal((long)image.Width, imageRecord.Get<long>("width"));
            Assert.Equal((long)image.Height, imageRecord.Get<long>("height"));
            Assert.Equal(17301505L, imageRecord.Get<long>("pixel-format"));
        }
        Assert.Equal(settings.SegmentationMode.ToString(), reader.Values.Get<string>(BlobSchema.Field(102)));
    }

    [Fact]
    public void Disabled_image_outputs_have_no_record_without_a_consumer()
    {
        var settings=Base(BlobSegmentationMode.SingleThreshold) with { OutputBlobImage=false, OutputBinaryImage=false };
        using var host=new Host(settings,new byte[]{0,255},2,1);
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        Assert.True(reader.Values!.Read(BlobSchema.Field(5)).IsNull);
        Assert.True(reader.Values.Read(BlobSchema.Field(6)).IsNull);
    }

    [Theory]
    [InlineData(5,6)]
    [InlineData(6,5)]
    public void Image_data_reference_requests_only_its_own_disabled_record(int requestedField,int otherField)
    {
        var settings=Base(BlobSegmentationMode.SingleThreshold) with { OutputBlobImage=false, OutputBinaryImage=false };
        using var host=new Host(settings,new byte[]{0,255,255,0},4,1);
        var graph=host.Session.PrepareGraphEdit(host.Flow.Id);
        var consumer=host.Session.CreateNode(graph,BlobTool.Id,new CanvasPoint(220,0));
        graph.AddConnection(new ConnectionDefinition("image-consumer",host.Node.Id,consumer.Id,BlobSchema.Field(requestedField),BlobTool.ImageInput));
        host.Session.CommitGraphEdit(graph,"连接按需图像");
        var port=new BlobTool().Describe().Outputs.Single(item=>item.Id==BlobSchema.Field(requestedField));
        var edit=host.Session.Editing.Host.BeginEdit(host.Flow.Id,consumer.Id);
        edit.ApplyFields(new Dictionary<string,ParameterSetting>
        { [BlobTool.ImageInput]=new ReferenceSetting(new OutputReference(host.Node.Id,port.Id,port.Type,memberFieldPath:new[]{"data"},scopeId:host.Flow.Id)) });
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{edit}));
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        using var reader=host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id);
        Assert.NotNull(reader);
        var record=reader.Values!.Get<RecordValue>(BlobSchema.Field(requestedField));
        Assert.Equal(4,record.Get<IReadOnlyImage>("data").CopyPixels().Length);
        Assert.Equal(4L,record.Get<long>("width"));
        Assert.True(reader.Values.Read(BlobSchema.Field(otherField)).IsNull);
    }

    [Fact]
    public void Fixed_soft_reverse_lut_has_independent_exact_anchors()
    {
        var settings = Base(BlobSegmentationMode.FixedSoftThreshold) with
        { FixedSoftLowerThreshold=100, FixedSoftUpperThreshold=150, ThresholdSoftness=254, Type=BlobThresholdMode.BinaryInv };
        var pixels = Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        using var host = new Host(settings,pixels,256,1);
        var softnessId=BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.ThresholdSoftness));
        Assert.Equal(254L,Assert.IsType<ConstantSetting>(host.Node.Configuration.Fields.Read(softnessId)).Value);
        var tool = new BlobTool();
        var settingIds = tool.SettingsSchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var onlySettings = new FieldSettings(tool.SettingsSchema.Inputs, host.Node.Configuration.Fields.Capture().Where(item => settingIds.Contains(item.Key)).ToDictionary(item => item.Key, item => item.Value));
        var materialized=Assert.IsType<BlobSettings>(tool.MaterializeSettings(onlySettings.Materialize(new Dictionary<string,object?>())));
        Assert.Equal(254,materialized.ThresholdSoftness);
        var execution=host.Execute(); using var batch=execution.Batch; using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        var actualOutput=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
        Assert.Equal(100,actualOutput.ThresholdMinUsed); Assert.Equal(150,actualOutput.ThresholdMaxUsed);
        using var directResources = new FrozenOutputResources();
        var directSession = new BlobSession();
        using var direct = directSession.Execute(settings,host.Input,directResources);
        Assert.Equal((byte)5,direct.BinaryImage!.CopyPixels()[149]);
        directSession.DisposeAsync().AsTask().GetAwaiter().GetResult();        var actual=reader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data").CopyPixels();
        // Independent S4 scalar oracle for the complete 256-entry fixed-soft table.
        var expectedTable=new byte[256];
        for(var p=0;p<256;p++)
            expectedTable[p]=p<=100?(byte)255:p>=150?(byte)0:(byte)(int)Math.Clamp(255f-((p-100)+1)*255f/51f+0.5f,0f,255f);
        var outputImage = actualOutput.BinaryImage!;
        var outputPixels = outputImage.CopyPixels();
        var outputAnchors = new[] { outputPixels[100],outputPixels[101],outputPixels[102],outputPixels[149],outputPixels[150] };
        var readerImage = reader.Values.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data");
        var inputPixels = host.Input.CopyPixels();
        var diagnostics = string.Join(";", actualOutput.ExecutionDiagnostics.Select(item => item.Code+":"+item.Message));
        Assert.True(new byte[]{255,245,240,5,0}.SequenceEqual(new[]{actual[100],actual[101],actual[102],actual[149],actual[150]}),
            $"input149={inputPixels[149]}; output={string.Join(',',outputAnchors)}; field={string.Join(',',new[]{actual[100],actual[101],actual[102],actual[149],actual[150]})}; " +
            $"outputSha={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(outputPixels))}; fieldSha={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual))}; " +
            $"inputLayout={host.Input.Layout}; outputLayout={outputImage.Layout}; fieldLayout={readerImage.Layout}; sameImage={ReferenceEquals(outputImage,readerImage)}; " +
            $"used={actualOutput.ThresholdMinUsed}/{actualOutput.ThresholdMaxUsed}/{actualOutput.TypeUsed}/{actualOutput.SegmentationModeUsed}; diag={diagnostics}");
        Assert.Equal(expectedTable,actual);
    }

    [Theory]
    [InlineData(BlobSortFeature.SortFeatureArea)] [InlineData(BlobSortFeature.SortFeaturePerimeter)]
    [InlineData(BlobSortFeature.SortFeatureCircularity)] [InlineData(BlobSortFeature.SortFeatureRect)]
    [InlineData(BlobSortFeature.SortFeatureCentroidX)] [InlineData(BlobSortFeature.SortFeatureCentroidY)]
    [InlineData(BlobSortFeature.SortFeatureRectX)] [InlineData(BlobSortFeature.SortFeatureRectY)]
    public void Eight_features_capacity_rows_region_and_blob_image_share_one_candidate_set(BlobSortFeature feature)
    {
        const int width=16,height=11; var pixels=new byte[width*height];
        var areas=new[]{3,5,7,9,11};
        for(var i=0;i<areas.Length;i++) for(var x=1;x<=areas[i];x++) pixels[(i*2+1)*width+x]=200;
        foreach(var mode in Enum.GetValues<BlobSortMode>())
        {
            using var host=new Host(Base(BlobSegmentationMode.None) with { SortFeature=feature,SortMode=mode,BlobCountLimit=2,OutputBoundary=true },pixels,width,height);
            var execution=host.Execute(); using var batch=execution.Batch; using var result=execution.Result;
            Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
            var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
            using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
            var rows=reader.Values!.Read(BlobSchema.BlobsPort);
            var image=reader.Values.Get<RecordValue>(BlobSchema.Field(5)).Get<IReadOnlyImage>("data").CopyPixels();
            Assert.Equal(2,output.Count); Assert.Equal(output.Count,rows.Count);
            Assert.Equal(output.Measurements.Sum(item=>item.Area), image.Count(pixel=>pixel!=0));
            using var region=output.Region.AcquireReader();
            Assert.Equal(output.Measurements.Sum(item=>item.Area),region.ReadRuns(0,region.RunCount).Span.ToArray().Sum(run=>run.LastColumnInclusive-run.FirstColumn+1));
            for(var i=0;i<output.Count;i++)
            {
                var row=rows.GetElement(i); var item=output.Measurements[i];
                Assert.Equal(item.ElementId,row.GetField("element-id").GetScalar<long>());
                Assert.Equal(item.Area,row.GetField("area").GetScalar<double>());
                using var selected=item.Region.AcquireReader();
                Assert.All(selected.ReadRuns(0,selected.RunCount).Span.ToArray(),run=>Assert.True(image.Skip(run.Row*width+run.FirstColumn).Take(run.LastColumnInclusive-run.FirstColumn+1).All(pixel=>pixel!=0)));
            }
        }
    }

    [Fact]
    public void Reverse_area_filter_is_or_not_sorted_endpoints()
    {
        const int width=9,height=11;var pixels=new byte[width*height];var areas=new[]{1,2,3,5,6};
        for(var i=0;i<areas.Length;i++)for(var x=1;x<=areas[i];x++)pixels[(2*i+1)*width+x]=200;
        using var host=new Host(Base(BlobSegmentationMode.None) with { IsAreaFilterEnabled=true,FilterAreaMin=5,FilterAreaMax=2,BlobCountLimit=10 },pixels,width,height);
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
        Assert.Equal(new double[]{1,2,5,6},output.Measurements.Select(item=>item.Area).OrderBy(value=>value));
    }
    [Fact]
    public void Fixed_soft_two_row_formal_session_preserves_soft_anchor()
    {
        var settings=Base(BlobSegmentationMode.FixedSoftThreshold) with
        { FixedSoftLowerThreshold=100, FixedSoftUpperThreshold=150, ThresholdSoftness=254, Type=BlobThresholdMode.BinaryInv };
        var pixels=Enumerable.Range(0,512).Select(i=>(byte)(i%256)).ToArray();
        using var host=new Host(settings,pixels,256,2);
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        var actual=reader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data").CopyPixels();
        Assert.Equal(new byte[]{5,5},new[]{actual[149],actual[256+149]});
    }
    [Fact]
    public void Boundary_demand_preserves_fixed_soft_image_in_direct_and_invocation_paths()
    {
        var settings=Base(BlobSegmentationMode.FixedSoftThreshold) with
        { FixedSoftLowerThreshold=100, FixedSoftUpperThreshold=150, ThresholdSoftness=254, Type=BlobThresholdMode.BinaryInv };
        var pixels=Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        using var inputOwner=new FrozenOutputResources();
        var input=inputOwner.CreateGray8(256,1,pixels);
        var directSession=new BlobSession();
        using var noBoundaryResources=new FrozenOutputResources();
        using var boundaryResources=new FrozenOutputResources();
        using var noBoundary=directSession.Execute(settings,input,noBoundaryResources,requested:BlobOutputRequest.None);
        var beforeBoundary=noBoundary.BinaryImage!.CopyPixels();
        using var boundary=directSession.Execute(settings,input,boundaryResources,requested:BlobOutputRequest.Boundary);
        var baseline=noBoundary.BinaryImage!.CopyPixels();
        var boundaryPixels=boundary.BinaryImage!.CopyPixels();
        var tool=new BlobTool();
        var inputs=new InputValues(tool.Describe().Inputs,new Dictionary<string,object?>{{BlobTool.ImageInput,input}});
        using var invokeResources=new FrozenOutputResources();
        var invokeSession=tool.CreateSession();
        try
        {
            var invocation=new ToolInvocation<BlobSettings>(settings,inputs,new DirectContext(invokeResources),
                new[]{new OutputValueRequest(BlobSchema.BlobsPort,new[]{"boundary"})});
            var outcome=invokeSession.ExecuteAsync(invocation).AsTask().GetAwaiter().GetResult();
            using var output=Assert.IsType<BlobOutput>(outcome.Output);
            Assert.True(outcome.Success,string.Join(";",outcome.Diagnostics.Select(item=>item.Message)));
            var invoked=output.BinaryImage!.CopyPixels();
            Assert.True(beforeBoundary[149]==5 && beforeBoundary.SequenceEqual(baseline) && beforeBoundary.SequenceEqual(boundaryPixels) && beforeBoundary.SequenceEqual(invoked),
                $"input149={pixels[149]}, before149={beforeBoundary[149]}, retained149={baseline[149]}, boundary149={boundaryPixels[149]}, invoke149={invoked[149]}, same={ReferenceEquals(noBoundary.BinaryImage,boundary.BinaryImage)}");
        }
        finally { invokeSession.DisposeAsync().AsTask().GetAwaiter().GetResult(); directSession.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    [Fact]
    public void Full_neutral_input_domain_survives_real_halcon_admission()
    {
        var pixels=Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        using var owner=new FrozenOutputResources();var input=owner.CreateGray8(256,1,pixels);
        using(var lease=input.AcquirePixels())
        using(var domain=lease.AcquireDomainReader())
        {
            Assert.Equal(1,domain.RunCount);var run=domain.ReadRuns(0,1).Span[0];
            Assert.Equal((0,0,255),(run.Row,run.FirstColumn,run.LastColumnInclusive));
        }
        using var native=SunEyeVision.HalAdapter.HalconResourceBridge.CopyImage(input);
        using var nativeDomain=native.GetDomain();
        nativeDomain.GetRegionRuns(out var rows,out var starts,out var ends);
        using(rows)using(starts)using(ends)
        {
            Assert.Equal(1,rows.Length);
            Assert.Equal((0,0,255),(rows[0].I,starts[0].I,ends[0].I));
        }
    }
    [Fact]
    public void Full_input_domain_is_independent_of_prior_native_image_format_on_new_threads()
    {
        static string Probe(int initialWidth)
        {
            string? result=null;Exception? failure=null;
            var thread=new Thread(()=>
            {
                try
                {
                    using var prime=new HalconDotNet.HImage();prime.GenImageConst("byte",initialWidth,1);
                    static string SystemValue(string key)
                    {
                        try { HalconDotNet.HOperatorSet.GetSystem(key,out var value);using(value)return value.ToString(); }
                        catch(Exception error){return error.GetType().Name+":"+error.Message;}
                    }
                    var before=$"width={SystemValue("width")};height={SystemValue("height")};clip={SystemValue("clip_region")};tsp_clip={SystemValue("tsp_clip_region")}";
                    using var owner=new FrozenOutputResources();var input=owner.CreateGray8(256,1,Enumerable.Range(0,256).Select(i=>(byte)i).ToArray());
                    using var neutral=input.AcquirePixels();using var neutralDomain=neutral.AcquireDomainReader();
                    var neutralRuns=neutralDomain.ReadRuns(0,neutralDomain.RunCount).ToArray();
                    using var native=SunEyeVision.HalAdapter.HalconResourceBridge.CopyImage(input);
                    using var domain=native.GetDomain();domain.GetRegionRuns(out var rows,out var starts,out var ends);
                    using(rows)using(starts)using(ends)
                    {
                        var runs=Enumerable.Range(0,rows.Length).Select(i=>$"{rows[i].I}:{starts[i].I}-{ends[i].I}").ToArray();
                        result=$"prime={initialWidth};{before};neutral={string.Join(',',neutralRuns.Select(r=>$"{r.Row}:{r.FirstColumn}-{r.LastColumnInclusive}"))};native={string.Join(',',runs)};afterWidth={SystemValue("width")};afterHeight={SystemValue("height")}";
                    }
                }
                catch(Exception error){failure=error;}
            });
            thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(20)),"原生域检查超时。");
            if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result!;
        }
        var small=Probe(128);var full=Probe(256);
        Assert.True(small.Contains("neutral=0:0-255")&&full.Contains("neutral=0:0-255")&&
            small.Contains("native=0:0-255")&&full.Contains("native=0:0-255"),small+" | "+full);
    }
    [Fact]
    public void Unavailable_required_blob_image_clears_publication_and_reader_outlives_owner()
    {
        var pixels=new byte[]{0,200,200,0,200};
        using var host=new Host(Base(BlobSegmentationMode.None),pixels,5,1);
        var graph=host.Session.PrepareGraphEdit(host.Flow.Id);
        var consumer=host.Session.CreateNode(graph,BlobTool.Id,new CanvasPoint(220,0));
        graph.AddConnection(new ConnectionDefinition("required-image",host.Node.Id,consumer.Id,BlobSchema.Field(5),BlobTool.ImageInput));
        host.Session.CommitGraphEdit(graph,"连接 BlobImage 消费者");
        var port=new BlobTool().Describe().Outputs.Single(item=>item.Id==BlobSchema.Field(5));
        var edit=host.Session.Editing.Host.BeginEdit(host.Flow.Id,consumer.Id);
        edit.ApplyFields(new Dictionary<string,ParameterSetting>
        { [BlobTool.ImageInput]=new ReferenceSetting(new OutputReference(host.Node.Id,port.Id,port.Type,memberFieldPath: new[] { "data" },scopeId:host.Flow.Id)) });
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{edit}));
        var first=host.Execute();
        DeclaredOutputReader? reader=null;IReadOnlyImage prior;
        try
        {
            Assert.True(first.Run.Success,string.Join(";",first.Run.Errors));
            reader=host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id);
            Assert.NotNull(reader);
            prior=reader.Values!.Get<RecordValue>(BlobSchema.Field(5)).Get<IReadOnlyImage>("data");
            Assert.Equal(new byte[]{0,255,255,0,255},prior.CopyPixels());
        }
        finally { first.Result.Dispose();first.Batch.Dispose(); }
        var disable=host.Session.Editing.Host.BeginEdit(host.Flow.Id,host.Node.Id);
        var field=BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsCircularityFilterEnabled));
        disable.ApplyFields(new Dictionary<string,ParameterSetting>{{field,new ConstantSetting(true)}});
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{disable}));
        Assert.Null(host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id));
        var failed=host.Execute();
        try
        {
            Assert.True(failed.Run.Success,string.Join(";",failed.Run.Errors));
            using var filteredReader=host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id);
            Assert.NotNull(filteredReader);Assert.True(filteredReader.Snapshot.IsSuccess);Assert.NotNull(filteredReader.Values);
            Assert.NotEqual(reader.Snapshot.Identity,filteredReader.Snapshot.Identity);
            var filteredImage=filteredReader.Values!.Get<RecordValue>(BlobSchema.Field(5)).Get<IReadOnlyImage>("data");
            Assert.Equal(new byte[]{0,255,255,0,0},filteredImage.CopyPixels());
            Assert.Equal(new byte[]{0,255,255,0,255},prior.CopyPixels());
        }
        finally { failed.Result.Dispose();failed.Batch.Dispose(); }
        host.Dispose();
        Assert.Equal(new byte[]{0,255,255,0,255},prior.CopyPixels());
        reader.Dispose();Assert.Throws<ObjectDisposedException>(()=>prior.CopyPixels());
    }
    [Fact]
    public void Seven_soft_fields_survive_real_copy_and_v4_save_reopen()
    {
        var settings=Base(BlobSegmentationMode.FixedSoftThreshold) with
        { FixedSoftLowerThreshold=22, FixedSoftUpperThreshold=199, ThresholdSoftness=41,
          RelativeLeftRatio=4, RelativeRightRatio=7, RelativeLowerRatio=23, RelativeUpperRatio=87 };
        using var host=new Host(settings,new byte[]{0,22,80,199,255},5,1);
        var names=new[]{nameof(BlobSettings.FixedSoftLowerThreshold),nameof(BlobSettings.FixedSoftUpperThreshold),
            nameof(BlobSettings.ThresholdSoftness),nameof(BlobSettings.RelativeLeftRatio),nameof(BlobSettings.RelativeRightRatio),
            nameof(BlobSettings.RelativeLowerRatio),nameof(BlobSettings.RelativeUpperRatio)};
        var values=new long[]{22,199,41,4,7,23,87};
        void Check(NodeDefinition node)
        {
            for(var index=0;index<names.Length;index++)
            {
                var id=BlobParameterDefinition.Instance.SettingsFieldId(names[index]);
                Assert.Equal(values[index],Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read(id)).Value);
            }
        }
        Check(host.Node);
        var fragment=host.Session.CaptureNodeClipboard(host.Flow.Id,new[]{host.Node});
        var pasted=host.Session.PasteNodeClipboard(host.Flow.Id,fragment,new CanvasPoint(200,0));
        var copied=Assert.Single(pasted.Nodes);Assert.NotEqual(host.Node.Id,copied.Id);Check(copied);
        var path=Path.Combine(SunEyeVision.TestShared.ProductRuntimeTestHost.ProductRoot,"..","..","_validation","blob-chain","cases",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);var file=Path.Combine(path,"blob.solution");
        var types=new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>());
        host.Session.SaveAs(file,Path.Combine(path,"recovery"),path,types,new System.Text.Json.JsonSerializerOptions(),_=>null);
        var reopened=host.Manager.Open(file,_=>types,_=>null,new System.Text.Json.JsonSerializerOptions());
        var nodes=reopened.GetNodes(host.Flow.Id);Assert.Equal(2,nodes.Count);
        Check(nodes.Single(node=>node.Id==host.Node.Id));Check(nodes.Single(node=>node.Id==copied.Id));
        using var batch=reopened.CaptureRunBatch(new[]{host.Flow.Id},workflowImages:new Dictionary<string,WorkflowImageInput>
        { [host.Flow.Id]=new WorkflowImageInput(host.Input,host.Input.AcquirePixels()) });
        using var result=batch.RunAsync(_=>{}).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        Assert.True(result.Workflows[host.Flow.Id].Success,string.Join(";",result.Workflows[host.Flow.Id].Errors));
    }
    [Fact]
    public void Region_copy_and_affine_are_thread_isolated_and_restore_both_clip_states()
    {
        var results=new System.Collections.Concurrent.ConcurrentQueue<string>();
        var failures=new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        using var barrier=new Barrier(4);
        var threads=Enumerable.Range(0,4).Select(index=>new Thread(()=>
        {
            try
            {
                var desired=index%2==0?"true":"false";
                HalconDotNet.HOperatorSet.SetSystem("tsp_clip_region",desired);
                using var prime=new HalconDotNet.HImage();prime.GenImageConst("byte",128,1);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("并发起点未汇合。");
                static string Clip(){HalconDotNet.HOperatorSet.GetSystem("tsp_clip_region",out var value);using(value)return value.S;}
                static string Runs(HalconDotNet.HRegion region)
                {
                    region.GetRegionRuns(out var rows,out var starts,out var ends);
                    using(rows)using(starts)using(ends)
                        return string.Join(',',Enumerable.Range(0,rows.Length).Select(i=>$"{rows[i].I}:{starts[i].I}-{ends[i].I}"));
                }
                using var owner=new FrozenOutputResources();
                var input=owner.CreateGray8(256,1,new byte[256]);
                var initial=Clip();
                using var region=SunEyeVision.HalAdapter.HalconResourceBridge.CopyRegion(input.EffectiveDomain,input.Frame,256,1);
                var afterCopy=Clip();
                var affine=new RecordValue(SpatialDeclarations.Affine2D,new Dictionary<string,object?>
                { ["m11"]=1d,["m12"]=0d,["m21"]=0d,["m22"]=1d,["offset-x"]=300d,["offset-y"]=0d,["from"]="from",["to"]="to" });
                using var transformed=SunEyeVision.HalAdapter.HalconResourceBridge.TransformRegion(region,affine);
                var afterTransform=Clip();
                results.Enqueue($"{desired}:{initial}:{afterCopy}:{afterTransform}:{Runs(region)}:{Runs(transformed)}");
            }
            catch(Exception error){failures.Enqueue(error);}
        })).ToArray();
        foreach(var thread in threads)thread.Start();
        foreach(var thread in threads)Assert.True(thread.Join(TimeSpan.FromSeconds(30)),"并发线程未排空。");
        Assert.Empty(failures);Assert.Equal(4,results.Count);
        foreach(var result in results)
        {
            var fields=result.Split(':');
            Assert.Equal(fields[0],fields[1]);Assert.Equal(fields[0],fields[2]);Assert.Equal(fields[0],fields[3]);
            Assert.Contains("0:0-255:0:300-555",result);
        }
    }
    [Fact]
    public void Cold_fixed_soft_512_input_survives_prior_small_native_image_on_fresh_thread()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                using var prime=new HalconDotNet.HImage();prime.GenImageConst("byte",128,1);
                HalconDotNet.HOperatorSet.GetSystem("width",out var width);
                using(width)Assert.True(width.I>=128);
                var input=Enumerable.Range(0,512).Select(i=>(byte)(i%256)).ToArray();
                var settings=Base(BlobSegmentationMode.FixedSoftThreshold) with
                { FixedSoftLowerThreshold=100,FixedSoftUpperThreshold=150,ThresholdSoftness=254,Type=BlobThresholdMode.BinaryInv };
                using var host=new Host(settings,input,512,1);
                var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
                Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
                using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
                var actual=reader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data").CopyPixels();
                Assert.Equal(512,actual.Length);Assert.Equal((byte)5,actual[149]);Assert.Equal((byte)5,actual[405]);
            }
            catch(Exception error){failure=error;}
        });
        thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(45)),"冷启动执行线程未排空。");
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
