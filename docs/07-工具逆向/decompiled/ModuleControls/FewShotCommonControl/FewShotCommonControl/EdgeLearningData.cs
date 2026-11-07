using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.XmlParser.ParamTab;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace FewShotCommonControl;

public class EdgeLearningData : NotifyPropertyChangedBaseNode
{
	public delegate void DeleteImageDelegate(object obj);

	public delegate void DeleteImageTagDelegate(object obj);

	public delegate void SetWindowShadowDelegate(bool bVisible);

	private ImageData _curImageData;

	private ObservableCollection<SampleImageData> _SampleImageDataList = new ObservableCollection<SampleImageData>();

	private List<string> _imageFormats = new List<string> { ".jpg", ".jpeg", ".bmp", ".png", ".tif", ".tiff" };

	private List<UICategoryData> _uiCategoryList = new List<UICategoryData>();

	private static readonly EdgeLearningData instance;

	public const uint MAX_REGISTER_NUM = 10u;

	public const uint MAX_CATEGORY_NUM = 10u;

	public const int MIN_IMAGE_WIDTH = 64;

	public const int MIN_IMAGE_HEIGHT = 64;

	public const int FreshCount = 5;

	public const string LANG_EN = "English";

	public const string MARKED_STRING = "已标为此类";

	public const string UNMARK_STRING = "标记为此类";

	private const int LOCAL_IMAGE_WIDTH = 120;

	private const int LOCAL_IMAGE_HEIGHT = 90;

	public const string DEFAULT_ROI_COLOR = "#0F6AF3";

	public static EdgeLearningData Instance => instance;

	public IVmModule OwnerModule { get; set; }

	public RegisterImageDialog Owner { get; set; }

	public IParamsConfig Param { get; set; }

	public AlgorithmTabRoot ROIRoot { get; set; }

	public IList ROIList { get; set; }

	public ImageData CurImageData
	{
		get
		{
			return _curImageData;
		}
		set
		{
			_curImageData = value;
		}
	}

	public bool IsEnglish { get; set; }

	public ObservableCollection<SampleImageData> SampleImageDataList
	{
		get
		{
			if (_SampleImageDataList == null)
			{
				_SampleImageDataList = new ObservableCollection<SampleImageData>();
			}
			return _SampleImageDataList;
		}
		set
		{
			if (_SampleImageDataList != value)
			{
				_SampleImageDataList = value;
			}
		}
	}

	public List<UICategoryData> UICategoryList
	{
		get
		{
			if (_uiCategoryList == null)
			{
				_uiCategoryList = new List<UICategoryData>();
			}
			return _uiCategoryList;
		}
		set
		{
			if (_uiCategoryList != value)
			{
				_uiCategoryList = value;
			}
		}
	}

	public bool IsExportToModel { get; set; }

	public event DeleteImageDelegate DeleteImageEvent;

	public event DeleteImageTagDelegate DeleteImageTagEvent;

	public event SetWindowShadowDelegate SetWindowShadowEvent;

	static EdgeLearningData()
	{
		instance = new EdgeLearningData();
	}

	private EdgeLearningData()
	{
	}

	public void OnDeleteImageEvent(object obj)
	{
		if (DeleteImageEvent != null)
		{
			DeleteImageEvent(obj);
		}
	}

	public void OnDeleteImageTagEvent(object obj)
	{
		if (DeleteImageTagEvent != null)
		{
			DeleteImageTagEvent(obj);
		}
	}

	public void OnSetWindowShadowEvent(bool bVisible)
	{
		if (SetWindowShadowEvent != null)
		{
			SetWindowShadowEvent(bVisible);
		}
	}

	public bool IsSupportImage(string format)
	{
		foreach (string imageFormat in _imageFormats)
		{
			if (imageFormat.Equals(format, StringComparison.CurrentCultureIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	public unsafe WriteableBitmap GetRGBImage(IntPtr dataPtr, int dataLen)
	{
		if (dataLen > 12)
		{
			byte* ptr = (byte*)dataPtr.ToPointer();
			int num = 0;
			int num2 = *(int*)(ptr + num);
			num += 4;
			int num3 = *(int*)(ptr + num);
			num += 4;
			int num4 = *(int*)(ptr + num);
			num += 4;
			if (num2 > 0 && num3 > 0 && num4 > 0 && dataLen >= 12 + num4)
			{
				if (num4 == num2 * num3)
				{
					WriteableBitmap writeableBitmap = new WriteableBitmap(num2, num3, 96.0, 96.0, PixelFormats.Gray8, null);
					writeableBitmap.WritePixels(new Int32Rect(0, 0, num2, num3), dataPtr + num, num4, writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8);
					return writeableBitmap;
				}
				if (num4 == num2 * num3 * 3)
				{
					WriteableBitmap writeableBitmap2 = new WriteableBitmap(num2, num3, 96.0, 96.0, PixelFormats.Rgb24, null);
					writeableBitmap2.WritePixels(new Int32Rect(0, 0, num2, num3), dataPtr + num, num4, writeableBitmap2.PixelWidth * writeableBitmap2.Format.BitsPerPixel / 8);
					return writeableBitmap2;
				}
			}
		}
		return null;
	}

	public bool CheckCategoryName(string name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			foreach (char c in name)
			{
				if (c == '\\' || c == '/' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
				{
					return false;
				}
			}
		}
		return true;
	}
}
