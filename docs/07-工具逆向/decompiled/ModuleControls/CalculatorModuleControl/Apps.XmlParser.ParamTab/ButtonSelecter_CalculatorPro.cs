using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Apps.Localization;
using Apps.Log;
using CalculatorModuleControl;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_CalculatorPro : NameValueItem, IBottomCommand, IButtonSelectedValueChange
{
	private Dictionary<string, string> _originexp = new Dictionary<string, string>();

	private const string _prefix = "var";

	private VmModule _moduleTag;

	private const int MaxLen = 256;

	private const string sendData = "command#data";

	private const string sendlargeStart = "command#large#start";

	private const string sendinglarge = "command#large#ing";

	private const string sendlargeend = "command#large#end";

	private const string sendStart = "command#start";

	private const string restIO = "command#io";

	private const string Restcmd = "command#restvalue";

	private Dictionary<string, VmIO> _filters = new Dictionary<string, VmIO>();

	private ObservableCollection<CalculatorItem> _items;

	private bool _buttonEnable = true;

	private bool isResultOK = false;

	public static string[] filternames = new string[10] { "fArrivalTimeStampLow", "fArrivalTimeStampHigh", "fLeaveTimeStampLow", "fLeaveTimeStampHigh", "模块状态", "Status", "ModuStatus", "ResultShow", "PositionX", "PositionY" };

	private DelegateCommand<int?> _addCommand;

	public DelegateCommand<int?> AddCommand
	{
		get
		{
			return _addCommand;
		}
		set
		{
			_addCommand = value;
			OnPropertyChanged("AddCommand");
		}
	}

	public ObservableCollection<CalculatorItem> Items
	{
		get
		{
			return _items;
		}
		set
		{
			_items = value;
			OnPropertyChanged("Items");
		}
	}

	public bool ButtonEnable
	{
		get
		{
			return _buttonEnable;
		}
		set
		{
			_buttonEnable = value;
			OnPropertyChanged("ButtonEnable");
		}
	}

	public event OnValueChangeEventHandler OnValueChange;

	public override void SetParentNode(object parentNode)
	{
		base.SetParentNode(parentNode);
	}

	public override int GetParamUnit()
	{
		GetModule();
		AlgorithmTabRoot algorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
		if (_moduleTag != null)
		{
			Init();
		}
		return base.GetParamUnit();
	}

	public override object GetPropertyValueByName(string name)
	{
		if (Items == null)
		{
			Init();
		}
		CalculatorItem calculatorItem = Items?.FirstOrDefault((CalculatorItem p) => "%" + p.Name + "%" == name || p.Name == name);
		return (calculatorItem == null) ? "0.0" : calculatorItem.Expression;
	}

	public object GetInitialValueByName(string name)
	{
		if (Items == null)
		{
			Init();
		}
		Dictionary<string, float> dictionary = new Dictionary<string, float>();
		if (name == "ResultShow")
		{
			foreach (CalculatorItem item in Items)
			{
				if (!(item.Type == "POINT"))
				{
					dictionary.Add(item.Name, item.Initial);
				}
			}
		}
		else
		{
			CalculatorItem calculatorItem = Items?.FirstOrDefault((CalculatorItem p) => "%" + p.Name + "%" == name || p.Name == name);
			if (calculatorItem != null)
			{
				dictionary.Add(name, calculatorItem.Initial);
			}
		}
		return dictionary;
	}

	public override void SetPropertyValueByName(string name, object value)
	{
		if (Items == null)
		{
			Init();
		}
		CalculatorItem calculatorItem = Items?.FirstOrDefault((CalculatorItem p) => "%" + p.Name + "%" == name || p.Name == name);
		if (calculatorItem != null)
		{
			calculatorItem.Expression = $"{value}";
			Update();
		}
	}

	public VmModule GetModule()
	{
		if (_moduleTag != null)
		{
			return _moduleTag;
		}
		if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
		{
			return null;
		}
		_moduleTag = algorithmTabRoot.Owner as VmModule;
		return _moduleTag;
	}

	private void RunOnce()
	{
		VmModule module = GetModule();
		if (module != null && ButtonEnable && Update())
		{
			_moduleTag.Run();
		}
	}

	private void DoOk()
	{
		VmModule module = GetModule();
		if (module == null)
		{
			isResultOK = false;
			return;
		}
		if (!ButtonEnable)
		{
			isResultOK = false;
			return;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (CalculatorItem item in Items)
		{
			string empty = string.Empty;
			try
			{
				empty = module.Params[item.Name];
				dictionary.Add(item.Name, empty);
			}
			catch
			{
			}
		}
		if (!Update())
		{
			isResultOK = false;
			return;
		}
		foreach (CalculatorItem item2 in Items)
		{
			string empty2 = string.Empty;
			try
			{
				empty2 = module.Params[item2.Name];
				if (dictionary.Keys.Contains(item2.Name) && dictionary[item2.Name] == empty2)
				{
					continue;
				}
			}
			catch
			{
				continue;
			}
			break;
		}
		if (OnValueChange != null)
		{
			ValueChange();
		}
		isResultOK = true;
	}

	private void AddItem(int? maxCount)
	{
		if (Items != null && maxCount.HasValue)
		{
			if (Items.Count == maxCount.Value)
			{
				string strMsg = AppLocalizationService.GetLang("Lang_NumOutErr") + maxCount;
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParmNum"), strMsg, MessageIcons.Warning);
				return;
			}
			CalculatorItem item = new CalculatorItem(this)
			{
				Name = GetDefaultName(),
				Expression = "0"
			};
			Items.Add(item);
		}
	}

	private void Init()
	{
		AddCommand = new DelegateCommand<int?>((Action<int?>)((int? maxCount) =>
		{
			AddItem(maxCount);
		}));
		VmModule module = GetModule();
		if (module == null)
		{
			return;
		}
		VmIOCollection outputs = module.Outputs;
		if (outputs == null || outputs.Count == 0)
		{
			return;
		}
		IEnumerable<VmIO> enumerable = outputs.Where((VmIO p) => p is DynamicVmIO && !p.IsResultShow);
		List<CalculatorItem> list = new List<CalculatorItem>();
		foreach (VmIO item in enumerable)
		{
			StringBuilder stringBuilder = new StringBuilder();
			try
			{
				string text = item.Name;
				if (text.StartsWith("%") && text.EndsWith("%"))
				{
					text = text.Substring(1, text.Length - 2);
				}
				stringBuilder.Append(module.Params[text]);
			}
			catch
			{
				continue;
			}
			string show = GetShow(stringBuilder.ToString());
			CalculatorItem calculatorItem = show.DeSerializationFromJson<CalculatorItem>();
			if (calculatorItem != null)
			{
				calculatorItem._parent = this;
				calculatorItem.vmIO = item;
				list.Add(calculatorItem);
			}
		}
		Items = new ObservableCollection<CalculatorItem>(list.OrderBy((CalculatorItem p) => p.Index));
		_originexp.Clear();
		foreach (CalculatorItem item2 in Items)
		{
			_originexp.Add(item2.Name, item2.Expression);
		}
	}

	private string GetShow(string input)
	{
		string text = input;
		VmIOCollection vmIOCollection = GetModule()?.Inputs;
		for (int num = vmIOCollection.Count - 1; num >= 0; num--)
		{
			string text2 = vmIOCollection[num].Name;
			if (text2.StartsWith("%") && text2.EndsWith("%"))
			{
				text2 = text2.Substring(1, text2.Length - 2);
			}
			text2 = text2.Base64StringToString();
			text = text.Replace(text2, (vmIOCollection[num].RelateIO == null) ? "NULL" : vmIOCollection[num].RelateIO.ModulePathName);
		}
		return text;
	}

	public IList GetFrontModule(string[] filterparams)
	{
		return GetModule()?.GetFrontFilterTree((VmModule p) => true, "Output", filterparams);
	}

	public IList GetFrontCombineModule(string typeName)
	{
		return GetModule()?.GetFrontCombinationTree((VmModule p) => true, "Output", typeName);
	}

	public string GetDefaultName()
	{
		int num = 0;
		List<string> list = (from b in Items
			where b.Name.StartsWith("var")
			select b.Name).ToList();
		List<int> list2 = new List<int>();
		foreach (string item in list)
		{
			string text = item.Substring("var".Length, item.Length - "var".Length);
			int result = -1;
			if (text.Length > 0 && int.TryParse(text, out result))
			{
				list2.Add(result);
				if (result > num)
				{
					num = result;
				}
			}
		}
		if (num == int.MaxValue)
		{
			num = 0;
		}
		for (; list2.Contains(num); num++)
		{
		}
		return "var" + num;
	}

	public bool Update()
	{
		ButtonEnable = false;
		VmModule module = GetModule();
		if (module == null || Items == null)
		{
			ButtonEnable = true;
			return false;
		}
		VmIOCollection outputs = module.Outputs;
		if (outputs == null)
		{
			ButtonEnable = true;
			return false;
		}
		bool flag = true;
		IEnumerable<VmIO> source = outputs.Where((VmIO p) => p is DynamicVmIO && !p.IsResultShow);
		if (source.Count() == Items.Count)
		{
			foreach (CalculatorItem item3 in Items)
			{
				try
				{
					string input = module.Params[item3.Name];
					input = GetShow(input);
					CalculatorItem calculatorItem = input.DeSerializationFromJson<CalculatorItem>();
					if (!calculatorItem.Equals(item3))
					{
						flag = false;
						break;
					}
				}
				catch
				{
					flag = false;
					break;
				}
			}
		}
		else
		{
			flag = false;
		}
		if (flag)
		{
			ButtonEnable = true;
			return true;
		}
		try
		{
			((IUserBytesData)module.Params)["command#start"] = new byte[1];
		}
		catch
		{
		}
		_filters.Clear();
		int num = 0;
		foreach (CalculatorItem item4 in Items)
		{
			item4.Index = num;
			num++;
			string s = Helper.VariableToModuleString(item4.ToJson(), module, new string[3] { "int", "float", "POINT" }, _filters);
			try
			{
				if (item4.Type == "POINT")
				{
					string text = Helper.VariableToModuleString(item4.Expression, module, new string[3] { "int", "float", "POINT" }, null);
					module.Params["command#verify"] = text + "#" + item4.Name;
					CheckPoint(item4, text);
				}
			}
			catch (Exception arg)
			{
				string strMsg = item4.Name + AppLocalizationService.GetLang("Lang_ParamErr");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamErr"), strMsg, MessageIcons.Error);
				LogHelper.objLog.Error($"Verify error: {arg}");
				ButtonEnable = true;
				return false;
			}
			try
			{
				byte[] bytes = Encoding.UTF8.GetBytes(s);
				((IUserBytesData)module.Params)["command#data"] = bytes;
			}
			catch
			{
				string strMsg = item4.Name + AppLocalizationService.GetLang("Lang_ParamErr");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamErr"), strMsg, MessageIcons.Error);
				ButtonEnable = true;
				return false;
			}
		}
		OnSetValueCompleted(success: true);
		try
		{
			module.Params["command#io"] = "";
		}
		catch
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamErr"), AppLocalizationService.GetLang("Lang_ReSetIOErr"), MessageIcons.Error);
			ButtonEnable = true;
			return false;
		}
		bool flag2 = false;
		List<string> list = source.Select((VmIO p) => p.Name + "_" + p.TypeName).ToList();
		List<string> list2 = Items.Select((CalculatorItem p) => "%" + p.Name + "%_" + p.Type).ToList();
		List<string> list3 = source.Select((VmIO p) => p.Name).ToList();
		List<Tuple<string, string>> list4 = new List<Tuple<string, string>>();
		foreach (CalculatorItem item5 in Items)
		{
			flag2 = true;
			if (item5.vmIO != null)
			{
				if (item5.vmIO.TypeName == item5.Type)
				{
					if (item5.vmIO.Name.Replace("%", "") == item5.Name)
					{
						list3.Remove(item5.vmIO.Name);
						continue;
					}
					list3.Remove(item5.vmIO.Name);
					item5.vmIO.Name = item5.Name;
				}
				else
				{
					list4.Add(new Tuple<string, string>(item5.Name, item5.Type));
				}
			}
			else
			{
				list4.Add(new Tuple<string, string>(item5.Name, item5.Type));
			}
		}
		foreach (string delete in list3)
		{
			flag2 = true;
			VmIO item = source.First((VmIO p) => p.Name == delete);
			outputs.Remove(item);
		}
		foreach (Tuple<string, string> item6 in list4)
		{
			flag2 = true;
			string item2 = item6.Item1;
			string type = item6.Item2;
			if (type == "POINT")
			{
				CombinRoot instance = CombinRoot.GetInstance();
				Combination obj5 = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.Style == type);
				AddIOFromCombination(obj5, module, module, outputs, item2);
			}
			else
			{
				outputs.Add(new DynamicVmIO
				{
					TypeName = type,
					Name = item2,
					Root = module,
					Parent = module
				});
			}
		}
		bool flag3 = false;
		if (!flag2)
		{
			foreach (CalculatorItem item7 in Items)
			{
				string expression = item7.Expression;
				string text2 = _originexp[item7.Name];
				if (!(expression == text2) && (!float.TryParse(expression, out var result) || !float.TryParse(text2, out result)))
				{
					flag3 = true;
				}
			}
		}
		if (!flag2 && !flag3)
		{
			flag3 = module.Inputs.Any((VmIO p) => p.RelateIO == null);
		}
		if (flag2 | flag3)
		{
			RestInput();
		}
		_originexp.Clear();
		foreach (CalculatorItem item8 in Items)
		{
			_originexp.Add(item8.Name, item8.Expression);
		}
		ButtonEnable = true;
		return true;
	}

	private void CheckPoint(CalculatorItem item, string str)
	{
		if (item.Type == "POINT")
		{
			if (!str.Contains("POINT") || str.Contains("int") || str.Contains("float") || str.Contains($"<{item.Name}>"))
			{
				throw new Exception("Subscribe error.");
			}
			string text = str;
			text = text.Replace("POINT:", "");
			string pattern = "<\\s*(<base64\\s{1,1}(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?\\s{1,1}/>)\\s*>(\\[((\\d+)|(<\\s*(<base64\\s{1,1}(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?\\s{1,1}/>)\\s*>))\\])?";
			text = Regex.Replace(text, pattern, (Match m) => "A");
			while (text.Contains("+-") || text.Contains("--"))
			{
				text = text.Replace("+-", "+").Replace("--", "-");
			}
			text = text.Replace("*-", "*");
			if (text.Contains("*+") || text.Contains("+*") || text.Contains("-*") || text.Contains("**"))
			{
				throw new Exception("Contains '*+' or '+*' or '-*'or '**'");
			}
			if (text.Contains("++") || text.Contains("-+"))
			{
				throw new Exception("Contains '++' or '-+'");
			}
			string pattern2 = "(\\d+(\\.\\d+)?)";
			text = Regex.Replace(text, pattern2, (Match m) => "B");
			if (Regex.Matches(text, "^[AB\\+\\-\\*\\(\\)]+$").Count != 1)
			{
				throw new Exception("Contains other char.");
			}
			if (!CalculateResult(text))
			{
				throw new Exception("Calculate result not right.");
			}
		}
	}

	private bool CalculateResult(string inputString)
	{
		if (string.IsNullOrWhiteSpace(inputString))
		{
			return false;
		}
		int num = 0;
		int num2 = 0;
		int num3 = inputString.Length;
		int num4 = 0;
		for (int i = 0; i < inputString.Length; i++)
		{
			if (inputString[i] == '(')
			{
				num++;
				if (i < num3 && i > num4)
				{
					num4 = i;
				}
			}
			else if (inputString[i] == ')')
			{
				num2++;
				if (i < num3)
				{
					num3 = i;
				}
			}
		}
		if (num != num2)
		{
			return false;
		}
		string result;
		if (num == 0)
		{
			return CheckInputOk(inputString, isFinally: true, out result);
		}
		string text = inputString.Substring(num4 + 1, num3 - num4 - 1);
		if (!CheckInputOk(text, isFinally: false, out var result2))
		{
			return false;
		}
		inputString = inputString.Replace("(" + text + ")", result2);
		return CalculateResult(inputString);
	}

	private bool CheckInputOk(string inputString, bool isFinally, out string result)
	{
		if (inputString.StartsWith("-A") || inputString.StartsWith("-B"))
		{
			inputString = inputString.Substring(1, inputString.Length - 1);
		}
		while (inputString.Contains("*"))
		{
			if (inputString.Contains("A*A"))
			{
				result = "";
				return false;
			}
			inputString = inputString.Replace("B*B", "B").Replace("A*B", "A").Replace("B*A", "A");
		}
		while (inputString.Contains("+") || inputString.Contains("-"))
		{
			if (inputString.Contains("A-B") || inputString.Contains("B-A") || inputString.Contains("A+B") || inputString.Contains("B+A"))
			{
				result = "";
				return false;
			}
			inputString = inputString.Replace("B-B", "B").Replace("B+B", "B").Replace("A-A", "A")
				.Replace("A+A", "A");
		}
		if (isFinally)
		{
			if (inputString != "A")
			{
				result = "";
				return false;
			}
		}
		else if (inputString != "A" && inputString != "B")
		{
			result = "";
			return false;
		}
		result = inputString;
		return true;
	}

	private VmIO AddIOFromCombination(object obj, VmModule owner, object parent, IList parentList, string suggestName = null)
	{
		if (obj is Combination combination)
		{
			string text = suggestName ?? combination.Name;
			DynamicVmIO dynamicVmIO = new DynamicVmIO
			{
				Name = text,
				Root = owner,
				Parent = parent,
				TypeName = combination.Style
			};
			parentList.Add(dynamicVmIO);
			foreach (object filter2 in combination.Filters)
			{
				AddIOFromCombination(filter2, owner, dynamicVmIO, dynamicVmIO.Childs, (text.StartsWith("%") && text.EndsWith("%")) ? text.Substring(1, text.Length - 2) : text);
			}
			return dynamicVmIO;
		}
		if (obj is Filter filter)
		{
			AddLeafIO(filter.ValueTypeA, owner, parent, parentList, filter.NameA + suggestName);
		}
		return null;
	}

	private VmIO AddLeafIO(string typeName, VmModule owner, object parent, IList parentList, string suggestName)
	{
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Name = suggestName,
			Root = owner,
			Parent = parent,
			TypeName = typeName
		};
		parentList.Add(dynamicVmIO);
		return dynamicVmIO;
	}

	public void RestValue()
	{
		VmModule module = GetModule();
		if (module == null || !ButtonEnable)
		{
			return;
		}
		try
		{
			module.Params["command#restvalue"] = "1";
		}
		catch
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Caculate"), AppLocalizationService.GetLang("Lang_ReSetFail"), MessageIcons.Error);
		}
	}

	public static void ReSetValueAllCalculatorModule()
	{
	}

	private void RestInput()
	{
		VmModule module = GetModule();
		if (module == null)
		{
			return;
		}
		for (int num = module.Inputs.Count - 1; num >= 0; num--)
		{
			module.Inputs.RemoveAt(num);
		}
		foreach (string key in _filters.Keys)
		{
			VmIO vmIO = null;
			if (_filters[key].TypeName == "POINT")
			{
				CombinRoot instance = CombinRoot.GetInstance();
				Combination obj = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.Style == _filters[key].TypeName);
				vmIO = AddIOFromCombination(obj, module, module, module.Inputs, key);
			}
			else
			{
				vmIO = new DynamicVmIO
				{
					Name = key,
					TypeName = _filters[key].TypeName,
					Root = module,
					Parent = module
				};
				module.Inputs.Add(vmIO);
			}
			if (vmIO != null)
			{
				vmIO.RelateIO = _filters[key];
			}
		}
	}

	public object ExecuteFunction(object obj = null)
	{
		if (obj == null)
		{
			RestValue();
			return null;
		}
		if (obj.ToString() == "OKCommand")
		{
			DoOk();
			return isResultOK;
		}
		if (obj.ToString() == "RunOnceCommand")
		{
			RunOnce();
			return null;
		}
		if (obj is string[])
		{
			string[] array = obj as string[];
			if (array.Length == 3 && array[0] == "SetValueCommand")
			{
				SetPropertyValueByName(array[1], array[2]);
			}
			else
			{
				if (array.Length == 2 && array[0] == "GetValueCommand")
				{
					return GetPropertyValueByName(array[1]);
				}
				if (array.Length == 2 && array[0] == "GetInitialValueCommand")
				{
					return GetInitialValueByName(array[1]);
				}
			}
			return null;
		}
		return null;
	}

	private void ValueChange()
	{
		if (OnValueChange == null)
		{
			return;
		}
		foreach (CalculatorItem item in Items)
		{
			KeyValueEventArgs args = new KeyValueEventArgs
			{
				Value = item.Expression,
				ValueType = item.Type,
				Key = item.Name
			};
			OnValueChange(this, args);
		}
	}
}
