using NSubstitute;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.DataClassification.Runtime;
using SunEyeVision.Tool.MachineLearningClassifier.Runtime;
using Xunit;

namespace SunEyeVision.Tools.ClassifyCalib.Tests;

/// <summary>
/// UI 骨架阶段：算法语义未取证，Session 执行必须明确返回"待补证"诊断，禁止静默产出空结果（rule-015）。
/// </summary>
public sealed class ClassificationSessionTests
{
    private static IToolExecutionContext Context() => CreateContext();

    private static IToolExecutionContext CreateContext()
    {
        var context = Substitute.For<IToolExecutionContext>();
        context.Identity.Returns(new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "classify-node", Guid.NewGuid()));
        context.Cancellation.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Data_classification_session_reports_pending_algorithm()
    {
        await using var session = new DataClassificationTool().CreateSession();
        var inputs = new InputValues(DataClassificationTool.Schema.Inputs, new Dictionary<string, object?>());
        var outcome = await session.ExecuteAsync(new ToolInvocation<DataClassificationSettings>(new(), inputs, Context()));
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        var diagnostic = Assert.Single(outcome.Diagnostics);
        Assert.Equal("DATA_CLASSIFICATION_ALGORITHM_PENDING", diagnostic.Code);
        Assert.Contains("取证", diagnostic.Message);
    }

    private static InputValues MLInputs(FrozenOutputResources resources)
    {
        var image = resources.CreateGray8(2, 2, new byte[4]);
        return new InputValues(MachineLearningClassifierTool.Schema.Inputs,
            new Dictionary<string, object?> { [MachineLearningClassifierTool.ImageInput] = image });
    }

    [Fact]
    public async Task ML_classifier_session_reports_pending_algorithm()
    {
        using var resources = new FrozenOutputResources();
        await using var session = new MachineLearningClassifierTool().CreateSession();
        var outcome = await session.ExecuteAsync(new ToolInvocation<MachineLearningClassifierSettings>(new(), MLInputs(resources), Context()));
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        var diagnostic = Assert.Single(outcome.Diagnostics);
        Assert.Equal("MLCLASSIFIER_ALGORITHM_PENDING", diagnostic.Code);
        Assert.Contains("取证", diagnostic.Message);
    }

    [Fact]
    public async Task Invalid_settings_are_reported_before_algorithm_diagnostic()
    {
        using var resources = new FrozenOutputResources();
        await using var session = new MachineLearningClassifierTool().CreateSession();
        var outcome = await session.ExecuteAsync(new ToolInvocation<MachineLearningClassifierSettings>(
            new() { SvmMaxIter = 5 }, MLInputs(resources), Context()));
        Assert.False(outcome.Success);
        Assert.Contains(outcome.Diagnostics, d => d.Code == "MLCLASSIFIER_SETTINGS");
    }
}
