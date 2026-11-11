using System.IO;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

public sealed class BlobSessionChainRegressionTests
{
    [Fact]
    public async Task Drawn_and_subscribed_masks_keep_separate_count_limits_through_execution()
    {
        using var resources = new FrozenOutputResources();
        await using var session = new BlobSession();
        var image = resources.CreateGray8(24, 24, Enumerable.Repeat((byte)200, 24 * 24).ToArray());
        var points = new[] { new Point2(2, 2), new Point2(8, 2), new Point2(2, 8) };
        var shape = new RecordValue(BlobLocalRoi.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = "polygon",
            ["vertices"] = new SequenceValue(TypeRef.SequenceOf(BlobLocalRoi.PointType),
                points.Select(point => BlobLocalRoi.Point(point.X, point.Y)))
        });
        var tool = new BlobTool();
        var ports = tool.Describe().Inputs;
        var fields = new FieldSettings(ports.Concat(tool.SettingsSchema.Inputs), new Dictionary<string, ParameterSetting>());
        RecordValue LocalMask(int count) => BlobLocalRoi.Root(
            BlobLocalRoi.Empty.Get<RecordValue>(BlobLocalRoi.Inspection),
            BlobLocalRoi.Group(BlobLocalRoi.Explicit,
                Enumerable.Range(0, count).Select(index => BlobLocalRoi.Item(Guid.NewGuid(), $"屏蔽区{index + 1}", shape))));
        InputValues LocalInput(RecordValue roi) => new(ports, new Dictionary<string, object?>
        {
            [BlobTool.ImageInput] = image, [BlobLocalRoi.InputId] = roi
        });
        var settings = Base(BlobSegmentationMode.SingleThreshold);
        var drawn = LocalMask(8);
        using var local = session.Execute(settings, BlobInput.Read(LocalInput(drawn)), resources);

        var overflow = LocalMask(9);
        Assert.Throws<ArgumentException>(() => tool.ExpandChanges(fields,
            new Dictionary<string, ParameterSetting> { [BlobLocalRoi.InputId] = new ConstantSetting(overflow) }));
        Assert.Throws<ArgumentException>(() => BlobInput.Read(LocalInput(overflow)));
        Assert.Throws<ArgumentException>(() => session.Execute(settings, image, resources, localRoi: overflow));

