using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Apps.Localization;
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

	private Point c1Point = new Point(0.0, 255.0);

	private int c2LowTo = 0;

	private int c2HighTo = 255;

	private Point c2Point = new Point(0.0, 255.0);

	private int c3LowTo = 0;

	private int c3HighTo = 255;

	private Point c3Point = new Point(0.0, 255.0);

	private bool isChecked = true;

	private bool canDelete;

	private bool isColorInversion;

	private bool isAutoSet = true;

	private const string specialChar = "~!@$%^&*(){}[]\\|/?<>.',，！";

	private string className;

	private bool isUseSelfAreaParam = false;

	private int selfHoleArea = 100;

	private int selfClutterArea = 100;

	public PointCollection dataPoints1 = new PointCollection();

	public PointCollection dataPoints2 = new PointCollection();

	public PointCollection dataPoints3 = new PointCollection();

	public PointCollection chromaticDiagramPoints = new PointCollection();

	public PointCollection lightnessBarPoints = new PointCollection();

	private DelegateCommand<ObjectEventArgs> _DelParamCommand = null;

	private ICommand _StartValueChangeCommand = null;

	private ICommand _SelectionChangedCommand = null;

	private bool _isPreRun = true;

	private Brush brushColor = Brushes.White;

	public Enumeration ColorTypes { get; } = new Enumeration
	{
		DisplayName = "RunParam_Color Type",
		Description = "Color Type",
		AccessMode = "O",
		CurValue = 2,
		DefaultValue = 2,
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
				C1Point = new Point(value, C1HighTo);
				OnPropertyChanged("C1LowTo");
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
				C1Point = new Point(C1LowTo, value);
				OnPropertyChanged("C1HighTo");
				ColorChange();
			}
		}
	}

	public Point C1Point
	{
		get
		{
			return c1Point;
		}
		set
		{
			if (c1Point != value)
			{
				c1Point = value;
				C1LowTo = (int)c1Point.X;
				C1HighTo = (int)c1Point.Y;
				OnPropertyChanged("C1Point");
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
				C2Point = new Point(value, C2HighTo);
				OnPropertyChanged("C2LowTo");
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
				C2Point = new Point(C2LowTo, value);
				OnPropertyChanged("C2HighTo");
				ColorChange();
			}
		}
	}

	public Point C2Point
	{
		get
		{
			return c2Point;
		}
		set
		{
			if (c2Point != value)
			{
				c2Point = value;
				C2LowTo = (int)c2Point.X;
				C2HighTo = (int)c2Point.Y;
				OnPropertyChanged("C2Point");
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
				C3Point = new Point(value, C3HighTo);
				OnPropertyChanged("C3LowTo");
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
				C3Point = new Point(C3LowTo, value);
				OnPropertyChanged("C3HighTo");
				ColorChange();
			}
		}
	}

	public Point C3Point
	{
		get
		{
			return c3Point;
		}
		set
		{
			if (c3Point != value)
			{
				c3Point = value;
				C3LowTo = (int)c3Point.X;
				C3HighTo = (int)c3Point.Y;
				OnPropertyChanged("C3Point");
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
				ColorSegmentList colorSegmentList = GetParentNode<ColorSegmentList>();
				if (colorSegmentList != null && colorSegmentList.SelectedRange != this)
				{
					colorSegmentList.SelectedRange = this;
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

	public bool IsAutoSet
	{
		get
		{
			return isAutoSet;
		}
		set
		{
			if (isAutoSet != value)
			{
				isAutoSet = value;
				OnPropertyChanged("IsAutoSet");
				ValueChange();
			}
		}
	}

	public string ClassName
	{
		get
		{
			if (string.IsNullOrEmpty(className))
			{
				className = "class" + Enumerable.Count((GetParentNode() as ColorSegmentList).ColorRangeCollection);
			}
			return className;
		}
		set
		{
			if (!(className != value))
			{
				return;
			}
			string text = "~!@$%^&*(){}[]\\|/?<>.',，！";
			foreach (char value2 in text)
			{
				if (value.Contains(value2))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ContainsIllegal"), MessageIcons.Error);
					return;
				}
			}
			className = value;
			OnPropertyChanged("ClassName");
			ValueChange();
		}
	}

	public bool IsUseSelfAreaParam
	{
		get
		{
			return isUseSelfAreaParam;
		}
		set
		{
			if (isUseSelfAreaParam != value)
			{
				isUseSelfAreaParam = value;
				OnPropertyChanged("IsUseSelfAreaParam");
				ValueChange();
			}
		}
	}

	public int SelfHoleArea
	{
		get
		{
			return selfHoleArea;
		}
		set
		{
			if (selfHoleArea != value)
			{
				selfHoleArea = value;
				OnPropertyChanged("SelfHoleArea");
			}
		}
	}

	public int SelfClutterArea
	{
		get
		{
			return selfClutterArea;
		}
		set
		{
			if (selfClutterArea != value)
			{
				selfClutterArea = value;
				OnPropertyChanged("SelfClutterArea");
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

	public PointCollection ChromaticDiagramPoints
	{
		get
		{
			return chromaticDiagramPoints;
		}
		set
		{
			if (chromaticDiagramPoints != value)
			{
				chromaticDiagramPoints = value;
				OnPropertyChanged("ChromaticDiagramPoints");
			}
		}
	}

	public PointCollection LightnessBarPoints
	{
		get
		{
			return lightnessBarPoints;
		}
		set
		{
			if (lightnessBarPoints != value)
			{
				lightnessBarPoints = value;
				OnPropertyChanged("LightnessBarPoints");
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

	public ICommand StartValueChangeCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_StartValueChangeCommand == null)
			{
				_StartValueChangeCommand = (ICommand)new DelegateCommand((Action)StartValueChangeCommandExcute);
			}
			return _StartValueChangeCommand;
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
		ColorSegmentList colorSegmentList = GetParentNode<ColorSegmentList>();
		if (colorSegmentList == null)
		{
			return;
		}
		if (colorSegmentList.ColorRangeCollection.Count <= 1)
		{
			HikMessageBox.Show("Params Settings", "Lang_PmuNeedOneRange", MessageIcons.Warning, isTranslate: true);
			return;
		}
		colorSegmentList.ColorRangeCollection.Remove(this);
		if (colorSegmentList.SelectedRange == null)
		{
			colorSegmentList.SelectedRange = colorSegmentList.ColorRangeCollection[0];
		}
		colorSegmentList.BaseSetParamUnit();
	}

	private void StartValueChangeCommandExcute()
	{
		ValueChange();
	}

	private void SelectionChangedCommandExcute(object e)
	{
		if (e != null)
		{
			ColorSegmentList colorSegmentList = GetParentNode<ColorSegmentList>();
			if (colorSegmentList == null || colorSegmentList.SelectedRange == this)
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
				c1Point = new Point(0.0, 255.0);
				OnPropertyChanged("C1Point");
				c2Point = new Point(0.0, 255.0);
				OnPropertyChanged("C2Point");
				c3Point = new Point(0.0, 255.0);
				OnPropertyChanged("C3Point");
				DataPoints1 = new PointCollection();
				DataPoints2 = new PointCollection();
				DataPoints3 = new PointCollection();
				ChromaticDiagramPoints = new PointCollection();
				LightnessBarPoints = new PointCollection();
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
		ColorSegmentList colorSegmentList = GetParentNode<ColorSegmentList>();
		if (colorSegmentList != null)
		{
			int num = colorSegmentList.BaseSetParamUnit();
			if (num >= 0)
			{
				colorSegmentList.PreExecuteRun();
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
		ColorSegmentList colorSegmentList = GetParentNode<ColorSegmentList>();
		if (colorSegmentList != null && colorSegmentList.SelectedRange == this)
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
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(C1HighTo).Append(",");
		stringBuilder.Append(C1LowTo).Append(",");
		stringBuilder.Append(C2HighTo).Append(",");
		stringBuilder.Append(C2LowTo).Append(",");
		stringBuilder.Append(C3HighTo).Append(",");
		stringBuilder.Append(C3LowTo).Append(",");
		stringBuilder.Append(ColorTypes.CurValue).Append(",");
		stringBuilder.Append(IsColorInversion ? "1" : "0").Append(",");
		stringBuilder.Append(IsChecked ? "1" : "0").Append(",");
		stringBuilder.Append(IsUseSelfAreaParam ? "0" : "1").Append(",");
		stringBuilder.Append(SelfHoleArea).Append(",");
		stringBuilder.Append(SelfClutterArea).Append(",");
		stringBuilder.Append(IsAutoSet ? "1" : "0").Append(",");
		stringBuilder.Append(ClassName);
		return stringBuilder.ToString();
	}
}
