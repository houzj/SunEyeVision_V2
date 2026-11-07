using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Apps.Json;
using Apps.Localization;
using Apps.Log;
using GlobalScriptUI;
using Hik.Xml;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_Shell : NameValueItem, IBottomCommand, ISpecialModuleParamTabVisible
{
	public string ShellCodeFilePath;

	public string Textinshell;

	private string CompileResult;

	private string _ShellFilePath = "";

	private readonly string tmpShellFileDir = AppDomain.CurrentDomain.BaseDirectory + "Module\\";

	private readonly string tmpShellFileDirS = AppDomain.CurrentDomain.BaseDirectory + "Module(sp)\\x64\\Logic\\";

	private string[] strCompileError = new string[4] { "LanguageCompileCreateObjectError", "LanguageCompileMissFunctionError", "LanguageCompileExecuteError", "LanguageCompileExeception" };

	private ShellModuleDynamicIO ShellInVarContainer;

	private ShellModuleDynamicIO ShellOutVarContainer;

	private GroupDisplayParam ShellDisplayContainer;

	public bool IsPasswordEnabled;

	public bool IsShowPassword;

	public string Password;

	public string ScriptRefrence;

	private bool _isInputParamTabVisible = true;

	private bool _isOutputParamTabVisible = true;

	private bool _isDisplayParamTabVisible = true;

	private int _selectedTabIndex;

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
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			string text = Path.Combine(ShellFilePath, "ShellCodeFile.cs");
			try
			{
				using FileStream fileStream = new FileStream(text, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
				fileStream.SetLength(0L);
				fileStream.Write(bytes, 0, bytes.Length);
				fileStream.Flush(flushToDisk: true);
			}
			catch (Exception ex)
			{
				LogHelper.Error("write file is error:" + ex.ToString());
			}
			ShellCodeFilePathA = text;
		}
	}

	private string ShellFilePath
	{
		get
		{
			if (string.IsNullOrEmpty(_ShellFilePath))
			{
				_ShellFilePath = GetShellPath();
			}
			return _ShellFilePath;
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

	public GroupDisplayParam ShellDisplayContainerA
	{
		get
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected Obj, but got Unknown
			if (ShellDisplayContainer == null)
			{
				ShellDisplayContainer = new GroupDisplayParam();
				((BaseNode)(object)ShellDisplayContainer).SetParentNode((object)this);
			}
			return ShellDisplayContainer;
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
				LogHelper.Error("设置脚本模块程序集失败：" + ex2.errorMessage);
			}
		}
	}

	public bool IsInputParamTabVisible
	{
		get
		{
			return _isInputParamTabVisible;
		}
		set
		{
			if (value != _isInputParamTabVisible)
			{
				_isInputParamTabVisible = value;
				OnPropertyChanged("IsInputParamTabVisible");
				CheckAndUpdateSelectedTab();
			}
		}
	}

	public bool IsOutputParamTabVisible
	{
		get
		{
			return _isOutputParamTabVisible;
		}
		set
		{
			if (value != _isOutputParamTabVisible)
			{
				_isOutputParamTabVisible = value;
				OnPropertyChanged("IsOutputParamTabVisible");
				CheckAndUpdateSelectedTab();
			}
		}
	}

	public bool IsDisplayParamTabVisible
	{
		get
		{
			return _isDisplayParamTabVisible;
		}
		set
		{
			if (value != _isDisplayParamTabVisible)
			{
				_isDisplayParamTabVisible = value;
				OnPropertyChanged("IsDisplayParamTabVisible");
				CheckAndUpdateSelectedTab();
			}
		}
	}

	public int SelectedTabIndex
	{
		get
		{
			return _selectedTabIndex;
		}
		set
		{
			if (_selectedTabIndex != value)
			{
				_selectedTabIndex = value;
				OnPropertyChanged("SelectedTabIndex");
			}
		}
	}

	private string GetShellPath()
	{
		try
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			Assembly assembly = assemblies.FirstOrDefault((Assembly x) => x.GetName().Name == "Script.Support");
			if (assembly != null)
			{
				return assembly.Location.Substring(0, assembly.Location.LastIndexOf("\\"));
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("GetShellPath is error:" + ex.ToString());
		}
		return "";
	}

	public override int GetParamUnit()
	{
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Expected Obj, but got Unknown
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		int result = 0;
		VmModule vmModule = GetVmModule();
		if ("ShellContent" == Name)
		{
			try
			{
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
		string text = vmModule?.Params["ScriptRefrence"];
		if (!string.IsNullOrEmpty(text))
		{
			List<ShellRefrences> list = new List<ShellRefrences>();
			string[] array = text.Split('\r');
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				if (!string.IsNullOrEmpty(text2))
				{
					string[] array3 = text2.Split('\n');
					if (array3 != null && array3.Length == 2)
					{
						ShellRefrences val = new ShellRefrences();
						val.Name = array3[0];
						val.refrencesType = (RefrencesType)Enum.Parse(typeof(RefrencesType), array3[1]);
						list.Add(val);
					}
				}
			}
			if (list.Count > 0)
			{
				ScriptRefrence = JsonConvert.SerializeObject(list);
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
				((IUserBytesData)vmModule.Params)["SetShellContent"] = Encoding.UTF8.GetBytes(textinshell);
			}
		}
		catch (Exception ex)
		{
			string message = AppLocalizationService.GetLang("ShellModule") + AppLocalizationService.GetLang("Lang_Modu") + AppLocalizationService.GetLang("Params Settings") + " : " + ex.Message;
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
					if (inoutIo.TypeName == "int" || inoutIo.TypeName == "float" || inoutIo.TypeName == "string" || inoutIo.TypeName == "ROIBOX")
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
					((IUserBytesData)vmModule.Params)["AddInputParam"] = StringUtil.UTF8GetBytesPadZero(stringBuilder.ToString());
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
					if (inoutIo2.TypeName == "int" || inoutIo2.TypeName == "float" || inoutIo2.TypeName == "string" || inoutIo2.TypeName == "ROIBOX")
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
					((IUserBytesData)vmModule.Params)["AddOutputParam"] = StringUtil.UTF8GetBytesPadZero(stringBuilder.ToString());
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
				if (ret.Contains("Compile complete --") && ret.EndsWith("Warnings") && ret.Contains("Errors"))
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
			Thread.Sleep(500);
			num++;
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				continue;
			}
			byte[] data = null;
			try
			{
				int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(vmModule.ID, "CompileResult", ref data);
				if (binaryData == -536870911)
				{
					continue;
				}
			}
			catch (VmException ex)
			{
				LogHelper.Error("GetBinaryData is error:" + ex.ToString());
			}
			if (data == null || data.Length == 0)
			{
				continue;
			}
			string text = StringUtil.UTF8GetString(data);
			try
			{
				if (!string.IsNullOrWhiteSpace(text) && text.Length > 3)
				{
					CompileResultA = text;
					isCompileRetOK = IsCompileResultOK(CompileResultA);
				}
			}
			catch (Exception ex2)
			{
				string text2 = AppLocalizationService.GetLang("编译结果") + AppLocalizationService.GetLang("失败") + " :";
				HikMessageBox.Show(text2 + ex2.Message, MessageIcons.Error);
				LogHelper.Error("获取编译结果失败！获取值:" + text);
				continue;
			}
			break;
		}
		while (num < 12);
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

	private bool IsTabVisible(int index)
	{
		return index switch
		{
			0 => IsInputParamTabVisible, 
			1 => IsOutputParamTabVisible, 
			2 => IsDisplayParamTabVisible, 
			_ => false, 
		};
	}

	public void SetParamTabVisible(string tabName, bool isVisible)
	{
		if (string.IsNullOrEmpty(tabName))
		{
			throw new VmException(-536870911);
		}
		int tabIndex = GetTabIndex(tabName);
		if (tabIndex == -1)
		{
			throw new VmException(-536870911);
		}
		bool flag = false;
		switch (tabIndex)
		{
		case 0:
			flag = IsInputParamTabVisible;
			IsInputParamTabVisible = isVisible;
			break;
		case 1:
			flag = IsOutputParamTabVisible;
			IsOutputParamTabVisible = isVisible;
			break;
		case 2:
			flag = IsDisplayParamTabVisible;
			IsDisplayParamTabVisible = isVisible;
			break;
		}
		if (!flag & isVisible)
		{
			SelectedTabIndex = tabIndex;
		}
	}

	private void CheckAndUpdateSelectedTab()
	{
		if (SelectedTabIndex == -1 || !IsTabVisible(SelectedTabIndex))
		{
			int num = FindNextVisibleTabIndex(SelectedTabIndex);
			if (num == -1 && AnyTabVisible())
			{
				num = FindNextVisibleTabIndex(-1);
			}
			SelectedTabIndex = num;
		}
	}

	private int GetTabIndex(string tabName)
	{
		Dictionary<int, string> dictionary = new Dictionary<int, string>
		{
			{
				0,
				AppLocalizationService.GetLang("Tab_Input Params")
			},
			{
				1,
				AppLocalizationService.GetLang("Tab_Output Params")
			},
			{
				2,
				AppLocalizationService.GetLang("Tab_Display Params")
			}
		};
		foreach (KeyValuePair<int, string> item in dictionary)
		{
			if (item.Value == tabName)
			{
				return item.Key;
			}
		}
		return -1;
	}

	private bool AnyTabVisible()
	{
		return IsInputParamTabVisible || IsOutputParamTabVisible || IsDisplayParamTabVisible;
	}

	private int FindNextVisibleTabIndex(int startIndex)
	{
		for (int i = startIndex + 1; i < 3; i++)
		{
			if (IsTabVisible(i))
			{
				return i;
			}
		}
		for (int j = 0; j < startIndex; j++)
		{
			if (IsTabVisible(j))
			{
				return j;
			}
		}
		return -1;
	}

	public List<string> GetParamTabNames()
	{
		List<string> list = new List<string>();
		try
		{
			list.Add(AppLocalizationService.GetLang("Tab_Input Params"));
			list.Add(AppLocalizationService.GetLang("Tab_Output Params"));
			list.Add(AppLocalizationService.GetLang("Tab_Display Params"));
		}
		catch (Exception)
		{
		}
		return list;
	}
}
