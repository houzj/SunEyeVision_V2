using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>
/// ROI 组的领域能力。宿主只依赖这些查询与槽位填充，不假设容器内部是否有视觉嵌套，
/// 因此容器既可以是单个面板，也可以是画布与参数行各自平级的控件集合。
/// </summary>
public interface INativeRoiControls
{
    /// <summary>容器的视觉根，供宿主做可见性、加载状态与提示联动。</summary>
    FrameworkElement Element { get; }

    /// <summary>控制图形区域可见性的来源控件。</summary>
    FrameworkElement GraphicVisibilitySource { get; }

    /// <summary>指定组当前是否为本地绘制。</summary>
    bool IsLocalDrawing(string group);

    /// <summary>屏蔽区当前是否为继承。</summary>
    bool IsInheritedMask { get; }

    /// <summary>提交全部活跃来源的待定编辑；失败时给出原因。</summary>
    bool TryCaptureActiveSources(out string error);

    /// <summary>把两个组的编辑器装进对应画布槽位。</summary>
    void BindLocalEditors(NativeRoiEditor detection, NativeRoiEditor mask);

    event Action? Changed;
}
