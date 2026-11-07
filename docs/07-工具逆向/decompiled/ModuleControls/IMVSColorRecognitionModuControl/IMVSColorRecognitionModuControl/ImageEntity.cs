using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace IMVSColorRecognitionModuControl;

public class ImageEntity : INotifyPropertyChanged
{
	private BitmapImage _ImageSource;

	public byte[] Buffer;

	private bool isFirstGetCali = true;

	private bool _IsCalibrated;

	private bool isFirstGetOKCali = true;

	private bool _IsOKCalibrated;

	private bool _IsVisible = true;

	public byte[] ImageBuffer;

	public BitmapImage ImageSource
	{
		get
		{
			return _ImageSource;
		}
		set
		{
			if (_ImageSource != value)
			{
				_ImageSource = value;
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs("ImageSource"));
				}
			}
		}
	}

	public object Owner { get; set; }

	public bool IsCalibrated
	{
		get
		{
			return _IsCalibrated;
		}
		set
		{
			if (_IsCalibrated != value)
			{
				_IsCalibrated = value;
				NotifyPropertyChanged("IsCalibrated");
			}
		}
	}

	public bool IsOKCalibrated
	{
		get
		{
			return _IsOKCalibrated;
		}
		set
		{
			if (_IsOKCalibrated != value)
			{
				_IsOKCalibrated = value;
				NotifyPropertyChanged("IsOKCalibrated");
			}
		}
	}

	public bool IsVisible
	{
		get
		{
			return _IsVisible;
		}
		set
		{
			_IsVisible = value;
			NotifyPropertyChanged("IsVisible");
		}
	}

	public int ImageIndex { get; set; }

	public string ImagePath { get; set; }

	public string ImageName
	{
		get
		{
			return Path.GetFileName(ImagePath);
		}
		set
		{
		}
	}

	public int ImageWidth { get; set; }

	public int ImageHeight { get; set; }

	public int ImageSize { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public bool GetCalibrated()
	{
		return false;
	}

	private bool GetOKCalibrated()
	{
		return false;
	}

	private static bool ThumbnailCallback()
	{
		return false;
	}

	public static void SaveBitmapImageIntoFile(BitmapImage bitmapImage, string filePath)
	{
		BitmapEncoder bitmapEncoder = new PngBitmapEncoder();
		bitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapImage));
		using FileStream stream = new FileStream(filePath, FileMode.Create);
		bitmapEncoder.Save(stream);
	}
}
