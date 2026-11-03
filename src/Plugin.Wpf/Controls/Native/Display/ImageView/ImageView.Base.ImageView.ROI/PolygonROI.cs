using SunEyeVision.Plugin.Wpf.Interop.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
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
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VisualElements;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.ROI;

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

    private ObservableCollection<Point>? _gesturePoints;
    private long _topology;
    private int _vertex=-1;
    public FrameworkElement? InteractionHandle=>RoiInteractionHandle;
    public static readonly RoutedEvent InteractionCancelledEvent=EventManager.RegisterRoutedEvent("InteractionCancelled",RoutingStrategy.Bubble,typeof(RoutedEventHandler),typeof(PolygonROI));

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
				NativeMessageBox.Show(AppLocalizationService.GetLang("多边形点数不超过") + " " + num, MessageIcons.Warning);
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
        var roi=(PolygonROI)d;var topology=++roi._topology;
        if(e.OldValue is INotifyCollectionChanged old)CollectionChangedEventManager.RemoveHandler(old,roi.PolygonPoints_CollectionChanged);
        roi.CancelInteraction();
        if(topology!=roi._topology||!ReferenceEquals(e.NewValue,roi.PolygonPoints))return;
        if(e.NewValue is INotifyCollectionChanged next)CollectionChangedEventManager.AddHandler(next,roi.PolygonPoints_CollectionChanged);
        roi.RefreshGeometryLayout();
    }
    private void PolygonPoints_CollectionChanged(object? sender,NotifyCollectionChangedEventArgs e)
    {
        if(!ReferenceEquals(sender,PolygonPoints))return;
        var topology=_topology;
        if(e.Action!=NotifyCollectionChangedAction.Replace){topology=++_topology;CancelInteraction();}
        if(topology!=_topology||!ReferenceEquals(sender,PolygonPoints))return;
        // Ordered snapshot, including duplicate points. Never identify a vertex by its value/hash.
        RefreshGeometryLayout();
    }
    public void RefreshGeometryLayout()
    {
        var source=PolygonPoints;var topology=_topology;var context=DataContext;var request=RoiRequestVersion;
        var ratio=ControlToImageRadio;var snapshot=source?.ToArray()??Array.Empty<Point>();var proof=CaptureRoiUpdateProof();
        bool Current()=>ReferenceEquals(source,PolygonPoints)&&topology==_topology&&ReferenceEquals(context,DataContext)
            &&request==RoiRequestVersion&&ratio==ControlToImageRadio&&proof();
        var left=snapshot.Length==0?0:snapshot.Min(p=>p.X);var top=snapshot.Length==0?0:snapshot.Min(p=>p.Y);
        var width=snapshot.Length==0?0:snapshot.Max(p=>p.X)-left;var height=snapshot.Length==0?0:snapshot.Max(p=>p.Y)-top;
        Points=new PointCollection(snapshot);if(!Current())return;
        ContainerWidth=width*ratio;if(!Current())return;
        ContainerHeight=height*ratio;if(!Current())return;
        SetCurrentValue(ContainerPositionProperty,new Point(left*ratio,top*ratio));if(!Current())return;
        Rects?.CalculateSize();
    }
    public override void OnApplyTemplate()
    {
        var templateVersion=BeginRoiTemplate();
        try
        {
        if(!CurrentRoiTemplate(templateVersion))return;
        if(move is not null)move.MouseLeftButtonDown-=Move_MouseLeftButtonDown;
        if(Rects is not null)Rects.MouseLeftButtonDown-=Move_MouseLeftButtonDown;
        if(mPolygon is not null)mPolygon.MouseLeftButtonDown-=Move_MouseLeftButtonDown;
        base.OnApplyTemplate();
        if(!CurrentRoiTemplate(templateVersion))return;
        move=GetTemplateChild("move") as Grid;Rects=GetTemplateChild("Rects") as MoveAreaPage;mPolygon=GetTemplateChild("mPolygon") as Polygon;
        if(move is not null)move.MouseLeftButtonDown+=Move_MouseLeftButtonDown;
        if(Rects is not null)Rects.MouseLeftButtonDown+=Move_MouseLeftButtonDown;
        if(mPolygon is not null)mPolygon.MouseLeftButtonDown+=Move_MouseLeftButtonDown;
        parent=this.GetParent<ImageViewItemsPage>();RefreshGeometryLayout();

        }
        finally{EndRoiTemplate(templateVersion);}
    }
    private void Move_MouseLeftButtonDown(object sender,MouseButtonEventArgs e)
    {
        if(sender is not FrameworkElement handle||parent is null||PolygonPoints is null||!IsEnabled||!IsHitTestVisible)return;
        var points=PolygonPoints;var topology=_topology;var surface=parent;
        var index=ReferenceEquals(handle,Rects)?Rects.SelectedIndex:-1;
        if(ReferenceEquals(handle,Rects)&&(index<0||index>=points.Count))return;
        var gesture=PrepareRoiGesture(handle,()=>{_gesturePoints=null;_vertex=-1;_isMove=false;},
            ()=>ReferenceEquals(points,PolygonPoints)&&topology==_topology&&ReferenceEquals(surface,parent));
        if(gesture is null)return;
        try
        {
            _gesturePoints=points;_vertex=index;
            clickPoint=e.GetPosition(surface);this.GetParent<ListViewItem>()?.RaiseEvent(e);
            if(!gesture.IsCurrent||!gesture.Capture()){CancelRoiGesture(gesture);return;}
            if(!gesture.Attach((source,args)=>{if(gesture.IsActive)Move_MouseMove(source,args);},
                (source,args)=>{if(gesture.IsActive)Move_MouseLeftButtonUp(source,args);}))
            {CancelRoiGesture(gesture);return;}
            e.Handled=true;
        }
        catch(Exception error){try{CancelRoiGesture(gesture);}catch(Exception cleanup){throw new AggregateException(error,cleanup);}throw;}
    }
    private bool MoveStep(Vector vector,RoiGesture? gesture=null)
    {
        var points=PolygonPoints;var topology=_topology;var context=DataContext;var request=RoiRequestVersion;var proof=CaptureRoiUpdateProof();
        bool Current()=>ReferenceEquals(context,DataContext)&&ReferenceEquals(points,PolygonPoints)&&topology==_topology
            &&request==RoiRequestVersion&&proof()&&(gesture is null||gesture.IsActive);
        if(points is null||!Current())return false;
        for(var index=0;index<points.Count;index++)
        {
            if(!Current())return false;
            points[index]=points[index]+vector;
        }
        if(!Current())return false;
        RefreshGeometryLayout();return Current();
    }
    private void Move_MouseMove(object sender,MouseEventArgs e)
    {
        var gesture=ActiveRoiGesture;
        if(gesture is null||!gesture.IsActive||!ReferenceEquals(sender,gesture.Handle))return;
        if(e.LeftButton!=MouseButtonState.Pressed){CancelRoiGesture(gesture);return;}
        if(!double.IsFinite(ControlToImageRadio)||ControlToImageRadio<=0)return;
        var position=e.GetPosition(parent);var vector=(position-clickPoint)/ControlToImageRadio;var vertex=_vertex;
        _isMove=true;
        if(vertex>=0)
        {
            var points=_gesturePoints!;
            if(vertex>=points.Count){CancelRoiGesture(gesture);return;}
            points[vertex]=points[vertex]+vector;
        }
        else if(!MoveStep(vector,gesture))return;
        if(gesture.IsActive)clickPoint=position;
    }
    private void Move_MouseLeftButtonUp(object sender,MouseButtonEventArgs e)
    {
        var gesture=ActiveRoiGesture;
        if(gesture is null||!gesture.IsActive||!ReferenceEquals(sender,gesture.Handle))return;
        FinishRoiGesture(gesture,_isMove);
    }
    public void CancelInteraction()=>CancelRoiGesture();

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
