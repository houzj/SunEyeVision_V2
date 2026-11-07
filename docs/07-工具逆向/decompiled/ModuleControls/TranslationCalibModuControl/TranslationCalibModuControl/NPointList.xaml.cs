using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using Microsoft.Win32;
using VM.Controls;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace TranslationCalibModuControl;

public partial class NPointList : UserControl, IOwner, IParentNode, IComponentConnector
{
	private object ParentNode = null;

	private ObservableCollection<PointList> pointList;

	private static string RestoreDirectory = string.Empty;

	private const int _formatLength = -10;

	public object Owner { get; set; }

	public ObservableCollection<PointList> PointList
	{
		get
		{
			return pointList;
		}
		set
		{
			pointList = value;
			m_ResultDataGrid.ItemsSource = value;
		}
	}

	object IParentNode.GetParentNode()
	{
		return ParentNode;
	}

	void IParentNode.SetParentNode(object parentNode)
	{
		ParentNode = parentNode;
	}

	public NPointList()
	{
		InitializeComponent();
	}

	private void UserControl_Loaded(object sender, RoutedEventArgs e)
	{
		Init();
		(Owner as Window).Height = 399.0;
	}

	private AlgorithmTabRoot GetRoot()
	{
		if (ParentNode == null || !(ParentNode is NameValueItem))
		{
			return null;
		}
		return (ParentNode as NameValueItem).GetTopBaseNode() as AlgorithmTabRoot;
	}

	private IParamsConfig GetParams()
	{
		AlgorithmTabRoot root = GetRoot();
		if (root != null && root.Owner != null)
		{
			return (root.Owner as IVmModule).Params;
		}
		return null;
	}

	private void Init()
	{
		IParamsConfig paramsConfig = GetParams();
		if (paramsConfig != null)
		{
			string intParamValue = GetIntParamValue("CalibPointTotalNum");
			int num = int.Parse(intParamValue);
			intParamValue = GetIntParamValue("RotPointTotalNum");
			num += int.Parse(intParamValue);
			PointList = new ObservableCollection<PointList>();
			for (int i = 0; i < num; i++)
			{
				string param = "ImagePointX" + i;
				string floatParamValue = GetFloatParamValue(param);
				param = "ImagePointY" + i;
				string floatParamValue2 = GetFloatParamValue(param);
				param = "WorldPointX" + i;
				string floatParamValue3 = GetFloatParamValue(param);
				param = "WorldPointY" + i;
				string floatParamValue4 = GetFloatParamValue(param);
				param = "DeltaAngle" + i;
				string floatParamValue5 = GetFloatParamValue(param);
				PointList.Add(new PointList(paramsConfig)
				{
					ID = i.ToString(),
					ImagePointX = floatParamValue,
					ImagePointY = floatParamValue2,
					WorldPointX = floatParamValue3,
					WorldPointY = floatParamValue4,
					DeltaAngle = floatParamValue5,
					OwnerList = PointList,
					TabRoot = GetRoot()
				});
			}
			m_ResultDataGrid.ItemsSource = PointList;
			DataContext = this;
		}
	}

	private string GetFloatParamValue(string param)
	{
		IParamsConfig paramsConfig = GetParams();
		if (paramsConfig == null)
		{
			return "0";
		}
		try
		{
			string text = ((IUserStringData)paramsConfig)[param];
			return float.Parse(text.ToString()).ToString("f3");
		}
		catch
		{
			return "0";
		}
	}

	private string GetIntParamValue(string param)
	{
		IParamsConfig paramsConfig = GetParams();
		if (paramsConfig == null)
		{
			return "0";
		}
		try
		{
			string text = ((IUserStringData)paramsConfig)[param];
			return text.ToString();
		}
		catch
		{
			return "0";
		}
	}

	private void ButtonApply_Click(object sender, RoutedEventArgs e)
	{
		if (Owner != null)
		{
			(Owner as Window)?.Close();
		}
	}

