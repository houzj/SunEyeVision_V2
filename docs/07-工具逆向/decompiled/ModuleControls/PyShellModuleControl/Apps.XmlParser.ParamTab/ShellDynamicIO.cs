using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Apps.Localization;
using Apps.Log;
using PyShellModuleControl;
using ShellCommonControl;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ShellDynamicIO : DynamicIO
{
	private string _displayValueType;

	private ObservableCollection<string> _shellInputVar = new ObservableCollection<string>();

	private ObservableCollection<string> _shellOutputVar = new ObservableCollection<string>();

	public override List<string> IoSelectValueTypes => new List<string>
	{
		"int", "float", "string", "byte", "IMAGE", "ROIBOX", "ANNULUS", "POLYGON", "POINT", "LINE",
		"FIXTURE", "RECT", "ELLIPSE", "pointset"
	};

	public string DisplayValueTypeA
	{
		get
		{
			if (string.IsNullOrEmpty(_displayValueType))
			{
				if (ValueTypeA == "ROIANNULUS")
				{
					_displayValueType = "ANNULUS";
				}
				else if (ValueTypeA == "ROIPOLYGON")
				{
					_displayValueType = "POLYGON";
				}
				else if (ValueTypeA == "Rect")
				{
					_displayValueType = "RECT";
				}
				else
				{
					_displayValueType = ValueTypeA;
				}
			}
			return _displayValueType;
		}
		set
		{
			if (!(value != _displayValueType))
			{
				return;
			}
			_displayValueType = value;
			OnPropertyChanged("DisplayValueTypeA");
			switch (value)
			{
			case "ANNULUS":
				ValueTypeA = "ROIANNULUS";
				return;
			case "POLYGON":
				ValueTypeA = "ROIPOLYGON";
				return;
			case "RECT":
				ValueTypeA = "Rect";
				return;
			}
			if (value.Contains("[]"))
			{
				ValueTypeA = value.Substring(0, value.Length - 2);
			}
			else
			{
				ValueTypeA = value;
			}
		}
	}

	public override string ValueTypeA
	{
		get
		{
			return base.ValueTypeA;
		}
		set
		{
			if (!(value != IoDataSource.TypeName))
			{
				return;
			}
			if (CombineType.Contains(value) || CombineType.Contains(IoDataSource.TypeName))
			{
				int num = (GetParentNode() as ShellModuleDynamicIO).ItemsA.IndexOf(this);
				(GetParentNode() as IDynamic).RemoveIO(this);
				(GetParentNode() as IDynamic).AddIO(NameA, value, num + 1);
			}
			else
			{
				bool flag = false;
				if (value.Equals("pointset") || IoDataSource.TypeName.Equals("pointset"))
				{
					flag = true;
				}
				IoDataSource.TypeName = value;
				if (flag && GetParentNode() is ShellModuleDynamicIO { IsOutput: not false } shellModuleDynamicIO)
				{
					ShellHelper.ChangeRecordObject(shellModuleDynamicIO.Owner as VmModule, shellModuleDynamicIO.ItemsA.ToList());
				}
			}
			SetParamUnit();
			OnPropertyChanged("ValueTypeA");
			OnPropertyChanged("FilterSelecter");
		}
	}

	public override string NameA
	{
		get
		{
			return base.NameA;
		}
		set
		{
			bool flag = false;
			if (ShellDefine.PYTHON_KEY_WORDS.Contains(value))
			{
				string strMsg = string.Format("{0}{1}{2}{3}{4}", AppLocalizationService.GetLang("Lang_KeywordAsNameWarn"), Environment.NewLine, AppLocalizationService.GetLang("Lang_KeywordListFollow"), Environment.NewLine, string.Join(",", ShellDefine.PYTHON_KEY_WORDS));
				HikMessageBox.Show(strMsg, MessageIcons.Warning);
				flag = true;
			}
			else if (!MathHelper.IsNameString(value))
			{
				HikMessageBox.Show("Lang_StringErr", MessageIcons.Warning, isTranslate: true);
				flag = true;
			}
			else if (!string.IsNullOrEmpty(value) && char.IsNumber(value.First()))
			{
				HikMessageBox.Show("Lang_StartWithNumberWarn", MessageIcons.Warning, isTranslate: true);
				flag = true;
			}
			if (flag)
			{
				OnPropertyChanged("NameA");
				return;
			}
			string nameA = base.NameA;
			base.NameA = value;
			if (!(nameA != base.NameA))
			{
				return;
			}
			if (GetParentNode() is ShellModuleDynamicIO { IsOutput: not false } shellModuleDynamicIO)
			{
				shellModuleDynamicIO.UpdateDisplayObjectName(IoDataSource, nameA, base.NameA);
				ShellHelper.ChangeRecordObject(shellModuleDynamicIO.Owner as VmModule, shellModuleDynamicIO.ItemsA.ToList());
			}
			try
			{
				string lang = AppLocalizationService.GetLang(nameA);
				UpdateParamSubscribe(lang, value);
			}
			catch (Exception ex)
			{
				LogHelper.Error("Update running param subscribe error: " + ex.Message + "\n" + ex.StackTrace);
			}
		}
	}

	public ObservableCollection<string> ShellInputVarA
	{
		get
		{
			if (_shellInputVar.Count == 0)
			{
				_shellInputVar.Add(AppLocalizationService.GetLang("无结果"));
			}
			return _shellInputVar;
		}
		set
		{
			if (_shellInputVar != value)
			{
				_shellInputVar = value;
				OnPropertyChanged("ShellInputVarA");
			}
		}
	}

	public ObservableCollection<string> ShellOutputVarA
	{
		get
		{
			if (_shellOutputVar.Count == 0)
			{
				_shellOutputVar.Add(AppLocalizationService.GetLang("无结果"));
			}
			return _shellOutputVar;
		}
		set
		{
			if (_shellOutputVar != value)
			{
				_shellOutputVar = value;
				OnPropertyChanged("ShellOutputVarA");
			}
		}
	}

	private void UpdateParamSubscribe(string oldName, string newName)
	{
		VmModule module = IoDataSource.Root;
		if (module == null)
		{
			return;
		}
		VmProcedure root = module.Root;
		if (root == null)
		{
			return;
		}
		foreach (VmModule module2 in root.Modules)
		{
			if (module2 == module || !(module2.Params.Data is AlgorithmTabRoot { Tabs: var tabs }))
			{
				continue;
			}
			(from q in tabs.SelectMany((object p) => (p as Tab).Collection)
				where q is ISubChild || q is ISubInfo
				select q).ToList().ForEach((object x) =>
			{
				if (x is ISubInfo { IsSubA: not false } subInfo)
				{
					UpdateBinding(subInfo, module, oldName, newName);
				}
				else if (x is ISubChild subChild)
				{
					foreach (object item in subChild.SubChildren.ToList())
					{
						if (item is ISubInfo { IsSubA: not false } subInfo2)
						{
							UpdateBinding(subInfo2, module, oldName, newName);
						}
					}
				}
			});
		}
	}

	private void UpdateBinding(ISubInfo subInfo, VmModule module, string oldName, string newName)
	{
		ButtonSelecter subData = subInfo.SubData;
		string curValueA = subData.CurValueA;
		if (!string.IsNullOrEmpty(curValueA))
		{
			string[] array = curValueA.Split(' ');
			if (array.Length != 0 && module.ID.ToString() == array[0] && curValueA.Contains(oldName))
			{
				string curValueA2 = curValueA.Replace(oldName, newName);
				subData.CurValueA = curValueA2;
			}
		}
	}
}
