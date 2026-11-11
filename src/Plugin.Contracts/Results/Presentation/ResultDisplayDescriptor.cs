namespace SunEyeVision.Plugin.Contracts.Results.Presentation;

/// <summary>Presentation-only fields: persisted with the tool, never invocation inputs or algorithm parameters.</summary>
public interface IToolDisplaySettingsProvider
{
    ToolSchema DisplaySettingsSchema { get; }
    InputValues CreateDisplaySettings();
}

public interface IToolResultDisplayProvider : IToolDisplaySettingsProvider
{
    ResultDisplayDescriptor ResultDisplay { get; }
    ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields);
}

public sealed record ResultDisplayColor(byte Red,byte Green,byte Blue,byte Alpha=255);
public sealed record ResultGraphicAppearance(bool Visible,ResultDisplayColor Ok,ResultDisplayColor Ng,double Opacity,double Width,
    ResultDisplayColor? Fill=null,bool? FixedJudgment=null,bool ShowMeasurements=false,string? PointLabel=null);
public sealed record ResultOutlineAppearance(bool Visible,ResultDisplayColor Ok,ResultDisplayColor Ng);
public sealed record ResultTextAppearance(bool Visible,string Content,ResultDisplayColor Ok,ResultDisplayColor Ng,
    double FontSize,double Opacity,double X,double Y,DisplayExpressionSetting? Expression=null,ParameterSetting? PositionX=null,ParameterSetting? PositionY=null);
/// <summary>Pure immutable appearance. Its host revision is independent of invocation/run identity.</summary>
public sealed record ResultDisplayAppearance(ResultGraphicAppearance Graphic,ResultTextAppearance Text,
    ResultGraphicAppearance? Inspection=null,ResultGraphicAppearance? Mask=null,ResultGraphicAppearance? Annulus=null,ResultOutlineAppearance? Outline=null,
    ResultGraphicAppearance? InnerRectangle=null,ResultGraphicAppearance? ReferencePoint=null,ResultGraphicAppearance? RunPoint=null);

/// <summary>Only declared same-row output paths. Does not own values, resources, callbacks or a renderer.</summary>
public sealed class ResultDisplayDescriptor
{
    public ResultDisplayDescriptor(ResultFieldPath rows,ResultFieldPath identity,ResultFieldPath rectangle,
        ResultFieldPath geometrySource,ResultFieldPath textValue,ResultFieldPath passed,ResultFieldPath? outline=null,
        ResultFieldPath? innerRectangle=null,ResultDisplayColor? selectedRectangleColor=null)
    {Rows=rows;Identity=identity;Rectangle=rectangle;GeometrySource=geometrySource;TextValue=textValue;Passed=passed;Outline=outline;InnerRectangle=innerRectangle;SelectedRectangleColor=selectedRectangleColor;}
    public ResultFieldPath Rows {get;}
    public ResultFieldPath Identity {get;}
    public ResultFieldPath Rectangle {get;}
    public ResultFieldPath GeometrySource {get;}
    public ResultFieldPath TextValue {get;}
    public ResultFieldPath Passed {get;}
    public ResultFieldPath? Outline {get;}
    public ResultFieldPath? InnerRectangle {get;}
    public ResultDisplayColor? SelectedRectangleColor {get;}
    public IReadOnlyList<OutputValueRequest> RequiredOutputs=>Outline is null?Array.Empty<OutputValueRequest>():new[]{new OutputValueRequest(Outline.PortId,Outline.MemberFieldPath)};
    public void Validate(ToolSchema schema)
    {
        var rows=Rows.Resolve(schema);
        if(rows.Shape!=ValueShape.Sequence||rows.Element?.Shape!=ValueShape.Record)throw new ArgumentException("Display rows require declared record results.");
        foreach(var path in new[]{Identity,Rectangle,GeometrySource,TextValue,Passed})
            if(!SameRow(path))
                throw new ArgumentException("Display paths must address members of the same declared rows.");
        bool SameRow(ResultFieldPath path) => path.PortId==Rows.PortId && path.MemberFieldPath.Count==Rows.MemberFieldPath.Count+1
            && path.MemberFieldPath.Take(Rows.MemberFieldPath.Count).SequenceEqual(Rows.MemberFieldPath);
        TypeRef Element(ResultFieldPath path)
        {var type=path.Resolve(schema);if(type.Shape!=ValueShape.Sequence)throw new ArgumentException("Display member lost its row cardinality.");return type.Element!;}
        if(Element(Identity)!=DataType.Integer||Element(Passed)!=DataType.Boolean||Element(TextValue)!=DataType.Number
            ||Element(GeometrySource)!=TypeRef.ResourceOf(ResourceKind.Region))throw new ArgumentException("Display identity, status, value or physical geometry source differs from its declaration.");
        if(Outline is {} outline)
        {
            if(!SameRow(outline))
                throw new ArgumentException("Outline must address the same result rows.");
            var boundary=Element(outline);
            var paths=boundary.Fields.SingleOrDefault(port=>port.Id=="paths")?.Type;
            var points=paths?.Element?.Fields.SingleOrDefault(port=>port.Id=="points")?.Type;
            var pointSet=boundary.Shape==ValueShape.Sequence&&boundary.Element?.Geometry==GeometryKind.Point;
            if(!pointSet&&(boundary.Shape!=ValueShape.Record||paths?.Shape!=ValueShape.Sequence||points?.Shape!=ValueShape.Sequence||points.Element?.Geometry!=GeometryKind.Point))
                throw new ArgumentException("Outline requires point geometry or boundary paths.");
        }
        var bounds=Element(Rectangle);
        if(bounds.Shape!=ValueShape.Record
            ||bounds.Fields.SingleOrDefault(port=>port.Id=="center")?.Type!=FixtureDeclarations.Point
            ||new[]{"width","height","angle-degrees"}.Any(id=>bounds.Fields.SingleOrDefault(port=>port.Id==id)?.Type!=DataType.Number))
            throw new ArgumentException("Display rectangles require a declared center, width, height and angle in degrees.");
        if(InnerRectangle is {} rectangle && (!SameRow(rectangle) || Element(rectangle).Geometry != GeometryKind.OrientedRectangle))
            throw new ArgumentException("An inner rectangle requires rectangle geometry in the same declared row.");
    }
}

