using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Apps.UIHelper.Extern;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public class ROIControlFrame : Control, INotifyPropertyChanged
{
	public static readonly DependencyProperty ThicknessScaleProperty = DependencyProperty.Register("ThicknessScale", typeof(double), typeof(ROIControlFrame), new PropertyMetadata(1.0, OnThicknessScaleChanged));

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register("StrokeThickness", typeof(double), typeof(ROIControlFrame), new PropertyMetadata(1.0, OnStrokeThicknessChanged));

	public static readonly DependencyProperty ControlToImageRadioProperty = DependencyProperty.Register("ControlToImageRadio", typeof(double), typeof(ROIControlFrame), new PropertyMetadata(1.0));

	public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(ROIControlFrame), new PropertyMetadata(false, OnIsSelectedChanged));

	protected double dwidth = 4.0;

	private double arrowWidth = 6.0;

	public double ThicknessScale
	{
		get
		{
			return (double)GetValue(ThicknessScaleProperty);
		}
		set
		{
			SetValue(ThicknessScaleProperty, value);
		}
	}

	public double OriStrokeThickness => 2.0 / ThicknessScale;

	public double StrokeThickness
	{
		get
		{
			return (double)GetValue(StrokeThicknessProperty);
		}
		set
		{
			SetValue(StrokeThicknessProperty, value);
		}
	}

	public double ControlToImageRadio
	{
		get
		{
			return (double)GetValue(ControlToImageRadioProperty);
		}
		set
		{
			SetValue(ControlToImageRadioProperty, value);
		}
	}

	public bool IsSelected
	{
		get
		{
			return (bool)GetValue(IsSelectedProperty);
		}
		set
		{
			SetValue(IsSelectedProperty, value);
		}
	}

	public double HotStrokeThickness
	{
		get
		{
			if (IsSelected || IsMouseOver)
			{
				return StrokeThickness / 2.0;
			}
			return StrokeThickness;
		}
	}

	public double NegativeStrokeThickness => -1.0 * HotStrokeThickness;

	public Thickness MoveRectMargin => new Thickness(dwidth * NegativeStrokeThickness);

	public Thickness MoveEllipseMargin => new Thickness((dwidth + 1.0) * NegativeStrokeThickness);

	public double LabelFontSize => 14.0 * HotStrokeThickness;

	public double ArrowWidth => HotStrokeThickness * arrowWidth;

	public Thickness ArrowMargin => new Thickness(0.0 - ArrowWidth, 0.0 - ArrowWidth, 0.0 - ArrowWidth, 0.0);

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string strPropertyInfo)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(strPropertyInfo));
		}
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property == UIElement.IsMouseOverProperty)
		{
			SetStrokeThickness(this);
		}
	}

	private static void OnThicknessScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SetStrokeThickness(d);
		ROIControlFrame rOIControlFrame = d as ROIControlFrame;
		rOIControlFrame.OnPropertyChanged("OriStrokeThickness");
	}

	private static void SetStrokeThickness(DependencyObject d)
	{
		ROIControlFrame rOIControlFrame = d as ROIControlFrame;
		if (rOIControlFrame.ThicknessScale != 0.0)
		{
			rOIControlFrame.StrokeThickness = (double)((!rOIControlFrame.IsSelected && !rOIControlFrame.IsMouseOver) ? 1 : 2) / rOIControlFrame.ThicknessScale;
		}
	}

	private static void OnStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ROIControlFrame rOIControlFrame = d as ROIControlFrame;
		rOIControlFrame.OnPropertyChanged("HotStrokeThickness");
		rOIControlFrame.OnPropertyChanged("NegativeStrokeThickness");
		rOIControlFrame.OnPropertyChanged("MoveRectMargin");
		rOIControlFrame.OnPropertyChanged("MoveEllipseMargin");
		rOIControlFrame.OnPropertyChanged("LabelFontSize");
		rOIControlFrame.OnPropertyChanged("ArrowMargin");
	}

	private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SetStrokeThickness(d);
		ROIControlFrame rOIControlFrame = d as ROIControlFrame;
		SubROIItemsPage parent = rOIControlFrame.GetParent<SubROIItemsPage>();
		ImageViewBox parent2 = parent.GetParent<ImageViewBox>();
		if ((bool)e.NewValue && parent2.ROIVisible)
		{
			parent2.NotifyROISelected(parent.DataContext, rOIControlFrame.DataContext);
		}
	}

	public static Bitmap GetRotateImage(Bitmap sr, int angle)
	{
		Bitmap bitmap = new Bitmap(sr);
		angle %= 360;
		int width = bitmap.Width;
		int height = bitmap.Height;
		Rectangle rotateRectangle = GetRotateRectangle(width, height, angle);
		int width2 = rotateRectangle.Width;
		int height2 = rotateRectangle.Height;
		Bitmap bitmap2 = null;
		Graphics graphics = null;
		try
		{
			bitmap2 = new Bitmap(width2, height2);
			graphics = Graphics.FromImage(bitmap2);
			System.Drawing.Point point = new System.Drawing.Point(width2 / 2, height2 / 2);
			graphics.TranslateTransform(point.X, point.Y);
			graphics.RotateTransform(angle);
			graphics.TranslateTransform(-point.X, -point.Y);
			System.Drawing.Point point2 = new System.Drawing.Point((width2 - width) / 2, (height2 - height) / 2);
			graphics.DrawImage(bitmap, new Rectangle(point2.X, point2.Y, width, height));
			graphics.ResetTransform();
			graphics.Save();
		}
		catch (Exception ex)
		{
			throw ex;
		}
		finally
		{
			graphics?.Dispose();
		}
		return bitmap2;
	}

	public static Rectangle GetRotateRectangle(int width, int height, float angle)
	{
		double num = (double)angle * Math.PI / 180.0;
		double num2 = Math.Cos(num);
		double num3 = Math.Sin(num);
		int width2 = (int)Math.Max(Math.Abs((double)width * num2 - (double)height * num3), Math.Abs((double)width * num2 + (double)height * num3));
		int height2 = (int)Math.Max(Math.Abs((double)width * num3 - (double)height * num2), Math.Abs((double)width * num3 + (double)height * num2));
		return new Rectangle(0, 0, width2, height2);
	}
}
