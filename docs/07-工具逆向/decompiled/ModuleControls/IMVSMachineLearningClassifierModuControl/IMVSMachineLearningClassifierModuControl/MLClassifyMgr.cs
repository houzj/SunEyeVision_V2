using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSMachineLearningClassifierModuControl;

internal class MLClassifyMgr
{
	public delegate void DeleteImageDelegate(object obj);

	public delegate void SetWindowShadowDelegate(bool bVisible);

	private ImageData _curImageData;

	private ObservableCollection<SampleImageData> _SampleImageDataList = new ObservableCollection<SampleImageData>();

	private List<string> _imageFormats = new List<string> { ".jpg", ".jpeg", ".bmp", ".png" };

	private Dictionary<string, string> _EmptyCategoryList = new Dictionary<string, string>();

	private static readonly MLClassifyMgr instance;

	public const uint MAX_REGISTER_NUM = 100u;

	public const uint MAX_CATEGORY_NUM = 200u;

	public const int MIN_IMAGE_WIDTH = 256;

	public const int MIN_IMAGE_HEIGHT = 256;

	public const int FreshCount = 5;

	public const string LANG_EN = "English";

	public static MLClassifyMgr Instance => instance;

	public IVmModule Module { get; set; }

	public IParamsConfig Param { get; set; }

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

	public MLImageDialog Owner { get; set; }

	public bool IsEnglish { get; set; }

	public ObservableCollection<SampleImageData> SampleImageDataList
	{
		get
		{
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

	public Dictionary<string, string> EmptyCategoryList
	{
		get
		{
			return _EmptyCategoryList;
		}
		set
		{
			if (_EmptyCategoryList != value)
			{
				_EmptyCategoryList = value;
			}
		}
	}

	public event DeleteImageDelegate DeleteImageEvent;

	public event SetWindowShadowDelegate SetWindowShadowEvent;

	static MLClassifyMgr()
	{
		instance = new MLClassifyMgr();
	}

	private MLClassifyMgr()
	{
	}

	public void OnDeleteImageEvent(object obj)
	{
		if (DeleteImageEvent != null)
		{
			DeleteImageEvent(obj);
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
