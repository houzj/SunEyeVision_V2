using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Apps.Json;
using Apps.Localization;
using Apps.Log;
using GlobalScriptUI;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_PyShell : NameValueItem, IBottomCommand
{
	public string ShellCodeFilePath;

	public string Textinshell;

	private string CompileResult;

	private string ShellFilePath = "";

	private readonly string tmpShellFileDir = AppDomain.CurrentDomain.BaseDirectory + "Module\\";

	private readonly string tmpShellFileDirS = AppDomain.CurrentDomain.BaseDirectory + "Module(sp)\\x64\\Logic";

	private string[] strCompileError = new string[4] { "LanguageCompileCreateObjectError", "LanguageCompileMissFunctionError", "LanguageCompileExecuteError", "LanguageCompileExeception" };

	private ShellModuleDynamicIO ShellInVarContainer;

	private ShellModuleDynamicIO ShellOutVarContainer;

	public bool IsPasswordEnabled;

	public bool IsShowPassword;

	public string Password;

	public string ScriptRefrence;

	public string ShellCodeFilePathA
	{
		get
		{
			return ShellCodeFilePath;
		}
		set
		{
			ShellCodeFilePath = value;
			OnPropertyChanged("ShellCodeFilePathA");
		}
	}

	public string TextinshellA
	{
		get
		{
			return Textinshell;
		}
		set
		{
			Textinshell = value;
			OnPropertyChanged("TextinshellA");
		}
	}

	public string CompileResultA
	{
		get
		{
			return CompileResult;
		}
		set
		{
			CompileResult = value;
			OnPropertyChanged("CompileResultA");
		}
	}

	public override string Value
	{
		get
		{
			return ShellFilePath;
		}
		set
		{
			TextinshellA = value;
		}
	}

	public ShellModuleDynamicIO ShellInVarContainerA
	{
		get
		{
			if (ShellInVarContainer == null)
			{
				ShellInVarContainer = new ShellModuleDynamicIO
				{
					IsOutput = false,
					DataSource = GetVmModule()
				};
				ShellInVarContainer.SetParentNode(this);
			}
			return ShellInVarContainer;
		}
		set
		{
			ShellInVarContainer = value;
		}
	}

	public ShellModuleDynamicIO ShellOutVarContainerA
	{
		get
		{
			if (ShellOutVarContainer == null)
			{
				ShellOutVarContainer = new ShellModuleDynamicIO
				{
					IsOutput = true,
					DataSource = GetVmModule()
				};
				ShellOutVarContainer.SetParentNode(this);
			}
			return ShellOutVarContainer;
		}
		set
		{
			ShellOutVarContainer = value;
		}
	}

	public bool IsPasswordEnabledA
	{
		get
		{
			return IsPasswordEnabled;
		}
		set
		{
			IsPasswordEnabled = value;
			OnPropertyChanged("IsPasswordEnabledA");
		}
	}

	public bool IsShowPasswordA
	{
		get
		{
			return IsShowPassword;
		}
		set
		{
			IsShowPassword = value;
			OnPropertyChanged("IsShowPasswordA");
		}
	}

	public string PasswordA
	{
		get
		{
			return Password;
		}
		set
		{
			Password = value;
			OnPropertyChanged("PasswordA");
		}
	}

	public string ScriptRefrenceA
	{
		get
		{
			return ScriptRefrence;
		}
		set
		{
			if (!(ScriptRefrence != value))
			{
				return;
			}
			ScriptRefrence = value;
			OnPropertyChanged("ScriptRefrenceA");
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			string tempParam = "";
			try
			{
				JsonConvert.DeserializeObject<List<ShellRefrences>>(ScriptRefrence)?.ForEach((ShellRefrences x) =>
				{
					//IL_0024: Unknown result type (might be due to invalid IL or missing references)
					//IL_002a: Expected I4, but got Unknown
					tempParam = tempParam + x.Name + "\n" + (int)x.refrencesType + "\r";
				});
			}
			catch (Exception ex)
			{
				LogHelper.Error("程序集字符反序列化失败," + ex.ToString());
			}
			if (string.IsNullOrEmpty(tempParam))
			{
				return;
			}
			try
			{
				vmModule.Params["ScriptRefrence"] = tempParam;
			}
			catch (VmException ex2)
			{
				LogHelper.Error("设置脚本模块程序集失败," + ex2.errorMessage);
			}
		}
	}

	public override int GetParamUnit()
	{
		int result = 0;
		if ("ShellContent" == Name)
		{
			try
			{
				VmModule vmModule = GetVmModule();
				if (vmModule != null)
				{
					string textinshell = Textinshell;
					byte[] bt = ((IUserBytesData)vmModule.Params)["ShellContent"];
					Value = StringUtil.UTF8GetString(bt);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error("get ShellContent is error:" + ex.ToString());
			}
		}
		return result;
	}

	private void ParamSetCommandExecute()
	{
		string text = tmpShellFileDirS;
		if (!(GetTopBaseNode() is AlgorithmTabRoot))
		{
			return;
		}
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				string textinshell = Textinshell;
				((IUserBytesData)vmModule.Params)["ShellContent"] = Encoding.UTF8.GetBytes(textinshell);
			}
		}
		catch (Exception ex)
		{
			string message = AppLocalizationService.GetLang("脚本") + AppLocalizationService.GetLang("模块") + AppLocalizationService.GetLang("参数设置") + " : " + ex.Message;
			LogHelper.Error(message);
		}
	}

	private int SetIOParams()
	{
		VmModule vmModule = GetVmModule();
		if (vmModule == null)
		{
			return -1;
		}
		VmIOCollection inputs = vmModule.Inputs;
		VmIOCollection outputs = vmModule.Outputs;
		string text = "";
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			if (inputs != null && inputs.Count > 0)
			{
				stringBuilder.Clear();
				foreach (VmIO item in inputs)
				{
					VmIO inoutIo;
					if ((inoutIo = item) == null)
					{
						continue;
					}
					stringBuilder.Append(inoutIo.Name);
					stringBuilder.Append(":");
					text = inoutIo.TypeName;
					if (inoutIo.TypeName == "int" || inoutIo.TypeName == "float" || inoutIo.TypeName == "string")
					{
						ShellDynamicIO shellDynamicIO = ShellInVarContainerA.ItemsA.OfType<ShellDynamicIO>().FirstOrDefault((ShellDynamicIO p) => p.DisplayNameA == AppLocalizationService.GetLang(inoutIo.Name));
						if (shellDynamicIO != null)
						{
							text = shellDynamicIO.DisplayValueTypeA;
						}
					}
					stringBuilder.Append(text);
					stringBuilder.Append(":");
					List<VmIO> allLeafIO = inoutIo.GetAllLeafIO();
					string value = string.Join("\r", allLeafIO.Select((VmIO p) => p.Name));
					stringBuilder.Append(value);
					stringBuilder.Append("\n");
				}
				if (stringBuilder.Length > 0)
				{
					((IUserBytesData)vmModule.Params)["AddInputParam"] = Encoding.UTF8.GetBytes(stringBuilder.ToString());
				}
			}
			if (outputs != null && outputs.Count > 0)
			{
				stringBuilder.Clear();
				foreach (VmIO item2 in outputs)
				{
					VmIO inoutIo2;
					if ((inoutIo2 = item2) == null || VmModule.fTimeStamp.Contains(inoutIo2.Name))
					{
						continue;
					}
					stringBuilder.Append(inoutIo2.Name);
					stringBuilder.Append(":");
					text = inoutIo2.TypeName;
					if (inoutIo2.TypeName == "int" || inoutIo2.TypeName == "float" || inoutIo2.TypeName == "string")
					{
						ShellDynamicIO shellDynamicIO2 = ShellOutVarContainerA.ItemsA.OfType<ShellDynamicIO>().FirstOrDefault((ShellDynamicIO p) => p.DisplayNameA == AppLocalizationService.GetLang(inoutIo2.Name));
						if (shellDynamicIO2 != null)
						{
							text = shellDynamicIO2.DisplayValueTypeA;
						}
					}
					stringBuilder.Append(text);
					stringBuilder.Append(":");
					List<VmIO> allLeafIO2 = inoutIo2.GetAllLeafIO();
					string value2 = string.Join("\r", allLeafIO2.Select((VmIO p) => p.Name));
					stringBuilder.Append(value2);
					stringBuilder.Append("\n");
				}
				if (stringBuilder.Length > 0)
				{
					((IUserBytesData)vmModule.Params)["AddOutputParam"] = Encoding.UTF8.GetBytes(stringBuilder.ToString());
				}
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Params Settings"), ex.Message, MessageIcons.Error);
		}
		return 0;
	}

	private bool IsCompileResultOK(string ret)
	{
		if (!string.IsNullOrWhiteSpace(ret))
		{
			try
			{
				string[] array = ret.Split('\r');
				ret = array[0].TrimEnd();
				if (ret.Contains("Compile complete --") && ret.Contains("Errors"))
				{
					int num = ret.LastIndexOf("-");
					int num2 = ret.IndexOf("Errors");
					string value = ret.Substring(num + 1, num2 - num - 1);
					if (Convert.ToInt32(value) == 0)
					{
						return true;
					}
					return false;
				}
			}
			catch
			{
				return false;
			}
		}
		return false;
	}

	public void CompileShellCode(out bool isCompileRetOK)
	{
		LogHelper.Info("脚本模块预编译，开始");
		isCompileRetOK = false;
		SetIOParams();
		ParamSetCommandExecute();
		int num = 0;
		do
		{
			Thread.Sleep(100);
			num++;
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				continue;
			}
			string text = vmModule.Params["CompileResult"];
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			string text2 = text.ToString();
			try
			{
				if (text2.Length > 3)
				{
					if (!string.IsNullOrWhiteSpace(text2))
					{
						CompileResultA = text2;
						isCompileRetOK = IsCompileResultOK(CompileResultA);
					}
					break;
				}
			}
			catch (Exception ex)
			{
				string text3 = AppLocalizationService.GetLang("编译结果") + AppLocalizationService.GetLang("失败") + " :";
				HikMessageBox.Show(text3 + ex.Message, MessageIcons.Error);
				LogHelper.Error("获取编译结果失败！获取值:" + text);
			}
		}
		while (num < 3);
		LogHelper.Info("脚本模块预编译，结束");
	}

	public void SaveShellCode()
	{
		ParamSetCommandExecute();
	}

	public bool CheckPasswordEnable()
	{
		if (IsPasswordEnabled && string.IsNullOrWhiteSpace(Password))
		{
			return false;
		}
		return true;
	}

	public object ExecuteFunction(object obj = null)
	{
		bool isCompileRetOK = false;
		if (obj == null)
		{
			CompileShellCode(out isCompileRetOK);
			return isCompileRetOK;
		}
		if ((bool)obj)
		{
			if (!CheckPasswordEnable())
			{
				HikMessageBox.Show("Lang_Err", "请输入密码！", MessageButtons.Normal, MessageIcons.Error);
				return false;
			}
			CompileShellCode(out isCompileRetOK);
			return isCompileRetOK;
		}
		return false;
	}
}
