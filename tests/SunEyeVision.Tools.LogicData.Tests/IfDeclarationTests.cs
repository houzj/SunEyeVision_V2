using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.If.Runtime;
using Xunit;

namespace SunEyeVision.Tools.LogicData.Tests;

/// <summary>条件分支的声明面：控制输出序号、无数据输入端口、默认值准入、字段可见性与校验。</summary>
public sealed class IfDeclarationTests
{
    private static readonly IfTool Tool=new();

    [Fact]
    public void Declares_four_control_outputs_in_slot_order()
    {
        var branches=Tool.Describe().ControlOutputs;
        Assert.Equal(IfFields.ConditionCount,branches.Count);
        Assert.Equal(new[]{"branch.0","branch.1","branch.2","branch.3"},branches.Select(branch=>branch.Id));
        Assert.Equal(new[]{"条件1","条件2","条件3","条件4"},branches.Select(branch=>branch.DisplayName));
    }

    [Fact]
    public void Has_no_data_input_ports_and_keeps_the_three_original_outputs()
    {
        var schema=Tool.Describe();
        Assert.Empty(schema.Inputs);
        Assert.Equal(new[]{IfOutput.ModuleStatus,IfOutput.ModuleRunTime,IfOutput.BranchMatch},
            schema.Outputs.Select(port=>port.Id));
    }

    [Fact]
    public void Default_settings_pass_admission_and_report_no_branch_work()
    {
        var definition=(IToolDefinition)Tool;
        Assert.Empty(definition.Validate(definition.CreateSettings()));
        var settings=IfFields.Materialize(IfFields.Read(new IfSettings()));
        Assert.False(settings.HasEnabledCondition);
        Assert.False(Tool.ShouldExecute(settings));
        Assert.Equal(IfFields.ConditionCount,settings.Conditions.Count);
        Assert.All(settings.Conditions,condition=>
        {
            Assert.False(condition.Enabled);
            Assert.Null(condition.Source);
            Assert.Equal(IfFields.DefaultMinimum,condition.Minimum);
            Assert.Equal(IfFields.DefaultMaximum,condition.Maximum);
        });
    }

    [Fact]
    public void Every_settings_field_has_one_presentation_row()
    {
        var schema=IfFields.Schema;
        var presentation=schema.Presentation??throw new InvalidOperationException("条件分支必须声明参数页布局。");
        Assert.Equal(schema.Inputs.Count,presentation.Fields.Count);
        Assert.All(schema.Inputs,port=>Assert.Single(presentation.Fields,field=>field.FieldId==port.Id));
    }

    [Fact]
    public void Source_and_range_rows_follow_the_enable_switch()
    {
        var fields=IfFields.Schema.Presentation!.Fields.ToDictionary(field=>field.FieldId,StringComparer.Ordinal);
        for(var index=0;index<IfFields.ConditionCount;index++)
        {
            Assert.Null(fields[IfFields.EnabledField(index)].Layout?.VisibleWhen);
            foreach(var id in new[]{IfFields.SourceField(index),IfFields.MinimumField(index),IfFields.MaximumField(index)})
            {
                var condition=fields[id].Layout?.VisibleWhen;
                Assert.NotNull(condition);
                Assert.Equal(IfFields.EnabledField(index),condition!.FieldId);
                Assert.Equal(true,condition.Value);
                Assert.False(condition.Negate);
            }
            Assert.Equal(IfFields.MaximumField(index),fields[IfFields.MinimumField(index)].Layout?.PairWith);
            Assert.Equal("有效区间",fields[IfFields.MinimumField(index)].Layout?.PairDisplayName);
        }
    }

    [Fact]
    public void Enabled_condition_requires_a_resolved_source()
    {
        var error=Assert.Single(Settings(Condition(enabled:true)).Validate());
        Assert.Equal("IF_CONDITION_SOURCE",error.Code);
        Assert.Equal(IfFields.SourceField(0),error.FieldId);
    }

    [Fact]
    public void Disabled_condition_without_source_stays_valid()
    {
        Assert.Empty(Settings(Condition(enabled:false)).Validate());
    }

    [Fact]
    public void Minimum_greater_than_maximum_is_rejected()
    {
        var error=Assert.Single(Settings(Condition(enabled:true,source:50,minimum:60,maximum:50)).Validate());
        Assert.Equal("IF_CONDITION_RANGE",error.Code);
    }

    [Fact]
    public void Range_outside_the_declared_limit_is_rejected()
    {
        var error=Assert.Single(Settings(Condition(enabled:true,source:0,minimum:-IfFields.RangeLimit-1,maximum:0)).Validate());
        Assert.Equal("IF_CONDITION_RANGE",error.Code);
    }

