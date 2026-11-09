using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob runtime entry. One invocation only; workflow scheduling belongs to the host.</summary>
public sealed class BlobTool : IToolDefinition<BlobSettings, BlobOutput>, IToolResultPresentationProvider, IToolSettingsFields, IToolCatalogProvider,IToolParameterBindingNames,IToolManualReferenceSources,IToolKnownSettingsValidation,IToolParameterReferenceBehavior,IToolExecutionInputs,IToolResultDisplayProvider
{
    public const string Id = "BlobAnalysis";
    public const string ImageInput = "input.image";
    public const string InspectionInput = "input.inspection.region";
    public const string MaskInput = "input.mask.region";
    public const string PoseInput = "input.pose";
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources {get;} =
        Array.AsReadOnly(new[] {11,12,19,20,21,22,23,24}.Select(id=>new ManualReferenceSource(BlobSchema.Field(id)))
            .Concat(new[]{"center-x","center-y","width","height","angle-degrees"}.Select(member=>new ManualReferenceSource(BlobSchema.BlobsPort,new[]{"oriented-bounds",member})))
            .Concat(new[]{5,6}.SelectMany(id=>new[]{"width","height","pixel-format"}.Select(member=>new ManualReferenceSource(BlobSchema.Field(id),new[]{member})))).ToArray());
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
        new Port(InspectionInput, TypeRef.ResourceOf(ResourceKind.Region), false, EditModes:FieldEditModes.Reference),
        new Port(MaskInput, TypeRef.ResourceOf(ResourceKind.Region), false, EditModes:FieldEditModes.Reference),
        new Port(PoseInput, SpatialDeclarations.Affine2D, false, EditModes:FieldEditModes.Constant|FieldEditModes.Reference),
        BlobLocalRoi.Port
        };
        return new(inputs.Concat(BlobRoiInheritance.Ports),BlobSchema.Outputs,new ToolPresentation(inputs.Where(port=>port.Id!=BlobLocalRoi.InputId).Select((port,index)=>BlobParameterDefinition.Instance.InputPresentation(port,index))),
            workflowImageInputId:BlobParameterDefinition.Instance.WorkflowImageInputId,outputDisplay:BlobSchema.OutputDisplay);
    }
    public IReadOnlyList<string> SelectExecutionInputs(FieldSettings configuration)=>BlobRoiInheritance.ActiveInputs(configuration,Describe().Inputs);
    public IReadOnlyList<Diagnostic> ValidateExecutionInputs(FieldSettings configuration)=>BlobRoiInheritance.ValidateConfiguration(configuration);
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
