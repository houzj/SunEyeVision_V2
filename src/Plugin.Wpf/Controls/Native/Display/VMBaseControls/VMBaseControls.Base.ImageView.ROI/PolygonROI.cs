using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
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
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IPolygonROI))]
public class PolygonROI : ROIControlBase
{
	private static PolygonROISubstitute ROISubstitute;

	private static int PolygonPointCount;

	[CompilerGenerated]
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private static int MaxPolygonPointNum__BackingField;

	private static bool isDrawFinished;

	private static bool isDrawOnePoint;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty PolygonPointsProperty;

	public static readonly DependencyProperty PointsProperty;

	private Grid move;

	private Polygon mPolygon;

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

	public ObservableCollection<Point> PolygonPoints
	{
		get
		{
			return (ObservableCollection<Point>)GetValue(PolygonPointsProperty);
		}
		set
		{
			SetValue(PolygonPointsProperty, value);
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

	static PolygonROI()
	{
		MaxPolygonPointNum__BackingField = 32;
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(PolygonROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(PolygonROI), new PropertyMetadata(0.0));
		PolygonPointsProperty = DependencyProperty.Register("PolygonPoints", typeof(ObservableCollection<Point>), typeof(PolygonROI), new PropertyMetadata(null, OnPolygonPointsChanged));
		PointsProperty = DependencyProperty.Register("Points", typeof(PointCollection), typeof(PolygonROI), new PropertyMetadata(null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PolygonROI), new FrameworkPropertyMetadata(typeof(PolygonROI)));
	}

	~PolygonROI()
	{
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		isDrawFinished = false;
		isDrawOnePoint = false;
		PolygonPointCount = 0;
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new PolygonROISubstitute();
		ROISubstitute.Points = new PointCollection();
		ROISubstitute.Points.Add(ROIControlBase.mouseOffset);
		ROISubstitute.Points.Add(ROIControlBase.mouseOffset);
		PolygonPointCount = 2;
		ROIControlBase.BaseAfterDraw(ROISubstitute, parent);
		parent.MouseMove += Parent_MouseMove;
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
				ROISubstitute.Points.RemoveAt(PolygonPointCount - 1);
			}
			if (PolygonPointCount == 1)
			{
				FrameworkElement frameworkElement = sender as FrameworkElement;
				ImageViewBox imageViewBox = frameworkElement.GetParent<ImageViewBox>();
				imageViewBox.IsDrawingOfROI = false;
				frameworkElement.ReleaseMouseCapture();
				frameworkElement.MouseMove -= Parent_MouseMove;
				frameworkElement.MouseLeftButtonDown -= Parent_MouseLeftButtonDown;
				frameworkElement.MouseRightButtonUp -= Parent_MouseRightButtonUp;
				ROIControlBase.BaseFinishDraw(ROISubstitute, frameworkElement);
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
		}
	}

	private static void Parent_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (ROISubstitute == null)
		{
			return;
		}
		ROIItemsPage rOIItemsPage = sender as ROIItemsPage;
		if (e.ClickCount == 1)
		{
			int num = MaxPolygonPointNum;
			ImageViewBox imageViewBox;
			if ((imageViewBox = rOIItemsPage.GetParent<ImageViewBox>()) != null)
			{
				int num2 = (int)imageViewBox.GetValue(PolygonROIAttach.CustomMaxPointNumProperty);
				if (num2 > 0)
				{
					num = num2;
				}
			}
			if (PolygonPointCount > num)
			{
				rOIItemsPage.MouseLeftButtonDown -= Parent_MouseLeftButtonDown;
				ImageViewBox imageViewBox2 = rOIItemsPage.GetParent<ImageViewBox>();
				imageViewBox2.IsDrawingOfROI = false;
				rOIItemsPage.ReleaseMouseCapture();
				HikMessageBox.Show(AppLocalizationService.GetLang("多边形点数不超过") + " " + num, MessageIcons.Warning);
				imageViewBox2.IsDrawingOfROI = true;
				rOIItemsPage.CaptureMouse();
				rOIItemsPage.MouseLeftButtonDown += Parent_MouseLeftButtonDown;
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
				ImageViewBox imageViewBox3 = rOIItemsPage.GetParent<ImageViewBox>();
				imageViewBox3.IsDrawingOfROI = true;
				rOIItemsPage.CaptureMouse();
			}
			ROISubstitute.Points.Add(ROISubstitute.Points.Last());
			PolygonPointCount++;
		}
		else if (isDrawOnePoint && PolygonPointCount > 3)
		{
			isDrawOnePoint = false;
			isDrawFinished = true;
			ROISubstitute.Points.RemoveAt(ROISubstitute.Points.Count - 1);
			PolygonPointCount--;
			Parent_MouseLeftButtonUp(rOIItemsPage, e);
		}
	}