    [Fact]
    public void Materialize_rejects_a_value_that_is_not_numeric()
    {
        var values=new Dictionary<string,object?>(IfFields.Read(new IfSettings()).CopyValues(),StringComparer.Ordinal)
        {
            [IfFields.MinimumField(0)]="不是数值"
        };
        Assert.Throws<ArgumentException>(()=>IfFields.Materialize(new InputValues(IfFields.Schema.Inputs,values)));
    }

    [Fact]
    public void Dormant_defaults_release_only_disabled_slot_bindings()
    {
        var fields=new FieldSettings(IfFields.Schema.Inputs,new Dictionary<string,ParameterSetting>(StringComparer.Ordinal)
        {
            [IfFields.SourceField(1)]=Reference(),
            [IfFields.SourceField(2)]=Reference(),
            [IfFields.EnabledField(2)]=new ConstantSetting(true)
        });
        var dormant=Tool.SelectDormantParameterDefaults(fields);
        Assert.Equal(new[]{IfFields.SourceField(1)},dormant.Keys);
        Assert.Null(dormant[IfFields.SourceField(1)].Value);
    }

    [Fact]
    public void IfBranchText_round_trips_original_row_format()
    {
        var text=IfBranchTextCodec.Serialize(new[]
        {
            new IfBranchTextCodec.Row("int0",1,100,IfBranchTextCodec.OkState,7),
            new IfBranchTextCodec.Row("float1",-2.5,100000,IfBranchTextCodec.NgState,IfBranchTextCodec.UnselectedRunModuleId)
        });
        Assert.Equal("int0:1:100:1:7;float1:-2.5:100000:2:-1;",text);
        Assert.Equal(new[] { "int0", "float1" },IfBranchTextCodec.Parse(text).Select(row=>row.Name));
    }

    [Fact]
    public void BranchText_is_persisted_as_internal_settings_state_without_becoming_a_data_input()
    {
        var text="int0:1:100:1:-1;";
        var settings=new IfSettings{BranchText=text};
        var values=IfFields.Read(settings);
        Assert.Equal(text,values.Get<string>(IfFields.BranchText));
        Assert.DoesNotContain(IfTool.Schema.Inputs,port=>port.Id==IfFields.BranchText);
        var materialized=IfFields.Materialize(values);
        Assert.Equal(text,materialized.BranchText);
    }

    [Fact]
    public void IfBranchText_uses_type_specific_original_bounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>IfBranchTextCodec.Serialize(new[]
        {
            new IfBranchTextCodec.Row("int0",-1000000000,0,IfBranchTextCodec.OkState,-1)
        }));
        Assert.Throws<ArgumentOutOfRangeException>(()=>IfBranchTextCodec.Serialize(new[]
        {
            new IfBranchTextCodec.Row("float0",-100001,0,IfBranchTextCodec.OkState,-1)
        }));
    }

    [Fact]
    public void Dynamic_model_matches_original_add_remove_and_reload_contract()
    {
        var model=new IfBranchDynamicModel();
        model.InitSource(2);
        var integer=model.AddIO();
        model.SelectTypeIndex=1;
        var floating=model.AddIO();
        Assert.Equal(new[]{"int0","float0"},model.Items.Select(row=>row.Name));
        Assert.Equal(2,integer.DecimalDigits);
        Assert.Equal(2,floating.DecimalDigits);

        integer.Minimum=-10;
        integer.Maximum=10;
        integer.RunModuleId=3;
        model.RemoveIO(floating);
        var text=model.SetParamUnit();
        model.GetParam(text);

        Assert.Single(model.Items);
        Assert.Equal("int0",model.Items[0].Name);
        Assert.Equal(-10,model.Items[0].Minimum);
        Assert.Equal(10,model.Items[0].Maximum);
        Assert.Equal(3,model.Items[0].RunModuleId);
    }

    private static ReferenceSetting Reference()
        =>new(new OutputReference("source","output.value",DataType.Number));

    private static IfSettings Settings(params IfConditionSettings[] slots)
    {
        var all=IfFields.CreateDefaultConditions().ToArray();
        for(var index=0;index<slots.Length;index++)all[index]=slots[index];
        return new IfSettings{Conditions=Array.AsReadOnly(all)}.Freeze();
    }

    private static IfConditionSettings Condition(bool enabled,double? source=null,
        double minimum=IfFields.DefaultMinimum,double maximum=IfFields.DefaultMaximum)
        =>new(){Enabled=enabled,Source=source,Minimum=minimum,Maximum=maximum};
}
