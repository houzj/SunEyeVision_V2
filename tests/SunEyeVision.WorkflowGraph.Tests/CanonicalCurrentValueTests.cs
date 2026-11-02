using System.Reflection;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;
public sealed class CanonicalCurrentValueTests
{
    [Fact]
    public void Bound_current_value_and_identity_survive_formal_solution_save_and_reopen()
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, Guid.NewGuid().ToString("N"));
        var manager = new SolutionManager(Path.Combine(root, "catalog"), RootCanvasFixture.Directory);
        var document = manager.CreateUntitledSolution();
        var node = new WorkflowNodeBase("history", "history", "history", "BlobAnalysis")
            { RuntimeConfiguration = RuntimeToolConfiguration.Create(RootCanvasFixture.Directory, "BlobAnalysis") };
        var config = node.RuntimeConfiguration!;
        var port = config.SettingsSchema.Inputs.First(p => (p.EditModes & FieldEditModes.Reference) != 0 && config.Fields.Read(p.Id) is ConstantSetting { Value: long });
        var binding = new VariableSetting(new VariableReference("solution", "variable", port.Type, storage: port.Storage));
        config = config.WithFieldSettings(new Dictionary<string, ParameterSetting> { [port.Id] = binding });
        config = (RuntimeToolConfiguration)typeof(RuntimeToolConfiguration).GetMethod("WithAcceptedParameterValue", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(config, new object[] { port.Id, 173L })!;
        node.RuntimeConfiguration = config;
        document.Workflows[0].Nodes.Add(node);
        var path = Path.Combine(root, "current.solution");
        Assert.True(manager.SaveSolution(path), manager.LastError);
        var loaded = manager.LoadSolutionOnly(path);
        Assert.NotNull(loaded);
        var restored = Assert.Single(Assert.Single(loaded!.Workflows).Nodes).RuntimeConfiguration!;
        Assert.Equal(173L, restored.ReadCurrentParameter(port.Id)!.Value);
        Assert.Equal(ParameterSettingCanonical.Serialize(binding.Reference), ParameterSettingCanonical.Serialize(Assert.IsType<VariableSetting>(restored.Fields.Read(port.Id)).Reference));
    }
}