	private static void Parent_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (ROISubstitute == null || !isDrawFinished)
		{
			return;
		}
		ROIItemsPage rOIItemsPage = sender as ROIItemsPage;
		PointCollection points = ROISubstitute.Points;
		double radio = ROISubstitute.ControlToImageRadio;
		IPolygonROI polygonROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as IPolygonROI;
		int imageWidth = 1;
		int imageHeight = 1;
		if (polygonROI is INormalizedROI)
		{
			imageWidth = rOIItemsPage.ImageWidth;
			imageHeight = rOIItemsPage.ImageHeight;
		}
		if (polygonROI.PolygonPoints == null)
		{
			polygonROI.PolygonPoints = new ObservableCollection<Point>();
		}
		else
		{
			polygonROI.PolygonPoints.Clear();
		}
		IEnumerable<Point> enumerable = points.Select((Point p) => new Point((p.X / radio - 0.0) / (double)imageWidth, (p.Y / radio - 0.0) / (double)imageHeight));
		foreach (Point item in enumerable)
		{
			polygonROI.PolygonPoints.Add(new Point(item.X, item.Y));
		}
		rOIItemsPage.MouseMove -= Parent_MouseMove;
		rOIItemsPage.MouseLeftButtonDown -= Parent_MouseLeftButtonDown;
		rOIItemsPage.MouseRightButtonUp -= Parent_MouseRightButtonUp;
		ROIControlBase.CompleteDraw(polygonROI, rOIItemsPage);
		ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		ROISubstitute = null;
	}

	private static void OnPolygonPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		PolygonROI polygonROI = d as PolygonROI;
		if (e.OldValue != null)
		{
			CollectionChangedEventManager.RemoveHandler(e.OldValue as INotifyCollectionChanged, polygonROI.PolygonPoints_CollectionChanged);
		}
		if (e.NewValue is ObservableCollection<Point> observableCollection)
		{
			if (observableCollection.Count < 3)
			{
				throw new ArgumentException("PolygonPoints Invalid");
			}
			polygonROI.ChangeContainerSize();
			polygonROI.ChangeContainerPosition();
			polygonROI.Points = new PointCollection(observableCollection);
			CollectionChangedEventManager.AddHandler(observableCollection, polygonROI.PolygonPoints_CollectionChanged);
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
		mPolygon = Template.FindName("mPolygon", this) as Polygon;
		mPolygon.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		parent = this.GetParent<ImageViewItemsPage>();
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		clickPoint = e.GetPosition(parent);
		this.GetParent<ListViewItem>().RaiseEvent(e);
		frameworkElement.CaptureMouse();
		this.GetParent<ImageViewBox>().IsDragROI = true;
		frameworkElement.MouseMove += Move_MouseMove;
		frameworkElement.MouseLeftButtonUp += Move_MouseLeftButtonUp;
		e.Handled = true;
	}

	private void MoveStep(Vector vector)
	{
		for (int i = 0; i < PolygonPoints.Count; i++)
		{
			ChangePoint(i, vector);
		}
		ChangeContainerPosition();
		mPolygon.InvalidateMeasure();
		mPolygon.InvalidateVisual();
		for (int j = 0; j < PolygonPoints.Count; j++)
		{
			Points[j] = PolygonPoints[j];
		}
		Rects.InvalidateMeasure();
	}

	private void Move_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton != MouseButtonState.Pressed)
		{
			return;
		}
		_isMove = true;
		FrameworkElement frameworkElement = sender as FrameworkElement;
		Point position = e.GetPosition(parent);
		Vector vector = (position - clickPoint) / ControlToImageRadio;
		string name = frameworkElement.Name;
		if (!(name == "mPolygon"))
		{
			if (name == "Rects")
			{
				int selectedIndex = Rects.SelectedIndex;
				ChangePoint(selectedIndex, vector);
				clickPoint = position;
			}
		}
		else
		{
			MoveStep(vector);
			clickPoint = position;
		}
	}

	private void ChangePoint(int index, Vector offSet)
	{
		try
		{
			double x = PolygonPoints[index].X + offSet.X;
			double y = PolygonPoints[index].Y + offSet.Y;
			PolygonPoints[index] = new Point(x, y);
		}
		catch (Exception ex)
		{
			LogHelper.Debug(ex.StackTrace);
			LogHelper.Debug((DataContext == null) ? "数据源不在" : "数据源在");
			if (DataContext != null)
			{
				ImageViewBox imageViewBox = this.GetParent<ImageViewBox>();
				LogHelper.Debug((imageViewBox == null) ? "父控件不在" : "父控件在");
				IList rOIList;
				if (imageViewBox != null && (rOIList = imageViewBox.ROIList) != null && rOIList.Count > 0)
				{
					for (int i = 0; i < rOIList.Count; i++)
					{
						if (rOIList[i] is IList list)
						{
							LogHelper.Debug((!list.Contains(this)) ? "数据不在" : "数据在");
						}
					}
				}
			}
			throw ex;
		}
	}

	private void ChangePosition(int index)
	{
		double x = PolygonPoints[index].X;
		double y = PolygonPoints[index].Y;
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
		if (_isMove)
		{
			NotifyROIParamChanged();
			_isMove = false;
		}
		AfterMouseUp();
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
		if (PolygonPoints == null)
		{
			ContainerWidth = 0.0;
			ContainerHeight = 0.0;
			return;
		}
		double num = PolygonPoints.Min((Point p) => p.X);
		double num2 = PolygonPoints.Max((Point p) => p.X);
		double num3 = PolygonPoints.Min((Point p) => p.Y);
		double num4 = PolygonPoints.Max((Point p) => p.Y);
		ContainerWidth = (num2 - num) * ControlToImageRadio;
		ContainerHeight = (num4 - num3) * ControlToImageRadio;
	}

	private void ChangeContainerPosition()
	{
		if (PolygonPoints == null)
		{
			ContainerPosition = default;
			return;
		}
		double x = (PolygonPoints.Min((Point p) => p.X) + 0.0) * ControlToImageRadio;
		double y = (PolygonPoints.Min((Point p) => p.Y) + 0.0) * ControlToImageRadio;
		ContainerPosition = new Point(x, y);
	}

	protected override bool Move(ROIMoveDirection rOIMoveDirection)
	{
		switch (rOIMoveDirection)
		{
		case ROIMoveDirection.Left:
			MoveStep(new Vector(-1.0, 0.0));
			break;
		case ROIMoveDirection.Right:
			MoveStep(new Vector(1.0, 0.0));
			break;
		case ROIMoveDirection.Up:
			MoveStep(new Vector(0.0, -1.0));
			break;
		case ROIMoveDirection.Down:
			MoveStep(new Vector(0.0, 1.0));
			break;
		default:
			return false;
		}
		return true;
	}
}
