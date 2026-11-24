using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>
/// 画布形状数据的公共基类，对应原件 <c>Apps.Data.ShapeDefine.Shape</c>
/// （Apps.Data.dll SHA-256 dba74d10...a88ad，TypeDef RID 95）。
/// 原件基类持有 CenterX/CenterY、Opacity、Color、FillColor、StrokeThickness、MatrixConvertEnable、
/// Tips、HighLightIndex 与 PropertyChanged；本方只保留渲染接口 IShape/IShapeBase/IMatrixEnable
/// 实际消费的成员：
/// <list type="bullet">
/// <item>CenterX/CenterY —— MicroPoint 用它合成 Point（原件 <c>MicroPoint.get_Point</c> 的显式实现）。</item>
/// <item>Stroke/Opacity —— 原件 IShapeBase。</item>
/// <item>Fill/StrokeThickness/ToolTip —— 原件 IShape。</item>
/// <item>MatrixConvertEnable —— 原件 IMatrixEnable。</item>
/// </list>
/// 不迁 PropertyChanged：MultiPointShape 只订阅三个 ObservableCollection 的 CollectionChanged，
/// 不订阅单个形状的属性变更；共享层也没有 ObservableObject（它只在 Plugin.Editing.Wpf，
/// Plugin.Wpf 不能反向依赖编辑框架）。需要刷新时由集合重建承担。
/// </summary>
public class ShapePrimitive : IShape, IMatrixEnable
{
    public double CenterX { get; set; }

    public double CenterY { get; set; }

    public string Stroke { get; set; } = "#FFFFFFFF";

    public double Opacity { get; set; } = 1;

    public string Fill { get; set; } = "Transparent";

    public double StrokeThickness { get; set; } = 1;

    public string ToolTip { get; set; } = "";

    public bool MatrixConvertEnable { get; set; }
}
