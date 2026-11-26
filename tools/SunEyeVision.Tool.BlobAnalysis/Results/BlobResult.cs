using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Contracts;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using System.Xml;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

using static SunEyeVision.Tool.BlobAnalysis.Runtime.BlobParameterDefinition;
using static SunEyeVision.Plugin.Contracts.DeclaredParameterSchema;


/// <summary>Declared Blob table metadata. Runtime appearance belongs only to BlobResultDisplay fields.</summary>
public sealed class BlobDisplayDefinition
{
    public static BlobDisplayDefinition Instance { get; } = Load();
    public ResultProjectionDescriptor Table { get; }
    /// <summary>结果页显示节点；空表示该工具不声明结果树。</summary>
    public IReadOnlyList<OutputDisplayNode> OutputNodes { get; }

    private BlobDisplayDefinition(XElement root, ToolSchema schema)
    {
        Shape(root,["schemaVersion"],["Table","OutputNodes"]);
        if(root.Name!="BlobDisplay"||Required(root,"schemaVersion")!="1")throw new InvalidDataException("Unsupported Blob display metadata.");
        XElement Section(string name)
        {
            var elements=root.Elements(name).ToArray();
            return elements.Length==1?elements[0]:throw new InvalidDataException("Exactly one Blob display section is required: "+name);
        }
        var table=Section("Table");Shape(table,["port","identity"],["Column"]);
        var port=Required(table,"port");
        Table=new(ResultProjectionKind.StandardTable,ResultCardinality.Many,table.Elements().Select(item=>
        {
            Shape(item,["key","label","labelResource","member","numericFormat","unavailable"],[]);
            if(item.Attribute("unavailable") is {} unavailable)
            {
                if(string.IsNullOrWhiteSpace(unavailable.Value)||item.Attribute("member") is not null||item.Attribute("numericFormat") is not null)
                    throw new InvalidDataException("An unavailable display column cannot declare an output member or numeric format.");
                return new ResultColumnDescriptor(Required(item,"key"),Required(item,"label"),null,
                    UnavailableReason:unavailable.Value,DisplayNameResourceKey:(string?)item.Attribute("labelResource"));
            }
            var source=new ResultFieldPath(port,Required(item,"member").Split('/'));
            var useSinglePrecision=(string?)item.Attribute("numericFormat") switch
            {
                null=>false,
                "float32"=>true,
                _=>throw new InvalidDataException("Unsupported Blob numeric display format.")
            };
            return new ResultColumnDescriptor(Required(item,"key"),Required(item,"label"),source,
                Geometry:new(port,["rotated-bounds"]),GeometrySource:new(port,["region"]),UseSinglePrecision:useSinglePrecision,
                DisplayNameResourceKey:(string?)item.Attribute("labelResource"));
        }),new(port),Required(table,"identity"));
        OutputNodes=OutputDisplayDeclaration.Load(root.Element("OutputNodes"),ResolveId);
        var errors=Table.Validate(schema);
        if(errors.Count!=0||Table.Columns.Count==0)throw new InvalidDataException("Invalid Blob table mapping: "+string.Join("; ",errors.Select(x=>x.Message)));
    }

    /// <summary>把 XML 里的 field:序号 简写展开成完整端口身份。</summary>
    private static string ResolveId(string id)
        =>id.StartsWith("field:",StringComparison.Ordinal)&&int.TryParse(id.AsSpan(6),out var number)?BlobSchema.Field(number):id;
    private static BlobDisplayDefinition Load()
    {
        using var stream=typeof(BlobTool).Assembly.GetManifestResourceStream("BlobAnalysis.BlobDisplay.xml")
            ??throw new InvalidDataException("BlobDisplay.xml is missing from the Runtime component.");
        return Read(stream,new BlobTool().Describe());
    }
    public static BlobDisplayDefinition Read(Stream stream,ToolSchema schema)
    {
        ArgumentNullException.ThrowIfNull(stream);ArgumentNullException.ThrowIfNull(schema);
        try
        {
            using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});
            return new(XDocument.Load(reader).Root??throw new InvalidDataException("Empty Blob display metadata."),schema);
        }
        catch(Exception e)when(e is XmlException or ArgumentException)
        {throw new InvalidDataException("Invalid Blob display metadata.",e);}
    }
}

public readonly record struct BlobBounds(double X, double Y, double Width, double Height);
public readonly record struct BlobOrientedBounds(double CenterX,double CenterY,double Width,double Height,double AngleDegrees);
public sealed record BlobBoundaryPath(long Id, long? ParentId, string Role, IReadOnlyList<Point2> Points);
public sealed record BlobMeasurement(long ElementId, int Label, double Area, double Perimeter,
    double CentroidX, double CentroidY, double Circularity, double Rectangularity, BlobBounds Bounds,
    BlobOrientedBounds OrientedBounds, IReadOnlyList<Point2> RotatedBounds, IReadOnlyList<BlobBoundaryPath> Boundary, IReadOnlyRegion Region, bool JudgmentPassed,
    double? MinGrayValue = null, double? MaxGrayValue = null, double? GrayContrast = null, double? BlobAngleDegrees = null,
    IReadOnlyList<Point2>? UnorderedContour = null, BlobOrientedBounds? InnerRectangle = null);

