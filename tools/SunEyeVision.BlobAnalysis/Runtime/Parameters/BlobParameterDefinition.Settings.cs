using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public sealed partial class BlobParameterDefinition
{
    private readonly IReadOnlyList<SettingBinding> _bindings;
    private sealed record SettingBinding(Port Port,FieldPresentation Presentation,
        Func<BlobSettings,object?> Read,Func<BlobSettings,object?,BlobSettings> Write);
    public ToolSchema SettingsSchema { get; }
    public IReadOnlyDictionary<string,string> ParameterBindingNames=>new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(
        _fields.Values.Where(item=>Boolean(item,"allowReference")).ToDictionary(item=>FieldId(item,"fieldId"),
            item=>Required(item,"bindingName"),StringComparer.Ordinal));

    public string SettingsFieldId(string property)
        => _fields.TryGetValue(property,out var item)?FieldId(item,"fieldId")
            :throw new ArgumentException("Unknown Blob setting: "+property,nameof(property));
    private static string FieldId(XElement item,string attribute)
    {
        var id=Required(item,attribute);
        if(!id.StartsWith("field:",StringComparison.Ordinal)||!Guid.TryParseExact(id[6..],"N",out _))
            throw new InvalidDataException("Blob editor requires an explicit stable field ID: "+id);
        return id;
    }
    private IReadOnlyList<SettingBinding> BuildSettingsBindings()
    {
        var bindings=new List<SettingBinding>();
        foreach(var item in _fields.Values)
        {
            var name=Required(item,"id");var type=ParameterType(name);
            var settingsProperty=SettingsProperty(name);var vmProperty=VmProperty(name);
            if(settingsProperty is null&&vmProperty is null)throw new InvalidDataException("Unknown Blob parameter: "+name);
            var choices=item.Elements("Choice").ToDictionary(x=>Required(x,"key"),x=>Required(x,"label"),StringComparer.Ordinal);
            var port=new Port(FieldId(item,"fieldId"),type==typeof(bool)?DataType.Boolean:type==typeof(int)?DataType.Integer:type==typeof(double)?DataType.Number:DataType.Text,
                Storage:type==typeof(int)?NumericStorage.Int32:type==typeof(double)?NumericStorage.Float64:null,
                EditModes:FieldEditModes.Constant|(Boolean(item,"allowReference")?FieldEditModes.Reference:FieldEditModes.None),
                Choices:type.IsEnum?new TextChoiceConstraint(choices.Keys):null);
            FieldCondition? condition=null;
            if(item.Attribute("visibleWhen") is { } visible)
            {
                if(!_fields.TryGetValue(visible.Value,out var controller))throw new InvalidDataException("Unknown Blob editor visibility source.");
                var controllerType=ParameterType(visible.Value);
                var raw=Required(item,"visibleValue");
                object value=controllerType.IsEnum?raw:controllerType==typeof(bool)?Boolean(item,"visibleValue")
                    :throw new InvalidDataException("Blob visibility requires a Boolean or enum controller.");
                condition=new(FieldId(controller,"fieldId"),value,item.Attribute("visibleNegate") is not null&&Boolean(item,"visibleNegate"));
            }
            else if(item.Attribute("visibleValue") is not null||item.Attribute("visibleNegate") is not null)
                throw new InvalidDataException("Incomplete Blob visibility declaration.");
            string? pair=null;
            if(item.Attribute("pairWith") is { } partner)
            {if(!_fields.TryGetValue(partner.Value,out var endpoint))throw new InvalidDataException("Unknown Blob range endpoint.");pair=FieldId(endpoint,"fieldId");}
            var layout=new FieldLayout(Page(name),Section(name),condition,pair,Advanced:Advanced(name),Minimum:Minimum(name),Maximum:Maximum(name),
                DecimalPlaces:DecimalPlaces(name),AllowReference:Boolean(item,"allowReference"),Increment:Increment(name));
            var presentation=new FieldPresentation(port.Id,Label(name),Section(name),bindings.Count,type.IsEnum?FieldEditorKind.Choice:FieldEditorKind.Scalar,
                type.IsEnum?choices.Keys:null,type.IsEnum?choices:null){Layout=layout};
            bindings.Add(new(port,presentation,settings=>
            {
                var value=settingsProperty is not null?settingsProperty.GetValue(settings):vmProperty!.GetValue(settings.ToVmParameters());
                return type.IsEnum?value!.ToString():type==typeof(int)?(object)(long)(int)value!:value;
            },(settings,value)=>
            {
                var copy=settings.Freeze();
                object? converted=type.IsEnum?Enum.Parse(type,(string)value!,false):type==typeof(int)?checked((int)(long)value!):value;
                if(settingsProperty is not null)settingsProperty.SetValue(copy,converted);
                else
                {
                    var vm=copy.ToVmParameters();vmProperty!.SetValue(vm,converted);copy=copy with {VmParameters=vm};
                }
                return copy;
            }));
        }
        if(bindings.Select(b=>b.Port.Id).Distinct(StringComparer.Ordinal).Count()!=bindings.Count)throw new InvalidDataException("Duplicate Blob editor field IDs.");
        return bindings.AsReadOnly();
    }
    public InputValues ReadSettings(BlobSettings settings)
    {
        var diagnostics=settings.Validate();
        if(diagnostics.Count!=0)throw new ArgumentException(string.Join("; ",diagnostics.Select(item=>item.Message)));
        return new(SettingsSchema.Inputs,_bindings.ToDictionary(b=>b.Port.Id,b=>PrepareField(b,b.Read(settings)),StringComparer.Ordinal));
    }
    private static object? PrepareField(SettingBinding field,object? value)
    {
        value=ValueRules.Freeze(field.Port,value);
        if(value is long or double)
        {
            var number=Convert.ToDouble(value,CultureInfo.InvariantCulture);var layout=field.Presentation.Layout!;
            if(layout.Minimum is {} min&&number<min||layout.Maximum is {} max&&number>max)
                throw new ArgumentException("Blob settings field is outside XML editor bounds: "+field.Port.Id);
        }
        return value;
    }
    public BlobSettings MaterializeSettings(InputValues resolved)
    {
        var values = new InputValues(SettingsSchema.Inputs, resolved.CopyValues());
        var result = new BlobSettings(false);
        foreach (var field in _bindings) result = field.Write(result, PrepareField(field,values.Get<object?>(field.Port.Id)));
        var diagnostics = result.Validate();
        if (diagnostics.Count != 0) throw new ArgumentException(string.Join("; ", diagnostics.Select(item => item.Message)));
        return result.Freeze();
    }
    public BlobSettings ApplySettings(BlobSettings settings,IReadOnlyDictionary<string,object?> changes)
    {
        var copy=settings.Freeze();
        foreach(var change in changes)
        {
            var field=_bindings.SingleOrDefault(b=>b.Port.Id==change.Key)??throw new ArgumentException("Unknown Blob settings field: "+change.Key);
            var value=PrepareField(field,change.Value);
            copy=field.Write(copy,value);
        }
        return copy.Freeze();
    }
}
