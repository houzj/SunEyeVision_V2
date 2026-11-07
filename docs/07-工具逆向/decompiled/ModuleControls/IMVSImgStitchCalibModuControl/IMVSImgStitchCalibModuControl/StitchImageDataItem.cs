using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Xml;
using Apps.Data;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using HikExternalCall.MvbOCR;

namespace IMVSImgStitchCalibModuControl;

public class StitchImageDataItem : BaseNode, INotifyPropertyChanged
{
	public ImageData ImageData;

	public ImageData ActualImageData;

	public bool IsReady;

	public bool IsSelected;

	private Enumeration SubpixelWindow;

	public bool FilteringEnabled = true;

	private Integer GrayscaleContrast;

	private Integer SubpixelWindowSize;

	private Integer OffsetX;

	private Integer OffsetY;

	public string strSubpixelWindow;

	public string strGrayscaleContrast;

	public string strSubpixelWindowSize;

	public string strOffsetX;

	public string strOffsetY;

	public byte[] CornersImg;

	public byte[] CornersObj;

	public string CornersNum;

	public ImageData ImageDataA
	{
		get
		{
			return ImageData;
		}
		set
		{
			if (ImageData != value)
			{
				ImageData = value;
				OnPropertyChanged("ImageDataA");
				OnPropertyChanged("IsNull");
			}
		}
	}

	public ImageData ActualImageDataA
	{
		get
		{
			return ActualImageData;
		}
		set
		{
			if (ActualImageData != value)
			{
				ActualImageData = value;
				OnPropertyChanged("ActualImageDataA");
			}
		}
	}

	public bool IsReadyA
	{
		get
		{
			return IsReady;
		}
		set
		{
			if (IsReady != value)
			{
				IsReady = value;
				OnPropertyChanged("IsReadyA");
			}
		}
	}

	public bool IsSelectedA
	{
		get
		{
			return IsSelected;
		}
		set
		{
			if (IsSelected != value)
			{
				IsSelected = value;
				OnPropertyChanged("IsSelectedA");
			}
		}
	}

	public bool IsNull => ImageDataA == null;

	public bool IsActualImageNull => ActualImageDataA == null;

	public Enumeration SubpixelWindowA
	{
		get
		{
			if (SubpixelWindow == null)
			{
				SubpixelWindow = new Enumeration
				{
					DisplayName = "RunParam_Sub-pixel window",
					Description = "RunParam_Sub-pixel window",
					AccessMode = "O",
					CurValue = 1,
					DefaultValue = 1
				};
				SubpixelWindow.EnumEntrysA = new ObservableCollection<object>
				{
					new EnumEntry
					{
						Description = "自适应",
						DisplayName = "自适应",
						Value = 1
					},
					new EnumEntry
					{
						Description = "设置值",
						DisplayName = "设置值",
						Value = 2
					}
				};
			}
			return SubpixelWindow;
		}
	}

	public bool FilteringEnabledA
	{
		get
		{
			return FilteringEnabled;
		}
		set
		{
			if (FilteringEnabled != value)
			{
				FilteringEnabled = value;
				OnPropertyChanged("FilteringEnabledA");
			}
		}
	}

	public Integer GrayscaleContrastA
	{
		get
		{
			if (GrayscaleContrast == null)
			{
				GrayscaleContrast = new Integer
				{
					DisplayName = "RunParam_Grayscale Contrast",
					Description = "RunParam_Grayscale Contrast",
					AccessMode = "O",
					CurValue = 15,
					DefaultValue = 15,
					IncValue = 1,
					MinValue = 1,
					MaxValue = 255,
					SubscribeVisible = false
				};
			}
			return GrayscaleContrast;
		}
	}

	public Integer SubpixelWindowSizeA
	{
		get
		{
			if (SubpixelWindowSize == null)
			{
				SubpixelWindowSize = new Integer
				{
					DisplayName = "设置窗口大小",
					Description = "亚像素窗口大小设置值",
					AccessMode = "O",
					CurValue = 7,
					DefaultValue = 7,
					IncValue = 1,
					MinValue = 3,
					MaxValue = 100,
					SubscribeVisible = false
				};
			}
			return SubpixelWindowSize;
		}
	}

	public Integer OffsetXA
	{
		get
		{
			if (OffsetX == null)
			{
				OffsetX = new Integer
				{
					DisplayName = "RunParam_Offset X",
					Description = "RunParam_Offset X",
					AccessMode = "O",
					CurValue = 0,
					DefaultValue = 0,
					IncValue = 1,
					MinValue = -500,
					MaxValue = 500,
					SubscribeVisible = false
				};
			}
			return OffsetX;
		}
	}

	public Integer OffsetYA
	{
		get
		{
			if (OffsetY == null)
			{
				OffsetY = new Integer
				{
					DisplayName = "RunParam_Offset Y",
					Description = "RunParam_Offset Y",
					AccessMode = "O",
					CurValue = 0,
					DefaultValue = 0,
					IncValue = 1,
					MinValue = -500,
					MaxValue = 500,
					SubscribeVisible = false
				};
			}
			return OffsetY;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public HKA_IMAGE GetImageDataStruct()
	{
		HKA_IMAGE result = new HKA_IMAGE
		{
			format = ((ImageDataA.PixelFormat == PixelFormats.Gray8) ? HKA_IMAGE_FORMAT.HKA_IMG_MONO_08 : HKA_IMAGE_FORMAT.HKA_IMG_RGB_RGB24_C3),
			width = ImageDataA.Width,
			height = ImageDataA.Height,
			step = new int[4]
		};
		result.step[0] = ImageDataA.Width;
		return result;
	}

	public MVBSTITCH_CALIBBOARD_CFG GetParamConfigStruct()
	{
		return new MVBSTITCH_CALIBBOARD_CFG
		{
			nGrayscaleContrast = GrayscaleContrastA.CurValueA,
			nSubpixelWinAuto = SubpixelWindowA.CurValueA,
			nSubpixelWinSize = SubpixelWindowSizeA.CurValueA,
			nMedianFilteringState = (FilteringEnabledA ? 1 : 2)
		};
	}

	protected override void BeforSaveBaseNode()
	{
		strGrayscaleContrast = GrayscaleContrastA.CurValueA.ToString();
		strSubpixelWindow = SubpixelWindowA.CurValueA.ToString();
		strSubpixelWindowSize = SubpixelWindowSizeA.CurValueA.ToString();
		strOffsetX = OffsetXA.CurValueA.ToString();
		strOffsetY = OffsetYA.CurValueA.ToString();
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		int result = 0;
		if (int.TryParse(strGrayscaleContrast, out result))
		{
			GrayscaleContrastA.CurValueA = result;
		}
		if (int.TryParse(strSubpixelWindow, out result))
		{
			SubpixelWindowA.CurValueA = result;
		}
		if (int.TryParse(strSubpixelWindowSize, out result))
		{
			SubpixelWindowSizeA.CurValueA = result;
		}
		if (int.TryParse(strOffsetX, out result))
		{
			OffsetXA.CurValueA = result;
		}
		if (int.TryParse(strOffsetY, out result))
		{
			OffsetYA.CurValueA = result;
		}
		return this;
	}
}
