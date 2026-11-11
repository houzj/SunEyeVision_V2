using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob runtime entry. One invocation only; workflow scheduling belongs to the host.</summary>
public sealed class BlobTool : IToolDefinition<BlobSettings, BlobOutput>, IToolResultPresentationProvider, IToolSettingsFields, IToolCatalogProvider,IToolParameterBindingNames,IToolManualReferenceSources,IToolKnownSettingsValidation,IToolParameterReferenceBehavior,IToolExecutionInputs,IToolResultDisplayProvider,IToolDormantParameterDefaults,IToolFieldTransitions
{
    public const string Id = "BlobAnalysis";
    public const string ImageInput = "input.image";
    public const string PoseInput = "input.pose";
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources {get;} =
        Array.AsReadOnly(new[] {11,12,19,20,23,24}.Select(id=>new ManualReferenceSource(BlobSchema.Field(id)))
            .Append(new ManualReferenceSource(BlobSchema.ModuleStatusPort))
            .Append(new ManualReferenceSource(BlobSchema.ModuleRunTimePort))
            .Append(new ManualReferenceSource(BlobSchema.BlobStatusesPort))
            .Append(new ManualReferenceSource(BlobSchema.CentroidPointsPort))
            .Concat(FixtureDeclarations.Point.Fields.Select(field=>new ManualReferenceSource(BlobSchema.CentroidPointsPort,new[]{field.Id})))
            .Concat(new[]{BlobSchema.MinGrayValuesPort,BlobSchema.MaxGrayValuesPort,BlobSchema.GrayContrastsPort,BlobSchema.PrincipalAnglesPort,
                BlobSchema.LongAxesPort,BlobSchema.ShortAxesPort,BlobSchema.RectangleTopLeftXPort,BlobSchema.RectangleTopLeftYPort,
                BlobSchema.RectangleBottomRightXPort,BlobSchema.RectangleBottomRightYPort}
                .Select(port=>new ManualReferenceSource(port)))
            .Concat(ShapeSources(BlobSchema.AxisAlignedRectanglesPort, BlobSchema.BoundsType, "origin"))
            .Concat(ShapeSources(BlobSchema.InnerRectanglesPort, BlobRoiInheritance.RectangleType))
            .Concat(ShapeSources(BlobSchema.OuterRectanglesPort, BlobRoiInheritance.RectangleType))
            .Concat(ShapeSources(BlobSchema.DetectionRectanglesPort, BlobRoiInheritance.RectangleType))
            .Concat(ShapeSources(BlobSchema.DetectionAnnuliPort, BlobRoiInheritance.AnnulusType))
            .Concat(new[]{5,6}.SelectMany(id=>new[]{"width","height","pixel-format"}.Select(member=>new ManualReferenceSource(BlobSchema.Field(id),new[]{member})))).ToArray());
    private static IEnumerable<ManualReferenceSource> ShapeSources(string portId,TypeRef shape,string pointMember="center")
    {
        yield return new(portId);
        foreach(var field in shape.Fields)yield return new(portId,new[]{field.Id});
        foreach(var field in FixtureDeclarations.Point.Fields)yield return new(portId,new[]{pointMember,field.Id});
    }
    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog {get;}=new("Blob分析","连通域分析与特征提取","blob","定位",10,"DEFAULT");
    public ToolSchema ResultSchema => new(Array.Empty<Port>(), BlobSchema.Outputs,outputDisplay:BlobSchema.OutputDisplay);
    public ResultProjectionDescriptor ResultProjection => BlobDisplayDefinition.Instance.Table;
    public ToolSchema SettingsSchema=>BlobParameterDefinition.Instance.SettingsSchema;
    public ToolSchema DisplaySettingsSchema=>BlobResultDisplay.Schema;
    public InputValues CreateDisplaySettings()=>BlobResultDisplay.Defaults();
    public ResultDisplayDescriptor ResultDisplay=>BlobResultDisplay.Descriptor;
    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings fields)=>BlobResultDisplay.Capture(fields);
    public IReadOnlyDictionary<string,string> ParameterBindingNames=>BlobParameterDefinition.Instance.ParameterBindingNames;
    public InputValues ReadSettings(object settings)=>BlobParameterDefinition.Instance.ReadSettings(settings as BlobSettings??throw new ArgumentException("BlobSettings is required."));
    public object MaterializeSettings(InputValues values)=>BlobParameterDefinition.Instance.MaterializeSettings(values);
    public IReadOnlyDictionary<string,ParameterReferenceBehavior> ParameterReferenceBehaviors {get;}
        =new System.Collections.ObjectModel.ReadOnlyDictionary<string,ParameterReferenceBehavior>(
            new[]{nameof(BlobSettings.Threshold),nameof(BlobSettings.UpperThreshold),nameof(BlobSettings.BlobCountLimit),nameof(BlobSettings.FilterAreaMin),nameof(BlobSettings.FilterAreaMax),
                nameof(BlobSettings.FixedSoftLowerThreshold),nameof(BlobSettings.FixedSoftUpperThreshold),nameof(BlobSettings.ThresholdSoftness),
                nameof(BlobSettings.RelativeLeftRatio),nameof(BlobSettings.RelativeRightRatio),nameof(BlobSettings.RelativeLowerRatio),nameof(BlobSettings.RelativeUpperRatio)}
                .ToDictionary(name=>BlobParameterDefinition.Instance.SettingsFieldId(name),_=>ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback));
    public ToolSchema Describe()
    {
        var inputs=new[] {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes:FieldEditModes.Reference),
        new Port(PoseInput, FixtureDeclarations.Info, false, EditModes:FieldEditModes.Constant|FieldEditModes.Reference),
        BlobLocalRoi.Port
        };
        return new(inputs.Concat(BlobRoiInheritance.Ports).Concat(BlobImageRoi.Ports).Concat(BlobFixtureInputs.Ports).Concat(BlobMaskInheritance.Ports),BlobSchema.Outputs,new ToolPresentation(inputs.Where(port=>port.Id!=BlobLocalRoi.InputId).Select((port,index)=>BlobParameterDefinition.Instance.InputPresentation(port,index))),
            workflowImageInputId:BlobParameterDefinition.Instance.WorkflowImageInputId,outputDisplay:BlobSchema.OutputDisplay);
    }
    public IReadOnlyList<string> SelectExecutionInputs(FieldSettings configuration)
        => BlobImageRoi.IsImageMode(configuration.Read(BlobImageRoi.ModeId))
            ? new[] { ImageInput, BlobImageRoi.ModeId, BlobImageRoi.ImageId, BlobImageRoi.OutputEnabledId }
            : BlobRoiInheritance.ActiveInputs(configuration, Describe().Inputs.Where(port => port.Id != BlobImageRoi.ImageId
                && (port.Id != BlobMaskInheritance.SourceId || BlobMaskInheritance.IsInherited(configuration.Read(BlobMaskInheritance.ModeId)))
                && !BlobFixtureInputs.IsProjection(port.Id) && (!BlobFixtureInputs.LeafIds.Contains(port.Id) || IsPositionCorrectionActive(configuration))));

    private static string CorrectionEnabledId => BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsPositionCorrectionEnabled));
    private static bool IsPositionCorrectionActive(FieldSettings configuration)
        => configuration.Declarations.ContainsKey(CorrectionEnabledId)
            && configuration.Read(CorrectionEnabledId) is ConstantSetting { Value:true };

    public IReadOnlyDictionary<string, ParameterSetting> ExpandChanges(FieldSettings current,
        IReadOnlyDictionary<string, ParameterSetting> changes)
    {
        if (changes.GetValueOrDefault(BlobLocalRoi.InputId) is ConstantSetting { Value: RecordValue roi })
            BlobLocalRoi.Validate(roi);
        var currentInheritance = BlobRoiInheritance.ReadMode(current);
        var inheritance = changes.TryGetValue(BlobRoiInheritance.ModeId, out var proposedMode)
            ? BlobRoiInheritance.ReadMode(proposedMode) : currentInheritance;
        var hidesInheritance = currentInheritance != BlobRoiInheritance.Local
            && changes.TryGetValue(BlobImageRoi.ModeId, out var proposedArea)
            && BlobImageRoi.IsImageMode(proposedArea)
            && !BlobImageRoi.IsImageMode(current.Read(BlobImageRoi.ModeId));
        if (hidesInheritance || inheritance != currentInheritance)
        {
            var roiChanges = new Dictionary<string, ParameterSetting>(changes, StringComparer.Ordinal);
            // Hiding InheritWay clears both source families, but keeps the radio selection.
            foreach (var id in BlobRoiInheritance.SourcesToClear(hidesInheritance ? BlobRoiInheritance.Local : inheritance))
            {
                if (roiChanges.TryGetValue(id, out var source) && source is not UnsetSetting)
                    throw new ArgumentException("当前ROI继承方式不能设置该来源。");
                if (current.Read(id) is not UnsetSetting) roiChanges[id] = new UnsetSetting();
            }
            changes = roiChanges;
        }
        if (changes.ContainsKey(BlobMaskInheritance.ModeId) || changes.ContainsKey(BlobMaskInheritance.SourceId))
        {
            var mode = changes.GetValueOrDefault(BlobMaskInheritance.ModeId) ?? current.Read(BlobMaskInheritance.ModeId);
            if (!BlobMaskInheritance.IsInherited(mode))
            {
                if (changes.TryGetValue(BlobMaskInheritance.SourceId, out var source) && source is not UnsetSetting)
                    throw new ArgumentException("绘制模式不能安装继承屏蔽区来源。");
                changes = new Dictionary<string,ParameterSetting>(changes,StringComparer.Ordinal)
                { [BlobMaskInheritance.SourceId] = new UnsetSetting() };
            }
        }
        if (!changes.ContainsKey(CorrectionEnabledId) && !changes.ContainsKey(BlobImageRoi.ModeId)
            && !changes.Keys.Any(id => BlobFixtureInputs.IsProjection(id) || BlobFixtureInputs.LeafIds.Contains(id)))
            return changes;
        ParameterSetting Read(string id) => changes.TryGetValue(id, out var setting)
            ? setting is UnsetSetting ? setting : FieldSettings.Freeze(current.Declarations[id], setting) : current.Read(id);
        var expanded = changes.Where(pair => !BlobFixtureInputs.IsProjection(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var (id, value) in changes.Where(pair => BlobFixtureInputs.IsProjection(pair.Key)))
            BlobFixtureInputs.Expand(id, value is UnsetSetting ? value : FieldSettings.Freeze(current.Declarations[id], value), expanded);
        if (Read(CorrectionEnabledId) is not ConstantSetting { Value:true } || BlobImageRoi.IsImageMode(Read(BlobImageRoi.ModeId)))
        {
            foreach (var leaf in BlobFixtureInputs.LeafIds)
            {
                if (expanded.TryGetValue(leaf, out var source) && source is not UnsetSetting)
                    throw new ArgumentException("位置修正启用且使用图形 ROI 时才可设置修正来源。");
                if (current.Read(leaf) is not UnsetSetting) expanded[leaf] = new UnsetSetting();
            }
        }
        return expanded;
    }

    public ParameterSetting ProjectInput(FieldSettings current, string fieldId)
        => BlobFixtureInputs.IsProjection(fieldId) ? BlobFixtureInputs.Project(current, fieldId) : current.Read(fieldId);

    public IReadOnlyList<Diagnostic> ValidateExecutionInputs(FieldSettings configuration)
        => BlobImageRoi.IsImageMode(configuration.Read(BlobImageRoi.ModeId))
            ? configuration.Read(BlobImageRoi.ImageId) is UnsetSetting
                ? new[] { new Diagnostic("BLOB_MASK_IMAGE_REQUIRED", "请选择屏蔽图像。", BlobImageRoi.ImageId) }
                : Array.Empty<Diagnostic>()
            : IsPositionCorrectionActive(configuration) && BlobFixtureInputs.LeafIds.Any(id => configuration.Read(id) is UnsetSetting)
                ? new[] { new Diagnostic("BLOB_FIXTURE_REQUIRED", "请选择完整的位置修正来源。", PoseInput) }
                : BlobMaskInheritance.IsInherited(configuration.Read(BlobMaskInheritance.ModeId))
                    && configuration.Read(BlobMaskInheritance.SourceId) is UnsetSetting
                    ? new[] { new Diagnostic("BLOB_MASK_SOURCE_REQUIRED","请选择继承屏蔽区来源。",BlobMaskInheritance.SourceId) }
                    : BlobRoiInheritance.ValidateConfiguration(configuration);
    public IReadOnlyDictionary<string, ConstantSetting> SelectDormantParameterDefaults(FieldSettings configuration)
    {
        var definition = BlobParameterDefinition.Instance;
        var selected = configuration.Read(definition.SettingsFieldId(nameof(BlobSettings.SegmentationMode))) as ConstantSetting;
        var result = new Dictionary<string, ConstantSetting>(StringComparer.Ordinal);
        var outsidePixelLimit = definition.SettingsFieldId(nameof(BlobFindParameters.MaxOutPixelNum));
        var outsidePixelEnabled = definition.SettingsFieldId(nameof(BlobFindParameters.MaxOutPixelNumEnable));
        if (configuration.Read(outsidePixelEnabled) is UnsetSetting or ConstantSetting { Value: false }
            && configuration.Read(outsidePixelLimit) is ReferenceSetting or VariableSetting)
            result.Add(outsidePixelLimit, new ConstantSetting(definition.Default<int>(nameof(BlobFindParameters.MaxOutPixelNum))));
        if (selected?.Value is not string name || !Enum.TryParse<BlobSegmentationMode>(name, out var mode)) return result;
        foreach (var property in BlobThresholdCapabilities.ControlledFields)
        {
            var id = definition.SettingsFieldId(property);
            if (!BlobThresholdCapabilities.IsThresholdFieldActive(mode, property)
                && configuration.Read(id) is ReferenceSetting or VariableSetting)
                result.Add(id, new ConstantSetting(definition.Default<int>(property)));
        }
        return result;
    }
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var definition=BlobParameterDefinition.Instance;
        var errors=new List<Diagnostic>();
        object? Constant(string name)=>settings.Read(definition.SettingsFieldId(name)) is ConstantSetting constant?constant.Value:null;
        foreach(var presentation in SettingsSchema.Presentation!.Fields)
        {
            if(settings.Read(presentation.FieldId) is not ConstantSetting {Value:long or double} constant)continue;
            var number=Convert.ToDouble(constant.Value,System.Globalization.CultureInfo.InvariantCulture);
            var layout=presentation.Layout;
            if(!double.IsFinite(number)||layout?.Minimum is double minimum&&number<minimum
                ||layout?.Maximum is double maximum&&number>maximum)
                errors.Add(new("BLOB_SETTINGS_INVALID",presentation.DisplayName+"超出允许范围。",presentation.FieldId));
        }
        void Range(string enabled,string minimum,string maximum)
        {
            if(Constant(enabled) is not true||minimum==nameof(BlobSettings.FilterAreaMin))return;
            var lower=Constant(minimum);var upper=Constant(maximum);
            if(lower is long or double && upper is long or double &&
               Convert.ToDouble(lower,System.Globalization.CultureInfo.InvariantCulture)>Convert.ToDouble(upper,System.Globalization.CultureInfo.InvariantCulture))
                errors.Add(new("BLOB_SETTINGS_INVALID","筛选下限不能超过上限。",definition.SettingsFieldId(minimum)));
        }
        if(!BlobThresholdCapabilities.IsSupported(Constant(nameof(BlobSettings.SegmentationMode)) as string))
            errors.Add(new("BLOB_MODE_NOT_READY","该阈值模式尚未完成原厂语义对齐。",definition.SettingsFieldId(nameof(BlobSettings.SegmentationMode))));
        Range(nameof(BlobSettings.IsAreaFilterEnabled),nameof(BlobSettings.FilterAreaMin),nameof(BlobSettings.FilterAreaMax));
        Range(nameof(BlobSettings.IsCircularityFilterEnabled),nameof(BlobSettings.FilterCircularityMin),nameof(BlobSettings.FilterCircularityMax));
        Range(nameof(BlobSettings.IsRectangularityFilterEnabled),nameof(BlobSettings.FilterRectangularityMin),nameof(BlobSettings.FilterRectangularityMax));
        return errors.AsReadOnly();
    }
    public IReadOnlyList<Diagnostic> Validate(BlobSettings settings) => settings.Validate();
    public ToolCapabilities Capabilities() => new(new[] { "halcon" });
    public IToolSession<BlobSettings, BlobOutput> CreateSession() => new BlobSession();
}
