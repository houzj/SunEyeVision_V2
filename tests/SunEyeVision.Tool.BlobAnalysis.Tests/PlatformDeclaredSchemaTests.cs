using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

public sealed class PlatformDeclaredSchemaTests
{
    [Fact]
    public void Platform_loader_reproduces_the_blob_settings_schema_exactly()
    {
        using var stream = typeof(BlobSettings).Assembly
            .GetManifestResourceStream("BlobAnalysis.BlobParameters.xml");
        Assert.NotNull(stream);
        var platform = DeclaredParameterSchema.LoadWithTypes(stream!, typeof(BlobSettings), null,
            BlobRoiInheritance.ExplicitType, BlobRoiInheritance.Ports,
            typeof(BlobFindParameters));

        var tool = BlobParameterDefinition.Instance.SettingsSchema;
        Assert.Equal(tool.Inputs.Select(port => port.Id), platform.Schema.Inputs.Select(port => port.Id));

        var expected = tool.Presentation!.Fields;
        var actual = platform.Schema.Presentation!.Fields;
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].FieldId, actual[index].FieldId);
            Assert.Equal(expected[index].DisplayName, actual[index].DisplayName);
            Assert.Equal(expected[index].Group, actual[index].Group);
            Assert.Equal(expected[index].Order, actual[index].Order);
            Assert.Equal(expected[index].Editor, actual[index].Editor);
            Assert.Equal(expected[index].ChoiceKeys, actual[index].ChoiceKeys);
            Assert.Equal(expected[index].Layout, actual[index].Layout);
        }
    }

    [Fact]
    public void Platform_loader_exposes_the_same_defaults_and_field_metadata()
    {
        using var stream = typeof(BlobSettings).Assembly
            .GetManifestResourceStream("BlobAnalysis.BlobParameters.xml");
        var platform = DeclaredParameterSchema.LoadWithTypes(stream!, typeof(BlobSettings), null,
            BlobRoiInheritance.ExplicitType, BlobRoiInheritance.Ports,
            typeof(BlobFindParameters));
        var tool = BlobParameterDefinition.Instance;

        Assert.Equal(tool.Default<int>("Threshold"), platform.Default<int>("Threshold"));
        Assert.Equal(tool.Default<int>("UpperThreshold"), platform.Default<int>("UpperThreshold"));
        Assert.Equal(tool.Default<BlobSegmentationMode>("SegmentationMode"), platform.Default<BlobSegmentationMode>("SegmentationMode"));
        Assert.Equal(tool.Default<BlobThresholdMode>("Type"), platform.Default<BlobThresholdMode>("Type"));

        foreach (var id in new[] { "Threshold", "UpperThreshold", "SegmentationMode", "Type" })
        {
            Assert.Equal(tool.Label(id), platform.Label(id));
            Assert.Equal(tool.Page(id), platform.Page(id));
            Assert.Equal(tool.Section(id), platform.Section(id));
            Assert.Equal(tool.VmCategory(id), platform.VmCategory(id));
            Assert.Equal(tool.Minimum(id), platform.Minimum(id));
            Assert.Equal(tool.Maximum(id), platform.Maximum(id));
            Assert.Equal(tool.Increment(id), platform.Increment(id));
            Assert.Equal(tool.DecimalPlaces(id), platform.DecimalPlaces(id));
            Assert.Equal(tool.SettingsFieldId(id), platform.SettingsFieldId(id));
        }
    }
}
