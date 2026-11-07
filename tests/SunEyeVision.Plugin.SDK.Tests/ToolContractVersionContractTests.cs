using System.Reflection;
using System.Reflection.Emit;
using SunEyeVision.Plugin.Contracts;
using Xunit;

[assembly: PluginContract(FoundationContract.Major, FoundationContract.Minor)]
namespace SunEyeVision.Plugin.Editing.Wpf.Tests;
public class ToolContractVersionContractTests
{
    [Fact] public void Null_assembly_is_rejected() => Assert.Throws<ArgumentNullException>(() => FoundationContract.RequireCurrent(null!));
    [Fact] public void Current_contract_is_accepted() => FoundationContract.RequireCurrent(Assembly.GetExecutingAssembly());
    [Fact] public void Missing_contract_is_rejected_with_diagnostic()
    {
        var error = Assert.Throws<InvalidOperationException>(() => FoundationContract.RequireCurrent(typeof(Uri).Assembly));
        Assert.Contains("missing", error.Message);
        Assert.Contains($"{FoundationContract.Major}.{FoundationContract.Minor}", error.Message);
    }
    [Theory] [InlineData(1, 0)] [InlineData(6, 1)] [InlineData(7, 0)]
    public void Wrong_contract_is_rejected_with_expected_and_actual_identity(int major, int minor)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        assembly.SetCustomAttribute(new CustomAttributeBuilder(typeof(PluginContractAttribute).GetConstructor(new[] { typeof(int), typeof(int) })!, new object[] { major, minor }));
        var error = Assert.Throws<InvalidOperationException>(() => FoundationContract.RequireCurrent(assembly));
        Assert.Contains($"{major}.{minor}", error.Message);
        Assert.Contains($"{FoundationContract.Major}.{FoundationContract.Minor}", error.Message);
    }
}