/// <summary>One final ordered result set. Independent resource readers can outlive this output owner.</summary>
public sealed class BlobOutput : IToolOutput, IDisposable, IToolResultGeometryOutput, IToolResultRecordOutput,
    IToolResultOutlineOutput
{
    private readonly IDisposable[] _owned;
    private readonly Dictionary<string,object?> _values;
    private int _disposed;
    public IEnumerable<string> DescribeResultRecords()
    {
        foreach (var item in Measurements)
            yield return $"Blob信息{item.Label}:面积{(float)item.Area},周长{(float)item.Perimeter},质心X{(float)item.CentroidX},质心Y{(float)item.CentroidY},角度{(float)item.OrientedBounds.AngleDegrees}";
    }
    internal BlobOutput(BlobSettings settings,BlobThresholdExecutionSnapshot threshold,IEnumerable<Diagnostic> diagnostics,IEnumerable<BlobSelectionEntry> selection, IEnumerable<BlobMeasurement> measurements, IReadOnlyImage? blobImage, IReadOnlyImage? binary,
        IReadOnlyImage? maskImage, IReadOnlyRegion region, IReadOnlyRegion ok, IReadOnlyRegion ng, double whiteRatio,
        double[] histogram, double[] means, bool isPass, IEnumerable<string> messages, IEnumerable<IDisposable> owned,ResultGeometrySnapshot resultGeometry,
        SequenceValue maskPolygons, bool publishContour, CancellationToken cancellation, long moduleStarted)
    {
        ResultGeometry=resultGeometry;
        ResultOutline=BuildOutline(measurements,resultGeometry.SourceFrame,publishContour);
        MaskPolygons=maskPolygons;
        ThresholdExecution=threshold;ExecutionDiagnostics=Array.AsReadOnly(diagnostics.ToArray());
        SelectionExecution=Array.AsReadOnly(selection.ToArray());
        ThresholdMinUsed=threshold.Lower;ThresholdMaxUsed=threshold.Upper;TypeUsed=threshold.Polarity;
        SegmentationModeUsed=settings.SegmentationMode.ToString();
        Measurements = Array.AsReadOnly(measurements.ToArray()); BlobImage = blobImage; BinaryImage = binary;
        MaskImage = maskImage;
        ResultProjection = BlobResultProjection.Create(Measurements,binary,blobImage,resultGeometry,maskPolygons,maskImage,
            publishContour,cancellation);
        Region = region; OkRegion = ok; NgRegion = ng; WhitePixelRatio = whiteRatio;
        Histogram = Array.AsReadOnly(histogram.ToArray()); ChannelMeans = Array.AsReadOnly(means.ToArray());
        IsPass = isPass; JudgmentMessages = Array.AsReadOnly(messages.ToArray()); _owned = owned.Distinct<IDisposable>(ReferenceEqualityComparer.Instance).ToArray();
        _values = BuildValues(cancellation);
        _values.Add(BlobSchema.ModuleRunTimePort,(double)(float)Stopwatch.GetElapsedTime(moduleStarted).TotalMilliseconds);
        _ = new InputValues(BlobSchema.Outputs, _values);
    }
    public ResultGeometrySnapshot ResultGeometry {get;}
    /// <summary>系统级轮廓点（原件 CONTOURPOINTS 的统一点集形态）。</summary>
    public ResultOutlineSet? ResultOutline {get;}
    public SequenceValue MaskPolygons {get;}
    private static ResultOutlineSet? BuildOutline(IEnumerable<BlobMeasurement> measurements, ImageFrameAddress frame,
        bool publishContour)
    {
        if(!publishContour)return null;
        var points=new List<Point2>();var indices=new List<int>();var scores=new List<float>();
        var blobIndex=0;
        foreach(var blob in measurements)
        {
            foreach(var point in BlobSchema.OutlinePoints(blob))
            {
                points.Add(point);indices.Add(blobIndex);
                scores.Add(blob.JudgmentPassed?0f:2f);
            }
            blobIndex++;
        }
        return points.Count==0?null:new ResultOutlineSet(frame,points,indices,scores);
    }
    public IReadOnlyList<BlobMeasurement> Measurements { get; }
    /// <summary>VM BlobFindResult 的中立投影，Unavailable 中的成员没有本方等价资源。</summary>
    public BlobResultProjection ResultProjection { get; }
    public BlobThresholdExecutionSnapshot ThresholdExecution {get;}
    public IReadOnlyList<Diagnostic> ExecutionDiagnostics {get;}
    public IReadOnlyList<BlobSelectionEntry> SelectionExecution {get;}
    public int? ThresholdMinUsed { get; }
    public int? ThresholdMaxUsed { get; }
    public string TypeUsed { get; }
    public string SegmentationModeUsed { get; }
    public IReadOnlyImage? BlobImage { get; }
    public IReadOnlyImage? BinaryImage { get; }
    public IReadOnlyImage? MaskImage { get; }
    public IReadOnlyRegion Region { get; }
    public IReadOnlyRegion OkRegion { get; }
    public IReadOnlyRegion NgRegion { get; }
    public double WhitePixelRatio { get; }
    public IReadOnlyList<double> Histogram { get; }
    public IReadOnlyList<double> ChannelMeans { get; }
    public bool IsPass { get; }
    public IReadOnlyList<string> JudgmentMessages { get; }
    public int Count => Measurements.Count;
    public double TotalArea => ResultProjection.TotalArea;
    public double AverageArea => Count == 0 ? 0 : TotalArea / Count;
    public double MinimumArea => Count == 0 ? 0 : Measurements.Min(x => x.Area);
    public double MaximumArea => Count == 0 ? 0 : Measurements.Max(x => x.Area);
    public double AverageCircularity => Count == 0 ? 0 : Measurements.Average(x => x.Circularity);
    public double AverageRectangularity => Count == 0 ? 0 : Measurements.Average(x => x.Rectangularity);
    private static RecordValue? ImageValue(IReadOnlyImage? image,TypeRef declaration)
    {
        if(image is null)return null;
        ResourceContractRules.ValidateImage(image);
        if(image.Layout.Format!=PixelLayout.Gray8)throw new NotSupportedException("Blob图像四成员发布仅支持真实Gray8格式。");
        return new(declaration,new Dictionary<string,object?>{
            ["data"]=image,["width"]=(long)image.Width,["height"]=(long)image.Height,["pixel-format"]=17301505L});
    }
    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return new ReadOnlyDictionary<string,object?>(_values.ToDictionary(
            pair => pair.Key, pair => pair.Value is Array array ? array.Clone() : pair.Value, StringComparer.Ordinal));
    }

    private Dictionary<string,object?> BuildValues(CancellationToken cancellation)
    {
        static double[]? NumberSequence(IReadOnlyList<float>? values) => values?.Select(value => (double)value).ToArray();
        static SequenceValue? NumericSequence(IReadOnlyList<float>? values)
            => values is null ? null : new SequenceValue(TypeRef.SequenceOf(DataType.Number), values.Select(value => (object?)(double)value));
        return new Dictionary<string, object?> {
            [BlobSchema.BlobsPort] = new SequenceValue(BlobSchema.BlobsType, Measurements.Select(measurement =>
            {
                cancellation.ThrowIfCancellationRequested();
                return BlobSchema.Freeze(measurement, ResultProjection.ContourNum.HasValue, cancellation);
            })),
            [BlobSchema.Field(5)] = ImageValue(BlobImage,BlobSchema.BlobImageType), [BlobSchema.Field(6)] = ImageValue(BinaryImage,BlobSchema.BinaryImageType),
            [BlobSchema.MaskImagePort] = ImageValue(MaskImage,BlobSchema.MaskImageType),
            [BlobSchema.MaskPolygonsPort] = MaskPolygons,
            [BlobSchema.MaskPolygonStringsPort] = ResultProjection.BlindPolygonString?.ToArray(),
            [BlobSchema.DetectionRectanglesPort] = ResultProjection.ROI is { } rectangles
                ? new SequenceValue(BlobSchema.OrientedRectanglesType, rectangles.Select(BlobSchema.FreezeBounds)) : null,
            [BlobSchema.DetectionAnnuliPort] = ResultProjection.ROIAnnulus is { } annuli
                ? new SequenceValue(TypeRef.SequenceOf(BlobRoiInheritance.AnnulusType), annuli.Select(BlobSchema.FreezeDetectionAnnulus)) : null,
            [BlobSchema.ContourPointsPort] = ResultProjection.ContourNum is int contourCount
                ? new RecordValue(BlobSchema.ContourPointsType, new Dictionary<string, object?>
                    { ["count"] = (long)contourCount, ["info"] = ResultProjection.ContourInfo }) : null,
            [BlobSchema.InnerRectanglesPort] = new SequenceValue(BlobSchema.OrientedRectanglesType, ResultProjection.MaxAreaInnerRect.Select(bounds => (object?)BlobSchema.FreezeBounds(bounds))),
            [BlobSchema.OuterRectanglesPort] = new SequenceValue(BlobSchema.OrientedRectanglesType, ResultProjection.MinBoudingRect.Select(bounds => (object?)BlobSchema.FreezeBounds(bounds))),
            [BlobSchema.LongAxesPort] = NumberSequence(ResultProjection.LongAxis),
            [BlobSchema.ShortAxesPort] = NumberSequence(ResultProjection.ShortAxis),
            [BlobSchema.ModuleStatusPort] = (long)ResultProjection.ModuStatus,
            [BlobSchema.BlobStatusesPort] = ResultProjection.BlobStatus.Select(status => (long)status).ToArray(),
            [BlobSchema.MinGrayValuesPort] = NumericSequence(ResultProjection.MinGrayValue),
            [BlobSchema.MaxGrayValuesPort] = NumericSequence(ResultProjection.MaxGrayValue),
            [BlobSchema.GrayContrastsPort] = NumericSequence(ResultProjection.GrayContrast),
            [BlobSchema.PrincipalAnglesPort] = NumericSequence(ResultProjection.BlobAngle),
            [BlobSchema.Field(8)] = ThresholdMinUsed is int lower ? (object)(long)lower : null, [BlobSchema.Field(9)] = ThresholdMaxUsed is int upper ? (object)(long)upper : null, [BlobSchema.Field(10)] = TypeUsed, [BlobSchema.Field(102)] = SegmentationModeUsed,
            [BlobSchema.Field(11)] = (long)ResultProjection.BlobNum, [BlobSchema.Field(12)] = TotalArea,
            [BlobSchema.Field(13)] = AverageArea, [BlobSchema.Field(16)] = WhitePixelRatio,
            [BlobSchema.Field(30)] = MinimumArea, [BlobSchema.Field(31)] = MaximumArea,
            [BlobSchema.Field(14)] = AverageCircularity,
            [BlobSchema.Field(15)] = AverageRectangularity,
            [BlobSchema.Field(17)] = Histogram.ToArray(), [BlobSchema.Field(18)] = ChannelMeans.ToArray(),
            [BlobSchema.Field(19)] = NumberSequence(ResultProjection.Area), [BlobSchema.Field(20)] = NumberSequence(ResultProjection.Perimeter),
            [BlobSchema.Field(21)] = NumberSequence(ResultProjection.CentroidPoint.Select(point => (float)point.X).ToArray()),
            [BlobSchema.Field(22)] = NumberSequence(ResultProjection.CentroidPoint.Select(point => (float)point.Y).ToArray()),
            [BlobSchema.CentroidPointsPort] = new SequenceValue(BlobSchema.CentroidPointsType,ResultProjection.CentroidPoint.Select(BlobSchema.FreezePoint)),
            [BlobSchema.Field(23)] = NumberSequence(ResultProjection.Circularity), [BlobSchema.Field(24)] = NumberSequence(ResultProjection.Rectangularity),
            [BlobSchema.Field(25)] = NumberSequence(ResultProjection.BlobRect.Select(bounds => (float)bounds.Width).ToArray()),
            [BlobSchema.Field(26)] = NumberSequence(ResultProjection.BlobRect.Select(bounds => (float)bounds.Height).ToArray()),
            [BlobSchema.AxisAlignedRectanglesPort] = new SequenceValue(BlobSchema.AxisAlignedRectanglesType,
                ResultProjection.BlobRect.Select(BlobSchema.FreezeAxisAlignedBounds)),
            [BlobSchema.RectangleTopLeftXPort] = NumberSequence(ResultProjection.BlobRectUpLeftX),
            [BlobSchema.RectangleTopLeftYPort] = NumberSequence(ResultProjection.BlobRectUpLeftY),
            [BlobSchema.RectangleBottomRightXPort] = NumberSequence(ResultProjection.BlobRectBottomRightX),
            [BlobSchema.RectangleBottomRightYPort] = NumberSequence(ResultProjection.BlobRectBottomRightY),
            [BlobSchema.Field(27)] = new SequenceValue(TypeRef.SequenceOf((TypeRef)DataType.Text), Measurements.Select(x=>(object?)$"{(int)x.Bounds.Width}×{(int)x.Bounds.Height}")),
            [BlobSchema.RegionPort] = Region
        };
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var faults = new List<Exception>();
        foreach (var resource in _owned) try { resource.Dispose(); } catch (Exception e) { faults.Add(e); }
        if (faults.Count != 0) throw new AggregateException(faults);
    }
}

