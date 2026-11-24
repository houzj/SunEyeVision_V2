using System.IO;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.ContourMatch.Runtime;
using SunEyeVision.TestShared;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>验证 ImageSource→ContourMatch 的正式执行、发布和结果读取链，不代替 UI 人工验收。</summary>
public sealed class ContourMatchWorkflowChainTests
{
    private const int Size = 64;

    private static byte[] Mark()
    {
        var pixels = new byte[Size * Size];
        for (var row = 0; row < 6; row++)
        for (var column = 0; column < 24; column++)
            pixels[(20 + row) * Size + 18 + column] = 255;
        for (var row = 0; row < 18; row++)
        for (var column = 0; column < 6; column++)
            pixels[(20 + row) * Size + 18 + column] = 255;
        return pixels;
    }

    private static IReadOnlyRegion FullRegion(FrozenOutputResources owner, IReadOnlyImage image)
        => owner.CreateRegion(image.EffectiveDomain.Frame, image.Frame,
            Enumerable.Range(0, image.Height).Select(row => new RegionRun(row, 0, image.Width - 1)).ToArray());

    [Fact]
    public void Image_source_to_contour_match_runs_and_publishes_rows()
    {
        var caseRoot = Path.Combine(ProductRuntimeTestHost.ProductRoot, "_validation", "ContourMatchWorkflow", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(caseRoot);
        var imagePath = Path.Combine(caseRoot, "source.pgm");
        var pixels = Mark();
        File.WriteAllBytes(imagePath, System.Text.Encoding.ASCII.GetBytes($"P5\n{Size} {Size}\n255\n").Concat(pixels).ToArray());

        using var owner = new FrozenOutputResources();
        var image = owner.CreateGray8(Size, Size, pixels);
        var modelData = ContourMatchTemplateBuilder.Create(image, FullRegion(owner, image), new ContourMatchSettings());
        var roi = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit, new[]
            {
                RoiDocumentValue.Item(Guid.NewGuid(), "搜索区域", new RecordValue(RoiDocumentValue.ShapeType,
                    new Dictionary<string, object?>
                    {
                        ["kind"] = "rectangle", ["center"] = RoiDocumentValue.Point(Size / 2d, Size / 2d),
                        ["width"] = (double)Size, ["height"] = (double)Size, ["angle-degrees"] = 0d
                    }))
            }),
            RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));

        try
        {
            using var session = DocumentSession.CreateNew(SunEyeVision.TestShared.ProductRuntimeTestHost.CreateRuntimeDirectory());
            var flow = session.GetWorkflows().Single();
            var graph = session.PrepareGraphEdit(flow.Id);
            var source = session.CreateNode(graph, "ImageLoad", new CanvasPoint(0, 0));
            var contour = session.CreateNode(graph, ContourMatchTool.Id, new CanvasPoint(260, 0));
            graph.AddConnection(new ConnectionDefinition("image-to-contour", source.Id, contour.Id, "output.image", "input.image"));
            session.CommitGraphEdit(graph, "建立图像源到轮廓匹配链");

            var sourceDraft = session.Editing.Host.BeginEdit(flow.Id, source.Id);
            var pathsPort = sourceDraft.SettingsSchema.Inputs.Single(port => port.Id == "local.paths");
            sourceDraft.ApplyFields(new Dictionary<string, ParameterSetting>
            {
                ["local.paths"] = new ConstantSetting(new SequenceValue(pathsPort.Type, new object?[] { imagePath }))
            });

            var contourDraft = session.Editing.Host.BeginEdit(flow.Id, contour.Id);
            var settings = new ContourMatchSettings { ModelData = modelData };
            var settingsValues = new ContourMatchTool().ReadSettings(settings).CopyValues()
                .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
            settingsValues[ContourMatchRoi.ModeId] = new ConstantSetting(ContourMatchRoi.Local);
            settingsValues[ContourMatchSearchRois.LocalId] = new ConstantSetting(roi);
            contourDraft.ApplyFields(settingsValues);
            session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, nodes: new[] { sourceDraft, contourDraft }));

            using var factory = new ImageSourceHostInputFactory();
            using var batch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: factory);
            using var result = batch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            var run = result.Workflows[flow.Id];
            Assert.True(run.Success, string.Join(";", run.Errors));

            using var published = session.AcquirePublishedResult(flow.Id, contour.Id);
            Assert.NotNull(published);
            Assert.Equal(1L, published!.Values!.Get<long>(ContourMatchSchema.Status));
            Assert.True(published.Values.Get<long>(ContourMatchSchema.Count) > 0);
            Assert.True(published.Values.Read(ContourMatchSchema.Rows).Count > 0);
        }
        finally
        {
            if (Directory.Exists(caseRoot)) Directory.Delete(caseRoot, true);
        }
    }
}
