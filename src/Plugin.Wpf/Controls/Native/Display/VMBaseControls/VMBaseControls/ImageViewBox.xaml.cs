using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.BaseData;
using Apps.Data;
using Apps.Data.Common;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Microsoft.Win32;
using Mvvm.Commands;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

public partial class ImageViewBox : Control, INotifyPropertyChanged, IImageViewBox, IComponentConnector, IStyleConnector
{
	public delegate void ControlAlert(object sender, string msg);

	public delegate void SetImageEnd(object sender, string msg);

	public delegate void FreshExtendContent(object sender, object Tag);

	public delegate void SplotVisChanged(bool splotVis);

	public const double PixelOffSet = 0.0;

	public static readonly DependencyProperty RoiAddCommandProperty = DependencyProperty.Register("RoiAddCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty RoiCopyCommandProperty = DependencyProperty.Register("RoiCopyCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty RoiPasteCommandProperty = DependencyProperty.Register("RoiPasteCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty RoiDelCommandProperty = DependencyProperty.Register("RoiDelCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty RoiAddWithImageCommandProperty = DependencyProperty.Register("RoiAddWithImageCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty RoiSelectedCommandProperty = DependencyProperty.Register("RoiSelectedCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty AdditionDataCommandProperty = DependencyProperty.Register("AdditionDataCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null, AdditionDataChanged));

	public static readonly DependencyProperty RoiVisibleChangedCommandProperty = DependencyProperty.Register("RoiVisibleChangedCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null, RoiVisibleChanged));

	public static readonly DependencyProperty ImageSizeChangedCommandProperty = DependencyProperty.Register("ImageSizeChangedCommand", typeof(ICommand), typeof(ImageViewBox), new PropertyMetadata(null, OnImageSizeChangedCommandChanged));

	public static readonly DependencyProperty IsDragROIProperty = DependencyProperty.Register("IsDragROI", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(false));

	public static readonly DependencyProperty ROIHitTestVisibleProperty = DependencyProperty.Register("ROIHitTestVisible", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(true));

	public static readonly DependencyProperty ROIVisibleProperty = DependencyProperty.Register("ROIVisible", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(true, OnROIVisibleChanged));

	public static readonly DependencyProperty IsPasteInSelectedSubROIListProperty = DependencyProperty.Register("IsPasteInSelectedSubROIList", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(false));

	public static readonly DependencyProperty SelectedSubROIListProperty = DependencyProperty.Register("SelectedSubROIList", typeof(IList), typeof(ImageViewBox), new PropertyMetadata(OnROIListChanged));

	public static readonly DependencyProperty ROIListProperty = DependencyProperty.Register("ROIList", typeof(IList), typeof(ImageViewBox), new PropertyMetadata(null, OnROIListChanged));

	public static readonly DependencyProperty AdditionObjectProperty = DependencyProperty.Register("AdditionObject", typeof(object), typeof(ImageViewBox), new PropertyMetadata(null, AdditionObjectChanged));

	public static readonly DependencyProperty ROITypeUIProperty = DependencyProperty.Register("ROITypeUI", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(false));

	public static readonly DependencyProperty RoiExternTemplateProperty = DependencyProperty.Register("RoiExternTemplate", typeof(DataTemplate), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty RoiRightTopExternTemplateProperty = DependencyProperty.Register("RoiRightTopExternTemplate", typeof(DataTemplate), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty IsStateForDrawROIProperty = DependencyProperty.Register("IsStateForDrawROI", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(false, IsStateForDrawROIChanged));

	public static readonly DependencyProperty DrawROITypeProperty = DependencyProperty.Register("DrawROIType", typeof(Type), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty ShapeHitTestVisibleProperty = DependencyProperty.Register("ShapeHitTestVisible", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(true));

	public static readonly DependencyProperty ShapeListProperty = DependencyProperty.Register("ShapeList", typeof(IList), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty GriddingGeometryProperty = DependencyProperty.Register("GriddingGeometry", typeof(GeometryGroup), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty GriddingVisibilityProperty = DependencyProperty.Register("GriddingVisibility", typeof(Visibility), typeof(ImageViewBox), new PropertyMetadata(Visibility.Visible));

	public static readonly DependencyProperty IsDisplayPixelAutoProperty = DependencyProperty.Register("IsDisplayPixelAuto", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(true));

	public static readonly DependencyProperty PixelXProperty = DependencyProperty.Register("PixelX", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty PixelYProperty = DependencyProperty.Register("PixelY", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty PixelRProperty = DependencyProperty.Register("PixelR", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty PixelGProperty = DependencyProperty.Register("PixelG", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty PixelBProperty = DependencyProperty.Register("PixelB", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty PixelInfoProperty = DependencyProperty.Register("PixelInfo", typeof(string), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty PixelInfoLabelProperty = DependencyProperty.Register("PixelInfoLabel", typeof(string), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty PhysicalXProperty = DependencyProperty.Register("PhysicalX", typeof(float), typeof(ImageViewBox), new PropertyMetadata(0f));

	public static readonly DependencyProperty PhysicalYProperty = DependencyProperty.Register("PhysicalY", typeof(float), typeof(ImageViewBox), new PropertyMetadata(0f));

	public static readonly DependencyProperty PhysicalRProperty = DependencyProperty.Register("PhysicalR", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty ShapeConvertMatrixProperty = DependencyProperty.Register("ShapeConvertMatrix", typeof(object), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty CordinateShowProperty = DependencyProperty.Register("CordinateShow", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty ImageNameProperty = DependencyProperty.Register("ImageName", typeof(string), typeof(ImageViewBox), new PropertyMetadata(null));

	public static readonly DependencyProperty ImageSourceWidthProperty = DependencyProperty.Register("ImageSourceWidth", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0, OnImageSourceWidthChanged));

	public static readonly DependencyProperty ImageSourceHeightProperty = DependencyProperty.Register("ImageSourceHeight", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty ImageScaleProperty = DependencyProperty.Register("ImageScale", typeof(double), typeof(ImageViewBox), new PropertyMetadata(1.0, OnImageScaleChanged));

	public static readonly DependencyProperty ImageOffSetXProperty = DependencyProperty.Register("ImageOffSetX", typeof(double), typeof(ImageViewBox), new PropertyMetadata(0.0));

	public static readonly DependencyProperty ImageOffSetYProperty = DependencyProperty.Register("ImageOffSetY", typeof(double), typeof(ImageViewBox), new PropertyMetadata(0.0));

	public static readonly DependencyProperty ImageDataSourceProperty = DependencyProperty.Register("ImageDataSource", typeof(IImageData), typeof(ImageViewBox), new PropertyMetadata(null, ImageSourceChanged));

	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(ImageViewBox), new PropertyMetadata(null, SourceChanged));

	public static readonly DependencyProperty ImageResizableProperty = DependencyProperty.Register("ImageResizable", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(true));

	public static readonly DependencyProperty ImageDragEanbleProperty = DependencyProperty.Register("ImageDragEanble", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(true));

	private Grid mainGrid;

	private Canvas mapcanvas;

	private Viewbox viewbox;

	private TransformGroup TG;

	public System.Windows.Controls.Image sImage;

	private System.Windows.Controls.Image pointsImage;

	private Canvas above;

	private Canvas extenAbove;

	private ROIItemsPage mROIView;

	public Grid ImageLayers;

	private ScaleTransform srf;

	private ImageBrush background;

	private double gridWidth = 0.0;

	public Byte2ImageSource _byte2Image = new Byte2ImageSource();

	public bool isCliped = false;

	public bool isHighQualityImage = false;

	private int curImageX = -1;

	private int curImageY = -1;

	private System.Windows.Point curPoint = default;

	private System.Windows.Point offset = new System.Windows.Point(0.0, 0.0);

	private const double MinScale = 0.3;

	private const double step = 0.5;

	private bool _enableZoomIn = true;

	private bool _enableZoomOut = true;

	private bool isDragging = false;

	private System.Windows.Point mouseOffset;

	private double theTop;

	private double theLeft;

	private System.Windows.Point oldMouseMovePosition = new System.Windows.Point(0.0, 0.0);

	private DelegateCommand<ObjectEventArgs> _MROI_DrawCompleteComamnd = null;

	private int _OffSetXPixel;

	private int _OffSetYPixel;

	private int _ClipXPixel;

	private int _ClipYPixel;

	private Thickness imageMargin;

	private bool needFlashExtern = false;

	public bool needFlash = false;

	private ICommand _SaveOriginalImageCommand = null;

	private ICommand _SaveRenderImageCommand = null;

	public double _imageViewWidth = 0.0;

	public double _imageViewHeight = 0.0;

	private ICommand _DeleteRoiCommand = null;

	private ICommand _CopyRoiCommand = null;

	private List<object> PasteList = new List<object>();

	private IROI pasteROI = null;

	private ICommand _PasteRoiCommand = null;

	private bool _IsMulRolModule;

	private ICommand _RoiEnableCommand = null;

	public static readonly DependencyProperty IsMenuVisibleProperty = DependencyProperty.Register("IsMenuVisible", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(true));

	private bool needRefreshShapes = false;

	private Regex colorRegex = new Regex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$");

	private string colorPath = null;

	public static readonly DependencyProperty SplotVisProperty = DependencyProperty.Register("SplotVis", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(false, OnSplotVisChanged));

	public static readonly DependencyProperty IsAutoModeProperty = DependencyProperty.Register("IsAutoMode", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(true, OnIsAutoModeChanged));

	public static readonly DependencyProperty MinValue3DProperty = DependencyProperty.Register("MinValue3D", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	public static readonly DependencyProperty MaxValue3DProperty = DependencyProperty.Register("MaxValue3D", typeof(int), typeof(ImageViewBox), new PropertyMetadata(4096));

	public static readonly DependencyProperty RangeValueProperty = DependencyProperty.Register("RangeValue", typeof(int), typeof(ImageViewBox), new PropertyMetadata(0));

	private int OldMinValue = 0;

	private int OldMaxValue = 4096;

	private DelegateCommand _ValueChangedCommand = null;

	public static readonly DependencyProperty PlotScaleXProperty = DependencyProperty.Register("PlotScaleX", typeof(double), typeof(ImageViewBox), new PropertyMetadata(0.0));

	public static readonly DependencyProperty PlotScaleYProperty = DependencyProperty.Register("PlotScaleY", typeof(double), typeof(ImageViewBox), new PropertyMetadata(0.0));

	private System.Windows.Point MouseRightButtonDownPoint;

	public static readonly DependencyProperty IsConvertPointCloudProperty = DependencyProperty.Register("IsConvertPointCloud", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(false, null));

	public static readonly DependencyProperty IsShowPointCloudProperty = DependencyProperty.Register("IsShowPointCloud", typeof(bool), typeof(ImageViewBox), new PropertyMetadata(false, null));

	public IImageViewAddition ImageBoxAddition { get; set; }

	public ICommand RoiAddCommand
	{
		get
		{
			return (ICommand)GetValue(RoiAddCommandProperty);
		}
		set
		{
			SetValue(RoiAddCommandProperty, value);
		}
	}

	public ICommand RoiCopyCommand
	{
		get
		{
			return (ICommand)GetValue(RoiCopyCommandProperty);
		}
		set
		{
			SetValue(RoiCopyCommandProperty, value);
		}
	}

	public ICommand RoiPasteCommand
	{
		get
		{
			return (ICommand)GetValue(RoiPasteCommandProperty);
		}
		set
		{
			SetValue(RoiPasteCommandProperty, value);
		}
	}

	public ICommand RoiDelCommand
	{
		get
		{
			return (ICommand)GetValue(RoiDelCommandProperty);
		}
		set
		{
			SetValue(RoiDelCommandProperty, value);
		}
	}

	public ICommand RoiAddWithImageCommand
	{
		get
		{
			return (ICommand)GetValue(RoiAddWithImageCommandProperty);
		}
		set
		{
			SetValue(RoiAddWithImageCommandProperty, value);
		}
	}

	public ICommand RoiSelectedCommand
	{
		get
		{
			return (ICommand)GetValue(RoiSelectedCommandProperty);
		}
		set
		{
			SetValue(RoiSelectedCommandProperty, value);
		}
	}

	public ICommand AdditionDataCommand
	{
		get
		{
			return (ICommand)GetValue(AdditionDataCommandProperty);
		}
		set
		{
			SetValue(AdditionDataCommandProperty, value);
		}
	}

	public ICommand RoiVisibleChangedCommand
	{
		get
		{
			return (ICommand)GetValue(RoiVisibleChangedCommandProperty);
		}
		set
		{
			SetValue(RoiVisibleChangedCommandProperty, value);
		}
	}

	public ICommand ImageSizeChangedCommand
	{
		get
		{
			return (ICommand)GetValue(ImageSizeChangedCommandProperty);
		}
		set
		{
			SetValue(ImageSizeChangedCommandProperty, value);
		}
	}

	public ControlAlert OnControlAlert { get; set; }

	public SetImageEnd OnSetImageEnd { get; set; }

	public FreshExtendContent OnFreshExtendContent { get; set; }

	public static double ImageDesignWidth => 500.0;

	public object Viewbox => viewbox;

	public object MapCanvas => mapcanvas;

	public System.Windows.Controls.Image SourceImage => sImage;

	public object ScaleTransform => srf;

	public double OnePixelWidth
	{
		get
		{
			if (sImage.Source != null)
			{
				return viewbox.ActualWidth * ImageScale / (double)ImageSourceWidth;
			}
			return 0.0;
		}
	}

	public object ExtenAbove => extenAbove;

	public bool IsDragROI
	{
		get
		{
			return (bool)GetValue(IsDragROIProperty);
		}
		set
		{
			SetValue(IsDragROIProperty, value);
		}
	}

	public bool ROIHitTestVisible
	{
		get
		{
			return (bool)GetValue(ROIHitTestVisibleProperty);
		}
		set
		{
			SetValue(ROIHitTestVisibleProperty, value);
		}
	}

	public bool ROIVisible
	{
		get
		{
			return (bool)GetValue(ROIVisibleProperty);
		}
		set
		{
			SetValue(ROIVisibleProperty, value);
		}
	}

	public bool IsPasteInSelectedSubROIList
	{
		get
		{
			return (bool)GetValue(IsPasteInSelectedSubROIListProperty);
		}
		set
		{
			SetValue(IsPasteInSelectedSubROIListProperty, value);
		}
	}

	public IList SelectedSubROIList
	{
		get
		{
			return (IList)GetValue(SelectedSubROIListProperty);
		}
		set
		{
			SetValue(SelectedSubROIListProperty, value);
		}
	}

	public IList ROIList
	{
		get
		{
			return (IList)GetValue(ROIListProperty);
		}
		set
		{
			SetValue(ROIListProperty, value);
		}
	}

	public object AdditionObject
	{
		get
		{
			return GetValue(AdditionObjectProperty);
		}
		set
		{
			SetValue(AdditionObjectProperty, value);
		}
	}

	public bool ROITypeUI
	{
		get
		{
			return (bool)GetValue(ROITypeUIProperty);
		}
		set
		{
			SetValue(ROITypeUIProperty, value);
		}
	}

	public DataTemplate RoiExternTemplate
	{
		get
		{
			return (DataTemplate)GetValue(RoiExternTemplateProperty);
		}
		set
		{
			SetValue(RoiExternTemplateProperty, value);
		}
	}

	public DataTemplate RoiRightTopExternTemplate
	{
		get
		{
			return (DataTemplate)GetValue(RoiRightTopExternTemplateProperty);
		}
		set
		{
			SetValue(RoiRightTopExternTemplateProperty, value);
		}
	}

	public bool IsStateForDrawROI
	{
		get
		{
			return (bool)GetValue(IsStateForDrawROIProperty);
		}
		set
		{
			SetValue(IsStateForDrawROIProperty, value);
		}
	}

	public Type DrawROIType
	{
		get
		{
			return (Type)GetValue(DrawROITypeProperty);
		}
		set
		{
			SetValue(DrawROITypeProperty, value);
		}
	}

	public bool ShapeHitTestVisible
	{
		get
		{
			return (bool)GetValue(ShapeHitTestVisibleProperty);
		}
		set
		{
			SetValue(ShapeHitTestVisibleProperty, value);
		}
	}

	public IList ShapeList
	{
		get
		{
			return (IList)GetValue(ShapeListProperty);
		}
		set
		{
			SetValue(ShapeListProperty, value);
		}
	}

	public GeometryGroup GriddingGeometry
	{
		get
		{
			return (GeometryGroup)GetValue(GriddingGeometryProperty);
		}
		set
		{
			SetValue(GriddingGeometryProperty, value);
		}
	}

	public Visibility GriddingVisibility
	{
		get
		{
			return (Visibility)GetValue(GriddingVisibilityProperty);
		}
		set
		{
			SetValue(GriddingVisibilityProperty, value);
		}
	}

	public bool IsDisplayPixelAuto
	{
		get
		{
			return (bool)GetValue(IsDisplayPixelAutoProperty);
		}
		set
		{
			SetValue(IsDisplayPixelAutoProperty, value);
		}
	}

	public int PixelX
	{
		get
		{
			return (int)GetValue(PixelXProperty);
		}
		set
		{
			SetValue(PixelXProperty, value);
		}
	}

	public int PixelY
	{
		get
		{
			return (int)GetValue(PixelYProperty);
		}
		set
		{
			SetValue(PixelYProperty, value);
		}
	}

	public int PixelR
	{
		get
		{
			return (int)GetValue(PixelRProperty);
		}
		set
		{
			SetValue(PixelRProperty, value);
		}
	}

	public int PixelG
	{
		get
		{
			return (int)GetValue(PixelGProperty);
		}
		set
		{
			SetValue(PixelGProperty, value);
		}
	}

	public int PixelB
	{
		get
		{
			return (int)GetValue(PixelBProperty);
		}
		set
		{
			SetValue(PixelBProperty, value);
		}
	}

	public string PixelInfo
	{
		get
		{
			return (string)GetValue(PixelInfoProperty);
		}
		set
		{
			SetValue(PixelInfoProperty, value);
		}
	}

	public string PixelInfoLabel
	{
		get
		{
			return (string)GetValue(PixelInfoLabelProperty);
		}
		set
		{
			SetValue(PixelInfoLabelProperty, value);
		}
	}

	public float PhysicalX
	{
		get
		{
			return (float)GetValue(PhysicalXProperty);
		}
		set
		{
			SetValue(PhysicalXProperty, value);
		}
	}

	public float PhysicalY
	{
		get
		{
			return (float)GetValue(PhysicalYProperty);
		}
		set
		{
			SetValue(PhysicalYProperty, value);
		}
	}

	public int PhysicalR
	{
		get
		{
			return (int)GetValue(PhysicalRProperty);
		}
		set
		{
			SetValue(PhysicalRProperty, value);
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

	public int CordinateShow
	{
		get
		{
			return (int)GetValue(CordinateShowProperty);
		}
		set
		{
			SetValue(CordinateShowProperty, value);
		}
	}

	public string ImageName
	{
		get
		{
			return (string)GetValue(ImageNameProperty);
		}
		set
		{
			SetValue(ImageNameProperty, value);
		}
	}

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

	public double ImageScale
	{
		get
		{
			return (double)GetValue(ImageScaleProperty);
		}
		set
		{
			SetValue(ImageScaleProperty, value);
		}
	}

	public double ImageOffSetX
	{
		get
		{
			return (double)GetValue(ImageOffSetXProperty);
		}
		set
		{
			SetValue(ImageOffSetXProperty, value);
		}
	}

	public double ImageOffSetY
	{
		get
		{
			return (double)GetValue(ImageOffSetYProperty);
		}
		set
		{
			SetValue(ImageOffSetYProperty, value);
		}
	}

	public IImageData ImageDataSource
	{
		get
		{
			return (IImageData)GetValue(ImageDataSourceProperty);
		}
		set
		{
			SetValue(ImageDataSourceProperty, value);
		}
	}

	public ImageSource Source
	{
		get
		{
			return (ImageSource)GetValue(SourceProperty);
		}
		set
		{
			SetValue(SourceProperty, value);
		}
	}

	public bool ImageResizable
	{
		get
		{
			return (bool)GetValue(ImageResizableProperty);
		}
		set
		{
			SetValue(ImageResizableProperty, value);
		}
	}

	public bool ImageDragEanble
	{
		get
		{
			return (bool)GetValue(ImageDragEanbleProperty);
		}
		set
		{
			SetValue(ImageDragEanbleProperty, value);
		}
	}

	public double ControlToImageRadio => GetRatio();

	public static bool IsDirectDownSampling { get; set; } = true;

	public bool EnableZoomIn
	{
		get
		{
			return _enableZoomIn;
		}
		set
		{
			if (value != _enableZoomIn)
			{
				_enableZoomIn = value;
				OnPropertyChanged("EnableZoomIn");
			}
		}
	}

	public bool EnableZoomOut
	{
		get
		{
			return _enableZoomOut;
		}
		set
		{
			if (value != _enableZoomOut)
			{
				_enableZoomOut = value;
				OnPropertyChanged("EnableZoomOut");
			}
		}
	}

	internal bool IsDrawingOfROI { get; set; }

	public ICommand MROI_DrawCompleteComamnd
	{
		get
		{
			if (_MROI_DrawCompleteComamnd == null)
			{
				_MROI_DrawCompleteComamnd = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)MROI_DrawCompleteComamndExecute);
			}
			return (ICommand)_MROI_DrawCompleteComamnd;
		}
	}

	public int OffSetXPixel
	{
		get
		{
			return _OffSetXPixel;
		}
		set
		{
			if (_OffSetXPixel != value)
			{
				_OffSetXPixel = value;
				OnPropertyChanged("OffSetXPixel");
			}
		}
	}

	public int OffSetYPixel
	{
		get
		{
			return _OffSetYPixel;
		}
		set
		{
			if (_OffSetYPixel != value)
			{
				_OffSetYPixel = value;
				OnPropertyChanged("OffSetYPixel");
			}
		}
	}

	public int ClipXPixel
	{
		get
		{
			return _ClipXPixel;
		}
		set
		{
			if (_ClipXPixel != value)
			{
				_ClipXPixel = value;
				OnPropertyChanged("ClipXPixel");
			}
		}
	}

	public int ClipYPixel
	{
		get
		{
			return _ClipYPixel;
		}
		set
		{
			if (_ClipYPixel != value)
			{
				_ClipYPixel = value;
				OnPropertyChanged("ClipYPixel");
			}
		}
	}

	public Thickness ImageMargin
	{
		get
		{
			return imageMargin;
		}
		set
		{
			if (imageMargin != value)
			{
				imageMargin = value;
				OnPropertyChanged("ImageMargin");
			}
		}
	}

	public double ThicknessScale => ViewBoxScale * ImageScale;

	public double ViewBoxScale
	{
		get
		{
			if (viewbox == null)
			{
				return 1.0;
			}
			return viewbox.ActualWidth / ImageDesignWidth;
		}
	}

	public ICommand SaveOriginalImageCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_SaveOriginalImageCommand == null)
			{
				_SaveOriginalImageCommand = (ICommand)new DelegateCommand((Action)SaveOriginalImage);
			}
			return _SaveOriginalImageCommand;
		}
	}

	public ICommand SaveRenderImageCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_SaveRenderImageCommand == null)
			{
				_SaveRenderImageCommand = (ICommand)new DelegateCommand((Action)SaveRenderImage);
			}
			return _SaveRenderImageCommand;
		}
	}

	public bool IsSaveRenderImageVisible { get; set; }

	public ICommand DeleteRoiCommand
	{
		get
		{
			if (_DeleteRoiCommand == null)
			{
				_DeleteRoiCommand = (ICommand)new DelegateCommand<object>((Action<object>)DeleteRoi, (Func<object, bool>)CanDeleteRoi, false);
			}
			return _DeleteRoiCommand;
		}
	}

	public ICommand CopyRoiCommand
	{
		get
		{
			if (_CopyRoiCommand == null)
			{
				_CopyRoiCommand = (ICommand)new DelegateCommand<object>((Action<object>)CopyRoi, (Func<object, bool>)CanCopyRoi, false);
			}
			return _CopyRoiCommand;
		}
	}

	public ICommand PasteRoiCommand
	{
		get
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Expected Obj, but got Unknown
			if (_PasteRoiCommand == null)
			{
				_PasteRoiCommand = (ICommand)new DelegateCommand((Action)PasteRoiRoi, (Func<bool>)(() => PasteList.Count > 0 && ROIHitTestVisible), false);
			}
			return _PasteRoiCommand;
		}
	}

	public bool IsMulRolModule
	{
		get
		{
			return _IsMulRolModule;
		}
		set
		{
			if (_IsMulRolModule != value)
			{
				_IsMulRolModule = value;
				OnPropertyChanged("IsMulRolModule");
			}
		}
	}

	public ICommand RoiEnableCommand
	{
		get
		{
			if (_RoiEnableCommand == null)
			{
				_RoiEnableCommand = (ICommand)new DelegateCommand<string>((Action<string>)RoiEnableExecute);
			}
			return _RoiEnableCommand;
		}
	}

	public bool IsMenuVisible
	{
		get
		{
			return (bool)GetValue(IsMenuVisibleProperty);
		}
		set
		{
			SetValue(IsMenuVisibleProperty, value);
		}
	}

	public bool NeedRefreshShapes
	{
		get
		{
			return needRefreshShapes;
		}
		set
		{
			if (needRefreshShapes != value)
			{
				needRefreshShapes = value;
				OnPropertyChanged("NeedRefreshShapes");
				if (needRefreshShapes)
				{
					needRefreshShapes = false;
					OnPropertyChanged("NeedRefreshShapes");
				}
			}
		}
	}

	public SplotVisChanged OnSplotVisChangedEvent { get; set; }

	public bool Range2ProfileEnable { get; set; }

	public bool SplotVis
	{
		get
		{
			return (bool)GetValue(SplotVisProperty);
		}
		set
		{
			SetValue(SplotVisProperty, value);
		}
	}

	public bool IsAutoMode
	{
		get
		{
			return (bool)GetValue(IsAutoModeProperty);
		}
		set
		{
			SetValue(IsAutoModeProperty, value);
		}
	}

	public int MinValue3D
	{
		get
		{
			return (int)GetValue(MinValue3DProperty);
		}
		set
		{
			SetValue(MinValue3DProperty, value);
		}
	}

	public int MaxValue3D
	{
		get
		{
			return (int)GetValue(MaxValue3DProperty);
		}
		set
		{
			SetValue(MaxValue3DProperty, value);
		}
	}

	public int RangeValue
	{
		get
		{
			return (int)GetValue(RangeValueProperty);
		}
		set
		{
			SetValue(RangeValueProperty, value);
		}
	}

	public bool IsShowRangeImage
	{
		get
		{
			if (ImageDataSource is IRangeImageData { RangeBuffer: not null })
			{
				return true;
			}
			if (ImageDataSource is ImageData imageData)
			{
				return imageData.DetailedPixelFormat == 17825976;
			}
			return false;
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	public double PlotScaleX
	{
		get
		{
			return (double)GetValue(PlotScaleXProperty);
		}
		set
		{
			SetValue(PlotScaleXProperty, value);
		}
	}

	public double PlotScaleY
	{
		get
		{
			return (double)GetValue(PlotScaleYProperty);
		}
		set
		{
			SetValue(PlotScaleYProperty, value);
		}
	}

	public bool IsConvertPointCloud
	{
		get
		{
			return (bool)GetValue(IsConvertPointCloudProperty);
		}
		set
		{
			SetValue(IsConvertPointCloudProperty, value);
		}
	}

	public bool IsShowPointCloud
	{
		get
		{
			return (bool)GetValue(IsShowPointCloudProperty);
		}
		set
		{
			SetValue(IsShowPointCloudProperty, value);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event EventHandler<RoiEventArgs> RoiAddEvent;

	public event EventHandler<RoiEventArgs> RoiParamChangedEvent;

	public event EventHandler<RoiEventArgs> RoiDeleteEvent;

	public event EventHandler<RoiEventArgs> RoiSelectedEvent;

	public event Action<IImageData, IImageData> ImageSourceChangeEvent;

	public event ObjectEventHandler OnDrawROICompleted;

	public event Action<string> UpdateImageEvent;

	public event Action RoiVisibleChangedEvent;

	public void OnPropertyChanged(string strPropertyInfo)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(strPropertyInfo));
		}
	}

	private static void AdditionDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != null && d is ImageViewBox { ImageBoxAddition: not null, AdditionDataCommand: { } additionDataCommand } imageViewBox)
		{
			additionDataCommand.Execute(imageViewBox.ImageBoxAddition);
		}
	}

	private static void RoiVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		imageViewBox.RoiVisibleChangedCommand?.Execute(imageViewBox.ROIVisible);
	}

	private static void OnImageSizeChangedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		if (e.NewValue is ICommand command && imageViewBox.ImageDataSource != null)
		{
			command.Execute(new System.Windows.Size(imageViewBox.ImageDataSource.Width, imageViewBox.ImageDataSource.Height));
		}
	}

	private static void OnROIVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		bool flag = (bool)e.NewValue;
		imageViewBox.ChangeHitTestVisible();
		imageViewBox.RoiVisibleChangedEvent?.Invoke();
		imageViewBox.RoiVisibleChangedCommand?.Execute(flag);
	}

	private static void OnROIListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		imageViewBox.ClearPasteData();
		imageViewBox.ImageBoxAddition?.ROIListChangedOut(e);
	}

	private static void AdditionObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
	}

	private static void IsStateForDrawROIChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		bool flag = (bool)e.NewValue;
		imageViewBox.ChangeHitTestVisible();
	}

	private void ChangeHitTestVisible()
	{
		bool flag = !IsStateForDrawROI && ROIVisible;
		SetValue(ROIHitTestVisibleProperty, flag);
		SetValue(ShapeHitTestVisibleProperty, !IsStateForDrawROI);
	}

	private float MatrixExchangeX(float x, float y)
	{
		List<double> list = (ShapeConvertMatrix as IMatrix)?.MatrixList;
		if (list == null)
		{
			return x;
		}
		if (list.Count != 9)
		{
			return x;
		}
		float num = (float)list[0] * x + (float)list[1] * y + (float)list[2];
		return num / ((float)list[6] * x + (float)list[7] * y + (float)list[8]);
	}

	private float MatrixExchangeY(float x, float y)
	{
		List<double> list = (ShapeConvertMatrix as IMatrix)?.MatrixList;
		if (list == null)
		{
			return y;
		}
		if (list.Count != 9)
		{
			return y;
		}
		float num = (float)list[3] * x + (float)list[4] * y + (float)list[5];
		return num / ((float)list[6] * x + (float)list[7] * y + (float)list[8]);
	}

	private static void OnImageSourceWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		imageViewBox.OnPropertyChanged("ControlToImageRadio");
	}

	private static void OnImageScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		imageViewBox.OnPropertyChanged("ThicknessScale");
	}

	private static void ImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = (ImageViewBox)d;
		IImageData arg = e.OldValue as IImageData;
		IImageData imageData = e.NewValue as IImageData;
		bool flag = true;
		if (imageData != null)
		{
			if (imageViewBox.ImageSourceWidth > 0)
			{
				imageViewBox.OffSetXPixel = (int)Math.Floor((double)imageViewBox.OffSetXPixel * (double)imageViewBox.ImageDataSource.Width / (double)imageViewBox.ImageSourceWidth);
				imageViewBox.OffSetYPixel = (int)Math.Floor((double)imageViewBox.OffSetYPixel * (double)imageViewBox.ImageDataSource.Height / (double)imageViewBox.ImageSourceHeight);
				imageViewBox.ClipXPixel = (int)Math.Floor((double)imageViewBox.ClipXPixel * (double)imageViewBox.ImageDataSource.Width / (double)imageViewBox.ImageSourceWidth);
				imageViewBox.ClipYPixel = (int)Math.Floor((double)imageViewBox.ClipYPixel * (double)imageViewBox.ImageDataSource.Height / (double)imageViewBox.ImageSourceHeight);
			}
			if (imageViewBox.ImageDataSource.Width != imageViewBox.ImageSourceWidth || imageViewBox.ImageDataSource.Height != imageViewBox.ImageSourceHeight)
			{
				imageViewBox.SetValue(ImageSourceWidthProperty, imageViewBox.ImageDataSource.Width);
				imageViewBox.SetValue(ImageSourceHeightProperty, imageViewBox.ImageDataSource.Height);
				imageViewBox.ImageSizeChangedCommand?.Execute(new System.Windows.Size(imageViewBox.ImageDataSource.Width, imageViewBox.ImageDataSource.Height));
			}
			flag = imageViewBox.DoImageSourceChanged();
		}
		else
		{
			imageViewBox.SplotVis = false;
			imageViewBox.ImageBoxAddition?.ImageSourceChangedOut();
			imageViewBox.Source = null;
			imageViewBox.ImageSourceWidth = 0;
			imageViewBox.ImageSourceHeight = 0;
			imageViewBox.Init();
		}
		imageViewBox.ImageSourceChangeEvent?.Invoke(arg, imageData);
		imageViewBox.OnSetImageEnd?.Invoke(imageViewBox, flag.ToString());
		imageViewBox.OnPropertyChanged("IsShowRangeImage");
	}

	public static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = (ImageViewBox)d;
		imageViewBox.needFlash = true;
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		mainGrid = Template.FindName("mainGrid", this) as Grid;
		mapcanvas = Template.FindName("mapcanvas", this) as Canvas;
		viewbox = Template.FindName("viewbox", this) as Viewbox;
		TG = Template.FindName("TG", this) as TransformGroup;
		sImage = Template.FindName("sImage", this) as System.Windows.Controls.Image;
		pointsImage = Template.FindName("pointsImage", this) as System.Windows.Controls.Image;
		above = Template.FindName("above", this) as Canvas;
		extenAbove = Template.FindName("ExtenAbove", this) as Canvas;
		mROIView = Template.FindName("mROIView", this) as ROIItemsPage;
		ImageLayers = Template.FindName("ImageLayers", this) as Grid;
		srf = Template.FindName("srf", this) as ScaleTransform;
		background = new ImageBrush
		{
			Viewport = new Rect(0.0, 0.0, 20.0, 22.0),
			TileMode = TileMode.Tile,
			ViewportUnits = BrushMappingMode.Absolute
		};
		if (!string.IsNullOrEmpty(colorPath))
		{
			SetBackground(colorPath);
			colorPath = null;
		}
		OnPropertyChanged("ViewBoxScale");
	}

	public ImageViewBox()
	{
		InitializeComponent();
		ImageBoxAddition = Ioc.HotGet<IAdditionService>()?.GetImageViewAdditionInstance(this);
	}

	private void SetAboveCanvas()
	{
		if (Source == null)
		{
			above.Opacity = 0.0;
			return;
		}
		System.Windows.Point point = viewbox.TranslatePoint(new System.Windows.Point(0.0, 0.0), mapcanvas);
		double num = viewbox.ActualWidth * ImageScale;
		double num2 = viewbox.ActualHeight * ImageScale;
		double num3 = num / (double)ImageSourceWidth;
		if (num3 > 50.0)
		{
			gridWidth = num3;
			GeometryGroup geometryGroup = new GeometryGroup();
			int num4 = (int)Math.Ceiling((0.0 - point.Y) / gridWidth);
			int num5 = (int)Math.Ceiling((above.ActualHeight - point.Y) / gridWidth);
			if (num4 < 0)
			{
				num4 = 0;
			}
			if ((double)num5 > num2 / gridWidth)
			{
				num5 = (int)Math.Ceiling(num2 / gridWidth);
			}
			for (int i = num4; i < num5; i++)
			{
				LineGeometry value = new LineGeometry(new System.Windows.Point(point.X, point.Y + gridWidth * (double)i), new System.Windows.Point(point.X + num, point.Y + gridWidth * (double)i));
				geometryGroup.Children.Add(value);
			}
			num4 = (int)Math.Ceiling((0.0 - point.X) / gridWidth);
			num5 = (int)Math.Ceiling((above.ActualWidth - point.X) / gridWidth);
			if (num4 < 0)
			{
				num4 = 0;
			}
			if ((double)num5 > num / gridWidth)
			{
				num5 = (int)Math.Ceiling(num / gridWidth);
			}
			for (int j = num4; j < num5; j++)
			{
				LineGeometry value2 = new LineGeometry(new System.Windows.Point(point.X + gridWidth * (double)j, point.Y), new System.Windows.Point(point.X + gridWidth * (double)j, point.Y + num2));
				geometryGroup.Children.Add(value2);
			}
			GriddingGeometry = geometryGroup;
			above.Opacity = 1.0;
		}
		else
		{
			above.Opacity = 0.0;
		}
	}

	internal void NotifyROIParamChanged(object obj)
	{
		RoiParamChangedEvent?.Invoke(this, new RoiEventArgs(obj));
	}

	private bool DoImageSourceChanged()
	{
		bool returnValue = false;
		IImageViewAddition imageBoxAddition = ImageBoxAddition;
		if (imageBoxAddition != null && imageBoxAddition.DoImageSourceChangedOut(out returnValue))
		{
			return returnValue;
		}
		SplotVis = false;
		return SetImageSource(IsDirectDownSampling);
	}

	public double GetRatio()
	{
		if (ImageSourceWidth == 0)
		{
			return 1.0;
		}
		return ImageDesignWidth / (double)ImageSourceWidth;
	}

	public bool UpdateImageSize(out System.Windows.Size imageSize)
	{
		imageSize = default;
		if (ImageDataSource == null || ImageDataSource.Width == 0 || ImageDataSource.Height == 0)
		{
			return false;
		}
		if (viewbox == null || double.IsNaN(viewbox.Width) || double.IsNaN(viewbox.Height) || viewbox.Width == 0.0 || viewbox.Height == 0.0 || sImage == null)
		{
			return false;
		}
		double num = (double)ImageDataSource.Width / (double)ImageDataSource.Height;
		double num2 = viewbox.Width / viewbox.Height;
		if (num > num2)
		{
			double width = viewbox.Width;
			double height = width / num;
			imageSize = new System.Windows.Size(width, height);
		}
		else
		{
			double height2 = viewbox.Height;
			double width2 = num * height2;
			imageSize = new System.Windows.Size(width2, height2);
		}
		return true;
	}

	private bool SetImageSource(bool isDirect = false, bool isInner = false)
	{
		if (isHighQualityImage != GlobalRenderVariable.IsHighQualityImage && sImage != null)
		{
			isHighQualityImage = GlobalRenderVariable.IsHighQualityImage;
			BitmapScalingMode bitmapScalingMode = (isHighQualityImage ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
			RenderOptions.SetBitmapScalingMode(sImage, bitmapScalingMode);
		}
		if (UpdateImageSize(out var imageSize))
		{
			if (double.IsNaN(imageSize.Width) || double.IsNaN(imageSize.Height))
			{
				return false;
			}
			System.Windows.Size size = new System.Windows.Size(imageSize.Width * ImageScale, imageSize.Height * ImageScale);
			bool flag = (long)ImageDataSource.Width * (long)ImageDataSource.Height > 100000000;
			if (GlobalRenderVariable.IsHighQualityImage && !flag)
			{
				size = new System.Windows.Size(ImageDataSource.Width, ImageDataSource.Height);
			}
			double num = (double)ImageDataSource.Width / size.Width;
			int num2 = (int)((double)OffSetXPixel / num);
			int num3 = (int)((double)OffSetYPixel / num);
			int num4 = (int)((double)ClipXPixel / num);
			int num5 = (int)((double)ClipYPixel / num);
			Thickness thickness = ImageMargin;
			double left = ImageDesignWidth * (double)num2 / size.Width;
			thickness.Left = left;
			double top = ImageDesignWidth * (double)ImageSourceHeight / (double)ImageSourceWidth * (double)num3 / size.Height;
			thickness.Top = top;
			double right = ImageDesignWidth * (double)num4 / size.Width;
			thickness.Right = right;
			double bottom = ImageDesignWidth * (double)ImageSourceHeight / (double)ImageSourceWidth * (double)num5 / size.Height;
			thickness.Bottom = bottom;
			bool status = true;
			ImageSource imageSource = null;
			if (GlobalRenderVariable.IsHighQualityImage)
			{
				if (((ImageDataSource.Width - OffSetXPixel - ClipXPixel) * (ImageDataSource.Height - OffSetYPixel - ClipYPixel) < 250000) | flag)
				{
					ImageMargin = thickness;
					if (size.Width < 1.0 || size.Height < 1.0)
					{
						Source = null;
					}
					else
					{
						BitmapScalingMode bitmapScalingMode2 = BitmapScalingMode.NearestNeighbor;
						RenderOptions.SetBitmapScalingMode(sImage, bitmapScalingMode2);
						isHighQualityImage = false;
						imageSource = _byte2Image.BuiltImageSourceA(ImageDataSource, (int)size.Width, (int)size.Height, ref status, OffSetXPixel, OffSetYPixel, ClipXPixel, ClipYPixel, isDirect);
						if (!status)
						{
							return false;
						}
						Source = imageSource;
						isCliped = true;
					}
				}
				else if (!isInner || isCliped)
				{
					ImageMargin = default;
					imageSource = _byte2Image.BuiltImageSourceA(ImageDataSource, (int)size.Width, (int)size.Height, ref status, 0, 0, 0, 0, isDirect);
					if (!status)
					{
						return false;
					}
					Source = imageSource;
					isCliped = false;
				}
			}
			else
			{
				ImageMargin = thickness;
				if (size.Width < 1.0 || size.Height < 1.0)
				{
					Source = null;
				}
				else
				{
					imageSource = _byte2Image.BuiltImageSourceA(ImageDataSource, (int)size.Width, (int)size.Height, ref status, OffSetXPixel, OffSetYPixel, ClipXPixel, ClipYPixel, isDirect);
					if (!status)
					{
						return false;
					}
					Source = imageSource;
					isCliped = true;
				}
			}
			_imageViewWidth = imageSize.Width * ImageScale;
			_imageViewHeight = imageSize.Height * ImageScale;
		}
		needFlash = true;
		return true;
	}

	public void ChangeSize(System.Windows.Point centerPoint, double val)
	{
		if (ImageResizable && val != 1.0 && (!(val > 1.0) || EnableZoomOut) && (!(val < 1.0) || EnableZoomIn))
		{
			if (ImageScale * val < 0.3)
			{
				val = 0.3 / ImageScale;
			}
			if (Math.Abs(curPoint.X - centerPoint.X) > 0.001 || Math.Abs(curPoint.Y - centerPoint.Y) > 0.001)
			{
				offset = new System.Windows.Point(offset.X + (centerPoint.X - curPoint.X) * (ImageScale - 1.0), offset.Y + (centerPoint.Y - curPoint.Y) * (ImageScale - 1.0));
			}
			curPoint.X = centerPoint.X;
			curPoint.Y = centerPoint.Y;
			ImageScale *= val;
			ImageOffSetX = -1.0 * (centerPoint.X * ImageScale - centerPoint.X) + offset.X;
			ImageOffSetY = -1.0 * (centerPoint.Y * ImageScale - centerPoint.Y) + offset.Y;
			ClipImage();
			needFlash = true;
			needFlashExtern = true;
		}
	}

	public void Init()
	{
		IImageViewAddition imageBoxAddition = ImageBoxAddition;
		if (imageBoxAddition == null || !imageBoxAddition.InitOut())
		{
			ImageScale = 1.0;
			EnableZoomIn = true;
			EnableZoomOut = true;
			ImageOffSetX = 0.0;
			ImageOffSetY = 0.0;
			offset.X = 0.0;
			offset.Y = 0.0;
			gridWidth = 0.0;
			viewbox?.SetValue(Canvas.LeftProperty, 0.0);
			viewbox?.SetValue(Canvas.TopProperty, 0.0);
			InitClipImage();
			SetImageSource();
			needFlashExtern = true;
		}
	}

	private void MouseMoveToChangePixel(System.Windows.Point imagePoint)
	{
		double ratio = GetRatio();
		imagePoint.X = Math.Floor(imagePoint.X / ratio);
		imagePoint.Y = Math.Floor(imagePoint.Y / ratio);
		if (imagePoint.X < 0.0 || imagePoint.X >= (double)ImageSourceWidth || imagePoint.Y < 0.0 || imagePoint.Y >= (double)ImageSourceHeight)
		{
			curImageX = -1;
			curImageY = -1;
		}
		else if ((double)curImageX != imagePoint.X || (double)curImageY != imagePoint.Y)
		{
			curImageX = (int)imagePoint.X;
			curImageY = (int)imagePoint.Y;
			CallChangePixel(curImageX, curImageY);
		}
	}

	public void CallChangePixel(int X, int Y)
	{
		if (IsDisplayPixelAuto)
		{
			ChangePixel(X, Y);
		}
	}

	private unsafe void ChangePixel(int X, int Y)
	{
		if (ImageDataSource == null)
		{
			return;
		}
		byte r = 0;
		byte g = 0;
		byte b = 0;
		int num = 0;
		int num2 = 0;
		short value = 0;
		int num3 = 1;
		int num4 = 0;
		PixelFormat pixelFormat = PixelFormats.Default;
		byte* ptr = null;
		bool flag = false;
		pixelFormat = Byte2ImageSource.StringToPixelFormat(ImageDataSource.PixelFormat);
		if (ImageDataSource.Buffer != null)
		{
			num = X;
			num2 = Y;
			num3 = ImageDataSource.Width * pixelFormat.BitsPerPixel / 8;
			fixed (byte* buffer = ImageDataSource.Buffer)
			{
				ptr = buffer;
			}
		}
		else
		{
			if (!(Source is WriteableBitmap writeableBitmap))
			{
				return;
			}
			double num5 = writeableBitmap.Width / (double)ImageDataSource.Width;
			double num6 = writeableBitmap.Height / (double)ImageDataSource.Height;
			num = (int)Math.Floor((double)X * num5);
			num2 = (int)Math.Floor((double)Y * num6);
			num3 = (int)Math.Ceiling(writeableBitmap.Width * (double)pixelFormat.BitsPerPixel / 32.0) * 4;
			ptr = (byte*)(void*)writeableBitmap.BackBuffer;
			flag = true;
		}
		switch (ImageDataSource.PixelFormat)
		{
		case "Gray8":
		{
			num4 = num2 * num3 + num;
			byte b2 = ptr[num4];
			r = b2;
			g = b2;
			b = b2;
			break;
		}
		case "Rgb24":
			num4 = num2 * num3 + pixelFormat.BitsPerPixel * num / 8;
			r = ptr[num4];
			g = (ptr + num4)[1];
			b = (ptr + num4)[2];
			break;
		case "Rgb24_P3":
			if (flag)
			{
				num4 = num2 * num3 + pixelFormat.BitsPerPixel * num / 8;
				r = ptr[num4];
				g = (ptr + num4)[1];
				b = (ptr + num4)[2];
			}
			else
			{
				int num7 = ImageDataSource.Width * ImageDataSource.Height;
				num4 = num2 * ImageDataSource.Width + num;
				r = ptr[num4];
				g = (ptr + num7)[num4];
				b = (ptr + num7 * 2)[num4];
			}
			break;
		}
		if (ImageDataSource is IRangeImageData { RangeBuffer: not null, RangeBuffer: var rangeBuffer } rangeImageData)
		{
			fixed (byte* ptr2 = rangeBuffer)
			{
				short* ptr3 = (short*)ptr2;
				num4 = Y * num3 + X;
				if (num4 >= 0 && num4 < rangeImageData.RangeBuffer.Length / 2)
				{
					value = ptr3[num4];
				}
			}
		}
		SetPixelInfo(X, Y, r, g, b, value);
	}

	public void SetPixelInfo(int X, int Y, int R, int G, int B, int value)
	{
		PhysicalY = MatrixExchangeY(X, Y);
		PhysicalX = MatrixExchangeX(X, Y);
		PixelX = X;
		PixelY = Y;
		PixelG = G;
		PixelR = R;
		PixelB = B;
		RangeValue = value;
		if (ImageDataSource is IImageDataExtern { NeedConvertColor: not false } imageDataExtern)
		{
			PixelInfoLabel = "  |  " + imageDataExtern.PixelInfoLabel;
			if (!(ImageDataSource.PixelFormat == "Rgb24") || imageDataExtern.PixelExtraInfo.Keys.Count == 0 || ImageDataSource.Buffer == null)
			{
				return;
			}
			try
			{
				int num = (Y * ImageDataSource.Width + X) * 3;
				byte blue = ImageDataSource.Buffer[num];
				byte green = ImageDataSource.Buffer[num + 1];
				byte red = ImageDataSource.Buffer[num + 2];
				System.Drawing.Color color = System.Drawing.Color.FromArgb(red, green, blue);
				string key = _byte2Image.GetClassImageGrayVal(color).ToString();
				if (imageDataExtern.PixelExtraInfo.ContainsKey(key))
				{
					PixelInfo = imageDataExtern.PixelExtraInfo[key];
					return;
				}
				PixelInfoLabel = "";
				PixelInfo = "";
				return;
			}
			catch
			{
				return;
			}
		}
		PixelInfoLabel = "";
		PixelInfo = "";
	}

	private void mapcanvas_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (!SplotVis)
		{
			double num = viewbox.ActualWidth * ImageScale;
			double num2 = num / (double)ImageSourceWidth;
			System.Windows.Point position = e.GetPosition(viewbox);
			double num3 = Math.Round((double)e.Delta / 120.0, MidpointRounding.AwayFromZero) * 0.5;
			double num4 = 1.0 + num3;
			if (num4 > 1.0 && num2 > 200.0)
			{
				return;
			}
			ChangeSize(position, num4);
		}
		e.Handled = true;
	}

	private void sfr_Changed(object sender, EventArgs e)
	{
		SetImageSource(isDirect: false, isInner: true);
		EnableZoomIn = ImageScale > 0.3;
		double num = viewbox.ActualWidth * ImageScale;
		double num2 = num / (double)ImageSourceWidth;
		EnableZoomOut = num2 < 200.0;
	}

	public void Enlarge()
	{
		IImageViewAddition imageBoxAddition = ImageBoxAddition;
		if ((imageBoxAddition == null || !imageBoxAddition.EnlargeOut()) && sImage.Source != null)
		{
			System.Windows.Point point = sImage.TranslatePoint(new System.Windows.Point(0.0, 0.0), mapcanvas);
			point.X = mapcanvas.ActualWidth / 2.0 - point.X;
			point.Y = mapcanvas.ActualHeight / 2.0 - point.Y;
			point.X /= ImageScale;
			point.Y /= ImageScale;
			if (ImageScale == 1.0)
			{
				curPoint = point;
			}
			ChangeSize(curPoint, 1.5);
		}
	}

	public void Shrink()
	{
		IImageViewAddition imageBoxAddition = ImageBoxAddition;
		if ((imageBoxAddition == null || !imageBoxAddition.ShrinkOut()) && sImage.Source != null)
		{
			System.Windows.Point point = sImage.TranslatePoint(new System.Windows.Point(0.0, 0.0), mapcanvas);
			point.X = mapcanvas.ActualWidth / 2.0 - point.X;
			point.Y = mapcanvas.ActualHeight / 2.0 - point.Y;
			point.X /= ImageScale;
			point.Y /= ImageScale;
			if (ImageScale == 1.0)
			{
				curPoint = point;
			}
			ChangeSize(curPoint, 0.5);
		}
	}

	private void viewbox_MouseMove(object sender, MouseEventArgs e)
	{
		if (isDragging && ImageDragEanble)
		{
			if (e.LeftButton != MouseButtonState.Pressed)
			{
				isDragging = false;
				return;
			}
			System.Windows.Point position = e.GetPosition(mapcanvas);
			if ((oldMouseMovePosition - position).Length < 0.1)
			{
				return;
			}
			oldMouseMovePosition = position;
			viewbox.SetValue(Canvas.TopProperty, position.Y - mouseOffset.Y + theTop);
			viewbox.SetValue(Canvas.LeftProperty, position.X - mouseOffset.X + theLeft);
			ClipImage();
			needFlash = true;
			if (OnFreshExtendContent != null)
			{
				OnFreshExtendContent(this, null);
			}
		}
		MouseMoveToChangePixel(e.GetPosition(ImageLayers));
	}

	private void ClipImage()
	{
		if (Source != null)
		{
			System.Windows.Point point = viewbox.TranslatePoint(new System.Windows.Point(0.0, 0.0), mapcanvas);
			bool flag = false;
			flag |= ClipImage(point.X, viewbox.ActualWidth, mapcanvas.ActualWidth, ImageSourceWidth, ref _OffSetXPixel, isRightBottom: false);
			flag |= ClipImage(point.Y, viewbox.ActualHeight, mapcanvas.ActualHeight, ImageSourceHeight, ref _OffSetYPixel, isRightBottom: false);
			flag |= ClipImage(point.X, viewbox.ActualWidth, mapcanvas.ActualWidth, ImageSourceWidth, ref _ClipXPixel, isRightBottom: true);
			if (flag | ClipImage(point.Y, viewbox.ActualHeight, mapcanvas.ActualHeight, ImageSourceHeight, ref _ClipYPixel, isRightBottom: true))
			{
				SetImageSource(isDirect: false, isInner: true);
			}
		}
	}

	private bool ClipImage(double offset, double viewboxRange, double parentViewRange, int imageSourceRange, ref int clipPixel, bool isRightBottom)
	{
		double num = (isRightBottom ? (offset + viewboxRange * ImageScale - parentViewRange) : (0.0 - offset));
		double num2 = viewboxRange * ImageScale;
		double d = num * (double)imageSourceRange / num2;
		int num3 = (int)Math.Floor(d);
		if (num3 > clipPixel)
		{
			clipPixel = ((num3 > imageSourceRange) ? imageSourceRange : num3);
			return true;
		}
		if (clipPixel > 0 && num3 < clipPixel)
		{
			clipPixel = ((num3 >= 0) ? num3 : 0);
			return true;
		}
		return false;
	}

	private void mROIView_LostMouseCapture(object sender, MouseEventArgs e)
	{
		if (IsDrawingOfROI)
		{
			mROIView.CaptureMouse();
		}
	}

	private void mouseLBDown(object sender, MouseButtonEventArgs e)
	{
		IsDrawingOfROI = false;
		if (!IsStateForDrawROI)
		{
			theTop = (double)viewbox.GetValue(Canvas.TopProperty);
			theLeft = (double)viewbox.GetValue(Canvas.LeftProperty);
			mouseOffset = e.GetPosition(mapcanvas);
			isDragging = true;
			(sender as FrameworkElement).CaptureMouse();
		}
		else
		{
			if (!ROIVisible || IsDrawingOfROI)
			{
				return;
			}
			IsDrawingOfROI = true;
			Type type = ROITempConverter.GetControlTypeByInterfaceType(DrawROIType);
			if (!(type == null))
			{
				while (type.GetMember("Draw", MemberTypes.Method, BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod).Count() == 0)
				{
					type = type.BaseType;
				}
				type.InvokeMember("Draw", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, new object[2] { mROIView, DrawROIType });
			}
		}
	}

	private void MROIView_DrawSubstituteComplete(object sender, ObjectEventArgs e)
	{
		IsDrawingOfROI = false;
		if (e.Param != null)
		{
			IROI iROI = e.Param as IROI;
			if (iROI is IROIUIType iROIUIType)
			{
				iROIUIType.UIType = ROITypeUI;
			}
			AddROI(iROI);
			OnDrawROICompleted?.Invoke(this, e);
		}
	}

	private void MROI_DrawCompleteComamndExecute(ObjectEventArgs e)
	{
		IsDrawingOfROI = false;
		if (e.Param != null)
		{
			IROI iROI = e.Param as IROI;
			if (iROI is IROIUIType iROIUIType)
			{
				iROIUIType.UIType = ROITypeUI;
			}
			AddROI(iROI);
			OnDrawROICompleted?.Invoke(this, e);
		}
	}

	private void mouseLBUp(object sender, MouseButtonEventArgs e)
	{
		if (isDragging)
		{
			isDragging = false;
		}
		(sender as FrameworkElement).ReleaseMouseCapture();
	}

	private void viewbox_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		OnPropertyChanged("ViewBoxScale");
		OnPropertyChanged("ThicknessScale");
	}

	private void mainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		ImageScale = 1.0;
		ImageOffSetX = 0.0;
		ImageOffSetY = 0.0;
		isDragging = false;
		double width = e.NewSize.Width;
		double height = e.NewSize.Height;
		viewbox.Width = width;
		viewbox.Height = height;
		viewbox.SetValue(Canvas.LeftProperty, 0.0);
		viewbox.SetValue(Canvas.TopProperty, 0.0);
		InitClipImage();
		SetImageSource();
		needFlashExtern = true;
	}

