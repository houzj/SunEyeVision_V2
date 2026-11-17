using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using Apps.Log;
using Microsoft.Win32;

namespace VisionMaster;

public class ServiceManager
{
	public static bool ServiceIsExisted(string serviceName)
	{
		ServiceController[] services = ServiceController.GetServices();
		ServiceController[] array = services;
		foreach (ServiceController serviceController in array)
		{
			if (serviceController.ServiceName == serviceName)
			{
				return true;
			}
		}
		return false;
	}

	public static bool CheckServiceDirectory(string serviceName)
	{
		string text = Directory.GetCurrentDirectory() + "\\Server";
		string name = "SYSTEM\\CurrentControlSet\\Services\\" + serviceName;
		string text2 = Registry.LocalMachine.OpenSubKey(name)?.GetValue("ImagePath").ToString();
		text2 = text2?.Substring(0, text2.LastIndexOf('\\'));
		if (text == text2)
		{
			return true;
		}
		return false;
	}

	public static bool ServiceIsRunning(string serviceName)
	{
		ServiceController serviceController = null;
		try
		{
			serviceController = new ServiceController(serviceName);
			if (serviceController.Status != ServiceControllerStatus.Running && serviceController.Status != ServiceControllerStatus.StartPending)
			{
				return false;
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message);
			return false;
		}
		finally
		{
			serviceController.Close();
		}
		return true;
	}

	public static bool InstallService()
	{
		string currentDirectory = Directory.GetCurrentDirectory();
		currentDirectory += "\\Server\\ServerInstall.bat";
		string workingPath = Directory.GetCurrentDirectory() + "\\Server";
		ExcuteBat(currentDirectory, workingPath);
		return true;
	}

	public static bool StartService(string serviceName)
	{
		bool result = true;
		ServiceController serviceController = null;
		try
		{
			serviceController = new ServiceController(serviceName);
			if (serviceController.Status != ServiceControllerStatus.Running && serviceController.Status != ServiceControllerStatus.StartPending)
			{
				serviceController.Start();
			}
		}
		catch (Exception ex)
		{
			result = false;
			LogHelper.Error(ex.Message);
		}
		finally
		{
			serviceController?.Close();
		}
		return result;
	}

	private static void ExcuteBat(string fileFullPath, string workingPath)
	{
		Process process = null;
		try
		{
			process = new Process();
			process.StartInfo.FileName = fileFullPath;
			process.StartInfo.WorkingDirectory = workingPath;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			process.WaitForExit();
		}
		catch (Exception ex)
		{
			LogHelper.Error("ex.Message :" + ex.StackTrace);
		}
	}
}
