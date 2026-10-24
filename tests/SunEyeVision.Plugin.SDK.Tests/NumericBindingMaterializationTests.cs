using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class NumericBindingMaterializationTests
{
    [Theory]
    [InlineData(typeof(long),typeof(int),BindingCompatibility.Conditional)]
    [InlineData(typeof(double),typeof(float),BindingCompatibility.Conditional)]
    [InlineData(typeof(int),typeof(long),BindingCompatibility.Compatible)]
    [InlineData(typeof(float),typeof(double),BindingCompatibility.Compatible)]
    [InlineData(typeof(int),typeof(double),BindingCompatibility.Compatible)]
    [InlineData(typeof(double),typeof(int),BindingCompatibility.Incompatible)]
    public void CandidateUsesTheActualDeclaredRepresentation(Type source,Type target,BindingCompatibility expected)
    {
        var output=new OutputVariable { Name="value",DeclaredType=source,VariableType=source==typeof(float)||source==typeof(double)?"Double":"Integer",IsCollection=false,FixedCount=1 };
        Assert.Equal(expected,BindingContractValidator.Validate(new[]{output},"value",new[]{target}).State);
    }
    [Fact]
    public void ActualPropertyOverridesAStalePersistedWideTargetHint()
    {
        var bindings=new ParameterBindingSet(new[]{new ParameterBindingDefinition
        {ParameterName="Number",BindingType=BindingType.Constant,ConstantValue=long.MaxValue,TargetType=typeof(long)}});
        var snapshot=new RuntimeParameterSnapshotResolver(new ParameterResolver()).Resolve(bindings,new Dictionary<string,ToolResults>(),new Dictionary<string,Type>{{"Number",typeof(int)}});
        Assert.False(snapshot.IsValid);Assert.Empty(snapshot.Values);
    }
    [Fact]
    public void CheckedPhysicalMaterializationRejectsOverflowAndPrecisionWithoutPartialArrays()
    {
        var resolver=new ParameterResolver();
        ParameterResolveResult Resolve(object value,Type target)=>resolver.Resolve(ParamSetting.CreateConstant("value",value),new Dictionary<string,ToolResults>(),target);
        Assert.False(Resolve(long.MaxValue,typeof(int)).IsSuccess);
        Assert.False(Resolve(-1L,typeof(uint)).IsSuccess);
        Assert.False(Resolve(long.MaxValue,typeof(uint)).IsSuccess);
        Assert.False(Resolve(16777217d,typeof(float)).IsSuccess);
        Assert.False(Resolve(9007199254740993L,typeof(double)).IsSuccess);
        Assert.False(Resolve(12d,typeof(int)).IsSuccess);
        Assert.Equal((long)uint.MaxValue,Resolve(uint.MaxValue,typeof(long)).Value);
        Assert.Equal(1.5f,Resolve(1.5d,typeof(float)).Value);
        Assert.Equal(17d,Resolve(17f,typeof(double)).Value);
        Assert.Equal(17L,Resolve(17,typeof(long)).Value);
        var original=new[]{1d,16777217d};
        var bindings=new ParameterBindingSet(new[]{new ParameterBindingDefinition
        {ParameterName="Values",BindingType=BindingType.Constant,ConstantValue=original,TargetType=typeof(float[])}});
        var snapshot=new RuntimeParameterSnapshotResolver(resolver).Resolve(bindings,new Dictionary<string,ToolResults>());
        Assert.False(snapshot.IsValid);Assert.Empty(snapshot.Values);Assert.Equal(new[]{1d,16777217d},original);
    }
}
