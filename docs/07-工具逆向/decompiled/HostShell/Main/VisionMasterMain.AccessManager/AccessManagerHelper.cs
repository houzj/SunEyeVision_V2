using System.Windows;
using Apps.XmlUI;
using FrontendUI.Design;
using VM.User;
using VMControls.Interface;

namespace VisionMasterMain.AccessManager;

public class AccessManagerHelper
{
	public static bool SetLoginUser(AccessUser loginUser)
	{
		if (CodeManager.GetInstance().OnAccessEnableCallBack == null)
		{
			return false;
		}
		switch (loginUser)
		{
		case AccessUser.Admin:
			UILogManager.logCreate("信息", "管理员登录");
			break;
		case AccessUser.Operator:
			UILogManager.logCreate("信息", "操作员登录");
			break;
		case AccessUser.Tech:
			UILogManager.logCreate("信息", "技术员登录");
			break;
		}
		bool flag = loginUser == CodeManager.GetInstance()._accessCurUserInfo.curLoginUser;
		CodeManager.GetInstance()._accessCurUserInfo.curLoginUser = loginUser;
		CodeManager.GetInstance().IsAdmin = CodeManager.GetInstance().IsAllAccessUser();
		CodeManager.GetInstance().SaveAccessInfo();
		if (loginUser != AccessUser.None && CodeManager.GetInstance().OnAccessEnableCallBack != null)
		{
			CodeManager.GetInstance().OnAccessEnableCallBack(bEnable: true, loginUser);
		}
		if (CodeManager.GetInstance().OnAllAccessCallBack != null)
		{
			CodeManager.GetInstance().OnAllAccessCallBack(CodeManager.GetInstance().IsAdmin);
		}
		if (!flag)
		{
			CodeManager.GetInstance().OnPropertyChanged("LoginUser");
		}
		if (CodeManager.GetInstance().GetLoginUser() != AccessUser.Operator)
		{
			return true;
		}
		if (Application.Current.MainWindow is MainWindow mainWindow)
		{
			Window window = FrontendWindowData.Instance.InitRunWindowFromDesignData();
			if (window != null)
			{
				window.ShowInTaskbar = true;
				window.Show();
				Ioc.Get<IApplicationOperation>()?.ReplaceMainWindow(window);
				if (mainWindow != null)
				{
					mainWindow.DataContext = null;
					mainWindow.Close();
				}
			}
		}
		return true;
	}
}
