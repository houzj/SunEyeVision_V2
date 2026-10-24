using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Core.Services.Serialization;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

public sealed class PluginRegistrationAtomicityTests
{
    [Fact]
    public void Same_type_registration_is_idempotent_and_temporary_instance_is_disposed()
    {
        const string id="A11.Atomic.Disposable";
        DisposableProbe.Constructions=0;DisposableProbe.Disposals=0;
        try
        {
            ToolRegistry.RegisterTool(typeof(DisposableProbe));
            var serializer=ParameterTypeRegistry.SerializationOptions;
            var metadata=ToolRegistry.GetToolMetadata(id);
            ToolRegistry.RegisterTool(typeof(DisposableProbe));
            Assert.Equal(1,DisposableProbe.Constructions);Assert.Equal(1,DisposableProbe.Disposals);
            Assert.Same(metadata,ToolRegistry.GetToolMetadata(id));
            Assert.Same(serializer,ParameterTypeRegistry.SerializationOptions);
            Assert.Single(ToolRegistry.GetAllPropertyMetadata(id));
        }
        finally{ToolRegistry.UnregisterTool(id);}
    }

    [Fact]
    public void Whole_batch_metadata_failure_publishes_neither_tool_and_disposes_candidates()
    {
        DisposableProbe.Constructions=0;DisposableProbe.Disposals=0;BadDisposable.Disposals=0;
        var serializer=ParameterTypeRegistry.SerializationOptions;
        try
        {
            Assert.ThrowsAny<Exception>(()=>ToolRegistry.PrepareRegistration(new[]{typeof(DisposableProbe),typeof(BadDisposable)}));
            Assert.False(ToolRegistry.ToolExists("A11.Atomic.Disposable"));
            Assert.False(ToolRegistry.ToolExists("A11.Atomic.BadDisposable"));
            Assert.Empty(ToolRegistry.GetAllPropertyMetadata("A11.Atomic.Disposable"));
            Assert.Same(serializer,ParameterTypeRegistry.SerializationOptions);
            Assert.Equal(1,DisposableProbe.Disposals);Assert.Equal(1,BadDisposable.Disposals);
        }
        finally{ToolRegistry.UnregisterTool("A11.Atomic.Disposable");ToolRegistry.UnregisterTool("A11.Atomic.BadDisposable");}
    }

    [Fact]
    public void Unknown_output_metadata_is_rejected_before_publication()
    {
        const string id="A11.Atomic.Unknown";
        try
        {
            Assert.ThrowsAny<Exception>(()=>ToolRegistry.RegisterTool(typeof(UnknownMetadata)));
            Assert.False(ToolRegistry.ToolExists(id));Assert.Empty(ToolRegistry.GetAllPropertyMetadata(id));
        }
        finally{ToolRegistry.UnregisterTool(id);}
    }

    [Fact]
    public void Clear_invalidates_a_prepared_registration_without_resurrecting_it()
    {
        const string id="A11.Atomic.Prepared";
        var prepared=ToolRegistry.PrepareRegistration(new[]{typeof(PreparedProbe)});
        ToolRegistry.UnregisterTool(id);
        Assert.Throws<InvalidOperationException>(()=>prepared.EnterCommit());
        Assert.False(ToolRegistry.ToolExists(id));
    }
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
    [Tool("A11.Atomic.Disposable","disposable")]
    public sealed class DisposableProbe : Probe,IDisposable
    {
        public static int Constructions,Disposals;
        public DisposableProbe()=>Constructions++;
        public void Dispose()=>Disposals++;
    }
    [Tool("A11.Atomic.BadDisposable","bad disposable")]
    public sealed class BadDisposable : Probe,IDisposable
    {
        public static int Disposals;
        public override IReadOnlyList<OutputVariable> OutputVariables=>throw new InvalidOperationException("Metadata failed.");
        public void Dispose()=>Disposals++;
    }
    [Tool("A11.Atomic.Unknown","unknown metadata")]
    public sealed class UnknownMetadata : Probe
    { public override IReadOnlyList<OutputVariable> OutputVariables=>new[]{new OutputVariable{Name="value",VariableType="NotADeclaredType"}}; }
    [Tool("A11.Atomic.Prepared","prepared")]
    public sealed class PreparedProbe : Probe { }
}
