using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

internal static partial class Program
{
    private static Task BindingCases()=>RunBindingCases();
    private static async Task RunBindingCases()
    {
        await Case("FT-G03-binding-persistence","Typed reference roundtrip preserves stable IDs, shape/count, coordinate frame and index intent",()=>Sync(()=>{
            var record=TypeRef.RecordOf("measurement",new[]{new Port("p",TypeRef.GeometryOf(GeometryKind.Point,GeometryFixture.Frame)),new Port("v",DataType.Number,false,true)});
            var type=TypeRef.SequenceOf(record,new CountConstraint(0,8));
            var reference=new OutputReference("source","stable-field",type,new[]{"p"},new BindingSelection.AtField(new("index","stable-index",DataType.Integer,scopeId:"flow")),"flow");
            var json=BindingReferenceSerializer.Serialize(reference);var restored=BindingReferenceSerializer.Deserialize(json);
            Require(restored.NodeId=="source"&&restored.PortId=="stable-field"&&restored.ScopeId=="flow"&&restored.Type==type&&restored.MemberFieldPath.Single()=="p","declaration/identity drift");
            Require(restored.Selection is BindingSelection.AtField at && at.Reference.PortId=="stable-index"&&at.Reference.Type==TypeRef.ScalarOf(ScalarKind.Int64),"dynamic index intent lost");
            Throws<System.Text.Json.JsonException>(()=>BindingReferenceSerializer.Deserialize(json.Replace("\"ContractMajor\":4","\"ContractMajor\":3")));
            var minor=System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();minor["ContractMinor"]=FoundationContract.Minor+1;
            Throws<System.Text.Json.JsonException>(()=>BindingReferenceSerializer.Deserialize(minor.ToJsonString()));
            minor.Remove("ContractMinor");Throws<System.Text.Json.JsonException>(()=>BindingReferenceSerializer.Deserialize(minor.ToJsonString()));
            Require(!json.Contains("$type")&&!json.Contains("RunId"),"runtime/CLR identity persisted as declaration");
        }));
        await Case("FT-A01-runtime-closure","Pure Runtime has no WPF, SDK, Workflow or native dependency",()=>Sync(()=>{
            var names=typeof(ParameterBindingService).Assembly.GetReferencedAssemblies().Select(a=>a.Name!).ToArray();
            Require(names.All(n=>n.StartsWith("System.")||n=="SunEyeVision.Plugin.Contracts"),string.Join(",",names));
        }));
        await Case("FT-F02-all-one-many","One plan checks 0/1/N per round without changing the source declaration",()=>Sync(()=>{
            var fixture=new BindingFixture(TypeRef.SequenceOf(DataType.Number));var resolver=new ParameterBindingService();
            var one=resolver.CompileBinding(fixture.Reference,new Port("target",DataType.Number),fixture.Scope);
            var many=resolver.CompileBinding(fixture.Reference,new Port("target",TypeRef.SequenceOf(DataType.Number)),fixture.Scope);
            Require(one.Compatibility==BindingCompatibility.Conditional&&many.Compatibility==BindingCompatibility.Compatible,"quantity status");
            foreach(int count in new[]{0,1,3})
            {
                var values=Enumerable.Range(1,count).Select(i=>(double)i).ToArray();var snapshot=fixture.Snapshot(values);
                var result=resolver.ResolveBinding(one.Plan!,snapshot);Require(result.Success==(count==1),"ExactlyOne accepted wrong count");
                var sequence=resolver.ResolveBinding(many.Plan!,snapshot);Require(sequence.Success&&((double[])sequence.Value!).Length==count,"Many lost values");
            }
            Require(fixture.Source.Schema.Outputs.Single().Type.Shape==ValueShape.Sequence,"source shape changed");
        }));
        await Case("FT-F02-singleton","Scalar automatically supplies a singleton when target count permits",()=>Sync(()=>{
            var fixture=new BindingFixture(DataType.Number);var resolver=new ParameterBindingService();
            var target=new Port("target",TypeRef.SequenceOf(DataType.Number));
            var compiled=resolver.CompileBinding(fixture.Reference,target,fixture.Scope);
            Require(compiled.Compatibility==BindingCompatibility.Compatible,"singleton requires an extra opt-in");
            Require(((double[])resolver.ResolveBinding(compiled.Plan!,fixture.Snapshot(4d)).Value!).Single()==4,"singleton lost value");
            Require(resolver.CompileBinding(fixture.Reference,new Port("target",TypeRef.SequenceOf(DataType.Number,new CountConstraint(2))),fixture.Scope).Compatibility==BindingCompatibility.Incompatible,"minCount2 accepted singleton");
            var edit=new ParameterEditSession(new[]{target},new Dictionary<string,ParameterSetting>(),resolver,fixture.Scope);
            Require(edit.Set("target",new ReferenceSetting(fixture.Reference))&&edit.TryCommit(),"edit used a different quantity rule");
        }));
        await Case("FT-F02-fixed-index","Potentially missing fixed index is Conditional and never falls back to zero",()=>Sync(()=>{
            var fixture=new BindingFixture(TypeRef.SequenceOf(DataType.Number,new CountConstraint(0,3)));var resolver=new ParameterBindingService();
            var reference=new OutputReference("source","value",fixture.Type,selection:new BindingSelection.AtConstant(1));
            var plan=resolver.CompileBinding(reference,new Port("target",DataType.Number),fixture.Scope);
            Require(plan.Compatibility==BindingCompatibility.Conditional,"index condition hidden");
            Require(!resolver.ResolveBinding(plan.Plan!,fixture.Snapshot(new[]{8d})).Success,"index fell back to zero");
            Require(resolver.ResolveBinding(plan.Plan!,fixture.Snapshot(new[]{8d,9d})).Value is 9d,"index selected wrong element");
            Throws<ArgumentOutOfRangeException>(()=>new BindingSelection.AtConstant(-1));
            Require(resolver.CompileBinding(new("source","value",fixture.Type,selection:new BindingSelection.AtConstant(3)),new("target",DataType.Number),fixture.Scope).Compatibility==BindingCompatibility.Incompatible,"static impossible index accepted");
        }));
        await Case("FT-G02-dynamic-index","Dynamic index adds a real dependency and rejects scope/cycle/run/range faults",()=>Sync(()=>{
            var fixture=new BindingFixture(TypeRef.SequenceOf(DataType.Number));var resolver=new ParameterBindingService();
            var indexNode=new BindingNodeId("flow","index");var indexType=TypeRef.ScalarOf(ScalarKind.Int64);
            var indexSource=new BindingSource(indexNode,fixture.ExecutionScope,new ToolSchema(Array.Empty<Port>(),new[]{new Port("index",indexType)}),1);
            fixture.Admit(indexSource);
            var scope=fixture.WithSources(new[]{fixture.Source,indexSource},new[]{fixture.Source.Node,indexNode});
            var reference=new OutputReference("source","value",fixture.Type,selection:new BindingSelection.AtField(new("index","index",indexType)));
            var compilation=resolver.CompileBinding(reference,new Port("target",DataType.Number),scope);
            Require(compilation.Compatibility==BindingCompatibility.Conditional&&compilation.Plan!.Dependencies.Contains(indexNode),"index dependency missing");
            foreach(long index in new[]{-1L,0L,1L,2L})
            {
                var indexResult=fixture.Result(indexSource,new Dictionary<string,object?>{{"index",index}});
                var snapshot=fixture.Snapshot(new[]{10d,20d},indexResult);var result=resolver.ResolveBinding(compilation.Plan!,snapshot);
                Require(result.Success==(index is 0 or 1),"dynamic index boundary");
                if(result.Success)Require((double)result.Value! == (index==0?10:20),"dynamic index value");
            }
            var hidden=fixture.WithSources(new[]{fixture.Source,indexSource},new[]{fixture.Source.Node});
            Require(resolver.CompileBinding(reference,new("target",DataType.Number),hidden).Compatibility==BindingCompatibility.Unresolved,"invisible index source accepted");
            Require(!resolver.ResolveBinding(compilation.Plan!,fixture.Snapshot(new[]{10d,20d})).Success,"missing index reused an older result");
            var validIndex=fixture.Result(indexSource,new Dictionary<string,object?>{{"index",1L}});
            foreach(var invalidIndex in new[]{validIndex with{Success=false},validIndex with{Identity=validIndex.Identity with{RunId=Guid.NewGuid()}},validIndex with{Identity=validIndex.Identity with{ScopeId=Guid.NewGuid()}},validIndex with{Identity=validIndex.Identity with{RequestId=Guid.NewGuid()}}})
                Require(!resolver.ResolveBinding(compilation.Plan!,fixture.Snapshot(new[]{10d,20d},invalidIndex)).Success,"invalid index result reused");
            var cyclic=fixture.WithSources(new[]{fixture.Source,indexSource},new[]{fixture.Source.Node,indexNode},new[]{KeyValuePair.Create(indexNode,(IReadOnlyList<BindingNodeId>)new[]{fixture.Consumer})});
            Require(resolver.CompileBinding(reference,new("target",DataType.Number),cyclic).Diagnostic?.Code=="cycle","index cycle ignored");
        }));
        await Case("FT-M03-numeric-precision","Implicit integer conversion is exact; reverse conversion is explicit and range checked",()=>Sync(()=>{
            var fixture=new BindingFixture(DataType.Integer);var resolver=new ParameterBindingService();
            var plan=resolver.CompileBinding(fixture.Reference,new("target",DataType.Number),fixture.Scope);
            Require(plan.Compatibility==BindingCompatibility.Conditional,"integer precision condition hidden");
            foreach(var value in new[]{0L,9007199254740992L,9007199254740993L,long.MinValue,long.MaxValue})
            {var result=resolver.ResolveBinding(plan.Plan!,fixture.Snapshot(value));Require(result.Success==(value!=9007199254740993L&&value!=long.MaxValue),"integer roundtrip precision");}
            var numbers=new BindingFixture(DataType.Number);
            Require(resolver.CompileBinding(numbers.Reference,new("target",DataType.Integer),numbers.Scope).Compatibility==BindingCompatibility.Incompatible,"reverse conversion implicit");
            var explicitPlan=resolver.CompileBinding(numbers.Reference,new("target",DataType.Integer),numbers.Scope,BindingConversion.Float64ToInt64Checked);
            foreach(var value in new[]{0d,42d,1.5d,9223372036854775808d})Require(resolver.ResolveBinding(explicitPlan.Plan!,numbers.Snapshot(value)).Success==(value is 0d or 42d),"explicit conversion range");
        }));
        await Case("FT-G03-plan-invalidation","Compiled plans reject changed schema/document and failed or stale source results",()=>Sync(()=>{
            var fixture=new BindingFixture(DataType.Number);var resolver=new ParameterBindingService();var plan=resolver.CompileBinding(fixture.Reference,new("target",DataType.Number),fixture.Scope).Plan!;
            var original=fixture.Result(fixture.Source,new Dictionary<string,object?>{{"value",1d}});
            foreach(var invalid in new[]{original with{Success=false},original with{SchemaRevision=2},original with{SchemaFingerprint="changed"},original with{Identity=original.Identity with{RunId=Guid.NewGuid()}},original with{Identity=original.Identity with{ScopeId=Guid.NewGuid()}},original with{Identity=original.Identity with{NodeId="different"}},original with{Identity=original.Identity with{RequestId=Guid.NewGuid()}}})
                Require(!resolver.ResolveBinding(plan,fixture.SnapshotWith(invalid)).Success,"invalid result reused");
            var changed=new BindingRunSnapshot(fixture.Consumer,fixture.Identity,5,new[]{original},fixture.Expected);Require(!resolver.ResolveBinding(plan,changed).Success,"document revision ignored");
            var reopened=new BindingRunSnapshot(fixture.Consumer,fixture.Identity with{DocumentSessionId=Guid.NewGuid()},4,new[]{original},fixture.Expected);Require(!resolver.ResolveBinding(plan,reopened).Success,"document session ignored");
            Require(original.Identity.RequestId!=fixture.Identity.RequestId&&resolver.ResolveBinding(plan,fixture.SnapshotWith(original)).Success,"different legitimate source/consumer request IDs rejected");
            var frozen=fixture.SnapshotWith(original);fixture.Admit(fixture.Source);
            Require(!resolver.ResolveBinding(plan,fixture.SnapshotWith(original)).Success,"new admitted request accepted old source result");
            Require(resolver.ResolveBinding(plan,fixture.Snapshot(9d)).Value is 9d,"new expected source request rejected");
            Require(resolver.ResolveBinding(plan,frozen).Value is 1d,"frozen snapshot chased a later source admission");
        }));
        await Case("FT-A02-member-projection","Stable member identities project records without last-value type inference",()=>Sync(()=>{
            var record=TypeRef.RecordOf("measurement",new[]{new Port("distance",DataType.Number)});var fixture=new BindingFixture(record);var resolver=new ParameterBindingService();
            string[] path={"distance"};var reference=new OutputReference("source","value",record,path);path[0]="renamed";
            var compilation=resolver.CompileBinding(reference,new("target",DataType.Number),fixture.Scope);
            Require(compilation.Compatibility==BindingCompatibility.Compatible,"required non-nullable member projection was incorrectly conditional");
            Require(resolver.ResolveBinding(compilation.Plan!,fixture.Snapshot(new Dictionary<string,object?>{{"distance",5d}})).Value is 5d,"member identity changed");
            Require(resolver.CompileBinding(new("source","value",record,new[]{"missing"}),new("target",DataType.Number),fixture.Scope).Compatibility==BindingCompatibility.Unresolved,"missing member silently rebound");
            var records=new BindingFixture(TypeRef.SequenceOf(record));
            var projected=resolver.CompileBinding(new("source","value",records.Type,new[]{"distance"}),new("target",TypeRef.SequenceOf(DataType.Number)),records.Scope);
            var values=new[]{new Dictionary<string,object?>{{"distance",2d}},new Dictionary<string,object?>{{"distance",3d}}};
            Require(((double[])resolver.ResolveBinding(projected.Plan!,records.Snapshot(values)).Value!).SequenceEqual(new[]{2d,3d}),"record sequence projection changed source cardinality");
            var nullableRecord=TypeRef.RecordOf("nullable-record",new[]{new Port("v",DataType.Number,false,true)});
            var nullableRows=new BindingFixture(TypeRef.SequenceOf(nullableRecord));
            var nullableProjection=resolver.CompileBinding(new("source","value",nullableRows.Type,new[]{"v"}),new("target",TypeRef.SequenceOf(DataType.Number)),nullableRows.Scope).Plan!;
            Require(resolver.CompileBinding(new("source","value",nullableRows.Type,new[]{"v"}),new("target",TypeRef.SequenceOf(DataType.Number)),nullableRows.Scope).Compatibility==BindingCompatibility.Conditional,"optional nullable member was incorrectly unconditional");
            foreach(var fields in new[]{new Dictionary<string,object?>{{"v",null}},new Dictionary<string,object?>()})
            {
                var result=resolver.ResolveBinding(nullableProjection,nullableRows.Snapshot(new[]{fields}));
                Require(!result.Success&&result.Diagnostic is not null,"null/missing projected element escaped as an exception or value");
            }
            Require(resolver.ResolveBinding(nullableProjection,nullableRows.Snapshot(new[]{new Dictionary<string,object?>{{"v",42d}}})).Success,"nonnull projected element rejected");
            var nullableScalar=new BindingFixture(nullableRecord);
            var nullableScalarPlan=resolver.CompileBinding(new("source","value",nullableRecord,new[]{"v"}),new("target",DataType.Number,true,true),nullableScalar.Scope).Plan!;
            var nullValue=resolver.ResolveBinding(nullableScalarPlan,nullableScalar.Snapshot(new Dictionary<string,object?>{{"v",null}}));
            Require(nullValue.Success&&nullValue.Value is null,"nullable scalar target lost null semantics");
        }));
    }

