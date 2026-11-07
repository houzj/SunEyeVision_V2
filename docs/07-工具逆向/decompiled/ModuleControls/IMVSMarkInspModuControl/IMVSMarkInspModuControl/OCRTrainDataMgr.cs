using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Apps.Data;
using VM.ModuleControl;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSMarkInspModuControl;

internal class OCRTrainDataMgr
{
	public delegate void DeleteImageDelegate(object obj);

	public delegate void ClearModelDelegate();

	public delegate void EmptyModelDelegate();

	public delegate void ClearAllModelDelegate();

	public delegate void SetWindowShadowDelegate(bool bVisible);

	public delegate void ShowProgressWindowDelegate();

	public delegate void CloseProgressWindowDelegate();

	private static readonly OCRTrainDataMgr instance;

	public const uint IMVS_OK = 0u;

	public const byte NO_DATA = 0;

	public const byte HAVE_DATA = byte.MaxValue;

	public const int DISPLAY_IMAGE_COUNT = 5;

	public const int MAX_NUM = 100;

	public const string OTHER_IMAGE = "1";

	public const string CURRENT_IMAGE = "0";

	public const string NULL_IMAGE = "2";

	public const int IMAGE_ADDRESS_OFFSET = 8;

	private ImageData _curImageData;

	public MVBI_BOX_F[] _boxes;

	public bool IsUpImageLimit;

	public static OCRTrainDataMgr Instance => instance;

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

	public IParamsConfig Param { get; set; }

	public IVmModule Module { get; set; }

	public OCRModelTrainDialog Owner { get; set; }

	public bool IsStartTrain { get; set; }

	public bool TrainTargetInit { get; set; }

	public ModuleTrainData TrainData { get; set; }

	public bool IsEnglish { get; set; }

	public event DeleteImageDelegate DeleteImageEvent;

	public event ClearModelDelegate ClearModelEvent;

	public event EmptyModelDelegate EmptyModelEvent;

	public event ClearAllModelDelegate ClearAllModelEvent;

	public event SetWindowShadowDelegate SetWindowShadowEvent;

	public event ShowProgressWindowDelegate ShowProgressWindowEvent;

	public event CloseProgressWindowDelegate CloseProgressWindowEvent;

	static OCRTrainDataMgr()
	{
		instance = new OCRTrainDataMgr();
	}

	private OCRTrainDataMgr()
	{
	}

	public void OnDeleteImageEvent(object obj)
	{
		if (DeleteImageEvent != null)
		{
			DeleteImageEvent(obj);
		}
	}

	public void OnClearModelEvent()
	{
		if (ClearModelEvent != null)
		{
			ClearModelEvent();
		}
	}

	public void OnEmptyModelEvent()
	{
		if (EmptyModelEvent != null)
		{
			EmptyModelEvent();
		}
	}

	public void OnClearAllModelEvent()
	{
		if (ClearAllModelEvent != null)
		{
			ClearAllModelEvent();
		}
	}

	public void OnSetWindowShadowEvent(bool bVisible)
	{
		if (SetWindowShadowEvent != null)
		{
			SetWindowShadowEvent(bVisible);
		}
	}

	public void OnShowProgressWindowEvent()
	{
		if (ShowProgressWindowEvent != null)
		{
			ShowProgressWindowEvent();
		}
	}

	public void OnCloseProgressWindowEvent()
	{
		if (CloseProgressWindowEvent != null)
		{
			CloseProgressWindowEvent();
		}
	}

	public void ClearTempData()
	{
		CurImageData = null;
		Module = null;
		Param = null;
		Owner = null;
		TrainData = null;
	}

	public unsafe ImageData GetRoughImage()
	{
		ImageData imageData = null;
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)Param)["GetRoughMatchImage"];
			if (array != null && array.Length != 0)
			{
				IntPtr intPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
				byte* ptr = (byte*)intPtr.ToPointer();
				int num = 0;
				int num2 = *(int*)(ptr + num);
				num += 4;
				int num3 = *(int*)(ptr + num);
				num += 4;
				int num4 = num2 * num3;
				if (num4 > 0)
				{
					imageData = new ImageData();
					imageData.ImageBuffer = new byte[num4];
					Marshal.Copy(intPtr + num, imageData.ImageBuffer, 0, num4);
					imageData.Height = num3;
					imageData.Width = num2;
					imageData.PixelFormat = PixelFormats.Gray8;
				}
			}
		}
		catch
		{
		}
		return imageData;
	}

	public unsafe ImageData GetCurImage()
	{
		ImageData imageData = null;
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)Param)["GetInputImage"];
			if (array != null && array.Length != 0)
			{
				IntPtr intPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
				byte* ptr = (byte*)intPtr.ToPointer();
				int num = 0;
				int num2 = *(int*)(ptr + num);
				num += 4;
				int num3 = *(int*)(ptr + num);
				num += 4;
				int num4 = num2 * num3;
				if (num4 > 0)
				{
					imageData = new ImageData();
					imageData.ImageBuffer = new byte[num4];
					Marshal.Copy(intPtr + num, imageData.ImageBuffer, 0, num4);
					imageData.Height = num3;
					imageData.Width = num2;
					imageData.PixelFormat = PixelFormats.Gray8;
				}
			}
		}
		catch
		{
		}
		return imageData;
	}

	public void SynDataProcess(SynDataOccasion occasion, ImageData imageData = null)
	{
		if (Param == null)
		{
			return;
		}
		try
		{
			switch (occasion)
			{
			case SynDataOccasion.SetCurrentImage:
				((IUserStringData)Param)["SetImageSource"] = "0";
				break;
			case SynDataOccasion.SetOtherImage:
				((IUserStringData)Param)["SetImageSource"] = "1";
				SetInputImage(imageData);
				break;
			case SynDataOccasion.SetNullImage:
				((IUserStringData)Param)["SetImageSource"] = "2";
				break;
			}
		}
		catch
		{
		}
	}

	private bool SetInputImage(ImageData imageData)
	{
		try
		{
			if (imageData.ImageBuffer != null && imageData.ImageBuffer.Length != 0)
			{
				int len = 0;
				IntPtr imagePtr = GetImagePtr(imageData, out len);
				if (imagePtr == IntPtr.Zero)
				{
					return false;
				}
				try
				{
					byte[] array = new byte[len];
					Marshal.Copy(imagePtr, array, 0, len);
					((IUserBytesData)Param)["SetInputImage"] = array;
					Marshal.FreeHGlobal(imagePtr);
					return true;
				}
				catch
				{
					Marshal.FreeHGlobal(imagePtr);
					return false;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private unsafe IntPtr GetImagePtr(ImageData imageData, out int len)
	{
		len = 0;
		if (imageData != null && imageData.Width > 0 && imageData.Height > 0)
		{
			int num = 8;
			len = num + imageData.Width * imageData.Height;
			IntPtr intPtr = Marshal.AllocHGlobal(len);
			void* ptr = intPtr.ToPointer();
			*(int*)ptr = imageData.Width;
			((int*)ptr)[1] = imageData.Height;
			int length = imageData.Width * imageData.Height;
			if (imageData.ImageBuffer != null)
			{
				Marshal.Copy(imageData.ImageBuffer, 0, intPtr + num, length);
				return intPtr;
			}
			return IntPtr.Zero;
		}
		return IntPtr.Zero;
	}
}
