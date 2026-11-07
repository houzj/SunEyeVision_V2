using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Apps.Data;
using Apps.Data.ShapeDefine;
using Apps.ErrorCode;
using Apps.Localization;
using HikPcUI.ImageView;
using VM.ModuleControl;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSMarkInspModuVAControl;

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

	public const int MAX_DETECT_ROI_NUM = 16;

	private ImageData _curImageData;

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

	public ModuleTrainData TrainData { get; set; }

	public bool IsEnglish { get; set; }

	public bool IsNoRecord { get; set; }

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

	public void DrawRects(HikImageView imageView, MVBI_BOX_F[] boxes)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		if (imageView != null && boxes != null && boxes.Length != 0)
		{
			foreach (MVBI_BOX_F val in boxes)
			{
				string tips = string.Format(AppLocalizationService.GetLang("Center:({0},{1}) Width:{2} Height:{3} Angle:{4}"), val.center_x, val.center_y, val.width, val.height, val.angle);
				imageView.CreatRectangleE(val.center_x, val.center_y, val.width, val.height, val.angle, "#FF00d53c", tips, 1.0);
			}
		}
	}

	public List<object> GetRects(MVBI_BOX_F[] boxes)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		if (boxes == null || boxes.Length == 0)
		{
			return null;
		}
		List<object> list = new List<object>();
		foreach (MVBI_BOX_F val in boxes)
		{
			string tips = string.Format(AppLocalizationService.GetLang("Center:({0},{1}) Width:{2} Height:{3} Angle:{4}"), val.center_x, val.center_y, val.width, val.height, val.angle);
			list.Add(new Rect
			{
				CenterX = val.center_x,
				CenterY = val.center_y,
				Width = val.width,
				Height = val.height,
				Angle = val.angle,
				Color = "#FF00d53c",
				Opacity = 1.0,
				Tips = tips,
				StrokeThickness = 1.0
			});
		}
		return list;
	}

	public void SetDatumImage(ImageData imageData, IParamsConfig param = null)
	{
		if (imageData.ImageBuffer != null && imageData.ImageBuffer.Length != 0)
		{
			int len = 0;
			IntPtr imagePtr = GetImagePtr(imageData, out len);
			if (imagePtr == IntPtr.Zero)
			{
				string text = AppLocalizationService.GetLang("Lang_SetDatumImageDataError") + "\r\n(";
				text = text + AppLocalizationService.GetLang("errInfo:") + AppLocalizationService.GetLang("Lang_GetImageDataError");
				text += ")";
				throw new Exception(text);
			}
			try
			{
				byte[] array = new byte[len];
				Marshal.Copy(imagePtr, array, 0, len);
				if (param != null)
				{
					((IUserBytesData)param)["SetInputImage"] = array;
				}
				else
				{
					((IUserBytesData)Param)["SetInputImage"] = array;
				}
				Marshal.FreeHGlobal(imagePtr);
				return;
			}
			catch (Exception ex)
			{
				Marshal.FreeHGlobal(imagePtr);
				int result = 0;
				string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				string text3 = AppLocalizationService.GetLang("Lang_SetDatumImageDataError") + "\r\n(";
				text3 += AppLocalizationService.GetLang("errInfo:");
				text3 += text2;
				text3 += ")";
				throw new Exception(text3);
			}
		}
		string text4 = AppLocalizationService.GetLang("Lang_SetDatumImageDataError") + "\r\n(";
		text4 = text4 + AppLocalizationService.GetLang("errInfo:") + AppLocalizationService.GetLang("Lang_ImageDataEmpty");
		text4 += ")";
		throw new Exception(text4);
	}

	public unsafe IntPtr GetImagePtr(ImageData imageData, out int len)
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
