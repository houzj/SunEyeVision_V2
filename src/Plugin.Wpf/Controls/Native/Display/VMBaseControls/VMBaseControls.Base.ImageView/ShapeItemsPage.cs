using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Apps.UIHelper.Extern;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

internal class ShapeItemsPage : ImageViewItemsPage
{
	public static readonly DependencyProperty ImageSourceWidthProperty;

	public static readonly DependencyProperty ImageSourceHeightProperty;

	public static readonly DependencyProperty ShapeConvertMatrixProperty;

	private double[,] InverseMatrix = null;

	private Grid shapeGrid;

	private Canvas maincanvas;

	private ShapeCanvas visualcanvas;

	private ImageViewBox imageView;

	private int shapeCount = 0;

	public int ImageSourceWidth
	{
		get
		{
			return (int)GetValue(ImageSourceWidthProperty);
		}
		set
		{
			SetValue(ImageSourceWidthProperty, value);
		}
	}

	public int ImageSourceHeight
	{
		get
		{
			return (int)GetValue(ImageSourceHeightProperty);
		}
		set
		{
			SetValue(ImageSourceHeightProperty, value);
		}
	}

	public object ShapeConvertMatrix
	{
		get
		{
			return GetValue(ShapeConvertMatrixProperty);
		}
		set
		{
			SetValue(ShapeConvertMatrixProperty, value);
		}
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		string name = e.Property.Name;
		if (!(name == "ItemsSource"))
		{
			if (name == "ThicknessScale")
			{
				OnThicknessScaleChanged();
			}
		}
		else
		{
			OnItemsSourceChanged(e);
		}
	}

	private void OnThicknessScaleChanged()
	{
		if (visualcanvas == null || visualcanvas.GetNumByType("rect") <= 0)
		{
			return;
		}
		ClearVisualShapes();
		if (!(ItemsSource is ObservableCollection<object> source))
		{
			return;
		}
		List<object> list = source.ToList();
		foreach (object item in list)
		{
			if (item is Tuple<string, object> || item is IMultiText)
			{
				AddShape(item);
			}
		}
	}

	private void OnItemsSourceChanged(DependencyPropertyChangedEventArgs e)
	{
		ClearShapes();
		if (e.OldValue is ObservableCollection<object> source)
		{
			CollectionChangedEventManager.RemoveHandler(source, ShapeListCollectionChanged);
		}
		if (e.NewValue is ObservableCollection<object> observableCollection)
		{
			AddShapes(observableCollection);
			CollectionChangedEventManager.RemoveHandler(observableCollection, ShapeListCollectionChanged);
			CollectionChangedEventManager.AddHandler(observableCollection, ShapeListCollectionChanged);
		}
	}

