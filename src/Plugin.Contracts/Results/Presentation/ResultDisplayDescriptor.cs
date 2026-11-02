namespace SunEyeVision.Plugin.Contracts.Results.Presentation;

/// <summary>Presentation-only fields: persisted with the tool, never invocation inputs or algorithm parameters.</summary>
public interface IToolResultDisplayProvider
{
    ToolSchema DisplaySettingsSchema { get; }
    InputValues CreateDisplaySettings();
    ResultDisplayDescriptor ResultDisplay { get; }
    ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields);
}

public sealed record ResultDisplayColor(byte Red,byte Green,byte Blue,byte Alpha=255);
public sealed record ResultGraphicAppearance(bool Visible,ResultDisplayColor Ok,ResultDisplayColor Ng,double Opacity,double Width);
public sealed record ResultTextAppearance(bool Visible,string Content,ResultDisplayColor Ok,ResultDisplayColor Ng,
    double FontSize,double Opacity,double X,double Y,DisplayExpressionSetting? Expression=null,ParameterSetting? PositionX=null,ParameterSetting? PositionY=null);
/// <summary>Pure immutable appearance. Its host revision is independent of invocation/run identity.</summary>
public sealed record ResultDisplayAppearance(ResultGraphicAppearance Graphic,ResultTextAppearance Text,
    ResultGraphicAppearance? Inspection=null,ResultGraphicAppearance? Mask=null,ResultGraphicAppearance? Annulus=null);

/// <summary>Only declared same-row output paths. Does not own values, resources, callbacks or a renderer.</summary>
public sealed class ResultDisplayDescriptor
{
    public ResultDisplayDescriptor(ResultFieldPath rows,ResultFieldPath identity,ResultFieldPath quadrilateral,
        ResultFieldPath geometrySource,ResultFieldPath textValue,ResultFieldPath passed)
    {Rows=rows;Identity=identity;Quadrilateral=quadrilateral;GeometrySource=geometrySource;TextValue=textValue;Passed=passed;}
    public ResultFieldPath Rows {get;}
    public ResultFieldPath Identity {get;}
    public ResultFieldPath Quadrilateral {get;}
    public ResultFieldPath GeometrySource {get;}
    public ResultFieldPath TextValue {get;}
    public ResultFieldPath Passed {get;}
    public void Validate(ToolSchema schema)
    {
        var rows=Rows.Resolve(schema);
        if(rows.Shape!=ValueShape.Sequence||rows.Element?.Shape!=ValueShape.Record)throw new ArgumentException("Display rows require declared record results.");
        foreach(var path in new[]{Identity,Quadrilateral,GeometrySource,TextValue,Passed})
            if(path.PortId!=Rows.PortId||path.MemberFieldPath.Count!=Rows.MemberFieldPath.Count+1
                ||!path.MemberFieldPath.Take(Rows.MemberFieldPath.Count).SequenceEqual(Rows.MemberFieldPath))
                throw new ArgumentException("Display paths must address members of the same declared rows.");
        TypeRef Element(ResultFieldPath path)
        {var type=path.Resolve(schema);if(type.Shape!=ValueShape.Sequence)throw new ArgumentException("Display member lost its row cardinality.");return type.Element!;}
        if(Element(Identity)!=DataType.Integer||Element(Passed)!=DataType.Boolean||Element(TextValue)!=DataType.Number
            ||Element(GeometrySource)!=TypeRef.ResourceOf(ResourceKind.Region))throw new ArgumentException("Display identity, status, value or physical geometry source differs from its declaration.");
        var geometry=Element(Quadrilateral);
        if(geometry.Shape!=ValueShape.Sequence||geometry.Element?.Shape!=ValueShape.Geometry||geometry.Element.Geometry!=GeometryKind.Point||geometry.Count!=CountConstraint.Exactly(4))
            throw new ArgumentException("Display quadrilaterals require declared point sequences.");
    }
}

/// <summary>Shared declaration composition only. Does not create defaults while loading or editing.</summary>
public static class ResultDisplayFields
{
    public static bool SameReferenceIdentity(OutputReference a,OutputReference b)=>new DisplayExpressionSetting("",new[]{new DisplayExpressionSlot("source",a)}).SameExpression(new("",new[]{new DisplayExpressionSlot("source",b)}));
    public static ToolSchema Schema(IToolDefinition definition)
        =>(definition as IToolResultDisplayProvider)?.DisplaySettingsSchema??new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
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
