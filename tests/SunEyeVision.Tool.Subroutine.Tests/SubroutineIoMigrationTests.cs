using System;
using System.Linq;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Tool.Subroutine.ViewModels;
using Xunit;

namespace SunEyeVision.Tool.Subroutine.Tests;
public sealed class SubroutineIoMigrationTests
{
    private static SubroutineParameterDialogViewModel Open(SubroutineParameters p)=>new("child","Group","owner",null,null,p);
    [Fact]
    public void Opening_and_reopening_do_not_create_IO_and_removing_last_row_preserves_empty_configuration()
    {
        var p=new SubroutineParameters{SubroutineId="child"};var vm=Open(p);
        try
        {
            Assert.Empty(p.Inputs);Assert.Empty(p.Outputs);Assert.True(p.Validate().IsValid);
            vm.AddInputCommand.Execute(null);vm.AddOutputCommand.Execute(null);
            vm.RemoveItemCommand.Execute(Assert.Single(p.Inputs));vm.RemoveItemCommand.Execute(Assert.Single(p.Outputs));
            Assert.Empty(p.Inputs);Assert.Empty(p.Outputs);
        }
        finally{vm.Dispose();}
        var reopened=Open(p);try{Assert.Empty(p.Inputs);Assert.Empty(p.Outputs);}finally{reopened.Dispose();}
    }
    [Fact]
    public void Add_uses_names_unique_across_both_directions_after_deletion_without_reusing_field_identity()
    {
        var p=new SubroutineParameters{SubroutineId="child"};var vm=Open(p);
        try
        {
            vm.AddInputCommand.Execute(null);vm.AddInputCommand.Execute(null);Assert.Equal(new[]{"in","in0"},p.Inputs.Select(i=>i.Name));
            var deleted=p.Inputs[0];vm.RemoveItemCommand.Execute(deleted);vm.AddOutputCommand.Execute(null);p.Outputs[0].Name="in";
            vm.AddInputCommand.Execute(null);Assert.Equal("in1",p.Inputs[1].Name);Assert.NotEqual(deleted.FieldId,p.Inputs[1].FieldId);Assert.True(p.Validate().IsValid);
        }
        finally{vm.Dispose();}
    }
    [Fact]
    public void Limit_and_name_contracts_apply_to_configuration_and_add_commands()
    {
        var p=new SubroutineParameters();for(var i=0;i<SubroutineIoRules.MaximumItems;i++)p.Inputs.Add(new(){Name="input"+i,TypeName="Integer"});
        var vm=Open(p);
        try
        {
            Assert.False(vm.AddInputCommand.CanExecute(null));vm.AddInputCommand.Execute(null);Assert.Equal(1024,p.Inputs.Count);Assert.True(p.Validate().IsValid);
            p.Inputs.Add(new(){Name="overflow"});Assert.False(p.Validate().IsValid);p.Inputs.RemoveAt(p.Inputs.Count-1);
            p.Inputs[0].Name=new string('名',31);Assert.True(p.Validate().IsValid);p.Inputs[0].Name=new string('名',32);Assert.False(p.Validate().IsValid);
            p.Inputs[0].Name="illegal.path";Assert.False(p.Validate().IsValid);p.Inputs[0].Name="restored";
            p.Outputs.Add(new(){Name=p.Inputs[0].Name});Assert.False(p.Validate().IsValid);p.Outputs[0].Name="result";
            p.Outputs[0].FieldId=p.Inputs[0].FieldId;Assert.False(p.Validate().IsValid);p.Outputs[0].FieldId=Guid.NewGuid();Assert.True(p.Validate().IsValid);
        }
        finally{vm.Dispose();}
    }
    [Fact]
    public void Actual_tool_executes_a_Group_without_synthetic_input_or_output_ports()
    {
        var calls=0;var p=new SubroutineParameters{SubroutineId="child",ExecuteSubWorkflowAsync=(_,_,_)=>{calls++;return Task.FromResult(new ExecutionResult{Success=true});}};
        var result=new SubroutineTool().Run(p);Assert.True(result.IsSuccess,result.ErrorMessage);Assert.Equal(1,calls);Assert.Empty(p.Inputs);Assert.Empty(p.Outputs);
    }
}
