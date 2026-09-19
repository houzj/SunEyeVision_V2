using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class ParameterContractValidationTests
{
    private sealed class ValidParameters : ToolParameters, IRuntimeParameterContract
    {
        public int Threshold { get; set; }

        public IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters() =>
        [
            new("Threshold", "阈值", "Integer", typeof(int))
        ];
    }

    private sealed class InvalidParameters : ToolParameters, IRuntimeParameterContract
    {
        public int Threshold { get; set; }

        public IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters() =>
        [
            new("Missing", "缺失", "Integer"),
            new("Threshold", "", "String"),
            new("Threshold", "重复", "Integer")
        ];
    }

    [Fact]
    public void ValidContract_Passes()
    {
        var violations = new ValidParameters().ValidateParameterContract();
        Assert.Empty(violations);
    }

    [Fact]
    public void MissingDuplicateAndTypeMismatch_AreReported()
    {
        var violations = new InvalidParameters().ValidateParameterContract();

        Assert.Contains(violations, x => x.Code == "PARAM_PROPERTY_MISSING");
        Assert.Contains(violations, x => x.Code == "PARAM_NAME_DUPLICATE");
        Assert.Contains(violations, x => x.Code == "PARAM_DISPLAY_EMPTY");
        Assert.Contains(violations, x => x.Code == "PARAM_VOCABULARY_MISMATCH");
    }

    [Fact]
    public void StrictMode_ReportsUndeclaredPublicProperties()
    {
        var parameters = new ValidParameters();
        var violations = parameters.ValidateParameterContract(strictComplete: true);
        Assert.DoesNotContain(violations, x => x.Code == "PARAM_UNDECLARED");
    }
}
