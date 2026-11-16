using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Plugin.Contracts;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

public sealed class BlobAnalysisParameterContractTests
{
    [Fact]
    public void Module_result_projection_keeps_vm_summary_without_duplicate_blob_record_rows()
    {
        var display = BlobSchema.OutputDisplay;
        var blob = display.Single(item => item.FieldId == BlobSchema.BlobsPort);
        var runtime = display.Single(item => item.FieldId == BlobSchema.ModuleRunTimePort);
        var statuses = display.Single(item => item.FieldId == BlobSchema.BlobStatusesPort);
        Assert.False(blob.DisplayInResultPanel);
        Assert.True(runtime.DisplayInResultPanel);
        Assert.True(display.Single(item => item.FieldId == BlobSchema.ModuleRunTimePort).DisplayInResultPanel);
        Assert.True(display.Single(item => item.FieldId == BlobSchema.ModuleRunTimePort).IsModuleExecutionSummary);
        Assert.True(display.Single(item => item.FieldId == BlobSchema.ModuleStatusPort).IsModuleExecutionSummary);
        Assert.Equal("单体状态", statuses.DisplayName);
        Assert.True(display.Single(item => item.FieldId == BlobSchema.ModuleStatusPort).DisplayInResultPanel);
    }

    [Fact]
    public void Semantic_projection_keys_stay_declared_for_blob_status_enum_and_polarity_outputs()
    {
        // ResultSemanticText 按显示名/原值投影；改名或改原值必须同步该投影并登记证据。
        var display = BlobSchema.OutputDisplay;
        Assert.Equal("模块状态", display.Single(item => item.FieldId == BlobSchema.ModuleStatusPort).DisplayName);
        Assert.Equal("单体状态", display.Single(item => item.FieldId == BlobSchema.BlobStatusesPort).DisplayName);
        Assert.Equal("阈值方式", display.Single(item => item.FieldId == BlobSchema.Field(102)).DisplayName);
        Assert.Equal("极性", display.Single(item => item.FieldId == BlobSchema.Field(10)).DisplayName);
        Assert.Equal("Binary", BlobThresholdMode.Binary.ToString());
        Assert.Equal("BinaryInv", BlobThresholdMode.BinaryInv.ToString());
    }

    [Fact]
    public void Original_output_mask_switch_is_optional_with_a_false_default_and_no_unset_clear_branch()
    {
        var port = BlobImageRoi.Ports.Single(item => item.Id == BlobImageRoi.OutputEnabledId);
        Assert.False(port.Required);
        Assert.Equal(FieldEditModes.Constant, port.EditModes);
    }

    [Theory]
    [InlineData(BlobRoiInheritance.RectangleParameters, true, false)]
    [InlineData(BlobRoiInheritance.Annulus, false, true)]
    [InlineData(BlobRoiInheritance.AnnulusParameters, false, true)]
    [InlineData(BlobRoiInheritance.Local, false, false)]
    public void Roi_mode_trigger_clears_the_declared_shape_families(string nextMode, bool keepRectangle, bool keepAnnulus)
    {
        var tool = new BlobTool();
        var ids = BlobRoiInheritance.Ports.Where(port => port.Id != BlobRoiInheritance.ModeId).ToArray();
        var values = ids.ToDictionary(port => port.Id, port => (ParameterSetting)new ReferenceSetting(
            new OutputReference("producer", "output", port.Type, scopeId: "flow")));
        values[BlobRoiInheritance.ModeId] = new ConstantSetting(BlobRoiInheritance.Rectangle);
        var current = new FieldSettings(tool.Describe().Inputs.Concat(tool.SettingsSchema.Inputs), values);
        var changed = current.Apply(tool.ExpandChanges(current,
            new Dictionary<string, ParameterSetting> { [BlobRoiInheritance.ModeId] = new ConstantSetting(nextMode) }));

        Assert.Equal(nextMode, BlobRoiInheritance.ReadMode(changed));
        foreach (var port in ids)
        {
            var keep = port.Id.StartsWith(BlobRoiInheritance.RectangleSource, StringComparison.Ordinal)
                ? keepRectangle : keepAnnulus;
            if (keep) Assert.IsType<ReferenceSetting>(changed.Read(port.Id));
            else Assert.IsType<UnsetSetting>(changed.Read(port.Id));
            Assert.IsType<ReferenceSetting>(current.Read(port.Id));
        }
    }

