using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Apps.UIHelper.Extern;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class RangeInfo : NameValueItem
{
	private const int COLOR_RGB = 1;

	private const int COLOR_HSV = 2;

	private const int COLOR_HSI = 3;

	private int c1LowTo = 0;

	private int c1HighTo = 255;

	private int c2LowTo = 0;

	private int c2HighTo = 255;

	private int c3LowTo = 0;

	private int c3HighTo = 255;

	private bool isChecked = true;

	private bool canDelete;

	private bool isColorInversion;

	public PointCollection dataPoints1 = new PointCollection();

	public PointCollection dataPoints2 = new PointCollection();

	public PointCollection dataPoints3 = new PointCollection();

	private DelegateCommand<ObjectEventArgs> _DelParamCommand = null;

	private ICommand _SelectionChangedCommand = null;

	private bool _isPreRun = true;

	private Brush brushColor = Brushes.White;

	public Enumeration ColorTypes { get; } = new Enumeration
	{
		DisplayName = "RunParam_Color Type",
		Description = "Color Type",
		AccessMode = "O",
		CurValue = 1,
		DefaultValue = 1,
		EnumEntrysA = new ObservableCollection<object>
		{
			new EnumEntry
			{
				Description = "RGB",
				DisplayName = "RGB",
				Value = 1
			},
			new EnumEntry
			{
				Description = "HSV",
				DisplayName = "HSV",
				Value = 2
			},
			new EnumEntry
			{
				Description = "HSI",
				DisplayName = "HSI",
				Value = 3
			}
		}
	};

	public int C1LowTo
	{
		get
		{
			return c1LowTo;
		}
		set
		{
			if (c1LowTo != value)
			{
				c1LowTo = value;
				OnPropertyChanged("C1LowTo");
				ValueChange();
				ColorChange();
			}
		}
	}

	public int C1HighTo
	{
		get
		{
			return c1HighTo;
		}
		set
		{
			if (c1HighTo != value)
			{
				c1HighTo = value;
				OnPropertyChanged("C1HighTo");
				ValueChange();
				ColorChange();
			}
		}
	}

	public int C2LowTo
	{
		get
		{
			return c2LowTo;
		}
		set
		{
			if (c2LowTo != value)
			{
				c2LowTo = value;
				OnPropertyChanged("C2LowTo");
				ValueChange();
				ColorChange();
			}
		}
	}

	public int C2HighTo
	{
		get
		{
			return c2HighTo;
		}
		set
		{
			if (c2HighTo != value)
			{
				c2HighTo = value;
				OnPropertyChanged("C2HighTo");
				ValueChange();
				ColorChange();
			}
		}
	}

	public int C3LowTo
	{
		get
		{
			return c3LowTo;
		}
		set
		{
			if (c3LowTo != value)
			{
				c3LowTo = value;
				OnPropertyChanged("C3LowTo");
				ValueChange();
				ColorChange();
			}
		}
	}

	public int C3HighTo
	{
		get
		{
			return c3HighTo;
		}
		set
		{
			if (c3HighTo != value)
			{
				c3HighTo = value;
				OnPropertyChanged("C3HighTo");
				ValueChange();
				ColorChange();
			}
		}
	}

	public bool IsChecked
	{
		get
		{
			return isChecked;
		}
		set
		{
			if (isChecked != value)
			{
				isChecked = value;
				ColorExtractList colorExtractList = GetParentNode<ColorExtractList>();
				if (colorExtractList != null && colorExtractList.SelectedRange != this)
				{
					colorExtractList.SelectedRange = this;
				}
				OnPropertyChanged("IsChecked");
				ValueChange();
			}
		}
	}

	public bool CanDelete
	{
		get
		{
			return canDelete;
		}
		set
		{
			if (canDelete != value)
			{
				canDelete = value;
				OnPropertyChanged("CanDelete");
			}
		}
	}

	public bool IsColorInversion
	{
		get
		{
			return isColorInversion;
		}
		set
		{
			if (isColorInversion != value)
			{
				isColorInversion = value;
				OnPropertyChanged("IsColorInversion");
				ValueChange();
			}
		}
	}

	public PointCollection DataPoints1
	{
		get
		{
			return dataPoints1;
		}
		set
		{
			if (dataPoints1 != value)
			{
				dataPoints1 = value;
				OnPropertyChanged("DataPoints1");
			}
		}
	}

	public PointCollection DataPoints2
	{
		get
		{
			return dataPoints2;
		}
		set
		{
			if (dataPoints2 != value)
			{
				dataPoints2 = value;
				OnPropertyChanged("DataPoints2");
			}
		}
	}

	public PointCollection DataPoints3
	{
		get
		{
			return dataPoints3;
		}
		set
		{
			if (dataPoints3 != value)
			{
				dataPoints3 = value;
				OnPropertyChanged("DataPoints3");
			}
		}
	}

	public ICommand DelParamCommand
	{
		get
		{
			if (_DelParamCommand == null)
			{
				_DelParamCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)DelParamCommandExcute);
			}
			return (ICommand)_DelParamCommand;
		}
	}

	public ICommand SelectionChangedCommand
	{
		get
		{
			if (_SelectionChangedCommand == null)
			{
				_SelectionChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)SelectionChangedCommandExcute);
			}
			return _SelectionChangedCommand;
		}
	}

	public Brush BrushColor
	{
		get
		{
			return brushColor;
		}
		set
		{
			if (brushColor != value)
			{
				brushColor = value;
				OnPropertyChanged("BrushColor");
			}
		}
	}

	public RangeInfo()
	{
		SetColor();
	}

	private void DelParamCommandExcute(ObjectEventArgs e)
	{
		ColorExtractList colorExtractList = GetParentNode<ColorExtractList>();
		if (colorExtractList == null)
		{
			return;
		}
		if (colorExtractList.ColorRangeCollection.Count <= 1)
		{
			HikMessageBox.Show("Params Settings", "Lang_PmuNeedOneRange", MessageIcons.Warning, isTranslate: true);
			return;
		}
		colorExtractList.ColorRangeCollection.Remove(this);
		if (colorExtractList.SelectedRange == null)
		{
			colorExtractList.SelectedRange = colorExtractList.ColorRangeCollection[0];
		}
		colorExtractList.BaseSetParamUnit();
	}

	private void SelectionChangedCommandExcute(object e)
	{
		if (e != null)
		{
			ColorExtractList colorExtractList = GetParentNode<ColorExtractList>();
			if (colorExtractList == null || colorExtractList.SelectedRange == this)
			{
				c1HighTo = 255;
				OnPropertyChanged("C1HighTo");
				c1LowTo = 0;
				OnPropertyChanged("C1LowTo");
				c2HighTo = 255;
				OnPropertyChanged("C2HighTo");
				c2LowTo = 0;
				OnPropertyChanged("C2LowTo");
				c3HighTo = 255;
				OnPropertyChanged("C3HighTo");
				c3LowTo = 0;
				OnPropertyChanged("C3LowTo");
				DataPoints1 = new PointCollection();
				DataPoints2 = new PointCollection();
				DataPoints3 = new PointCollection();
				ValueChange();
				ColorChange();
			}
		}
	}

	private void ValueChange()
	{
		if (!_isPreRun)
		{
			return;
		}
		ColorExtractList colorExtractList = GetParentNode<ColorExtractList>();
		if (colorExtractList != null)
		{
			int num = colorExtractList.BaseSetParamUnit();
			if (num >= 0)
			{
				colorExtractList.PreExecuteRun();
			}
		}
	}

	internal void SetPreRunStatus(bool isPreRun)
	{
		_isPreRun = isPreRun;
		if (isPreRun)
		{
			ValueChange();
		}
	}

	private void ColorChange()
	{
		ColorExtractList colorExtractList = GetParentNode<ColorExtractList>();
		if (colorExtractList != null && colorExtractList.SelectedRange == this)
		{
			SetColor();
		}
	}

	public void SetColor()
	{
		if (ColorTypes.CurValueA == 1)
		{
			BrushColor = new SolidColorBrush(Color.FromRgb((byte)((C1HighTo + C1LowTo) / 2), (byte)((C2HighTo + C2LowTo) / 2), (byte)((C3HighTo + C3LowTo) / 2)));
		}
		else if (ColorTypes.CurValueA == 2)
		{
			int num = C1HighTo;
			if (C1HighTo < C1LowTo)
			{
				num = C1HighTo + 255;
			}
			BrushColor = HSV2RGB((double)(num + C1LowTo) / 2.0, (double)(C2HighTo + C2LowTo) / 2.0, (double)(C3HighTo + C3LowTo) / 2.0);
		}
		else if (ColorTypes.CurValueA == 3)
		{
			int num2 = C1HighTo;
			if (C1HighTo < C1LowTo)
			{
				num2 = C1HighTo + 255;
			}
			BrushColor = HSI2RGB((double)(num2 + C1LowTo) / 2.0, (double)(C2HighTo + C2LowTo) / 2.0, (double)(C3HighTo + C3LowTo) / 2.0);
		}
	}

	private void hsvMapping(double h, double s, double v, ref int x, ref int y, ref int z)
	{
		double num = h * 1.0 / 255.0 * 360.0;
		double num2 = s * 1.0 / 255.0 * 100.0;
		double num3 = v * 1.0 / 255.0 * 100.0;
		x = (int)num;
		y = (int)num2;
		z = (int)num3;
	}

	public Brush HSV2RGB(double h, double s, double v)
	{
		if (h > 255.0)
		{
			h -= 255.0;
		}
		int x = 0;
		int y = 0;
		int z = 0;
		hsvMapping(h, s, v, ref x, ref y, ref z);
		h = x;
		s = y;
		v = z;
		if (h == 360.0)
		{
			h = 359.0;
		}
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = s * 1.0 / 100.0;
		double num5 = v * 1.0 / 100.0;
		if (s == 0.0)
		{
			return new SolidColorBrush(Color.FromRgb((byte)(num5 * 255.0), (byte)(num5 * 255.0), (byte)(num5 * 255.0)));
		}
		double num6 = h * 1.0 / 60.0;
		int num7 = (int)num6;
		double num8 = num6 - (double)num7;
		double num9 = num5 * (1.0 - num4);
		double num10 = num5 * (1.0 - num8 * num4);
		double num11 = num5 * (1.0 - (1.0 - num8) * num4);
		switch (num7)
		{
		case 0:
			num = num5;
			num2 = num11;
			num3 = num9;
			break;
		case 1:
			num = num10;
			num2 = num5;
			num3 = num9;
			break;
		case 2:
			num = num9;
			num2 = num5;
			num3 = num11;
			break;
		case 3:
			num = num9;
			num2 = num10;
			num3 = num5;
			break;
		case 4:
			num = num11;
			num2 = num9;
			num3 = num5;
			break;
		case 5:
			num = num5;
			num2 = num9;
			num3 = num10;
			break;
		}
		num *= 255.0;
		num2 *= 255.0;
		num3 *= 255.0;
		num = ((num > 255.0) ? 255.0 : num);
		num = ((num < 0.0) ? 0.0 : num);
		num2 = ((num2 > 255.0) ? 255.0 : num2);
		num2 = ((num2 < 0.0) ? 0.0 : num2);
		num3 = ((num3 > 255.0) ? 255.0 : num3);
		num3 = ((num3 < 0.0) ? 0.0 : num3);
		return new SolidColorBrush(Color.FromRgb((byte)num, (byte)num2, (byte)num3));
	}

	public Brush HSI2RGB(double h, double s, double i)
	{
		if (h > 255.0)
		{
			h -= 255.0;
		}
		h /= 255.0;
		s /= 255.0;
		i /= 255.0;
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = h * 2.0 * Math.PI;
		if (num4 >= 0.0 && num4 < Math.PI * 2.0 / 3.0)
		{
			num3 = i * (1.0 - s);
			num = i * (1.0 + s * Math.Cos(num4) / Math.Cos(Math.PI / 3.0 - num4));
			num2 = 3.0 * i - (num + num3);
		}
		else if (num4 >= Math.PI * 2.0 / 3.0 && num4 < 4.1887902047863905)
		{
			num = i * (1.0 - s);
			num2 = i * (1.0 + s * Math.Cos(num4 - Math.PI * 2.0 / 3.0) / Math.Cos(Math.PI - num4));
			num3 = 3.0 * i - (num + num2);
		}
		else
		{
			num2 = i * (1.0 - s);
			num3 = i * (1.0 + s * Math.Cos(num4 - 4.1887902047863905) / Math.Cos(5.235987755982989 - num4));
			num = 3.0 * i - (num2 + num3);
		}
		num = num * 255.0 + 0.5;
		num2 = num2 * 255.0 + 0.5;
		num3 = num3 * 255.0 + 0.5;
		num = ((num > 255.0) ? 255.0 : num);
		num = ((num < 0.0) ? 0.0 : num);
		num2 = ((num2 > 255.0) ? 255.0 : num2);
		num2 = ((num2 < 0.0) ? 0.0 : num2);
		num3 = ((num3 > 255.0) ? 255.0 : num3);
		num3 = ((num3 < 0.0) ? 0.0 : num3);
		return new SolidColorBrush(Color.FromRgb((byte)num, (byte)num2, (byte)num3));
	}

	public override string ToString()
	{
		return C1HighTo + "," + C1LowTo + "," + C2HighTo + "," + C2LowTo + "," + C3HighTo + "," + C3LowTo + "," + ColorTypes.CurValue + "," + (IsColorInversion ? "1" : "0") + "," + (IsChecked ? "1" : "0");
	}
}