	private void InitClipImage()
	{
		OffSetXPixel = 0;
		OffSetYPixel = 0;
		ClipXPixel = 0;
		ClipYPixel = 0;
		ImageMargin = new Thickness(0.0);
	}

	private void Viewbox_LayoutUpdated(object sender, EventArgs e)
	{
		if (needFlash)
		{
			needFlash = false;
			SetAboveCanvas();
			NeedRefreshShapes = true;
		}
		if (needFlashExtern)
		{
			needFlashExtern = false;
			if (OnFreshExtendContent != null)
			{
				OnFreshExtendContent(this, null);
			}
		}
	}

	public void SaveOriginalImage()
	{
		try
		{
			if (ImageDataSource != null)
			{
				IImageData imageDataSource = ImageDataSource;
				SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Filter = "JPG|*.jpg|JPEG|*.jpeg|PNG|*.png|BMP|*.bmp",
					Title = AppLocalizationService.GetLang("Lang_SaveAs")
				};
				saveFileDialog.FileName = DateTime.Now.ToString("yyyyMMddHHmmss");
				if (saveFileDialog.ShowDialog() == true && !string.IsNullOrEmpty(saveFileDialog.FileName))
				{
					ImageSaveOpenCvHelper.SaveRawImageSyn(saveFileDialog.FileName, imageDataSource);
				}
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	public void SaveRenderImage()
	{
		try
		{
			if (ImageDataSource != null)
			{
				IImageData imageDataSource = ImageDataSource;
				List<object> shapesSource = (ShapeList as IEnumerable<object>)?.Where((object p) => !(p is IIsVisible) || (p is IIsVisible isVisible && isVisible.IsVisible)).ToList();
				SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Filter = "JPG|*.jpg|JPEG|*.jpeg|PNG|*.png|BMP|*.bmp",
					Title = AppLocalizationService.GetLang("Lang_SaveAs")
				};
				saveFileDialog.FileName = DateTime.Now.ToString("yyyyMMddHHmmss");
				if (saveFileDialog.ShowDialog() == true && !string.IsNullOrEmpty(saveFileDialog.FileName))
				{
					ImageSaveOpenCvHelper.SaveRoiImageSyn(saveFileDialog.FileName, (ImageData)imageDataSource, shapesSource, null);
				}
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	public double GetImageViewWidth()
	{
		return _imageViewWidth;
	}

	public double GetImageViewHeight()
	{
		return _imageViewHeight;
	}

	public void AddImageLayer(System.Windows.Controls.Image layer)
	{
		if (ImageLayers != null && !ImageLayers.Children.Contains(layer))
		{
			ImageLayers.Children.Add(layer);
		}
	}

	public void RemoveImageLayer(System.Windows.Controls.Image layer)
	{
		if (ImageLayers != null && ImageLayers.Children.Contains(layer))
		{
			ImageLayers.Children.Remove(layer);
		}
	}

	public bool CheckImageLayer(System.Windows.Controls.Image layer)
	{
		if (ImageLayers == null)
		{
			return false;
		}
		if (ImageLayers.Children.Contains(layer))
		{
			return true;
		}
		return false;
	}

	private void AddROI(IROI roi, IList roiParentList = null)
	{
		if (SelectedSubROIList != null)
		{
			SelectedSubROIList.Add(roi);
		}
		else
		{
			ROIList.Add(roi);
		}
		RoiAddEvent?.Invoke(this, new RoiEventArgs(roi));
		RoiAddCommand?.Execute(roi);
		RoiAddWithImageCommand?.Execute(new RoiWithTagEventArgs(roi, ImageDataSource));
	}

	private void PasteROI(IROI roi, IList roiParentList)
	{
		roiParentList.Add(roi);
		RoiPasteCommand?.Execute(new RoiWithTagEventArgs(roi, roiParentList));
	}

	private void RemoveROI(IROI roi, IList roiParentList = null)
	{
		if (roiParentList == null)
		{
			if (SelectedSubROIList != null)
			{
				SelectedSubROIList.Remove(roi);
			}
			else
			{
				ROIList.Remove(roi);
			}
		}
		else
		{
			roiParentList.Remove(roi);
		}
		RoiDeleteEvent?.Invoke(this, new RoiEventArgs(roi));
		RoiDelCommand?.Execute(new RoiWithTagEventArgs(roi, roiParentList));
	}

	private void sImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		Focus();
	}

	private void mROIView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = (SplotVis ? (ImageBoxAddition?.ROIViewOut as FrameworkElement) : mROIView);
		MouseRightButtonDownPoint = e.GetPosition(frameworkElement);
		Focus();
		VisualTreeHelper.HitTest(frameworkElement, null, (HitTestResult f) =>
		{
			FrameworkElement parent = f.VisualHit.GetParent<FrameworkElement>();
			if (parent != null && parent.IsHitTestVisible && parent.Visibility == Visibility.Visible && parent.DataContext is IROISelectable iROISelectable)
			{
				iROISelectable.IsSelected = true;
				return HitTestResultBehavior.Stop;
			}
			return HitTestResultBehavior.Continue;
		}, new PointHitTestParameters(MouseRightButtonDownPoint));
	}

	private bool CanDeleteRoi(object datas)
	{
		if (RoiDelCommand != null && datas != null)
		{
			IROI roi = (datas as IList)[0] as IROI;
			IList tag = (datas as IList)[1] as IList;
			return RoiDelCommand.CanExecute(new RoiWithTagEventArgs(roi, tag));
		}
		return true;
	}

	public void DeleteRoi(object datas)
	{
		IROI roi = (datas as IList)[0] as IROI;
		IList roiParentList = (datas as IList)[1] as IList;
		RemoveROI(roi, roiParentList);
	}

	public void DeleteRoi(IROI roi)
	{
		if (roi != null)
		{
			RemoveROI(roi);
		}
	}

	private bool CanCopyRoi(object datas)
	{
		if (RoiCopyCommand != null && datas != null)
		{
			IROI roi = (datas as IList)[0] as IROI;
			IList tag = (datas as IList)[1] as IList;
			return RoiCopyCommand.CanExecute(new RoiWithTagEventArgs(roi, tag));
		}
		return true;
	}

	private void CopyRoi(object datas)
	{
		ClearPasteData();
		int hashCode = (datas as IList)[0].GetHashCode();
		int hashCode2 = (datas as IList)[1].GetHashCode();
		PasteList.Add(new List<int> { hashCode, hashCode2 });
		pasteROI = ((datas as IList)[0] as IROIClone)?.Clone();
		if (pasteROI is IROISign iROISign)
		{
			iROISign.Sign = ((datas as IList)[0] as IROISign)?.Sign ?? true;
		}
		if (pasteROI is IROILabel iROILabel && (datas as IList)[0] is IROILabel iROILabel2)
		{
			iROILabel.Label = iROILabel2.Label;
		}
		if (pasteROI is IROIColor iROIColor && (datas as IList)[0] is IROIColor iROIColor2)
		{
			iROIColor.RoiColor = iROIColor2.RoiColor;
		}
	}

	private void ClearPasteData()
	{
		PasteList.Clear();
		pasteROI = null;
	}

	private void PasteRoiRoi()
	{
		System.Windows.Point position = Mouse.GetPosition(mROIView);
		int num = 1;
		int num2 = 1;
		if (pasteROI is INormalizedROI)
		{
			num = ImageSourceWidth;
			num2 = ImageSourceHeight;
		}
		position = new System.Windows.Point(position.X / ControlToImageRadio / (double)num, position.Y / ControlToImageRadio / (double)num2);
		if (!(pasteROI is IROIPaste iROIPaste))
		{
			return;
		}
		bool value = (iROIPaste as IROISign)?.Sign ?? true;
		string text = (iROIPaste as IROILabel)?.Label;
		string text2 = (iROIPaste as IROIColor)?.RoiColor;
		IROIPaste iROIPaste2 = (iROIPaste as IROIClone).Clone() as IROIPaste;
		iROIPaste2.PastROI(position);
		if (iROIPaste2 is IROISign iROISign)
		{
			iROISign.Sign = value;
		}
		if (iROIPaste2 is IROILabel iROILabel && !string.IsNullOrEmpty(text))
		{
			iROILabel.Label = text;
		}
		if (iROIPaste2 is IROIColor iROIColor && !string.IsNullOrEmpty(text2))
		{
			iROIColor.RoiColor = text2;
		}
		IList rOIList;
		if ((rOIList = ROIList) == null)
		{
			return;
		}
		IList list = null;
		if (IsPasteInSelectedSubROIList)
		{
			list = SelectedSubROIList;
		}
		else
		{
			int hash_ls = (int)(PasteList.First() as IList)[1];
			list = ((rOIList.GetHashCode() != hash_ls) ? rOIList.OfType<IList>().SingleOrDefault((IList p) => p.GetHashCode() == hash_ls) : ROIList);
		}
		if (list != null)
		{
			PasteROI(iROIPaste2 as IROI, list);
		}
	}

	public void PrepareRawRoi(Type roiType, ROIUseType useType = ROIUseType.Other)
	{
		if (roiType == null)
		{
			UnPrepareRawRoi();
			return;
		}
		DrawROIType = roiType;
		IsStateForDrawROI = true;
	}

	public void UnPrepareRawRoi()
	{
		DrawROIType = null;
		IsStateForDrawROI = false;
	}

	public void RoiEnableExecute(string b)
	{
		ROIHitTestVisible = Convert.ToBoolean(b);
	}

	internal void SetPointsImage(ImageSource source)
	{
		if (pointsImage != null)
		{
			pointsImage.Source = source;
		}
	}

	public void ClearExternShapes()
	{
		if (pointsImage != null)
		{
			pointsImage.Source = null;
		}
	}

	public void SetBackground(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				BitmapImage bitmapImage = new BitmapImage(new Uri(path, UriKind.Absolute));
				if (bitmapImage.Width > 100.0 || bitmapImage.Height > 100.0)
				{
					HikMessageBox.Show("Lang_ImageTooLargeThan100", MessageIcons.Error, isTranslate: true);
				}
				else if (background != null && mapcanvas != null)
				{
					background.ImageSource = bitmapImage;
					mapcanvas.Background = background;
				}
				else
				{
					colorPath = path;
				}
			}
			else if (!string.IsNullOrEmpty(path) && colorRegex.IsMatch(path))
			{
				System.Drawing.Color color = ColorTranslator.FromHtml(path);
				if (mapcanvas != null)
				{
					mapcanvas.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
				}
				else
				{
					colorPath = path;
				}
			}
			else
			{
				HikMessageBox.Show("Lang_PathORColorError", MessageIcons.Error, isTranslate: true);
			}
		}
		catch (Exception)
		{
			HikMessageBox.Show("Lang_PathORColorError", MessageIcons.Error, isTranslate: true);
		}
	}