    [Theory]
    [InlineData(BlobRoiInheritance.Rectangle)]
    [InlineData(BlobRoiInheritance.RectangleParameters)]
    [InlineData(BlobRoiInheritance.Annulus)]
    [InlineData(BlobRoiInheritance.AnnulusParameters)]
    public void Image_roi_clears_detection_sources_without_resetting_the_creation_choice(string mode)
    {
        var tool = new BlobTool();
        var sources = BlobRoiInheritance.Ports.Where(port => port.Id != BlobRoiInheritance.ModeId).ToArray();
        var values = sources.ToDictionary(port => port.Id, port => (ParameterSetting)new ReferenceSetting(
            new OutputReference("producer", "output", port.Type, scopeId: "flow")));
        values[BlobRoiInheritance.ModeId] = new ConstantSetting(mode);
        values[BlobMaskInheritance.ModeId] = new ConstantSetting(true);
        values[BlobMaskInheritance.SourceId] = new ReferenceSetting(
            new OutputReference("producer", "mask", BlobMaskInheritance.PolygonStrings, scopeId: "flow"));
        var current = new FieldSettings(tool.Describe().Inputs.Concat(tool.SettingsSchema.Inputs), values);
        FieldSettings SelectArea(FieldSettings fields, long area) => fields.Apply(tool.ExpandChanges(fields,
            new Dictionary<string, ParameterSetting> { [BlobImageRoi.ModeId] = new ConstantSetting(area) }));

        var image = SelectArea(current, 1);
        Assert.Equal(mode, BlobRoiInheritance.ReadMode(image));
        Assert.All(sources, port => Assert.IsType<UnsetSetting>(image.Read(port.Id)));
        Assert.IsType<ReferenceSetting>(image.Read(BlobMaskInheritance.SourceId));
        Assert.DoesNotContain(BlobRoiInheritance.ModeId, tool.SelectExecutionInputs(image));
        Assert.DoesNotContain(BlobMaskInheritance.SourceId, tool.SelectExecutionInputs(image));

        var graphic = SelectArea(image, 0);
        Assert.Equal(mode, BlobRoiInheritance.ReadMode(graphic));
        Assert.All(sources, port => Assert.IsType<UnsetSetting>(graphic.Read(port.Id)));
        Assert.Contains(BlobMaskInheritance.SourceId, tool.SelectExecutionInputs(graphic));
        Assert.All(tool.ValidateExecutionInputs(graphic), error => Assert.Equal("BLOB_ROI_SOURCE_REQUIRED", error.Code));
        Assert.NotEmpty(tool.ValidateExecutionInputs(graphic));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inactive_position_correction_clears_the_source_as_part_of_the_trigger(bool changeRoiType)
    {
        var tool = new BlobTool();
        var enabled = BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsPositionCorrectionEnabled));
        var position = new Dictionary<string, object?>
        {
            ["point"] = new Dictionary<string, object?> { ["x"] = 0d, ["y"] = 0d },
            ["angle"] = 0d, ["scale-x"] = 1d, ["scale-y"] = 1d
        };
        var correction = new ConstantSetting(new RecordValue(FixtureDeclarations.Info, new Dictionary<string, object?>
        {
            ["reference"] = position, ["run"] = position
        }));
        var empty = new FieldSettings(tool.Describe().Inputs.Concat(tool.SettingsSchema.Inputs),new Dictionary<string,ParameterSetting>());
        var current = empty.Apply(tool.ExpandChanges(empty,
            new Dictionary<string, ParameterSetting> { [enabled] = new ConstantSetting(true), [BlobTool.PoseInput] = correction }));
        Assert.IsType<UnsetSetting>(current.Read(BlobTool.PoseInput));
        Assert.All(BlobFixtureInputs.LeafIds, id => Assert.IsType<ConstantSetting>(current.Read(id)));
        var changes = changeRoiType
            ? new Dictionary<string, ParameterSetting> { [BlobImageRoi.ModeId] = new ConstantSetting(1L) }
            : new Dictionary<string, ParameterSetting> { [enabled] = new ConstantSetting(false) };
        var edited = current.Apply(tool.ExpandChanges(current, changes));
        Assert.All(BlobFixtureInputs.LeafIds,id=>Assert.IsType<UnsetSetting>(edited.Read(id)));
        Assert.IsType<ConstantSetting>(tool.ProjectInput(current,BlobTool.PoseInput));
        Assert.Throws<ArgumentException>(() => tool.ExpandChanges(edited,
            new Dictionary<string, ParameterSetting> { [BlobTool.PoseInput] = correction }));
    }

    [Fact]
    public void Fixture_and_point_projections_expand_one_subscription_and_preserve_index()
    {
        var tool=new BlobTool();
        var enabled=BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsPositionCorrectionEnabled));
        var fields=new FieldSettings(tool.Describe().Inputs.Concat(tool.SettingsSchema.Inputs),
            new Dictionary<string,ParameterSetting> { [enabled]=new ConstantSetting(true) });
        var index=new BindingSelection.AtConstant(2);
        var fixture=new OutputReference("producer","pose",TypeRef.SequenceOf(FixtureDeclarations.Info),selection:index,scopeId:"flow");
        var bound=fields.Apply(tool.ExpandChanges(fields,new Dictionary<string,ParameterSetting> { [BlobTool.PoseInput]=new ReferenceSetting(fixture) }));
        Assert.All(BlobFixtureInputs.LeafIds,id=>Assert.Same(index,Assert.IsType<ReferenceSetting>(bound.Read(id)).Reference.Selection));
        Assert.Equal(ParameterSettingCanonical.Serialize(fixture),
            ParameterSettingCanonical.Serialize(Assert.IsType<ReferenceSetting>(tool.ProjectInput(bound,BlobTool.PoseInput)).Reference));
        var point=new OutputReference("second","point",FixtureDeclarations.Point,scopeId:"flow");
        var mixed=bound.Apply(tool.ExpandChanges(bound,new Dictionary<string,ParameterSetting> { [BlobFixtureInputs.ReferencePoint]=new ReferenceSetting(point) }));
        Assert.IsType<UnsetSetting>(tool.ProjectInput(mixed,BlobTool.PoseInput));
        Assert.Equal("second",Assert.IsType<ReferenceSetting>(tool.ProjectInput(mixed,BlobFixtureInputs.ReferencePoint)).Reference.NodeId);
        Assert.Equal("producer",Assert.IsType<ReferenceSetting>(tool.ProjectInput(mixed,BlobFixtureInputs.RunPoint)).Reference.NodeId);
        Assert.All(BlobFixtureInputs.LeafIds,id=>Assert.Contains(id,tool.SelectExecutionInputs(mixed)));
        Assert.DoesNotContain(BlobTool.PoseInput,tool.SelectExecutionInputs(mixed));
    }

    [Fact]
    public void Original_parent_range_bounds_remain_distinct_from_endpoint_bounds()
    {
        var definition=BlobParameterDefinition.Instance;
        var fields=definition.SettingsSchema.Presentation!.Fields;
        var pairs=fields.Where(field=>field.Layout?.PairWith is not null).ToArray();
        Assert.Equal(27,pairs.Length);
        Assert.All(pairs,field=>{Assert.NotNull(field.Layout!.PairMinimum);Assert.NotNull(field.Layout.PairMaximum);Assert.False(string.IsNullOrWhiteSpace(field.Layout.PairDisplayName));});
        FieldLayout Layout(string name)=>fields.Single(field=>field.FieldId==definition.SettingsFieldId(name)).Layout!;
        Assert.Equal(0.01,Layout("MinAxisRatio").PairMinimum);
        Assert.Equal(0d,Layout("MaxAxisRatio").Minimum);
        Assert.Equal(1d,Layout("BlobNumLimitLow").PairMinimum);
        Assert.Equal(0d,Layout("BlobNumLimitHigh").Minimum);
        Assert.Equal(0d,Layout("FixedSoftLowerThreshold").PairMinimum);
        Assert.Equal(1d,Layout("FixedSoftLowerThreshold").Minimum);
        Assert.Equal("面积范围",Layout("FilterAreaMin").PairDisplayName);
        Assert.Equal("最小面积",definition.Label("FilterAreaMin"));
        Assert.Equal("RECT宽范围",Layout("MinRectWidth").PairDisplayName);
        Assert.Equal("最小RECT宽",definition.Label("MinRectWidth"));
        Assert.Equal("RECT宽使能",definition.Label("SelectByRectWidth"));
        Assert.Equal("轴比范围",definition.Label("AxisRatioEnable"));
        Assert.Equal("Blob个数为0",definition.Label("OKWhenNumIsZero"));
        foreach(var name in new[] { "BlobAreaLimitLow", "BlobAreaLimitHigh", "BlobTotalAreaLimitLow", "BlobTotalAreaLimitHigh",
            "CentroidXLimitLow", "CentroidXLimitHigh", "CentroidYLimitLow", "CentroidYLimitHigh", "CenterXLimitLow", "CenterXLimitHigh",
            "CenterYLimitLow", "CenterYLimitHigh", "BoxHeightLimitLow", "BoxHeightLimitHigh", "BoxWidthLimitLow", "BoxWidthLimitHigh",
            "PerimeterLimitLow", "PerimeterLimitHigh", "LongAxisLimitLow", "LongAxisLimitHigh", "ShortAxisLimitLow", "ShortAxisLimitHigh" })
        {
            Assert.Equal(1d,Layout(name).Increment);
            Assert.Equal(2,Layout(name).DecimalPlaces);
        }
        Assert.Equal(0.1,Layout("AngleLimitLow").Increment);
        Assert.Equal(0.1,Layout("AngleLimitHigh").Increment);
        Assert.Equal(0.01,Layout("CircularityLimitLow").Increment);
    }

    [Fact]
    public void Original_filter_defaults_match_new_settings_and_formal_roundtrip()
    {
        var definition=BlobParameterDefinition.Instance;
        var fresh=new BlobSettings();
        foreach(var settings in new[]{fresh,definition.MaterializeSettings(definition.ReadSettings(fresh))})
        {
            var vm=settings.ToFindParameters();
            Assert.Equal(100,vm.MinRectWidth);Assert.Equal(200,vm.MaxRectWidth);
            Assert.Equal(100,vm.MinRectHeight);Assert.Equal(200,vm.MaxRectHeight);
            Assert.Equal(10,vm.MinPerimeter);Assert.Equal(10,vm.MinLongAxis);
        }
        Assert.Equal(100,definition.Default<int>("MinRectWidth"));
        Assert.Equal(200,definition.Default<int>("MaxRectWidth"));
        Assert.Equal(100,definition.Default<int>("MinRectHeight"));
        Assert.Equal(200,definition.Default<int>("MaxRectHeight"));
        Assert.Equal(10,definition.Default<int>("MinPerimeter"));
        Assert.Equal(10,definition.Default<int>("MinLongAxis"));
    }

    [Fact]
    public void Original_three_tab_layout_keeps_advanced_fields_out_of_default_sections()
    {
        var definition=BlobParameterDefinition.Instance;
        foreach(var name in new[]{"SelectByHistInfo","SelectByBoxAngle","SelectByRectWidth","SelectByRectHeight",
            "SelectByPerimeter","SelectByShortAxis","SelectByLongAxis","IsCircularityFilterEnabled",
            "IsRectangularityFilterEnabled","SelectByCentraBias","AxisRatioEnable","SortFeature","SortMode","Connectivity",
            "CentroidXLimitEnable","CentroidYLimitEnable","AngleLimitEnable","CenterXLimitEnable","CenterYLimitEnable",
            "BoxHeightLimitEnable","BoxWidthLimitEnable","PerimeterLimitEnable","LongAxisLimitEnable","ShortAxisLimitEnable",
            "CircularityLimitEnable","RectangularityLimitEnable"})
            Assert.True(definition.Advanced(name),name);
        foreach(var name in new[]{"HoleMinArea","BlobContourType","OutputBlobImage","OutputBinaryImage",
            "OKWhenNumIsZero","BlobNumLimitEnable","BlobAreaLimitEnable","BlobTotalAreaLimitEnable"})
            Assert.False(definition.Advanced(name),name);
        var fields=definition.SettingsSchema.Presentation!.Fields.Select(field=>field.FieldId).ToArray();
        void Before(string first,string second)=>Assert.True(Array.IndexOf(fields,definition.SettingsFieldId(first))
            <Array.IndexOf(fields,definition.SettingsFieldId(second)),first+" → "+second);
        Before("BlobCountLimit","HoleMinArea");Before("HoleMinArea","IsAreaFilterEnabled");
        Before("FilterAreaMax","BlobContourType");Before("BlobContourType","OutputBlobImage");
        Before("SelectByLongAxis","IsCircularityFilterEnabled");Before("IsRectangularityFilterEnabled","SelectByCentraBias");
    }

    [Fact]
    public void Active_contour_mode_is_the_only_editor_setting_and_survives_parameter_roundtrips()
    {
        // The installed AlgorithmTab XML comments out the old Boolean editor.
        // Only the active three-valued mode belongs to the product schema.
        var definition = BlobParameterDefinition.Instance;
        Assert.DoesNotContain(definition.SettingsSchema.Inputs, port => port.Id == "field:3799fbc2f80e58bf9abc7d2c0ad70067");
        foreach (var mode in Enum.GetValues<BlobContourType>())
        {
            var settings = definition.ApplySettings(new BlobSettings(), new Dictionary<string, object?>
            {
                [definition.SettingsFieldId(nameof(BlobFindParameters.BlobContourType))] = mode.ToString()
            });
            var restored = definition.MaterializeSettings(definition.ReadSettings(settings.Freeze()));
            var snapshot = restored.ToFindParameters();
            Assert.Equal(mode, snapshot.BlobContourType);
            var projected = snapshot.ApplyTo(new BlobSettings()).Freeze();
            Assert.Equal(mode, projected.ToFindParameters().BlobContourType);
        }
    }

    [Fact]
    public void Contract_declares_neutral_inputs_and_XML_settings()
    {
        var tool=new BlobTool();var settings=new BlobSettings();
        Assert.Empty(tool.Validate(settings));
        Assert.Equal(new[]{"input.image","input.pose","input.roi.local",
            "input.roi.inheritance-mode","input.roi.rectangle","input.roi.annulus",
            "input.roi.rectangle.center-x","input.roi.rectangle.center-y","input.roi.rectangle.width","input.roi.rectangle.height","input.roi.rectangle.angle-degrees",
            "input.roi.annulus.center-x","input.roi.annulus.center-y","input.roi.annulus.inner-radius","input.roi.annulus.outer-radius","input.roi.annulus.start-degrees","input.roi.annulus.sweep-degrees",BlobImageRoi.ModeId,BlobImageRoi.ImageId,BlobImageRoi.OutputEnabledId}
            .Concat(BlobFixtureInputs.Ports.Select(port=>port.Id)).Concat(BlobMaskInheritance.Ports.Select(port=>port.Id)),tool.Describe().Inputs.Select(port=>port.Id));
        Assert.Equal(104,tool.SettingsSchema.Inputs.Count);
        Assert.Contains(tool.SettingsSchema.Presentation!.Fields,field=>field.DisplayName==BlobParameterDefinition.Instance.Label(nameof(BlobSettings.FilterAreaMin)));
        Assert.Equal(new[]{"input.image","input.pose"},
            tool.Describe().Presentation!.Fields.Select(field=>field.FieldId));
        var references=typeof(BlobTool).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
        Assert.DoesNotContain("SunEyeVision.Plugin.Editing.Wpf",references);
        Assert.DoesNotContain("PresentationFramework",references);
        Assert.DoesNotContain("SunEyeVision.UI",references);
    }
    [Fact]
    public void Six_declared_modes_preserve_formal_identity_and_are_admitted()
    {
        var tool=new BlobTool();
        foreach(var mode in Enum.GetValues<BlobSegmentationMode>())
        {
            var fields=new FieldSettings(tool.SettingsSchema.Inputs,
                tool.ReadSettings(new BlobSettings { SegmentationMode=mode }).CopyValues()
                    .ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value)));
            Assert.DoesNotContain(tool.ValidateKnownSettings(fields),issue=>issue.Code=="BLOB_MODE_NOT_READY");
            Assert.Equal(mode,((BlobSettings)tool.MaterializeSettings(fields.Materialize(new Dictionary<string,object?>()))).SegmentationMode);
        }
    }
    [Fact]
    public void Double_threshold_accepts_reversed_endpoints_without_rewriting_settings()
    {
        var settings=new BlobSettings{Threshold=100,SegmentationMode=BlobSegmentationMode.DoubleThreshold,UpperThreshold=30};
        Assert.Empty(settings.Validate());
        Assert.True(BlobThresholdCapabilities.IsSupported(settings.SegmentationMode));
        Assert.Equal(100,settings.Threshold);
        Assert.Equal(30,settings.UpperThreshold);
    }
}
