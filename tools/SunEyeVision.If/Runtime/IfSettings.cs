using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.If.Runtime;

/// <summary>一个条件槽的冻结配置：是否启用、取值来源（引用解析后的数值）、闭区间上下限。</summary>
public sealed record IfConditionSettings
{
    public bool Enabled{get;init;}
    /// <summary>本轮解析后的取值；未绑定来源时为 null。</summary>
    public double? Source{get;init;}
    public double Minimum{get;init;}=IfFields.DefaultMinimum;
    public double Maximum{get;init;}=IfFields.DefaultMaximum;
}

/// <summary>条件分支的唯一持久化设置；执行期只读，不持有运行时状态。</summary>
public sealed record IfSettings:IToolSettings<IfSettings>
{
    public IReadOnlyList<IfConditionSettings> Conditions{get;init;}=IfFields.CreateDefaultConditions();

    public IfSettings Freeze()=>this with
    {
        Conditions=Array.AsReadOnly((Conditions??throw new ArgumentException("条件列表不能为空。")).ToArray())
    };

    public bool HasEnabledCondition=>Conditions.Any(condition=>condition.Enabled);

    /// <summary>只校验配置本身：默认设置（全部槽位未启用）必须通过，才能完成工具准入。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors=new List<Diagnostic>();
        if(Conditions is null)
        {
            errors.Add(new("IF_CONDITION_SETTINGS","条件列表不能为空。"));
            return errors.AsReadOnly();
        }
        if(Conditions.Count!=IfFields.ConditionCount)
            errors.Add(new("IF_CONDITION_SETTINGS",$"条件数量必须是已声明的 {IfFields.ConditionCount} 个槽位。"));
        for(var index=0;index<Conditions.Count;index++)
        {
            var condition=Conditions[index];
            if(condition is null)
            {
                errors.Add(new("IF_CONDITION_SETTINGS",$"条件{index+1}不能为空。",IfFields.EnabledField(index)));
                continue;
            }
            if(!double.IsFinite(condition.Minimum)||!double.IsFinite(condition.Maximum)||
                Math.Abs(condition.Minimum)>IfFields.RangeLimit||Math.Abs(condition.Maximum)>IfFields.RangeLimit)
            {
                errors.Add(new("IF_CONDITION_RANGE",
                    $"条件{index+1}的有效区间必须落在 ±{IfFields.RangeLimit} 之内。",IfFields.MinimumField(index)));
                continue;
            }
            if(condition.Minimum>condition.Maximum)
                errors.Add(new("IF_CONDITION_RANGE",
                    $"条件{index+1}的最小值不能大于最大值。",IfFields.MinimumField(index)));
            if(!condition.Enabled)continue;
            if(condition.Source is not {} source)
                errors.Add(new("IF_CONDITION_SOURCE",
                    $"条件{index+1}已启用，但没有选择取值来源。",IfFields.SourceField(index)));
            else if(!double.IsFinite(source))
                errors.Add(new("IF_CONDITION_SOURCE",
                    $"条件{index+1}的取值来源不是有限数值。",IfFields.SourceField(index)));
        }
        return errors.AsReadOnly();
    }
}