    private sealed class BindingFixture
    {
        private readonly Dictionary<BindingNodeId,RunIdentity> _expected=new();
        public IReadOnlyDictionary<BindingNodeId,RunIdentity> Expected=>_expected;
        public BindingFixture(TypeRef type)
        {
            Type=type;Consumer=new("flow","consumer");ExecutionScope=Guid.NewGuid();Identity=new(Guid.NewGuid(),Guid.NewGuid(),ExecutionScope,"consumer",Guid.NewGuid());
            Source=new(new("flow","source"),ExecutionScope,new ToolSchema(Array.Empty<Port>(),new[]{new Port("value",type)}),1);
            Admit(Source);
            Reference=new("source","value",type);Scope=WithSources(new[]{Source},new[]{Source.Node});
        }
        public TypeRef Type {get;} public BindingNodeId Consumer {get;} public Guid ExecutionScope {get;} public RunIdentity Identity {get;}
        public BindingSource Source {get;} public OutputReference Reference {get;} public BindingScopeSnapshot Scope {get;}
        public void Admit(BindingSource source)=>_expected[source.Node]=Identity with{NodeId=source.Node.NodeId,ScopeId=source.ExecutionScopeId,RequestId=Guid.NewGuid()};
        public BindingScopeSnapshot WithSources(IEnumerable<BindingSource> sources,IEnumerable<BindingNodeId> visible,IEnumerable<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>? graph=null)
            =>new(Consumer,Identity.DocumentSessionId,4,sources,visible,graph??Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
        public BindingNodeResult Result(BindingSource source,IReadOnlyDictionary<string,object?> values)
            =>new(source.Node,_expected[source.Node],source.SchemaRevision,source.Schema.Fingerprint,true,new(source.Schema.Outputs,values));
        public BindingRunSnapshot Snapshot(object value,params BindingNodeResult[] extra)=>SnapshotWith(new[]{Result(Source,new Dictionary<string,object?>{{"value",value}})}.Concat(extra).ToArray());
        public BindingRunSnapshot SnapshotWith(params BindingNodeResult[] results)=>new(Consumer,Identity,4,results,_expected);
    }
}
