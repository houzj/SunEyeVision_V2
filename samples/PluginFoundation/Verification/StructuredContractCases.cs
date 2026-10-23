using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Foundation.Host;
using SunEyeVision.Tool.Kernels;

internal static partial class Program
{
    private static async Task StructuredCases()
    {
        await Case("FT-H01-borrowed-constant", "Edit configuration rejects borrowed images including declared nested resources",()=>Sync(()=>{
            var before=ImageLease.LiveBuffers;
            using(var lease=ImageLease.CreateGray8(1,1,new byte[]{3}))
            {
                var imagePort=new Port("image",DataType.Image);
                Throws<ArgumentException>(()=>new ParameterEditSession(new[]{imagePort},new Dictionary<string,ParameterSetting>{{"image",new ConstantSetting(lease.View)}}));
                var session=new ParameterEditSession(new[]{imagePort},new Dictionary<string,ParameterSetting>());
                Require(!session.Set("image",new ConstantSetting(lease.View))&&!session.TryCommit(),"borrowed image committed");
                session.Cancel();Require(session.Committed.Count==0,"borrowed image backup escaped");
                Require(session.Set("image",new ReferenceSetting(new("source","image",DataType.Image)))&&session.TryCommit(),"legal image binding rejected");
                var record=TypeRef.RecordOf("image-config",new[]{imagePort});
                var nested=new ParameterEditSession(new[]{new Port("record",record)},new Dictionary<string,ParameterSetting>());
                Require(!nested.Set("record",new ConstantSetting(new Dictionary<string,object?>{{"image",lease.View}})),"nested borrowed image committed");
                lease.Dispose();Throws<ObjectDisposedException>(()=>lease.View.CopyPixels());
                Require(session.Committed["image"] is ReferenceSetting,"binding depended on released lease");
            }
            Require(ImageLease.LiveBuffers==before,"configuration retained leaked resource");
        }));
        await Case("FT-A02-structured-freeze","Nested records and sequences freeze and reject undeclared or wrong-shape values",()=>Sync(()=>{
            var numbers=TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64),new CountConstraint(1,2));
            var row=TypeRef.RecordOf("row",new[]{new Port("samples",numbers),new Port("label",DataType.Text,false,true)});
            var rows=TypeRef.SequenceOf(row,CountConstraint.Exactly(1));
            double[] array={1,2};var fields=new Dictionary<string,object?>{{"samples",array}};
            var input=new InputValues(new[]{new Port("rows",rows)},new Dictionary<string,object?>{{"rows",new[]{fields}}});
            array[0]=9;fields.Clear();
            var frozen=(RecordValue)input.Get<SequenceValue>("rows")[0]!;
            frozen.Get<double[]>("samples")[0]=8;
            Require(frozen.Get<double[]>("samples")[0]==1&&!frozen.Contains("label"),"nested mutation escaped");
            Throws<ArgumentException>(()=>new RecordValue(row,new Dictionary<string,object?>{{"samples",new[]{1d}},{"extra",true}}));
            Throws<ArgumentException>(()=>new SequenceValue(rows,new object?[]{null}));
            Throws<ArgumentException>(()=>new InputValues(new[]{new Port("rows",rows)},new Dictionary<string,object?>{{"rows",new[]{1L}}}));
        }));
        await Case("FT-M03-geometry-values","Seven closed geometry kinds preserve canonical coordinates and freeze polygon vertices",()=>Sync(()=>{
            var frame=GeometryFixture.Frame;
            var vertices=new[]{new Point2(0,0),new Point2(3,0),new Point2(0,4)};
            var polygon=new PolygonGeometry(frame,vertices);vertices[0]=new(9,9);
            GeometryValue[] shapes={new PointGeometry(frame,new(1,2)),new LineGeometry(frame,new(0,0),new(3,4)),
                new CircleGeometry(frame,new(0,0),2),new ArcGeometry(frame,new(0,0),2,0,Math.PI),
                new OrientedRectangleGeometry(frame,new(0,0),6,8,Math.PI/2),new EllipseGeometry(frame,new(0,0),3,2,0),polygon};
            foreach(var shape in shapes)Require(ReferenceEquals(ValueRules.Freeze(new Port("shape",TypeRef.GeometryOf(shape.Kind,frame)),shape),shape),"immutable geometry rejected");
            Require(polygon.Vertices[0]==new Point2(0,0),"vertices mutated");
            Throws<ArgumentException>(()=>new Point2(double.NaN,0));
            Throws<ArgumentOutOfRangeException>(()=>new CircleGeometry(frame,new(0,0),0));
            Throws<ArgumentException>(()=>ValueRules.Freeze(new Port("p",TypeRef.GeometryOf(GeometryKind.Point,frame)),new PointGeometry(new("model:a",CoordinateDomain.Model,CoordinateUnit.Millimeter),new(0,0))));
            var otherImage=new CoordinateFrame("image:other",CoordinateDomain.Image,CoordinateUnit.Pixel);
            Throws<ArgumentException>(()=>ValueRules.Freeze(new Port("p",TypeRef.GeometryOf(GeometryKind.Point,frame)),new PointGeometry(otherImage,new(0,0))));
        }));
        await Case("FT-M03-geometry-call","Pure geometry fixture runs Definition/Session/Invocation with shared production math",async()=>{
            await using var host=new NodeHost<TestSettings,MapOutput>(new GeometryFixture());
            using var result=await host.Start(new(),new Dictionary<string,object?>{
                {"start",new PointGeometry(GeometryFixture.Frame,new(0,0))},{"end",new PointGeometry(GeometryFixture.Frame,new(3,4))}},Id()).Completion;
            Require(result.Status==RunStatus.Completed,"geometry invocation failed");
            var record=result.Values!.Get<RecordValue>("measurement");
            Require(record.Get<double>("distance")==5,"shared kernel distance");
            Require(Math.Abs(record.Get<double>("angleRadians")-Math.Atan2(4,3))<1e-14,"degree/radian boundary wrong");
            var line=record.Get<LineGeometry>("line");Require(line.End==new Point2(3,4),"geometry projection changed");
        });
        await Case("FT-A03-nested-image","Declared nested images are borrowed, retained and released through real invocation",async()=>{
            var before=ImageLease.LiveBuffers;
            foreach(var raw in new[]{false,true})
            {
                using(var input=ImageLease.CreateGray8(1,1,new byte[]{7}))
                {
                    await using var host=new NodeHost<TestSettings,MapOutput>(new NestedImageFixture(raw));
                    using var result=await host.Start(new(),new Dictionary<string,object?>{{"record",new Dictionary<string,object?>{{"image",input.View}}}},Id()).Completion;
                    Require(result.Status==(raw?RunStatus.Failed:RunStatus.Completed),"nested ownership bypass");
                    if(!raw)Require(result.Values!.Get<RecordValue>("record").Get<IReadOnlyImage>("image").CopyPixels()[0]==7,"nested image lost");
                }
                Require(ImageLease.LiveBuffers==before,"nested image leaked");
            }
        });
    }
}

