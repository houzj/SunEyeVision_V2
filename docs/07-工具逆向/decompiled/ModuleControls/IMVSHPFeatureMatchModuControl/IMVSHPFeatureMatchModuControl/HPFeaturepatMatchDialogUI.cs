using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Apps.BaseData;
using Apps.Data;
using Apps.Data.ShapeDefine;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using FeatureMatchCommonControl;
using Hik.Util;
using Hik.Xml;
using HikExternalCall.Common;
using HikExternalCall.ImageProcess;
using HikPcUI.ImageView;
using Microsoft.Win32;
using ModelCommonHelper;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VM.ModuleControl.Controls.FeatureMatch;
using VM.PlatformSDKCS;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMBaseControls;
using VMBaseControls.Base.ImageView;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace IMVSHPFeatureMatchModuControl;

public class HPFeaturepatMatchDialogUI : FeaturepatMatchDialogUIBase
{
	public ImageViewBox imageViewBox;

	protected Button BtnCurImage;

	protected RadioButton Arrow;

	protected ContentControl windowShadow;

	protected ToggleButton cbIsSaveImage;

	protected Grid viewGrid;

	private bool IsMatching = false;

	public bool isLoading = false;

	internal bool isClosing = false;

	public HPFeaturepatMatchDialogData featurepatMatchDialogData;

	public Window Owner;

	public bool NeedBuild;

	public byte[] ModelData;

	protected System.Windows.Controls.Image tempImage = new System.Windows.Controls.Image();

	private List<string> history = new List<string>();

	private int historyIndex = -1;

	private ImageData _Image;

	private IImageData _ImageDataSource;

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	protected Byte2ImageSource MaskSourceConvertor = new Byte2ImageSource();

	private ICommand _SelectCurImageCommand;

	private ICommand _SelectOtherImageCommand;

	private ICommand _SaveCommand;

	private ICommand _ClearCommand;

	protected const uint MaskSignColorRed = 1291786778u;

	protected const uint HighLightMaskSignColorRed = 2583632410u;

	private const uint MaskSignColorBlue = 2148494067u;

	private const uint HighLightMaskSignColorBlue = 2953800435u;

	private ICommand _BackStepCommand;

	private ICommand _ForwardStepCommand;

	private int _PanRectWidth = 10;

	private int _PanRectWidth2 = 10;

	private ICommand _Rubber2Command;

	private RubberControl rubber;

	private RubberControl rubber2;

	private ICommand _RubberCommand;

	private ICommand _MatchCommand;

	private ICommand _RadioButtonCommand;

	private RadioButton curSelectedRadioButton;

	private bool CrossIsReady = false;

	private bool CrossIsSet = false;

	private ICommand _ConfirmCommand;

	private Assembly assembly = null;

	private const int COLOR_GREEN = 65280;

	private ICommand _DeleteCommand;

	private ICommand _ZoomOrgBtnCommand;

	private ICommand _ZoomFullBtnCommand;

	private ICommand _IsUsePenCommand;

	private ICommand _GirdMouseDownCommand;

	public IParamsConfig OwnerModuleParam { get; set; }

	public IVmModule OwnerModule { get; set; }

	public List<SignViewItem> SignList { get; set; }

	private HPFeaturepatMatchDialogData HPFeaturepatMatchDialogDataBase { get; set; }

	public SignViewItem SelectedSignItem { get; set; }

	public ImageData CurImage
	{
		get
		{
			return _Image;
		}
		set
		{
			_Image = value;
			if (_Image != null)
			{
				ShowImage();
				historyIndex = -1;
				RecordHistory();
				((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("BackEnable");
				((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("ForwardEnable");
			}
		}
	}

	public bool BackEnable => history.Count > historyIndex && historyIndex > 0;

	public bool ForwardEnable => history.Count > historyIndex + 1 && historyIndex > -2;

	public IImageData ImageDataSource
	{
		get
		{
			return _ImageDataSource;
		}
		set
		{
			if (_ImageDataSource != value)
			{
				_ImageDataSource = value;
				((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("ImageDataSource");
			}
		}
	}

	public ICommand SelectCurImageCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _SelectCurImageCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)SelectCurImageCommandExecute);
				ICommand command2 = (ICommand)val;
				_SelectCurImageCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand SelectOtherImageCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _SelectOtherImageCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)SelectOtherImageCommandExecute);
				ICommand command2 = (ICommand)val;
				_SelectOtherImageCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	protected virtual string FilterName => "xml files(*.hpmxml)|*.hpmxml";

	public ICommand SaveCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _SaveCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)SaveCommandExecute);
				ICommand command2 = (ICommand)val;
				_SaveCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand ClearCommand => _ClearCommand ?? (_ClearCommand = new DelegateCommand(ClearCommandExecute));

	public ICommand BackStepCommand => _BackStepCommand ?? (_BackStepCommand = new DelegateCommand(BackStepCommandExecute));

	public ICommand ForwardStepCommand => _ForwardStepCommand ?? (_ForwardStepCommand = new DelegateCommand(ForwardStepCommandExecute));

