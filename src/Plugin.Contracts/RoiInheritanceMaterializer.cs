using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>一类 ROI 形状的继承声明：模式取值、来源字段、成员顺序与生成的形状种类。</summary>
public sealed record RoiFamilySpec(
    string ModeValue,
    string ParametersModeValue,
    string SourceFieldId,
    IReadOnlyList<string> Members,
    string ShapeKind)
{
    /// <summary>继承来源的记录类型；同一声明只创建一个实例，保证引用相等。</summary>
    public TypeRef SourceRecordType { get; } = TypeRef.RecordOf("roi.inherit." + ShapeKind,
        new[] { new Port("center", FixtureDeclarations.Point) }
            .Concat(Members.Skip(2).Select(id => new Port(id, DataType.Number, Storage: NumericStorage.Float32))).ToArray());

    /// <summary>参数模式下的字段前缀，由 ParametersModeValue 推导：RectangleParameters → input.roi.rectangle.。</summary>
    public string ParametersPrefix { get; } = "input.roi."
        + (ParametersModeValue.EndsWith("Parameters", StringComparison.Ordinal)
            ? ParametersModeValue[..^"Parameters".Length].ToLowerInvariant() + "."
            : ParametersModeValue.ToLowerInvariant() + ".");
}

/// <summary>ROI 继承声明：本地模式、两类形状家族与继承条目上限。</summary>
public sealed record RoiInheritanceSpec(
    string ModeFieldId,
    string LocalMode,
    RoiFamilySpec Rectangle,
    RoiFamilySpec Annulus,
    int MaximumInherited)
{
    public IReadOnlyList<RoiFamilySpec> Families => new[] { Rectangle, Annulus };

    public IReadOnlyList<string> AllModeValues => new[]
    {
        LocalMode, Rectangle.ModeValue, Rectangle.ParametersModeValue,
        Annulus.ModeValue, Annulus.ParametersModeValue
    };

    public string ValidateMode(string mode)
        => AllModeValues.Contains(mode, StringComparer.Ordinal)
            ? mode
            : throw new ArgumentException("ROI 继承方式无效：" + mode);
}

/// <summary>
/// 按声明把继承参数拼成 ROI 形状。对应原厂平台的 RoiManager + InputItem：
/// 工具只给字段名与模式取值，拆解与拼装都在这里完成。
/// </summary>
public static class RoiInheritanceMaterializer
{
    public static string ReadMode(InputValues inputs, RoiInheritanceSpec spec)
        => inputs.Contains(spec.ModeFieldId) ? spec.ValidateMode(inputs.Get<string>(spec.ModeFieldId)) : spec.LocalMode;

    /// <summary>从参数设置读取继承模式；未设置视为本地绘制，非固定值直接拒绝。</summary>
    public static string ReadMode(ParameterSetting setting, RoiInheritanceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return setting switch
        {
            UnsetSetting => spec.LocalMode,
            ConstantSetting { Value: string mode } => spec.ValidateMode(mode),
            _ => throw new ArgumentException("ROI 继承方式必须是固定模式。")
        };
    }

    public static IReadOnlyList<Port> DeclarePorts(RoiInheritanceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var ports = new List<Port>
        {
            new(spec.ModeFieldId, DataType.Text, Required: false, EditModes: FieldEditModes.Constant)
        };
        // 先声明两族的来源字段，再声明各自的逐成员参数，保持既有端口顺序稳定。
        foreach (var family in spec.Families)
            ports.Add(new Port(family.SourceFieldId, TypeRef.SequenceOf(family.SourceRecordType), Required: false,
                EditModes: FieldEditModes.Reference));
        foreach (var family in spec.Families)
            foreach (var member in family.Members)
                ports.Add(new Port(LeafId(family, member), DataType.NumberArray, Required: false,
                    EditModes: FieldEditModes.Reference));
        return ports.AsReadOnly();
    }

    /// <summary>把继承来源物化成区域文档；本地模式原样返回。</summary>
    public static RecordValue Materialize(InputValues inputs, RoiInheritanceSpec spec, RecordValue? local)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(spec);
        var mode = ReadMode(inputs, spec);
        if (mode == spec.LocalMode) return local ?? RoiDocumentValue.Empty;

        var family = spec.Families.FirstOrDefault(item =>
            item.ModeValue == mode || item.ParametersModeValue == mode)
            ?? throw new ArgumentException("ROI 继承方式无效：" + mode);
        var combined = family.ModeValue == mode;
        var definition = local ?? RoiDocumentValue.Empty;
        RoiDocumentValue.Validate(definition);

