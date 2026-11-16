using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>The ten canonical input leaves. Fixture and point editor ports are projections of these leaves.</summary>
public static class BlobFixtureInputs
{
    public const string PositionCorrectionModeId = "input.pose.mode";
    public const string ReferencePoint = "input.pose.reference.point";
    public const string RunPoint = "input.pose.run.point";
    private const FieldEditModes SourceModes = FieldEditModes.Constant | FieldEditModes.Reference;
    private static readonly string[] PoseNames = ["reference", "run"];
    private static readonly string[] Coordinates = ["x", "y"];
    private static readonly string[] Values = ["angle", "scale-x", "scale-y"];
    private static readonly (string Id, string[] Path)[] Leaves = PoseNames.SelectMany(pose =>
        Coordinates.Select(axis => Leaf(pose, "point", axis)).Concat(Values.Select(value => Leaf(pose, value)))).ToArray();

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(PositionCorrectionModeId, DataType.Integer, false, EditModes:FieldEditModes.Constant),
        new Port(ReferencePoint, FixtureDeclarations.Point, false, EditModes:SourceModes),
        new Port(RunPoint, FixtureDeclarations.Point, false, EditModes:SourceModes)
    }.Concat(Leaves.Select(leaf => new Port(leaf.Id, DataType.Number, false, Storage:NumericStorage.Float32, EditModes:SourceModes))).ToArray());
    public static IReadOnlyList<string> LeafIds { get; } = Array.AsReadOnly(Leaves.Select(leaf => leaf.Id).ToArray());
    public static IReadOnlyList<string> LeavesFor(string projection) => Array.AsReadOnly(Members(projection).Select(member => member.Id).ToArray());
    internal static bool IsProjection(string id) => id is BlobTool.PoseInput or ReferencePoint or RunPoint;

    private static (string Id, string[] Path) Leaf(params string[] path) => (BlobTool.PoseInput + "." + string.Join(".", path), path);
    private static string[] Prefix(string id) => id switch
    {
        BlobTool.PoseInput => [],
        ReferencePoint => ["reference", "point"],
        RunPoint => ["run", "point"],
        _ => throw new ArgumentException("未知位置修正组合。", nameof(id))
    };
    private static TypeRef ProjectionType(string id) => id == BlobTool.PoseInput ? FixtureDeclarations.Info : FixtureDeclarations.Point;
    private static (string Id, string[] Path)[] Members(string id)
    {
        var prefix = Prefix(id);
        return Leaves.Where(leaf => leaf.Path.Take(prefix.Length).SequenceEqual(prefix))
            .Select(leaf => (leaf.Id, leaf.Path.Skip(prefix.Length).ToArray())).ToArray();
    }

    internal static void Expand(string id, ParameterSetting setting, IDictionary<string, ParameterSetting> changes)
    {
        var type = ProjectionType(id);
        foreach (var (leaf, path) in Members(id))
        {
            ParameterSetting value = setting switch
            {
                UnsetSetting => new UnsetSetting(),
                ConstantSetting { Value:RecordValue record } when record.Declaration == type => new ConstantSetting(ReadNumber(record, path)),
                ReferenceSetting source when Matches(source.Reference, type)
                    => source with { Reference = Reference(source.Reference, source.Reference.MemberFieldPath.Concat(path)) },
                _ => throw new ArgumentException("位置修正组合要求对应类型的单个结构化来源。")
            };
            if (changes.ContainsKey(leaf)) throw new ArgumentException("同一编辑不能重复设置位置修正叶。");
            changes.Add(leaf, value);
        }
    }

    internal static ParameterSetting Project(FieldSettings fields, string id)
    {
        var members = Members(id);
        var settings = members.Select(member => fields.Read(member.Id)).ToArray();
        if (settings.All(setting => setting is ConstantSetting))
        {
            var numbers = members.Select((member, index) => (member.Path, ((ConstantSetting)settings[index]).Value))
                .ToDictionary(item => string.Join(".", item.Path), item => (double)item.Value!, StringComparer.Ordinal);
            return new ConstantSetting(Build(ProjectionType(id), [], path => numbers[string.Join(".", path)]));
        }
        ReferenceSetting? first = null;
        string? identity = null;
        for (var i = 0; i < members.Length; i++)
        {
            var path = members[i].Path;
            if (settings[i] is not ReferenceSetting source || source.Reference.MemberFieldPath.Count < path.Length ||
                !source.Reference.MemberFieldPath.TakeLast(path.Length).SequenceEqual(path))
                return new UnsetSetting();
            var reference = Reference(source.Reference, source.Reference.MemberFieldPath.SkipLast(path.Length));
            var key = ParameterSettingCanonical.Serialize(reference);
            if (first is null)
            {
                first = source with { Reference = reference };
                identity = key;
            }
            else if (key != identity || first.Conversion != source.Conversion || first.OriginId != source.OriginId ||
                first.RebindingReason != source.RebindingReason)
                return new UnsetSetting();
        }
        return first is not null && Matches(first.Reference, ProjectionType(id))
            ? first : new UnsetSetting();
    }

    internal static RecordValue? Read(InputValues values)
    {
        if (!LeafIds.Any(values.Contains)) return null;
        return Build(FixtureDeclarations.Info, [], path => values.Get<double>(Leaf(path).Id));
    }

    private static RecordValue Build(TypeRef type, string[] prefix, Func<string[], double> read)
    {
        var fields = new Dictionary<string, object?>();
        foreach (var port in type.Fields)
        {
            string[] path = [..prefix, port.Id];
            fields.Add(port.Id, port.Type.Shape == ValueShape.Record ? Build(port.Type, path, read) : read(path));
        }
        return new(type, fields);
    }

    private static double ReadNumber(RecordValue record, IReadOnlyList<string> path)
    {
        foreach (var member in path.Take(path.Count - 1)) record = record.Get<RecordValue>(member);
        return record.Get<double>(path[^1]);
    }

    private static OutputReference Reference(OutputReference reference, IEnumerable<string> path)
        => new(reference.NodeId, reference.PortId, reference.Type, path, reference.Selection, reference.ScopeId, reference.Storage);

    private static bool Matches(OutputReference reference, TypeRef type)
    {
        var selected = ParameterReferenceRules.SelectedType(reference);
        return selected == type || selected.Shape == ValueShape.Sequence && selected.Element == type;
    }
}
