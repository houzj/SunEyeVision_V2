using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>平台统一的参数声明加载器：把工具 XML 解析成 Schema 与字段元数据，工具不再各写一份。</summary>
public sealed class DeclaredParameterSchema
{
    private readonly IReadOnlyDictionary<string, XElement> _fields;
    private readonly IReadOnlyList<string> _fieldOrder = Array.Empty<string>();
    private readonly IReadOnlyDictionary<string, XElement> _inputs;
    private readonly IReadOnlyDictionary<string, Type> _types;

    private DeclaredParameterSchema(XElement root, IReadOnlyDictionary<string, Type> types)
    {
        // 根元素名属于工具身份，平台不据此限定；只校验声明版本。
        if ((string?)root.Attribute("schemaVersion") != "1")
            throw new InvalidDataException("Unsupported parameter metadata schema version: " + root.Name);
        Shape(root, new[] { "schemaVersion", "workflowImageInputId" }, new[] { "Inputs", "Group" });
        // 消费图像的工具声明工作流图像输入；产出图像的工具（如图像源）不声明。
        WorkflowImageInputId = (string?)root.Attribute("workflowImageInputId") ?? string.Empty;
        _types = types;

        // 输入编辑器声明可为空：图像源类工具自行产生图像，没有上游图像输入页。
        if (root.Elements("Inputs").Count() > 1)
            throw new InvalidDataException("At most one input editor declaration is allowed.");
        var inputs = root.Element("Inputs");
        if (inputs is null)
        {
            _inputs = new Dictionary<string, XElement>(StringComparer.Ordinal);
        }
        else
        {
            Shape(inputs, Array.Empty<string>(), new[] { "Input" });
            foreach (var input in inputs.Elements())
            {
                Shape(input, new[] { "id", "label", "page", "section" }, Array.Empty<string>());
                Required(input, "id"); Required(input, "label"); Required(input, "page"); Required(input, "section");
            }
            _inputs = inputs.Elements().ToDictionary(input => Required(input, "id"), StringComparer.Ordinal);
            if (_inputs.Count != inputs.Elements().Count())
                throw new InvalidDataException("Duplicate input editor identity.");
        }

        foreach (var group in root.Elements("Group"))
        {
            Shape(group, new[] { "page", "section", "advanced", "vmCategory" }, new[] { "Parameter" });
            Required(group, "page"); Required(group, "section"); Required(group, "vmCategory");
            if (group.Attribute("advanced") is not null) Boolean(group, "advanced");
            if (!group.HasElements) throw new InvalidDataException("Empty parameter group.");
            foreach (var field in group.Elements())
            {
                Shape(field, new[]
                {
                    "id", "fieldId", "allowReference", "bindingName", "type", "default", "label",
                    "minimum", "maximum", "decimals", "increment", "visibleWhen", "visibleValue",
                    "visibleNegate", "orVisibleWhen", "orVisibleValue", "orVisibleNegate",
                    "pairWith", "pairMinimum", "pairMaximum", "pairLabel", "editor",
                    "storage", "defaultPresence", "booleanTips"
                }, new[] { "Choice" });
                Required(field, "id"); Required(field, "type"); Required(field, "label");
                FieldId(field, "fieldId");
                if (field.Attribute("allowReference") is not null && Boolean(field, "allowReference")) Required(field, "bindingName");
                else if (field.Attribute("bindingName") is not null)
                    throw new InvalidDataException("Non-reference parameters cannot declare binding names.");
                foreach (var choice in field.Elements("Choice"))
                {
                    Shape(choice, new[] { "key", "label" }, Array.Empty<string>());
                    Required(choice, "key"); Required(choice, "label");
                }
            }
        }

        var ids = root.Elements("Group").Elements("Parameter").Select(field => Required(field, "id")).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new InvalidDataException("Duplicate parameter identity.");
        _fieldOrder = ids;
        _fields = root.Elements("Group").Elements("Parameter")
            .ToDictionary(field => Required(field, "id"), StringComparer.Ordinal);

        if (_fields.Keys.Any(id => !_types.ContainsKey(id)))
            throw new InvalidDataException("Parameter XML fields differ from the settings model.");

        foreach (var (id, type) in _fields.Keys.Select(id => (id, _types[id])))
        {
            // 可空包装不改变声明的种类，解包后再判定。
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            var expected = underlying == typeof(int) || underlying == typeof(long) ? "Integer"
                : underlying == typeof(double) ? "Number"
                : underlying == typeof(bool) ? "Boolean"
                : underlying == typeof(string) ? "Text"
                : underlying == typeof(IReadOnlyList<string>) ? "TextSequence"
                : underlying.IsEnum ? "Enum" : null;
            if (expected is null)
                throw new InvalidDataException("Unsupported parameter type: " + id + " (" + underlying.Name + ")");
            if (Required(_fields[id], "type") != expected)
                throw new InvalidDataException("Parameter field type differs: " + id);
            var choices = _fields[id].Elements("Choice").Select(choice => Required(choice, "key")).ToArray();
            if (underlying.IsEnum
                ? !choices.ToHashSet(StringComparer.Ordinal).SetEquals(Enum.GetNames(underlying)) || choices.Distinct().Count() != choices.Length
                : underlying != typeof(string) && choices.Length != 0)
                throw new InvalidDataException("Parameter choices differ from the declared enum: " + id);
            var minimum = Minimum(id); var maximum = Maximum(id); var step = Increment(id);
            if (minimum > maximum || !double.IsFinite(step) || step <= 0)
                throw new InvalidDataException("Invalid editor bounds: " + id);
            // 无默认值的参数由使用方按未设置处理，声明里不强制给出。
            // 序列默认由工具在设置模型里给出，声明层不解析非空列表。
            if (_fields[id].Attribute("default") is null || underlying == typeof(IReadOnlyList<string>))
            {
                if (underlying == typeof(int) || underlying == typeof(double)) { Required(_fields[id], "increment"); Required(_fields[id], "decimals"); }
                continue;
            }
            // 文本参数的默认值允许为空串，缺失才算未声明。
            var raw = (string?)_fields[id].Attribute("default") ?? string.Empty;
            try
            {
                var value = type.IsEnum ? Enum.Parse(type, raw, false) : Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
                if (type.IsEnum && !Enum.IsDefined(type, value))
                    throw new InvalidDataException("Unknown enum default: " + id);
                if (type == typeof(int) || type == typeof(long) || type == typeof(double))
                {
                    var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    if (minimum is null || maximum is null || !double.IsFinite(number) || number < minimum || number > maximum)
                        throw new InvalidDataException("Invalid numeric default or bounds: " + id);
                    Required(_fields[id], "increment"); Required(_fields[id], "decimals");
                    var decimals = DecimalPlaces(id);
                    // 整型（含 long）的元数据必须为整数且在类型范围内。
                    if (type == typeof(int) || type == typeof(long))
                    {
                        var lower = type == typeof(int) ? (double)int.MinValue : (double)long.MinValue;
                        var upper = type == typeof(int) ? (double)int.MaxValue : (double)long.MaxValue;
                        if (decimals != 0 || minimum < lower || maximum > upper
                            || minimum != Math.Truncate(minimum.Value) || maximum != Math.Truncate(maximum.Value)
                            || step != Math.Truncate(step))
                            throw new InvalidDataException("Nonintegral integer metadata: " + id);
                    }
                }
                else if (_fields[id].Attributes().Any(attribute => attribute.Name.LocalName is "minimum" or "maximum" or "decimals" or "increment"))
                    throw new InvalidDataException("Numeric metadata on nonnumeric field: " + id);
            }
            catch (Exception error) when (error is FormatException or OverflowException or ArgumentException)
            {
                throw new InvalidDataException("Invalid parameter default: " + id, error);
            }
        }

        Schema = BuildSchema();
    }

