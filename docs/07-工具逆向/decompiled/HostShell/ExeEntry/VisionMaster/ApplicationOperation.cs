using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VM.WatchDog;
using VMControls.Interface;

namespace VisionMaster;

public class ApplicationOperation : IApplicationOperation
{
	public void RegisterMainWindow(Window win)
	{
		System.Windows.Application.Current.MainWindow = win;
		App.AddMainWindowClosingHandle();
	}

	public void ReplaceMainWindow(Window newWindow)
	{
		App.RemoveMainWindowClosingHandle();
		System.Windows.Application.Current.MainWindow = newWindow;
		App.AddMainWindowClosingHandle();
	}

	public void UnRegisterMainWindow(Window win)
	{
		App.RemoveMainWindowClosingHandle();
		System.Windows.Application.Current.MainWindow = null;
	}

	public void Dispose()
	{
		Program.MainData.DoClose();
		WatchDogService.Instance.Dispose();
		ServerSDKManager.serverSDKManager.mSolutionManager.ShieldModuleCbResult();
		bool isDispose = false;
		string config = ConfigurationHelper.GetConfig("IsRunBackground");
		bool isRunBackground = config == "1";
		if (!isRunBackground)
		{
			Program.MainData.IsShowProgressMask = true;
			Program.MainData.MaskMessage = "正在关闭方案";
		}
		RunSyncWithEvent(() =>
		{
			LogHelper.Info("SDK destroy handle start.");
			VmSolution.Instance.Dispose(isRunBackground);
			isDispose = true;
			LogHelper.Info("SDK destroy handle end.");
		});
		LogHelper.Info("ComPatibleTool destroy start");
		Process[] processesByName = Process.GetProcessesByName("CompatibleTool");
		Process[] array = processesByName;
		foreach (Process process in array)
		{
			if (!process.HasExited)
			{
				process.Kill();
				process.WaitForExit(10000);
			}
		}
		LogHelper.Info("ComPatibleTool destroy end");
	}

	private void RunSyncWithEvent(Action function)
	{
		bool threadEnd = false;
		object syncObj = new object();
		Task.Run(() =>
		{
			lock (syncObj)
			{
				function();
				threadEnd = true;
			}
		});
		while (true)
		{
			lock (syncObj)
			{
				if (threadEnd)
				{
					break;
				}
				System.Windows.Forms.Application.DoEvents();
				Thread.Sleep(1);
			}
		}
	}
}
