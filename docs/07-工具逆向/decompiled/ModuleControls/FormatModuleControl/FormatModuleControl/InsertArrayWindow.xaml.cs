using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using VM.Controls;

namespace FormatModuleControl;

public partial class InsertArrayWindow : BaseWindow, IComponentConnector
{
	private ArrayData arrayManager;

	private bool isFinish;

	public ArrayData ArrayData
	{
		get
		{
			return arrayManager;
		}
		set
		{
			if (arrayManager != value)
			{
				arrayManager = value;
				OnPropertyChanged("ArrayManager");
			}
		}
	}

	public bool IsFinish
	{
		get
		{
			return isFinish;
		}
		set
		{
			if (isFinish != value)
			{
				isFinish = value;
				OnPropertyChanged("IsFinish");
			}
		}
	}

	public double ArrayWidth
	{
		get
		{
			double result = 370.0;
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				result = 470.0;
			}
			return result;
		}
	}

	public double BtnSelectWidth
	{
		get
		{
			double result = 276.0;
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				result = 352.0;
			}
			return result;
		}
	}

	public InsertArrayWindow(ArrayData arrayData)
	{
		InitializeComponent();
		if (arrayData == null)
		{
			ArrayData = new ArrayData();
		}
		else
		{
			ArrayData = arrayData.Clone() as ArrayData;
			if (ArrayData != null)
			{
				foreach (object array in ArrayData.ArrayList)
				{
					if (array is ArrayInfo arrayInfo)
					{
						int num = ArrayData.ArrayList.IndexOf(array);
						arrayInfo.UpEnable = num > 0;
						arrayInfo.DownEnable = num < ArrayData.ArrayList.Count - 1;
					}
				}
			}
		}
		DataContext = ArrayData;
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		IsFinish = true;
		Close();
	}

	private void scroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		MouseWheelEventArgs e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
		e2.RoutedEvent = UIElement.MouseWheelEvent;
		e2.Source = sender;
		scroll.RaiseEvent(e2);
	}

	private void InsertArrayWindow_Loaded(object sender, RoutedEventArgs e)
	{
		if (ArrayData?.GetParentNode<Format>()?.GetTopBaseNode() is AlgorithmTabRoot { IsSubscribeInWindow: not false } algorithmTabRoot)
		{
			algorithmTabRoot.IsSubscribeInWindow = false;
			algorithmTabRoot.IsSubscribeInWindow = true;
		}
		Loaded -= InsertArrayWindow_Loaded;
	}
}
