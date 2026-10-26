using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 标量输出统一为"长度 1 数组" + 解析器"无索引默认取 [0]"契约测试（方案决策3）。
/// 覆盖：IO 边界提升、长度1默认解包、显式索引、[] 取整个数组、越界报错。
/// 几何数组的解析行为用手动 OutputVariable 验证（不以 GetOutputVariables 触发词汇表校验）。
/// </summary>
public class OutputVariableResolverScalarArrayTests
{
    [Fact]
    public void Stable_field_identity_preserves_suffix_and_never_falls_back_to_a_reused_name()
    {
        var result=new IdentifiedDisplayResults{Status=ExecutionStatus.Success};result.AddResultItem("Renamed",new long[]{3,9});
        var field=result.GetOutputVariables().Single(v=>v.Name=="Renamed");
        Assert.Equal(9L,OutputVariableResolver.Resolve(result,"OldName[1]","node",field.FieldId));
        Assert.Equal(new long[]{3,9},Assert.IsType<long[]>(OutputVariableResolver.Resolve(result,"OldName[]","node",field.FieldId)));
        Assert.Throws<InvalidOperationException>(()=>OutputVariableResolver.Resolve(result,"Renamed","node",Guid.NewGuid()));
    }

    #region 标量：经 GetOutputVariables 真实提升

    private class ScalarResults : ToolResults
    {
        public double ThresholdUsed { get; set; }

        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),   // 基类：执行耗时 + 节点状态
                new("ThresholdUsed", "实际阈值"),
            ];
        }
    }

    private static IDictionary<string, ToolResults> MakeScalarResults(ScalarResults sample)
        => new Dictionary<string, ToolResults> { ["N"] = sample };

    [Fact]
    public void GetOutputVariables_ScalarPromotedToLengthOneArray()
    {
        var sample = new ScalarResults { ThresholdUsed = 128.5, IsPass = true };
        var vars = sample.GetOutputVariables();

        var isPass = vars.First(v => v.Name == "IsPass");
        var arr = Assert.IsType<bool[]>(isPass.RuntimeValue);
        Assert.Single(arr);
        Assert.True(arr[0]);

        var threshold = vars.First(v => v.Name == "ThresholdUsed");
        var tArr = Assert.IsType<double[]>(threshold.RuntimeValue);
        Assert.Single(tArr);
        Assert.Equal(128.5, tArr[0]);
    }

    [Fact]
    public void Resolve_NoIndex_ScalarUnwrapsToSingleValue()
    {
        var sample = new ScalarResults { IsPass = true };
        var resolved = OutputVariableResolver.Resolve(MakeScalarResults(sample), "N", "IsPass");
        Assert.IsType<bool>(resolved);
        Assert.True((bool)resolved!);
    }

    [Fact]
    public void Resolve_ExplicitIndex0_ScalarReturnsElement()
    {
        var sample = new ScalarResults { IsPass = false };
        var resolved = OutputVariableResolver.Resolve(MakeScalarResults(sample), "N", "IsPass[0]");
        Assert.IsType<bool>(resolved);
        Assert.False((bool)resolved!);
    }

    [Fact]
    public void Resolve_ExplicitWholeArray_ReturnsArray()
    {
        var sample = new ScalarResults { IsPass = true };
        var resolved = OutputVariableResolver.Resolve(MakeScalarResults(sample), "N", "IsPass[]");
        var arr = Assert.IsType<bool[]>(resolved);
        Assert.Single(arr);
    }

    [Fact]
    public void Resolve_OutOfRange_ThrowsIndexOutOfRange()
    {
        var sample = new ScalarResults { IsPass = true };
        Assert.Throws<System.IndexOutOfRangeException>(() =>
            OutputVariableResolver.Resolve(MakeScalarResults(sample), "N", "IsPass[1]"));
    }

    #endregion

    #region 几何数组：手动 OutputVariable，专测解析器 length-1 解包/索引

    private class FakeResults : ToolResults
    {
        private readonly IReadOnlyList<OutputVariable> _vars;
        public FakeResults(IReadOnlyList<OutputVariable> vars) => _vars = vars;
        public override IReadOnlyList<OutputVariable> GetOutputVariables() => _vars;
    }

    private static IDictionary<string, ToolResults> MakeGeometryResults(Circle2d[] circles)
    {
        var variables = new List<OutputVariable>
        {
            new OutputVariable
            {
                Name = "CircleFound",
                VariableType = "Circle",
                DisplayName = "圆结果",
                RuntimeValue = circles,
            }
        };
        return new Dictionary<string, ToolResults> { ["N"] = new FakeResults(variables) };
    }

    [Fact]
    public void Resolve_GeometryLengthOne_UnwrapsToSingleElement()
    {
        var results = MakeGeometryResults(new[] { new Circle2d(10, 20, 5) });
        var resolved = OutputVariableResolver.Resolve(results, "N", "CircleFound");
        var circle = Assert.IsType<Circle2d>(resolved);
        Assert.Equal(5.0, circle.Radius);
    }

    [Fact]
    public void Resolve_GeometryIndex0_ReturnsSingleElement()
    {
        var results = MakeGeometryResults(new[]
        {
            new Circle2d(10, 20, 5),
            new Circle2d(30, 40, 8),
        });
        var resolved = OutputVariableResolver.Resolve(results, "N", "CircleFound[0]");
        var circle = Assert.IsType<Circle2d>(resolved);
        Assert.Equal(5.0, circle.Radius);
    }

    [Fact]
    public void Resolve_GeometryLengthGreaterOne_NoIndexKeepsArray()
    {
        var results = MakeGeometryResults(new[]
        {
            new Circle2d(10, 20, 5),
            new Circle2d(30, 40, 8),
            new Circle2d(50, 60, 9),
        });
        var resolved = OutputVariableResolver.Resolve(results, "N", "CircleFound");
        var arr = Assert.IsType<Circle2d[]>(resolved);
        Assert.Equal(3, arr.Length);
    }

    #endregion
    private sealed class IdentifiedDisplayResults : GenericToolResults
    {
        private readonly Guid _fieldId=Guid.NewGuid();
        public override System.Collections.Generic.IReadOnlyList<OutputVariable> GetOutputVariables()
        {
            var variables=base.GetOutputVariables();
            foreach(var variable in variables.Where(v=>v.Name=="Renamed"))variable.FieldId=_fieldId;
            return variables;
        }
    }

}
