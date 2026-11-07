using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using HikExternalCall.Common;
using HikExternalCall.ImageProcess;
using Mvvm.Commands;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSColorRecognitionModuControl;

public class LabelMask : INotifyPropertyChanged
{
	private int IMVS_RECV_TIMEOUT = -536870648;

	private string _maskImagePath = string.Empty;

	private ImageEntity _image;

	private ImageEntity _thbnail;

	private ImageEntity _maskBuffer;

	private MaskData _maskData;

	private ColorData _colorDataHSI;

	public int _selectedIdx = 0;

	private DelegateCommand _DeleteCommand = null;

	public object Owner { get; set; }

	public ICommand DeleteCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand((Action)DeleteCommandCmdExecute);
			}
			return (ICommand)_DeleteCommand;
		}
	}

	public IVmModule Module { get; set; }

	public IParamsConfig Param { get; set; }

	public int MinRoiX { get; set; }

	public int MinRoiY { get; set; }

	public int MaxRoiX { get; set; }

	public int MaxRoiY { get; set; }

	public ImageEntity Image
	{
		get
		{
			if (_image == null)
			{
				_image = new ImageEntity();
			}
			return _image;
		}
		set
		{
			if (_image != value)
			{
				_image = value;
			}
		}
	}

	public ImageEntity Thbnail
	{
		get
		{
			if (_thbnail == null)
			{
				_thbnail = new ImageEntity();
			}
			return _thbnail;
		}
		set
		{
			if (_thbnail != value)
			{
				_thbnail = value;
			}
		}
	}

	public ImageEntity MaskBuffer
	{
		get
		{
			if (_maskBuffer == null)
			{
				_maskBuffer = new ImageEntity();
			}
			return _maskBuffer;
		}
		set
		{
			if (_maskBuffer != value)
			{
				_maskBuffer = value;
			}
		}
	}

	public ColorData ColorDataHSI
	{
		get
		{
			if (_colorDataHSI == null)
			{
				_colorDataHSI = new ColorData();
			}
			return _colorDataHSI;
		}
		set
		{
			if (_colorDataHSI != value)
			{
				_colorDataHSI = value;
			}
		}
	}

	public int SelectedIdx
	{
		get
		{
			return _selectedIdx;
		}
		set
		{
			if (_selectedIdx != value)
			{
				_selectedIdx = value;
				NotifyPropertyChanged("SelectedIdx");
			}
		}
	}

	public string MaskImagePath
	{
		get
		{
			return _maskImagePath;
		}
		set
		{
			if (_maskImagePath != value)
			{
				_maskImagePath = value;
			}
		}
	}

	public MaskData MaskData
	{
		get
		{
			return _maskData;
		}
		set
		{
			if (_maskData != value)
			{
				_maskData = value;
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	private void DeleteCommandCmdExecute()
	{
		if (Owner != null)
		{
			DeleteMaskImg();
			(Owner as LabelType).MaskList.Remove(this);
		}
	}

	public void DeleteMaskImg()
	{
		if (_maskImagePath != string.Empty)
		{
			if (File.Exists(_maskImagePath))
			{
				File.Delete(_maskImagePath);
			}
			_maskImagePath = "";
		}
	}

	public void GenerateMask(ObservableCollection<object> roi)
	{
		if (_maskData == null)
		{
			_maskData = new MaskData();
			_maskData.ItemsData = roi;
			_maskData.ImageHeight = Image.ImageHeight;
			_maskData.ImageWidth = Image.ImageWidth;
			_maskData.MinRoiX = Image.ImageWidth;
			_maskData.MinRoiY = Image.ImageHeight;
			string text = Path.GetDirectoryName(Image.ImagePath) + "\\" + Path.GetFileNameWithoutExtension(Image.ImagePath) + "_mask";
			string extension = Path.GetExtension(Image.ImagePath);
			_maskImagePath = text + extension;
		}
		else
		{
			_maskData.ItemsData = roi;
			_maskData.MaxRoiX = 0;
			_maskData.MaxRoiY = 0;
			_maskData.MinRoiX = Image.ImageWidth;
			_maskData.MinRoiY = Image.ImageHeight;
		}
		_maskData.SaveAlgorithmData(_maskImagePath);
		MinRoiX = _maskData.MinRoiX;
		MinRoiY = _maskData.MinRoiY;
		MaxRoiX = _maskData.MaxRoiX;
		MaxRoiY = _maskData.MaxRoiY;
	}

	public unsafe bool CaculateColor(uint timeOut)
	{
		if (Param == null)
		{
			return false;
		}
		int PixelFormat = 0;
		ImageData imageData = new ImageData();
		ImageProcessManager.GetRgbC3ImageData(Image.ImagePath, ref imageData.Width, ref imageData.Height, ref imageData.Size, ref imageData.ImageBuffer, ref PixelFormat);
		imageData.PixelFormat = PixelFormats.Rgb24;
		ImageData imageData2 = new ImageData();
		ImageProcessManager.GetMono8ImageData(_maskImagePath, ref imageData2.Width, ref imageData2.Height, ref imageData2.ImageBuffer);
		imageData2.PixelFormat = PixelFormats.Gray8;
		imageData2.Size = imageData2.Width * imageData2.Height;
		int num = 0;
		num += 4;
		num += 4;
		num += 4;
		num += 4;
		num += 4;
		num += 4;
		num += 4;
		num += 4;
		num += imageData.Size;
		num += imageData2.Size;
		byte[] array = new byte[num];
		try
		{
			byte* ptr = (byte*)ExternalCallHelper.Bytes2IntPtr(array, out var hObject).ToPointer();
			int num2 = 0;
			*(int*)(ptr + num2) = imageData.Size;
			num2 += 4;
			*(int*)(ptr + num2) = imageData.Width;
			num2 += 4;
			*(int*)(ptr + num2) = imageData.Height;
			num2 += 4;
			*(int*)(ptr + num2) = 201;
			num2 += 4;
			*(int*)(ptr + num2) = imageData2.Size;
			num2 += 4;
			*(int*)(ptr + num2) = imageData2.Width;
			num2 += 4;
			*(int*)(ptr + num2) = imageData2.Height;
			num2 += 4;
			*(int*)(ptr + num2) = 1;
			num2 += 4;
			Marshal.Copy(imageData.ImageBuffer, 0, (IntPtr)(ptr + num2), imageData.Size);
			num2 += imageData.Size;
			Marshal.Copy(imageData2.ImageBuffer, 0, (IntPtr)(ptr + num2), imageData2.Size);
			num2 += imageData2.Size;
			ExternalCallHelper.FreeGCHandle(hObject);
		}
		catch (Exception ex)
		{
			LogHelper.Info("Exception in color CaculateColor" + ex.Message);
		}
		((IUserBytesData)Param)["SetSampleData"] = array;
		byte[] array2 = ((IUserBytesData)Param)["GetColorData"];
		if (array2 != null && array2.Length != 0)
		{
			SelectedIdx = 1;
			ColorDataHSI.GetColorData(array2);
			return true;
		}
		throw new Exception(AppLocalizationService.GetLang("获取直方图数据失败"));
	}

	public void Clear()
	{
		if (File.Exists(_maskImagePath))
		{
			File.Delete(_maskImagePath);
		}
		_maskImagePath = "";
		SelectedIdx = 0;
		if (_maskData != null)
		{
			_maskData.ItemsData.Clear();
			_maskData = null;
		}
		ColorDataHSI.Clear();
	}
}