    public static DeclaredParameterSchema Load(Stream stream, Type settingsType,
        Func<PropertyInfo, bool>? includeProperty = null, params Type[] additionalTypes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(settingsType);
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        void Collect(Type owner)
        {
            foreach (var property in owner.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (owner == settingsType && includeProperty is not null && !includeProperty(property)) continue;
                types.TryAdd(property.Name, property.PropertyType);
            }
        }
        Collect(settingsType);
        foreach (var type in additionalTypes) Collect(type);
        try
        {
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1024 * 1024
            });
            var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("Empty parameter metadata.");
            return new DeclaredParameterSchema(root, types);
        }
        catch (XmlException error)
        {
            throw new InvalidDataException("Malformed parameter XML.", error);
        }
    }

    public ToolSchema Schema { get; }
    public string WorkflowImageInputId { get; }
    public IReadOnlyDictionary<string, XElement> Fields => _fields;
    public IReadOnlyList<string> InputIds => _inputs.Keys.ToArray();

    public IReadOnlyDictionary<string, string> BindingNames => _fields.Values
        .Where(field => Boolean(field, "allowReference"))
        .ToDictionary(field => FieldId(field, "fieldId"), field => Required(field, "bindingName"), StringComparer.Ordinal);

    public string SettingsFieldId(string property) => _fields.TryGetValue(property, out var field)
        ? FieldId(field, "fieldId")
        : throw new ArgumentException("Unknown setting: " + property, nameof(property));

    public Type ParameterType(string id) => _types.TryGetValue(id, out var type)
        ? type
        : throw new InvalidDataException("Unknown parameter: " + id);

    public bool Contains(string id) => _fields.ContainsKey(id);
    public string Label(string id) => Required(_fields[id], "label");
    public string Page(string id) => Required(_fields[id].Parent!, "page");
    public string Section(string id) => Required(_fields[id].Parent!, "section");
    public string VmCategory(string id) => Required(_fields[id].Parent!, "vmCategory");

    public bool Advanced(string id) => _fields[id].Parent!.Attribute("advanced") is not null
        && Boolean(_fields[id].Parent!, "advanced");

    public T Default<T>(string id)
    {
        if (ParameterType(id) != typeof(T)) throw new InvalidDataException("Requested default type differs: " + id);
        var raw = Required(_fields[id], "default");
        if (typeof(T).IsEnum)
        {
            var parsed = Enum.Parse(typeof(T), raw, false);
            if (!Enum.IsDefined(typeof(T), parsed)) throw new InvalidDataException("Unknown enum default: " + id);
            return (T)parsed;
        }
        var result = (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
        if (result is double number && !double.IsFinite(number)) throw new InvalidDataException("Non-finite default: " + id);
        return result;
    }

    public double? Minimum(string id) => Number(id, "minimum");
    public double? Maximum(string id) => Number(id, "maximum");
    public double Increment(string id) => Number(id, "increment") ?? 1;
    public int DecimalPlaces(string id) => _fields[id].Attribute("decimals") is null ? 0 : Integer(_fields[id], "decimals", 0, 15);
    public double? Number(string id, string attribute) => _fields[id].Attribute(attribute) is { } value
        ? Finite(_fields[id], attribute)
        : null;

    public FieldPresentation InputPresentation(Port port, int order)
    {
        var input = _inputs[port.Id];
        return new FieldPresentation(port.Id, Required(input, "label"), Required(input, "section"), order,
            port.Type.Resource == ResourceKind.Image ? FieldEditorKind.Image
                : port.Type.Resource == ResourceKind.Region ? FieldEditorKind.Region
                : FieldEditorKind.Record)
        {
            Layout = new FieldLayout(Required(input, "page"), Required(input, "section"))
        };
    }

    private ToolSchema BuildSchema()
    {
        var bindings = new List<(Port Port, FieldPresentation Presentation)>();
        foreach (var field in _fieldOrder.Select(id => _fields[id]))
        {
            var name = Required(field, "id");
            var type = ParameterType(name);
            var choices = field.Elements("Choice").ToDictionary(choice => Required(choice, "key"), choice => Required(choice, "label"), StringComparer.Ordinal);
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            var port = new Port(FieldId(field, "fieldId"),
                // 值类型可空、或文本且未给出默认值，在契约上都按非必填处理。
                Required: Nullable.GetUnderlyingType(type) is null
                    && !(underlyingType == typeof(string) && field.Attribute("default") is null),
                Type: underlyingType == typeof(bool) ? DataType.Boolean
                : underlyingType == typeof(int) || underlyingType == typeof(long) ? DataType.Integer
                : underlyingType == typeof(double) ? DataType.Number
                : underlyingType == typeof(IReadOnlyList<string>) ? TypeRef.SequenceOf(DataType.Text)
                : DataType.Text,
                Storage: (string?)field.Attribute("storage") switch
                {
                    "int32" => NumericStorage.Int32,
                    "int64" => NumericStorage.Int64,
                    "float32" => NumericStorage.Float32,
                    "float64" => NumericStorage.Float64,
                    null => underlyingType == typeof(int) ? NumericStorage.Int32
                        : underlyingType == typeof(long) ? NumericStorage.Int64
                        : underlyingType == typeof(double) ? NumericStorage.Float64 : null,
                    var unknown => throw new InvalidDataException("Unknown numeric storage: " + unknown)
                },
                EditModes: FieldEditModes.Constant | (field.Attribute("allowReference") is not null && Boolean(field, "allowReference") ? FieldEditModes.Reference : FieldEditModes.None),
                DefaultPresence: (string?)field.Attribute("defaultPresence") == "declared"
                    ? FieldDefaultPresence.UseDeclaredDefault : FieldDefaultPresence.Unset,
                Choices: choices.Count != 0 ? new TextChoiceConstraint(choices.Keys) : null);
            FieldCondition? condition = Condition(field, "visibleWhen", "visibleValue", "visibleNegate");
            FieldCondition? orCondition = Condition(field, "orVisibleWhen", "orVisibleValue", "orVisibleNegate");
            string? pair = null;
            if (field.Attribute("pairWith") is { } partner)
            {
                if (!_fields.TryGetValue(partner.Value, out var endpoint))
                    throw new InvalidDataException("Unknown range endpoint.");
                pair = FieldId(endpoint, "fieldId");
            }
            var layout = new FieldLayout(Page(name), Section(name), condition, pair, Advanced: Advanced(name),
                Minimum: Minimum(name), Maximum: Maximum(name), DecimalPlaces: DecimalPlaces(name),
                AllowReference: Boolean(field, "allowReference"), Increment: Increment(name),
                PairMinimum: Number(name, "pairMinimum"), PairMaximum: Number(name, "pairMaximum"),
                PairDisplayName: (string?)field.Attribute("pairLabel"), VmCategory: VmCategory(name),
                OrVisibleWhen: orCondition,
                BooleanTips: (string?)field.Attribute("booleanTips") is { Length: > 0 } tips
                    ? new BooleanStateTips(tips) : null);
            // 显式 editor 覆盖类型推断；未声明时按类型选默认编辑器。
            var editor = (string?)field.Attribute("editor") switch
            {
                null => type.IsEnum ? FieldEditorKind.Choice : FieldEditorKind.Scalar,
                "folder" => FieldEditorKind.FolderPath,
                "sequence" => FieldEditorKind.Sequence,
                "choice" => FieldEditorKind.Choice,
                "scalar" => FieldEditorKind.Scalar,
                var unknown => throw new InvalidDataException("Unknown editor kind: " + unknown)
            };
            var presentation = new FieldPresentation(port.Id, Label(name), Section(name), bindings.Count,
                editor,
                choices.Count != 0 ? choices.Keys : null, choices.Count != 0 ? choices : null)
            {
                Layout = layout
            };
            bindings.Add((port, presentation));
        }
        if (bindings.Select(binding => binding.Port.Id).Distinct(StringComparer.Ordinal).Count() != bindings.Count)
            throw new InvalidDataException("Duplicate editor field IDs.");
        return new ToolSchema(bindings.Select(binding => binding.Port), Array.Empty<Port>(),
            new ToolPresentation(bindings.Select(binding => binding.Presentation)));
    }

    /// <summary>解析一组可见性条件；when/value/negate 三属性必须成组出现。</summary>
    private FieldCondition? Condition(XElement field, string whenAttribute, string valueAttribute, string negateAttribute)
    {
        if (field.Attribute(whenAttribute) is not { } when)
        {
            if (field.Attribute(valueAttribute) is not null || field.Attribute(negateAttribute) is not null)
                throw new InvalidDataException("Incomplete visibility declaration: " + whenAttribute);
            return null;
        }
        if (!_fields.TryGetValue(when.Value, out var controller))
            throw new InvalidDataException("Unknown editor visibility source: " + when.Value);
        var controllerType = ParameterType(when.Value);
        var raw = Required(field, valueAttribute);
        // 带选项的文本参数与枚举等价，都可作为可见性控制器。
        object value = controllerType.IsEnum ? raw
            : controllerType == typeof(bool) ? Boolean(field, valueAttribute)
            : controllerType == typeof(string) ? raw
            : throw new InvalidDataException("Visibility requires a Boolean, enum, or text controller.");
        return new FieldCondition(FieldId(controller, "fieldId"), value,
            field.Attribute(negateAttribute) is not null && Boolean(field, negateAttribute));
    }

    private static string FieldId(XElement item, string attribute)
    {
        // 字段身份只需稳定且唯一：可以是 field:<Guid>（Blob 的形态），
        // 也可以是 source.kind 这类语义串（图像源的形态）。改名时机由工具自行保证。
        return Required(item, attribute);
    }

    public static void Shape(XElement element, string[] attributes, string[] children)
    {
        if (element.Name.NamespaceName.Length != 0
            || element.Attributes().Any(attribute => attribute.Name.NamespaceName.Length != 0 || !attributes.Contains(attribute.Name.LocalName))
            || element.Elements().Any(child => child.Name.NamespaceName.Length != 0 || !children.Contains(child.Name.LocalName))
            || element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)))
            throw new InvalidDataException("Unknown metadata element/attribute/content: " + element.Name);
    }

    public static string Required(XElement element, string name) => (string?)element.Attribute(name) is { } value && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidDataException("Missing/empty XML attribute: " + name);

    public static double Finite(XElement element, string name) => double.TryParse(Required(element, name),
        NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
        ? number
        : throw new InvalidDataException("Invalid finite number: " + name);

    public static int Integer(XElement element, string name, int minimum, int maximum) =>
        int.TryParse(Required(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
        && number >= minimum && number <= maximum
            ? number
            : throw new InvalidDataException("Invalid integer: " + name);

    public static bool Boolean(XElement element, string name) => bool.TryParse(Required(element, name), out var value)
        ? value
        : throw new InvalidDataException("Invalid Boolean: " + name);
}
