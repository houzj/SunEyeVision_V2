using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.Script.Models;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

/// <summary>
/// 脚本输出数组化契约测试（方案：工具输出数组化契约一致性修复）。
/// 守护"脚本输出与静态工具一致，交付契约层统一为 T[]"（对齐 Halcon tuple）。
/// 覆盖：标量提升为 T[1]、已是数组保持、null 不造数组。
/// </summary>
public class ScriptOutputArrayPromotionTests
{
    private static ScriptResults MakeResults(string name, string typeName, object? value)
    {
        var results = new ScriptResults
        {
            OutputValues = new Dictionary<string, object?> { [name] = value },
            OutputConfigs = new List<ScriptOutputConfig>
            {
                new() { Name = name, TypeName = typeName },
            },
        };
        return results;
    }

    [Fact]
    public void GetOutputVariables_ScalarDouble_PromotedToLengthOneArray()
    {
        var results = MakeResults("score", "Double", 3.14);
        var var = results.GetOutputVariables().First(v => v.Name == "score");

        var arr = Assert.IsType<double[]>(var.RuntimeValue);
        Assert.Single(arr);
        Assert.Equal(3.14, arr[0]);
    }

    [Fact]
    public void GetOutputVariables_ScalarBoolean_PromotedToLengthOneArray()
    {
        var results = MakeResults("isPass", "Boolean", true);
        var var = results.GetOutputVariables().First(v => v.Name == "isPass");

        var arr = Assert.IsType<bool[]>(var.RuntimeValue);
        Assert.Single(arr);
        Assert.True(arr[0]);
    }

    [Fact]
    public void GetOutputVariables_AlreadyArray_KeepsArrayShape()
    {
        var values = new[] { 1.0, 2.0, 3.0 };
        var results = MakeResults("scores", "Double", values);
        var var = results.GetOutputVariables().First(v => v.Name == "scores");

        var arr = Assert.IsType<double[]>(var.RuntimeValue);
        Assert.Equal(3, arr.Length);
    }

    [Fact]
    public void GetOutputVariables_ArrayValueTargetArrayType_NotDoubleWrapped()
    {
        // 已传数组值 + RoI 数组词目：值形态优先，保持数组，不重复包装
        var rois = new RoiShape[0];
        var results = MakeResults("rois", "ROI", rois);
        var var = results.GetOutputVariables().First(v => v.Name == "rois");

        Assert.IsType<RoiShape[]>(var.RuntimeValue);
    }

    [Fact]
    public void GetOutputVariables_NullValue_DoesNotCreateArray()
    {
        var results = MakeResults("score", "Double", null);
        var var = results.GetOutputVariables().First(v => v.Name == "score");

        Assert.Null(var.RuntimeValue);
    }

    [Fact]
    public void GetOutputVariables_SingleRoiValue_RepackedToArray()
    {
        // 动态生产者（脚本/子程序）中间被解包成单对象后，包装器应重新包回 T[1]
        var single = new RoiShape();
        var results = MakeResults("rois", "ROI", single);
        var var = results.GetOutputVariables().First(v => v.Name == "rois");

        var arr = Assert.IsType<RoiShape[]>(var.RuntimeValue);
        Assert.Single(arr);
    }
}
