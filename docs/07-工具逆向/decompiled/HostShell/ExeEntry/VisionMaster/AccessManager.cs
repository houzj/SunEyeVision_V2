using System;
using System.Windows;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Apps.XmlUI;
using VM.User;
using VM.Utility;
using VisionMasterMain;

namespace VisionMaster;

public class AccessManager : NotifyPropertyChangedBase
{
	private static object syncObj = new object();

	public static AccessManager instance = null;

	private Visibility _accessButtonVisibility = Visibility.Collapsed;

	private object _accessButtonHeader = string.Empty;

	private DelegateCommand _ChangeLoginUserCommand = null;

	public static AccessManager Instance => GetInstance();

	public Visibility AccessButtonVisibility
	{
		get
		{
			return _accessButtonVisibility;
		}
		set
		{
			if (_accessButtonVisibility != value)
			{
				_accessButtonVisibility = value;
				OnPropertyChanged("AccessButtonVisibility");
			}
		}
	}

	public object AccessButtonHeader
	{
		get
		{
			return _accessButtonHeader;
		}
		set
		{
			if (_accessButtonHeader != value)
			{
				_accessButtonHeader = value;
				OnPropertyChanged("AccessButtonHeader");
			}
		}
	}

	public ICommand ChangeLoginUserCommand => _ChangeLoginUserCommand ?? (_ChangeLoginUserCommand = new DelegateCommand(ChangeLoginUserCommandExecute));

	public AccessManager()
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected Obj, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected Obj, but got Unknown
		if (CodeManager.GetInstance().OnAccessEnableCallBack == null)
		{
			CodeManager val = CodeManager.GetInstance();
			val.OnAccessEnableCallBack = (AccessEnableCallBack)Delegate.Combine((Delegate)(object)val.OnAccessEnableCallBack, (Delegate)new AccessEnableCallBack(SetAccessMenuItemVisible));
		}
	}

	~AccessManager()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected Obj, but got Unknown
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected Obj, but got Unknown
		if (CodeManager.GetInstance().OnAccessEnableCallBack == null)
		{
			CodeManager val = CodeManager.GetInstance();
			val.OnAccessEnableCallBack = (AccessEnableCallBack)Delegate.Remove((Delegate)(object)val.OnAccessEnableCallBack, (Delegate)new AccessEnableCallBack(SetAccessMenuItemVisible));
		}
	}

	public static AccessManager GetInstance()
	{
		if (instance == null)
		{
			lock (syncObj)
			{
				if (instance == null)
				{
					instance = new AccessManager();
				}
			}
		}
		return instance;
	}

	private void ChangeLoginUserCommandExecute()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		LoginWindow val = new LoginWindow();
		UILogManager.logCreate("信息", AppLocalizationService.GetLang("Lang_Role_Switch"), true, true);
		val.SetUserStatus(false);
		((Window)(object)val).ShowDialog();
	}

	public bool SetAccessMenuItemVisible(bool bEnable, AccessUser curLoginUser)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected I4, but got Unknown
		if (bEnable)
		{
			AccessButtonVisibility = Visibility.Visible;
			string infoKey = "未命名";
			switch ((int)curLoginUser)
			{
			case 1:
				infoKey = "Lang_Role_Admin";
				break;
			case 2:
				infoKey = "Lang_Role_Tech";
				break;
			case 3:
				infoKey = "Lang_Role_Operator";
				break;
			}
			AccessButtonHeader = AppLocalizationService.GetLang(infoKey);
		}
		else
		{
			AccessButtonVisibility = Visibility.Collapsed;
		}
		return true;
	}
}
