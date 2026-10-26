using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IMatrixCircleROI))]
public class MatrixCircleROI : RectROI
{
	public static readonly DependencyProperty MProperty = DependencyProperty.Register("M", typeof(uint), typeof(MatrixCircleROI), new PropertyMetadata(uint.MaxValue));

	public static readonly DependencyProperty NProperty = DependencyProperty.Register("N", typeof(uint), typeof(MatrixCircleROI), new PropertyMetadata(uint.MaxValue));

	public static readonly DependencyProperty CircleRadiusProperty = DependencyProperty.Register("CircleRadius", typeof(double), typeof(MatrixCircleROI), new PropertyMetadata(double.NaN));

	public static readonly DependencyProperty MinCirleRadiusProperty = DependencyProperty.Register("MinCirleRadius", typeof(double), typeof(MatrixCircleROI), new PropertyMetadata(double.NaN));

	private RotateTransform rotateTransform = new RotateTransform();

	public uint M
	{
		get
		{
			return (uint)GetValue(MProperty);
		}
		set
		{
			SetValue(MProperty, value);
		}
	}

	public uint N
	{
		get
		{
			return (uint)GetValue(NProperty);
		}
		set
		{
			SetValue(NProperty, value);
		}
	}

	public double CircleRadius
	{
		get
		{
			return (double)GetValue(CircleRadiusProperty);
		}
		set
		{
			SetValue(CircleRadiusProperty, value);
		}
	}

	public double MinCirleRadius
	{
		get
		{
			return (double)GetValue(MinCirleRadiusProperty);
		}
		set
		{
			SetValue(MinCirleRadiusProperty, value);
		}
	}

	public MatrixCircleROI()
	{
		SetBinding(MProperty, new Binding("M"));
		SetBinding(NProperty, new Binding("N"));
		SetBinding(CircleRadiusProperty, new Binding("Radius")
		{
			Mode = BindingMode.TwoWay
		});
		SetBinding(MinCirleRadiusProperty, new Binding("MinRadius")
		{
			Mode = BindingMode.TwoWay
		});
	}

	public new static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		RectROI.ROISubstitute = new RectROISubstitute();
		RectROI.ROISubstitute.SubstituteRect = new Rect(ROIControlBase.mouseOffset.X, ROIControlBase.mouseOffset.Y, 0.0, 0.0);
		ROIControlBase.BaseAfterDraw(RectROI.ROISubstitute, parent);
		parent.MouseMove += RectROI.Parent_MouseMove;
		parent.MouseLeftButtonUp += RectROI.Parent_MouseLeftButtonUp;
		RectROI.GetDataFunc = RectCetData;
	}

	private static object RectCetData(Rect rect, double radio, ROIItemsPage parent)
	{
		IMatrixCircleROI matrixCircleROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as IMatrixCircleROI;
		int num = 1;
		int num2 = 1;
		if (matrixCircleROI is INormalizedROI)
		{
			num = parent.ImageWidth;
			num2 = parent.ImageHeight;
		}
		matrixCircleROI.CenterPoint = new Point(((rect.X + rect.Width / 2.0) / radio - 0.0) / (double)num, ((rect.Y + rect.Height / 2.0) / radio - 0.0) / (double)num2);
		matrixCircleROI.Width = rect.Width / radio / (double)num;
		matrixCircleROI.Height = rect.Height / radio / (double)num2;
		matrixCircleROI.M = 3u;
		matrixCircleROI.N = 3u;
		double num3 = Math.Min(rect.Width / radio, rect.Height / radio) / 4.0;
		matrixCircleROI.Radius = num3 / (double)num;
		matrixCircleROI.MinRadius = matrixCircleROI.Radius * 2.0 / 3.0;
		return matrixCircleROI;
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		MartrixCircleBackControl martrixCircleBackControl = new MartrixCircleBackControl();
		martrixCircleBackControl.SetBinding(MartrixCircleBackControl.MProperty, new Binding("M")
		{
			Source = this,
			Mode = BindingMode.OneWay
		});
		martrixCircleBackControl.SetBinding(MartrixCircleBackControl.NProperty, new Binding("N")
		{
			Source = this,
			Mode = BindingMode.OneWay
		});
		martrixCircleBackControl.SetBinding(MartrixCircleBackControl.RadiusProperty, new Binding("CircleRadius")
		{
			Source = this,
			Mode = BindingMode.TwoWay
		});
		martrixCircleBackControl.SetBinding(MartrixCircleBackControl.MinRadiusProperty, new Binding("MinCirleRadius")
		{
			Source = this,
			Mode = BindingMode.TwoWay
		});
		martrixCircleBackControl.SetBinding(MartrixCircleBackControl.StrokeThicknessProperty, new Binding("StrokeThickness")
		{
			Source = this,
			Mode = BindingMode.OneWay
		});
		martrixCircleBackControl.SetBinding(MartrixCircleBackControl.RectAngleProperty, new Binding("RectAngle")
		{
			Source = this,
			Mode = BindingMode.TwoWay
		});
		martrixCircleBackControl.SetBinding(MartrixCircleBackControl.ControlToImageRadioProperty, new Binding("ControlToImageRadio")
		{
			Source = this,
			Mode = BindingMode.OneWay
		});
		move.Children.Insert(0, martrixCircleBackControl);
		martrixCircleBackControl.ControlMouseLeftButtonDown = (MouseButtonEventHandler)Delegate.Combine(martrixCircleBackControl.ControlMouseLeftButtonDown, new MouseButtonEventHandler(Move_MouseLeftButtonDown));
	}

	protected override void ExternMouseMove(string icName, Point curPoint, Vector offSet, ref Point clickPoint)
	{
		if (!(icName == "SizeRect"))
		{
			if (icName == "InnerSizeRect")
			{
				Point containerPosition = ContainerPosition;
				rotateTransform.Angle = RectAngle;
				Point point = rotateTransform.Transform(containerPosition);
				Vector vector = curPoint - point;
				vector.Normalize();
				double num = MinCirleRadius + offSet * vector;
				if (num <= 0.5 || MinCirleRadius > CircleRadius)
				{
					MinCirleRadius = CircleRadius;
					return;
				}
				MinCirleRadius = num;
				clickPoint = curPoint;
			}
		}
		else
		{
			Point containerPosition2 = ContainerPosition;
			rotateTransform.Angle = RectAngle;
			Point point2 = rotateTransform.Transform(containerPosition2);
			Vector vector2 = curPoint - point2;
			vector2.Normalize();
			double num2 = CircleRadius + offSet * vector2;
			if (!(num2 <= 0.5))
			{
				CircleRadius = num2;
				clickPoint = curPoint;
			}
		}
	}
}
