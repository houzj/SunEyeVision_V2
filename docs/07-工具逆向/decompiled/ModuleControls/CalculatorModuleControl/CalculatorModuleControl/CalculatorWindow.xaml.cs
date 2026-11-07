using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using Apps.XmlParser.Variable;
using Apps.XmlUI.ParamTab;
using Hik.Util;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace CalculatorModuleControl;

public partial class CalculatorWindow : BaseWindow, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private CalculatorItem _calculatorItem;

	private VmModule _moduleTag;

	private readonly CalculatorItem _realItem;

	public static readonly DependencyProperty ButtonEnableProperty = DependencyProperty.Register("ButtonEnable", typeof(bool), typeof(CalculatorWindow), new PropertyMetadata(true));

	internal SubTreeView SubTreeView;

	public bool ButtonEnable
	{
		get
		{
			return (bool)GetValue(ButtonEnableProperty);
		}
		set
		{
			SetValue(ButtonEnableProperty, value);
		}
	}

	public VmModule ModuleTagInfo
	{
		get
		{
			if (_moduleTag != null)
			{
				return _moduleTag;
			}
			if (_realItem == null)
			{
				_moduleTag = null;
			}
			else
			{
				_moduleTag = ((_realItem.GetParent() == null) ? null : _realItem.GetParent().GetModule());
			}
			return _moduleTag;
		}
	}

	public IList EnumEntrysA
	{
		get
		{
			if (_realItem == null)
			{
				return null;
			}
			int startIndex = 0;
			int includedStringInfo = GetIncludedStringInfo(ExpressBox.Text, ExpressBox.CaretIndex, "[", "]", ref startIndex);
			IList list = null;
			if (_realItem.Type == "POINT")
			{
				list = ((includedStringInfo >= 0) ? null : _realItem.GetParent()?.GetFrontCombineModule(_realItem.Type));
			}
			else
			{
				list = _realItem.GetParent()?.GetFrontModule((includedStringInfo < 0) ? new string[2] { "int", "float" } : new string[1] { "int" });
				if (CalculatorItem != null && list != null && includedStringInfo < 0)
				{
					list.Add(new Holder
					{
						DisplayNameA = ModuleTagInfo.Name,
						EnumEntrysA = new ObservableCollection<object>
						{
							new VmIO
							{
								Name = CalculatorItem.Name,
								TypeName = CalculatorItem.Type,
								Root = ModuleTagInfo
							}
						}
					});
				}
			}
			return list;
		}
	}

	public CalculatorItem CalculatorItem
	{
		get
		{
			return _calculatorItem;
		}
		set
		{
			_calculatorItem = value;
			OnPropertyChanged("CalculatorItem");
		}
	}

	public CalculatorWindow(CalculatorItem calculatorItem)
	{
		InitializeComponent();
		_realItem = calculatorItem;
		CalculatorItem = new CalculatorItem(null)
		{
			Name = calculatorItem.Name,
			Expression = calculatorItem.Expression,
			Initial = calculatorItem.Initial,
			Summation = calculatorItem.Summation,
			Type = calculatorItem.Type,
			RemarkInfo = calculatorItem.RemarkInfo
		};
	}

	public void InsertTextToEnd(string input)
	{
		if (string.IsNullOrEmpty(ExpressBox.Text))
		{
			ExpressBox.Text = input;
		}
		else
		{
			ExpressBox.Text += input;
		}
		ExpressBox.CaretIndex = ExpressBox.Text.Length;
		ExpressBox.Focus();
	}

	public void InsertText(int index, string input)
	{
		if (string.IsNullOrEmpty(input) || index > ExpressBox.Text.Length - 1)
		{
			InsertTextToEnd(input);
			return;
		}
		ExpressBox.Text = ExpressBox.Text.Insert(index, input);
		ExpressBox.CaretIndex = index + input.Length;
		ExpressBox.Focus();
	}

	public void InsertText(string input)
	{
		InsertText(ExpressBox.CaretIndex, input);
	}

	private bool IsZero()
	{
		return ExpressBox.Text.Equals("0");
	}

	private void ClearText()
	{
		ExpressBox.Text = string.Empty;
		ExpressBox.CaretIndex = 0;
		ExpressBox.Focus();
	}

	public void RemoveText(int index)
	{
		if (!string.IsNullOrEmpty(ExpressBox.Text) && index >= 0)
		{
			if (index > ExpressBox.Text.Length - 1)
			{
				index = ExpressBox.Text.Length - 1;
			}
			ExpressBox.Text = ExpressBox.Text.Remove(index, 1);
			ExpressBox.CaretIndex = index;
			ExpressBox.Focus();
		}
	}

	public void RemoveText()
	{
		if (!string.IsNullOrEmpty(ExpressBox.Text))
		{
			if (ExpressBox.SelectedText.Length > 0)
			{
				int selectionStart = ExpressBox.SelectionStart;
				ExpressBox.Text = ExpressBox.Text.Remove(ExpressBox.SelectionStart, ExpressBox.SelectionLength);
				ExpressBox.CaretIndex = selectionStart;
				ExpressBox.Focus();
			}
			else
			{
				RemoveText(ExpressBox.CaretIndex - 1);
			}
		}
	}

	public static int GetIncludedStringInfo(string input, int currentIndex, string left, string right, ref int startIndex)
	{
		try
		{
			if (currentIndex <= 0)
			{
				return -1;
			}
			if (string.IsNullOrEmpty(input))
			{
				return -1;
			}
			if (currentIndex > input.Length - 1)
			{
				return -1;
			}
			int num = input.LastIndexOf(left, currentIndex - 1, currentIndex, StringComparison.Ordinal);
			if (num < 0)
			{
				return -1;
			}
			startIndex += num + left.Length;
			int num2 = input.IndexOf(right, startIndex, StringComparison.Ordinal);
			if (num2 < 0)
			{
				return -1;
			}
			if (num2 < currentIndex)
			{
				return -1;
			}
			return num2 - startIndex;
		}
		catch (Exception)
		{
			return -1;
		}
	}

	private void PopupBtn_OnClick(object sender, RoutedEventArgs e)
	{
		if (PopupBtn.IsChecked == true)
		{
			OnPropertyChanged("EnumEntrysA");
			Popup.PlacementTarget = PopupBtn;
			Popup.IsOpen = true;
		}
		else
		{
			Popup.IsOpen = false;
		}
	}

	private void Popup_Closed(object sender, EventArgs e)
	{
		PopupBtn.IsChecked = false;
	}

	public override void Close()
	{
		if (HikMessageBox.Show(AppLocalizationService.GetLang("是否放弃保存直接退出？"), "", MessageButtons.YesOrNo, MessageIcons.Question) == VM.Controls.DialogResult.Yes)
		{
			base.Close();
		}
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button button))
		{
			return;
		}
		if (button.Content is string text)
		{
			if (char.IsLetter(text[0]))
			{
				if (IsZero())
				{
					ClearText();
				}
				InsertText(string.Concat(button.Content, "()"));
			}
			else
			{
				InsertText($"{text}");
			}
		}
		else if (button.Content is Image)
		{
			RemoveText();
		}
	}

	private void SubTreeView_OnOnTreeNodeDoubleClick(object sender, ObjectEventArgs e)
	{
		if (IsZero())
		{
			ClearText();
		}
		Popup.IsOpen = false;
		if (e.Param is VmIO vmIO)
		{
			bool flag = vmIO.Root == ModuleTagInfo;
			int startIndex = 0;
			string empty = string.Empty;
			int includedStringInfo;
			string text;
			if (flag)
			{
				includedStringInfo = GetIncludedStringInfo(ExpressBox.Text, ExpressBox.CaretIndex, "<", ">", ref startIndex);
				text = vmIO.Name;
				empty = $"<{text}>";
			}
			else
			{
				if (Popup.PlacementTarget != PopupBtn)
				{
					includedStringInfo = GetIncludedStringInfo(ExpressBox.Text, ExpressBox.CaretIndex, "[", "]", ref startIndex);
					text = vmIO.ModulePathName;
					if (includedStringInfo > 0)
					{
						ExpressBox.Select(startIndex, includedStringInfo);
						RemoveText();
					}
					InsertText($"<{text}>");
					return;
				}
				includedStringInfo = GetIncludedStringInfo(ExpressBox.Text, ExpressBox.CaretIndex, "<", ">", ref startIndex);
				text = vmIO.ModulePathName;
				empty = $"<{text}>[0]";
			}
			if (includedStringInfo >= 0)
			{
				ExpressBox.Select(startIndex, includedStringInfo);
				RemoveText();
				InsertText(text);
			}
			else
			{
				InsertText(empty);
			}
		}
		else
		{
			if (!(e.Param is Holder { HolderTarget: not null } holder))
			{
				return;
			}
			if (holder.HolderTarget.ToString() == "null")
			{
				if (Popup.PlacementTarget == PopupBtn)
				{
					ClearText();
					return;
				}
				int startIndex2 = 0;
				int includedStringInfo2 = GetIncludedStringInfo(ExpressBox.Text, ExpressBox.CaretIndex, "[", "]", ref startIndex2);
				if (includedStringInfo2 > 0)
				{
					ExpressBox.Select(startIndex2, includedStringInfo2);
					RemoveText();
				}
			}
			else
			{
				if (!(holder.DisplayNameA == "Lang_VariableManager"))
				{
					return;
				}
				VmIO subFilteFromGlobalVariable = GetSubFilteFromGlobalVariable(holder);
				if (subFilteFromGlobalVariable == null)
				{
					return;
				}
				string modulePathName = subFilteFromGlobalVariable.ModulePathName;
				if (Popup.PlacementTarget == PopupBtn)
				{
					InsertText($"<{modulePathName}>[0]");
					return;
				}
				int startIndex3 = 0;
				int includedStringInfo3 = GetIncludedStringInfo(ExpressBox.Text, ExpressBox.CaretIndex, "[", "]", ref startIndex3);
				if (includedStringInfo3 > 0)
				{
					ExpressBox.Select(startIndex3, includedStringInfo3);
					RemoveText();
				}
				InsertText($"<{modulePathName}>");
			}
		}
	}

	private VmIO GetSubFilteFromGlobalVariable(object obj)
	{
		if (obj == null)
		{
			return null;
		}
		if ((obj as Holder).HolderTarget != null)
		{
			object obj2 = ActivatorWrap.CreateInstance("Apps.XmlUI.ParamTab.GlobalVarDlgHelper");
			string[] array = new string[0];
			if ((obj as Holder).HolderTarget is List<object> list)
			{
				if (list.Count < 2)
				{
					return null;
				}
				array = list[0] as string[];
			}
			else if ((obj as Holder).HolderTarget is string[] array2)
			{
				array = array2;
			}
			string[] array3 = null;
			array3 = ((array.Length == 0) ? new string[3] { "int", "float", "string" } : array);
			IVmModule localVarModule = ModuleTagInfo.GetLocalVarModule();
			object returnObj = obj2?.GetType().InvokeMember("ShowVarSelectDlg", BindingFlags.InvokeMethod, null, obj2, new object[3] { true, localVarModule, array3 });
			if (returnObj != null)
			{
				object owner = (returnObj as VarItem).Owner;
				if (owner != null)
				{
					return (owner as VmModule).Outputs.FirstOrDefault((VmIO x) => x.Name == (returnObj as VarItem).Name);
				}
			}
		}
		return null;
	}

	private void ExpressBox_OnSelectionChanged(object sender, RoutedEventArgs e)
	{
		int startIndex = 0;
		int includedStringInfo = GetIncludedStringInfo(ExpressBox.Text, ExpressBox.CaretIndex, "[", "]", ref startIndex);
		if (includedStringInfo < 0)
		{
			if (Popup.PlacementTarget == ExpressBox && Popup.IsOpen)
			{
				Popup.IsOpen = false;
			}
		}
		else if (_realItem.Type != "POINT")
		{
			OnPropertyChanged("EnumEntrysA");
			Popup.PlacementTarget = ExpressBox;
			Popup.IsOpen = true;
		}
	}

	private void Verify_OnClick(object sender, RoutedEventArgs e)
	{
		ButtonEnable = false;
		VmModule moduleTagInfo = ModuleTagInfo;
		if (moduleTagInfo == null)
		{
			return;
		}
		string text = Helper.VariableToModuleString(CalculatorItem.Expression, moduleTagInfo, new string[3] { "int", "float", "POINT" }, null);
		try
		{
			moduleTagInfo.Params["command#verify"] = text + "#" + CalculatorItem.Name;
			if (CalculatorItem.Type == "POINT")
			{
				if (!text.Contains("POINT") || text.Contains("int") || text.Contains("float") || text.Contains($"<{CalculatorItem.Name}>"))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Caculate"), AppLocalizationService.GetLang("Lang_FmuCheckFail"), MessageIcons.Error);
					ButtonEnable = true;
					return;
				}
				string text2 = text;
				text2 = text2.Replace("POINT:", "");
				string pattern = "<\\s*(<base64\\s{1,1}(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?\\s{1,1}/>)\\s*>(\\[((\\d+)|(<\\s*(<base64\\s{1,1}(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?\\s{1,1}/>)\\s*>))\\])?";
				text2 = Regex.Replace(text2, pattern, (Match m) => "A");
				while (text2.Contains("+-") || text2.Contains("--"))
				{
					text2 = text2.Replace("+-", "+").Replace("--", "-");
				}
				text2 = text2.Replace("*-", "*");
				if (text2.Contains("*+") || text2.Contains("+*") || text2.Contains("-*") || text2.Contains("**"))
				{
					throw new Exception("Contains '*+' or '+*' or ‘-*’or ‘**’");
				}
				if (text2.Contains("++") || text2.Contains("-+"))
				{
					throw new Exception("Contains '++' or '-+'");
				}
				string pattern2 = "(\\d+(\\.\\d+)?)";
				text2 = Regex.Replace(text2, pattern2, (Match m) => "B");
				if (Regex.Matches(text2, "^[AB\\+\\-\\*\\(\\)]+$").Count != 1)
				{
					throw new Exception("Contains other char.");
				}
				if (!CalculateResult(text2))
				{
					throw new Exception("Calculate result not right.");
				}
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Caculate"), AppLocalizationService.GetLang("Lang_FmuCheckSuc"), MessageIcons.Ok);
		}
		catch (Exception arg)
		{
			LogHelper.objLog.Error($"Verify error: {arg}");
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Caculate"), AppLocalizationService.GetLang("Lang_FmuCheckFail"), MessageIcons.Error);
		}
		finally
		{
			ButtonEnable = true;
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

	private void Ok_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
	}

	private void Cancel_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
	}
}
