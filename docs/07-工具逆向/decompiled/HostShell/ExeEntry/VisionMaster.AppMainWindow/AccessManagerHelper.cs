using System;
using System.Windows;
using Apps.Data;
using Apps.Log;
using Apps.XmlUI;
using FrontendUI.Design;
using VM.Controls;
using VM.User;
using VM.Utility;
using VMControls.Interface;
using VisionMasterMain;

namespace VisionMaster.AppMainWindow;

public class AccessManagerHelper
{
	public static bool LoadAccessInfo()
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			byte[] array = Convert.FromBase64String(ConfigurationHelper.GetConfig(CodeManager.GetInstance().accessInfoNodeName));
			if (array == null)
			{
				return false;
			}
			object obj = CodeManager.GetInstance().BytesToStruct(array, typeof(AccessUserInfo));
			if (obj == null)
			{
				return false;
			}
			AccessUserInfo val = (AccessUserInfo)obj;
			string diskID = CodeManager.GetDiskID();
			if (val.adminEnable && val.IDInfo != diskID && !val.IDInfo.Contains(diskID) && !diskID.Contains(val.IDInfo) && val.IDInfo != "")
			{
				return false;
			}
			CodeManager.GetInstance()._accessCurUserInfo = val;
		}
		catch (Exception ex)
		{
			LogHelper.Info("读取权限信息失败:" + ex.Message);
			return false;
		}
		SetLoginUser(CodeManager.GetInstance()._accessCurUserInfo.curLoginUser, isSaveAccessInfo: false, isShowFront: false);
		return true;
	}

	public static bool SetLoginUser(AccessUser loginUser, bool isSaveAccessInfo = true, bool isShowFront = true)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Invalid comparison between Unknown and I4
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Expected I4, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Invalid comparison between Unknown and I4
		if (CodeManager.GetInstance().OnAccessEnableCallBack == null)
		{
			AccessManager instance = AccessManager.GetInstance();
			if (CodeManager.GetInstance().OnAccessEnableCallBack == null)
			{
				return false;
			}
		}
		bool flag = loginUser == CodeManager.GetInstance()._accessCurUserInfo.curLoginUser;
		CodeManager.GetInstance()._accessCurUserInfo.curLoginUser = loginUser;
		CodeManager.GetInstance().IsAdmin = CodeManager.GetInstance().IsAllAccessUser();
		if (isSaveAccessInfo)
		{
			CodeManager.GetInstance().SaveAccessInfo();
		}
		if ((int)loginUser > 0 && CodeManager.GetInstance().OnAccessEnableCallBack != null)
		{
			CodeManager.GetInstance().OnAccessEnableCallBack.Invoke(true, loginUser);
		}
		switch (loginUser - 1)
		{
		case 0:
			UILogManager.logCreate("信息", "管理员登录", true, true);
			break;
		case 2:
			UILogManager.logCreate("信息", "操作员登录", true, true);
			break;
		case 1:
			UILogManager.logCreate("信息", "技术员登录", true, true);
			break;
		}
		if (CodeManager.GetInstance().OnAllAccessCallBack != null)
		{
			CodeManager.GetInstance().OnAllAccessCallBack.Invoke(CodeManager.GetInstance().IsAdmin);
		}
		if (!flag)
		{
			((NotifyPropertyChangedBase)(object)CodeManager.GetInstance()).OnPropertyChanged("LoginUser");
		}
		if ((int)CodeManager.GetInstance().GetLoginUser() != 3)
		{
			return true;
		}
		if (isShowFront)
		{
			Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
			{
				Window mainWindow = Application.Current.MainWindow;
				MainWindow val;
				if ((val = (MainWindow)(object)((mainWindow is MainWindow) ? mainWindow : null)) != null)
				{
					Window window = FrontendWindowData.Instance.InitRunWindowFromDesignData();
					if (window != null)
					{
						window.ShowInTaskbar = true;
						window.Show();
						Ioc.Get<IApplicationOperation>()?.ReplaceMainWindow(window);
						if (val != null)
						{
							((FrameworkElement)(object)val).DataContext = null;
							((BaseWindow)(object)val).Close();
						}
					}
				}
			}));
		}
		return true;
	}
}
