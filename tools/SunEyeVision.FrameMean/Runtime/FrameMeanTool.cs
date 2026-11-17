using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>帧均值 runtime entry. One invocation only; accumulation belongs to the execution node.</summary>
public sealed class FrameMeanTool : IToolDefinition<FrameMeanSettings,FrameMeanOutput>,IToolSettingsFields,
    IToolCatalogProvider,IToolResultPresentationProvider,IToolRuntimeInitialization
{
    public const string Id="FrameMean";
    public const string ImageInput="input.image";
    public string ToolId=>Id;
    public ToolCatalogDescriptor Catalog {get;}=new("帧均值","多帧图像统计平均，输出累计均值图像","frame-mean","图像处理",10,"IMAGE");
    public ToolSchema SettingsSchema=>FrameMeanFields.Schema;
    public ToolSchema ResultSchema {get;}=new(Array.Empty<Port>(),FrameMeanOutput.Ports,outputDisplay:FrameMeanOutput.Display);
    public ResultProjectionDescriptor ResultProjection {get;}=new(ResultProjectionKind.PropertyList,ResultCardinality.One,new[] {
        new ResultColumnDescriptor("image","均值图像",new(FrameMeanOutput.Image)),
        new ResultColumnDescriptor("count","已统计图像数量",new(FrameMeanOutput.ImageCount)),
        new ResultColumnDescriptor("time","模块耗时",new(FrameMeanOutput.ModuleRunTime))
    });
    public InputValues ReadSettings(object settings)=>FrameMeanFields.Read(RequireSettings(settings));
    public object MaterializeSettings(InputValues values)=>FrameMeanFields.Materialize(values);
    private static FrameMeanSettings RequireSettings(object settings)=>settings as FrameMeanSettings
        ??throw new ArgumentException("帧均值设置类型不正确。");
    public ToolSchema Describe()=>Schema;
    public static ToolSchema Schema {get;}=new(new[] {
        new Port(ImageInput,TypeRef.ResourceOf(ResourceKind.Image),EditModes:FieldEditModes.Reference)
    },FrameMeanOutput.Ports,new ToolPresentation(Array.Empty<FieldPresentation>()),
        workflowImageInputId:ImageInput,outputDisplay:FrameMeanOutput.Display);
    public IReadOnlyList<Diagnostic> Validate(FrameMeanSettings settings)=>settings.Validate();
    public ToolCapabilities Capabilities()=>new(new[]{"halcon"});
    public void InitializeRuntime()=>HalconPlatform.Initialise();
    public IToolSession<FrameMeanSettings,FrameMeanOutput> CreateSession()=>new FrameMeanSession();
}
