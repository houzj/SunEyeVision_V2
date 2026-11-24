using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.ContourMatch.Wpf.Views;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

public sealed class ContourMatchTemplateTrainingCodecTests
{
    [Fact]
    public void Decode_PreservesNoneCoverageForEmptyBackgroundOnSecondCreate()
    {
        var shape = new RecordValue(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = "rectangle",
            ["center"] = RoiDocumentValue.Point(100, 80),
            ["width"] = 40d,
            ["height"] = 30d,
            ["angle-degrees"] = 0d
        });
        var document = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit,
                new[] { RoiDocumentValue.Item(Guid.NewGuid(), "正样本1", shape) }),
            RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));

        var roundTripped = TemplateTrainingCodec.Decode(TemplateTrainingCodec.Encode(document));

        RoiDocumentValue.Validate(roundTripped);
        Assert.Equal(RoiDocumentValue.Explicit, RoiDocumentValue.Coverage(roundTripped, RoiDocumentValue.Inspection));
        Assert.Equal(RoiDocumentValue.None, RoiDocumentValue.Coverage(roundTripped, RoiDocumentValue.Mask));
        Assert.Empty(RoiDocumentValue.Items(roundTripped, RoiDocumentValue.Mask));
    }
}
