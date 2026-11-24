using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 位置修正的**本模块 UI 控制字段**（纯控制，不是 FIXTURE 数据契约本体）。
/// 取证：原厂四状态截图（docs/evidence/vm-contourmatch-ui-20261121/）+ IMVSContourMatchModu.xml:52-72。
///
/// 字段 ID 唯一性约束（平台 ComposeInputEditor 按 id 缓存同一实例，DeclaredInputFieldsView.cs:161-165）：
/// 按点(mode=1)与按坐标(mode=2)即使显示同名 label，字段 ID 也必须不同，否则 Assist 切换模式时
/// 同一 DeclaredFieldRow 被 Insert 两次 → "Visual 已是另一个 Visual 的子级"异常。
/// 故两组用不同 ID 前缀（point-* / coord-*），label 保持原厂中文。
/// </summary>
public static class ContourMatchFixtureControl
{
    public const string EnabledId = "input.fixture.enabled";
    public const string ModeId = "input.fixture.mode";
    public const string Information = ContourMatchSchema.FixtureInput; // FIXTURE 端口引用（数据契约冻结）

    // 按点（mode=1）：8 行
    public const string PointBasePoint = "input.fixture.point.base-point";
    public const string PointBaseAngle = "input.fixture.point.base-angle";
    public const string PointBaseScaleX = "input.fixture.point.base-scale-x";
    public const string PointBaseScaleY = "input.fixture.point.base-scale-y";
    public const string PointRunPoint = "input.fixture.point.run-point";
    public const string PointRunAngle = "input.fixture.point.run-angle";
    public const string PointRunScaleX = "input.fixture.point.run-scale-x";
    public const string PointRunScaleY = "input.fixture.point.run-scale-y";

    // 按坐标（mode=2）：10 行
    public const string CoordBaseX = "input.fixture.coord.base-x";
    public const string CoordBaseY = "input.fixture.coord.base-y";
    public const string CoordBaseAngle = "input.fixture.coord.base-angle";
    public const string CoordBaseScaleX = "input.fixture.coord.base-scale-x";
    public const string CoordBaseScaleY = "input.fixture.coord.base-scale-y";
    public const string CoordRunX = "input.fixture.coord.run-x";
    public const string CoordRunY = "input.fixture.coord.run-y";
    public const string CoordRunAngle = "input.fixture.coord.run-angle";
    public const string CoordRunScaleX = "input.fixture.coord.run-scale-x";
    public const string CoordRunScaleY = "input.fixture.coord.run-scale-y";