/// <summary>Shared declaration composition only. Does not create defaults while loading or editing.</summary>
public static class ResultDisplayFields
{
    public static bool SameReferenceIdentity(OutputReference a,OutputReference b)=>new DisplayExpressionSetting("",new[]{new DisplayExpressionSlot("source",a)}).SameExpression(new("",new[]{new DisplayExpressionSlot("source",b)}));
    public static ToolSchema Schema(IToolDefinition definition)
        =>(definition as IToolDisplaySettingsProvider)?.DisplaySettingsSchema??new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
    public static ToolSchema EditorSchema(IToolDefinition definition)
    {
        var algorithm=((IToolSettingsFields)definition).SettingsSchema;var display=Schema(definition);
        if(algorithm.Inputs.Concat(definition.Describe().Inputs).Any(port=>(port.EditModes&FieldEditModes.DisplayExpression)!=0))
            throw new ArgumentException("Display expressions cannot be declared as invocation inputs or algorithm settings.");
        if(display.Inputs.Any(port=>port.EditModes==FieldEditModes.None||(port.EditModes&~(FieldEditModes.Constant|FieldEditModes.Reference|FieldEditModes.DisplayExpression))!=0
            ||(port.EditModes&FieldEditModes.DisplayExpression)!=0&&port.Type!=DataType.Text)||display.Outputs.Count!=0
            ||display.Inputs.Select(port=>port.Id).Intersect(algorithm.Inputs.Concat(definition.Describe().Inputs).Select(port=>port.Id),StringComparer.Ordinal).Any())
            throw new ArgumentException("Display fields must have unique presentation-only identities.");
        return display.Inputs.Count==0?algorithm:new ToolSchema(algorithm.Inputs.Concat(display.Inputs),Array.Empty<Port>(),
            new ToolPresentation((algorithm.Presentation?.Fields??Array.Empty<FieldPresentation>()).Concat(display.Presentation?.Fields??Array.Empty<FieldPresentation>())));
    }
    public static ResultDisplayAppearance? Capture(IToolDefinition definition,FieldSettings fields)
        =>definition is IToolResultDisplayProvider provider?provider.CaptureResultDisplay(fields.Select(provider.DisplaySettingsSchema.Inputs)):null;
}
