using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class ExplicitConstantConversionTests
{
    [Theory]
    [InlineData(12d,true,true)]
    [InlineData(12d,false,false)]
    [InlineData(12.5d,true,false)]
    [InlineData(1e20,true,false)]
    public async Task Direct_async_constant_uses_the_declared_operation_and_actual_property(double value,bool explicitOperation,bool expected)
    {
        var parameters=new Parameters();var owner=new ToolExecutionOwner();var tool=new Probe();
        var request=new ToolExecutionRequest(parameters){NodeId="number",ExecutionOwner=owner,
            ParameterBindings=new(new[]{new ParameterBindingDefinition{ParameterName="Number",BindingType=BindingType.Constant,
                ConstantValue=value,TargetType=typeof(long),Conversion=explicitOperation?BindingConversion.Float64ToInt64Checked:BindingConversion.Automatic}})};
        var executor=new ToolExecutor(new ParameterResolver());
        if(expected)
        {var result=(Results)await executor.ExecuteAsync(tool,request);Assert.Equal(12,result.Number);Assert.Equal(1,tool.Entries);}
        else{await Assert.ThrowsAsync<InvalidOperationException>(async()=>await executor.ExecuteAsync(tool,request));Assert.Equal(0,tool.Entries);}
        Assert.Equal(999,parameters.Number);Assert.Equal(0,owner.ActiveRuns);
    }
    [Theory]
    [InlineData(false,true)]
    [InlineData(true,false)]
    public async Task Direct_async_array_checks_every_element_before_entering_the_body(bool fractional,bool expected)
    {
        var input=new[]{12d,fractional?13.5d:13d};var parameters=new Parameters();var owner=new ToolExecutionOwner();var tool=new Probe();
        var request=new ToolExecutionRequest(parameters){NodeId="array",ExecutionOwner=owner,
            ParameterBindings=new(new[]{new ParameterBindingDefinition{ParameterName="Numbers",BindingType=BindingType.Constant,
                ConstantValue=input,TargetType=typeof(long[]),Conversion=BindingConversion.Float64ToInt64Checked}})};
        var executor=new ToolExecutor(new ParameterResolver());
        if(expected){var result=(Results)await executor.ExecuteAsync(tool,request);Assert.Equal(new[]{12,13},result.Numbers);Assert.Equal(1,tool.Entries);}
        else{await Assert.ThrowsAsync<InvalidOperationException>(async()=>await executor.ExecuteAsync(tool,request));Assert.Equal(0,tool.Entries);}
        Assert.Equal(new[]{999},parameters.Numbers);Assert.Equal(new[]{12d,fractional?13.5d:13d},input);Assert.Equal(0,owner.ActiveRuns);
    }
    [Fact]
    public void Unknown_operation_is_rejected_even_when_no_type_change_is_needed()
    {
        var binding=ParamSetting.CreateConstant("value",12);binding.Conversion=(BindingConversion)999;
        var resolver=new ParameterResolver();
        Assert.False(resolver.Resolve(binding,new Dictionary<string,ToolResults>(),typeof(int)).IsSuccess);
        Assert.False(resolver.Resolve(binding,new Dictionary<string,ToolResults>(),typeof(int[])).IsSuccess);
        Assert.Throws<ArgumentOutOfRangeException>(()=>BindingClrAdapter.ConvertNumeric(12,typeof(int),(BindingConversion)999));
    }
    [Fact]
    public void Explicit_scalar_promotion_and_single_item_unwrap_keep_the_same_operation()
    {
        var resolver=new ParameterResolver();
        var scalar=ParamSetting.CreateConstant("value",12d);scalar.Conversion=BindingConversion.Float64ToInt64Checked;
        var promoted=resolver.Resolve(scalar,new Dictionary<string,ToolResults>(),typeof(int[]));
        Assert.True(promoted.IsSuccess,promoted.ErrorMessage);Assert.Equal(new[]{12},(int[])promoted.Value!);
        var array=ParamSetting.CreateConstant("value",new[]{12d});array.Conversion=BindingConversion.Float64ToInt64Checked;
        var unwrapped=resolver.Resolve(array,new Dictionary<string,ToolResults>(),typeof(int?));
        Assert.True(unwrapped.IsSuccess,unwrapped.ErrorMessage);Assert.Equal(12,unwrapped.Value);
        var empty=ParamSetting.CreateConstant("value",Array.Empty<double>());
        Assert.False(resolver.Resolve(empty,new Dictionary<string,ToolResults>(),typeof(int[])).IsSuccess);
        empty.Conversion=BindingConversion.Float64ToInt64Checked;
        var converted=resolver.Resolve(empty,new Dictionary<string,ToolResults>(),typeof(int[]));
        Assert.True(converted.IsSuccess,converted.ErrorMessage);Assert.Empty((int[])converted.Value!);
    }
    public sealed class Parameters:ToolParameters{public int Number{get;set;}=999;public int[] Numbers{get;set;}=new[]{999};}
    public sealed class Results:ToolResults{public int Number{get;set;}public int[] Numbers{get;set;}=Array.Empty<int>();}
    private sealed class Probe:IToolPlugin<Parameters,Results>,IAsyncToolPlugin
    {
        public int Entries{get;private set;}
        public Results Run(Parameters parameters)=>throw new InvalidOperationException("Async entry required.");
        public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        {
            request.ClaimToolBody();Entries++;var parameters=(Parameters)request.Parameters;
            var result=new Results{Number=parameters.Number,Numbers=parameters.Numbers.ToArray()};result.SetSuccess();
            return ValueTask.FromResult<ToolResults>(result);
        }
    }
}
