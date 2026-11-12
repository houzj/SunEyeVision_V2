using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
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
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.View.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

public partial class NativeMessageBox : BaseWindow, INotifyPropertyChanged, IComponentConnector
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

	/// <summary>
	/// 测试宿主自动应答钩子：设置后模态弹窗不再 ShowDialog 阻塞，直接返回钩子给出的应答。
	/// 生产进程不设置该钩子，行为不变。参数为（标题，消息），返回应答；返回 null 表示不接管。
	/// </summary>
	internal static Func<string?, string?, DialogResult?>? TestAutoRespond;

	private static DialogResult? TryAutoRespond(string? title, string? message)
	{
		var respond = TestAutoRespond;
		return respond is null ? null : respond(title, message);
	}

    internal static void ShowOwnedWarning(Window owner,string message,Action<NativeMessageBox> created,Func<bool> current,string? title=null)
    {
        owner.Dispatcher.VerifyAccess();
        var window=new NativeMessageBox(message,MessageIcons.Warning);
        if(title is not null)window.Title=title;
        try{window.Owner=owner;window.WindowStartupLocation=WindowStartupLocation.CenterOwner;created(window);if(current())window.ShowDialog();}
        finally{window.Close();}
    }

    public static DialogResult ShowOwned(Window owner, string title, string message, MessageButtons buttons, MessageIcons icon)
    {
        owner.Dispatcher.VerifyAccess();
        var window = new NativeMessageBox(message, buttons, icon)
        {
            Owner = owner,
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        return window.ShowDialog();
    }

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
			return new NativeMessageBox(strMsg).ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg)
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
			return new NativeMessageBox(strMsg, messageButtons).ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, messageButtons)
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
			return new NativeMessageBox(strMsg, messageButtons, icons).ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, messageButtons, icons)
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
			return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strtemp, icons)
			{
				Owner = ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null))
			}.ShowDialog());
		}
		return new NativeMessageBox(strtemp, icons).ShowDialog();
	}

	public static NativeMessageBox CreateMessageBox(string strMsg, MessageIcons icons)
	{
		if (IsSdkMode())
		{
			return null;
		}
		if (Application.Current == null)
		{
			return new NativeMessageBox(strMsg, icons);
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, icons)
		{
			Owner = ((Application.Current == null) ? null : ((Application.Current.MainWindow == null) ? null : (Application.Current.MainWindow.IsActive ? Application.Current.MainWindow : null)))
		});
	}

	public static NativeMessageBox CreateMessageBox(string[] strArrMsg, MessageIcons icons)
	{
		return CreateMessageBox(strArrMsg.Aggregate((string first, string next) => MsgFindResoure(first) + MsgFindResoure(next)), icons);
	}

	public static NativeMessageBox CreateMessageBox(string strMsg, MessageIcons icons, MessageButtons messageButtons)
	{
		if (IsSdkMode())
		{
			return null;
		}
		if (Application.Current == null)
		{
			return new NativeMessageBox(strMsg, icons);
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, messageButtons, icons)
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
			return new NativeMessageBox(strtemp, icons).ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strtemp, icons)
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
			return new NativeMessageBox(strMsg)
			{
				Title = title,
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg)
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
			return new NativeMessageBox(strMsg, messageButtons)
			{
				Title = title,
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, messageButtons)
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
			return new NativeMessageBox(strMsg, messageButtons, icons)
			{
				Title = title,
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, messageButtons, icons)
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
			return new NativeMessageBox(strMsg, icons)
			{
				Title = title,
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, icons)
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
			return new NativeMessageBox(strMsg)
			{
				Title = title
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg)
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
			return new NativeMessageBox(strMsg, messageButtons)
			{
				Title = title
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, messageButtons)
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
			return new NativeMessageBox(strtemp, messageButtons, icons)
			{
				Title = strtitemp
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strtemp, messageButtons, icons)
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
		NativeMessageBox messageBox = new NativeMessageBox(strMsg, MessageButtons.YesOrNo, MessageIcons.Question)
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
		NativeMessageBox hikMessageBox = messageBox;
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
			return new NativeMessageBox(strtemp, icons)
			{
				Title = strtitemp
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strtemp, icons)
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
			return new NativeMessageBox(strtemp, icons)
			{
				Title = strtitemp
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strtemp, icons)
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
			return new NativeMessageBox(strMsg)
			{
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg)
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
			return new NativeMessageBox(strMsg, messageButtons)
			{
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, messageButtons)
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
			return new NativeMessageBox(strMsg, messageButtons, icons)
			{
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, messageButtons, icons)
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
			return new NativeMessageBox(strMsg, icons)
			{
				Owner = ((owner == null) ? null : (owner.IsActive ? owner : null))
			}.ShowDialog();
		}
		return Application.Current.Dispatcher.Invoke(() => new NativeMessageBox(strMsg, icons)
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
		NativeMessageBox messageBox = CreateMessageBox(strMsg, icons, messageButtons);
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

	private NativeMessageBox()
	{
		Initialize();
	}

	private void OnOkClick(object sender, RoutedEventArgs args) => DialogResult = DialogResult.Ok;
	private void OnYesClick(object sender, RoutedEventArgs args) => DialogResult = DialogResult.Yes;
	private void OnNoClick(object sender, RoutedEventArgs args) => DialogResult = DialogResult.No;
	private void OnCancelClick(object sender, RoutedEventArgs args) => DialogResult = DialogResult.Cancel;

	private void Initialize()
	{
		InitializeComponent();
        SetResourceReference(StyleProperty,typeof(BaseWindow));
		MinButtonVisibility = Visibility.Collapsed;
		OkBtn.Click += OnOkClick;
		YesBtn.Click += OnYesClick;
		NoBtn.Click += OnNoClick;
		CancelBtn.Click += OnCancelClick;
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
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/PresentationControls/resources/default.png", UriKind.Relative));
			break;
		case MessageIcons.Question:
			Icon.Visibility = Visibility.Visible;
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/PresentationControls/resources/ask.png", UriKind.Relative));
			break;
		case MessageIcons.Warning:
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/PresentationControls/resources/waring.png", UriKind.Relative));
			Icon.Visibility = Visibility.Visible;
			GetInfo.Visibility = Visibility.Visible;
			break;
		case MessageIcons.Error:
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/PresentationControls/resources/error.png", UriKind.Relative));
			Icon.Visibility = Visibility.Visible;
			GetInfo.Visibility = Visibility.Visible;
			break;
		case MessageIcons.Ok:
			Icon.Source = new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Display/PresentationControls/resources/ok.png", UriKind.Relative));
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

	private NativeMessageBox(string strMsg)
	{
		Message = strMsg;
		Initialize();
	}

	private NativeMessageBox(string strMsg, MessageButtons messageButtons)
	{
		Message = strMsg;
		_buttons = messageButtons;
		Initialize();
	}

	private NativeMessageBox(string strMsg, MessageButtons messageButtons, MessageIcons icon)
	{
		Message = strMsg;
		_buttons = messageButtons;
		_icons = icon;
		Initialize();
	}

	private NativeMessageBox(string strMsg, MessageIcons icon)
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
		OkBtn.Click -= OnOkClick;
		YesBtn.Click -= OnYesClick;
		NoBtn.Click -= OnNoClick;
		CancelBtn.Click -= OnCancelClick;
		base.OnClosed(e);
	}

	private new DialogResult ShowDialog()
	{
		if (TryAutoRespond(Title, Message) is { } answered)
		{
			return answered;
		}
		if (IsSdkMode())
		{
			return DialogResult.None;
		}
		_alivecount++;
		try{base.ShowDialog();return DialogResult;}
		finally{_alivecount--;}
	}

	private void Close_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

    internal string WriteDiagnostic()
	{
		StackTrace stackTrace = new StackTrace();
		string path = AppDomain.CurrentDomain.BaseDirectory + "\\log\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "ExternError.txt";
		TextWriter textWriter = null;
		try
		{
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
        return path;
    }

    private void GetInfo_Click(object sender,RoutedEventArgs e)
    {
        var path=WriteDiagnostic();
		try
		{
			Process.Start("explorer.exe", Path.GetFullPath(Path.GetDirectoryName(path)));
		}
		catch
		{
		}
	}

}
