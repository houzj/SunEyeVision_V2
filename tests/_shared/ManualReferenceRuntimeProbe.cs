using System.Collections.Concurrent;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.TestFixtures;

public sealed record ManualReferenceProbeSettings:IToolSettings<ManualReferenceProbeSettings>
{
    public string CaseId {get;init;}="";
    public string Label {get;init;}="";
    public bool Source {get;init;}
    public double Value {get;init;}=5;
    public long Index {get;init;}=1;
    public double Input {get;init;}=777;
    public long IntegerInput {get;init;}=999;
    public ManualReferenceProbeSettings Freeze()=>this with{};
}
public sealed class ManualReferenceProbeState
{
    public ConcurrentQueue<string> Trace {get;}=new();
}
public sealed class ManualReferenceProbeOutput(double value,long index):IToolOutput,IDisposable
{
    public double Value {get;}=value;
    public long Index {get;}=index;
    public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?> {
        ["output.number"]=Value,["output.index"]=Index,["output.values"]=new double[]{Value,Value+10},
        ["output.hidden"]=Value+30,["output.indices"]=new SequenceValue(ManualReferenceProbeDefinitionBase.IntegerSequence,new object?[]{0L,Index}),
        ["output.record"]=new RecordValue(ManualReferenceProbeDefinitionBase.RecordType,new Dictionary<string,object?> {
            ["allowed"]=Value+20,["hidden"]=Value+30,["index"]=Index,["hidden-index"]=Index})};
    public void Dispose(){}
}
// This test-only assembly references Contracts only. Its outputs are all real; qualification
// changes selection/admission, never whether the deterministic session produces a value.
public abstract class ManualReferenceProbeDefinitionBase:IToolDefinition<ManualReferenceProbeSettings,ManualReferenceProbeOutput>,IToolSettingsFields,IToolCatalogProvider,IToolParameterBindingNames
{
    public const string NumberInput="input.number",IntegerInput="input.integer",GeneralInput="runtime.input.number";
    public static ConcurrentDictionary<string,ManualReferenceProbeState> States {get;}=new();
    public static TypeRef RecordType {get;}=TypeRef.RecordOf("manual-reference.record",new[] {
        new Port("allowed",DataType.Number),new Port("hidden",DataType.Number),new Port("index",DataType.Integer),new Port("hidden-index",DataType.Integer)});
    public static TypeRef IntegerSequence {get;}=TypeRef.SequenceOf(DataType.Integer);
    public static ToolSchema OutputSchema {get;}=new(new[]{new Port(GeneralInput,DataType.Number,Required:false,EditModes:FieldEditModes.Reference)},new[] {
        new Port("output.number",DataType.Number),new Port("output.index",DataType.Integer),new Port("output.values",DataType.NumberArray),
        new Port("output.hidden",DataType.Number),new Port("output.record",RecordType),new Port("output.indices",IntegerSequence)});
    public abstract string ToolId {get;}
    public ToolCatalogDescriptor Catalog {get;}=new("普通来源资格测试","仅验证正式来源与执行消费合同","generic","测试",0);
    public ToolSchema Describe()=>OutputSchema;
    public ToolSchema SettingsSchema {get;}=new(new[] {
        new Port("case",DataType.Text),new Port("label",DataType.Text),new Port("source",DataType.Boolean),
        new Port("value",DataType.Number),new Port("index",DataType.Integer),
        new Port(NumberInput,DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new Port(IntegerInput,DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference)},Array.Empty<Port>());
    public IReadOnlyDictionary<string,string> ParameterBindingNames=>SettingsSchema.Inputs.ToDictionary(port=>port.Id,port=>port.Id,StringComparer.Ordinal);
    public InputValues ReadSettings(object settings)
    {
        var value=(ManualReferenceProbeSettings)settings;
        return new(SettingsSchema.Inputs,new Dictionary<string,object?> {
            ["case"]=value.CaseId,["label"]=value.Label,["source"]=value.Source,["value"]=value.Value,["index"]=value.Index,
            [NumberInput]=value.Input,[IntegerInput]=value.IntegerInput});
    }
    public object MaterializeSettings(InputValues values)=>new ManualReferenceProbeSettings {
        CaseId=values.Get<string>("case"),Label=values.Get<string>("label"),Source=values.Get<bool>("source"),
        Value=values.Get<double>("value"),Index=values.Get<long>("index"),Input=values.Get<double>(NumberInput),IntegerInput=values.Get<long>(IntegerInput)};
    public IReadOnlyList<Diagnostic> Validate(ManualReferenceProbeSettings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<ManualReferenceProbeSettings,ManualReferenceProbeOutput> CreateSession()=>new Session();
    private sealed class Session:IToolSession<ManualReferenceProbeSettings,ManualReferenceProbeOutput>
    {
        public ValueTask<ToolOutcome<ManualReferenceProbeOutput>> ExecuteAsync(ToolInvocation<ManualReferenceProbeSettings> invocation)
        {
            invocation.Context.Cancellation.ThrowIfCancellationRequested();var settings=invocation.Settings;
            var state=States[settings.CaseId];state.Trace.Enqueue(settings.Label);
            invocation.Context.Cancellation.ThrowIfCancellationRequested();
            var general=invocation.Inputs.Read(GeneralInput);
            var number=settings.Source?settings.Value:general.IsMissing?settings.Input:general.GetScalar<double>();
            return ValueTask.FromResult(ToolOutcome<ManualReferenceProbeOutput>.Completed(
                new(number,settings.Source?settings.Index:settings.IntegerInput)));
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
public sealed class ManualReferenceQualifiedProbe:ManualReferenceProbeDefinitionBase,IToolManualReferenceSources
{
    public const string Id="ManualReferenceQualifiedProbe";
    public override string ToolId=>Id;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources {get;}=Array.AsReadOnly(new[] {
        new ManualReferenceSource("output.number"),new ManualReferenceSource("output.index"),new ManualReferenceSource("output.values"),new ManualReferenceSource("output.indices"),
        new ManualReferenceSource("output.record",new[]{"allowed"}),new ManualReferenceSource("output.record",new[]{"index"})});
}
public sealed class ManualReferenceUndeclaredProbe:ManualReferenceProbeDefinitionBase
{
    public const string Id="ManualReferenceUndeclaredProbe";
    public override string ToolId=>Id;
}
