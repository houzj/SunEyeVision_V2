using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public partial class HikMessageBox : BaseWindow, INotifyPropertyChanged, IComponentConnector
{
	private readonly MessageButtons _buttons;

	private readonly MessageIcons _icons;

	private string _message;

	private DialogResult _dialogResult;

	private static int _alivecount;

	public string Message
	{
		get
		{
			return _message;
		}
		set
		{
			_message = value;
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs("Message"));
			}
		}
	}

	private new DialogResult DialogResult
	{
		get
		{
			return _dialogResult;
		}
		set
		{
			_dialogResult = value;
			Close();
		}
	}

	public static bool Alive => _alivecount > 0;

	public new event PropertyChangedEventHandler PropertyChanged;

	private static IEnumerable<string> GetCommandLines(int id)
	{
		return new List<string>();
	}

	private static bool IsSdkMode()
	{
		return false;
	}

	public static string MsgFindResoure(string strMsg)
	{
		string text = "";
		object lang = AppLocalizationService.GetLang(strMsg);
		if (lang != null)
		{
			return lang.ToString();
		}
		return strMsg;
	}

	public static DialogResult Show(string strMsg)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg).ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg)
		{
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(string[] strArrMsg)
	{
		return Show(strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next)));
	}

	public static DialogResult Show(string strMsg, MessageButtons messageButtons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, messageButtons).ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, messageButtons)
		{
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(string[] strArrMsg, MessageButtons messageButtons)
	{
		return Show(strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next)), messageButtons);
	}

	public static DialogResult Show(string strMsg, MessageButtons messageButtons, MessageIcons icons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, messageButtons, icons).ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, messageButtons, icons)
		{
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(string[] strArrMsg, MessageButtons messageButtons, MessageIcons icons)
	{
		return Show(strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next)), messageButtons, icons);
	}

	public static DialogResult Show(string strMsg, MessageIcons icons, bool isTranslate = false)
	{
		string strtemp = strMsg;
		if (isTranslate)
		{
			strtemp = MsgFindResoure(strMsg);
		}
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
		{
			if (Application.Current == null)
			{
				MessageBox.Show(strMsg);
				return DialogResult.Ok;
			}
			return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strtemp, icons)
			{
				Owner = ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null))
			}.ShowDialog());
		}
		return new HikMessageBox(strtemp, icons).ShowDialog();
	}

	public static HikMessageBox CreateMessageBox(string strMsg, MessageIcons icons)
	{
		if (IsSdkMode())
		{
			return null;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, icons);
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, icons)
		{
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		});
	}

	public static HikMessageBox CreateMessageBox(string[] strArrMsg, MessageIcons icons)
	{
		return CreateMessageBox(strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next)), icons);
	}

	public static HikMessageBox CreateMessageBox(string strMsg, MessageIcons icons, MessageButtons messageButtons)
	{
		if (IsSdkMode())
		{
			return null;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, icons);
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, messageButtons, icons)
		{
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		});
	}

	public static DialogResult Show(string[] strArrMsg, MessageIcons icons)
	{
		string strtemp = "";
		foreach (string text in strArrMsg)
		{
			strtemp += text;
		}
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strtemp, icons).ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strtemp, icons)
		{
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string title, string strMsg)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg)
			{
				Title = title,
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg)
		{
			Title = title,
			Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string title, string[] strArrMsg)
	{
		string title2 = MsgFindResoure(title);
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(owner, title2, strMsg);
	}

	public static DialogResult Show(Window owner, string title, string strMsg, MessageButtons messageButtons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, messageButtons)
			{
				Title = title,
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, messageButtons)
		{
			Title = title,
			Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string title, string[] strArrMsg, MessageButtons messageButtons)
	{
		string title2 = MsgFindResoure(title);
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(owner, title2, strMsg, messageButtons);
	}

	public static DialogResult Show(Window owner, string title, string strMsg, MessageButtons messageButtons, MessageIcons icons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, messageButtons, icons)
			{
				Title = title,
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, messageButtons, icons)
		{
			Title = title,
			Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string title, string[] strArrMsg, MessageButtons messageButtons, MessageIcons icons)
	{
		string title2 = MsgFindResoure(title);
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(owner, title2, strMsg, messageButtons, icons);
	}

	public static DialogResult Show(Window owner, string title, string strMsg, MessageIcons icons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, icons)
			{
				Title = title,
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, icons)
		{
			Title = title,
			Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string title, string[] strArrMsg, MessageIcons icons)
	{
		string title2 = MsgFindResoure(title);
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(owner, title2, strMsg, icons);
	}

	public static DialogResult Show(string title, string strMsg)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg)
			{
				Title = title
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg)
		{
			Title = title,
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(string title, string[] strArrMsg)
	{
		string title2 = MsgFindResoure(title);
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(title2, strMsg);
	}

	public static DialogResult Show(string title, string strMsg, MessageButtons messageButtons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, messageButtons)
			{
				Title = title
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, messageButtons)
		{
			Title = title,
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(string title, string[] strArrMsg, MessageButtons messageButtons)
	{
		string title2 = MsgFindResoure(title);
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(title2, strMsg, messageButtons);
	}

	public static DialogResult Show(string title, string strMsg, MessageButtons messageButtons, MessageIcons icons, bool isTranslate = false, bool isShowInThread = false)
	{
		string strtemp = strMsg;
		string strtitemp = title;
		if (isTranslate)
		{
			strtemp = MsgFindResoure(strMsg);
			strtitemp = MsgFindResoure(title);
		}
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if ((Application.Current == null) | isShowInThread)
		{
			return new HikMessageBox(strtemp, messageButtons, icons)
			{
				Title = strtitemp
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strtemp, messageButtons, icons)
		{
			Title = strtitemp,
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(string title, string strMsg, string[] buttonsContent, bool isShowInThread = false)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		HikMessageBox messageBox = new HikMessageBox(strMsg, MessageButtons.YesOrNo, MessageIcons.Question)
		{
			Title = title
		};
		if (buttonsContent.Count() == 2)
		{
			messageBox.YesBtn.Content = buttonsContent[0];
			messageBox.NoBtn.Content = buttonsContent[1];
		}
		if ((Application.Current == null) | isShowInThread)
		{
			return messageBox.ShowDialog();
		}
		HikMessageBox hikMessageBox = messageBox;
		Window owner;
		if (Application.Current == null)
		{
			owner = null;
		}
		else if (Application.Current.MainWindow == null)
		{
			owner = null;
		}
		else
		{
			owner = (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null);
		}
		hikMessageBox.Owner = owner;
		return Application.Current.Dispatcher.Invoke(() => messageBox.ShowDialog());
	}

	public static DialogResult Show(string title, string[] messages, MessageButtons messageButtons, MessageIcons icons)
	{
		string strMsg = messages.Aggregate("", (string current, string msg) => current + msg);
		return Show(title, strMsg, messageButtons, icons);
	}

	public static DialogResult Show(string title, string strMsg, MessageIcons icons, bool isTranslate = false)
	{
		string strtemp = strMsg;
		string strtitemp = title;
		if (isTranslate)
		{
			strtemp = MsgFindResoure(strMsg);
			strtitemp = MsgFindResoure(title);
		}
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strtemp, icons)
			{
				Title = strtitemp
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strtemp, icons)
		{
			Title = strtitemp,
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(string title, string[] strArrMsg, MessageIcons icons)
	{
		string strtemp = "";
		foreach (string text in strArrMsg)
		{
			strtemp += text;
		}
		string strtitemp = title;
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strtemp, icons)
			{
				Title = strtitemp
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strtemp, icons)
		{
			Title = strtitemp,
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string strMsg)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg)
			{
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg)
		{
			Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string[] strArrMsg)
	{
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(owner, strMsg);
	}

	public static DialogResult Show(Window owner, string strMsg, MessageButtons messageButtons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, messageButtons)
			{
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, messageButtons)
		{
			Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string[] strArrMsg, MessageButtons messageButtons)
	{
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(owner, strMsg, messageButtons);
	}

	public static DialogResult Show(Window owner, string strMsg, MessageButtons messageButtons, MessageIcons icons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, messageButtons, icons)
			{
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, messageButtons, icons)
		{
			Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string[] strArrMsg, MessageButtons messageButtons, MessageIcons icons)
	{
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(owner, strMsg, messageButtons, icons);
	}

	public static DialogResult Show(Window owner, string strMsg, MessageIcons icons)
	{
		if (IsSdkMode())
		{
			return DialogResult.SDKModel;
		}
		if (Application.Current == null)
		{
			return new HikMessageBox(strMsg, icons)
			{
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new HikMessageBox(strMsg, icons)
		{
			Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
		}.ShowDialog());
	}

	public static DialogResult Show(Window owner, string[] strArrMsg, MessageIcons icons)
	{
		string strMsg = strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next));
		return Show(owner, strMsg, icons);
	}

	public static void Show(string strMsg, MessageIcons icons, MessageButtons messageButtons, uint autoTimes)
	{
		HikMessageBox messageBox = CreateMessageBox(strMsg, icons, messageButtons);
		if (messageBox == null)
		{
			return;
		}
		if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
		{
			if (Application.Current == null)
			{
				MessageBox.Show(strMsg);
				return;
			}
			Application.Current.Dispatcher.Invoke(() => messageBox.ShowDialogDelay(autoTimes));
		}
		else
		{
			messageBox.ShowDialogDelay(autoTimes);
		}
	}

	private HikMessageBox()
	{
		Initialize();
	}

	private void Initialize()
	{
		InitializeComponent();
		MinButtonVisibility = Visibility.Collapsed;
		OkBtn.Click += (object s, RoutedEventArgs e) =>
		{
			DialogResult = DialogResult.Ok;
		};
		YesBtn.Click += (object s, RoutedEventArgs e) =>
		{
			DialogResult = DialogResult.Yes;
		};
		NoBtn.Click += (object s, RoutedEventArgs e) =>
		{
			DialogResult = DialogResult.No;
		};
		CancelBtn.Click += (object s, RoutedEventArgs e) =>
		{
			DialogResult = DialogResult.Cancel;
		};
		switch (_buttons)
		{
		case MessageButtons.Normal:
		{
			OkBtn.Visibility = Visibility.Visible;
			Button yesBtn5 = YesBtn;
			Button noBtn3 = NoBtn;
			Visibility visibility3 = (CancelBtn.Visibility = Visibility.Collapsed);
			Visibility visibility5 = (noBtn3.Visibility = visibility3);
			yesBtn5.Visibility = visibility5;
			break;
		}
		case MessageButtons.YesOrNoOrCancel:
		{
			OkBtn.Visibility = Visibility.Collapsed;
			Button yesBtn4 = YesBtn;
			Button noBtn2 = NoBtn;
			Visibility visibility3 = (CancelBtn.Visibility = Visibility.Visible);
			Visibility visibility5 = (noBtn2.Visibility = visibility3);
			yesBtn4.Visibility = visibility5;
			break;
		}
		case MessageButtons.YesOrNo:
		{
			Button cancelBtn2 = CancelBtn;
			Visibility visibility5 = (OkBtn.Visibility = Visibility.Collapsed);
			cancelBtn2.Visibility = visibility5;
			Button yesBtn3 = YesBtn;
			visibility5 = (NoBtn.Visibility = Visibility.Visible);
			yesBtn3.Visibility = visibility5;
			break;
		}
		case MessageButtons.OkOrCancel:
		{
			Button yesBtn2 = YesBtn;
			Visibility visibility5 = (NoBtn.Visibility = Visibility.Collapsed);
			yesBtn2.Visibility = visibility5;
			Button cancelBtn = CancelBtn;
			visibility5 = (OkBtn.Visibility = Visibility.Visible);
			cancelBtn.Visibility = visibility5;
			break;
		}
		case MessageButtons.NoButton:
		{
			Button okBtn = OkBtn;
			Button yesBtn = YesBtn;
			Button noBtn = NoBtn;
			Visibility visibility = (CancelBtn.Visibility = Visibility.Collapsed);
			Visibility visibility3 = (noBtn.Visibility = visibility);
			Visibility visibility5 = (yesBtn.Visibility = visibility3);
			okBtn.Visibility = visibility5;
			break;
		}
		}
		switch (_icons)
		{
		case MessageIcons.None:
			Icon.Visibility = Visibility.Visible;
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/VMControls/resources/default.png", UriKind.Relative));
			break;
		case MessageIcons.Question:
			Icon.Visibility = Visibility.Visible;
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/VMControls/resources/ask.png", UriKind.Relative));
			break;
		case MessageIcons.Warning:
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/VMControls/resources/waring.png", UriKind.Relative));
			Icon.Visibility = Visibility.Visible;
			GetInfo.Visibility = Visibility.Visible;
			break;
		case MessageIcons.Error:
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/VMControls/resources/error.png", UriKind.Relative));
			Icon.Visibility = Visibility.Visible;
			GetInfo.Visibility = Visibility.Visible;
			break;
		case MessageIcons.Ok:
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/VMControls/resources/ok.png", UriKind.Relative));
			Icon.Visibility = Visibility.Visible;
			break;
		}
		Msg.Visibility = (string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible);
		if (string.IsNullOrEmpty(Message))
		{
			TitleC.Margin = new Thickness(0.0, 20.0, 0.0, 0.0);
		}
		IEnumerable<Button> enumerable = from Button p in Buttons.Children
			where p.Visibility == Visibility.Visible
			select p;
		Button[] array = (enumerable as Button[]) ?? enumerable.ToArray();
		if (array.Length > 2)
		{
			Button[] array2 = array;
			for (int num = 0; num < array2.Length; num++)
			{
				array2[num].Width = 66.0;
			}
		}
	}

	private HikMessageBox(string strMsg)
	{
		Message = strMsg;
		Initialize();
	}

	private HikMessageBox(string strMsg, MessageButtons messageButtons)
	{
		Message = strMsg;
		_buttons = messageButtons;
		Initialize();
	}

	private HikMessageBox(string strMsg, MessageButtons messageButtons, MessageIcons icon)
	{
		Message = strMsg;
		_buttons = messageButtons;
		_icons = icon;
		Initialize();
	}

	private HikMessageBox(string strMsg, MessageIcons icon)
	{
		Message = strMsg;
		_icons = icon;
		Initialize();
	}

	protected override void OnLoaded()
	{
		TitleViewer.Visibility = (string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible);
		if (MsgScroll.ComputedVerticalScrollBarVisibility == Visibility.Visible)
		{
			MsgScroll.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		}
		base.OnLoaded();
	}

	protected override void OnClosed(EventArgs e)
	{
		OkBtn.RemoveEvents();
		YesBtn.RemoveEvents();
		NoBtn.RemoveEvents();
		CancelBtn.RemoveEvents();
		base.OnClosed(e);
		_alivecount--;
		this.RemoveEvents();
	}

	private new DialogResult ShowDialog()
	{
		if (IsSdkMode())
		{
			return DialogResult.None;
		}
		_alivecount++;
		base.ShowDialog();
		return DialogResult;
	}

	private void Close_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void GetInfo_Click(object sender, RoutedEventArgs e)
	{
		StackTrace stackTrace = new StackTrace();
		string path = AppDomain.CurrentDomain.BaseDirectory + "\\log\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "ExternError.txt";
		TextWriter textWriter = null;
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			textWriter = File.CreateText(path);
			string value = stackTrace.ToString();
			textWriter.Write(value);
			textWriter.Flush();
		}
		finally
		{
			textWriter?.Close();
		}
		try
		{
			Process.Start("explorer.exe", Path.GetFullPath(Path.GetDirectoryName(path)));
		}
		catch
		{
		}
	}

}