	private void ButtonExport_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
			saveFileDialog.Filter = "txt|*.txt";
			saveFileDialog.FilterIndex = 1;
			saveFileDialog.RestoreDirectory = true;
			AlgorithmTabRoot root = GetRoot();
			IVmModule vmModule = root.Owner as IVmModule;
			string text = ConfigurationHelper.INIGetDefaultOpenPath(vmModule?.ModuleDir);
			if (text != null)
			{
				saveFileDialog.InitialDirectory = text;
			}
			if (!string.IsNullOrEmpty(RestoreDirectory))
			{
				saveFileDialog.InitialDirectory = RestoreDirectory;
			}
			if (saveFileDialog.ShowDialog() == true)
			{
				RestoreDirectory = saveFileDialog.InitialDirectory;
				try
				{
					File.WriteAllText(saveFileDialog.FileName, GetPointListText());
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SaveSuccess"), MessageIcons.Ok);
				}
				catch (Exception ex)
				{
					LogHelper.Error($"TranslationCalibModu Export Error:{ex.Message}");
					HikMessageBox.Show(AppLocalizationService.GetLang("Savefailed"), MessageIcons.Error);
				}
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(saveFileDialog.FileName), vmModule?.ModuleDir);
			}
		}
		catch (Exception ex2)
		{
			HikMessageBox.Show(ex2.Message, MessageIcons.Error);
		}
	}

	private string GetPointListText()
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < PointList.Count; i++)
		{
			if (!float.TryParse(PointList[i].ImagePointX, out var _) || !float.TryParse(PointList[i].ImagePointY, out var _) || !float.TryParse(PointList[i].WorldPointX, out var _) || !float.TryParse(PointList[i].WorldPointY, out var _) || !float.TryParse(PointList[i].DeltaAngle, out var _))
			{
				throw new Exception("data is not float");
			}
			string value = $"{PointList[i].ImagePointX,-10} {PointList[i].ImagePointY,-10} {PointList[i].WorldPointX,-10} {PointList[i].WorldPointY,-10} {PointList[i].DeltaAngle,-10}\r\n";
			stringBuilder.Append(value);
		}
		return stringBuilder.ToString();
	}

	private void ButtonImport_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
			openFileDialog.Filter = "txt|*.txt";
			openFileDialog.FilterIndex = 1;
			openFileDialog.RestoreDirectory = true;
			AlgorithmTabRoot root = GetRoot();
			IVmModule vmModule = root.Owner as IVmModule;
			string text = ConfigurationHelper.INIGetDefaultOpenPath(vmModule?.ModuleDir);
			if (text != null)
			{
				openFileDialog.InitialDirectory = text;
			}
			if (openFileDialog.ShowDialog() == true)
			{
				RestoreDirectory = openFileDialog.InitialDirectory;
				string pointListTxt = File.ReadAllText(openFileDialog.FileName);
				ObservableCollection<PointList> pointListByText = GetPointListByText(pointListTxt);
				if (pointListByText == null)
				{
					throw new Exception(AppLocalizationService.GetLang("Lang_ImportCalibPointError"));
				}
				PointList = pointListByText;
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadSuccess"), MessageIcons.Ok);
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), vmModule?.ModuleDir);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	private ObservableCollection<PointList> GetPointListByText(string pointListTxt)
	{
		string[] array = pointListTxt.Split(new string[1] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
		int count = PointList.Count;
		ObservableCollection<PointList> observableCollection = new ObservableCollection<PointList>();
		if ((ParentNode as NameValueItem).GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
		{
			IParamsConfig ownerParam = (algorithmTabRoot.Owner as IVmModule).Params;
			for (int i = 0; i < Math.Min(array.Length, count); i++)
			{
				string[] array2 = array[i].Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				string[] array3 = array2;
				foreach (string strNumber in array3)
				{
					if (!Apps.XmlParser.MathHelper.IsNumber(strNumber))
					{
						return null;
					}
				}
				observableCollection.Add(new PointList(ownerParam)
				{
					ID = i.ToString(),
					ImagePointX = array2[0],
					ImagePointY = array2[1],
					WorldPointX = array2[2],
					WorldPointY = array2[3],
					DeltaAngle = array2[4],
					OwnerList = observableCollection,
					TabRoot = GetRoot()
				});
			}
		}
		return observableCollection;
	}

	public void ButtonClear_Click(object sender, RoutedEventArgs e)
	{
		IParamsConfig paramsConfig = GetParams();
		if (paramsConfig != null)
		{
			try
			{
				((IUserStringData)paramsConfig)["Clear"] = "0";
				HikMessageBox.Show("Lang_InfoTips", "Lang_ClearCalibPointSuc", MessageButtons.Normal, MessageIcons.Ok, isTranslate: true);
			}
			catch
			{
				HikMessageBox.Show("Lang_Err", "Lang_ClearCalibPointErr", MessageButtons.Normal, MessageIcons.Error, isTranslate: true);
				return;
			}
			for (int i = 0; i < PointList.Count; i++)
			{
				PointList[i].ImagePointX = "0.000";
				PointList[i].ImagePointY = "0.000";
				PointList[i].WorldPointX = "0.000";
				PointList[i].WorldPointY = "0.000";
				PointList[i].DeltaAngle = "0.000";
			}
		}
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = listParam;
		if (scrollViewer != null)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + num) : Math.Max(0.0, scrollViewer.VerticalOffset - num));
			if (num2 != scrollViewer.VerticalOffset)
			{
				scrollViewer.ScrollToVerticalOffset(num2);
				scrollViewer.Focus();
			}
			e.Handled = true;
		}
	}
}
