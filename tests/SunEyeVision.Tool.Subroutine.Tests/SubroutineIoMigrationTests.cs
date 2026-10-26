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
    private static AvailableDataSource Source(string node,string field,Type type,string scope)=>new()
    {SourceWorkflowId=scope,SourceNodeId=node,SourceNodeName="模块",PropertyName=field,DisplayName=field,PropertyType=type,VariableType=SdkTypeVocabulary.ToTypeVocabulary(type)??""};
    [Fact]
    public void Batch_import_is_scoped_deduplicated_named_and_detaches_runtime_values_and_preserves_array_identity()
    {
        var array=Source("producer","计数",typeof(long[]),"parent");array.CurrentValue=new long[]{1,2};
        var inside=Source("internal","结果",typeof(double[]),"child");var query=new DataSourceQueryService();query.AddExtraDataSourceProvider(()=>new(){array});
        var p=new SubroutineParameters{SubroutineId="child"};var vm=new SubroutineParameterDialogViewModel("child","Group","owner",query,new(){inside},p);
        try
        {
            vm.ImportSources(true,new[]{array,array},true);var input=Assert.Single(p.Inputs);Assert.Equal("模块_计数",input.Name);Assert.Equal(typeof(long[]),input.ParamType);Assert.Equal("producer.计数[]",input.BindingSource);Assert.NotSame(array,input.Source);Assert.Null(input.Source!.CurrentValue);
            vm.ImportSources(false,new[]{inside},false);var output=Assert.Single(p.Outputs);Assert.Equal(typeof(double[]),output.ParamType);Assert.Equal("child",output.Source!.SourceWorkflowId);
            Assert.Throws<InvalidOperationException>(()=>vm.ImportSources(true,new[]{inside},false));Assert.Single(p.Inputs);
            var snapshot=(SubroutineParameters)p.CreateExecutionSnapshot();Assert.Equal(input.FieldId,snapshot.Inputs[0].FieldId);Assert.Equal(typeof(long[]),snapshot.Inputs[0].ParamType);Assert.Equal("parent",snapshot.Inputs[0].Source!.SourceWorkflowId);
        }
        finally{vm.Dispose();}
    }
    [Fact]
    public void Batch_preflights_every_type_and_limit_before_publishing_any_row()
    {
        var valid=Source("a","value",typeof(long),"parent");var invalid=Source("b","unsupported",typeof(object),"parent");var query=new DataSourceQueryService();query.AddExtraDataSourceProvider(()=>new(){valid,invalid});
        var p=new SubroutineParameters();var vm=new SubroutineParameterDialogViewModel("child","Group","owner",query,null,p);
        try
        {
            Assert.Throws<InvalidOperationException>(()=>vm.ImportSources(true,new[]{valid,invalid},false));Assert.Empty(p.Inputs);
            for(var index=0;index<1023;index++)p.Inputs.Add(new(){Name="entry"+index});
            Assert.Throws<InvalidOperationException>(()=>vm.ImportSources(true,new[]{valid,invalid},false));Assert.Equal(1023,p.Inputs.Count);
        }
        finally{vm.Dispose();}
    }
    [Fact]
    public void Imported_array_output_runs_without_scalar_collapse_and_declares_the_collection_contract()
    {
        var values=new GenericToolResults{Status=ExecutionStatus.Success};values.AddResultItem("Values",new long[]{2,4,8});
        var declared=values.GetOutputVariables().Single(v=>v.Name=="Values");
        var source=new AvailableDataSource{SourceWorkflowId="child",SourceNodeId="inside",SourceNodeName="内部",PropertyName="Values",DisplayName="数组",PropertyType=typeof(long[]),VariableType="Integer",SourceFieldId=declared.FieldId};
        var p=new SubroutineParameters{SubroutineId="child",ExecuteSubWorkflowAsync=(_,_,_)=>Task.FromResult(new ExecutionResult{Success=true,NodeResults=new System.Collections.Generic.Dictionary<string,NodeExecutionResult>{["inside"]=new(){Result=values}}})};
        var vm=new SubroutineParameterDialogViewModel("child","Group","owner",null,new(){source},p);
        try
        {
            vm.ImportSources(false,new[]{source},false);var result=new SubroutineTool().Run(p);Assert.True(result.IsSuccess,result.ErrorMessage);
            Assert.Equal(new long[]{2,4,8},Assert.IsType<long[]>(result.ResolvedOutputValues["数组"]));
            var output=Assert.Single(result.GetOutputVariables(),v=>v.Name=="数组");Assert.True(output.IsCollection);Assert.Equal(typeof(long[]),output.DeclaredType);
        }
        finally{vm.Dispose();}
    }

    private sealed class ReleaseProbe : IDisposable
    {public int Count;public void Dispose()=>Count++;}
    [Fact]
    public void Imported_image_array_keeps_collection_schema_and_child_resource_lease_until_Group_result_release()
    {
        var image=NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage>();var owner=new ReleaseProbe();
        var values=new GenericToolResults{Status=ExecutionStatus.Success};values.AddResultItem("Images",new[]{image});values.RegisterOutputResource(owner);
        var source=Source("inside","Images",typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage[]),"child");
        var p=new SubroutineParameters{SubroutineId="child",ExecuteSubWorkflowAsync=(_,_,_)=>
        {
            var execution=new ExecutionResult{Success=true,NodeResults=new System.Collections.Generic.Dictionary<string,NodeExecutionResult>{["inside"]=new(){Result=values}}};
            values.Resources!.Dispose();return Task.FromResult(execution);
        }};
        var vm=new SubroutineParameterDialogViewModel("child","Group","owner",null,new(){source},p);
        try
        {
            vm.ImportSources(false,new[]{source},false);var result=new SubroutineTool().Run(p);
            try
            {
                Assert.True(result.IsSuccess,result.ErrorMessage);Assert.Equal(0,owner.Count);
                var output=Assert.Single(result.GetOutputVariables(),v=>v.Name=="Images");Assert.True(output.IsCollection);Assert.NotNull(output.ValueDeclaration);
                Assert.Same(image,Assert.Single(Assert.IsType<SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage[]>(result.ResolvedOutputValues["Images"])));
            }
            finally{result.Resources?.Dispose();}
            Assert.Equal(1,owner.Count);
        }
        finally{vm.Dispose();values.Resources?.Dispose();}
    }

}
