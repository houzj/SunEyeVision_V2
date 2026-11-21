using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>
/// 原厂 <c>IMVSFrameMeanModuAlgorithmTab.xml</c> 的 ResultShow 页只有一个
/// <c>ImageViewConfig</c>（CheckZone，对应 <c>IMVSFrameMeanModuDisplay.xml</c> 的 ROI 对象：
/// okcolor=#0000ff、ngcolor=#ff0000、linewidth=2）。ROI 配置在原厂页签里被注释，
/// 因此这里只声明显示行，不声明 ROI 几何或叠加数据。
/// </summary>
public static class FrameMeanResultDisplay
{
    public const string CheckZone = "display.check-zone";
    public static string Field(string role) => CheckZone + "." + role;

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
        void Add(string role, string label, TypeRef type, object value, double? minimum = null,
            double? maximum = null, double? increment = null, int? decimals = null)
        {
            var id = Field(role);
            result.Add(new(new Port(id, type, EditModes: FieldEditModes.Constant),
                new(id, label, "图像显示", order++,
                    type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar)
                {
                    Layout = new FieldLayout("结果显示", "图像显示", Minimum: minimum, Maximum: maximum,
                        Increment: increment ?? 1,
                        DecimalPlaces: type == DataType.Number ? decimals ?? 2 : 0, AllowReference: false,
                        DialogGroup: CheckZone, DialogTitle: "检测区域", DialogPrimary: role == "visible")
                }, value));
        }
        Add("visible", "检测区域", DataType.Boolean, true);
        Add("ok", "OK颜色", PresentationDeclarations.Rgba, PresentationDeclarations.Color(0, 0, 255));
        Add("ng", "NG颜色", PresentationDeclarations.Rgba, PresentationDeclarations.Color(255, 0, 0));
        Add("opacity", "透明度", DataType.Number, 1d, 0, 1, 0.01);
        Add("width", "线宽", DataType.Number, 2d, 1, 10, 1);
        return result.AsReadOnly();
    }
}