    private static Port Ref(string id) => new(id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference);

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new Port[]
    {
        new(EnabledId, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant),
        new(ModeId, DataType.Integer, Required: false, EditModes: FieldEditModes.Constant),
        new(PointBasePoint, FixtureDeclarations.Point, Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new(PointRunPoint, FixtureDeclarations.Point, Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        Ref(PointBaseAngle), Ref(PointBaseScaleX), Ref(PointBaseScaleY),
        Ref(PointRunAngle), Ref(PointRunScaleX), Ref(PointRunScaleY),
        Ref(CoordBaseX), Ref(CoordBaseY), Ref(CoordBaseAngle), Ref(CoordBaseScaleX), Ref(CoordBaseScaleY),
        Ref(CoordRunX), Ref(CoordRunY), Ref(CoordRunAngle), Ref(CoordRunScaleX), Ref(CoordRunScaleY)
    });

    private static readonly string[] PointIds =
    {
        PointBasePoint, PointBaseAngle, PointBaseScaleX, PointBaseScaleY,
        PointRunPoint, PointRunAngle, PointRunScaleX, PointRunScaleY
    };

    private static readonly string[] CoordinateIds =
    {
        CoordBaseX, CoordBaseY, CoordBaseAngle, CoordBaseScaleX, CoordBaseScaleY,
        CoordRunX, CoordRunY, CoordRunAngle, CoordRunScaleX, CoordRunScaleY
    };

    /// <summary>只把当前 Assist 模式的来源纳入执行快照，避免隐藏行的旧引用继续影响运行。</summary>
    public static IReadOnlyList<string> ActiveInputs(FieldSettings fields, IEnumerable<Port> inputs)
    {
        var active = new HashSet<string>(StringComparer.Ordinal) { EnabledId, ModeId };
        var enabled = fields.Declarations.ContainsKey(EnabledId)
            && fields.Read(EnabledId) is ConstantSetting { Value: true };
        var mode = fields.Declarations.ContainsKey(ModeId)
            && fields.Read(ModeId) is ConstantSetting { Value: long value } ? value : 0L;
        if (enabled && mode == 1) foreach (var id in PointIds) active.Add(id);
        if (enabled && mode == 2) foreach (var id in CoordinateIds) active.Add(id);
        var own = Ports.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        return Array.AsReadOnly(inputs.Where(port => !own.Contains(port.Id) || active.Contains(port.Id))
            .Select(port => port.Id).ToArray());
    }

    /// <summary>
    /// 按原厂一个 FIXTURE 端口的执行合同，将按信息/按点/按坐标控制字段合成为同一份姿态记录。
    /// 未启用时不消费位置修正；按信息模式直接使用上游 FIXTURE 记录。
    /// </summary>
    public static RecordValue? Read(InputValues values)
    {
        // 存量运行快照可能没有 Assist 控制字段；此时沿用冻结的 FIXTURE 端口，
        // 不能把“未声明开关”误当成“已启用但缺来源”。
        if (!values.Contains(EnabledId))
            return values.Contains(Information) ? values.Get<RecordValue?>(Information) : null;
        if (!values.Get<bool>(EnabledId)) return null;
        var mode = values.Contains(ModeId) ? values.Get<long>(ModeId) : 0L;
        try
        {
            return mode switch
            {
                0 => values.Contains(Information)
                    ? values.Get<RecordValue?>(Information)
                        ?? throw new ContourMatchCapabilityException("CONTOURMATCH_FIXTURE_REQUIRED", "已启用位置修正但修正信息为空。")
                    : throw new ContourMatchCapabilityException("CONTOURMATCH_FIXTURE_REQUIRED", "已启用位置修正但缺少修正信息来源。"),
                1 => BuildPoint(values),
                2 => BuildCoordinate(values),
                _ => throw new ContourMatchCapabilityException("CONTOURMATCH_FIXTURE_INVALID", "位置修正选择方式无效。")
            };
        }
        catch (KeyNotFoundException error)
        {
            throw new ContourMatchCapabilityException("CONTOURMATCH_FIXTURE_REQUIRED", "位置修正来源不完整：" + error.Message);
        }
    }

    private static RecordValue BuildPoint(InputValues values)
    {
        return BuildInfo(
            PointValue(values, PointBasePoint),
            Number(values, PointBaseAngle), Number(values, PointBaseScaleX), Number(values, PointBaseScaleY),
            PointValue(values, PointRunPoint),
            Number(values, PointRunAngle), Number(values, PointRunScaleX), Number(values, PointRunScaleY));
    }

    private static RecordValue BuildCoordinate(InputValues values)
    {
        return BuildInfo(
            Point(Number(values, CoordBaseX), Number(values, CoordBaseY)),
            Number(values, CoordBaseAngle), Number(values, CoordBaseScaleX), Number(values, CoordBaseScaleY),
            Point(Number(values, CoordRunX), Number(values, CoordRunY)),
            Number(values, CoordRunAngle), Number(values, CoordRunScaleX), Number(values, CoordRunScaleY));
    }

    private static RecordValue BuildInfo(RecordValue referencePoint, double referenceAngle, double referenceScaleX,
        double referenceScaleY, RecordValue runPoint, double runAngle, double runScaleX, double runScaleY)
    {
        if (referencePoint.Declaration != FixtureDeclarations.Point || runPoint.Declaration != FixtureDeclarations.Point)
            throw new ContourMatchCapabilityException("CONTOURMATCH_FIXTURE_INVALID", "位置修正点类型无效。");
        if (!double.IsFinite(referenceAngle) || !double.IsFinite(runAngle)
            || !double.IsFinite(referenceScaleX) || !double.IsFinite(referenceScaleY)
            || !double.IsFinite(runScaleX) || !double.IsFinite(runScaleY)
            || referenceScaleX <= 0 || referenceScaleY <= 0 || runScaleX <= 0 || runScaleY <= 0)
            throw new ContourMatchCapabilityException("CONTOURMATCH_FIXTURE_INVALID", "位置修正角度必须有限且尺度必须大于零。");
        return new RecordValue(FixtureDeclarations.Info, new Dictionary<string, object?>
        {
            ["reference"] = Pose(referencePoint, referenceAngle, referenceScaleX, referenceScaleY),
            ["run"] = Pose(runPoint, runAngle, runScaleX, runScaleY)
        });
    }

    private static RecordValue Pose(RecordValue point, double angle, double scaleX, double scaleY)
        => new(FixtureDeclarations.Pose, new Dictionary<string, object?>
        {
            ["point"] = point, ["angle"] = angle, ["scale-x"] = scaleX, ["scale-y"] = scaleY
        });

    private static RecordValue Point(double x, double y)
        => new(FixtureDeclarations.Point, new Dictionary<string, object?> { ["x"] = x, ["y"] = y });

    private static RecordValue PointValue(InputValues values, string id)
        => values.Contains(id) ? values.Get<RecordValue?>(id) ?? throw Missing(id) : throw Missing(id);

    private static double Number(InputValues values, string id)
    {
        if (!values.Contains(id)) throw Missing(id);
        var value = values.Get<double>(id);
        if (!double.IsFinite(value)) throw new ContourMatchCapabilityException("CONTOURMATCH_FIXTURE_INVALID", "位置修正数值必须有限：" + id);
        return value;
    }

    private static ContourMatchCapabilityException Missing(string id)
        => new("CONTOURMATCH_FIXTURE_REQUIRED", "位置修正缺少来源：" + id);
}
