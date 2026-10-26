using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using Apps.Log;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IPolylineROI))]
public class PolylineROI : ROIControlBase
{
	private static PolylineROISubstitute ROISubstitute;

	private static int PolygonPointCount;

	[CompilerGenerated]
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private static int MaxPolygonPointNum__BackingField;

	private static bool isDrawFinished;

	private static bool isDrawOnePoint;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty PolylinePointsProperty;

	public static readonly DependencyProperty PointsProperty;

	private Grid move;

	private Polyline mPolygon;

	private Point clickPoint;

	private ImageViewItemsPage parent;

	private MoveAreaPage Rects;

	public static int MaxPolygonPointNum
	{
		[CompilerGenerated]
		get
		{
			return MaxPolygonPointNum__BackingField;
		}
		[CompilerGenerated]
		set
		{
			MaxPolygonPointNum__BackingField = value;
		}
	}

	public double ContainerWidth
	{
		get
		{
			return (double)GetValue(ContainerWidthProperty);
		}
		set
		{
			SetValue(ContainerWidthProperty, value);
		}
	}

	public double ContainerHeight
	{
		get
		{
			return (double)GetValue(ContainerHeightProperty);
		}
		set
		{
			SetValue(ContainerHeightProperty, value);
		}
	}

	public ObservableCollection<Point> PolylinePoints
	{
		get
		{
			return (ObservableCollection<Point>)GetValue(PolylinePointsProperty);
		}
		set
		{
			SetValue(PolylinePointsProperty, value);
		}
	}

	public PointCollection Points
	{
		get
		{
			return (PointCollection)GetValue(PointsProperty);
		}
		set
		{
			SetValue(PointsProperty, value);
		}
	}

	public new double NegativeStrokeThickness => -1.0 * HotStrokeThickness;

	public new Thickness MoveRectMargin => new Thickness(dwidth * NegativeStrokeThickness);

	static PolylineROI()
	{
		MaxPolygonPointNum__BackingField = int.MaxValue;
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(PolylineROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(PolylineROI), new PropertyMetadata(0.0));
		PolylinePointsProperty = DependencyProperty.Register("PolylinePoints", typeof(ObservableCollection<Point>), typeof(PolylineROI), new PropertyMetadata(null, OnPolygonPointsChanged));
		PointsProperty = DependencyProperty.Register("Points", typeof(PointCollection), typeof(PolylineROI), new PropertyMetadata(null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PolylineROI), new FrameworkPropertyMetadata(typeof(PolylineROI)));
	}

	~PolylineROI()
	{
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		isDrawFinished = false;
		isDrawOnePoint = false;
		PolygonPointCount = 0;
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new PolylineROISubstitute();
		ROISubstitute.Points = new PointCollection();
		ROISubstitute.Points.Add(ROIControlBase.mouseOffset);
		ROISubstitute.Points.Add(ROIControlBase.mouseOffset);
		PolygonPointCount = 2;
		ROIControlBase.BaseAfterDraw(ROISubstitute, parent);
		parent.MouseMove += Parent_MouseMove;
		parent.MouseLeftButtonUp += Parent_MouseLeftButtonUp;
		parent.MouseLeftButtonDown += Parent_MouseLeftButtonDown;
		parent.MouseRightButtonUp += Parent_MouseRightButtonUp;
	}