internal sealed class GeometryFixture:IToolDefinition<TestSettings,MapOutput>
{
    internal static readonly CoordinateFrame Frame=new("image:fixture",CoordinateDomain.Image,CoordinateUnit.Pixel);
    private static readonly TypeRef Output=TypeRef.RecordOf("point-measurement",new[]{new Port("distance",DataType.Number),new Port("angleRadians",DataType.Number),new Port("line",TypeRef.GeometryOf(GeometryKind.Line,Frame))});
    public string ToolId=>"ContractFixture.PureGeometry";
    public ToolSchema Describe(TestSettings settings)=>new(new[]{new Port("start",TypeRef.GeometryOf(GeometryKind.Point,Frame)),new Port("end",TypeRef.GeometryOf(GeometryKind.Point,Frame))},new[]{new Port("measurement",Output)});
    public IReadOnlyList<Diagnostic> Validate(TestSettings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities(TestSettings settings)=>ToolCapabilities.None;
    public IToolSession<TestSettings,MapOutput> CreateSession()=>new Session();
    private sealed class Session:IToolSession<TestSettings,MapOutput>
    {
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        public ValueTask<ToolOutcome<MapOutput>> ExecuteAsync(ToolInvocation<TestSettings> invocation)
        {
            var a=invocation.Inputs.Get<PointGeometry>("start").Point;var b=invocation.Inputs.Get<PointGeometry>("end").Point;
            var measured=PointPairMath.Measure(a.X,a.Y,b.X,b.Y);
            return ValueTask.FromResult(ToolOutcome<MapOutput>.Completed(new(new Dictionary<string,object?>{{"measurement",new Dictionary<string,object?>{
                {"distance",measured.Distance},{"angleRadians",measured.AngleDeg*Math.PI/180},{"line",new LineGeometry(Frame,a,b)}}}})));
        }
    }
}

internal sealed class NestedImageFixture(bool raw):IToolDefinition<TestSettings,MapOutput>
{
    private static readonly TypeRef Record=TypeRef.RecordOf("image-record",new[]{new Port("image",DataType.Image)});
    public string ToolId=>"ContractFixture.NestedImage";
    public ToolSchema Describe(TestSettings settings)=>new(new[]{new Port("record",Record)},new[]{new Port("record",Record)});
    public IReadOnlyList<Diagnostic> Validate(TestSettings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities(TestSettings settings)=>ToolCapabilities.None;
    public IToolSession<TestSettings,MapOutput> CreateSession()=>new Session(raw);
    private sealed class Session(bool raw):IToolSession<TestSettings,MapOutput>
    {
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        public ValueTask<ToolOutcome<MapOutput>> ExecuteAsync(ToolInvocation<TestSettings> invocation)
        {
            var input=invocation.Inputs.Get<RecordValue>("record").Get<IReadOnlyImage>("image");
            var image=raw?input:invocation.Context.Resources.RetainInput(input);
            return ValueTask.FromResult(ToolOutcome<MapOutput>.Completed(new(new Dictionary<string,object?>{{"record",new Dictionary<string,object?>{{"image",image}}}})));
        }
    }
}