	public int PanRectWidth
	{
		get
		{
			return _PanRectWidth;
		}
		set
		{
			if (_PanRectWidth != value)
			{
				if (CurImage != null && (value > CurImage.Width || value > CurImage.Height))
				{
					_PanRectWidth = Convert.ToInt32((CurImage.Width > CurImage.Height) ? CurImage.Height : CurImage.Width);
					((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("PanRectWidth");
				}
				else
				{
					_PanRectWidth = value;
					((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("PanRectWidth");
				}
			}
		}
	}

	public int PanRectWidth2
	{
		get
		{
			return _PanRectWidth2;
		}
		set
		{
			if (CurImage != null && (value > CurImage.Width || value > CurImage.Height))
			{
				_PanRectWidth2 = Convert.ToInt32((CurImage.Width > CurImage.Height) ? CurImage.Height : CurImage.Width);
				((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("PanRectWidth2");
			}
			else
			{
				_PanRectWidth2 = value;
				((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("PanRectWidth2");
			}
		}
	}

	public ICommand Rubber2Command => _Rubber2Command ?? (_Rubber2Command = (ICommand)new DelegateCommand<RadioButton>((Action<RadioButton>)Rubber2CommandExecute));

	public ICommand RubberCommand => _RubberCommand ?? (_RubberCommand = (ICommand)new DelegateCommand<RadioButton>((Action<RadioButton>)RubberCommandExecute));

	public ICommand MatchCommand => _MatchCommand ?? (_MatchCommand = (ICommand)new DelegateCommand<bool?>((Action<bool?>)MatchCommandExecute));

	public ICommand RadioButtonCommand => _RadioButtonCommand ?? (_RadioButtonCommand = (ICommand)new DelegateCommand<RadioButton>((Action<RadioButton>)RadioButtonCommandExecute));

	public ICommand ConfirmCommand => _ConfirmCommand ?? (_ConfirmCommand = new DelegateCommand(ConfirmCommandExecute));

	public Assembly Assembly
	{
		get
		{
			if (assembly == null)
			{
				string currentPath = CurrentPathHelper.Instance.GetCurrentPath();
				currentPath += "Module(sp)\\x64\\.CommonControl\\PatCreateCSharp.dll";
				try
				{
					assembly = Assembly.UnsafeLoadFrom(currentPath);
				}
				catch (Exception arg)
				{
					LogHelper.objLog.Error($"Get assembly error:{arg},assembly path={currentPath}");
				}
			}
			return assembly;
		}
	}

	public object OwnerParam { get; internal set; }

	public ICommand DeleteCommand => _DeleteCommand ?? (_DeleteCommand = (ICommand)new DelegateCommand<Button>((Action<Button>)DeleteCommandExecute));

	public ICommand ZoomOrgBtnCommand => _ZoomOrgBtnCommand ?? (_ZoomOrgBtnCommand = new DelegateCommand(ZoomOrgBtnCommandExecute));

	public ICommand ZoomFullBtnCommand => _ZoomFullBtnCommand ?? (_ZoomFullBtnCommand = new DelegateCommand(ZoomFullBtnCommandExecute));

	public ICommand IsUsePenCommand => _IsUsePenCommand ?? (_IsUsePenCommand = (ICommand)new DelegateCommand<ToggleButton>((Action<ToggleButton>)IsUsePenCommandExecute));

	public ICommand GirdMouseDownCommand => _GirdMouseDownCommand ?? (_GirdMouseDownCommand = new DelegateCommand(GirdMouseDownCommandExecute));

	static HPFeaturepatMatchDialogUI()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HPFeaturepatMatchDialogUI), new FrameworkPropertyMetadata(typeof(HPFeaturepatMatchDialogUI)));
	}

	public HPFeaturepatMatchDialogUI()
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected Obj, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected Obj, but got Unknown
	}

	~HPFeaturepatMatchDialogUI()
	{
		try
		{
		}
		finally
		{
			((object)this).Finalize();
		}
	}

	public override void OnApplyTemplate()
	{
		((FrameworkElement)this).OnApplyTemplate();
		imageViewBox = ((Control)this).Template.FindName("imageViewBox", (FrameworkElement)(object)this) as ImageViewBox;
		BtnCurImage = ((Control)this).Template.FindName("BtnCurImage", (FrameworkElement)(object)this) as Button;
		Arrow = ((Control)this).Template.FindName("Arrow", (FrameworkElement)(object)this) as RadioButton;
		windowShadow = ((Control)this).Template.FindName("windowShadow", (FrameworkElement)(object)this) as ContentControl;
		cbIsSaveImage = ((Control)this).Template.FindName("cbIsSaveImage", (FrameworkElement)(object)this) as ToggleButton;
		viewGrid = ((Control)this).Template.FindName("viewGrid", (FrameworkElement)(object)this) as Grid;
		Init();
		InitSign();
		RefreshPoints();
		imageViewBox.DataContext = ((FrameworkElement)this).DataContext;
	}

	private void Init()
	{
		featurepatMatchDialogData.IsStateForDrawROI = false;
		NeedBuild = true;
		((FrameworkElement)this).Loaded += FastFeaturepatMatchDialogUI_Loaded;
		((FrameworkElement)this).Unloaded += FastFeaturepatMatchDialogUI_Unloaded;
		this.imageViewBox.RoiAddEvent += ImageViewBox_RoiAddEvent;
		this.imageViewBox.RoiParamChangedEvent += ImageViewBox_RoiParamChangedEvent;
		this.imageViewBox.MouseLeftButtonDown += imageView_MouseLeftButtonDown;
		ImageViewBox imageViewBox = this.imageViewBox;
		imageViewBox.OnFreshExtendContent = (ImageViewBox.FreshExtendContent)Delegate.Combine(imageViewBox.OnFreshExtendContent, new ImageViewBox.FreshExtendContent(imageView_OnFreshExtendContent));
		((FrameworkElement)this).DataContext = featurepatMatchDialogData;
		this.imageViewBox.DataContext = featurepatMatchDialogData;
		tempImage.MouseLeftButtonUp += tempImage_PreviewMouseLeftButtonUp;
		tempImage.PreviewKeyUp += tempImage_PreviewKeyUp;
		tempImage.MouseEnter += tempImage_MouseEnter;
		tempImage.MouseMove += tempImage_MouseMove;
		tempImage.MouseWheel += tempImage_MouseWheel;
		tempImage.IsHitTestVisible = false;
		tempImage.Focusable = true;
		tempImage.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
		Owner.Closing += Owner_Closing;
		HPFeaturepatMatchDialogDataBase = ((FrameworkElement)this).DataContext as HPFeaturepatMatchDialogData;
		if (((FrameworkElement)this).DataContext != null)
		{
			ICatchCoreInfo obj = OwnerModule as ICatchCoreInfo;
			if (obj != null && obj.IsModelTrigger())
			{
				HPFeaturepatMatchDialogDataBase.MatchCommand -= HPFeaturepatMatchDialogUI_MatchCommand;
				HPFeaturepatMatchDialogDataBase.MatchCommand += HPFeaturepatMatchDialogUI_MatchCommand;
			}
		}
		if (HPFeaturepatMatchDialogDataBase != null)
		{
			((FeaturepatMatchDialogDataBase)HPFeaturepatMatchDialogDataBase).PropertyChanged -= HPFeaturepatMatchDialogUI_PropertyChanged;
			((FeaturepatMatchDialogDataBase)HPFeaturepatMatchDialogDataBase).PropertyChanged += HPFeaturepatMatchDialogUI_PropertyChanged;
		}
		Panel.SetZIndex(tempImage, 100);
	}

	private void HPFeaturepatMatchDialogUI_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "IsUsePen" && !HPFeaturepatMatchDialogDataBase.IsUsePen)
		{
			ClearCommandExecute();
		}
	}

	private void HPFeaturepatMatchDialogUI_MatchCommand()
	{
		MatchSelfExecute();
	}

	private void Owner_Closing(object sender, CancelEventArgs e)
	{
		if (((FrameworkElement)this).DataContext != null)
		{
			(((FrameworkElement)this).DataContext as HPFeaturepatMatchDialogData).MatchCommand -= HPFeaturepatMatchDialogUI_MatchCommand;
		}
	}

	private void ImageViewBox_RoiParamChangedEvent(object sender, VMControls.RenderInterface.RoiEventArgs e)
	{
		RecordHistory();
		MatchSelfExecute();
	}

	protected void MatchSelfExecute()
	{
		ICatchCoreInfo obj = OwnerModule as ICatchCoreInfo;
		if (obj != null && obj.IsModelTrigger() && !IsMatching)
		{
			try
			{
				IsMatching = true;
				MatchCommandExecute(false);
			}
			finally
			{
				IsMatching = false;
			}
		}
	}

	private void FastFeaturepatMatchDialogUI_Unloaded(object sender, RoutedEventArgs e)
	{
		(imageViewBox.ExtenAbove as Canvas).Children.Remove(tempImage);
	}

	private void FastFeaturepatMatchDialogUI_Loaded(object sender, RoutedEventArgs e)
	{
		(imageViewBox.ExtenAbove as Canvas).Children.Add(tempImage);
	}

	private void ImageViewBox_RoiAddEvent(object sender, VMControls.RenderInterface.RoiEventArgs e)
	{
		if (curSelectedRadioButton != null)
		{
			curSelectedRadioButton.IsChecked = false;
			curSelectedRadioButton = null;
		}
	}

	protected virtual void CustomInitSign()
	{
		SignList.Add(new SignViewItem
		{
			DisplayValue = AppLocalizationService.GetLang("Lang_EnhanceMatch"),
			ComboxImgNormal = "/Apps.XmlUI;component/Resources/ic_matching_normal.png",
			ComboxImgDisable = "/Apps.XmlUI;component/Resources/ic_matching_disable.png",
			ComboxImgSelect = "/Apps.XmlUI;component/Resources/ic_matching_select.png",
			ComboxItemImgNormal = "/Apps.XmlUI;component/Resources/ic_matching_small_normal.png",
			ComboxItemImgDisable = "/Apps.XmlUI;component/Resources/ic_matching_small_disable.png",
			ComboxItemImgSelect = "/Apps.XmlUI;component/Resources/ic_matching_small_select.png"
		});
	}

	protected virtual void InitSign()
	{
		SignList = new List<SignViewItem>();
		SignList.Add(new SignViewItem
		{
			DisplayValue = AppLocalizationService.GetLang("Lang_Match"),
			ComboxImgNormal = "/Apps.XmlUI;component/Resources/ic_yes_normal.png",
			ComboxImgDisable = "/Apps.XmlUI;component/Resources/ic_yes_disable.png",
			ComboxImgSelect = "/Apps.XmlUI;component/Resources/ic_yes_select.png",
			ComboxItemImgNormal = "/Apps.XmlUI;component/Resources/ic_yes_small_normal.png",
			ComboxItemImgDisable = "/Apps.XmlUI;component/Resources/ic_yes_small_disable.png",
			ComboxItemImgSelect = "/Apps.XmlUI;component/Resources/ic_yes_small_select.png"
		});
		CustomInitSign();
		SignList.Add(new SignViewItem
		{
			DisplayValue = AppLocalizationService.GetLang("Lang_Blind"),
			ComboxImgNormal = "/Apps.XmlUI;component/Resources/ic_shield_normal.png",
			ComboxImgDisable = "/Apps.XmlUI;component/Resources/ic_shield_disable.png",
			ComboxImgSelect = "/Apps.XmlUI;component/Resources/ic_shield_select.png",
			ComboxItemImgNormal = "/Apps.XmlUI;component/Resources/ic_shield_small_normal.png",
			ComboxItemImgDisable = "/Apps.XmlUI;component/Resources/ic_shield_small_disable.png",
			ComboxItemImgSelect = "/Apps.XmlUI;component/Resources/ic_shield_small_select.png"
		});
	}

	public void HideCurImageButton()
	{
		BtnCurImage.Visibility = Visibility.Collapsed;
	}

	private void tempImage_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void tempImage_MouseMove(object sender, MouseEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void tempImage_MouseEnter(object sender, MouseEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void tempImage_PreviewKeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Delete)
		{
			byte[] otherMask = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask;
			if (otherMask != null)
			{
				ReViewMaskArea(otherMask);
				tempImage.Tag = null;
				RecordHistory();
			}
			byte[] otherMask2 = featurepatMatchDialogData.otherMask2;
			if (otherMask2 != null)
			{
				ReViewMaskArea(otherMask2);
				tempImage.Tag = null;
				RecordHistory();
			}
		}
	}

	private void ReViewMaskArea(byte[] otherMask)
	{
		if (otherMask != null)
		{
			byte[] bytes = BitConverter.GetBytes(0);
			for (int i = 0; i < otherMask.Length; i++)
			{
				if (otherMask[i] > 10)
				{
					otherMask[i] = 0;
				}
			}
		}
		DrawMask();
	}

	private void tempImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void imageView_OnFreshExtendContent(object sender, object Tag)
	{
		DrawMask();
	}

	protected virtual void imageView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (Arrow.IsChecked != true)
		{
			return;
		}
		System.Windows.Point point = new System.Windows.Point(imageViewBox.PixelX, imageViewBox.PixelY);
		if (CurImage == null || point.X < 0.0 || point.X >= (double)CurImage.Width || point.Y < 0.0 || point.Y >= (double)CurImage.Height)
		{
			return;
		}
		int num = (int)(point.Y * (double)CurImage.Width + point.X);
		byte[] otherMask = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask;
		if (otherMask != null && otherMask.Count() > num)
		{
			byte b = otherMask[num];
			if (b > 0 && b < 10)
			{
				ReFreshMask(otherMask);
				uint color = 2583632410u;
				setUnicomColor(num, otherMask, color, b);
				DrawMask();
				tempImage.Focus();
			}
			else if (b == 0)
			{
				ReFreshMask(otherMask);
			}
		}
		otherMask = featurepatMatchDialogData.otherMask2;
		if (otherMask != null && otherMask.Count() > num)
		{
			byte b2 = otherMask[num];
			if (b2 > 0 && b2 < 10)
			{
				ReFreshMask(otherMask);
				uint color2 = 2953800435u;
				setUnicomColor(num, otherMask, color2, b2);
				DrawMask();
				tempImage.Focus();
			}
			else if (b2 == 0)
			{
				ReFreshMask(otherMask);
			}
		}
	}

	protected void setUnicomColor(int index, byte[] otherMask, uint color, byte masknum)
	{
		int[] BorderArray = new int[1];
		if (index > 0 && index < otherMask.Length && IsSamePoint(otherMask[index], masknum))
		{
			SetHighLightColor(index, otherMask, color);
			BorderArray[0] = index;
			int Dindex = 1;
			while (Dindex > 0)
			{
				setUnicomColor(otherMask, color, masknum, ref BorderArray, ref Dindex);
			}
		}
	}

	private void setUnicomColor(byte[] otherMask, uint color, byte masknum, ref int[] BorderArray, ref int Dindex)
	{
		int num = 0;
		int[] array = new int[Dindex];
		Buffer.BlockCopy(BorderArray, 0, array, 0, Dindex * 4);
		for (int i = 0; i < Dindex; i++)
		{
			int num2 = array[i];
			int num3 = num2 / CurImage.Width;
			int num4 = num2 % CurImage.Width;
			int num5 = num2 / CurImage.Width * CurImage.Width;
			int num6 = num5 + CurImage.Width - 1;
			if (num3 - 1 >= 0)
			{
				int k = (num3 - 1) * CurImage.Width + num4;
				SetNewBordPoint(otherMask, masknum, k, color, ref BorderArray, ref num);
			}
			if (num3 - 1 >= 0 && num4 + 1 < CurImage.Width)
			{
				int k2 = num4 + 1 + (num3 - 1) * CurImage.Width;
				SetNewBordPoint(otherMask, masknum, k2, color, ref BorderArray, ref num);
			}
			if (num4 + 1 < CurImage.Width)
			{
				int k3 = num5 + num4 + 1;
				SetNewBordPoint(otherMask, masknum, k3, color, ref BorderArray, ref num);
			}
			if (num4 + 1 < CurImage.Width && num3 + 1 < CurImage.Height)
			{
				int k4 = num4 + 1 + (num3 + 1) * CurImage.Width;
				SetNewBordPoint(otherMask, masknum, k4, color, ref BorderArray, ref num);
			}
			if (num3 + 1 < CurImage.Height)
			{
				int k5 = (num3 + 1) * CurImage.Width + num4;
				SetNewBordPoint(otherMask, masknum, k5, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0 && num3 + 1 < CurImage.Height)
			{
				int k6 = (num3 + 1) * CurImage.Width + num4 - 1;
				SetNewBordPoint(otherMask, masknum, k6, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0)
			{
				int k7 = num3 * CurImage.Width + num4 - 1;
				SetNewBordPoint(otherMask, masknum, k7, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0 && num3 - 1 >= 0)
			{
				int k8 = (num3 - 1) * CurImage.Width + num4 - 1;
				SetNewBordPoint(otherMask, masknum, k8, color, ref BorderArray, ref num);
			}
		}
		Dindex = num;
	}

	private void SetNewBordPoint(byte[] otherMask, byte masknum, int k, uint color, ref int[] BorderArray, ref int num)
	{
		if (IsSamePoint(otherMask[k], masknum))
		{
			SetHighLightColor(k, otherMask, color);
			JudgeArray(ref BorderArray, num);
			BorderArray[num] = k;
			num++;
		}
	}

	private void JudgeArray(ref int[] BorderArray, int num)
	{
		if (BorderArray.Length < num + 1)
		{
			int[] src = BorderArray;
			BorderArray = new int[BorderArray.Length + 10000];
			Buffer.BlockCopy(src, 0, BorderArray, 0, num * 4);
		}
	}

	private void SetHighLightColor(int index, byte[] otherMask, uint color)
	{
		otherMask[index] += 10;
	}

	private bool IsSamePoint(byte point, byte cPoint)
	{
		return point > 0 && point < 10 && point == cPoint;
	}

	protected virtual void CustomRefreshByModelData()
	{
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).NotifyPropertyChanged("FinePyramidscale");
	}

	public virtual void RefreshByModelData()
	{
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).NotifyPropertyChanged("ItemsData");
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).NotifyPropertyChanged("FixPointX");
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).NotifyPropertyChanged("FixPointY");
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).NotifyPropertyChanged("RoughPyramidscale");
		CustomRefreshByModelData();
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).NotifyPropertyChanged("HighThreshold");
		CurImage = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).CurImageDataA;
		FreshOtherMask();
		RefreshPoints();
	}

	protected virtual HPFeaturepatMatchDialogData GetFeaturepatMatchDialogData()
	{
		return new HPFeaturepatMatchDialogData();
	}

	protected void RecordHistory()
	{
		try
		{
			HPFeaturepatMatchDialogData hPFeaturepatMatchDialogData = GetFeaturepatMatchDialogData();
			((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData).ItemsData = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData;
			((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData).otherMask = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask;
			hPFeaturepatMatchDialogData.otherMask2 = featurepatMatchDialogData.otherMask2;
			((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData)._FixPointX = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData)._FixPointX;
			((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData)._FixPointY = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData)._FixPointY;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ModelData = null;
			string text = System.IO.Path.GetFullPath("./Temporary") + "\\FeaturepatMatch_" + (historyIndex + 1) + ".xml";
			((BaseNode)(object)hPFeaturepatMatchDialogData).Save(text);
			historyIndex++;
			history.Insert(historyIndex, text);
			history.RemoveRange(historyIndex + 1, history.Count - (historyIndex + 1));
			((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("BackEnable");
			((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("ForwardEnable");
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"Record History Err:{ex.Message}");
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	private void ChangeHistory(string path)
	{
		HPFeaturepatMatchDialogData hPFeaturepatMatchDialogData = GetFeaturepatMatchDialogData();
		if (hPFeaturepatMatchDialogData != null)
		{
			((BaseNode)(object)hPFeaturepatMatchDialogData).loadXml(path, false);
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData = ((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData).ItemsData;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ReFreshData();
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask = ((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData).otherMask;
			featurepatMatchDialogData.otherMask2 = hPFeaturepatMatchDialogData.otherMask2;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).FixPointX = ((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData).FixPointX;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).FixPointY = ((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData).FixPointY;
			AddBindingCollectionEvent();
			FreshOtherMask();
		}
	}

	public void ShowImage()
	{
		if (CurImage != null)
		{
			try
			{
				ImageDataSource = CurImage;
			}
			catch (Exception ex)
			{
				LogHelper.objLog.Error($"Set ImageSource Err:{ex.Message}");
			}
		}
	}

	private void InitData()
	{
		NeedBuild = true;
		tempImage.Source = null;
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).PointsData.Clear();
		if (!featurepatMatchDialogData.MatchPointLock)
		{
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).FixPointX = 0.0;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).FixPointY = 0.0;
		}
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).NotifyPropertyChanged("EditOutlinePointsEnable");
	}

	protected virtual void ItemsData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		featurepatMatchDialogData.AutoSetFixPoint();
		RecordHistory();
		ObservableCollection<IROI> itemsData = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData;
		if (itemsData != null && itemsData.Count > 0)
		{
			MatchSelfExecute();
		}
		else
		{
			DoClear();
		}
	}

	protected void Cross_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		RecordHistory();
	}

	private void DoClear()
	{
		if (featurepatMatchDialogData != null)
		{
			if (((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData.Count > 0)
			{
				((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData.Clear();
			}
			bool flag = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask != null || featurepatMatchDialogData.otherMask2 != null || Enumerable.Count(((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ShapeList, (object p) => p is IMultiPoint multiPoint && multiPoint.MicroPoints.Count > 0) > 0;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask = null;
			featurepatMatchDialogData.otherMask2 = null;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ShapeList.Clear();
			imageViewBox?.ClearExternShapes();
			if (flag)
			{
				RecordHistory();
			}
		}
		ClearMaskData();
	}

	private void ClearMaskData()
	{
		tempImage.Source = null;
		MaskSourceConvertor.Clear();
	}

	private void SelectCurImageCommandExecute()
	{
		LogHelper.objLog.Info("Select current image button click.");
		try
		{
			GetCurImage();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner, AppLocalizationService.GetLang("Lang_Feature"), ex.Message, MessageIcons.Error);
			LogHelper.objLog.Error($"Get curImage error:{ex.Message}");
			return;
		}
		if (CurImage != null && CurImage.ImageBuffer != null)
		{
			InitData();
			DoClear();
		}
		LogHelper.objLog.Info("Select current image button done.");
	}

	private void SelectOtherImageCommandExecute()
	{
		LogHelper.objLog.Info("Select other image buuton click.");
		ImageData imageData = new ImageData();
		imageData.PixelFormat = PixelFormats.Gray8;
		try
		{
			string path = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
			ImageProcessManager.GetMono8ImageDataByFileDialog(ref imageData.Width, ref imageData.Height, ref imageData.ImageBuffer, ref path);
			if (imageData.ImageBuffer != null)
			{
				InitData();
				DoClear();
				CurImage = imageData;
			}
			if (File.Exists(path))
			{
				ConfigurationHelper.INISetDefaultOpenPath(System.IO.Path.GetDirectoryName(path), OwnerModule?.ModuleDir);
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"Choose Other Image Err:{ex.Message}");
			HikMessageBox.Show(Owner, AppLocalizationService.GetLang("Lang_Feature"), ex.Message, MessageIcons.Error);
		}
		LogHelper.objLog.Info("Select other image buuton click done.");
	}

	public void GetCurImage()
	{
		if (OwnerModule == null || OwnerModuleParam == null)
		{
			return;
		}
		if (OwnerModule is IVmModuleSet vmModuleSet)
		{
			CurImage = vmModuleSet.GetFirstParamRelationModule((OwnerParam as NameValueItem).Name)?.GetFirstData();
			GetImageBuffer(CurImage);
			DoClear();
			return;
		}
		string path = System.IO.Path.GetDirectoryName(OwnerModuleParam.XmlPath) + $"\\{OwnerModule.ModuleTypeName}Display.xml";
		ParamRoot paramRoot = new ParamRoot(path);
		paramRoot.Owner = OwnerModule;
		if (paramRoot == null)
		{
			return;
		}
		IList<object> displayList = paramRoot.GetDisplayList();
		foreach (Apps.XmlParser.Object item in displayList)
		{
			if (item.TypeA == "image" || item.TypeA == "pixelinfoimage")
			{
				ImageData imageData = (ImageData)item.GetValue(uint.MaxValue);
				GetImageBuffer(imageData);
				if (imageData != null && imageData.PixelFormat != PixelFormats.Gray8)
				{
					throw new Exception(AppLocalizationService.GetLang("Lang_UnsupportFormatWarning"));
				}
				CurImage = imageData;
				DoClear();
				break;
			}
		}
	}

	private void GetImageBuffer(ImageData image)
	{
		if (image != null && image.ImageBuffer == null)
		{
			image.ImageBuffer = Byte2ImageSource.GetImageBufferByFileName(image.MemoryAddress, image.Width, image.Height, image.PixelFormat);
			image = ((image.ImageBuffer == null) ? null : image);
		}
	}

	protected virtual SDKFeatureMatchUIData GetUIData()
	{
		return (SDKFeatureMatchUIData)(object)new SDKHPFeatureMatchUIData();
	}

	protected virtual void CustomSave(string fileName)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected Obj, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected Obj, but got Unknown
		SDKModelData val = new SDKModelData();
		SDKFeatureMatchUIData uIData = GetUIData();
		SDKFeatureMatchData sDKImportData = new SDKFeatureMatchData
		{
			ModuleModelData = (object)new SDKFeaturetMatchModelData
			{
				AddSpareModel_ByteArray = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ModelData
			},
			UserInterfaceData = uIData
		};
		if (cbIsSaveImage.IsChecked == true)
		{
			FeaturepatMatchDialogDataBase val2 = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).DeepCopyModelData(true, false);
			uIData.BasicData_ByteArray = Encoding.UTF8.GetBytes(((BaseNode)(object)val2).ToXmlStringWithHead(true));
			uIData.CurImageData_ByteArray = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).CurImageDataA.ImageBuffer;
			uIData.OtherMask_ByteArray = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask;
			uIData.PointsData_ByteArray = ExternalCallHelper.StructsToBytes(((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).PointsData);
			if (uIData is SDKHPFeatureMatchUIData sDKHPFeatureMatchUIData)
			{
				sDKHPFeatureMatchUIData.OtherMask2_ByteArray = featurepatMatchDialogData.otherMask2;
				sDKHPFeatureMatchUIData.PointsNums = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).PointsData.Count;
			}
		}
		else
		{
			FeaturepatMatchDialogDataBase val3 = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).DeepCopyModelData(true, true);
			uIData.BasicData_ByteArray = Encoding.UTF8.GetBytes(((BaseNode)(object)val3).ToXmlStringWithHead(true));
		}
		val.SDKImportData = sDKImportData;
		string s = ((BaseNode)(object)val).ToXmlStringWithHead(false);
		try
		{
			EncryptionAndDecryptionHelper.WriteLibFile(fileName, Encoding.UTF8.GetBytes(s));
		}
		catch (VmException ex)
		{
			int errorCode = ex.errorCode;
			HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + " " + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + " " + AppLocalizationService.GetLang(ErrorCodeInfo.GetErrorInfo(errorCode)), MessageIcons.Error);
		}
		catch (Exception ex2)
		{
			string lang = AppLocalizationService.GetLang("保存文件数据失败");
			LogHelper.Error($"{lang}:{ex2.Message}");
			HikMessageBox.Show(lang, MessageIcons.Error, isTranslate: true);
		}
	}

	private void SaveCommandExecute()
	{
		LogHelper.objLog.Info("Export model start.");
		int num = ShowPoints(featurepatMatchDialogData, needPopTip: true, needOkPopTip: false, needApply: true);
		if (num != 0)
		{
			LogHelper.objLog.Error($"Show points fail,errorcode={num}.");
			return;
		}
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = FilterName
		};
		string path = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
		Apps.UIHelper.Extern.UIHelper.CheckInitDirectory(saveFileDialog, path);
		LogHelper.objLog.Info($"Want to export file:{saveFileDialog.FileName}.");
		if (saveFileDialog.ShowDialog() == true)
		{
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).CurImageDataA = CurImage;
			try
			{
				CustomSave(saveFileDialog.FileName);
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SaveSuccess"), MessageIcons.Ok);
				if (File.Exists(saveFileDialog.FileName))
				{
					ConfigurationHelper.INISetDefaultOpenPath(System.IO.Path.GetDirectoryName(saveFileDialog.FileName), OwnerModule?.ModuleDir);
				}
			}
			catch (OutOfMemoryException)
			{
				LogHelper.objLog.Error("Export Model Err: model too large.");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ExportModelError"), MessageIcons.Error);
				return;
			}
			catch (Exception ex2)
			{
				LogHelper.objLog.Error($"Export Model Err:{ex2.Message}");
				HikMessageBox.Show(AppLocalizationService.GetLang("Savefailed"), MessageIcons.Error);
				return;
			}
		}
		LogHelper.objLog.Info("Export model end.");
	}

	private void ClearCommandExecute()
	{
		LogHelper.objLog.Info("Delete click.");
		if (featurepatMatchDialogData != null)
		{
			if (((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData == null)
			{
				((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData = new ObservableCollection<IROI>();
			}
			CrossIsSet = false;
			DoClear();
		}
		LogHelper.objLog.Info("Delete done.");
	}

	protected void FreshOtherMask()
	{
		if (featurepatMatchDialogData != null)
		{
			if (((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask != null || featurepatMatchDialogData.otherMask2 != null)
			{
				DrawMask();
			}
			else
			{
				ClearMaskData();
			}
		}
	}

	protected void DrawMask()
	{
		if (CurImage != null)
		{
			DrawMask(new System.Drawing.Point(0, 0), CurImage.Width, CurImage.Height);
		}
	}

	internal void AddBindingCollectionEvent()
	{
		CollectionChangedEventManager.AddHandler(((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData, ItemsData_CollectionChanged);
		CollectionChangedEventManager.AddHandler(((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).SpecialRoiList, Cross_CollectionChanged);
	}

	internal void RemoveBindingCollectionEvent()
	{
		CollectionChangedEventManager.RemoveHandler(((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData, ItemsData_CollectionChanged);
		CollectionChangedEventManager.RemoveHandler(((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).SpecialRoiList, Cross_CollectionChanged);
	}

	protected unsafe virtual void DrawMask(System.Drawing.Point point, int sWidth, int sHeight, bool isNormal = true)
	{
		if (imageViewBox == null || imageViewBox.Source == null)
		{
			return;
		}
		bool isUsePen = featurepatMatchDialogData.IsUsePen;
		if ((!isUsePen && ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask == null) || (isUsePen && featurepatMatchDialogData.otherMask2 == null))
		{
			return;
		}
		System.Windows.Point point2 = new System.Windows.Point(((imageViewBox.MapCanvas as Canvas).ActualWidth - (imageViewBox.Viewbox as Viewbox).ActualWidth) / 2.0 + Canvas.GetLeft(imageViewBox.Viewbox as Viewbox) + imageViewBox.ImageOffSetX, ((imageViewBox.MapCanvas as Canvas).ActualHeight - (imageViewBox.Viewbox as Viewbox).ActualHeight) / 2.0 + Canvas.GetTop(imageViewBox.Viewbox as Viewbox) + imageViewBox.ImageOffSetY);
		double onePixelWidth = imageViewBox.OnePixelWidth;
		int num = 0;
		int num2 = 0;
		int num3 = (int)((imageViewBox.MapCanvas as Canvas)?.ActualWidth).Value;
		int num4 = (int)((imageViewBox.MapCanvas as Canvas)?.ActualHeight).Value;
		if (num3 == 0 || num4 == 0)
		{
			return;
		}
		WriteableBitmap writeableBitmap = MaskSourceConvertor.Init(num3, num4, PixelFormats.Bgra32);
		if (writeableBitmap == null)
		{
			return;
		}
		tempImage.Source = writeableBitmap;
		writeableBitmap.Lock();
		byte* ptr = (byte*)writeableBitmap.BackBuffer.ToPointer();
		int num5 = writeableBitmap.BackBufferStride / 4;
		uint* ptr2 = (uint*)ptr;
		if (isNormal)
		{
			for (int i = 0; i < num4; i++)
			{
				uint* ptr3 = ptr2 + i * num5;
				for (int j = 0; j < num3; j++)
				{
					ptr3[j] = 0u;
				}
			}
		}
		if (!isUsePen)
		{
			if (((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask != null)
			{
				fixed (byte* otherMask = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask)
				{
					uint num6 = 0u;
					int num7 = (int)Math.Ceiling(onePixelWidth);
					int width = CurImage.Width;
					int height = CurImage.Height;
					int num8 = 0;
					int num9 = Math.Max((int)((double)point.X * onePixelWidth + point2.X), 0);
					int num10 = Math.Max((int)((double)point.Y * onePixelWidth + point2.Y), 0);
					int num11 = Math.Min((int)((double)(point.X + sWidth) * onePixelWidth + point2.X) + 1, num3);
					int num12 = Math.Min((int)((double)(point.Y + sHeight) * onePixelWidth + point2.Y) + 1, num4);
					for (int k = num10; k < num12; k++)
					{
						for (int l = num9; l < num11; l++)
						{
							num = (int)(((double)l - point2.X) / onePixelWidth);
							if (num < 0 || num >= width)
							{
								continue;
							}
							num2 = (int)(((double)k - point2.Y) / onePixelWidth);
							if (num2 >= 0 && num2 < height)
							{
								num8 = num2 * width + num;
								if (otherMask[num8] != 0)
								{
									num6 = ((otherMask[num8] <= 10) ? 1291786778u : 2583632410u);
									uint* ptr4 = ptr2 + k * num5 + l;
									*ptr4 = num6;
								}
							}
						}
					}
					if (isNormal)
					{
						writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
					}
					else
					{
						try
						{
							writeableBitmap.AddDirtyRect(new Int32Rect(num9, num10, num11 - num9, num12 - num10));
						}
						catch
						{
							writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
						}
					}
				}
			}
		}
		else if (featurepatMatchDialogData.otherMask2 != null)
		{
			fixed (byte* otherMask2 = featurepatMatchDialogData.otherMask2)
			{
				uint num13 = 0u;
				int num14 = (int)Math.Ceiling(onePixelWidth);
				int width2 = CurImage.Width;
				int height2 = CurImage.Height;
				int num15 = 0;
				int num16 = Math.Max((int)((double)point.X * onePixelWidth + point2.X), 0);
				int num17 = Math.Max((int)((double)point.Y * onePixelWidth + point2.Y), 0);
				int num18 = Math.Min((int)((double)(point.X + sWidth) * onePixelWidth + point2.X) + 1, num3);
				int num19 = Math.Min((int)((double)(point.Y + sHeight) * onePixelWidth + point2.Y) + 1, num4);
				for (int m = num17; m < num19; m++)
				{
					for (int n = num16; n < num18; n++)
					{
						num = (int)(((double)n - point2.X) / onePixelWidth);
						if (num < 0 || num >= width2)
						{
							continue;
						}
						num2 = (int)(((double)m - point2.Y) / onePixelWidth);
						if (num2 >= 0 && num2 < height2)
						{
							num15 = num2 * width2 + num;
							if (otherMask2[num15] != 0)
							{
								num13 = ((otherMask2[num15] <= 10) ? 2148494067u : 2953800435u);
								uint* ptr5 = ptr2 + m * num5 + n;
								*ptr5 = num13;
							}
						}
					}
				}
				if (isNormal)
				{
					writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
				}
				else
				{
					try
					{
						writeableBitmap.AddDirtyRect(new Int32Rect(num16, num17, num18 - num16, num19 - num17));
					}
					catch
					{
						writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
					}
				}
			}
		}
		writeableBitmap.Unlock();
	}

	private void BackStepCommandExecute()
	{
		LogHelper.objLog.Info("Back step.");
		if (history.Count > historyIndex && historyIndex > 0)
		{
			historyIndex--;
			string path = history[historyIndex];
			ChangeHistory(path);
		}
		((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("BackEnable");
		((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("ForwardEnable");
		LogHelper.objLog.Info("Back step done.");
	}

	private void ForwardStepCommandExecute()
	{
		LogHelper.objLog.Info("Foreward step.");
		if (history.Count > historyIndex + 1 && historyIndex > -2)
		{
			historyIndex++;
			string path = history[historyIndex];
			ChangeHistory(path);
		}
		((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("BackEnable");
		((FeaturepatMatchDialogUIBase)this).NotifyPropertyChanged("ForwardEnable");
		LogHelper.objLog.Info("Foreward step done.");
	}

	private void Rubber2CommandExecute(RadioButton radioButton)
	{
		if (radioButton.IsChecked.Value)
		{
			rubber = new RubberControl();
			RubberControl rubberControl = rubber;
			rubberControl.OnRubberFinish = (RubberControl.RubberFinish)Delegate.Combine(rubberControl.OnRubberFinish, new RubberControl.RubberFinish(rubber_OnRubberFinish));
			RubberControl rubberControl2 = rubber;
			rubberControl2.OnWipe = (RubberControl.Wipe)Delegate.Combine(rubberControl2.OnWipe, new RubberControl.Wipe(rubber_OnWipe));
			rubber.DataContext = this;
			rubber.SetBinding(RubberControl.DWidthProperty, "PanRectWidth");
			rubber.SetBinding(RubberControl.DHeightProperty, "PanRectWidth");
			featurepatMatchDialogData.DrawROIType = typeof(ToolROIModel);
			featurepatMatchDialogData.IsStateForDrawROI = true;
			curSelectedRadioButton = radioButton;
			rubber.RegisterRoi(imageViewBox);
			tempImage.IsHitTestVisible = true;
			LogHelper.objLog.Info("Eraser checked done.");
		}
		else
		{
			LogHelper.objLog.Info("Eraser unchecked.");
			if (rubber != null)
			{
				rubber.UnRegisterRoi(imageViewBox);
			}
			tempImage.IsHitTestVisible = false;
			featurepatMatchDialogData.IsStateForDrawROI = false;
			LogHelper.objLog.Info("Eraser unchecked done.");
		}
	}

	private void RubberCommandExecute(RadioButton radioButton)
	{
		if (radioButton.IsChecked.Value)
		{
			LogHelper.objLog.Info("Eraser checked.");
			rubber2 = new RubberControl();
			RubberControl rubberControl = rubber2;
			rubberControl.OnRubberFinish = (RubberControl.RubberFinish)Delegate.Combine(rubberControl.OnRubberFinish, new RubberControl.RubberFinish(rubber_OnRubberFinish));
			RubberControl rubberControl2 = rubber2;
			rubberControl2.OnWipe = (RubberControl.Wipe)Delegate.Combine(rubberControl2.OnWipe, new RubberControl.Wipe(rubber2_OnWipe));
			rubber2.DataContext = this;
			rubber2.SetBinding(RubberControl.DWidthProperty, "PanRectWidth2");
			rubber2.SetBinding(RubberControl.DHeightProperty, "PanRectWidth2");
			featurepatMatchDialogData.DrawROIType = typeof(ToolROIModel);
			featurepatMatchDialogData.IsStateForDrawROI = true;
			curSelectedRadioButton = radioButton;
			rubber2.RegisterRoi(imageViewBox);
			tempImage.IsHitTestVisible = true;
			LogHelper.objLog.Info("Eraser checked done.");
		}
		else
		{
			LogHelper.objLog.Info("Eraser unchecked.");
			if (rubber2 != null)
			{
				rubber2.UnRegisterRoi(imageViewBox);
			}
			tempImage.IsHitTestVisible = false;
			featurepatMatchDialogData.IsStateForDrawROI = false;
			LogHelper.objLog.Info("Eraser unchecked done.");
		}
	}

	private void rubber_OnRubberFinish(object sender, object Data)
	{
		RecordHistory();
	}

	public void ThumbMouseLeftButtonDown()
	{
		if (rubber != null)
		{
			rubber.IsDisplayArea = true;
		}
	}

	public void ThumbMouseLeftButtonUp()
	{
		if (rubber != null)
		{
			rubber.IsDisplayArea = false;
		}
	}

	private void rubber_OnWipe(int[] indexs, List<System.Windows.Point> bagPoints)
	{
		LogHelper.objLog.Info("Eraser wipe.");
		if (featurepatMatchDialogData == null)
		{
			return;
		}
		byte[] bytes = BitConverter.GetBytes(1291786778u);
		if (((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask == null)
		{
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask = new byte[CurImage.Width * CurImage.Height];
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (int num5 in indexs)
		{
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask[num5] = 1;
			int num6 = num5 % CurImage.Width;
			int num7 = num5 / CurImage.Width;
			if (num > num6)
			{
				num = num6;
			}
			if (num2 > num7)
			{
				num2 = num7;
			}
			if (num3 < num6)
			{
				num3 = num6;
			}
			if (num4 < num7)
			{
				num4 = num7;
			}
		}
		DrawMask(new System.Drawing.Point(num, num2), num3 - num + 1, num4 - num2 + 1, isNormal: false);
		LogHelper.objLog.Info("Eraser wipe done.");
	}

	private void rubber2_OnWipe(int[] indexs, List<System.Windows.Point> bagPoints)
	{
		LogHelper.objLog.Info("Eraser wipe.");
		if (featurepatMatchDialogData == null)
		{
			return;
		}
		if (featurepatMatchDialogData.otherMask2 == null)
		{
			featurepatMatchDialogData.otherMask2 = new byte[CurImage.Width * CurImage.Height];
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (int num5 in indexs)
		{
			featurepatMatchDialogData.otherMask2[num5] = 1;
			int num6 = num5 % CurImage.Width;
			int num7 = num5 / CurImage.Width;
			if (num > num6)
			{
				num = num6;
			}
			if (num2 > num7)
			{
				num2 = num7;
			}
			if (num3 < num6)
			{
				num3 = num6;
			}
			if (num4 < num7)
			{
				num4 = num7;
			}
		}
		DrawMask(new System.Drawing.Point(num, num2), num3 - num + 1, num4 - num2 + 1, isNormal: false);
		LogHelper.objLog.Info("Eraser wipe done.");
	}

	private void MatchCommandExecute(bool? needpopTip = false)
	{
		LogHelper.objLog.Info("Create model click.");
		if (Arrow != null)
		{
			Arrow.IsChecked = true;
		}
		ReFreshMask(((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask);
		ReFreshMask(featurepatMatchDialogData.otherMask2);
		int num = FeaturePatCreateByEraser(needpopTip);
		featurepatMatchDialogData.IsStateForDrawROI = false;
		if (num == 0)
		{
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).SetCompareSit();
		}
		LogHelper.objLog.Info("Create model done.");
	}

	protected virtual void CustomFeaturePatCreateByEraser(int ret, bool? needpopTip)
	{
		if (ret != 0)
		{
			LogHelper.objLog.Error($"Show points fail, using eraser,errorcode={ret}");
		}
		else if (needpopTip.HasValue && needpopTip.Value)
		{
			HikMessageBox.Show("创建模版成功", MessageIcons.Ok, isTranslate: true);
		}
	}

	public int FeaturePatCreateByEraser(bool? needpopTip = true)
	{
		LogHelper.objLog.Info("Use eraser.");
		int num = 0;
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).PointsData.Clear();
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).NotifyPropertyChanged("EditOutlinePointsEnable");
		num = ShowPoints(featurepatMatchDialogData, needPopTip: true);
		CustomFeaturePatCreateByEraser(num, needpopTip);
		LogHelper.objLog.Info("Use eraser done.");
		return num;
	}

	private void PrepareRawRoi()
	{
	}

	protected virtual void CustomHikFrameChecked()
	{
	}

	private void RadioButtonCommandExecute(RadioButton radioButton)
	{
		if (radioButton.IsChecked.Value)
		{
			LogHelper.objLog.Info($"Radio button {radioButton.Name} is checked.");
			switch (radioButton.Name)
			{
			case "hikFrame":
				CustomHikFrameChecked();
				featurepatMatchDialogData.DrawROIType = typeof(HikCoverRoiParameter);
				break;
			case "hikPolygon":
				featurepatMatchDialogData.DrawROIType = typeof(HikPolygonRoiParameter);
				break;
			case "hikCoverCircle":
				featurepatMatchDialogData.DrawROIType = typeof(HikCoverCircleRoiParameter);
				break;
			case "hikSector":
				featurepatMatchDialogData.DrawROIType = typeof(FeatureMathSectorROIModel);
				break;
			case "hikCross":
				featurepatMatchDialogData.DrawROIType = typeof(CrossROIModel);
				break;
			case "hikEllipse":
				featurepatMatchDialogData.DrawROIType = typeof(HikEllipseRoiParameter);
				break;
			case "hikLine":
				featurepatMatchDialogData.DrawROIType = typeof(HikCoverLineRoiParameter);
				break;
			case "hikMultiLine":
				featurepatMatchDialogData.DrawROIType = typeof(HikMultiLineRoiParameter);
				break;
			default:
				featurepatMatchDialogData.IsStateForDrawROI = false;
				curSelectedRadioButton = null;
				break;
			}
			switch (radioButton.Name)
			{
			case "hikFrame":
			case "hikPolygon":
			case "hikCoverCircle":
			case "hikSector":
			case "hikEllipse":
			case "hikLine":
			case "hikMultiLine":
				featurepatMatchDialogData.SelectedROIList = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData;
				featurepatMatchDialogData.IsStateForDrawROI = true;
				curSelectedRadioButton = radioButton;
				break;
			case "hikCross":
				featurepatMatchDialogData.SelectedROIList = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).SpecialRoiList;
				featurepatMatchDialogData.IsStateForDrawROI = true;
				curSelectedRadioButton = radioButton;
				break;
			default:
				featurepatMatchDialogData.IsStateForDrawROI = false;
				curSelectedRadioButton = null;
				break;
			}
			LogHelper.objLog.Info("Radio button checked done.");
		}
		else
		{
			LogHelper.objLog.Info($"Radio button {radioButton.Name} is unchecked.");
			if (curSelectedRadioButton == null && radioButton != Arrow)
			{
				Arrow.IsChecked = true;
			}
			featurepatMatchDialogData.IsStateForDrawROI = false;
			LogHelper.objLog.Info("Radio button unchecked done.");
		}
	}

	private void SetCrossReady(bool isReady)
	{
		CrossIsReady = isReady;
	}

	private void SetCrossSet()
	{
		CrossIsReady = false;
		CrossIsSet = true;
	}

	private void ConfirmCommandExecute()
	{
		LogHelper.objLog.Info("Ok button click.");
		ReFreshMask(((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask);
		ReFreshMask(featurepatMatchDialogData.otherMask2);
		bool flag = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).CheckChanged();
		bool flag2 = ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData.Count == 0;
		if ((!flag | flag2) || ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ModelData == null)
		{
			int num = ShowPoints(featurepatMatchDialogData, needPopTip: true, needOkPopTip: false, needApply: true);
			if (num != 0)
			{
				LogHelper.objLog.Error($"Show points fail, errorcode={num}");
				return;
			}
		}
		if (cbIsSaveImage.IsChecked != true)
		{
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).CurImageDataA = null;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ModelImageData = null;
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).otherMask = null;
		}
		isClosing = true;
		Owner.DialogResult = true;
		Owner.Close();
		RemoveBindingCollectionEvent();
		LogHelper.objLog.Info("Ok button done.");
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ClearCompareSit();
	}

	protected virtual int CreatePatE(HPFeaturepatMatchDialogData featurepatMatchData, dynamic dynamicObject)
	{
		return dynamicObject.CreateDipolePatE(featurepatMatchData);
	}

	protected virtual void CustomShowPoints(HPFeaturepatMatchDialogData featurepatMatchData, ref int ret)
	{
		if ((((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData == null || ((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData.OfType<IROISign>().Count((IROISign p) => p.Sign != false) == 0) && featurepatMatchData.otherMask2 == null)
		{
			ret = -2;
			throw new Exception(AppLocalizationService.GetLang("Lang_NoROIRegion"));
		}
	}

	public int ShowPoints(HPFeaturepatMatchDialogData featurepatMatchData, bool needPopTip, bool needOkPopTip = true, bool needApply = false)
	{
		int ret = -1;
		try
		{
			if (CurImage == null)
			{
				ret = -2;
				throw new Exception(AppLocalizationService.GetLang("Lang_NoImageErr"));
			}
			CustomShowPoints(featurepatMatchData, ref ret);
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).CurImageDataA = CurImage;
			Type type = Assembly?.GetType("PatCreateCSharp.PatCreateManagerCSharp");
			PropertyInfo property = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			dynamic dynamicObject = property.GetValue(null, null);
			bool waitFlag = false;
			Exception innerEx = null;
			((DispatcherObject)this).Dispatcher.Invoke(() =>
			{
				windowShadow.Visibility = Visibility.Visible;
			});
			Thread thread = new Thread(() =>
			{
				try
				{
					ret = CreatePatE(featurepatMatchData, dynamicObject);
				}
				catch (Exception ex2)
				{
					innerEx = ex2;
				}
				waitFlag = true;
			});
			thread.IsBackground = true;
			thread.Start();
			SyncWaitor.Wait(ref waitFlag);
			((DispatcherObject)this).Dispatcher.Invoke(() =>
			{
				windowShadow.Visibility = Visibility.Collapsed;
			});
			if (innerEx != null)
			{
				ret = -1;
				throw innerEx;
			}
			if (ret != 0)
			{
				if (ret == -3)
				{
					throw new Exception();
				}
				throw new Exception(AppLocalizationService.GetLang("Lang_CreateModelError") + ":" + ErrorCodeInfo.GetErrorInfo(ret));
			}
			FeatureMatchHelper.RefreshModelImageData((FeaturepatMatchDialogDataBase)(object)featurepatMatchData, false);
			RefreshPoints();
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"Creat Model Err:{ex.Message}");
			if (needPopTip)
			{
				string empty = string.Empty;
				HikMessageBox.Show(strMsg: (ret == -3) ? string.Format("{0},{1}", AppLocalizationService.GetLang("模型内存申请失败"), AppLocalizationService.GetLang("Lang_modelLimit")) : ((ret != -1) ? ex.Message : AppLocalizationService.GetLang("Lang_PatCreateInnerError")), owner: Owner, icons: MessageIcons.Error);
			}
		}
		return ret;
	}

	protected void RefreshPoints()
	{
		if (!((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ShapeList.Any((object p) => p is IMultiPoint))
		{
			MultiPoint multiPoint = new MultiPoint();
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ShapeList.Add(multiPoint);
			multiPoint.OutlinePoints = new ObservableCollection<IOutlinePoint>();
			multiPoint.MicroPoints = new ObservableCollection<IMicroPoint>();
			multiPoint.Rectangles = new ObservableCollection<IRectangle>();
		}
		HPFeaturepatMatchDialogData hPFeaturepatMatchDialogData = featurepatMatchDialogData;
		if (((hPFeaturepatMatchDialogData != null) ? ((FeaturepatMatchDialogDataBase)hPFeaturepatMatchDialogData).ShapeList.FirstOrDefault((object p) => p is IMultiPoint) : null) is MultiPoint multiPoint2)
		{
			multiPoint2.MicroPoints.Clear();
			foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in ((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).PointsData)
			{
				MicroPoint microPoint = new MicroPoint();
				microPoint.CenterX = pointsDatum.x;
				microPoint.CenterY = pointsDatum.y;
				microPoint.Color = "#66FF00";
				microPoint.Opacity = 1.0;
				multiPoint2.MicroPoints.Add(microPoint);
			}
		}
		UpdateShapeDisplay((FeaturepatMatchDialogDataBase)(object)featurepatMatchDialogData);
	}

	private void UpdateShapeDisplay(FeaturepatMatchDialogDataBase model, bool isSelfSet = false)
	{
		object obj = model.ShapeList.FirstOrDefault((object p) => p is IMultiPoint);
		model.ShapeList.Clear();
		imageViewBox?.ClearExternShapes();
		if (obj != null)
		{
			model.ShapeList.Add(obj);
		}
	}

	protected void ReFreshMask(byte[] otherMask)
	{
		if (otherMask != null)
		{
			ReFreshMaskData(otherMask);
			DrawMask();
		}
	}

	private void ReFreshMaskData(byte[] otherMask)
	{
		byte[] bytes = BitConverter.GetBytes(1291786778u);
		for (int i = 0; i < otherMask.Length; i++)
		{
			if (otherMask[i] > 10)
			{
				otherMask[i] -= 10;
			}
		}
	}

	private void DeleteCommandExecute(Button button)
	{
		LogHelper.objLog.Info("Clear mask.");
		if (((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData.Contains(button.Tag))
		{
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).ItemsData.Remove(button.Tag as IROI);
		}
		LogHelper.objLog.Info("Clear mask done.");
	}

	private void ZoomOrgBtnCommandExecute()
	{
		LogHelper.objLog.Info("Zoom origin.");
		imageViewBox.Init();
		LogHelper.objLog.Info("Zoom origin done.");
	}

	private void ZoomFullBtnCommandExecute()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected Obj, but got Unknown
		LogHelper.objLog.Info("Zoom full.");
		FullScreen val = new FullScreen();
		viewGrid.Children.Remove(imageViewBox);
		val.SetChild((UIElement)imageViewBox);
		((Window)(object)val).ShowDialog();
		val.RemoveChild();
		viewGrid.Children.Add(imageViewBox);
		LogHelper.objLog.Info("Zoom full done.");
	}

	private void IsUsePenCommandExecute(ToggleButton toggleButton)
	{
		ResetConfig();
		((FeaturepatMatchDialogDataBase)featurepatMatchDialogData).IsSetCrossByManual = false;
	}

	private void ResetConfig()
	{
		if (isClosing)
		{
			return;
		}
		if (isLoading)
		{
			isLoading = false;
			return;
		}
		if (CurImage != null && CurImage.ImageBuffer != null)
		{
			InitData();
			DoClear();
		}
		historyIndex = -1;
		history.Clear();
		RecordHistory();
		Arrow.IsChecked = true;
	}

	private void GirdMouseDownCommandExecute()
	{
		if (CrossIsReady && imageViewBox.PixelX > 0 && imageViewBox.PixelY > 0)
		{
			SetCrossSet();
			UpdateShapeDisplay((FeaturepatMatchDialogDataBase)(object)featurepatMatchDialogData, isSelfSet: true);
			featurepatMatchDialogData.IsStateForDrawROI = false;
			Arrow.IsChecked = true;
		}
	}
}
