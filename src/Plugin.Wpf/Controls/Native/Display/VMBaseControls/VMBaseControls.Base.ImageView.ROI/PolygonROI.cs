using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
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
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

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

    private FrameworkElement? _handle;
    private ObservableCollection<Point>? _gesturePoints;
    private long _topology,_gestureTopology;
    private int _vertex=-1;
    public FrameworkElement? InteractionHandle=>_handle;
    public static readonly RoutedEvent InteractionCancelledEvent=EventManager.RegisterRoutedEvent("InteractionCancelled",RoutingStrategy.Bubble,typeof(RoutedEventHandler),typeof(PolygonROI));
    public PolygonROI()
    {
        Unloaded+=(_,_)=>CancelInteraction();
        DataContextChanged+=(_,_)=>CancelInteraction();
        AddHandler(Mouse.LostMouseCaptureEvent,new MouseEventHandler((_,args)=>{if(ReferenceEquals(args.OriginalSource,_handle)){try{CancelInteraction();}finally{RaiseEvent(new RoutedEventArgs(InteractionCancelledEvent,this));}}}),true);
    }

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
			SetCurrentValue(ContainerWidthProperty, value);
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
			SetCurrentValue(ContainerHeightProperty, value);
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
			SetCurrentValue(PolygonPointsProperty, value);
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
			SetCurrentValue(PointsProperty, value);
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

    private static void OnPolygonPointsChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var roi=(PolygonROI)d;roi.CancelInteraction();roi._topology++;
        if(e.OldValue is INotifyCollectionChanged old)CollectionChangedEventManager.RemoveHandler(old,roi.PolygonPoints_CollectionChanged);
        if(e.NewValue is INotifyCollectionChanged next)CollectionChangedEventManager.AddHandler(next,roi.PolygonPoints_CollectionChanged);
        roi.RefreshGeometryLayout();
    }
    private void PolygonPoints_CollectionChanged(object? sender,NotifyCollectionChangedEventArgs e)
    {
        if(!ReferenceEquals(sender,PolygonPoints))return;
        if(e.Action!=NotifyCollectionChangedAction.Replace){_topology++;CancelInteraction();}
        // Ordered snapshot, including duplicate points. Never identify a vertex by its value/hash.
        RefreshGeometryLayout();
    }
    public void RefreshGeometryLayout()
    {
        var snapshot=PolygonPoints?.ToArray()??Array.Empty<Point>();
        var source=PolygonPoints;var topology=_topology;var context=DataContext;
        Points=new PointCollection(snapshot);
        if(!ReferenceEquals(source,PolygonPoints)||topology!=_topology||!ReferenceEquals(context,DataContext))return;
        ChangeContainerSize();ChangeContainerPosition();Rects?.CalculateSize();
    }
    public override void OnApplyTemplate()
    {
        CancelInteraction();
        if(move is not null)move.MouseLeftButtonDown-=Move_MouseLeftButtonDown;
        if(Rects is not null)Rects.MouseLeftButtonDown-=Move_MouseLeftButtonDown;
        if(mPolygon is not null)mPolygon.MouseLeftButtonDown-=Move_MouseLeftButtonDown;
        base.OnApplyTemplate();
        move=GetTemplateChild("move") as Grid;Rects=GetTemplateChild("Rects") as MoveAreaPage;mPolygon=GetTemplateChild("mPolygon") as Polygon;
        if(move is not null)move.MouseLeftButtonDown+=Move_MouseLeftButtonDown;
        if(Rects is not null)Rects.MouseLeftButtonDown+=Move_MouseLeftButtonDown;
        if(mPolygon is not null)mPolygon.MouseLeftButtonDown+=Move_MouseLeftButtonDown;
        parent=this.GetParent<ImageViewItemsPage>();RefreshGeometryLayout();
    }
    private void Move_MouseLeftButtonDown(object sender,MouseButtonEventArgs e)
    {
        if(_handle is not null||sender is not FrameworkElement handle||parent is null||PolygonPoints is null||!IsEnabled||!IsHitTestVisible)return;
        var points=PolygonPoints;var topology=_topology;var index=ReferenceEquals(handle,Rects)?Rects.SelectedIndex:-1;
        if(ReferenceEquals(handle,Rects)&&(index<0||index>=points.Count))return;
        clickPoint=e.GetPosition(parent);this.GetParent<ListViewItem>()?.RaiseEvent(e);
        if(!IsLoaded||!ReferenceEquals(points,PolygonPoints)||topology!=_topology)return;
        _handle=handle;_gesturePoints=points;_gestureTopology=topology;_vertex=index;
        handle.MouseMove+=Move_MouseMove;handle.MouseLeftButtonUp+=Move_MouseLeftButtonUp;
        if(!handle.CaptureMouse()){CancelInteraction();return;}
        if(!Current(handle)){CancelInteraction();return;}
        if(this.GetParent<ImageViewBox>() is {} image)image.IsDragROI=true;
        e.Handled=true;
    }
    private bool Current(FrameworkElement handle)=>ReferenceEquals(_handle,handle)&&ReferenceEquals(_gesturePoints,PolygonPoints)&&_gestureTopology==_topology&&IsLoaded&&ReferenceEquals(Mouse.Captured,handle);
    private bool MoveStep(Vector vector)
    {
        var points=PolygonPoints;var topology=_topology;
        var context=DataContext;
        if(points is null)return false;
        for(var index=0;index<points.Count;index++)
        {
            if(!ReferenceEquals(context,DataContext)||!ReferenceEquals(points,PolygonPoints)||topology!=_topology)return false;
            points[index]=points[index]+vector;
        }
        if(!ReferenceEquals(context,DataContext)||!ReferenceEquals(points,PolygonPoints)||topology!=_topology)return false;
        RefreshGeometryLayout();return true;
    }
    private void Move_MouseMove(object sender,MouseEventArgs e)
    {
        if(sender is not FrameworkElement handle||!Current(handle))return;
        if(e.LeftButton!=MouseButtonState.Pressed){CancelInteraction();return;}
        if(!double.IsFinite(ControlToImageRadio)||ControlToImageRadio<=0)return;
        var position=e.GetPosition(parent);var vector=(position-clickPoint)/ControlToImageRadio;
        _isMove=true;
        if(_vertex>=0){var points=_gesturePoints!;if(_vertex>=points.Count){CancelInteraction();return;}points[_vertex]=points[_vertex]+vector;}
        else MoveStep(vector);
        if(Current(handle))clickPoint=position;
    }
    private void Move_MouseLeftButtonUp(object sender,MouseButtonEventArgs e)
    {
        if(sender is not FrameworkElement handle||!Current(handle))return;
        var changed=_isMove;var context=DataContext;var points=PolygonPoints;var topology=_topology;
        try{CancelInteraction();if(changed&&IsLoaded&&IsEnabled&&IsHitTestVisible&&ReferenceEquals(context,DataContext)&&ReferenceEquals(points,PolygonPoints)&&topology==_topology)NotifyROIParamChanged();}
        finally{AfterMouseUp();}
    }
    public void CancelInteraction()
    {
        var handle=_handle;_handle=null;_gesturePoints=null;_vertex=-1;_isMove=false;
        if(handle is not null)
        {handle.MouseMove-=Move_MouseMove;handle.MouseLeftButtonUp-=Move_MouseLeftButtonUp;if(handle.IsMouseCaptured)handle.ReleaseMouseCapture();}
        if(this.GetParent<ImageViewBox>() is {} image)image.IsDragROI=false;
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
		if (PolygonPoints == null || PolygonPoints.Count == 0)
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
		if (PolygonPoints == null || PolygonPoints.Count == 0)
		{
			SetCurrentValue(ContainerPositionProperty, default(Point));
			return;
		}
		double x = (PolygonPoints.Min((Point p) => p.X) + 0.0) * ControlToImageRadio;
		double y = (PolygonPoints.Min((Point p) => p.Y) + 0.0) * ControlToImageRadio;
		SetCurrentValue(ContainerPositionProperty, new Point(x, y));
	}

    protected override bool Move(ROIMoveDirection direction)=>direction switch
    {
        ROIMoveDirection.Left=>MoveStep(new(-1,0)),ROIMoveDirection.Right=>MoveStep(new(1,0)),
        ROIMoveDirection.Up=>MoveStep(new(0,-1)),ROIMoveDirection.Down=>MoveStep(new(0,1)),_=>false
    };
}
