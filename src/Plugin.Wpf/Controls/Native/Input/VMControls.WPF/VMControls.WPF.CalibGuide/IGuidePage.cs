namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.CalibGuide;

public interface IGuidePage
{
	object Owner { get; set; }

	object CurDiagram { get; set; }

	bool CanChangeMode { get; set; }
}