	private static void Parent_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (PolygonPointCount > 0)
		{
			PolygonPointCount--;
			if (PolygonPointCount > 0)
			{
				ROISubstitute?.Points?.RemoveAt(PolygonPointCount - 1);
			}
			e.Handled = true;
		}
	}

	private static void Parent_MouseMove(object sender, MouseEventArgs e)
	{
		if (ROISubstitute != null && PolygonPointCount > 0)
		{
			Point position = e.GetPosition(sender as IInputElement);
			ROISubstitute.Points[PolygonPointCount - 1] = position;
			e.Handled = true;
		}
	}

	private static bool CheckRaised(Point p1, Point p2, Point p3, Point p4, Point pCur)
	{
		CalLineFormula(p1, p2, out var A, out var B, out var C);
		double num = A * pCur.X + B * pCur.Y + C;
		double num2 = A * p4.X + B * p4.Y + C;
		if (num * num2 <= 0.0)
		{
			return false;
		}
		CalLineFormula(p3, p4, out var A2, out var B2, out var C2);
		double num3 = A2 * pCur.X + B2 * pCur.Y + C2;
		double num4 = A2 * p1.X + B2 * p1.Y + C2;
		if (num3 * num4 <= 0.0)
		{
			return false;
		}
		CalLineFormula(p1, p4, out var A3, out var B3, out var C3);
		double num5 = A3 * p3.X + B3 * p3.Y + C3;
		double num6 = A3 * pCur.X + B3 * pCur.Y + C3;
		if (num5 * num6 >= 0.0)
		{
			return false;
		}
		return true;
	}

	private static bool CalLineFormula(Point p1, Point p2, out double A, out double B, out double C)
	{
		A = 0.0;
		B = 0.0;
		C = 0.0;
		if (p1.X == p2.X && p1.Y == p2.Y)
		{
			return false;
		}
		double x = p1.X;
		double y = p1.Y;
		double x2 = p2.X;
		double y2 = p2.Y;
		A = y2 - y;
		B = x - x2;
		C = y * x2 - x * y2;
		return true;
	}

	private static void Parent_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (ROISubstitute == null)
		{
			return;
		}
		if (e.ClickCount == 1)
		{
			if (PolygonPointCount + 1 > MaxPolygonPointNum)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("多边形点数不超过") + " " + MaxPolygonPointNum, MessageIcons.Warning);
				PolygonPointCount--;
				ROISubstitute.Points.RemoveAt(ROISubstitute.Points.Count - 2);
				return;
			}
			isDrawOnePoint = true;
			if (PolygonPointCount == 0)
			{
				Point position = e.GetPosition(sender as IInputElement);
				ROISubstitute.Points = new PointCollection();
				ROISubstitute.Points.Add(position);
				PolygonPointCount++;
			}
			string text = "";
			foreach (Point point in ROISubstitute.Points)
			{
				text += $"({point.X},{point.Y})";
			}
			LogHelper.Error(text);
			LogHelper.Error($"X={ROISubstitute.Points.Last().X},Y={ROISubstitute.Points.Last().Y}");
			ROISubstitute.Points.Add(ROISubstitute.Points.Last());
			PolygonPointCount++;
		}
		else if (isDrawOnePoint && PolygonPointCount > 3)
		{
			isDrawOnePoint = false;
			isDrawFinished = true;
			ROISubstitute.Points.RemoveAt(ROISubstitute.Points.Count - 1);
			PolygonPointCount--;
		}
		e.Handled = true;
	}

	private static void Parent_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (ROISubstitute == null)
		{
			return;
		}
		if (isDrawFinished)
		{
			PointCollection points = ROISubstitute.Points;
			double radio = ROISubstitute.ControlToImageRadio;
			IPolylineROI polylineROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as IPolylineROI;
			if (polylineROI.PolylinePoints == null)
			{
				ObservableCollection<Point> observableCollection = new ObservableCollection<Point>();
				foreach (Point item in points)
				{
					observableCollection.Add(new Point(item.X, item.Y));
				}
				polylineROI.PolylinePoints = observableCollection;
			}
			else
			{
				polylineROI.PolylinePoints.Clear();
				IEnumerable<Point> enumerable = points.Select((Point p) => new Point(p.X / radio - 0.0, p.Y / radio - 0.0));
				foreach (Point item2 in enumerable)
				{
					polylineROI.PolylinePoints.Add(new Point(item2.X, item2.Y));
				}
			}
			FrameworkElement frameworkElement = sender as FrameworkElement;
			frameworkElement.MouseMove -= Parent_MouseMove;
			frameworkElement.MouseLeftButtonUp -= Parent_MouseLeftButtonUp;
			frameworkElement.MouseLeftButtonDown -= Parent_MouseLeftButtonDown;
			frameworkElement.MouseRightButtonUp -= Parent_MouseRightButtonUp;
			ROIControlBase.BaseFinishDraw(ROISubstitute, frameworkElement);
			ROISubstitute = null;
		}
		e.Handled = true;
	}

	private static void OnPolygonPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		PolylineROI polylineROI = d as PolylineROI;
		if (e.OldValue != null)
		{
			CollectionChangedEventManager.RemoveHandler(e.OldValue as INotifyCollectionChanged, polylineROI.PolygonPoints_CollectionChanged);
		}
		if (e.NewValue is ObservableCollection<Point> observableCollection)
		{
			if (observableCollection.Count < 3)
			{
				throw new ArgumentException("PolygonPoints Invalid");
			}
			polylineROI.ChangeContainerSize();
			polylineROI.ChangeContainerPosition();
			polylineROI.Points = new PointCollection(observableCollection);
			CollectionChangedEventManager.AddHandler(observableCollection, polylineROI.PolygonPoints_CollectionChanged);
		}
	}

	private void PolygonPoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		ChangeContainerSize();
		ChangeContainerPosition();
		NotifyCollectionChangedAction action = e.Action;
		if (action != NotifyCollectionChangedAction.Replace)
		{
			return;
		}
		for (int i = 0; i < e.NewItems.Count; i++)
		{
			int num = Points.Select((Point p) => p.GetHashCode()).ToList().IndexOf(e.OldItems[i].GetHashCode());
			if (num >= 0)
			{
				Points[num] = (Point)e.NewItems[i];
			}
		}
		Rects?.CalculateSize();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Grid;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		Rects = Template.FindName("Rects", this) as MoveAreaPage;
		Rects.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		mPolygon = Template.FindName("mPolygon", this) as Polyline;
		mPolygon.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		parent = this.GetParent<ImageViewItemsPage>();
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		clickPoint = e.GetPosition(parent);
		frameworkElement.CaptureMouse();
		frameworkElement.MouseMove += Move_MouseMove;
		frameworkElement.MouseLeftButtonUp += Move_MouseLeftButtonUp;
		e.Handled = true;
	}

	private void Move_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton != MouseButtonState.Pressed)
		{
			return;
		}
		FrameworkElement frameworkElement = sender as FrameworkElement;
		Point position = e.GetPosition(parent);
		Vector offSet = (position - clickPoint) / ControlToImageRadio;
		string name = frameworkElement.Name;
		if (!(name == "mPolygon"))
		{
			if (name == "Rects")
			{
				int selectedIndex = Rects.SelectedIndex;
				ChangePoint(selectedIndex, offSet);
				clickPoint = position;
			}
			return;
		}
		for (int i = 0; i < PolylinePoints.Count; i++)
		{
			ChangePoint(i, offSet);
		}
		ChangeContainerPosition();
		mPolygon.InvalidateMeasure();
		mPolygon.InvalidateVisual();
		for (int j = 0; j < PolylinePoints.Count; j++)
		{
			Points[j] = PolylinePoints[j];
		}
		Rects.InvalidateMeasure();
		clickPoint = position;
	}

	private void ChangePoint(int index, Vector offSet)
	{
		double x = PolylinePoints[index].X + offSet.X;
		double y = PolylinePoints[index].Y + offSet.Y;
		PolylinePoints[index] = new Point(x, y);
	}

	private void ChangePosition(int index)
	{
		double x = PolylinePoints[index].X;
		double y = PolylinePoints[index].Y;
		mPolygon.Points[index] = new Point(x, y);
		ChangeContainerSize();
		ChangeContainerPosition();
		mPolygon.InvalidateMeasure();
		mPolygon.InvalidateVisual();
		Rects.InvalidateMeasure();
	}

	private void Move_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		frameworkElement.MouseMove -= Move_MouseMove;
		frameworkElement.MouseLeftButtonUp -= Move_MouseLeftButtonUp;
		frameworkElement.ReleaseMouseCapture();
		DataChangedCommand?.Execute(DataContext);
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		string name = e.Property.Name;
		if (!(name == "ControlToImageRadio"))
		{
			if (name == "StrokeThickness")
			{
				OnPropertyChanged("MoveRectMargin");
				OnPropertyChanged("NegativeStrokeThickness");
			}
		}
		else
		{
			ChangeContainerSize();
			ChangeContainerPosition();
		}
	}

	private void ChangeContainerSize()
	{
		if (PolylinePoints == null)
		{
			ContainerWidth = 0.0;
			ContainerHeight = 0.0;
			return;
		}
		double num = PolylinePoints.Min((Point p) => p.X);
		double num2 = PolylinePoints.Max((Point p) => p.X);
		double num3 = PolylinePoints.Min((Point p) => p.Y);
		double num4 = PolylinePoints.Max((Point p) => p.Y);
		ContainerWidth = (num2 - num) * ControlToImageRadio;
		ContainerHeight = (num4 - num3) * ControlToImageRadio;
	}

	private void ChangeContainerPosition()
	{
		if (PolylinePoints == null)
		{
			ContainerPosition = default;
			return;
		}
		double x = (PolylinePoints.Min((Point p) => p.X) + 0.0) * ControlToImageRadio;
		double y = (PolylinePoints.Min((Point p) => p.Y) + 0.0) * ControlToImageRadio;
		ContainerPosition = new Point(x, y);
	}
}
