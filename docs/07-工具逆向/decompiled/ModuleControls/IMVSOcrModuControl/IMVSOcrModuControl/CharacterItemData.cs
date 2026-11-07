using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Media;
using HikExternalCall.MvbOCR;

namespace IMVSOcrModuControl;

public class CharacterItemData : INotifyPropertyChanged
{
	private string _Character_Tilte;

	private ImageSource _Character_ImageSource;

	private ImageSource _Character_GrayImageSource;

	public ObservableCollection<CharacterItemData> _Collection = new ObservableCollection<CharacterItemData>();

	public CharacterItemData Parent { get; set; }

	public string Character_Tilte
	{
		get
		{
			return _Character_Tilte;
		}
		set
		{
			if (_Character_Tilte != value)
			{
				_Character_Tilte = value;
				NotifyPropertyChanged("Character_Tilte");
			}
		}
	}

	public ImageSource Character_ImageSource
	{
		get
		{
			return _Character_ImageSource;
		}
		set
		{
			if (_Character_ImageSource != value)
			{
				_Character_ImageSource = value;
				NotifyPropertyChanged("Character_ImageSource");
			}
		}
	}

	public ImageSource Character_GrayImageSource
	{
		get
		{
			return _Character_GrayImageSource;
		}
		set
		{
			if (_Character_GrayImageSource != value)
			{
				_Character_GrayImageSource = value;
				NotifyPropertyChanged("Character_GrayImageSource");
			}
		}
	}

	public CharacterImageData CImageData { get; set; }

	public byte ChFontLabel { get; set; }

	public string ChFontLabelChar
	{
		get
		{
			return ((char)ChFontLabel).ToString() ?? "";
		}
		set
		{
			if (!string.IsNullOrEmpty(value))
			{
				ChFontLabel = (byte)value[0];
			}
			NotifyPropertyChanged("ChFontLabelChar");
		}
	}

	public MVBOCR_CHARACTER_FIELD_TYPE FontType { get; set; }

	public IntPtr CharIntPtr { get; set; } = IntPtr.Zero;

	public ObservableCollection<CharacterItemData> Collection
	{
		get
		{
			return _Collection;
		}
		set
		{
			if (_Collection != value)
			{
				_Collection = value;
				NotifyPropertyChanged("Collection");
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

	public CharacterItemData()
	{
		CollectionChangedEventManager.AddHandler(Collection, CollectionChanged);
	}

	private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		NotifyPropertyChanged("Collection.Count");
	}
}
