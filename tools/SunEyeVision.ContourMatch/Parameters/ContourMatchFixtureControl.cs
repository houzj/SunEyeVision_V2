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
}