/// <summary>
/// 原件「模块结果树」的显示声明：项名、顺序、分组与成员路径都来自这里，渲染代码不再起名。
/// 证据：docs/collaboration/evidence/Blob模块结果树原件结构-20261112.json（原厂截图 6 张）。
/// 原件把记录型结果按成员转置成列（每成员一行、值为全部 Blob 的该成员、分号拼接），
/// 因此「质心点」「最小外接矩形」「Blob矩形框」等组合项由成员行承载，不再展开元素索引。
/// </summary>
internal static class BlobOutputDisplay
{
    /// <summary>结果树完全来自 BlobDisplay.xml 的 OutputNodes 声明，代码里不再逐项罗列。</summary>
    internal static IReadOnlyList<OutputDisplayNode> Create() => BlobDisplayDefinition.Instance.OutputNodes;
}

public static class BlobResultDisplay
{
    public const string Graphic="display.blob";
    public const string InnerRectangle="display.inner-rectangle";
    public const string ReferencePoint="display.reference-point", RunPoint="display.run-point";
    public const string Text="display.text";
    public const string Outline="display.outline";
    public const string Inspection="display.inspection",Mask="display.mask",Annulus="display.annulus";
    public static string Field(string group,string role)=>group+"."+role;
    private sealed record Definition(Port Port,FieldPresentation Presentation,object Value);
    private static readonly IReadOnlyList<Definition> Definitions=Build();
    public static ToolSchema Schema {get;}=new(Definitions.Select(item=>item.Port),Array.Empty<Port>(),new ToolPresentation(Definitions.Select(item=>item.Presentation)));
    public static ResultDisplayDescriptor Descriptor {get;}=new(new(BlobSchema.BlobsPort),new(BlobSchema.BlobsPort,new[]{"element-id"}),
        new(BlobSchema.BlobsPort,new[]{"oriented-bounds"}),new(BlobSchema.BlobsPort,new[]{"region"}),
        new(BlobSchema.BlobsPort,new[]{"area"}),new(BlobSchema.BlobsPort,new[]{"judgment-passed"}),
        innerRectangle:new(BlobSchema.BlobsPort,new[]{"inner-rectangle"}), selectedRectangleColor:new(255,255,0),
        centroid:new(BlobSchema.BlobsPort,new[]{"centroid"}));
    public static InputValues Defaults()=>new(Schema.Inputs,Definitions.ToDictionary(item=>item.Port.Id,item=>(object?)item.Value,StringComparer.Ordinal));
    private static IReadOnlyList<Definition> Build()
    {
        var result=new List<Definition>();
        var displayOrder=BlobParameterDefinition.Instance.SettingsSchema.Presentation!.Fields.Max(field=>field.Order)+1;
        void Add(string id,string label,string section,TypeRef type,object value,double? minimum=null,double? maximum=null,double? increment=null,string? dialog=null,bool primary=false,int? decimals=2)
        {
            var modes=id==Field(Text,"content")?FieldEditModes.Constant|FieldEditModes.DisplayExpression:
                id==Field(Text,"x")||id==Field(Text,"y")?FieldEditModes.Constant|FieldEditModes.Reference:FieldEditModes.Constant;
            var port=new Port(id,type,EditModes:modes);
            result.Add(new(port,new(id,label,section,displayOrder+result.Count,type.Shape==ValueShape.Record?FieldEditorKind.Record : FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.FromPort(type, type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar)){Layout=new("结果显示",section,
                Minimum:minimum,Maximum:maximum,Increment:increment??1,DecimalPlaces:type==DataType.Number?decimals:0,
                DialogGroup:dialog,DialogTitle:dialog is null?null:dialog switch
                {
                    InnerRectangle=>"最大内接矩形",Inspection=>"检测区域",Mask=>"屏蔽区多边形",Annulus=>"扇环检测区域",
                    ReferencePoint=>"基准点",RunPoint=>"运行点",_=>"Blob"
                },DialogPrimary:primary)},value));
        }
        var ok=PresentationDeclarations.Color(102,255,0);var ng=PresentationDeclarations.Color(255,0,0);
        void GraphicFields(string group,string label,RecordValue color,double opacity=1,double width=2)
        {
            Add(Field(group,"visible"),label,"图像显示",DataType.Boolean,true,dialog:group,primary:true);
            Add(Field(group,"ok"),"OK颜色","图像显示",PresentationDeclarations.Rgba,color,dialog:group);
            Add(Field(group,"ng"),"NG颜色","图像显示",PresentationDeclarations.Rgba,ng,dialog:group);
            Add(Field(group,"opacity"),"透明度","图像显示",DataType.Number,opacity,0,1,.01,group);
            Add(Field(group,"width"),"线宽","图像显示",DataType.Number,width,1,10,1,group);
        }
        GraphicFields(Graphic,"Blob",ok);
        GraphicFields(InnerRectangle,"最大内接矩形",ok);
        GraphicFields(Inspection,"检测区域",PresentationDeclarations.Color(0,0,255));
        Add(Field(Outline,"visible"),"轮廓点","图像显示",DataType.Boolean,true);
        GraphicFields(ReferencePoint,"基准点",ok,width:1);
        GraphicFields(RunPoint,"运行点",ng,width:1);
        Add(Field(Text,"visible"),"文本","图像显示",DataType.Boolean,true);
        GraphicFields(Mask,"屏蔽区多边形",PresentationDeclarations.Color(0,0,255),.5);
        GraphicFields(Annulus,"扇环检测区域",PresentationDeclarations.Color(0,0,255));
        Add(Field(Text,"content"),"内容","文本显示",DataType.Text,"Blob面积:{面积}");
        Add(Field(Text,"ok"),"OK颜色","文本显示",PresentationDeclarations.Rgba,ok);
        Add(Field(Text,"ng"),"NG颜色","文本显示",PresentationDeclarations.Rgba,ng);
        Add(Field(Text,"size"),"字号","文本显示",DataType.Number,10d,6,72,1);
        Add(Field(Text,"opacity"),"透明度","文本显示",DataType.Number,1d,0,1,.1);
        Add(Field(Text,"x"),"位置X","文本显示",DataType.Number,0d,decimals:null);
        Add(Field(Text,"y"),"位置Y","文本显示",DataType.Number,0d,decimals:null);
        return result.AsReadOnly();
    }
    public static ResultDisplayAppearance Capture(FieldSettings fields)
    {
        T Read<T>(string id)=>fields.Get<T>(id);
        double Number(string group,string role)
        {
            var id=Field(group,role);var value=Read<double>(id);var layout=Definitions.Single(item=>item.Port.Id==id).Presentation.Layout!;
            if(!double.IsFinite(value)||value<layout.Minimum||value>layout.Maximum||layout.DecimalPlaces is {} digits&&Math.Round(value,digits,MidpointRounding.ToEven)!=value)
                throw new ArgumentException("显示数值超出范围或精度："+id);
            return value;
        }
        ResultDisplayColor Color(string group,string role)
        {var value=Read<RecordValue>(Field(group,role));return new(checked((byte)value.Get<long>("r")),checked((byte)value.Get<long>("g")),checked((byte)value.Get<long>("b")),checked((byte)value.Get<long>("a")));}
        var expression=fields.Read(Field(Text,"content")) switch {DisplayExpressionSetting configured=>ParseContent(configured.Text,configured.Slots),ConstantSetting {Value:string raw}=>ParseContent(raw),_=>throw new ArgumentException("文本内容设置无效。")};
        var content=expression.Text;
        ParameterSetting Position(string role){var setting=fields.Read(Field(Text,role));if(setting is ReferenceSetting reference){ValidateDisplayReference(reference.Reference,true);return reference;}if(setting is ConstantSetting {Value:double number}&&double.IsFinite(number))return setting;throw new ArgumentException("文本位置必须是有限数或已声明数字来源。");}
        var x=Position("x");var y=Position("y");
        ResultGraphicAppearance Style(string group)=>new(Read<bool>(Field(group,"visible")),Color(group,"ok"),Color(group,"ng"),Number(group,"opacity"),Number(group,"width"));
        var configuredSize = Number(Text,"size");
        // The original bridge reads Int32 from the feature string. Its text renderer uses 10 when that parse yields zero.
        var renderedSize = configuredSize == Math.Truncate(configuredSize) ? configuredSize : 10d;
        return new(Style(Graphic),
            new(Read<bool>(Field(Text,"visible")),content,Color(Text,"ok"),Color(Text,"ng"),renderedSize,Number(Text,"opacity"),x is ConstantSetting {Value:double xv}?xv:0,y is ConstantSetting {Value:double yv}?yv:0,expression,x,y),
            Style(Inspection) with { ShowMeasurements = true },
            Style(Mask) with { Fill = new(255,84,0) },
            Style(Annulus) with { ShowMeasurements = true },
            new(Read<bool>(Field(Outline,"visible")),new(102,255,0),new(255,0,0)),Style(InnerRectangle),
            Style(ReferencePoint) with { FixedJudgment = true, PointLabel = "Fixture BasePoint" },
            Style(RunPoint) with { FixedJudgment = true, PointLabel = "Fixture RunPoint" });
    }
    public static void ValidateDisplayReference(OutputReference reference,bool numeric=false)
    {
        if(reference.Selection is BindingSelection.AtVariable)throw new ArgumentException("显示来源暂不支持变量索引。");
        if(reference.Selection is BindingSelection.AtField index)ValidateDisplayReference(index.Reference);
        var type=ParameterReferenceRules.SelectedType(reference);while(type.Shape==ValueShape.Sequence)type=type.Element!;
        if(type.Shape!=ValueShape.Scalar||type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64 or ScalarKind.Text)||numeric&&type.Scalar==ScalarKind.Text)
            throw new ArgumentException("显示来源只支持整数、浮点或文本。");
    }
    public static DisplayExpressionSetting ParseContent(string content,IEnumerable<DisplayExpressionSlot>? available=null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var slots=new List<DisplayExpressionSlot>();var known=(available??Array.Empty<DisplayExpressionSlot>()).ToArray();
        var position=0;
        while(position<content.Length)
        {
            var start=content.IndexOfAny(new[]{'{','}'},position);if(start<0)break;
            if(content[start]!='{')throw new ArgumentException("文本表达式括号不完整。");
            var end=content.IndexOf('}',start+1);
            if(end<0||content.AsSpan(start+1,end-start-1).Contains('{'))throw new ArgumentException("文本表达式括号不完整。");
            var token=content.Substring(start+1,end-start-1);
            var matches=known.Where(slot=>slot.Token==token).ToArray();
            if(matches.Length>1&&!matches.Skip(1).All(slot=>new DisplayExpressionSetting("",new[]{slot}).SameExpression(new("",new[]{matches[0]}))))
                throw new ArgumentException("文本来源名称不唯一，请重新选择。");
            if(matches.Length!=0)slots.Add(matches[0]);
            else if(System.Text.RegularExpressions.Regex.Match(token,@"^(0|面积|Area)(?::([FfNnGgRr][0-9]?))?$") is {Success:true} area)
                slots.Add(new(token,new ResultFieldPath(BlobSchema.BlobsPort,new[]{"area"}),area.Groups[2].Success?area.Groups[2].Value:null));
            else throw new ArgumentException("文本来源尚未选择或已失效："+token);
            position=end+1;
        }
        foreach(var slot in slots)if(slot.Reference is {} reference)ValidateDisplayReference(reference);
        return new(content,slots);
    }
    public static void ValidateContent(string content)=>_=ParseContent(content);
}