	internal void NotifyROISelected(object ls, object source)
	{
		RoiSelectedCommand?.Execute(new RoiWithTagEventArgs(source, ls));
	}

	private void viewbox_MouseLeave(object sender, MouseEventArgs e)
	{
		PixelInfoLabel = "";
		PixelInfo = "";
	}

	private static void OnSplotVisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		if (!imageViewBox.SplotVis)
		{
			imageViewBox.ImageBoxAddition?.InnerImageViewWAddition?.ClearGraphs();
		}
		imageViewBox.OnSplotVisChangedEvent?.Invoke(imageViewBox.SplotVis);
	}

	private static void OnIsAutoModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ImageViewBox imageViewBox = d as ImageViewBox;
		object oldValue = e.OldValue;
		object newValue = e.NewValue;
		if (imageViewBox != null && imageViewBox.IsShowRangeImage && oldValue != newValue)
		{
			imageViewBox.ImageBoxAddition?.InnerImageViewWAddition?.SetImageSource3D();
		}
	}

	protected void ValueChangedCmdExecute()
	{
		if (IsShowRangeImage && (OldMinValue != MinValue3D || OldMaxValue != MaxValue3D))
		{
			OldMinValue = MinValue3D;
			OldMaxValue = MaxValue3D;
			ImageBoxAddition?.InnerImageViewWAddition.SetImageSource3D();
		}
	}

	public double GetViewBoxWidth()
	{
		return viewbox.ActualWidth;
	}

	public double GetViewBoxHeight()
	{
		return viewbox.ActualHeight;
	}

	private void mouseLBDown2(object sender, MouseButtonEventArgs e)
	{
		if (IsStateForDrawROI)
		{
			if (!IsDrawingOfROI)
			{
				Type type = ROITempConverter.GetControlTypeByInterfaceType(DrawROIType);
				if (type != null)
				{
					while (type.GetMember("Draw", MemberTypes.Method, BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod).Count() == 0)
					{
						type = type.BaseType;
					}
					type.InvokeMember("Draw", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, new object[2]
					{
						ImageBoxAddition?.ROIViewOut,
						DrawROIType
					});
				}
				IsDrawingOfROI = true;
			}
			else
			{
				(ImageBoxAddition?.ROIViewOut as ROIItemsPage).CaptureMouse();
			}
		}
		else
		{
			IsDrawingOfROI = false;
		}
	}

	private void mouseLBUp2(object sender, MouseButtonEventArgs e)
	{
		if (isDragging)
		{
			isDragging = false;
		}
		(sender as FrameworkElement).ReleaseMouseCapture();
	}

	private void sImage_MouseRightButtonDown2(object sender, MouseButtonEventArgs e)
	{
		MouseRightButtonDownPoint = e.GetPosition(ImageBoxAddition?.InputElementOut as IInputElement);
		Focus();
	}
}
