namespace SunEyeVision.Plugin.Contracts;

/// <summary>Finite UI-neutral layout; conditions address declared stable field IDs, never CLR paths.</summary>
public sealed record FieldCondition(string FieldId, object? Value);
public sealed record FieldLayout(string Page, string Section, FieldCondition? VisibleWhen=null,
    string? PairWith=null, bool Advanced=false, double? Minimum=null, double? Maximum=null,
    int DecimalPlaces=2, bool AllowReference=true);

public enum FieldEditorKind { Scalar, Choice, Record, Sequence, Geometry, Region, ResultPresentation, Image }
[Flags] public enum FieldEditModes { None=0, Constant=1, Reference=2, NumericGeometry=4, Gesture=8 }
public enum FieldDefaultPresence { Unset, ExplicitValueRequired }
public enum RegionFieldMode { LocalGeometry, InheritedGeometry, InheritedRegion }
/// <summary>Immutable closed text values, enforced by ValueRules for constants and resolved references alike.</summary>
public sealed class TextChoiceConstraint
{
    public TextChoiceConstraint(IEnumerable<string> keys)
    {
        var values=keys.ToArray();
        if(values.Length==0||values.Any(string.IsNullOrWhiteSpace)||values.Distinct(StringComparer.Ordinal).Count()!=values.Length)
            throw new ArgumentException("Text choices require a nonempty closed set of unique keys.");
        Keys=Array.AsReadOnly(values);
    }
    public IReadOnlyList<string> Keys{get;}
}
public sealed record RegionFieldGroup(string Role, string ModeFieldId, string LocalGeometryFieldId,
    string GeometrySourceFieldId, string RegionSourceFieldId);