        var subscribed = new SequenceValue(BlobMaskInheritance.PolygonStrings,
            Enumerable.Repeat("2,2,4,2,4,4,2,4", 9));
        var values = new InputValues(ports, new Dictionary<string, object?>
        {
            [BlobTool.ImageInput] = image,
            [BlobMaskInheritance.ModeId] = true,
            [BlobMaskInheritance.SourceId] = subscribed
        });
        using var inherited = session.Execute(settings, BlobInput.Read(values), resources);
        Assert.Equal(local.BinaryImage!.CopyPixels(), inherited.BinaryImage!.CopyPixels());
        var masks = inherited.ResultGeometry.Groups.Single(group => group.Role == ResultGeometryRole.Mask);
        Assert.Equal(9, masks.Shapes.Count);
    }

    [Theory]
    [InlineData("full-screen", true)]
    [InlineData("rectangle", true)]
    [InlineData("image-mask", false)]
    public async Task Full_frame_direct_geometry_excludes_top_and_left_without_changing_the_published_mask(
        string selection, bool directRectangle)
    {
        const int side = 5;
        using var inputs = new FrozenOutputResources();
        using var outputs = new FrozenOutputResources();
        await using var session = new BlobSession();
        var image = inputs.CreateGray8(side, side, Enumerable.Repeat((byte)200, side * side).ToArray());
        var white = Enumerable.Repeat((byte)255, side * side).ToArray();
        var settings = Base(BlobSegmentationMode.SingleThreshold) with { Threshold = 100, Type = BlobThresholdMode.Binary };
        var values = new Dictionary<string, object?>
        {
            [BlobTool.ImageInput] = image,
            [BlobImageRoi.OutputEnabledId] = true
        };
        if (selection == "rectangle")
        {
            var shape = new RecordValue(BlobLocalRoi.ShapeType, new Dictionary<string, object?>
            {
                ["kind"] = "rectangle", ["center"] = BlobLocalRoi.Point(side / 2d, side / 2d),
                ["width"] = (double)side, ["height"] = (double)side, ["angle-degrees"] = 0d
            });
            values[BlobLocalRoi.InputId] = BlobLocalRoi.Root(
                BlobLocalRoi.Group(BlobLocalRoi.Explicit, new[] { BlobLocalRoi.Item(Guid.NewGuid(), "矩形", shape) }),
                BlobLocalRoi.Group(BlobLocalRoi.None, Array.Empty<RecordValue>()));
        }
        else if (selection == "image-mask")
        {
            values[BlobImageRoi.ModeId] = 1L;
            values[BlobImageRoi.ImageId] = inputs.CreateGray8(side, side, white);
        }
        var input = BlobInput.Read(new InputValues(new BlobTool().Describe().Inputs, values));
        using var output = session.Execute(settings, input, outputs);

        var expected = white.Select((pixel, index) =>
            directRectangle && (index / side == 0 || index % side == 0) ? (byte)0 : pixel).ToArray();
        Assert.Equal(expected, output.BinaryImage!.CopyPixels());
        Assert.Equal(expected, output.BlobImage!.CopyPixels());
        Assert.Equal(white, output.MaskImage!.CopyPixels());
        Assert.Equal(directRectangle ? 16d : 25d, Assert.Single(output.Measurements).Area);
        var inspection = output.ResultGeometry.Groups.Single(group => group.Role == ResultGeometryRole.Inspection);
        Assert.Equal(ResultGeometryCoverage.Explicit, inspection.Coverage);
        var published = output.ToValues();
        if (directRectangle)
        {
            var rectangle = Assert.Single(inspection.Shapes);
            Assert.Equal("rectangle", rectangle.Get<string>("kind"));
            Assert.Equal(new Point2(side / 2d, side / 2d), rectangle.Get<PointGeometry>("center").Point);
            Assert.Equal((double)side, rectangle.Get<double>("width"));
            Assert.Equal((double)side, rectangle.Get<double>("height"));
            Assert.Equal(0d, rectangle.Get<double>("angle-degrees"));
            var inherited = Assert.IsType<RecordValue>(Assert.Single(Assert.IsType<SequenceValue>(published[BlobSchema.DetectionRectanglesPort])));
            Assert.Equal(BlobRoiInheritance.RectangleType, inherited.Declaration);
            Assert.Equal(side / 2d, inherited.Get<RecordValue>("center").Get<double>("x"));
            Assert.Equal((double)side, inherited.Get<double>("width"));
        }
        else Assert.Null(published[BlobSchema.DetectionRectanglesPort]);
    }

    [Theory]
    [InlineData("annulus")]
    [InlineData("sector")]
    public async Task Published_detection_annulus_is_consumable_by_the_next_blob(string kind)
    {
        using var resources = new FrozenOutputResources();
        await using var session = new BlobSession();
        var image = resources.CreateGray8(32, 32, Enumerable.Repeat((byte)200, 32 * 32).ToArray());
        var ring = new RecordValue(BlobLocalRoi.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = kind, ["center"] = BlobLocalRoi.Point(16, 16), ["inner-radius"] = 4d,
            ["outer-radius"] = 9d, ["start-radians"] = Math.PI / 4d, ["end-radians"] = 3d * Math.PI / 4d
        });
        var roi = BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.Explicit, new[] { BlobLocalRoi.Item(Guid.NewGuid(), "扇环", ring) }),
            BlobLocalRoi.Group(BlobLocalRoi.None, Array.Empty<RecordValue>()));
        var ports = new BlobTool().Describe().Inputs;
        var settings = Base(BlobSegmentationMode.SingleThreshold);
        using var producer = session.Execute(settings, BlobInput.Read(new InputValues(ports, new Dictionary<string, object?>
        {
            [BlobTool.ImageInput] = image, [BlobLocalRoi.InputId] = roi
        })), resources);
        var annuli = Assert.IsType<SequenceValue>(producer.ToValues()[BlobSchema.DetectionAnnuliPort]);
        var source = Assert.IsType<RecordValue>(Assert.Single(annuli));
        Assert.Equal(45d, source.Get<double>("start-degrees"), 10);
        Assert.Equal(90d, source.Get<double>("sweep-degrees"), 10);
        using var consumer = session.Execute(settings, BlobInput.Read(new InputValues(ports, new Dictionary<string, object?>
        {
            [BlobTool.ImageInput] = image,
            [BlobRoiInheritance.ModeId] = BlobRoiInheritance.Annulus,
            [BlobRoiInheritance.AnnulusSource] = annuli
        })), resources);
        Assert.Equal(producer.BinaryImage!.CopyPixels(), consumer.BinaryImage!.CopyPixels());
        Assert.Equal(producer.Count, consumer.Count);
    }

    [Fact]
    public async Task Geometric_roi_translates_into_the_image_before_domain_clipping()
    {
        using var inputs=new FrozenOutputResources();
        using var outputs=new FrozenOutputResources();
        await using var session=new BlobSession();
        var image=inputs.CreateGray8(8,8,Enumerable.Repeat((byte)200,64).ToArray());
        var shape=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>
        {
            ["kind"]="rectangle",["center"]=BlobLocalRoi.Point(-3,2),["width"]=2d,["height"]=2d,["angle-degrees"]=0d
        });
        var maskShape = new RecordValue(BlobLocalRoi.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = "polygon",
            ["vertices"] = new SequenceValue(TypeRef.SequenceOf(BlobLocalRoi.PointType),
                new[] { BlobLocalRoi.Point(-5, 5), BlobLocalRoi.Point(-3, 5), BlobLocalRoi.Point(-3, 7), BlobLocalRoi.Point(-5, 7) })
        });
        var roi=BlobLocalRoi.Root(
            BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"矩形",shape)}),
            BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"屏蔽区",maskShape)}));
        var tool=new BlobTool();var descriptor=tool.Describe();
        var fixture=new RecordValue(FixtureDeclarations.Info,new Dictionary<string,object?>
        {
            ["reference"]=FixturePose(0,0,0,1,1),["run"]=FixturePose(6,0,0,1,1)
        });
        var enabled=BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsPositionCorrectionEnabled));
        var fields=new FieldSettings(descriptor.Inputs.Concat(tool.SettingsSchema.Inputs),
            new Dictionary<string,ParameterSetting> { [enabled]=new ConstantSetting(true) });
        var bindings=tool.ExpandChanges(fields,new Dictionary<string,ParameterSetting> { [BlobTool.PoseInput]=new ConstantSetting(fixture) });
        var inputValues=bindings.ToDictionary(pair=>pair.Key,pair=>((ConstantSetting)pair.Value).Value);
        inputValues.Add(BlobTool.ImageInput,image);inputValues.Add(BlobLocalRoi.InputId,roi);
        var values=new InputValues(descriptor.Inputs,inputValues);
        using var output=session.Execute(Base(BlobSegmentationMode.SingleThreshold) with{IsPositionCorrectionEnabled=true},
            BlobInput.Read(values),outputs);
        var measurement=Assert.Single(output.Measurements);
        Assert.Equal(3d,measurement.CentroidX);
        Assert.Equal(2d,measurement.CentroidY);
        Assert.Equal(9d,measurement.Area);
        var snapshot=output.ResultGeometry;
        Assert.Equal(image.Frame,snapshot.SourceFrame);
        var detection=snapshot.Groups.Single(group=>group.Role==ResultGeometryRole.Inspection);
        Assert.False(detection.OutlineOnly);
        Assert.Equal(ResultGeometryCoverage.Explicit,detection.Coverage);
        var corrected = Assert.Single(detection.Shapes);
        Assert.Equal(new Point2(3, 2), corrected.Get<PointGeometry>("center").Point);
        Assert.Equal(2d, corrected.Get<double>("width"));
        Assert.Equal(2d, corrected.Get<double>("height"));
        Assert.Equal(new BlobOrientedBounds(3, 2, 2, 2, 0), Assert.Single(output.ResultProjection.ROI!));
        var mask = snapshot.Groups.Single(group => group.Role == ResultGeometryRole.Mask);
        Assert.False(mask.OutlineOnly);
        var maskVertices = Assert.Single(mask.Shapes).Get<SequenceValue>("vertices").Cast<PointGeometry>().Select(point => point.Point);
        var expectedMask = new Point2[] { new(1, 5), new(3, 5), new(3, 7), new(1, 7) };
        Assert.Equal(expectedMask, maskVertices);
        Assert.Equal(expectedMask, Assert.IsType<PolygonGeometry>(Assert.Single(output.MaskPolygons)).Vertices);
        Assert.Equal(expectedMask, output.ResultProjection.BlindPolygonPoints);
        Point2 FixturePoint(ResultGeometryRole role) => Assert.Single(snapshot.Groups.Single(group=>group.Role==role).Shapes)
            .Get<PointGeometry>("center").Point;
        Assert.Equal(new Point2(0,0),FixturePoint(ResultGeometryRole.ReferencePoint));
        Assert.Equal(new Point2(6,0),FixturePoint(ResultGeometryRole.RunPoint));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Corrected_annulus_keeps_the_declaration_only_when_its_radii_remain_circular(bool uniform)
    {
        using var resources = new FrozenOutputResources();
        await using var session = new BlobSession();
        var image = resources.CreateGray8(32, 32, Enumerable.Repeat((byte)200, 1024).ToArray());
        var shape = new RecordValue(BlobLocalRoi.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = "annulus", ["center"] = BlobLocalRoi.Point(4, 4),
            ["inner-radius"] = 2d, ["outer-radius"] = 5d,
            ["start-radians"] = Math.PI / 4, ["end-radians"] = 3 * Math.PI / 4
        });
        var roi = BlobLocalRoi.Root(
            BlobLocalRoi.Group(BlobLocalRoi.Explicit, new[] { BlobLocalRoi.Item(Guid.NewGuid(), "扇环", shape) }),
            BlobLocalRoi.Group(BlobLocalRoi.None, Array.Empty<RecordValue>()));
        var pose = new RecordValue(SpatialDeclarations.Affine2D, new Dictionary<string, object?>
        {
            ["from"] = BlobSchema.ImageFrame.Id, ["to"] = BlobSchema.ImageFrame.Id,
            ["m11"] = 0d, ["m12"] = 2d, ["m21"] = uniform ? -2d : -1d, ["m22"] = 0d,
            ["offset-x"] = 16d, ["offset-y"] = 4d
        });
        using var output = session.Execute(Base(BlobSegmentationMode.SingleThreshold) with { IsPositionCorrectionEnabled = true },
            image, resources, pose: pose, localRoi: roi);
        var annulus = output.ResultGeometry.Groups.Single(group => group.Role == ResultGeometryRole.Annulus);
        Assert.Equal(!uniform, annulus.OutlineOnly);
        if (uniform)
            Assert.Equal(new BlobAnnulus(8, 12, 4, 10, 135, 90), Assert.Single(output.ResultProjection.ROIAnnulus!));
        else
            Assert.Null(output.ResultProjection.ROIAnnulus);
    }

    [Fact]
    public void Fixture_maps_reference_center_and_axes_to_run_pose()
    {
        var fixture=new RecordValue(FixtureDeclarations.Info,new Dictionary<string,object?>
        {
            ["reference"]=FixturePose(2,3,0,2,1),["run"]=FixturePose(10,20,90,4,3)
        });
        var matrix=FixtureDeclarations.ToAffine2D(fixture,BlobSchema.ImageFrame);
        Assert.Equal(0d,matrix.Get<double>("m11"),10);
        Assert.Equal(2d,matrix.Get<double>("m12"),10);
        Assert.Equal(-3d,matrix.Get<double>("m21"),10);
        Assert.Equal(0d,matrix.Get<double>("m22"),10);
        Assert.Equal(19d,matrix.Get<double>("offset-x"),10);
        Assert.Equal(16d,matrix.Get<double>("offset-y"),10);
    }

    private static RecordValue FixturePose(double x,double y,double angle,double scaleX,double scaleY) =>
        new(FixtureDeclarations.Pose,new Dictionary<string,object?>
        {
            ["point"]=new RecordValue(FixtureDeclarations.Point,new Dictionary<string,object?> {["x"]=x,["y"]=y}),
            ["angle"]=angle,["scale-x"]=scaleX,["scale-y"]=scaleY
        });

    [Theory]
    [InlineData(BlobContourType.NoneContour,false,false)]
    [InlineData(BlobContourType.NoneSequentialContour,true,false)]
    [InlineData(BlobContourType.SequentialContour,true,true)]
    public async Task Contour_modes_publish_points_and_topology_independently(BlobContourType mode,bool hasPoints,bool hasBoundary)
    {
        using var inputs=new FrozenOutputResources();
        using var outputs=new FrozenOutputResources();
        await using var session=new BlobSession();
        var image=inputs.CreateGray8(4,4,Enumerable.Repeat((byte)200,16).ToArray());
        var settings=Base(BlobSegmentationMode.SingleThreshold) with {FindParameters=new BlobFindParameters {BlobContourType=mode}};
        using var output=session.Execute(settings,image,outputs);
        var measurement=Assert.Single(output.Measurements);
        var row=BlobSchema.Freeze(measurement);
        Assert.Equal(hasPoints,row.Contains("outline-points"));
        Assert.Equal(hasBoundary,row.Contains("boundary"));
        var published = output.ToValues();
        var area = Assert.IsType<double[]>(published[BlobSchema.Field(19)]);
        area[0] = -1;
        var nextRead = output.ToValues();
        Assert.Equal(measurement.Area, Assert.Single(Assert.IsType<double[]>(nextRead[BlobSchema.Field(19)])));
        Assert.Same(published[BlobSchema.BlobsPort], nextRead[BlobSchema.BlobsPort]);
        var moduleContour = nextRead[BlobSchema.ContourPointsPort];
        if (hasPoints)
        {
            var points = row.Get<SequenceValue>("outline-points").Cast<PointGeometry>().ToArray();
            var aggregate = Assert.IsType<RecordValue>(moduleContour);
            Assert.Equal((long)points.Length, aggregate.Get<long>("count"));
            var info = aggregate.Get<RecordValue>("info");
            Assert.Equal(points.Select(point => (double)(float)point.Point.X), info.Get<double[]>("x"));
            Assert.Equal(points.Select(point => (double)(float)point.Point.Y), info.Get<double[]>("y"));
            var mixed = BlobResultProjection.Create(new[] { measurement with { JudgmentPassed = true },
                measurement with { JudgmentPassed = false } }, null, null, output.ResultGeometry, publishContour:true);
            Assert.Equal(Enumerable.Repeat(0d, points.Length).Concat(Enumerable.Repeat(2d, points.Length)),
                mixed.ContourInfo!.Get<double[]>("score"));
            Assert.Equal(Enumerable.Repeat(0L, points.Length).Concat(Enumerable.Repeat(1L, points.Length)),
                mixed.ContourInfo.Get<SequenceValue>("blob-index").Cast<long>());
        }
        else Assert.Null(moduleContour);
        if (hasBoundary)
        {
            var path = Assert.IsType<RecordValue>(Assert.Single(row.Get<RecordValue>("boundary").Get<SequenceValue>("paths")));
            var pathPoints = path.Get<SequenceValue>("points");
            Assert.Same(pathPoints, row.Get<SequenceValue>("contour"));
            Assert.Same(pathPoints, row.Get<SequenceValue>("outline-points"));
        }
        if(mode==BlobContourType.NoneSequentialContour)
            Assert.Equal(12,measurement.UnorderedContour!.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Image_roi_limits_threshold_output_and_publishes_an_independent_mask_when_enabled(bool outputMask)
    {
        using var inputs=new FrozenOutputResources();
        using var outputs=new FrozenOutputResources();
        await using var session=new BlobSession();
        var pixels=Enumerable.Repeat((byte)200,16).ToArray();pixels[5]=0;
        var image=inputs.CreateGray8(4,4,pixels);
        var maskPixels=new byte[]{0,0,0,0,0,1,127,0,0,255,1,0,0,0,0,0};
        var mask=inputs.CreateGray8(4,4,maskPixels);
        using var output=session.Execute(Base(BlobSegmentationMode.SingleThreshold),
            new BlobInput(image,null,null,null,ImageMask:mask,OutputMask:outputMask),outputs);
        Assert.Equal(3d,Assert.Single(output.Measurements).Area);
        var detection=output.ResultGeometry.Groups.Single(group=>group.Role==ResultGeometryRole.Inspection);
        Assert.Equal(image.Frame,output.ResultGeometry.SourceFrame);
        Assert.True(detection.OutlineOnly);
        Assert.Single(detection.Shapes);
        Assert.DoesNotContain(output.ResultGeometry.Groups, group => group.Role is ResultGeometryRole.ReferencePoint or ResultGeometryRole.RunPoint);
        Assert.Equal(maskPixels.Select((pixel,index)=>pixel==0||pixels[index]==0?(byte)0:(byte)255),output.BinaryImage!.CopyPixels());
        var published=output.ToValues()[BlobSchema.MaskImagePort];
        Assert.DoesNotContain(nameof(BlobResultProjection.OutputMask), output.ResultProjection.Unavailable);
        Assert.Same(output.MaskImage, output.ResultProjection.OutputMask);
        if(outputMask)
        {
            var record=Assert.IsType<RecordValue>(published);
            var publishedImage = record.Get<IReadOnlyImage>("data");
            Assert.Same(output.MaskImage, publishedImage);
            Assert.Equal(maskPixels.Select(pixel=>pixel==0?(byte)0:(byte)255),publishedImage.CopyPixels());
        }
        else
        {
            Assert.Null(published);
            Assert.Null(output.ResultProjection.OutputMask);
        }
    }

    [Fact]
    public async Task Raster_roi_preserves_outer_and_hole_boundaries_without_filling_either()
    {
        using var inputs=new FrozenOutputResources();
        using var outputs=new FrozenOutputResources();
        await using var session=new BlobSession();
        var image=inputs.CreateGray8(6,6,Enumerable.Repeat((byte)200,36).ToArray());
        var pixels=new byte[36];
        for(var y=1;y<=4;y++)
            for(var x=1;x<=4;x++)
                if(y is 1 or 4 || x is 1 or 4) pixels[y*6+x]=255;
        var mask=inputs.CreateGray8(6,6,pixels);
        using var output=session.Execute(Base(BlobSegmentationMode.SingleThreshold),
            new BlobInput(image,null,null,null,ImageMask:mask),outputs);
        Assert.Equal(12d,Assert.Single(output.Measurements).Area);
        var detection=output.ResultGeometry.Groups.Single(group=>group.Role==ResultGeometryRole.Inspection);
        Assert.Equal(ResultGeometryCoverage.Explicit,detection.Coverage);
        Assert.True(detection.OutlineOnly);
        Assert.Equal(2,detection.Shapes.Count);
    }

    [Fact]
    public async Task Published_polygon_mask_can_be_inherited_by_the_next_blob()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new BlobSession();
        var image=resources.CreateGray8(8,8,Enumerable.Repeat((byte)200,64).ToArray());
        var vertices=new SequenceValue(TypeRef.SequenceOf(BlobLocalRoi.PointType),
            new[]{BlobLocalRoi.Point(2,2),BlobLocalRoi.Point(4,2),BlobLocalRoi.Point(4,4),BlobLocalRoi.Point(2,4)});
        var polygon=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?> {["kind"]="polygon",["vertices"]=vertices});
        var root=BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.FullDomain,Array.Empty<RecordValue>()),
            BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"屏蔽区",polygon)}));
        var settings=Base(BlobSegmentationMode.SingleThreshold);
        using var producer=session.Execute(settings,image,resources,localRoi:root);
        Assert.Equal(55d,Assert.Single(producer.Measurements).Area);
        var published=Assert.IsType<string[]>(producer.ToValues()[BlobSchema.MaskPolygonStringsPort]);
        Assert.Equal("2.000000,2.000000,4.000000,2.000000,4.000000,4.000000,2.000000,4.000000",Assert.Single(published));
        var values=new InputValues(new BlobTool().Describe().Inputs,new Dictionary<string,object?>
        {
            [BlobTool.ImageInput]=image,[BlobMaskInheritance.ModeId]=true,[BlobMaskInheritance.SourceId]=published
        });
        using var consumer=session.Execute(settings,BlobInput.Read(values),resources);
        Assert.Equal(producer.BinaryImage!.CopyPixels(),consumer.BinaryImage!.CopyPixels());
        Assert.Equal(55d,Assert.Single(consumer.Measurements).Area);
        var mask=consumer.ResultGeometry.Groups.Single(group=>group.Role==ResultGeometryRole.Mask);
        Assert.Equal("polygon",Assert.Single(mask.Shapes).Get<string>("kind"));
    }

    [Theory]
    [InlineData("1,2,3,4,5")]
    [InlineData("1,2,NaN,4,5,6")]
    [InlineData("1,2,3,,5,6")]
    [InlineData("1,2,3,4,5,1e100")]
    public void Malformed_inherited_mask_is_rejected_before_algorithm_execution(string payload)
    {
        var inputs = new InputValues(BlobMaskInheritance.Ports, new Dictionary<string, object?>
        {
            [BlobMaskInheritance.ModeId] = true,
            [BlobMaskInheritance.SourceId] = new[] { payload }
        });
        Assert.Throws<ArgumentException>(() => BlobMaskInheritance.Materialize(inputs, BlobLocalRoi.Empty));
    }

    [Fact]
    public void Disabled_outside_pixel_filter_preserves_its_binding_without_requiring_the_source()
    {
        using var host = new Host(Base(BlobSegmentationMode.SingleThreshold), new byte[32 * 32], 32, 32);
        var definition = BlobParameterDefinition.Instance;
        var limit = definition.SettingsFieldId(nameof(BlobFindParameters.MaxOutPixelNum));
        var enable = definition.SettingsFieldId(nameof(BlobFindParameters.MaxOutPixelNumEnable));
        var draft = host.Session.Editing.Host.BeginEdit(host.Flow.Id, host.Node.Id);
        var reference = new OutputReference("missing", "pixel-count", DataType.Integer, scopeId: host.Flow.Id);
        draft.ApplyFields(new Dictionary<string, ParameterSetting> { [limit] = new ReferenceSetting(reference) });
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host, nodes: new[] { draft }));

        var execution = host.Execute();
        using (execution.Batch)
        using (execution.Result)
            Assert.True(execution.Run.Success, string.Join(";", execution.Run.Errors));
        var retained = Assert.IsType<ReferenceSetting>(host.Node.Configuration.Fields.Read(limit)).Reference;
        Assert.Equal(reference.NodeId, retained.NodeId);
        Assert.Equal(reference.PortId, retained.PortId);
        Assert.Equal(reference.ScopeId, retained.ScopeId);

        var activate = host.Session.Editing.Host.BeginEdit(host.Flow.Id, host.Node.Id);
        activate.ApplyFields(new Dictionary<string, ParameterSetting> { [enable] = new ConstantSetting(true) });
        Assert.Throws<InvalidOperationException>(() => host.Session.CommitEdit(
            DocumentEditProposal.Prepare(host.Session.Editing.Host, nodes: new[] { activate })));
        Assert.False(Assert.IsType<bool>(Assert.IsType<ConstantSetting>(host.Node.Configuration.Fields.Read(enable)).Value));
    }

    [Fact]
    public void Dormant_high_threshold_binding_is_saved_in_configuration_but_not_resolved_until_active()
    {
        using var host=new Host(Base(BlobSegmentationMode.SingleThreshold),new byte[32*32],32,32);
        var definition=BlobParameterDefinition.Instance;
        var upper=definition.SettingsFieldId(nameof(BlobSettings.UpperThreshold));
        var mode=definition.SettingsFieldId(nameof(BlobSettings.SegmentationMode));
        var binding=new ReferenceSetting(new OutputReference("missing","upper",DataType.Integer,scopeId:host.Flow.Id));
        var draft=host.Session.Editing.Host.BeginEdit(host.Flow.Id,host.Node.Id);
        draft.ApplyFields(new Dictionary<string,ParameterSetting>{{upper,binding}});
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{draft}));
        var execution=host.Execute();
        using(execution.Batch)
        using(execution.Result)
            Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        Assert.Equal("missing",Assert.IsType<ReferenceSetting>(host.Node.Configuration.Fields.Read(upper)).Reference.NodeId);

        var activate=host.Session.Editing.Host.BeginEdit(host.Flow.Id,host.Node.Id);
        activate.ApplyFields(new Dictionary<string,ParameterSetting>{{mode,new ConstantSetting("DoubleThreshold")}});
        Assert.Throws<InvalidOperationException>(()=>host.Session.CommitEdit(
            DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{activate})));
        Assert.Equal("SingleThreshold",Assert.IsType<ConstantSetting>(host.Node.Configuration.Fields.Read(mode)).Value);
    }

    [Theory]
    [InlineData(0.00000006,true)]
    [InlineData(0.00000024,false)]
    public void Centroid_limit_applies_original_single_precision_margin(double minimum,bool expected)
    {
        var pixels=new byte[32*32];
        for(var row=4;row<8;row++)pixels[row*32]=200;
        var vm=new BlobFindParameters {CentroidXLimitEnable=true,CentroidXLimitLow=minimum,CentroidXLimitHigh=1};
        using var host=new Host(Base(BlobSegmentationMode.None) with{FindParameters=vm},pixels,32,32);
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
        Assert.Equal(expected,Assert.Single(output.Measurements).JudgmentPassed);
        Assert.Equal(expected,output.IsPass);
        using var reader = execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        Assert.Equal(expected ? 1L : 0L, reader.Values!.Get<long>(BlobSchema.ModuleStatusPort));
        Assert.Equal(expected ? 1L : 0L, Assert.Single(reader.Values.Get<SequenceValue>(BlobSchema.BlobStatusesPort).Cast<long>()));
    }

    [Theory]
    [InlineData(false,false,0,false)]
    [InlineData(false,true,0,true)]
    [InlineData(true,false,0,true)]
    [InlineData(true,true,0,false)]
    [InlineData(true,false,1,false)]
    [InlineData(true,false,2,false)]
    [InlineData(false,true,1,false)]
    [InlineData(false,true,2,false)]
    public void Module_judgment_combines_zero_mode_and_overall_limits_without_rewriting_per_blob_status(
        bool hasBlob,bool zeroMode,int failedLimit,bool expected)
    {
        var pixels=new byte[32*32];
        if(hasBlob)for(var row=4;row<7;row++)for(var column=4;column<7;column++)pixels[row*32+column]=200;
        var vm=new BlobFindParameters {OKWhenNumIsZero=zeroMode,
            BlobNumLimitEnable=failedLimit==1,BlobNumLimitLow=2,BlobNumLimitHigh=3,
            BlobTotalAreaLimitEnable=failedLimit==2,BlobTotalAreaLimitLow=100,BlobTotalAreaLimitHigh=200};
        using var host=new Host(Base(BlobSegmentationMode.None) with{FindParameters=vm},pixels,32,32);
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
        Assert.Equal(expected,output.IsPass);
        Assert.Equal(hasBlob?1:0,output.Count);
        Assert.All(output.Measurements,item=>Assert.True(item.JudgmentPassed));
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        Assert.Equal(expected?1L:0L,reader.Values!.Get<long>(BlobSchema.ModuleStatusPort));
        Assert.Equal(hasBlob?new[]{1L}:Array.Empty<long>(),reader.Values.Get<SequenceValue>(BlobSchema.BlobStatusesPort).Cast<long>());
    }

    private sealed class DirectContext(IOutputResources resources) : IToolExecutionContext
    {
        public RunIdentity Identity { get; } = new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"direct",Guid.NewGuid());
        public CancellationToken Cancellation => default; public IOutputResources Resources => resources; public void Log(string message) { }
    }
    private sealed class Host : IDisposable
    {
        internal readonly DocumentSessionManager Manager;
        internal readonly DocumentSession Session;
        internal readonly WorkflowDefinition Flow;
        internal readonly NodeDefinition Node;
        private readonly FrozenOutputResources _inputs = new();
        private readonly IReadOnlyImage _image;
        internal IReadOnlyImage Input => _image;
        internal Host(BlobSettings settings, byte[] pixels, int width, int height)
        {
            Manager = new DocumentSessionManager(SunEyeVision.TestShared.ProductRuntimeTestHost.CreateRuntimeDirectory());
            Session = Manager.CreateNew("Blob正式会话");
            Flow = Session.GetWorkflows().Single();
            var graph = Session.PrepareGraphEdit(Flow.Id);
            Node = Session.CreateNode(graph, BlobTool.Id, new CanvasPoint(0, 0));
            Session.CommitGraphEdit(graph, "添加 Blob");
            var draft = Session.Editing.Host.BeginEdit(Flow.Id, Node.Id);
            var fields = new BlobTool().ReadSettings(settings).CopyValues()
                .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
            draft.ApplyFields(fields);
            Session.CommitEdit(DocumentEditProposal.Prepare(Session.Editing.Host, nodes: new[] { draft }));
            _image = _inputs.CreateGray8(width, height, pixels);
        }
        internal (DocumentRunBatch Batch, DocumentRunResult Result, ExecutionResult Run) Execute()
        {
            var batch = Session.CaptureRunBatch(new[] { Flow.Id }, workflowImages: new Dictionary<string, WorkflowImageInput>
            { [Flow.Id] = new WorkflowImageInput(_image, _image.AcquirePixels()) });
            try
            {
                var result = batch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                return (batch, result, result.Workflows[Flow.Id]);
            }
            catch { batch.Dispose(); throw; }
        }
        public void Dispose() { Manager.Dispose(); _inputs.Dispose(); }
    }

    private static BlobSettings Base(BlobSegmentationMode mode) => new()
    { SegmentationMode = mode, IsAreaFilterEnabled = false, OutputBlobImage = true,
      OutputBinaryImage = true, BlobCountLimit = 20000 };

    public static IEnumerable<object[]> Modes()
    {
        yield return new object[] { Base(BlobSegmentationMode.SingleThreshold) with { Threshold = 100 }, new byte[] {0,99,100,150,200,255}, new byte[] {0,0,255,255,255,255} };
        yield return new object[] { Base(BlobSegmentationMode.SingleThreshold) with { Threshold = 100, Type = BlobThresholdMode.BinaryInv }, new byte[] {0,99,100,255}, new byte[] {255,255,0,0} };
        yield return new object[] { Base(BlobSegmentationMode.SingleThreshold) with { Threshold = 0, Type = BlobThresholdMode.BinaryInv }, new byte[] {0,1,255}, new byte[] {0,0,0} };
        yield return new object[] { Base(BlobSegmentationMode.DoubleThreshold) with { Threshold = 100, UpperThreshold = 150 }, new byte[] {99,100,150,151}, new byte[] {0,255,255,0} };
        yield return new object[] { Base(BlobSegmentationMode.DoubleThreshold) with { Threshold = 150, UpperThreshold = 150 }, new byte[] {149,150,151}, new byte[] {0,255,0} };
        yield return new object[] { Base(BlobSegmentationMode.DoubleThreshold) with { Threshold = 150, UpperThreshold = 100 }, new byte[] {0,99,100,150,200,255}, new byte[] {255,255,0,0,255,255} };
        yield return new object[] { Base(BlobSegmentationMode.DoubleThreshold) with { Threshold = 255, UpperThreshold = 0 }, new byte[] {0,1,254,255}, new byte[] {0,0,0,0} };
        yield return new object[] { Base(BlobSegmentationMode.None), new byte[] {0,1,127,255}, new byte[] {0,1,127,255} };
        yield return new object[] { Base(BlobSegmentationMode.AutoThreshold), new byte[] {0,255}, new byte[] {255,255} };
        yield return new object[] { Base(BlobSegmentationMode.FixedSoftThreshold) with { FixedSoftLowerThreshold=150, FixedSoftUpperThreshold=100, ThresholdSoftness=254, Type=BlobThresholdMode.BinaryInv }, new byte[] {99,100,150,151,255}, new byte[] {255,255,255,0,0} };
        yield return new object[] { Base(BlobSegmentationMode.RelativeSoftThreshold) with { RelativeLeftRatio=0, RelativeRightRatio=0, RelativeLowerRatio=40, RelativeUpperRatio=60, Type=BlobThresholdMode.BinaryInv }, Enumerable.Range(0,256).Select(i=>(byte)i).ToArray(), Array.Empty<byte>() };
    }

    [Theory, MemberData(nameof(Modes))]
    public void Six_modes_run_through_admitted_document_session(BlobSettings settings, byte[] input, byte[] expected)
    {
        using var host = new Host(settings, input, input.Length, 1);
        var execution = host.Execute(); using var batch = execution.Batch; using var result = execution.Result;
        Assert.True(execution.Run.Success, string.Join(";", execution.Run.Errors));
        using var reader = execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        var binary = reader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data");
        var actual = binary.CopyPixels();
        if (settings.SegmentationMode == BlobSegmentationMode.RelativeSoftThreshold)
        {
            Assert.Equal((byte)255, actual[0]); Assert.Equal((byte)255, actual[101]);
            Assert.Equal((byte)0, actual[152]); Assert.Equal((byte)0, actual[255]);
        }
        else Assert.Equal(expected, actual);
        if (settings.SegmentationMode is BlobSegmentationMode.SingleThreshold or BlobSegmentationMode.DoubleThreshold)
        {
            var blobImage = reader.Values.Get<RecordValue>(BlobSchema.Field(5)).Get<IReadOnlyImage>("data");
            Assert.Equal(expected, blobImage.CopyPixels());
            Assert.Equal(expected.LongCount(pixel => pixel != 0), (long)reader.Values.Get<double>(BlobSchema.Field(12)));
        }
        foreach (var field in new[] { 5, 6 })
        {
            var imageRecord = reader.Values.Get<RecordValue>(BlobSchema.Field(field));
            var image = imageRecord.Get<IReadOnlyImage>("data");
            Assert.Equal((long)image.Width, imageRecord.Get<long>("width"));
            Assert.Equal((long)image.Height, imageRecord.Get<long>("height"));
            Assert.Equal(17301505L, imageRecord.Get<long>("pixel-format"));
        }
        Assert.Equal(settings.SegmentationMode.ToString(), reader.Values.Get<string>(BlobSchema.Field(102)));
    }

    [Theory]
    [InlineData(BlobSegmentationMode.AutoThreshold, 100, 100, 0, 255, 255, 255)]
    [InlineData(BlobSegmentationMode.RelativeSoftThreshold, 60, 160, 255, 139, 23, 0)]
    public async Task Histogram_thresholds_ignore_pixels_outside_the_mask_and_publish_the_work_image(
        BlobSegmentationMode mode, int lower, int upper, int first, int second, int third, int fourth)
    {
        using var inputs = new FrozenOutputResources();
        using var outputs = new FrozenOutputResources();
        await using var session = new BlobSession();
        var mask = inputs.CreateGray8(3, 3, new byte[] { 0, 255, 0, 255, 0, 255, 0, 255, 0 });
        var settings = Base(mode) with
        {
            Type = mode == BlobSegmentationMode.AutoThreshold ? BlobThresholdMode.Binary : BlobThresholdMode.BinaryInv,
            RelativeLeftRatio = 25, RelativeRightRatio = 25,
            RelativeLowerRatio = 25, RelativeUpperRatio = 75, ThresholdSoftness = 10
        };
        var expected = new byte[] { 0, (byte)first, 0, (byte)second, 0, (byte)third, 0, (byte)fourth, 0 };
        foreach (var outside in new byte[] { 0, 255 })
        {
            var image = inputs.CreateGray8(3, 3, new byte[] { outside, 10, outside, 100, outside, 150, outside, 210, outside });
            using var result = session.Execute(settings, new BlobInput(image, null, null, null, ImageMask: mask), outputs);
            Assert.Equal(lower, result.ThresholdMinUsed);
            Assert.Equal(upper, result.ThresholdMaxUsed);
            var published = Assert.IsType<RecordValue>(result.ToValues()[BlobSchema.Field(6)]);
            Assert.Equal(expected, published.Get<IReadOnlyImage>("data").CopyPixels());
        }
    }

    [Fact]
    public void Disabled_image_outputs_have_no_record_without_a_consumer()
    {
        var settings=Base(BlobSegmentationMode.SingleThreshold) with { OutputBlobImage=false, OutputBinaryImage=false };
        using var host=new Host(settings,new byte[]{0,255},2,1);
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        Assert.True(reader.Values!.Read(BlobSchema.Field(5)).IsNull);
        Assert.True(reader.Values.Read(BlobSchema.Field(6)).IsNull);
    }

    [Theory]
    [InlineData(5,6)]
    [InlineData(6,5)]
    public void Image_reference_obeys_the_producer_output_switch(int requestedField,int otherField)
    {
        var settings=Base(BlobSegmentationMode.SingleThreshold) with { OutputBlobImage=false, OutputBinaryImage=false };
        using var host=new Host(settings,new byte[]{0,255,255,0},4,1);
        var graph=host.Session.PrepareGraphEdit(host.Flow.Id);
        var consumer=host.Session.CreateNode(graph,BlobTool.Id,new CanvasPoint(220,0));
        graph.AddConnection(new ConnectionDefinition("image-consumer",host.Node.Id,consumer.Id,BlobSchema.Field(requestedField),BlobTool.ImageInput));
        host.Session.CommitGraphEdit(graph,"连接图像结果");
        var port=new BlobTool().Describe().Outputs.Single(item=>item.Id==BlobSchema.Field(requestedField));
        var edit=host.Session.Editing.Host.BeginEdit(host.Flow.Id,consumer.Id);
        edit.ApplyFields(new Dictionary<string,ParameterSetting>
        { [BlobTool.ImageInput]=new ReferenceSetting(new OutputReference(host.Node.Id,port.Id,port.Type,memberFieldPath:new[]{"data"},scopeId:host.Flow.Id)) });
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{edit}));
        var disabled=host.Execute();
        using(var batch=disabled.Batch)
        using(var result=disabled.Result)
        using(var reader=host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id))
        {
            Assert.NotNull(reader);
            Assert.True(reader.Snapshot.IsSuccess);
            Assert.True(reader.Values!.Read(BlobSchema.Field(requestedField)).IsNull);
            Assert.True(reader.Values.Read(BlobSchema.Field(otherField)).IsNull);
            Assert.False(disabled.Run.NodeResults[consumer.Id].Success);
        }

        var property=requestedField==5?nameof(BlobSettings.OutputBlobImage):nameof(BlobSettings.OutputBinaryImage);
        var field=BlobParameterDefinition.Instance.SettingsFieldId(property);
        var enable=host.Session.Editing.Host.BeginEdit(host.Flow.Id,host.Node.Id);
        enable.ApplyFields(new Dictionary<string,ParameterSetting> { [field]=new ConstantSetting(true) });
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{enable}));
        var enabled=host.Execute();using var enabledBatch=enabled.Batch;using var enabledResult=enabled.Result;
        Assert.True(enabled.Run.Success,string.Join(";",enabled.Run.Errors));
        Assert.True(enabled.Run.NodeResults[consumer.Id].Success);
        using var published=host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id);
        Assert.NotNull(published);
        var record=published.Values!.Get<RecordValue>(BlobSchema.Field(requestedField));
        Assert.Equal(4,record.Get<IReadOnlyImage>("data").CopyPixels().Length);
        Assert.Equal(4L,record.Get<long>("width"));
        Assert.True(published.Values.Read(BlobSchema.Field(otherField)).IsNull);
    }

    [Fact]
    public void Fixed_soft_reverse_lut_has_independent_exact_anchors()
    {
        var settings = Base(BlobSegmentationMode.FixedSoftThreshold) with
        { FixedSoftLowerThreshold=100, FixedSoftUpperThreshold=150, ThresholdSoftness=254, Type=BlobThresholdMode.BinaryInv };
        var pixels = Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        using var host = new Host(settings,pixels,256,1);
        var softnessId=BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.ThresholdSoftness));
        Assert.Equal(254L,Assert.IsType<ConstantSetting>(host.Node.Configuration.Fields.Read(softnessId)).Value);
        var tool = new BlobTool();
        var settingIds = tool.SettingsSchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var onlySettings = new FieldSettings(tool.SettingsSchema.Inputs, host.Node.Configuration.Fields.Capture().Where(item => settingIds.Contains(item.Key)).ToDictionary(item => item.Key, item => item.Value));
        var materialized=Assert.IsType<BlobSettings>(tool.MaterializeSettings(onlySettings.Materialize(new Dictionary<string,object?>())));
        Assert.Equal(254,materialized.ThresholdSoftness);
        var execution=host.Execute(); using var batch=execution.Batch; using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        var actualOutput=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
        Assert.Equal(100,actualOutput.ThresholdMinUsed); Assert.Equal(150,actualOutput.ThresholdMaxUsed);
        using var directResources = new FrozenOutputResources();
        var directSession = new BlobSession();
        using var direct = directSession.Execute(settings,host.Input,directResources);
        Assert.Equal((byte)5,direct.BinaryImage!.CopyPixels()[149]);
        directSession.DisposeAsync().AsTask().GetAwaiter().GetResult();        var actual=reader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data").CopyPixels();
        // Independent S4 scalar oracle for the complete 256-entry fixed-soft table.
        var expectedTable=new byte[256];
        for(var p=0;p<256;p++)
            expectedTable[p]=p<=100?(byte)255:p>=150?(byte)0:(byte)(int)Math.Clamp(255f-((p-100)+1)*255f/51f+0.5f,0f,255f);
        var outputImage = actualOutput.BinaryImage!;
        var outputPixels = outputImage.CopyPixels();
        var outputAnchors = new[] { outputPixels[100],outputPixels[101],outputPixels[102],outputPixels[149],outputPixels[150] };
        var readerImage = reader.Values.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data");
        var inputPixels = host.Input.CopyPixels();
        var diagnostics = string.Join(";", actualOutput.ExecutionDiagnostics.Select(item => item.Code+":"+item.Message));
        Assert.True(new byte[]{255,245,240,5,0}.SequenceEqual(new[]{actual[100],actual[101],actual[102],actual[149],actual[150]}),
            $"input149={inputPixels[149]}; output={string.Join(',',outputAnchors)}; field={string.Join(',',new[]{actual[100],actual[101],actual[102],actual[149],actual[150]})}; " +
            $"outputSha={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(outputPixels))}; fieldSha={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual))}; " +
            $"inputLayout={host.Input.Layout}; outputLayout={outputImage.Layout}; fieldLayout={readerImage.Layout}; sameImage={ReferenceEquals(outputImage,readerImage)}; " +
            $"used={actualOutput.ThresholdMinUsed}/{actualOutput.ThresholdMaxUsed}/{actualOutput.TypeUsed}/{actualOutput.SegmentationModeUsed}; diag={diagnostics}");
        Assert.Equal(expectedTable,actual);
    }

    [Fact]
    public void Blob_result_projection_publishes_gray_statistics_for_each_retained_region()
    {
        var pixels=Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        using var host=new Host(Base(BlobSegmentationMode.None),pixels,256,1);
        var execution=host.Execute();
        using var batch=execution.Batch;
        using var workflowResult=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        var result=output.ResultProjection;
        Assert.NotNull(result.MinGrayValue);
        Assert.NotNull(result.MaxGrayValue);
        Assert.NotNull(result.GrayContrast);
        Assert.Single(result.MinGrayValue!);
        Assert.Single(result.MaxGrayValue!);
        Assert.Single(result.GrayContrast!);
        Assert.Equal(1f,result.MinGrayValue[0]);
        Assert.Equal(255f,result.MaxGrayValue[0]);
        Assert.Equal(127.5f,result.GrayContrast[0]);
        Assert.NotNull(result.BlobAngle);
        Assert.Single(result.BlobAngle!);
        Assert.Equal((float)output.Measurements[0].BlobAngleDegrees!.Value,result.BlobAngle![0]);
        Assert.DoesNotContain(nameof(BlobResultProjection.BlobAngle),result.Unavailable);
        Assert.DoesNotContain(nameof(BlobResultProjection.MinGrayValue),result.Unavailable);
        Assert.DoesNotContain(nameof(BlobResultProjection.MaxGrayValue),result.Unavailable);
        Assert.DoesNotContain(nameof(BlobResultProjection.GrayContrast),result.Unavailable);
        var fields=new (string Port,double Value)[]
        {
            (BlobSchema.MinGrayValuesPort,1d), (BlobSchema.MaxGrayValuesPort,255d),
            (BlobSchema.GrayContrastsPort,127.5d), (BlobSchema.PrincipalAnglesPort,result.BlobAngle![0])
        };
        var tool=new BlobTool();
        foreach(var field in fields)
        {
            var port=reader.Values!.Declarations[field.Port];
            Assert.Equal(NumericStorage.Float32,port.Storage);
            Assert.Contains(tool.ManualReferenceSources,source=>source.PortId==field.Port);
            Assert.Equal(field.Value,Assert.IsType<double>(Assert.Single(reader.Values.Get<SequenceValue>(field.Port))));
        }
    }

    [Fact]
    public async Task Uniform_blob_publishes_zero_gray_contrast()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new BlobSession();
        var pixels=Enumerable.Repeat((byte)140,16).ToArray();
        var input=resources.CreateGray8(4,4,pixels);
        using var output=session.Execute(Base(BlobSegmentationMode.None),input,resources);
        var result=Assert.Single(output.Measurements);
        Assert.Equal(140d,result.MinGrayValue);
        Assert.Equal(140d,result.MaxGrayValue);
        Assert.Equal(0d,result.GrayContrast);
    }

    [Fact]
    public void Rectangle_and_axis_outputs_share_the_formal_measurement_row()
    {
        const int side=8;
        var pixels=new byte[side*side];
        for(var point=2;point<=5;point++)
        {
            pixels[point*side+point]=255;
            pixels[point*side+point+1]=255;
        }
        using var host=new Host(Base(BlobSegmentationMode.None) with {Connectivity=BlobConnectivity.Connected_8},pixels,side,side);
        var execution=host.Execute();
        using var batch=execution.Batch;
        using var workflowResult=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        var values=reader.Values!;
        var row=values.Read(BlobSchema.BlobsPort).GetElement(0);
        var rectangle=values.Read(BlobSchema.OuterRectanglesPort).GetElement(0);
        var rowRectangle=row.GetField("oriented-bounds");
        Assert.NotEqual(0d,rectangle.GetField("angle-degrees").GetScalar<double>());
        foreach(var axis in FixtureDeclarations.Point.Fields)
            Assert.Equal(rowRectangle.GetField("center").GetField(axis.Id).GetScalar<double>(),rectangle.GetField("center").GetField(axis.Id).GetScalar<double>());
        foreach(var field in BlobRoiInheritance.RectangleType.Fields.Where(field=>field.Id!="center"))
            Assert.Equal(rowRectangle.GetField(field.Id).GetScalar<double>(),rectangle.GetField(field.Id).GetScalar<double>());
        Assert.Equal(row.GetField("long-axis").GetScalar<double>(),values.Read(BlobSchema.LongAxesPort).GetElement(0).GetScalar<double>());
        Assert.Equal(row.GetField("short-axis").GetScalar<double>(),values.Read(BlobSchema.ShortAxesPort).GetElement(0).GetScalar<double>());
        var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
        Assert.Equal(Assert.Single(output.Measurements).OrientedBounds,Assert.Single(output.ResultProjection.MinBoudingRect));
        Assert.Equal(Assert.Single(output.Measurements).Bounds,Assert.Single(output.ResultProjection.BlobRect));
        var bounds=Assert.Single(output.Measurements).Bounds;
        var aligned=values.Read(BlobSchema.AxisAlignedRectanglesPort).GetElement(0);
        var rowBounds=row.GetField("bounds");
        foreach(var axis in FixtureDeclarations.Point.Fields)
            Assert.Equal(rowBounds.GetField("origin").GetField(axis.Id).GetScalar<double>(),aligned.GetField("origin").GetField(axis.Id).GetScalar<double>());
        foreach(var sideLength in new[]{"width","height"})
            Assert.Equal(rowBounds.GetField(sideLength).GetScalar<double>(),aligned.GetField(sideLength).GetScalar<double>());
        Assert.Equal((double)(float)bounds.X,Assert.Single(values.Get<double[]>(BlobSchema.RectangleTopLeftXPort)));
        Assert.Equal((double)(float)bounds.Y,Assert.Single(values.Get<double[]>(BlobSchema.RectangleTopLeftYPort)));
        Assert.Equal((double)(float)(bounds.X+bounds.Width),Assert.Single(values.Get<double[]>(BlobSchema.RectangleBottomRightXPort)));
        Assert.Equal((double)(float)(bounds.Y+bounds.Height),Assert.Single(values.Get<double[]>(BlobSchema.RectangleBottomRightYPort)));
    }

    [Theory]
    [InlineData(BlobSortFeature.SortFeatureArea)] [InlineData(BlobSortFeature.SortFeaturePerimeter)]
    [InlineData(BlobSortFeature.SortFeatureCircularity)] [InlineData(BlobSortFeature.SortFeatureRect)]
    [InlineData(BlobSortFeature.SortFeatureCentroidX)] [InlineData(BlobSortFeature.SortFeatureCentroidY)]
    [InlineData(BlobSortFeature.SortFeatureRectX)] [InlineData(BlobSortFeature.SortFeatureRectY)]
    [InlineData(BlobSortFeature.SortFeatureBoxAngle)] [InlineData(BlobSortFeature.SortFeatureBoxWidth)]
    [InlineData(BlobSortFeature.SortFeatureBoxHeight)] [InlineData(BlobSortFeature.SortFeatureAxisAngle)]
    [InlineData(BlobSortFeature.SortFeatureAxisRatio)]
    public void Thirteen_features_capacity_rows_region_and_blob_image_share_one_candidate_set(BlobSortFeature feature)
    {
        const int width=16,height=11; var pixels=new byte[width*height];
        var areas=new[]{3,5,7,9,11};
        for(var i=0;i<areas.Length;i++) for(var x=1;x<=areas[i];x++) pixels[(i*2+1)*width+x]=200;
        foreach(var mode in Enum.GetValues<BlobSortMode>())
        {
            using var host=new Host(Base(BlobSegmentationMode.None) with { SortFeature=feature,SortMode=mode,BlobCountLimit=2,FindParameters=new BlobFindParameters { BlobContourType=BlobContourType.SequentialContour } },pixels,width,height);
            var execution=host.Execute(); using var batch=execution.Batch; using var result=execution.Result;
            Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
            var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
            using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
            var rows=reader.Values!.Read(BlobSchema.BlobsPort);
            var image=reader.Values.Get<RecordValue>(BlobSchema.Field(5)).Get<IReadOnlyImage>("data").CopyPixels();
            Assert.Equal(2,output.Count); Assert.Equal(output.Count,rows.Count);
            Assert.Equal(NumericStorage.Int32,reader.Values.Declarations[BlobSchema.Field(11)].Storage);
            Assert.Equal((long)rows.Count,reader.Values.Get<long>(BlobSchema.Field(11)));
            Assert.Equal(NumericStorage.Float32,reader.Values.Declarations[BlobSchema.Field(12)].Storage);
            Assert.Equal((double)output.ResultProjection.TotalArea,reader.Values.Get<double>(BlobSchema.Field(12)));
            var numericResults = new (int Field, Func<BlobMeasurement,double> Measure)[]
            {
                (19,item=>item.Area), (20,item=>item.Perimeter),
                (21,item=>item.CentroidX), (22,item=>item.CentroidY),
                (23,item=>item.Circularity), (24,item=>item.Rectangularity),
                (25,item=>item.Bounds.Width), (26,item=>item.Bounds.Height)
            };
            foreach(var (field,measure) in numericResults)
            {
                var id=BlobSchema.Field(field);
                Assert.Equal(NumericStorage.Float32,reader.Values.Declarations[id].Storage);
                Assert.Equal(output.Measurements.Select(item=>(double)(float)measure(item)),reader.Values.Get<double[]>(id));
            }
            Assert.Equal(output.Measurements.Sum(item=>item.Area), image.Count(pixel=>pixel!=0));
            using var region=output.Region.AcquireReader();
            Assert.Equal(output.Measurements.Sum(item=>item.Area),region.ReadRuns(0,region.RunCount).Span.ToArray().Sum(run=>run.LastColumnInclusive-run.FirstColumn+1));
            for(var i=0;i<output.Count;i++)
            {
                var row=rows.GetElement(i); var item=output.Measurements[i];
                Assert.Equal(item.ElementId,row.GetField("element-id").GetScalar<long>());
                Assert.Equal(item.Area,row.GetField("area").GetScalar<double>());
                using var selected=item.Region.AcquireReader();
                Assert.All(selected.ReadRuns(0,selected.RunCount).Span.ToArray(),run=>Assert.True(image.Skip(run.Row*width+run.FirstColumn).Take(run.LastColumnInclusive-run.FirstColumn+1).All(pixel=>pixel!=0)));
            }
        }
    }

    [Theory]
    [InlineData(BlobSortFeature.SortFeatureBoxAngle)]
    [InlineData(BlobSortFeature.SortFeatureAxisAngle)]
    public void Signed_angles_select_capacity_before_ordering_without_rejecting_negative_measurements(BlobSortFeature feature)
    {
        const int width=48,height=40;
        var pixels=new byte[width*height];
        for(var offset=0;offset<10;offset++)
        for(var thickness=0;thickness<3;thickness++)
        {
            pixels[(3+offset)*width+3+offset+thickness]=200;
            pixels[(23+offset)*width+23-offset+thickness]=200;
            pixels[(5+thickness)*width+30+offset]=200;
        }
        double[] Run(BlobSortMode mode,int count)
        {
            using var host=new Host(Base(BlobSegmentationMode.None) with {SortFeature=feature,SortMode=mode,BlobCountLimit=count},pixels,width,height);
            var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
            Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
            var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
            return output.Measurements.Select(item=>feature==BlobSortFeature.SortFeatureBoxAngle
                ?item.OrientedBounds.AngleDegrees:item.BlobAngleDegrees!.Value).ToArray();
        }
        var all=Run(BlobSortMode.SortModeNotSort,10);
        Assert.Equal(3,all.Length);
        Assert.Contains(all,value=>value<0);Assert.Contains(all,value=>value>0);
        Assert.Equal(all.OrderBy(value=>value).Take(2),Run(BlobSortMode.SortModeAscend,2));
        Assert.Equal(all.OrderByDescending(value=>value).Take(2),Run(BlobSortMode.SortModeDecend,2));
    }

    [Fact]
    public void Reverse_area_filter_is_or_not_sorted_endpoints()
    {
        const int width=9,height=11;var pixels=new byte[width*height];var areas=new[]{1,2,3,5,6};
        for(var i=0;i<areas.Length;i++)for(var x=1;x<=areas[i];x++)pixels[(2*i+1)*width+x]=200;
        using var host=new Host(Base(BlobSegmentationMode.None) with { IsAreaFilterEnabled=true,FilterAreaMin=5,FilterAreaMax=2,BlobCountLimit=10 },pixels,width,height);
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        var output=Assert.IsType<BlobOutput>(execution.Run.NodeResults[host.Node.Id].Result!.Output);
        Assert.Equal(new double[]{1,2,5,6},output.Measurements.Select(item=>item.Area).OrderBy(value=>value));
    }
    [Fact]
    public void Fixed_soft_two_row_formal_session_preserves_soft_anchor()
    {
        var settings=Base(BlobSegmentationMode.FixedSoftThreshold) with
        { FixedSoftLowerThreshold=100, FixedSoftUpperThreshold=150, ThresholdSoftness=254, Type=BlobThresholdMode.BinaryInv };
        var pixels=Enumerable.Range(0,512).Select(i=>(byte)(i%256)).ToArray();
        using var host=new Host(settings,pixels,256,2);
        var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
        Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
        using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
        var actual=reader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data").CopyPixels();
        Assert.Equal(new byte[]{5,5},new[]{actual[149],actual[256+149]});
    }
    [Fact]
    public void Boundary_demand_preserves_fixed_soft_image_in_direct_and_invocation_paths()
    {
        var settings=Base(BlobSegmentationMode.FixedSoftThreshold) with
        { FixedSoftLowerThreshold=100, FixedSoftUpperThreshold=150, ThresholdSoftness=254, Type=BlobThresholdMode.BinaryInv };
        var pixels=Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        using var inputOwner=new FrozenOutputResources();
        var input=inputOwner.CreateGray8(256,1,pixels);
        var directSession=new BlobSession();
        using var noBoundaryResources=new FrozenOutputResources();
        using var boundaryResources=new FrozenOutputResources();
        using var noBoundary=directSession.Execute(settings,input,noBoundaryResources,requested:BlobOutputRequest.None);
        var beforeBoundary=noBoundary.BinaryImage!.CopyPixels();
        using var boundary=directSession.Execute(settings,input,boundaryResources,requested:BlobOutputRequest.Boundary);
        Assert.Null(boundary.ToValues()[BlobSchema.ContourPointsPort]);
        var baseline=noBoundary.BinaryImage!.CopyPixels();
        var boundaryPixels=boundary.BinaryImage!.CopyPixels();
        var tool=new BlobTool();
        var inputs=new InputValues(tool.Describe().Inputs,new Dictionary<string,object?>{{BlobTool.ImageInput,input}});
        using var invokeResources=new FrozenOutputResources();
        var invokeSession=tool.CreateSession();
        try
        {
            var invocation=new ToolInvocation<BlobSettings>(settings,inputs,new DirectContext(invokeResources),
                new[]{new OutputValueRequest(BlobSchema.BlobsPort,new[]{"boundary"})});
            var outcome=invokeSession.ExecuteAsync(invocation).AsTask().GetAwaiter().GetResult();
            using var output=Assert.IsType<BlobOutput>(outcome.Output);
            Assert.True(outcome.Success,string.Join(";",outcome.Diagnostics.Select(item=>item.Message)));
            var invoked=output.BinaryImage!.CopyPixels();
            Assert.True(beforeBoundary[149]==5 && beforeBoundary.SequenceEqual(baseline) && beforeBoundary.SequenceEqual(boundaryPixels) && beforeBoundary.SequenceEqual(invoked),
                $"input149={pixels[149]}, before149={beforeBoundary[149]}, retained149={baseline[149]}, boundary149={boundaryPixels[149]}, invoke149={invoked[149]}, same={ReferenceEquals(noBoundary.BinaryImage,boundary.BinaryImage)}");
        }
        finally { invokeSession.DisposeAsync().AsTask().GetAwaiter().GetResult(); directSession.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    [Fact]
    public void Packed_rgb_admission_preserves_channels_and_effective_domain()
    {
        using var owner = new FrozenOutputResources();
        var frame = new ImageFrameAddress(Guid.NewGuid(), "packed-rgb");
        var domain = owner.CreateRegion(RoiDeclarations.ImageFrame, frame, new[] { new RegionRun(0, 1, 2) });
        var input = owner.CreateImage(new ImageLayout(3, 1, 12, PixelLayout.Rgb24), frame, domain,
            new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 99, 99, 99 });
        using var native = SunEyeVision.HalAdapter.HalconResourceBridge.CopyImage(input);
        var captured = SunEyeVision.HalAdapter.HalImageSnapshotSource.Capture(native, frame, owner, rgb24: true);
        Assert.Equal(PixelLayout.Rgb24, captured.Layout.Format);
        Assert.Equal(new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255 }, captured.CopyPixels());
        using var reader = captured.EffectiveDomain.AcquireReader();
        Assert.Equal(new[] { new RegionRun(0, 1, 2) }, reader.ReadRuns(0, 1).ToArray());
    }

    [Fact]
    public void Full_neutral_input_domain_survives_real_halcon_admission()
    {
        var pixels=Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        using var owner=new FrozenOutputResources();var input=owner.CreateGray8(256,1,pixels);
        using(var lease=input.AcquirePixels())
        using(var domain=lease.AcquireDomainReader())
        {
            Assert.Equal(1,domain.RunCount);var run=domain.ReadRuns(0,1).Span[0];
            Assert.Equal((0,0,255),(run.Row,run.FirstColumn,run.LastColumnInclusive));
        }
        using var native=SunEyeVision.HalAdapter.HalconResourceBridge.CopyImage(input);
        using var nativeDomain=native.GetDomain();
        nativeDomain.GetRegionRuns(out var rows,out var starts,out var ends);
        using(rows)using(starts)using(ends)
        {
            Assert.Equal(1,rows.Length);
            Assert.Equal((0,0,255),(rows[0].I,starts[0].I,ends[0].I));
        }
    }
    [Fact]
    public void Full_input_domain_is_independent_of_prior_native_image_format_on_new_threads()
    {
        static string Probe(int initialWidth)
        {
            string? result=null;Exception? failure=null;
            var thread=new Thread(()=>
            {
                try
                {
                    using var prime=new HalconDotNet.HImage();prime.GenImageConst("byte",initialWidth,1);
                    static string SystemValue(string key)
                    {
                        try { HalconDotNet.HOperatorSet.GetSystem(key,out var value);using(value)return value.ToString(); }
                        catch(Exception error){return error.GetType().Name+":"+error.Message;}
                    }
                    var before=$"width={SystemValue("width")};height={SystemValue("height")};clip={SystemValue("clip_region")};tsp_clip={SystemValue("tsp_clip_region")}";
                    using var owner=new FrozenOutputResources();var input=owner.CreateGray8(256,1,Enumerable.Range(0,256).Select(i=>(byte)i).ToArray());
                    using var neutral=input.AcquirePixels();using var neutralDomain=neutral.AcquireDomainReader();
                    var neutralRuns=neutralDomain.ReadRuns(0,neutralDomain.RunCount).ToArray();
                    using var native=SunEyeVision.HalAdapter.HalconResourceBridge.CopyImage(input);
                    using var domain=native.GetDomain();domain.GetRegionRuns(out var rows,out var starts,out var ends);
                    using(rows)using(starts)using(ends)
                    {
                        var runs=Enumerable.Range(0,rows.Length).Select(i=>$"{rows[i].I}:{starts[i].I}-{ends[i].I}").ToArray();
                        result=$"prime={initialWidth};{before};neutral={string.Join(',',neutralRuns.Select(r=>$"{r.Row}:{r.FirstColumn}-{r.LastColumnInclusive}"))};native={string.Join(',',runs)};afterWidth={SystemValue("width")};afterHeight={SystemValue("height")}";
                    }
                }
                catch(Exception error){failure=error;}
            });
            thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(20)),"原生域检查超时。");
            if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result!;
        }
        var small=Probe(128);var full=Probe(256);
        Assert.True(small.Contains("neutral=0:0-255")&&full.Contains("neutral=0:0-255")&&
            small.Contains("native=0:0-255")&&full.Contains("native=0:0-255"),small+" | "+full);
    }
    [Fact]
    public void Unavailable_required_blob_image_clears_publication_and_reader_outlives_owner()
    {
        var pixels=new byte[]{0,200,200,0,200};
        using var host=new Host(Base(BlobSegmentationMode.None),pixels,5,1);
        var graph=host.Session.PrepareGraphEdit(host.Flow.Id);
        var consumer=host.Session.CreateNode(graph,BlobTool.Id,new CanvasPoint(220,0));
        graph.AddConnection(new ConnectionDefinition("required-image",host.Node.Id,consumer.Id,BlobSchema.Field(5),BlobTool.ImageInput));
        host.Session.CommitGraphEdit(graph,"连接 BlobImage 消费者");
        var port=new BlobTool().Describe().Outputs.Single(item=>item.Id==BlobSchema.Field(5));
        var edit=host.Session.Editing.Host.BeginEdit(host.Flow.Id,consumer.Id);
        edit.ApplyFields(new Dictionary<string,ParameterSetting>
        { [BlobTool.ImageInput]=new ReferenceSetting(new OutputReference(host.Node.Id,port.Id,port.Type,memberFieldPath: new[] { "data" },scopeId:host.Flow.Id)) });
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{edit}));
        var first=host.Execute();
        DeclaredOutputReader? reader=null;IReadOnlyImage prior;
        try
        {
            Assert.True(first.Run.Success,string.Join(";",first.Run.Errors));
            reader=host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id);
            Assert.NotNull(reader);
            prior=reader.Values!.Get<RecordValue>(BlobSchema.Field(5)).Get<IReadOnlyImage>("data");
            Assert.Equal(new byte[]{0,255,255,0,255},prior.CopyPixels());
        }
        finally { first.Result.Dispose();first.Batch.Dispose(); }
        var disable=host.Session.Editing.Host.BeginEdit(host.Flow.Id,host.Node.Id);
        var field=BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsCircularityFilterEnabled));
        disable.ApplyFields(new Dictionary<string,ParameterSetting>{{field,new ConstantSetting(true)}});
        host.Session.CommitEdit(DocumentEditProposal.Prepare(host.Session.Editing.Host,nodes:new[]{disable}));
        Assert.Null(host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id));
        var failed=host.Execute();
        try
        {
            Assert.True(failed.Run.Success,string.Join(";",failed.Run.Errors));
            using var filteredReader=host.Session.AcquirePublishedResult(host.Flow.Id,host.Node.Id);
            Assert.NotNull(filteredReader);Assert.True(filteredReader.Snapshot.IsSuccess);Assert.NotNull(filteredReader.Values);
            Assert.NotEqual(reader.Snapshot.Identity,filteredReader.Snapshot.Identity);
            var filteredImage=filteredReader.Values!.Get<RecordValue>(BlobSchema.Field(5)).Get<IReadOnlyImage>("data");
            Assert.Equal(new byte[]{0,255,255,0,0},filteredImage.CopyPixels());
            Assert.Equal(new byte[]{0,255,255,0,255},prior.CopyPixels());
        }
        finally { failed.Result.Dispose();failed.Batch.Dispose(); }
        host.Dispose();
        Assert.Equal(new byte[]{0,255,255,0,255},prior.CopyPixels());
        reader.Dispose();Assert.Throws<ObjectDisposedException>(()=>prior.CopyPixels());
    }
    [Fact]
    public void Seven_soft_fields_survive_real_copy_and_v4_save_reopen()
    {
        var settings=Base(BlobSegmentationMode.FixedSoftThreshold) with
        { FixedSoftLowerThreshold=22, FixedSoftUpperThreshold=199, ThresholdSoftness=41,
          RelativeLeftRatio=4, RelativeRightRatio=7, RelativeLowerRatio=23, RelativeUpperRatio=87 };
        using var host=new Host(settings,new byte[]{0,22,80,199,255},5,1);
        var names=new[]{nameof(BlobSettings.FixedSoftLowerThreshold),nameof(BlobSettings.FixedSoftUpperThreshold),
            nameof(BlobSettings.ThresholdSoftness),nameof(BlobSettings.RelativeLeftRatio),nameof(BlobSettings.RelativeRightRatio),
            nameof(BlobSettings.RelativeLowerRatio),nameof(BlobSettings.RelativeUpperRatio)};
        var values=new long[]{22,199,41,4,7,23,87};
        void Check(NodeDefinition node)
        {
            for(var index=0;index<names.Length;index++)
            {
                var id=BlobParameterDefinition.Instance.SettingsFieldId(names[index]);
                Assert.Equal(values[index],Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read(id)).Value);
            }
        }
        Check(host.Node);
        var fragment=host.Session.CaptureNodeClipboard(host.Flow.Id,new[]{host.Node});
        var pasted=host.Session.PasteNodeClipboard(host.Flow.Id,fragment,new CanvasPoint(200,0));
        var copied=Assert.Single(pasted.Nodes);Assert.NotEqual(host.Node.Id,copied.Id);Check(copied);
        var path=Path.Combine(SunEyeVision.TestShared.ProductRuntimeTestHost.ProductRoot,"..","..","_validation","blob-chain","cases",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);var file=Path.Combine(path,"blob.solution");
        var types=new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>());
        host.Session.SaveAs(file,Path.Combine(path,"recovery"),path,types,new System.Text.Json.JsonSerializerOptions(),_=>null);
        var reopened=host.Manager.Open(file,_=>types,_=>null,new System.Text.Json.JsonSerializerOptions());
        var nodes=reopened.GetNodes(host.Flow.Id);Assert.Equal(2,nodes.Count);
        Check(nodes.Single(node=>node.Id==host.Node.Id));Check(nodes.Single(node=>node.Id==copied.Id));
        using var batch=reopened.CaptureRunBatch(new[]{host.Flow.Id},workflowImages:new Dictionary<string,WorkflowImageInput>
        { [host.Flow.Id]=new WorkflowImageInput(host.Input,host.Input.AcquirePixels()) });
        using var result=batch.RunAsync(_=>{}).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        Assert.True(result.Workflows[host.Flow.Id].Success,string.Join(";",result.Workflows[host.Flow.Id].Errors));
    }
    [Fact]
    public void Region_copy_and_affine_are_thread_isolated_and_restore_both_clip_states()
    {
        var results=new System.Collections.Concurrent.ConcurrentQueue<string>();
        var failures=new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        using var barrier=new Barrier(4);
        var threads=Enumerable.Range(0,4).Select(index=>new Thread(()=>
        {
            try
            {
                var desired=index%2==0?"true":"false";
                HalconDotNet.HOperatorSet.SetSystem("tsp_clip_region",desired);
                using var prime=new HalconDotNet.HImage();prime.GenImageConst("byte",128,1);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("并发起点未汇合。");
                static string Clip(){HalconDotNet.HOperatorSet.GetSystem("tsp_clip_region",out var value);using(value)return value.S;}
                static string Runs(HalconDotNet.HRegion region)
                {
                    region.GetRegionRuns(out var rows,out var starts,out var ends);
                    using(rows)using(starts)using(ends)
                        return string.Join(',',Enumerable.Range(0,rows.Length).Select(i=>$"{rows[i].I}:{starts[i].I}-{ends[i].I}"));
                }
                using var owner=new FrozenOutputResources();
                var input=owner.CreateGray8(256,1,new byte[256]);
                var initial=Clip();
                using var region=SunEyeVision.HalAdapter.HalconResourceBridge.CopyRegion(input.EffectiveDomain,input.Frame,256,1);
                var afterCopy=Clip();
                var affine=new RecordValue(SpatialDeclarations.Affine2D,new Dictionary<string,object?>
                { ["m11"]=1d,["m12"]=0d,["m21"]=0d,["m22"]=1d,["offset-x"]=300d,["offset-y"]=0d,["from"]="from",["to"]="to" });
                using var transformed=SunEyeVision.HalAdapter.HalconResourceBridge.TransformRegion(region,affine);
                var afterTransform=Clip();
                results.Enqueue($"{desired}:{initial}:{afterCopy}:{afterTransform}:{Runs(region)}:{Runs(transformed)}");
            }
            catch(Exception error){failures.Enqueue(error);}
        })).ToArray();
        foreach(var thread in threads)thread.Start();
        foreach(var thread in threads)Assert.True(thread.Join(TimeSpan.FromSeconds(30)),"并发线程未排空。");
        Assert.Empty(failures);Assert.Equal(4,results.Count);
        foreach(var result in results)
        {
            var fields=result.Split(':');
            Assert.Equal(fields[0],fields[1]);Assert.Equal(fields[0],fields[2]);Assert.Equal(fields[0],fields[3]);
            Assert.Contains("0:0-255:0:300-555",result);
        }
    }
    [Fact]
    public void Cold_fixed_soft_512_input_survives_prior_small_native_image_on_fresh_thread()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                using var prime=new HalconDotNet.HImage();prime.GenImageConst("byte",128,1);
                HalconDotNet.HOperatorSet.GetSystem("width",out var width);
                using(width)Assert.True(width.I>=128);
                var input=Enumerable.Range(0,512).Select(i=>(byte)(i%256)).ToArray();
                var settings=Base(BlobSegmentationMode.FixedSoftThreshold) with
                { FixedSoftLowerThreshold=100,FixedSoftUpperThreshold=150,ThresholdSoftness=254,Type=BlobThresholdMode.BinaryInv };
                using var host=new Host(settings,input,512,1);
                var execution=host.Execute();using var batch=execution.Batch;using var result=execution.Result;
                Assert.True(execution.Run.Success,string.Join(";",execution.Run.Errors));
                using var reader=execution.Run.AcquireDeclaredOutput(Assert.Single(execution.Run.DeclaredOutputs).Identity);
                var actual=reader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data").CopyPixels();
                Assert.Equal(512,actual.Length);Assert.Equal((byte)5,actual[149]);Assert.Equal((byte)5,actual[405]);
            }
            catch(Exception error){failure=error;}
        });
        thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(45)),"冷启动执行线程未排空。");
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
