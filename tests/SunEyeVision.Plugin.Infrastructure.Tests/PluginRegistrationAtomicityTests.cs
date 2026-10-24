using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

public sealed class PluginRegistrationAtomicityTests
{
    [Fact]
    public void Rejected_duplicate_keeps_the_existing_tool_and_metadata()
    {
        const string id="A11.Atomic.Existing";
        try
        {
            ToolRegistry.RegisterTool(typeof(Existing));
            var metadata=ToolRegistry.GetToolMetadata(id);
            Assert.ThrowsAny<Exception>(()=>ToolRegistry.RegisterTool(typeof(Conflicting)));
            Assert.IsType<Existing>(ToolRegistry.CreateToolInstance(id));
            Assert.Same(metadata,ToolRegistry.GetToolMetadata(id));
            Assert.Single(ToolRegistry.GetAllPropertyMetadata(id));
        }
        finally{ToolRegistry.UnregisterTool(id);}
    }

    [Fact]
    public void Failed_output_metadata_does_not_publish_a_tool_or_partial_fields()
    {
        const string id="A11.Atomic.BadMetadata";
        try
        {
            Assert.ThrowsAny<Exception>(()=>ToolRegistry.RegisterTool(typeof(BadMetadata)));
            Assert.False(ToolRegistry.ToolExists(id));
            Assert.Null(ToolRegistry.GetToolMetadata(id));
            Assert.Empty(ToolRegistry.GetAllPropertyMetadata(id));
        }
        finally{ToolRegistry.UnregisterTool(id);}
    }

    public abstract class Probe : IToolPlugin
    {
        public Type ParamsType=>typeof(GenericToolParameters);
        public Type ResultType=>typeof(GenericToolResults);
        public virtual IReadOnlyList<OutputVariable> OutputVariables=>new[]{new OutputVariable{Name="Value",VariableType="Double",DeclaredType=typeof(double)}};
        public ToolResults Run(ToolParameters parameters)=>new GenericToolResults();
    }
    [Tool("A11.Atomic.Existing","existing")]
    public sealed class Existing : Probe { }
    [Tool("A11.Atomic.Existing","conflicting")]
    public sealed class Conflicting : Probe { public Conflicting()=>throw new InvalidOperationException("Candidate constructor failed."); }
    [Tool("A11.Atomic.BadMetadata","bad metadata")]
    public sealed class BadMetadata : Probe
    { public override IReadOnlyList<OutputVariable> OutputVariables=>throw new InvalidOperationException("Candidate output declaration failed."); }
}
