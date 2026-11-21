using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>
/// 原厂 <c>IMVSColorExtractModuAlgorithmTab.xml</c> 的 ResultShow 页图像显示项：
/// CheckZone（ROI）、FixtureInitPoint、FixtureRunPoint、CheckZoneAnnular。
/// 颜色与线宽取自 <c>IMVSColorExtractModuDisplay.xml</c>。
/// </summary>
public static class ColorExtractResultDisplay
{
    public const string CheckZone = "display.check-zone";
    public const string FixtureInitPoint = "display.fixture-init-point";
    public const string FixtureRunPoint = "display.fixture-run-point";
    public const string CheckZoneAnnular = "display.check-zone-annular";

    public static string Field(string group, string role) => group + "." + role;

    private sealed record Definition(Port Port, FieldPresentation Presentation, object Value);
    private static readonly IReadOnlyList<Definition> Definitions = Build();

    public static ToolSchema Schema { get; } = new(Definitions.Select(item => item.Port), Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(item => item.Presentation)));

    public static InputValues Defaults()
        => new(Schema.Inputs, Definitions.ToDictionary(item => item.Port.Id, item => (object?)item.Value, StringComparer.Ordinal));

    private static IReadOnlyList<Definition> Build()
    {
        var result = new List<Definition>();
        var order = 0;
        void Group(string group, string label, ResultDisplayColor ok, double width)
        {
            void Add(string role, string roleLabel, TypeRef type, object value, double? minimum = null,
                double? maximum = null, double increment = 1, int? decimals = null)
            {
                var id = Field(group, role);
                result.Add(new(new Port(id, type, EditModes: FieldEditModes.Constant),
                    new(id, roleLabel, "图像显示", order++,
                        type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar)
                    {
                        Layout = new FieldLayout("结果显示", "图像显示", Minimum: minimum, Maximum: maximum,
                            Increment: increment, DecimalPlaces: type == DataType.Number ? decimals ?? 2 : 0,
                            AllowReference: false, DialogGroup: group, DialogTitle: label, DialogPrimary: role == "visible")
                    }, value));
            }
            Add("visible", label, DataType.Boolean, true);
            Add("ok", "OK颜色", PresentationDeclarations.Rgba, PresentationDeclarations.Color(ok.Red, ok.Green, ok.Blue));
            Add("ng", "NG颜色", PresentationDeclarations.Rgba, PresentationDeclarations.Color(255, 0, 0));
            Add("opacity", "透明度", DataType.Number, 1d, 0, 1, increment: 0.01);
            Add("width", "线宽", DataType.Number, width, 1, 10);
        }
        Group(CheckZone, "检测区域", new ResultDisplayColor(0, 0, 255), 2);
        Group(FixtureInitPoint, "基准点", new ResultDisplayColor(102, 255, 0), 1);
        Group(FixtureRunPoint, "运行点", new ResultDisplayColor(255, 0, 0), 1);
        Group(CheckZoneAnnular, "扇环区域", new ResultDisplayColor(0, 0, 255), 2);
        return result.AsReadOnly();
    }
}
