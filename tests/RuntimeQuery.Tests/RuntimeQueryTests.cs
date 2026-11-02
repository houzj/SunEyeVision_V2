using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Xunit;

public sealed class RuntimeQueryTests
{
    private static readonly TypeRef Number=TypeRef.ScalarOf(ScalarKind.Int64);
    private static AvailableDataSource Source(string scope,string port,BindingSelection? selection=null) => new()
    { Binding=new ReferenceSetting(new OutputReference("node",port,Number,selection:selection,scopeId:scope)),DisplayName="显示" };
    [Fact] public void Identity_is_binding_not_caption_or_dotted_path()
    {
        var a=Source("one","port.with.dot");var b=Source("one","port.with.dot");b.DisplayName="改名";
        Assert.Equal(a,b);Assert.Equal(a.GetHashCode(),b.GetHashCode());Assert.NotEqual(a,Source("two","port.with.dot"));
        Assert.NotEqual(a,Source("one","port"));Assert.Equal("port.with.dot",a.PropertyName);
        Assert.Equal("port.with.dot",BindingReferenceSerializer.Deserialize(a.GetBindingPath()).PortId);
        Assert.Equal(a,a.WithConnectionOwnership("edge"));
        Assert.Equal("connection:edge",Assert.IsType<ReferenceSetting>(a.WithConnectionOwnership("edge").Binding).OriginId);
    }
    [Fact] public void Variable_identity_and_invalid_binding_are_explicit()
    {
        var a=new AvailableDataSource { Binding=new VariableSetting(new VariableReference("solution","id",Number)) };
        Assert.True(a.IsValid);Assert.Empty(a.SourceNodeId);Assert.Equal("id",BindingReferenceSerializer.DeserializeVariable(a.GetBindingPath()).VariableId);
        Assert.NotEqual(a,new AvailableDataSource { Binding=new VariableSetting(new VariableReference("other","id",Number)) });
        Assert.Throws<ArgumentException>(()=>new AvailableDataSource { Binding=new ConstantSetting(1) });
        Assert.Throws<ArgumentException>(()=>new AvailableDataSource { Binding=null! });
        Assert.Throws<ArgumentException>(()=>new AvailableDataSource { Binding=new ReferenceSetting(new OutputReference("node","port",Number)) });
        Assert.Throws<ArgumentException>(()=>new AvailableDataSource { Binding=new ReferenceSetting(null!) });
        Assert.Throws<InvalidOperationException>(()=>a.CurrentValue=new object());
    }
    [Fact] public void Sequence_selection_and_member_path_are_identity()
    {
        var record=TypeRef.RecordOf("record",new[]{new Port("x",Number),new Port("y",Number)});
        var list=TypeRef.SequenceOf(record);
        AvailableDataSource Make(string member,int index)=>new(){Binding=new ReferenceSetting(new OutputReference("node","port",list,new[]{member},new BindingSelection.AtConstant(index),"scope"))};
        Assert.NotEqual(Make("x",0),Make("x",1));Assert.NotEqual(Make("x",0),Make("y",0));Assert.Equal(Make("x",1),Make("x",1));
        var compound=new AvailableDataSource { Binding=new ReferenceSetting(new OutputReference("node","port.x",Number,scopeId:"scope")) };
        var parent=TypeRef.RecordOf("r",new[]{new Port("x",Number)});
        var member=new AvailableDataSource { Binding=new ReferenceSetting(new OutputReference("node","port",parent,new[]{"x"},scopeId:"scope")) };
        Assert.NotEqual(compound,member);Assert.Equal(parent,Assert.IsType<ReferenceSetting>(member.Binding).Reference.Type);
    }
    [Fact] public void Query_uses_scoped_schema_and_declared_display_without_runtime_result()
    {
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("port.with.dot",Number)},outputDisplay:new[]{new OutputDisplayNode("port.with.dot","中文")});
        var provider=new Provider(schema);var query=new DataSourceQueryService(provider,provider){WorkflowId="scope"};
        var source=Assert.Single(query.GetAvailableDataSources("target"));
        var binding=Assert.IsType<ReferenceSetting>(source.Binding);
        Assert.Equal("scope",binding.Reference.ScopeId);Assert.Equal("port.with.dot",binding.Reference.PortId);Assert.Equal("中文",source.DisplayName);
        Assert.Null(source.CurrentValue);Assert.True(source.IsDesignTime);Assert.False(source.HasExecuted);
        Assert.Single(query.GetAvailableDataSources("target",typeof(double)));
        provider.Schema=null;Assert.Throws<InvalidOperationException>(()=>query.GetAvailableDataSources("target"));
        provider.Exists=false;Assert.Empty(query.GetAvailableDataSources("target"));
    }
    [Fact] public void Scalar_preview_reads_exact_port_under_reader_and_does_not_hold_resources()
    {
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("port",Number),new Port("port.with.dot",Number)});
        var provider=new Provider(schema);var query=new DataSourceQueryService(provider,provider){WorkflowId="scope"};
        var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");
        var resource=new Release();
        using var result=DeclaredOutputSnapshot.Capture(schema,0,identity,"node",new ToolOutcome(true,new Output(),BusinessJudgment.Pass,Array.Empty<Diagnostic>()),resource,0,sourceWorkflowId:"scope");
        query.SetSootReader(_=>result);
        var sources=query.GetNodeOutputProperties("node");
        Assert.Equal(11L,sources.Single(item=>((ReferenceSetting)item.Binding).Reference.PortId=="port").CurrentValue);
        Assert.Equal(22L,sources.Single(item=>((ReferenceSetting)item.Binding).Reference.PortId=="port.with.dot").CurrentValue);
        result.Dispose();Assert.Equal(1,resource.Count);
    }
    private sealed class Release:IDisposable { public int Count; public void Dispose()=>Count++; }
    private sealed class Output:IToolOutput
    { public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>{{"port",11L},{"port.with.dot",22L}}; }
    private sealed class Provider(ToolSchema schema):IWorkflowConnectionProvider,INodeInfoProvider
    {
        public ToolSchema? Schema=schema;
        public bool Exists=true;
        public List<string> GetParentNodeIds(string nodeId)=>new(){"node"};
        public List<string> GetChildNodeIds(string nodeId)=>new();
        public List<string> GetAllNodeIds()=>new(){"node","target"};
        public string GetNodeName(string id)=>id;
        public string GetNodeType(string id)=>"test";
        public string? GetNodeIcon(string id)=>null;
        public bool NodeExists(string id)=>Exists;
        public ToolSchema? GetSchema(string id)=>id=="node"?Schema:null;
    }
}
