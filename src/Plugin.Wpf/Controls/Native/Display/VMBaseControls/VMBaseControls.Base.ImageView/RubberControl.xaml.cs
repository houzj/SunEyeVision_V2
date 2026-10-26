using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public partial class RubberControl : UserControl, INotifyPropertyChanged, IComponentConnector
{
	public delegate void RubberFinish(object sender, object Data);

	public delegate void Wipe(int[] Indexs, List<Point> bagPoints);

	private static SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFA5C03"));

	private Grid mainGrid;

	private Canvas RubberContainer;

	public RubberFinish OnRubberFinish;

	public Wipe OnWipe;

	private ImageViewBox owner;

	private double curX = -1.0;

	private double curY = -1.0;

	private bool IsDragging = false;

	public static readonly DependencyProperty DWidthProperty = DependencyProperty.Register("DWidth", typeof(int), typeof(RubberControl), new PropertyMetadata(0, DWidthChangeCallBack));

	public static readonly DependencyProperty DHeightProperty = DependencyProperty.Register("DHeight", typeof(int), typeof(RubberControl), new PropertyMetadata(0, DHeightChangeCallBack));

	public static readonly DependencyProperty IsDisplayAreaProperty = DependencyProperty.Register("IsDisplayArea", typeof(bool), typeof(RubberControl), new PropertyMetadata(false, IsDisplayAreaChangeCallBack));

	private Rectangle recGeometry = new Rectangle();

	private double oldEndX = -1.0;

	private double oldEndY = -1.0;

	public static SolidColorBrush SubstituteBrush => brush;

	public int DWidth
	{
		get
		{
			return (int)GetValue(DWidthProperty);
		}
		set
		{
			SetValue(DWidthProperty, value);
		}
	}

	public int DHeight
	{
		get
		{
			return (int)GetValue(DHeightProperty);
		}
		set
		{
			SetValue(DHeightProperty, value);
		}
	}

	public bool IsDisplayArea
	{
		get
		{
			return (bool)GetValue(IsDisplayAreaProperty);
		}
		set
		{
			SetValue(IsDisplayAreaProperty, value);
		}
	}

	public double TWidth
	{
		get
		{
			if (owner != null)
			{
				return (double)DWidth * owner.ControlToImageRadio * owner.ViewBoxScale * owner.ImageScale;
			}
			return 0.0;
		}
	}

	public double THeight
	{
		get
		{
			if (owner != null)
			{
				return (double)DHeight * owner.ControlToImageRadio * owner.ViewBoxScale * owner.ImageScale;
			}
			return 0.0;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private static void DWidthChangeCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as RubberControl).OnPropertyChanged("TWidth");
	}

	private static void DHeightChangeCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as RubberControl).OnPropertyChanged("THeight");
	}

	private static void IsDisplayAreaChangeCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as RubberControl).DoDisplayAreaChange();
	}

	private void DoDisplayAreaChange()
	{
		if (IsDisplayArea)
		{
			if (mainGrid == null)
			{
				mainGrid = GetParentObject<Grid>(owner.Viewbox as DependencyObject, "mainGrid");
			}
			mainGrid?.Children.Add(recGeometry);
			OnPropertyChanged("TWidth");
			OnPropertyChanged("THeight");
		}
		else
		{
			if (mainGrid == null)
			{
				mainGrid = GetParentObject<Grid>(owner.Viewbox as DependencyObject, "mainGrid");
			}
			mainGrid?.Children.Remove(recGeometry);
		}
	}

	public RubberControl()
	{
		InitializeComponent();
		recGeometry.Stroke = SubstituteBrush;
		recGeometry.StrokeThickness = 1.0;
		recGeometry.SetBinding(FrameworkElement.WidthProperty, new Binding("TWidth"));
		recGeometry.SetBinding(FrameworkElement.HeightProperty, new Binding("THeight"));
		recGeometry.DataContext = this;
	}

	public void SetSize(int width, int height)
	{
		DWidth = width;
		DHeight = height;
	}

	public void RegisterRoi(ImageViewBox imageView)
	{
		owner = imageView;
		if (owner != null)
		{
			if (RubberContainer == null)
			{
				RubberContainer = GetChildObject<Canvas>(imageView.Viewbox as DependencyObject, "mExtenView");
			}
			RubberContainer?.Children.Add(this);
			owner.MouseLeftButtonDown += owner_MouseLeftButtonDown;
			(owner.Viewbox as Viewbox).MouseLeave += viewbox_MouseLeave;
			(owner.Viewbox as Viewbox).MouseMove += owner_OnDrawRoi_MouseMoveChange;
			owner.SourceImage.MouseUp += owner_OnDrawRoi_MouseUpChange;
			owner.SourceImage.Cursor = Cursors.None;
			bd.BorderThickness = new Thickness(2.0 / owner.ImageScale);
		}
	}

	private void viewbox_MouseLeave(object sender, MouseEventArgs e)
	{
		bd.Visibility = Visibility.Collapsed;
	}

	private void owner_OnScaleChange(object sender, object e)
	{
		bd.BorderThickness = new Thickness(2.0 / owner.ImageScale);
	}

	public void UnRegisterRoi(ImageViewBox imageView)
	{
		owner = imageView;
		if (owner != null)
		{
			if (RubberContainer == null)
			{
				RubberContainer = GetChildObject<Canvas>(imageView.Viewbox as DependencyObject, "mExtenView");
			}
			RubberContainer?.Children.Remove(this);
			owner.SourceImage.Cursor = Cursors.Arrow;
			owner.MouseLeftButtonDown -= owner_MouseLeftButtonDown;
			(owner.Viewbox as Viewbox).MouseMove -= owner_OnDrawRoi_MouseMoveChange;
			owner.SourceImage.MouseUp -= owner_OnDrawRoi_MouseUpChange;
			(owner.Viewbox as Viewbox).MouseLeave -= viewbox_MouseLeave;
			owner.IsDrawingOfROI = false;
		}
	}

	private void owner_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		owner.SourceImage.CaptureMouse();
		IsDragging = true;
		wipe(curX, curY);
	}

	private void owner_OnDrawRoi_MouseUpChange(object sender, MouseEventArgs e)
	{
		owner.SourceImage.ReleaseMouseCapture();
		if (IsDragging)
		{
			IsDragging = false;
			oldEndX = -1.0;
			oldEndY = -1.0;
			if (OnRubberFinish != null)
			{
				OnRubberFinish(this, null);
			}
		}
	}

	private void owner_OnDrawRoi_MouseMoveChange(object sender, MouseEventArgs e)
	{
		double controlToImageRadio = owner.ControlToImageRadio;
		double num = owner.ImageSourceWidth;
		double num2 = owner.ImageSourceHeight;
		double num3 = owner.PixelX;
		double num4 = owner.PixelY;
		if (num3 < 0.0 || num4 < 0.0 || num3 >= num || num4 >= num2)
		{
			bd.Visibility = Visibility.Collapsed;
		}
		else
		{
			bd.Visibility = Visibility.Visible;
		}
		if ((double)DWidth * controlToImageRadio != Width)
		{
			Width = (double)DWidth * controlToImageRadio;
		}
		if ((double)DHeight * controlToImageRadio != Height)
		{
			Height = (double)DHeight * controlToImageRadio;
		}
		double num5 = (double)DHeight / 2.0;
		double num6 = (double)DWidth / 2.0;
		if (num4 - Math.Ceiling(num5 - 0.5) < 0.0)
		{
			num4 = Math.Ceiling(num5 - 0.5);
		}
		if (num3 - Math.Ceiling(num6 - 0.5) < 0.0)
		{
			num3 = Math.Ceiling(num6 - 0.5);
		}
		if (num4 + num5 > num2)
		{
			num4 = num2 - num5;
		}
		if (num3 + num6 > num)
		{
			num3 = num - num6;
		}
		double num7 = (num3 - Math.Ceiling(num6 - 0.5)) * controlToImageRadio;
		if (!double.IsInfinity(num7))
		{
			SetValue(Canvas.LeftProperty, num7);
		}
		num7 = (num4 - Math.Ceiling(num5 - 0.5)) * controlToImageRadio;
		if (!double.IsInfinity(num7))
		{
			SetValue(Canvas.TopProperty, num7);
		}
		if (IsDragging && (num3 != curX || num4 != curY))
		{
			wipe(num3, num4);
		}
		curX = num3;
		curY = num4;
	}

	private void wipe(double X, double Y)
	{
		if (oldEndX == -1.0)
		{
			oldEndX = X;
			oldEndY = Y;
		}
		int num = (int)Math.Ceiling(Math.Min(X, oldEndX) - (double)DWidth / 2.0);
		int num2 = (int)Math.Ceiling(Math.Min(Y, oldEndY) - (double)DHeight / 2.0);
		int num3 = (int)Math.Ceiling(Math.Max(X, oldEndX) + (double)DWidth / 2.0);
		int num4 = (int)Math.Ceiling(Math.Max(Y, oldEndY) + (double)DHeight / 2.0);
		int num5 = num3 - num;
		int num6 = num4 - num2;
		byte[,] array = new byte[num5, num6];
		if ((X - oldEndX) * (Y - oldEndY) < 0.0)
		{
			for (int i = 0; i < num5 - DWidth; i++)
			{
				for (int j = 0; j < num6 - DHeight; j++)
				{
					if ((num6 - DHeight + 1) * (num5 - DWidth - i) > (j + 1) * (num5 - DWidth + 1))
					{
						array[i, j] = 1;
						array[num5 - i - 1, num6 - j - 1] = 1;
					}
				}
			}
		}
		else
		{
			for (int k = DWidth; k < num5; k++)
			{
				for (int l = 0; l < num6 - DHeight; l++)
				{
					if ((k - DHeight + 1) * (num6 - DHeight + 1) > (l + 1) * (num5 - DWidth + 1))
					{
						array[k, l] = 1;
						array[num5 - k - 1, num6 - l - 1] = 1;
					}
				}
			}
		}
		int[] array2 = RemovePoints(num, num2, array);
		if (array2 != null)
		{
			DoWipe(array2);
		}
		oldEndX = X;
		oldEndY = Y;
	}

	public int[] RemovePoints(int sx, int sy, byte[,] tempByte)
	{
		if (owner.SourceImage.Source == null)
		{
			return null;
		}
		int num = (int)Math.Floor((double)owner.ImageSourceWidth);
		int num2 = (int)Math.Floor((double)owner.ImageSourceHeight);
		int length = tempByte.GetLength(0);
		int length2 = tempByte.GetLength(1);
		int num3 = ((sx >= 0) ? sx : 0);
		int num4 = sx + length;
		if (num4 > num)
		{
			num4 = num;
		}
		int num5 = ((sy >= 0) ? sy : 0);
		int num6 = sy + length2;
		if (num6 > num2)
		{
			num6 = num2;
		}
		int num7 = num4 - num3;
		int num8 = num6 - num5;
		int[] array = new int[num7 * num8];
		int num9 = 0;
		for (int i = num3 - sx; i < num7; i++)
		{
			for (int j = num5 - sy; j < num8; j++)
			{
				if (tempByte[i, j] == 0)
				{
					int num10 = i + num3 + (num5 + j) * num;
					array[num9] = num10;
					num9++;
				}
			}
		}
		int[] array2 = new int[num9];
		Buffer.BlockCopy(array, 0, array2, 0, num9 * 4);
		return array2;
	}

	public void DoWipe(int[] tempList)
	{
		if (OnWipe != null)
		{
			OnWipe(tempList, null);
		}
	}

	public static bool IsInPolygon(Point checkPoint, List<Point> polygonPoints)
	{
		bool flag = false;
		int count = polygonPoints.Count;
		int i = 0;
		int index = count - 1;
		for (; i < count; i++)
		{
			Point point = polygonPoints[i];
			Point point2 = polygonPoints[index];
			if (checkPoint.Y < point2.Y)
			{
				if (point.Y <= checkPoint.Y && (checkPoint.Y - point.Y) * (point2.X - point.X) > (checkPoint.X - point.X) * (point2.Y - point.Y))
				{
					flag = !flag;
				}
			}
			else if (checkPoint.Y < point.Y && (checkPoint.Y - point.Y) * (point2.X - point.X) < (checkPoint.X - point.X) * (point2.Y - point.Y))
			{
				flag = !flag;
			}
			index = i;
		}
		return flag;
	}

	public static T GetChildObject<T>(DependencyObject obj, string name) where T : FrameworkElement
	{
		DependencyObject dependencyObject = null;
		T val = null;
		for (int i = 0; i <= VisualTreeHelper.GetChildrenCount(obj) - 1; i++)
		{
			dependencyObject = VisualTreeHelper.GetChild(obj, i);
			if (dependencyObject is T && ((((T)dependencyObject).Name == name) | string.IsNullOrEmpty(name)))
			{
				return (T)dependencyObject;
			}
			val = GetChildObject<T>(dependencyObject, name);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	public static T GetParentObject<T>(DependencyObject obj, string name) where T : FrameworkElement
	{
		for (DependencyObject parent = VisualTreeHelper.GetParent(obj); parent != null; parent = VisualTreeHelper.GetParent(parent))
		{
			if (parent is T && ((((T)parent).Name == name) | string.IsNullOrEmpty(name)))
			{
				return (T)parent;
			}
		}
		return null;
	}
}