        var source = combined ? inputs.Read(family.SourceFieldId) : null;
        var leaves = combined
            ? null
            : family.Members.ToDictionary(member => member, member => inputs.Read(LeafId(family, member)), StringComparer.Ordinal);
        if (source is { IsMissing: true } || leaves?.Values.Any(reader => reader.IsMissing) == true)
            throw new ArgumentException("请选择完整的 ROI 继承来源。");
        var count = source?.Count ?? leaves!.Values.Min(reader => reader.Count);
        if (count == 0) throw new ArgumentException("ROI 继承来源没有有效数据。");

        var rows = new List<RecordValue>();
        for (var index = 0; index < Math.Min(spec.MaximumInherited, count); index++)
        {
            var element = source?.GetElement(index);
            double Number(string member)
            {
                var value = element is not null
                    ? ShapeMember(element, member).GetScalar<double>()
                    : leaves![member].GetElement(index).GetScalar<double>();
                if (!double.IsFinite(value)) throw new ArgumentException("ROI 继承参数必须有限。");
                return value;
            }
            var values = new Dictionary<string, object?>
            {
                ["kind"] = family.ShapeKind,
                ["center"] = RoiDocumentValue.Point(Number("center-x"), Number("center-y"))
            };
            if (family.ShapeKind == "rectangle")
            {
                values["width"] = Number("width");
                values["height"] = Number("height");
                values["angle-degrees"] = Number("angle-degrees");
            }
            else
            {
                var start = RoiDocumentValue.NormalizeDegrees(Number("start-degrees")) * Math.PI / 180d;
                var sweep = Number("sweep-degrees");
                if (Math.Abs(sweep) > 360) throw new ArgumentException("圆环继承角度范围不能超过 360 度。");
                values["inner-radius"] = Number("inner-radius");
                values["outer-radius"] = Number("outer-radius");
                values["start-radians"] = start;
                values["end-radians"] = start + sweep * Math.PI / 180d;
            }
            var shape = new RecordValue(RoiDocumentValue.ShapeType, values);
            RoiDocumentValue.ValidateShape(shape);
            rows.Add(RoiDocumentValue.Item(InheritedIdentity(index), "继承区域" + (index + 1), shape));
        }
        return RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit, rows),
            definition.Get<RecordValue>(RoiDocumentValue.Mask));
    }

    /// <summary>切换模式时需要清空的字段：另一族来源与该族逐成员参数。</summary>
    public static IEnumerable<string> SourcesToClear(RoiInheritanceSpec spec, string mode)
    {
        ArgumentNullException.ThrowIfNull(spec);
        foreach (var family in spec.Families)
        {
            var keeps = family.ModeValue == mode || family.ParametersModeValue == mode;
            if (keeps) continue;
            yield return family.SourceFieldId;
            foreach (var member in family.Members) yield return LeafId(family, member);
        }
    }

    /// <summary>当前模式下真正生效的输入字段。</summary>
    public static IReadOnlyList<string> ActiveInputs(InputValues inputs, RoiInheritanceSpec spec, IEnumerable<Port> ports)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var mode = ReadMode(inputs, spec);
        var own = DeclarePorts(spec).Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal) { spec.ModeFieldId };
        foreach (var family in spec.Families)
        {
            if (family.ModeValue == mode) active.Add(family.SourceFieldId);
            else if (family.ParametersModeValue == mode)
                foreach (var member in family.Members) active.Add(LeafId(family, member));
        }
        return Array.AsReadOnly(ports.Where(port => !own.Contains(port.Id) || active.Contains(port.Id))
            .Select(port => port.Id).ToArray());
    }

    public static string LeafId(RoiFamilySpec family, string member)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(member);
        return family.ParametersPrefix + member;
    }


    private static DeclaredValueReader ShapeMember(DeclaredValueReader shape, string member) => member switch
    {
        "center-x" => shape.GetField("center").GetField("x"),
        "center-y" => shape.GetField("center").GetField("y"),
        _ => shape.GetField(member)
    };

    /// <summary>继承条目的稳定身份：固定命名空间 + 序号，保证同一来源重算后身份不变。</summary>
    private static Guid InheritedIdentity(int index)
    {
        var bytes = new Guid("c8a845d3-1695-4761-ac6b-165fe86dc248").ToByteArray();
        BitConverter.GetBytes(index).CopyTo(bytes, 12);
        return new Guid(bytes);
    }
}
