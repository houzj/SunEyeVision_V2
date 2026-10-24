using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ParameterRegistrationGenerationTests
{
    public sealed class GenerationParameters : ToolParameters {public int Number{get;set;}}
    public sealed class ConflictingParameters : ToolParameters { }

    private static JsonSerializerOptions[] Facades()=>new[]{JsonSerializationOptions.Default,JsonSerializationOptions.Compact,WorkflowSerializationOptions.Default,WorkflowSerializationOptions.Compact};

    [Fact]
    public void Prewarmed_four_facades_refresh_together_and_captured_options_keep_their_catalog()
    {
        var before=Facades();
        foreach(var options in before)JsonSerializer.Serialize<ToolParameters>(new GenericToolParameters(),options);
        var prepared=ParameterTypeRegistry.PrepareRegistration(new[]{new KeyValuePair<Type,string>(typeof(GenerationParameters),"A11.Generation.Parameters")});
        Assert.Equal(before,Facades());
        using(var commit=prepared.EnterCommit())commit.Publish();
        var after=Facades();
        for(var index=0;index<after.Length;index++)
        {
            Assert.NotSame(before[index],after[index]);
            var json=JsonSerializer.Serialize<ToolParameters>(new GenerationParameters{Number=37},after[index]);
            Assert.Equal(37,Assert.IsType<GenerationParameters>(JsonSerializer.Deserialize<ToolParameters>(json,after[index])).Number);
            Assert.ThrowsAny<Exception>(()=>JsonSerializer.Deserialize<ToolParameters>(json,before[index]));
            Assert.DoesNotContain("A11.Generation.Parameters",JsonSerializer.Serialize<ToolParameters>(new GenericToolParameters(),before[index]));
        }
        Assert.Equal(after,Facades());
        Assert.Throws<InvalidOperationException>(()=>ParameterTypeRegistry.PrepareRegistration(new[]{new KeyValuePair<Type,string>(typeof(ConflictingParameters),"A11.Generation.Parameters")}));
        Assert.Throws<InvalidOperationException>(()=>ParameterTypeRegistry.PrepareRegistration(new[]{new KeyValuePair<Type,string>(typeof(GenerationParameters),"A11.Generation.Renamed")}));
        Assert.Equal(after,Facades());
    }
}
