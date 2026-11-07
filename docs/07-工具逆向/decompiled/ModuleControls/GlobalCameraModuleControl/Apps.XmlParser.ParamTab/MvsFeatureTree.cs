using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Xml;
using Apps.Log;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class MvsFeatureTree : NameValueItem
{
	public static uint u_moduleID;

	private IntPtr Handle;

	private VM_FT_DEVICEINFO st_deviceinfo = default;

	private string newcameraname = "";

	private string oldcameraname = "";

	private string WindowTitle = "";

	private DelegateCommand<object> _clickCommand = null;

	private VmModule owner;

	public const string path = "C:\\Windows\\Temp\\FeatureTree\\FavoriteFeatures.xml";

	public ICommand ClickCommand
	{
		get
		{
			if (_clickCommand == null)
			{
				_clickCommand = new DelegateCommand<object>((Action<object>)ClickCommandExecute);
			}
			return (ICommand)_clickCommand;
		}
	}

	internal VmModule Owner
	{
		get
		{
			if (owner == null)
			{
				owner = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule;
			}
			return owner;
		}
	}

	[DllImport("user32.dll")]
	public static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

	public MvsFeatureTree()
	{
		MVSDlg.InitCamerXmlEvent();
	}

	private void ClickCommandExecute(object obj)
	{
		try
		{
			UpdateMVSXml();
			MVSDlg.UpdateCamerParam(Owner);
			if (!(MVSDlg.MvsNewDevice == "") && !(MVSDlg.MvsNewDevice == MVSDlg.CameraClosed))
			{
				if (MVSDlg.MvsCurDevice == MVSDlg.MvsNewDevice)
				{
					MVSDlg.MvsStart();
				}
				else
				{
					MVSDlg.MvsInit();
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("MvsFeatureTree ClickCommandExecute exception:" + ex.ToString());
		}
	}

	private void UpdateMVSXml()
	{
		XmlDocument xmlDocument = new XmlDocument();
		if (Owner.UserData["MvsConfig"] != null)
		{
			string text = StringUtil.UTF8GetString(Owner.UserData["MvsConfig"]);
			if (!string.IsNullOrEmpty(text))
			{
				xmlDocument.LoadXml(text);
				xmlDocument.Save("C:\\Windows\\Temp\\FeatureTree\\FavoriteFeatures.xml");
			}
			return;
		}
		try
		{
			if (File.Exists("C:\\Windows\\Temp\\FeatureTree\\FavoriteFeatures.xml"))
			{
				File.Delete("C:\\Windows\\Temp\\FeatureTree\\FavoriteFeatures.xml");
			}
		}
		catch
		{
		}
	}

	public override void OnInniterBusinessFinish()
	{
		base.OnInniterBusinessFinish();
	}
}