/// <summary>Presentation only points at declared stable fields; it never infers types or stores values.</summary>
public sealed class FieldPresentation
{
    public FieldPresentation(string fieldId,string displayName,string group,int order,FieldEditorKind editor,
        IEnumerable<string>? choiceKeys=null,IReadOnlyDictionary<string,string>? choiceLabels=null)
    {
        if(string.IsNullOrWhiteSpace(fieldId)||!Enum.IsDefined(editor))throw new ArgumentException("A declared field and known editor kind are required.");
        FieldId=fieldId;DisplayName=displayName??throw new ArgumentNullException(nameof(displayName));Group=group??"";Order=order;Editor=editor;
        var choices=(choiceKeys??Array.Empty<string>()).ToArray();
        if(choices.Any(string.IsNullOrWhiteSpace)||choices.Distinct(StringComparer.Ordinal).Count()!=choices.Length
            ||editor==FieldEditorKind.Choice&&choices.Length==0||editor!=FieldEditorKind.Choice&&choices.Length!=0)
            throw new ArgumentException("Only choice editors have a nonempty closed set of unique keys.");
        ChoiceKeys=Array.AsReadOnly(choices);
        var labels=choiceLabels?.ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal)??new Dictionary<string,string>();
        if(labels.Any(item=>!choices.Contains(item.Key,StringComparer.Ordinal)||string.IsNullOrWhiteSpace(item.Value)))
            throw new ArgumentException("Choice display labels must address declared keys.");
        ChoiceLabels=new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(labels);
    }
    public FieldLayout? Layout { get; init; }
    public string FieldId{get;} public string DisplayName{get;} public string Group{get;} public int Order{get;}
    public IReadOnlyDictionary<string,string> ChoiceLabels{get;}
    public FieldEditorKind Editor{get;} public IReadOnlyList<string> ChoiceKeys{get;}
}
public sealed class ToolPresentation
{
    public ToolPresentation(IEnumerable<FieldPresentation> fields,bool hasProfessionalView=false,IEnumerable<RegionFieldGroup>? regions=null)
    {
        Fields=Array.AsReadOnly(fields.ToArray());Regions=Array.AsReadOnly((regions??Array.Empty<RegionFieldGroup>()).ToArray());HasProfessionalView=hasProfessionalView;
        if(Fields.Any(field=>field is null)||Fields.Select(field=>field.FieldId).Distinct(StringComparer.Ordinal).Count()!=Fields.Count)
            throw new ArgumentException("Presentation fields must be unique.");
    }
    public IReadOnlyList<FieldPresentation> Fields{get;} public IReadOnlyList<RegionFieldGroup> Regions{get;} public bool HasProfessionalView{get;}
    internal void Validate(IReadOnlyList<Port> inputs)
    {
        var ports=inputs.ToDictionary(port=>port.Id,StringComparer.Ordinal);
        foreach(var field in Fields)
        {
            if(!ports.TryGetValue(field.FieldId,out var port))throw new ArgumentException("Presentation references an undeclared input.");
            if(field.Editor==FieldEditorKind.Choice&&port.Type!=TypeRef.ScalarOf(ScalarKind.Text))throw new ArgumentException("Choice keys require a text declaration.");
            if(field.Editor==FieldEditorKind.Choice&&(port.Choices is null||!field.ChoiceKeys.ToHashSet(StringComparer.Ordinal).SetEquals(port.Choices.Keys)))
                throw new ArgumentException("Choice presentation must match the authoritative Port value constraint.");
            var compatible=field.Editor switch {
                FieldEditorKind.Scalar=>port.Type.Shape==ValueShape.Scalar,
                FieldEditorKind.Choice=>port.Type.Shape==ValueShape.Scalar&&port.Type.Scalar==ScalarKind.Text,
                FieldEditorKind.Record or FieldEditorKind.ResultPresentation=>port.Type.Shape==ValueShape.Record,
                FieldEditorKind.Sequence=>port.Type.Shape==ValueShape.Sequence,
                FieldEditorKind.Geometry=>IsGeometryDefinition(port.Type),
                FieldEditorKind.Region=>port.Type==TypeRef.ResourceOf(ResourceKind.Region)||IsGeometryDefinition(port.Type),
                FieldEditorKind.Image=>port.Type==TypeRef.ResourceOf(ResourceKind.Image),
                _=>false};
            if(!compatible)throw new ArgumentException("Editor kind disagrees with its declared field type.");
            if(field.Layout is {} layout)
            {
                if(string.IsNullOrWhiteSpace(layout.Page)||string.IsNullOrWhiteSpace(layout.Section)||layout.DecimalPlaces is <0 or >15)
                    throw new ArgumentException("Invalid field layout.");
                if(layout.VisibleWhen is {} condition)
                {if(!ports.TryGetValue(condition.FieldId,out var controller))throw new ArgumentException("Visibility references an undeclared field.");ValueRules.Freeze(controller,condition.Value);}
                if(layout.PairWith is {} paired&&(paired==port.Id||!ports.TryGetValue(paired,out var other)||other.Type!=port.Type||port.Type.Shape!=ValueShape.Scalar||port.Type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64)))
                    throw new ArgumentException("A range requires two distinct declared numeric fields of the same type.");
                if(layout.Minimum is {} minimum&&!double.IsFinite(minimum)||layout.Maximum is {} maximum&&!double.IsFinite(maximum)||layout.Minimum>layout.Maximum)
                    throw new ArgumentException("Invalid visual numeric bounds.");
            }

        }
        var roles=new HashSet<string>(StringComparer.Ordinal);
        foreach(var region in Regions)
        {
            if(region is null||string.IsNullOrWhiteSpace(region.Role)||!roles.Add(region.Role))throw new ArgumentException("Region roles must be unique.");
            var ids=new[]{region.ModeFieldId,region.LocalGeometryFieldId,region.GeometrySourceFieldId,region.RegionSourceFieldId};
            if(ids.Distinct(StringComparer.Ordinal).Count()!=4||ids.Any(id=>!ports.ContainsKey(id)))throw new ArgumentException("A region group requires four distinct declared inputs.");
            if(ports[region.ModeFieldId].Type!=TypeRef.ScalarOf(ScalarKind.Text)
                ||ports[region.LocalGeometryFieldId].Type!=ports[region.GeometrySourceFieldId].Type
                ||ports[region.LocalGeometryFieldId].Type.Shape!=ValueShape.Sequence
                ||!IsGeometryDefinition(ports[region.LocalGeometryFieldId].Type)
                ||ports[region.RegionSourceFieldId].Type!=TypeRef.ResourceOf(ResourceKind.Region))
                throw new ArgumentException("Region group declarations disagree with their typed roles.");
            var mode=ports[region.ModeFieldId];
            if(mode.Choices is null||!mode.Choices.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Enum.GetNames<RegionFieldMode>())
                ||(ports[region.LocalGeometryFieldId].EditModes&FieldEditModes.Constant)==0
                ||(ports[region.GeometrySourceFieldId].EditModes&FieldEditModes.Reference)==0
                ||(ports[region.RegionSourceFieldId].EditModes&FieldEditModes.Reference)==0)
                throw new ArgumentException("Region modes and editing capabilities must agree with their four typed roles.");
        }
    }
    private static bool IsGeometryDefinition(TypeRef type)=>type.Shape==ValueShape.Geometry
        ||type.Shape==ValueShape.Sequence&&IsGeometryDefinition(type.Element!)
        ||type.Shape==ValueShape.Record&&type.RecordId=="sdk.roi-shape"
            &&type.Fields.Any(field=>field.Id=="kind"&&field.Type==TypeRef.ScalarOf(ScalarKind.Text))
            &&type.Fields.Any(field=>field.Type.Shape==ValueShape.Geometry);
}