public sealed record BlobAnnulus(double CenterX,double CenterY,double InnerRadius,double OuterRadius,double StartAngle,double AngleRange);

/// <summary>
/// VM BlobFindResult 的中立投影。可直接对应的成员保留值，原件结果中暂无等价资源的成员保持 null 并列入 Unavailable。
/// </summary>
public sealed record BlobResultProjection(
    int ModuStatus,
    IReadOnlyList<int> BlobStatus,
    int BlobNum,
    IReadOnlyList<float> Area,
    float TotalArea,
    IReadOnlyList<Point2> CentroidPoint,
    IReadOnlyList<BlobOrientedBounds> MinBoudingRect,
    IReadOnlyList<float>? MinGrayValue,
    IReadOnlyList<float>? MaxGrayValue,
    IReadOnlyList<float>? GrayContrast,
    IReadOnlyList<BlobOrientedBounds> MaxAreaInnerRect,
    IReadOnlyList<BlobBounds> BlobRect,
    IReadOnlyList<float> BlobRectUpLeftX,
    IReadOnlyList<float> BlobRectUpLeftY,
    IReadOnlyList<float> BlobRectBottomRightX,
    IReadOnlyList<float> BlobRectBottomRightY,
    IReadOnlyList<float>? BlobAngle,
    IReadOnlyList<float> Perimeter,
    IReadOnlyList<float> LongAxis,
    IReadOnlyList<float> ShortAxis,
    IReadOnlyList<float> Circularity,
    IReadOnlyList<float> Rectangularity,
    IReadOnlyList<float>? Score,
    IReadOnlyImage? BinaryImageComb,
    IReadOnlyImage? BlobImageComb,
    IReadOnlyList<BlobOrientedBounds>? ROI,
    IReadOnlyList<BlobAnnulus>? ROIAnnulus,
    RecordValue? ContourInfo,
    int? ContourNum,
    IReadOnlyList<int>? BlindPolygonPointNum,
    IReadOnlyList<Point2>? BlindPolygonPoints,
    IReadOnlyList<string>? BlindPolygonString,
    IReadOnlyImage? OutputMask,
    IReadOnlySet<string> Unavailable)
{
    public static BlobResultProjection Create(IReadOnlyList<BlobMeasurement> measurements,IReadOnlyImage? binary,IReadOnlyImage? blob,
        ResultGeometrySnapshot geometry,
        SequenceValue? maskPolygons=null, IReadOnlyImage? maskImage=null, bool publishContour=false, CancellationToken cancellation=default)
    {
        var ordered=measurements.ToArray();
        var bounds=Array.AsReadOnly(ordered.Select(item=>item.Bounds).ToArray());
        var unavailable=new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Score)
        };
        var minGray=OptionalSequence(ordered.Select(item=>item.MinGrayValue));
        var maxGray=OptionalSequence(ordered.Select(item=>item.MaxGrayValue));
        var grayContrast=OptionalSequence(ordered.Select(item=>item.GrayContrast));
        var blobAngle=OptionalSequence(ordered.Select(item=>item.BlobAngleDegrees));
        var rectangles = ReadDetectionGeometry(geometry, ResultGeometryRole.Inspection,
            shape => shape.Get<string>("kind") == "rectangle", shape =>
            {
                var center = shape.Get<PointGeometry>("center").Point;
                return new BlobOrientedBounds(center.X, center.Y, shape.Get<double>("width"),
                    shape.Get<double>("height"), shape.Get<double>("angle-degrees"));
            });
        var annuli = ReadDetectionGeometry(geometry, ResultGeometryRole.Annulus,
            shape => shape.Get<string>("kind") is "annulus" or "sector", shape =>
            {
                var center = shape.Get<PointGeometry>("center").Point;
                var start = shape.Get<double>("start-radians");
                var end = shape.Get<double>("end-radians");
                return new BlobAnnulus(center.X, center.Y, shape.Get<double>("inner-radius"),
                    shape.Get<double>("outer-radius"), start * 180d / Math.PI,
                    Math.Clamp((end - start) * 180d / Math.PI, -360d, 360d));
            });
        if (rectangles is null) unavailable.Add(nameof(ROI));
        if (annuli is null) unavailable.Add(nameof(ROIAnnulus));
        if(minGray is null) unavailable.Add(nameof(MinGrayValue));
        if(maxGray is null) unavailable.Add(nameof(MaxGrayValue));
        if(grayContrast is null) unavailable.Add(nameof(GrayContrast));
        if(blobAngle is null) unavailable.Add(nameof(BlobAngle));
        int? contourCount = publishContour
            ? ordered.Sum(item => item.UnorderedContour?.Count ?? item.Boundary.Sum(path => path.Points.Count)) : null;
        var contourInfo = contourCount is > 0 ? CaptureContourInfo(ordered, contourCount.Value, cancellation) : null;
        var masks=maskPolygons?.Cast<PolygonGeometry>().ToArray();
        if(masks is null){unavailable.Add(nameof(BlindPolygonPointNum));unavailable.Add(nameof(BlindPolygonPoints));unavailable.Add(nameof(BlindPolygonString));}
        return new(
            ModuStatus:geometry.Passed?1:0,
            BlobStatus:Array.AsReadOnly(ordered.Select(item=>item.JudgmentPassed?1:0).ToArray()),
            BlobNum:ordered.Length,
            Area:Array.AsReadOnly(ordered.Select(item=>(float)item.Area).ToArray()),
            TotalArea:(float)BlobNumericSemantics.Float32Sum(ordered.Select(item=>item.Area)),
            CentroidPoint:Array.AsReadOnly(ordered.Select(item=>new Point2(item.CentroidX,item.CentroidY)).ToArray()),
            MinBoudingRect:Array.AsReadOnly(ordered.Select(item=>item.OrientedBounds).ToArray()),
            MinGrayValue:minGray,MaxGrayValue:maxGray,GrayContrast:grayContrast,
            MaxAreaInnerRect:Array.AsReadOnly(ordered.Select(item=>item.InnerRectangle).OfType<BlobOrientedBounds>().ToArray()),
            BlobRect:bounds,
            BlobRectUpLeftX:Array.AsReadOnly(ordered.Select(item=>(float)item.Bounds.X).ToArray()),
            BlobRectUpLeftY:Array.AsReadOnly(ordered.Select(item=>(float)item.Bounds.Y).ToArray()),
            BlobRectBottomRightX:Array.AsReadOnly(ordered.Select(item=>(float)(item.Bounds.X+item.Bounds.Width)).ToArray()),
            BlobRectBottomRightY:Array.AsReadOnly(ordered.Select(item=>(float)(item.Bounds.Y+item.Bounds.Height)).ToArray()),
            BlobAngle:blobAngle,
            Perimeter:Array.AsReadOnly(ordered.Select(item=>(float)item.Perimeter).ToArray()),
            LongAxis:Array.AsReadOnly(ordered.Select(item=>BlobNumericSemantics.LongAxis(item.OrientedBounds)).ToArray()),
            ShortAxis:Array.AsReadOnly(ordered.Select(item=>BlobNumericSemantics.ShortAxis(item.OrientedBounds)).ToArray()),
            Circularity:Array.AsReadOnly(ordered.Select(item=>(float)item.Circularity).ToArray()),
            Rectangularity:Array.AsReadOnly(ordered.Select(item=>(float)item.Rectangularity).ToArray()),
            Score:null,BinaryImageComb:binary,BlobImageComb:blob,
            ROI:rectangles,ROIAnnulus:annuli,ContourInfo:contourInfo,ContourNum:contourCount,
            BlindPolygonPointNum:masks is null?null:Array.AsReadOnly(masks.Select(polygon=>polygon.Vertices.Count).ToArray()),
            BlindPolygonPoints:masks is null?null:Array.AsReadOnly(masks.SelectMany(polygon=>polygon.Vertices).ToArray()),
            BlindPolygonString:masks is null?null:Array.AsReadOnly(masks.Select(BlobMaskInheritance.Encode).ToArray()),OutputMask:maskImage,
            Unavailable:unavailable);
    }

    private static RecordValue CaptureContourInfo(IReadOnlyList<BlobMeasurement> blobs, int count, CancellationToken cancellation)
    {
        var x = new double[count];
        var y = new double[count];
        var score = new double[count];
        var index = new long[count];
        var pointIndex = 0;
        for (var blobIndex = 0; blobIndex < blobs.Count; blobIndex++)
        {
            var blob = blobs[blobIndex];
            foreach (var point in BlobSchema.OutlinePoints(blob))
            {
                if ((pointIndex & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                x[pointIndex] = (float)point.X;
                y[pointIndex] = (float)point.Y;
                score[pointIndex] = blob.JudgmentPassed ? 0 : 2;
                index[pointIndex++] = blobIndex;
            }
        }
        return new(BlobSchema.ContourInfoType, new Dictionary<string, object?>
        {
            ["x"] = x, ["y"] = y, ["score"] = score, ["blob-index"] = index
        });
    }

    private static IReadOnlyList<T>? ReadDetectionGeometry<T>(ResultGeometrySnapshot geometry, ResultGeometryRole role,
        Func<RecordValue, bool> supported, Func<RecordValue, T> project)
    {
        var group = geometry.Groups.SingleOrDefault(group => group.Role == role);
        if (group is null || group.OutlineOnly || group.Coverage is ResultGeometryCoverage.Unavailable or ResultGeometryCoverage.FullDomain
            || !group.Shapes.All(supported))
            return null;
        return Array.AsReadOnly(group.Shapes.Select(project).ToArray());
    }

    private static IReadOnlyList<float>? OptionalSequence(IEnumerable<double?> values)
    {
        var items=values.ToArray();
        if(items.Any(value=>value is null)) return items.Length==0?Array.Empty<float>():null;
        return Array.AsReadOnly(items.Select(value=>(float)value!.Value).ToArray());
    }

}

/// <summary>Stateless projection of the sole private persisted ROI into the existing neutral display declaration.</summary>
public static class BlobRoiDisplayProjection
{
    public static RecordValue Shape(RecordValue shape)
    {
        RoiDocumentValue.ValidateShape(shape);
        var fields=shape.CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(shape.Contains("angle-degrees"))fields["angle-degrees"]=RoiDocumentValue.NormalizeDegrees(shape.Get<double>("angle-degrees"));
        PointGeometry Point(RecordValue point)=>new(RoiDeclarations.ImageFrame,new(point.Get<double>("x"),point.Get<double>("y")));
        if(shape.Contains("center"))fields["center"]=Point(shape.Get<RecordValue>("center"));
        if(shape.Contains("vertices"))
        {
            fields["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(port=>port.Id=="vertices").Type,
                shape.Get<SequenceValue>("vertices").Cast<RecordValue>().Select(point=>(object?)Point(point)));
            fields["closed"]=true;
        }
        return new RecordValue(RoiDeclarations.Shape,fields);
    }
}

/// <summary>Existing stable output IDs and Contracts records, without SDK reflection or visual types.</summary>
public static class BlobSchema
{
    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;
    public static string Field(int number) => "field:" + Guid.Parse("8ca10000-0000-4000-8000-" + number.ToString("D12", System.Globalization.CultureInfo.InvariantCulture)).ToString("N");
    public static string BlobsPort => Field(100);
    public static string RegionPort => Field(101);
    public static string MaskImagePort => Field(103);
    public static string InnerRectanglesPort => Field(104);
    public static string MaskPolygonsPort => Field(105);
    public static string MaskPolygonStringsPort => Field(124);
    public static string DetectionRectanglesPort => Field(106);
    public static string DetectionAnnuliPort => Field(107);
    public static string ContourPointsPort => Field(108);
    public static string ModuleStatusPort => Field(4);
    public static string ModuleRunTimePort => Field(123);
    public static string BlobStatusesPort => Field(109);
    public static string MinGrayValuesPort => Field(110);
    public static string MaxGrayValuesPort => Field(111);
    public static string GrayContrastsPort => Field(112);
    public static string PrincipalAnglesPort => Field(113);
    public static string OuterRectanglesPort => Field(114);
    public static string LongAxesPort => Field(115);
    public static string ShortAxesPort => Field(116);
    public static string CentroidPointsPort => Field(117);
    public static string AxisAlignedRectanglesPort => Field(118);
    public static string RectangleTopLeftXPort => Field(119);
    public static string RectangleTopLeftYPort => Field(120);
    public static string RectangleBottomRightXPort => Field(121);
    public static string RectangleBottomRightYPort => Field(122);
    public static TypeRef CentroidPointsType { get; } = TypeRef.SequenceOf(FixtureDeclarations.Point);
    public static TypeRef PointType { get; } = TypeRef.GeometryOf(GeometryKind.Point, ImageFrame);
    public static TypeRef BoundsType { get; } = TypeRef.RecordOf("blob.bounds", new[]
    {
        new Port("origin", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage:NumericStorage.Float32),
        new Port("height", DataType.Number, Storage:NumericStorage.Float32)
    });
    public static TypeRef AxisAlignedRectanglesType { get; } = TypeRef.SequenceOf(BoundsType);
    public static TypeRef OrientedRectanglesType { get; } = TypeRef.SequenceOf(BlobRoiInheritance.RectangleType);
    public static TypeRef BoundaryType { get; } = SpatialDeclarations.Boundary(ImageFrame);
    public static TypeRef BlobType { get; } = TypeRef.RecordOf("blob.result", new[] {
        new Port("element-id", DataType.Integer), new Port("label", DataType.Integer), new Port("area", DataType.Number), new Port("perimeter", DataType.Number),
        new Port("centroid-x", DataType.Number), new Port("centroid-y", DataType.Number), new Port("centroid", PointType),
        new Port("circularity", DataType.Number), new Port("rectangularity", DataType.Number), new Port("bounds", BoundsType),
        new Port("oriented-bounds",BlobRoiInheritance.RectangleType), new Port("long-axis", DataType.Number), new Port("short-axis", DataType.Number),
        new Port("rotated-bounds", TypeRef.SequenceOf(PointType, CountConstraint.Exactly(4)), false),
        new Port("contour", TypeRef.SequenceOf(PointType), false), new Port("contour-lossy", DataType.Boolean, false), new Port("judgment-passed", DataType.Boolean),
        new Port("boundary", BoundaryType, false), new Port("outline-points", TypeRef.SequenceOf(PointType), false),
        new Port("inner-rectangle", TypeRef.GeometryOf(GeometryKind.OrientedRectangle, ImageFrame), false),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region)) });
    public static TypeRef BlobsType { get; } = TypeRef.SequenceOf(BlobType);
    private static TypeRef ImageRecord(string name)=>TypeRef.RecordOf(name,new[]{
        new Port("data",TypeRef.ResourceOf(ResourceKind.Image)),
        new Port("width",DataType.Integer,Storage:NumericStorage.Int32),
        new Port("height",DataType.Integer,Storage:NumericStorage.Int32),
        new Port("pixel-format",DataType.Integer,Storage:NumericStorage.Int32)});
    public static TypeRef BlobImageType {get;}=ImageRecord("blob.image");
    public static TypeRef BinaryImageType {get;}=ImageRecord("blob.binary-image");
    public static TypeRef MaskImageType {get;}=ImageRecord("blob.mask-image");
    public static TypeRef ContourInfoType { get; } = TypeRef.RecordOf("blob.contour-info", new[]
    {
        new Port("x", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("y", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("score", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("blob-index", TypeRef.SequenceOf(DataType.Integer), Storage:NumericStorage.Int32)
    });
    public static TypeRef ContourPointsType { get; } = TypeRef.RecordOf("blob.contour-points", new[]
    {
        new Port("info", ContourInfoType, Nullable:true),
        new Port("count", DataType.Integer, Storage:NumericStorage.Int32)
    });
    public static IReadOnlyList<Port> Outputs { get; } = Array.AsReadOnly(new[] {
        new Port(ModuleStatusPort, DataType.Integer, Storage:NumericStorage.Int32),
        new Port(ModuleRunTimePort, DataType.Number, Storage:NumericStorage.Float32),
        new Port(BlobStatusesPort, TypeRef.SequenceOf(DataType.Integer), Storage:NumericStorage.Int32),
        new Port(Field(5), BlobImageType, Nullable:true),
        new Port(Field(6), BinaryImageType, Nullable:true),
        new Port(Field(8), DataType.Integer, Nullable:true), new Port(Field(9), DataType.Integer, Nullable:true), new Port(Field(10), DataType.Text), new Port(Field(102), DataType.Text),
        new Port(Field(11), DataType.Integer, Storage:NumericStorage.Int32),
        new Port(Field(12), DataType.Number, Storage:NumericStorage.Float32), new Port(Field(13), DataType.Number),
        new Port(Field(14), DataType.Number), new Port(Field(15), DataType.Number), new Port(Field(16), DataType.Number),
        new Port(Field(17), DataType.NumberArray), new Port(Field(18), DataType.NumberArray),
        new Port(Field(19), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(20), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(21), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(22), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(CentroidPointsPort, CentroidPointsType),
        new Port(Field(23), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(24), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(25), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(26), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(AxisAlignedRectanglesPort, AxisAlignedRectanglesType),
        new Port(RectangleTopLeftXPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(RectangleTopLeftYPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(RectangleBottomRightXPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(RectangleBottomRightYPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(27), TypeRef.SequenceOf((TypeRef)DataType.Text)),
        new Port(Field(30),DataType.Number),new Port(Field(31),DataType.Number),
        new Port(BlobsPort, BlobsType), new Port(RegionPort, TypeRef.ResourceOf(ResourceKind.Region)),
        new Port(MaskImagePort, MaskImageType, Nullable:true), new Port(InnerRectanglesPort, OrientedRectanglesType),
        new Port(MaskPolygonsPort, BlobMaskInheritance.Polygons),
        new Port(MaskPolygonStringsPort, BlobMaskInheritance.PolygonStrings),
        new Port(DetectionRectanglesPort, OrientedRectanglesType, Nullable:true),
        new Port(DetectionAnnuliPort, TypeRef.SequenceOf(BlobRoiInheritance.AnnulusType), Nullable:true),
        new Port(ContourPointsPort, ContourPointsType, Nullable:true),
        new Port(MinGrayValuesPort, TypeRef.SequenceOf(DataType.Number), Nullable:true, Storage:NumericStorage.Float32),
        new Port(MaxGrayValuesPort, TypeRef.SequenceOf(DataType.Number), Nullable:true, Storage:NumericStorage.Float32),
        new Port(GrayContrastsPort, TypeRef.SequenceOf(DataType.Number), Nullable:true, Storage:NumericStorage.Float32),
        new Port(PrincipalAnglesPort, TypeRef.SequenceOf(DataType.Number), Nullable:true, Storage:NumericStorage.Float32),
        new Port(OuterRectanglesPort, OrientedRectanglesType),
        new Port(LongAxesPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(ShortAxesPort, DataType.NumberArray, Storage:NumericStorage.Float32)
    });

    public static IReadOnlyList<OutputDisplayNode> OutputDisplay {get;}=BlobOutputDisplay.Create();

    internal static RecordValue FreezePoint(Point2 point) => new(FixtureDeclarations.Point,
        new Dictionary<string, object?> { ["x"] = (double)(float)point.X, ["y"] = (double)(float)point.Y });

    internal static RecordValue FreezeAxisAlignedBounds(BlobBounds bounds) => new(BoundsType,
        new Dictionary<string, object?>
        {
            ["origin"] = FreezePoint(new(bounds.X, bounds.Y)),
            ["width"] = (double)(float)bounds.Width,
            ["height"] = (double)(float)bounds.Height
        });

    internal static RecordValue FreezeBounds(BlobOrientedBounds bounds) => new(BlobRoiInheritance.RectangleType,
        new Dictionary<string, object?>
        {
            ["center"] = FreezePoint(new(bounds.CenterX,bounds.CenterY)),
            ["width"] = (double)(float)bounds.Width, ["height"] = (double)(float)bounds.Height,
            ["angle-degrees"] = (double)(float)bounds.AngleDegrees
        });

    internal static RecordValue FreezeDetectionAnnulus(BlobAnnulus annulus) => new(BlobRoiInheritance.AnnulusType,
        new Dictionary<string, object?>
        {
            ["center"] = FreezePoint(new(annulus.CenterX,annulus.CenterY)),
            ["inner-radius"] = (double)(float)annulus.InnerRadius, ["outer-radius"] = (double)(float)annulus.OuterRadius,
            ["start-degrees"] = (double)(float)annulus.StartAngle, ["sweep-degrees"] = (double)(float)annulus.AngleRange
        });

    internal static IEnumerable<Point2> OutlinePoints(BlobMeasurement blob) =>
        blob.UnorderedContour ?? blob.Boundary.SelectMany(path => path.Points);

    public static RecordValue Freeze(BlobMeasurement blob, bool showOutline = true, CancellationToken cancellation = default)
    {
        var values = new Dictionary<string, object?> {
            ["element-id"] = blob.ElementId, ["label"] = (long)blob.Label, ["area"] = blob.Area, ["perimeter"] = blob.Perimeter,
            ["centroid-x"] = blob.CentroidX, ["centroid-y"] = blob.CentroidY, ["centroid"] = new PointGeometry(ImageFrame, new(blob.CentroidX, blob.CentroidY)),
            ["circularity"] = blob.Circularity, ["rectangularity"] = blob.Rectangularity,
            ["bounds"] = FreezeAxisAlignedBounds(blob.Bounds),
            ["oriented-bounds"] = FreezeBounds(blob.OrientedBounds),
            ["long-axis"] = (double)BlobNumericSemantics.LongAxis(blob.OrientedBounds),
            ["short-axis"] = (double)BlobNumericSemantics.ShortAxis(blob.OrientedBounds),
            ["judgment-passed"] = blob.JudgmentPassed, ["region"] = blob.Region,
            ["rotated-bounds"] = new SequenceValue(TypeRef.SequenceOf(PointType, CountConstraint.Exactly(4)), blob.RotatedBounds.Select(p => (object?)new PointGeometry(ImageFrame, p)))
        };
        if (blob.InnerRectangle is { } rectangle)
            values["inner-rectangle"] = new OrientedRectangleGeometry(ImageFrame,
                new(rectangle.CenterX, rectangle.CenterY), rectangle.Width, rectangle.Height, rectangle.AngleDegrees * Math.PI / 180);
        SequenceValue PointSet(IEnumerable<Point2> points) => new(TypeRef.SequenceOf(PointType),
            points.Select((point, index) =>
            {
                if ((index & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                return new PointGeometry(ImageFrame, point);
            }));
        var boundaryPoints = blob.Boundary.Select(path => PointSet(path.Points)).ToArray();
        if (blob.UnorderedContour is { } unordered)
        {
            values["contour"] = PointSet(unordered);
            values["contour-lossy"] = false;
        }
        else if (blob.Boundary.Count != 0)
        {
            values["contour-lossy"] = blob.Boundary.Count != 1;
            for (var index = 0; index < blob.Boundary.Count; index++)
            {
                if (blob.Boundary[index] is not { Role: "outer", ParentId: null }) continue;
                values["contour"] = boundaryPoints[index];
                break;
            }
        }
        if (showOutline && (blob.UnorderedContour is not null || blob.Boundary.Count != 0))
            values["outline-points"] = blob.UnorderedContour is not null ? values["contour"]
                : boundaryPoints.Length == 1 ? boundaryPoints[0]
                : new SequenceValue(TypeRef.SequenceOf(PointType), boundaryPoints.SelectMany(points => points));
        if (blob.Boundary.Count != 0)
        {
            var pathType = BoundaryType.Fields.Single(x => x.Id == "paths").Type.Element!;
            var paths = blob.Boundary.Select((path, index) => {
                var fields = new Dictionary<string, object?> { ["path-id"] = path.Id, ["role"] = path.Role, ["closed"] = true,
                    ["points"] = boundaryPoints[index] };
                if (path.ParentId is { } parent) fields["parent-path-id"] = parent;
                return (object?)new RecordValue(pathType, fields);
            });
            var boundary = new RecordValue(BoundaryType, new Dictionary<string, object?> { ["paths"] = new SequenceValue(TypeRef.SequenceOf(pathType), paths) });
            SpatialDeclarations.ValidateBoundary(boundary, ImageFrame);
            values["boundary"] = boundary;
        }
        return new(BlobType, values);
    }
}

/// <summary>Finite Blob presentation roles; no WPF, live parameters, reflection or scheduling.</summary>
public static class BlobPresentation
{
 public static FieldLayout Layout(string key, IReadOnlyDictionary<string,string> ids)
 {
  var basic=key.StartsWith("input.")||key=="setting.pose-enabled";
  var result=key.StartsWith("presentation.");
  var page=basic?"基本参数":result?"结果显示":"运行参数";
  var section=basic?"ROI区域":key.StartsWith("presentation.judgments.")?"结果判断":key.StartsWith("presentation.graphics.")?"图像显示":key.StartsWith("presentation.texts.")?"文本显示":"运行参数";
  FieldCondition? condition=null;string? paired=null;
  var minimum=default(double?);var maximum=default(double?);var decimals=2;var increment=1d;
  var advanced=false;var allowReference=!key.EndsWith("Enabled")&&!key.EndsWith("IsVisible")&&!key.EndsWith("IsBold");
  if(key is "setting.pose-enabled" or "setting.SegmentationMode")allowReference=false;
  string? Id(string logical)=>ids.TryGetValue(logical,out var id)?id:null;
  void When(string logical,object value){if(Id(logical) is {} id)condition=new(id,value);}
  if(key is "setting.Threshold" or "setting.UpperThreshold"){minimum=0;maximum=255;decimals=0;}
  // 证据 IMVSBlobFindModuAlgorithmTab.xml 的 ThresholdType（EnumEntry 的 Value）：
  //   1=BinaryImage 2=SingleThreshold 3=DoubleThreshold 4=AutoThreshold
  //   5=FixedSoftThreshold 6=RelativeSoftThreshold
  // 同一控件在多个类型下都显示（原厂 MultiTrigger），故用多值条件。
  var segmentation=ids["setting.SegmentationMode"];
  if(key=="setting.Threshold")
      condition=new(segmentation,new object?[]{"SingleThreshold","DoubleThreshold","AutoThreshold"});
  if(key=="setting.UpperThreshold")
      condition=new(segmentation,new object?[]{"DoubleThreshold","AutoThreshold"});
  // 极性：AutoThreshold 由算法自定极性（原厂 0x4 无 Polarity），其余类型都显示。
  if(key=="setting.Type")condition=new(segmentation,"AutoThreshold",true);
  if(key.StartsWith("setting.Filter"))
  {
   section="Blob筛选";
   var family=key.Contains("Circularity")?"Circularity":key.Contains("Rectangularity")?"Rectangularity":"Area";
   When("setting.Is"+family+"FilterEnabled",true);
   minimum=0;maximum=family=="Area"?9999999:1;
   if(family=="Area")decimals=0;else increment=.01;
   if(key.EndsWith("Min"))paired=Id(key[..^3]+"Max");
  }
  if(key is "setting.IsAreaFilterEnabled" or "setting.IsCircularityFilterEnabled" or "setting.IsRectangularityFilterEnabled" or "setting.SortMode" or "setting.BlobCountLimit")section="Blob筛选";
  if(key=="setting.BlobCountLimit"){minimum=1;maximum=20000;decimals=0;}
  if(key.StartsWith("setting.Output"))section="输出";
  if(key.StartsWith("presentation.judgments."))
  {
   allowReference=false;var prefix=key[..key.LastIndexOf('.')];
   if(key.EndsWith(".Min")||key.EndsWith(".Max")){When(prefix+".Enabled",true);if(key.EndsWith(".Min"))paired=Id(prefix+".Max");}
   advanced=!key.Contains("output-not-empty")&&!key.Contains("blob-count")&&!key.Contains("total-area");
  }
  string? dialog=null,title=null;var primary=false;
  if(key.StartsWith("presentation.graphics."))
  {
   dialog=key[..key.IndexOf('.',"presentation.graphics.".Length)];primary=key.EndsWith(".IsVisible");
   title=dialog["presentation.graphics.".Length..] switch {"blobs"=>"Blob","contours"=>"轮廓","inspection-roi"=>"检测区域","mask-roi"=>"屏蔽区域",_=>throw new ArgumentException("Unknown declared Blob display group.")};
  }
  // Original ImageViewConfig_subContent bounds, enforced by the real numeric composition.
  if(key.StartsWith("presentation.")&&key.EndsWith(".Style.Opacity")){minimum=0;maximum=1;increment=.01;}
  if(key.StartsWith("presentation.graphics.")&&key.EndsWith(".Style.LineWidth")){minimum=1;maximum=10;}
  if(key.StartsWith("setting.")&&BlobParameterDefinition.Instance.Contains(key[8..]))
  {
   var id=key[8..];var definition=BlobParameterDefinition.Instance;
   page=definition.Page(id);section=definition.Section(id);minimum=definition.Minimum(id);maximum=definition.Maximum(id);
   decimals=definition.DecimalPlaces(id);increment=definition.Increment(id);
  }
  // 证据 IMVSBlobAnalysisModuAlgorithmTab.xml：Tab_ROI Area / Tab_Run Params / Tab_Check Result
  // 三个 Category 标 CanTriggerRun="TRUE"；本函数产出的页与之逐一对应，故三页均标可触发运行。
  var canTriggerRun=page is "基本参数" or "运行参数" or "结果显示";
  return new(page,section,condition,paired,advanced,minimum,maximum,decimals,allowReference,dialog,title,primary,increment,
      CanTriggerRun:canTriggerRun);
 }
}
