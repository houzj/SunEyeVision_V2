using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class RectROISubstitute : ROISubstitute, IComponentConnector
{
	public static readonly DependencyProperty SubstituteRectProperty = DependencyProperty.Register("SubstituteRect", typeof(Rect), typeof(RectROISubstitute), new PropertyMetadata(default(Rect), OnSubstituteRectChanged));

	private RectangleGeometry RG;

	public Rect SubstituteRect
	{
		get
		{
			return (Rect)GetValue(SubstituteRectProperty);
		}
		set
		{
			SetValue(SubstituteRectProperty, value);
		}
	}

	private static void OnSubstituteRectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		RectROISubstitute rectROISubstitute = d as RectROISubstitute;
		if (rectROISubstitute.RG != null)
		{
			rectROISubstitute.RG.Rect = (Rect)e.NewValue;
		}
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		RG = Template.FindName("RG", this) as RectangleGeometry;
		RG.Rect = SubstituteRect;
	}

	public RectROISubstitute()
	{
		InitializeComponent();
	}
}
