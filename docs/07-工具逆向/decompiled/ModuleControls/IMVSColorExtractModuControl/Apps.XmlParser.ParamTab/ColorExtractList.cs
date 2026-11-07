using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Apps.BaseData;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Mvvm.Commands;
using VM.Controls;
using VMControls.BaseInterface;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class ColorExtractList : ROISelecter, IAddROIWidthImageCommand
{
	private const int MAX_NUMBER = 16;

	private AlgorithmTabRoot _Root;

	private DelegateCommand<ObjectEventArgs> _RadioButtonCheckCommand = null;

	private ObservableCollection<RangeInfo> colorRangeCollection = new ObservableCollection<RangeInfo>();

	private RangeInfo selectedRange;

	private DelegateCommand<object> _AddCommand = null;

	private ICommand _AddROIWidthImageCommand;

	private AlgorithmTabRoot Root
	{
		get
		{
			if (_Root == null)
			{
				_Root = GetParentNode<AlgorithmTabRoot>();
			}
			return _Root;
		}
	}

	public ICommand RadioButtonCheckCommand
	{
		get
		{
			if (_RadioButtonCheckCommand == null)
			{
				_RadioButtonCheckCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)RadioButtonCheckCommandExcute);
			}
			return (ICommand)_RadioButtonCheckCommand;
		}
	}

	public ObservableCollection<RangeInfo> ColorRangeCollection
	{
		get
		{
			return colorRangeCollection;
		}
		set
		{
			if (colorRangeCollection != value)
			{
				colorRangeCollection = value;
				OnPropertyChanged("ColorRangeCollection");
			}
		}
	}

	public RangeInfo SelectedRange
	{
		get
		{
			return selectedRange;
		}
		set
		{
			if (selectedRange != value)
			{
				selectedRange = value;
				OnPropertyChanged("SelectedRange");
			}
		}
	}

	public ICommand AddCommand
	{
		get
		{
			if (_AddCommand == null)
			{
				_AddCommand = new DelegateCommand<object>((Action<object>)AddCommandExcute);
			}
			return (ICommand)_AddCommand;
		}
	}

	public override string Value
	{
		get
		{
			string text = string.Empty;
			for (int i = 0; i < Enumerable.Count(ColorRangeCollection); i++)
			{
				if (i > 0)
				{
					text += "#";
				}
				text += ColorRangeCollection[i].ToString();
			}
			return text;
		}
		set
		{
			Serialize(value);
		}
	}

	ICommand IAddROIWidthImageCommand.AddROIWidthImageCommand
	{
		get
		{
			return AddROIWidthImageCommand;
		}
		set
		{
			throw new NotImplementedException();
		}
	}

	public ICommand AddROIWidthImageCommand => _AddROIWidthImageCommand ?? (_AddROIWidthImageCommand = (ICommand)new DelegateCommand<object>((Action<object>)AddROIWidthImageCommandExecute));

	public ColorExtractList()
	{
		if (ColorRangeCollection.Count == 0)
		{
			RangeInfo rangeInfo = new RangeInfo();
			ColorRangeCollection.Add(rangeInfo);
			rangeInfo.SetParentNode(this);
			OnPropertyChanged("ColorRangeCollection");
		}
		if (SelectedRange == null)
		{
			SelectedRange = ColorRangeCollection[0];
		}
	}

	public override void InniterBusiness()
	{
	}

	private void RadioButtonCheckCommandExcute(ObjectEventArgs e)
	{
		ValueChangedCmdExecute(RoiType.Box);
	}

	private void AddCommandExcute(object obj)
	{
		if (Enumerable.Count(ColorRangeCollection) >= 16)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + 16, MessageButtons.Normal, MessageIcons.Warning);
		}
		else
		{
			RangeInfo rangeInfo = new RangeInfo();
			ColorRangeCollection.Add(rangeInfo);
			rangeInfo.SetParentNode(this);
			SelectedRange = rangeInfo;
			BaseSetParamUnit();
			OnPropertyChanged("ColorRangeCollection");
		}
	}

	private string getRectFromRoi(IROI Roi, int height, int width)
	{
		string result = null;
		if (Roi == null)
		{
			return result;
		}
		if (Roi is IRectROI rectROI && rectROI != null)
		{
			RangeInfo rangeInfo = SelectedRange;
			Point point = new Point(0.0, 0.0);
			point.X = rectROI.CenterPoint.X - rectROI.Width / 2.0;
			point.Y = rectROI.CenterPoint.Y - rectROI.Height / 2.0;
			result = $"{width} {height} {point.X} {point.Y} {rectROI.Height} {rectROI.Width} {rangeInfo.ColorTypes.CurValueA}";
		}
		return result;
	}

	protected override void AddROICommandExecute(object roi)
	{
	}

	protected void AddROIWidthImageCommandExecute(object data)
	{
		UpDate(data as RoiWithTagEventArgs);
		BoxA = false;
		ClearOldROI((data as RoiWithTagEventArgs).Roi);
		IsStateForDrawROI = false;
		BaseSetParamUnit();
	}

	protected override void ClearOldROI(IROI newROI)
	{
		((Collection<IROI>)(object)NormalizeROIList).Clear();
	}

	private void UpDate(RoiWithTagEventArgs data)
	{
		try
		{
			SelectedRange.SetPreRunStatus(isPreRun: false);
			if (!(data.Tag is ImageData imageData))
			{
				HikMessageBox.Show("Lang_ImageDataEmpty", MessageIcons.Error, isTranslate: true);
				return;
			}
			imageData.Buffer = Byte2ImageSource.GetImageBufferByFileName(imageData.MemoryAddress, imageData.Width, imageData.Height, imageData.PixelFormat);
			if (imageData.Buffer == null)
			{
				HikMessageBox.Show("Lang_ImageDataEmpty", MessageIcons.Error, isTranslate: true);
			}
			else if (((IImageData)imageData).PixelFormat == "Gray8")
			{
				HikMessageBox.Show("图像格式不正确或者不支持", MessageIcons.Error, isTranslate: true);
			}
			else
			{
				if (!(data.Roi is IRectROI rectROI))
				{
					return;
				}
				RectROIModel rectROIModel = new RectROIModel();
				rectROIModel.CenterPoint = new Point(rectROI.CenterPoint.X * (double)imageData.Width, rectROI.CenterPoint.Y * (double)imageData.Height);
				rectROIModel.Width = rectROI.Width * (double)imageData.Width;
				rectROIModel.Height = rectROI.Height * (double)imageData.Height;
				string rectFromRoi = getRectFromRoi(rectROIModel, imageData.Height, imageData.Width);
				IParamsConfig paramsConfig = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
				try
				{
					((IUserStringData)paramsConfig)["Rectangle"] = rectFromRoi;
					((IUserStringData)paramsConfig)["Pixformat"] = imageData.PixelFormatString;
				}
				catch (Exception ex)
				{
					LogHelper.Error($"Set Image Rectangle Error:error code={ex.Message}");
					return;
				}
				byte[] array = new byte[imageData.Buffer.Length];
				imageData.Buffer.CopyTo(array, 0);
				try
				{
					((IUserBytesData)paramsConfig)["RectangleImageData"] = array;
				}
				catch (Exception ex2)
				{
					LogHelper.Error($"Set Image Data Error:error code={ex2.Message}");
					return;
				}
				string empty = string.Empty;
				try
				{
					empty = ((IUserStringData)paramsConfig)["GetHistogramData"];
				}
				catch (Exception ex3)
				{
					LogHelper.Error($"Get Histogram Data Error:error code={ex3.Message}");
					return;
				}
				if (empty != string.Empty)
				{
					string[] array2 = empty.Split(',');
					if (array2.Count() == 6)
					{
						if (!int.TryParse(array2[0], out var result))
						{
							return;
						}
						SelectedRange.C1HighTo = result;
						if (!int.TryParse(array2[1], out var result2))
						{
							return;
						}
						SelectedRange.C1LowTo = result2;
						if (!int.TryParse(array2[2], out var result3))
						{
							return;
						}
						SelectedRange.C2HighTo = result3;
						if (!int.TryParse(array2[3], out var result4))
						{
							return;
						}
						SelectedRange.C2LowTo = result4;
						if (!int.TryParse(array2[4], out var result5))
						{
							return;
						}
						SelectedRange.C3HighTo = result5;
						if (!int.TryParse(array2[5], out var result6))
						{
							return;
						}
						SelectedRange.C3LowTo = result6;
					}
				}
				byte[] array3 = new byte[1024];
				try
				{
					array3 = ((IUserBytesData)paramsConfig)["GetHistogram1"];
				}
				catch (Exception ex4)
				{
					LogHelper.Error($"Get Histogram1 Error:error code={ex4.Message}");
					return;
				}
				SelectedRange.DataPoints1.Clear();
				PointCollection pointCollection = new PointCollection();
				for (int i = 0; i < array3.Count(); i += 4)
				{
					pointCollection.Add(new Point(i / 4, BitConverter.ToInt32(array3, i)));
				}
				SelectedRange.DataPoints1 = pointCollection;
				byte[] array4 = new byte[1024];
				try
				{
					array4 = ((IUserBytesData)paramsConfig)["GetHistogram2"];
				}
				catch (Exception ex5)
				{
					LogHelper.Error($"Get Histogram2 Error:error code={ex5.Message}");
					return;
				}
				SelectedRange.DataPoints2.Clear();
				PointCollection pointCollection2 = new PointCollection();
				for (int j = 0; j < array4.Count(); j += 4)
				{
					pointCollection2.Add(new Point(j / 4, BitConverter.ToInt32(array4, j)));
				}
				SelectedRange.DataPoints2 = pointCollection2;
				byte[] array5 = new byte[1024];
				try
				{
					array5 = ((IUserBytesData)paramsConfig)["GetHistogram3"];
				}
				catch (Exception ex6)
				{
					LogHelper.Error($"Get Histogram3 Error:error code={ex6.Message}");
					return;
				}
				SelectedRange.DataPoints3.Clear();
				PointCollection pointCollection3 = new PointCollection();
				for (int k = 0; k < array5.Count(); k += 4)
				{
					pointCollection3.Add(new Point(k / 4, BitConverter.ToInt32(array5, k)));
				}
				SelectedRange.DataPoints3 = pointCollection3;
			}
		}
		finally
		{
			SelectedRange.SetPreRunStatus(isPreRun: true);
		}
	}

	public override int GetParamUnit()
	{
		int result = BaseGetParamUnit();
		for (int i = 0; i < ColorRangeCollection.Count; i++)
		{
			colorRangeCollection[i].SetColor();
		}
		return result;
	}

	private void Serialize(string str)
	{
		if (string.IsNullOrWhiteSpace(str))
		{
			return;
		}
		ColorRangeCollection.Clear();
		SelectedRange = null;
		string[] array = str.Split('#');
		for (int i = 0; i < array.Count(); i++)
		{
			string[] array2 = array[i].Split(',');
			if (array2.Count() != 9)
			{
				continue;
			}
			RangeInfo rangeInfo = new RangeInfo();
			if (!int.TryParse(array2[0], out var result))
			{
				return;
			}
			rangeInfo.C1HighTo = result;
			if (!int.TryParse(array2[1], out var result2))
			{
				return;
			}
			rangeInfo.C1LowTo = result2;
			if (!int.TryParse(array2[2], out var result3))
			{
				return;
			}
			rangeInfo.C2HighTo = result3;
			if (!int.TryParse(array2[3], out var result4))
			{
				return;
			}
			rangeInfo.C2LowTo = result4;
			if (!int.TryParse(array2[4], out var result5))
			{
				return;
			}
			rangeInfo.C3HighTo = result5;
			if (!int.TryParse(array2[5], out var result6))
			{
				return;
			}
			rangeInfo.C3LowTo = result6;
			if (!int.TryParse(array2[6], out var result7) && result7 >= 1 && result7 <= 3)
			{
				return;
			}
			rangeInfo.ColorTypes.CurValueA = result7;
			if (array2[7] == "0")
			{
				rangeInfo.IsColorInversion = false;
			}
			else
			{
				if (!(array2[7] == "1"))
				{
					return;
				}
				rangeInfo.IsColorInversion = true;
			}
			if (array2[8] == "0")
			{
				rangeInfo.IsChecked = false;
			}
			else
			{
				if (!(array2[8] == "1"))
				{
					return;
				}
				rangeInfo.IsChecked = true;
			}
			ColorRangeCollection.Add(rangeInfo);
			rangeInfo.SetParentNode(this);
		}
		if (ColorRangeCollection.Count == 0)
		{
			RangeInfo rangeInfo2 = new RangeInfo();
			ColorRangeCollection.Add(rangeInfo2);
			rangeInfo2.SetParentNode(this);
		}
		if (SelectedRange == null)
		{
			SelectedRange = ColorRangeCollection[0];
		}
	}
}
