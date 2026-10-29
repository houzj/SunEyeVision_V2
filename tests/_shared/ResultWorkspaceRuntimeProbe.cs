using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.TestFixtures;

// Test-only declarations for result consumers. No algorithm, UI, SDK or product deployment.
public sealed record WorkspaceProbeSettings : IToolSettings<WorkspaceProbeSettings>
{
    public WorkspaceProbeSettings Freeze()=>this with{};
}
public sealed class WorkspaceProbeOutput(IReadOnlyDictionary<string,object?> values) : IToolOutput,IDisposable
{
    public int Disposals {get;private set;}
    public IReadOnlyDictionary<string,object?> ToValues()=>values;
    public void Dispose()=>Disposals++;
}
public sealed class WorkspaceProbeDefinition : IToolDefinition<WorkspaceProbeSettings,WorkspaceProbeOutput>,IToolCatalogProvider,IToolSettingsFields
{
    public static readonly CoordinateFrame Coordinates=new("workspace-probe:pixel",CoordinateDomain.Image,CoordinateUnit.Pixel);
    public static readonly ToolSchema Schema=new(Array.Empty<Port>(),new[]{
        new Port("image",TypeRef.ResourceOf(ResourceKind.Image)),new Port("region",TypeRef.ResourceOf(ResourceKind.Region)),
        new Port("point",TypeRef.GeometryOf(GeometryKind.Point,Coordinates)),new Port("count",TypeRef.ScalarOf(ScalarKind.Int64)),
        new Port("items",TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64)))});
    public string ToolId=>"ResultWorkspaceProbe";
    public ToolCatalogDescriptor Catalog {get;}=new("结果工作区测试声明","仅供结果消费者受控测试","generic","定位",0);
    public ToolSchema Describe()=>Schema;
    public ToolSchema SettingsSchema=>new(Array.Empty<Port>(),Array.Empty<Port>());
    public InputValues ReadSettings(object settings)=>new(SettingsSchema.Inputs,new Dictionary<string,object?>());
    public object MaterializeSettings(InputValues values){_=new InputValues(SettingsSchema.Inputs,values.CopyValues());return new WorkspaceProbeSettings();}
    public IReadOnlyList<Diagnostic> Validate(WorkspaceProbeSettings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<WorkspaceProbeSettings,WorkspaceProbeOutput> CreateSession()
        =>throw new NotSupportedException("结果消费者测试声明不提供算法执行。");
}
