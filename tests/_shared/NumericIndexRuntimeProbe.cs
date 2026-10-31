using System.Collections.Concurrent;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.NumericIndexFixtures;

public sealed record NumericIndexProbeSettings:IToolSettings<NumericIndexProbeSettings>
{
    public string CaseId {get;init;}="";
    public string Label {get;init;}="";
    public bool Source {get;init;}
    public double Value {get;init;}=5;
    public long Index {get;init;}=1;
    public double Input {get;init;}=777;
    public long IntegerInput {get;init;}=999;
    public NumericIndexProbeSettings Freeze()=>this with{};
}
public sealed class NumericIndexProbeState
{
    public ConcurrentQueue<string> Trace {get;}=new();
}
public sealed class NumericIndexProbeOutput(double value,long index,string label):IToolOutput,IDisposable
{
    public double Value {get;}=value;
    public long Index {get;}=index;
    public IReadOnlyDictionary<string,object?> ToValues()
    {
        var values=new Dictionary<string,object?> {
        ["output.number"]=Value,["output.index"]=Index,["output.values"]=new double[]{Value,Value+10},
        ["output.hidden"]=Value+30,["output.indices"]=new SequenceValue(NumericIndexProbeDefinitionBase.IntegerSequence,label=="empty-main"?Array.Empty<object?>():label=="big-main"?new object?[]{2147483648L,90L,173L}:label=="ordinal"?new object?[]{0L,1L,2L}:new object?[]{37L,90L,173L}),
        ["output.index-array"]=new SequenceValue(NumericIndexProbeDefinitionBase.IntegerSequence,label=="empty-index"?Array.Empty<object?>():new object?[]{Index,2L}),
        ["output.record"]=new RecordValue(NumericIndexProbeDefinitionBase.RecordType,new Dictionary<string,object?> {
            ["allowed"]=Value+20,["hidden"]=Value+30,["index"]=Index,["hidden-index"]=Index})};
        if(label=="missing-index")values.Remove("output.index");
        return values;
    }
    public void Dispose(){}
}
// This test-only assembly references Contracts only. Its outputs are all real; qualification
// changes selection/admission, never whether the deterministic session produces a value.
public abstract class NumericIndexProbeDefinitionBase:IToolDefinition<NumericIndexProbeSettings,NumericIndexProbeOutput>,IToolSettingsFields,IToolCatalogProvider,IToolParameterBindingNames
{
    public const string NumberInput="input.number",IntegerInput="input.integer",GeneralInput="runtime.input.number";
    public static ConcurrentDictionary<string,NumericIndexProbeState> States {get;}=new();
    public static TypeRef RecordType {get;}=TypeRef.RecordOf("manual-reference.record",new[] {
        new Port("allowed",DataType.Number),new Port("hidden",DataType.Number),new Port("index",DataType.Integer),new Port("hidden-index",DataType.Integer)});
    public static TypeRef IntegerSequence {get;}=TypeRef.SequenceOf(DataType.Integer);
    public static ToolSchema OutputSchema {get;}=new(new[]{new Port(GeneralInput,DataType.Number,Required:false,EditModes:FieldEditModes.Reference)},new[] {
        new Port("output.number",DataType.Number),new Port("output.index",DataType.Integer,Required:false),new Port("output.values",DataType.NumberArray),
        new Port("output.hidden",DataType.Number),new Port("output.record",RecordType),new Port("output.indices",IntegerSequence),new Port("output.index-array",IntegerSequence)});
    public abstract string ToolId {get;}
    public ToolCatalogDescriptor Catalog {get;}=new("普通来源资格测试","仅验证正式来源与执行消费合同","generic","测试",0);
    public ToolSchema Describe()=>OutputSchema;
    public ToolSchema SettingsSchema {get;}=new(new[] {
        new Port("case",DataType.Text),new Port("label",DataType.Text),new Port("source",DataType.Boolean),
        new Port("value",DataType.Number),new Port("index",DataType.Integer),
        new Port(NumberInput,DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new Port(IntegerInput,DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference)},Array.Empty<Port>());
    public IReadOnlyDictionary<string,string> ParameterBindingNames=>SettingsSchema.Inputs.ToDictionary(port=>port.Id,port=>port.Id,StringComparer.Ordinal);
    public InputValues ReadSettings(object settings)
    {
        var value=(NumericIndexProbeSettings)settings;
        return new(SettingsSchema.Inputs,new Dictionary<string,object?> {
            ["case"]=value.CaseId,["label"]=value.Label,["source"]=value.Source,["value"]=value.Value,["index"]=value.Index,
            [NumberInput]=value.Input,[IntegerInput]=value.IntegerInput});
    }
    public object MaterializeSettings(InputValues values)=>new NumericIndexProbeSettings {
        CaseId=values.Get<string>("case"),Label=values.Get<string>("label"),Source=values.Get<bool>("source"),
        Value=values.Get<double>("value"),Index=values.Get<long>("index"),Input=values.Get<double>(NumberInput),IntegerInput=values.Get<long>(IntegerInput)};
    public IReadOnlyList<Diagnostic> Validate(NumericIndexProbeSettings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<NumericIndexProbeSettings,NumericIndexProbeOutput> CreateSession()=>new Session();
    private sealed class Session:IToolSession<NumericIndexProbeSettings,NumericIndexProbeOutput>
    {
        public ValueTask<ToolOutcome<NumericIndexProbeOutput>> ExecuteAsync(ToolInvocation<NumericIndexProbeSettings> invocation)
        {
            invocation.Context.Cancellation.ThrowIfCancellationRequested();var settings=invocation.Settings;
            var state=States[settings.CaseId];state.Trace.Enqueue(settings.Label);
            if(settings.Label=="fail")return ValueTask.FromResult(ToolOutcome<NumericIndexProbeOutput>.Failed("probe-failed","Producer failed."));
            invocation.Context.Cancellation.ThrowIfCancellationRequested();
            var general=invocation.Inputs.Read(GeneralInput);
            var number=settings.Source?settings.Value:general.IsMissing?settings.Input:general.GetScalar<double>();
            return ValueTask.FromResult(ToolOutcome<NumericIndexProbeOutput>.Completed(
                new(number,settings.Source?settings.Index:settings.IntegerInput,settings.Label)));
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
public class NumericIndexQualifiedProbe:NumericIndexProbeDefinitionBase,IToolManualReferenceSources,IToolParameterReferenceBehavior
{
    public const string Id="NumericIndexQualifiedProbe";
    public virtual IReadOnlyDictionary<string,ParameterReferenceBehavior> ParameterReferenceBehaviors {get;}=new Dictionary<string,ParameterReferenceBehavior>{{IntegerInput,ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback}};
    public override string ToolId=>Id;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources {get;}=Array.AsReadOnly(new[] {
        new ManualReferenceSource("output.number"),new ManualReferenceSource("output.index"),new ManualReferenceSource("output.values"),new ManualReferenceSource("output.indices"),new ManualReferenceSource("output.index-array"),
        new ManualReferenceSource("output.record",new[]{"allowed"}),new ManualReferenceSource("output.record",new[]{"index"})});
}
public sealed class NumericIndexStrictProbe:NumericIndexQualifiedProbe
{
    public new const string Id="NumericIndexStrictProbe";
    public override string ToolId=>Id;
    public override IReadOnlyDictionary<string,ParameterReferenceBehavior> ParameterReferenceBehaviors {get;}=new Dictionary<string,ParameterReferenceBehavior>();
}
public sealed class NumericIndexSecondProbe:NumericIndexQualifiedProbe
{
    public new const string Id="NumericIndexSecondProbe";
    public override string ToolId=>Id;
}
public sealed class NumericIndexInvalidProbe:NumericIndexQualifiedProbe
{
    public new const string Id="NumericIndexInvalidProbe";
    public override string ToolId=>Id;
    public override IReadOnlyDictionary<string,ParameterReferenceBehavior> ParameterReferenceBehaviors {get;}=new Dictionary<string,ParameterReferenceBehavior>{{GeneralInput,ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback}};
}