	private static void OnShapeConvertMatrixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d is ShapeItemsPage shapeItemsPage))
		{
			return;
		}
		if (shapeItemsPage.ShapeConvertMatrix is IMatrix { MatrixList: not null } matrix && matrix.MatrixList.Count == 9)
		{
			double[,] array = new double[3, 3];
			int num = 0;
			for (int i = 0; i < 3; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					array[i, j] = matrix.MatrixList[num];
					num++;
				}
			}
			shapeItemsPage.InverseMatrix = MatrixHelper.Inverse(array);
		}
		else
		{
			shapeItemsPage.InverseMatrix = null;
		}
	}

	private void AddShapes(IList ls)
	{
		if (maincanvas == null)
		{
			return;
		}
		foreach (object l in ls)
		{
			AddShape(l);
		}
	}

	private void ShapeListCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			AddShapes(e.NewItems);
		}
		else if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			ClearShapes();
		}
	}

	private void AddShape(object data)
	{
		if (shapeCount >= GlobalRenderVariable.ShapeRenderingMaxNumber)
		{
			return;
		}
		FrameworkElement frameworkElement = null;
		if (data is Tuple<string, object> tuple)
		{
			DrawShapes(tuple);
			shapeCount += (tuple.Item2 as List<object>)?.Count ?? 0;
		}
		else
		{
			if (data is IMultiText multiText)
			{
				shapeCount += TextShape.AddMultiText(multiText, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, visualcanvas, imageView);
				return;
			}
			if (data is IMultiPoint)
			{
				frameworkElement = new MultiPointShape();
				frameworkElement.DataContext = data;
			}
			else if (data is IRectangle rect)
			{
				frameworkElement = RectShape.GetRectGeometry(rect, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
			else if (data is IAnnularSector annular)
			{
				frameworkElement = AnnularSectorShape.GetAnnularGeometry(annular, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
			else if (data is IArrowPath arrowPath)
			{
				frameworkElement = ArrowPathShape.GetSegmentData(arrowPath, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
			else if (data is ICircle circle)
			{
				frameworkElement = CircleShape.GetCircleGeometry(circle, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
			else if (data is IEllipse ellipse)
			{
				frameworkElement = CircleShape.GetEllipseGeometry(ellipse, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
			else if (data is ICoordinateShape coordinate)
			{
				frameworkElement = CoordinateShape.GetCoordinate(coordinate, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
			else if (data is ICross)
			{
				frameworkElement = new CrossShape();
				frameworkElement.DataContext = data;
			}
			else if (data is IAngleShape angleShape)
			{
				if (angleShape is IMatrixEnable { MatrixConvertEnable: not false })
				{
					Point pixelPiont = DrawShapeHelper.GetPixelPiont(angleShape.InitialLineStartPoint, InverseMatrix);
					Point pixelPiont2 = DrawShapeHelper.GetPixelPiont(angleShape.InitialLineEndPoint, InverseMatrix);
					Point pixelPiont3 = DrawShapeHelper.GetPixelPiont(angleShape.TerminalLineStartPoint, InverseMatrix);
					Point pixelPiont4 = DrawShapeHelper.GetPixelPiont(angleShape.TerminalLineEndPoint, InverseMatrix);
					Point pixelPiont5 = DrawShapeHelper.GetPixelPiont(angleShape.Intersection, InverseMatrix);
					data = angleShape.GetAngleShape(pixelPiont, pixelPiont2, pixelPiont3, pixelPiont4, pixelPiont5, angleShape.Angle);
				}
				frameworkElement = new LineAngleShape();
				frameworkElement.DataContext = data;
			}
			else if (data is ILine line)
			{
				frameworkElement = LineShape.GetLineGeometry(line, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
			else if (data is IPoint point)
			{
				frameworkElement = PointShape.GetPointGeometry(point, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas);
			}
			else if (data is IPolygon polygon)
			{
				frameworkElement = PolygonShape.GetPolygonShape(polygon, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
			else if (data is IPolyline polyline)
			{
				frameworkElement = PolylineShape.GetPolylineGeometry(polyline, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale);
			}
		}
		if (frameworkElement != null)
		{
			maincanvas.Children.Add(frameworkElement);
			shapeCount++;
		}
	}

	private void DrawShapes(Tuple<string, object> tuple)
	{
		switch (tuple.Item1)
		{
		case "IRectangle":
			if (!(tuple.Item2 is List<object> list8))
			{
				break;
			}
			{
				foreach (object item in list8)
				{
					if (item is IRectangle rect)
					{
						RectShape.AddRectGeometry(rect, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas);
					}
				}
				break;
			}
		case "ICircle":
			if (!(tuple.Item2 is List<object> list4))
			{
				break;
			}
			{
				foreach (object item2 in list4)
				{
					if (item2 is ICircle circle)
					{
						CircleShape.AddCircleGeometry(circle, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas, InverseMatrix);
					}
				}
				break;
			}
		case "IEllipse":
			if (!(tuple.Item2 is List<object> list6))
			{
				break;
			}
			{
				foreach (object item3 in list6)
				{
					if (item3 is IEllipse ellipse)
					{
						CircleShape.AddEllipseGeometry(ellipse, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas);
					}
				}
				break;
			}
		case "IAnnularSector":
			if (!(tuple.Item2 is List<object> list10))
			{
				break;
			}
			{
				foreach (object item4 in list10)
				{
					if (item4 is IAnnularSector annular)
					{
						AnnularSectorShape.AddAnnularGeometry(annular, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas);
					}
				}
				break;
			}
		case "IPoint":
			if (!(tuple.Item2 is List<object> list2))
			{
				break;
			}
			if (list2.Count < 1000)
			{
				foreach (object item5 in list2)
				{
					if (item5 is IPoint point)
					{
						PointShape.AddPointGeometry(point, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas, InverseMatrix);
					}
				}
				break;
			}
			PointShape.AddPointsGeometry(list2, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas, InverseMatrix);
			break;
		case "ILine":
			if (!(tuple.Item2 is List<object> list9))
			{
				break;
			}
			{
				foreach (object item6 in list9)
				{
					if (item6 is ILine line)
					{
						LineShape.AddLineGeometry(line, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas, InverseMatrix);
					}
				}
				break;
			}
		case "IPolygon":
			if (!(tuple.Item2 is List<object> list7))
			{
				break;
			}
			{
				foreach (object item7 in list7)
				{
					if (item7 is IPolygon polygon)
					{
						PolygonShape.AddPolygonGeometry(polygon, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas);
					}
				}
				break;
			}
		case "IArrowPath":
			if (!(tuple.Item2 is List<object> list5))
			{
				break;
			}
			{
				foreach (object item8 in list5)
				{
					if (item8 is IArrowPath arrowPath)
					{
						ArrowPathShape.AddArrowPathGeometry(arrowPath, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas);
					}
				}
				break;
			}
		case "ICoordinateShape":
			if (!(tuple.Item2 is List<object> list3))
			{
				break;
			}
			{
				foreach (object item9 in list3)
				{
					if (item9 is ICoordinateShape coordinate)
					{
						CoordinateShape.AddCoordinateGeometry(coordinate, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas);
					}
				}
				break;
			}
		case "IPolyline":
			if (!(tuple.Item2 is List<object> list))
			{
				break;
			}
			{
				foreach (object item10 in list)
				{
					if (item10 is IPolyline polyline)
					{
						PolylineShape.AddPolylineGeometry(polyline, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, ThicknessScale, visualcanvas);
					}
				}
				break;
			}
		}
	}

	private void ClearShapes()
	{
		maincanvas?.Children.Clear();
		ClearVisualShapes();
		shapeCount = 0;
	}

	private void ClearVisualShapes()
	{
		if (shapeGrid != null && visualcanvas != null)
		{
			shapeGrid.Children.Remove(visualcanvas);
			visualcanvas = new ShapeCanvas();
			visualcanvas.IsHitTestVisible = true;
			shapeGrid.Children.Add(visualcanvas);
		}
		shapeCount = maincanvas?.Children.Count ?? 0;
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		shapeGrid = Template.FindName("shapeGrid", this) as Grid;
		maincanvas = Template.FindName("maincanvas", this) as Canvas;
		visualcanvas = Template.FindName("visualcanvas", this) as ShapeCanvas;
		try
		{
			imageView = this.GetParent<ImageViewBox>();
		}
		catch (Exception)
		{
		}
		if (!(ItemsSource is ObservableCollection<object> source))
		{
			return;
		}
		List<object> list = source.ToList();
		if (maincanvas == null)
		{
			return;
		}
		foreach (object item in list)
		{
			AddShape(item);
		}
	}

	static ShapeItemsPage()
	{
		ImageSourceWidthProperty = DependencyProperty.Register("ImageSourceWidth", typeof(int), typeof(ShapeItemsPage), new PropertyMetadata(0));
		ImageSourceHeightProperty = DependencyProperty.Register("ImageSourceHeight", typeof(int), typeof(ShapeItemsPage), new PropertyMetadata(0));
		ShapeConvertMatrixProperty = DependencyProperty.Register("ShapeConvertMatrix", typeof(object), typeof(ShapeItemsPage), new PropertyMetadata(null, OnShapeConvertMatrixChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ShapeItemsPage), new FrameworkPropertyMetadata(typeof(ShapeItemsPage)));
	}
}
