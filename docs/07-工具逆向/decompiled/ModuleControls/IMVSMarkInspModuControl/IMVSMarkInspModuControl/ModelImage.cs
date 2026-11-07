using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMVSMarkInspModuControl;

public class ModelImage : INotifyPropertyChanged
{
	private string _name;

	private WriteableBitmap _source;

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			_name = value;
			OnPropertyChanged("Name");
		}
	}

	public WriteableBitmap Source
	{
		get
		{
			return _source;
		}
		set
		{
			_source = value;
			OnPropertyChanged("Source");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public static ObservableCollection<ModelImage> GetModuleImages(IntPtr dataPtr, int dataLen)
	{
		ObservableCollection<ModelImage> observableCollection = new ObservableCollection<ModelImage>();
		int num = 0;
		int num2 = 0;
		while (num2 < dataLen)
		{
			WriteableBitmap mono8Image = GetMono8Image(dataPtr + num2, out var readLen);
			if (readLen == 0)
			{
				break;
			}
			num2 += readLen;
			observableCollection.Add(new ModelImage
			{
				Source = mono8Image,
				Name = $"{num}"
			});
			num++;
		}
		return observableCollection;
	}

	public unsafe static WriteableBitmap GetMono8Image(IntPtr dataPtr, out int readLen)
	{
		readLen = 0;
		byte* ptr = (byte*)dataPtr.ToPointer();
		int num = 0;
		int num2 = *(int*)(ptr + num);
		num += 4;
		int num3 = *(int*)(ptr + num);
		num += 4;
		if (num2 > 0 && num3 > 0)
		{
			WriteableBitmap writeableBitmap = new WriteableBitmap(num2, num3, 96.0, 96.0, PixelFormats.Gray8, null);
			writeableBitmap.WritePixels(new Int32Rect(0, 0, num2, num3), dataPtr + num, num2 * num3, writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8);
			readLen = 8 + num2 * num3;
			return writeableBitmap;
		}
		return null;
	}
}
