using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class DataClassificationConfig : NameValueItem
{
	public ObservableCollection<ClassificationItem> _ClassificationItems;

	public Boolean _BooleanData;

	public ClassificationItem _SelectedClassificationItem;

	private ICommand _AddClassificationItemCommand = null;

	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

	private bool hasRegistered;

	private VmModule OwnerVm => GetVmModule();

	public ObservableCollection<ClassificationItem> ClassificationItems
	{
		get
		{
			if (_ClassificationItems == null)
			{
				_ClassificationItems = new ObservableCollection<ClassificationItem>();
			}
			return _ClassificationItems;
		}
		set
		{
			if (_ClassificationItems != value)
			{
				_ClassificationItems = value;
				OnPropertyChanged("ClassificationItems");
			}
		}
	}

	public Boolean BooleanData
	{
		get
		{
			if (_BooleanData == null)
			{
				BooleanDataInit();
			}
			return _BooleanData;
		}
	}

	public ClassificationItem SelectedClassificationItem
	{
		get
		{
			return _SelectedClassificationItem;
		}
		set
		{
			if (_SelectedClassificationItem != value)
			{
				_SelectedClassificationItem = value;
				OnPropertyChanged("SelectedClassificationItem");
				if (_SelectedClassificationItem != null && _SelectedClassificationItem.ConditionGroupItems.Count > 0)
				{
					_SelectedClassificationItem.SelectedConditionGroupItem = _SelectedClassificationItem?.ConditionGroupItems[0];
				}
			}
		}
	}

	public ICommand AddClassificationItemCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_AddClassificationItemCommand == null)
			{
				_AddClassificationItemCommand = (ICommand)new DelegateCommand((Action)AddClassificationItemCmdExecute);
			}
			return _AddClassificationItemCommand;
		}
	}

	public ICommand PreviewMouseWheelCommand
	{
		get
		{
			if (_PreviewMouseWheelCommand == null)
			{
				_PreviewMouseWheelCommand = new DelegateCommand<object>((Action<object>)PreviewMouseWheelCommandExcute);
			}
			return (ICommand)_PreviewMouseWheelCommand;
		}
	}

	public void BooleanDataInit()
	{
		_BooleanData = new Boolean();
		_BooleanData.OnCurValueChanged -= BooleanDataParamValueChanged;
		_BooleanData.OnCurValueChanged += BooleanDataParamValueChanged;
	}

	public void BooleanDataParamValueChanged(object sender, EventArgs e)
	{
		bool curValue = (sender as Boolean).CurValue;
		SetIsIncompatible(curValue);
	}

	public void AddClassificationItemCmdExecute()
	{
		if (Enumerable.Count(ClassificationItems) >= 10)
		{
			string strMsg = AppLocalizationService.GetLang("Lang_classifitionCountError") + $"【{10}】";
			HikMessageBox.Show(strMsg);
			return;
		}
		VmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule;
		int i;
		for (i = 0; !CheckGroupName(i); i++)
		{
			if (i >= 10)
			{
				return;
			}
		}
		string classificationName = AppLocalizationService.GetLang("Lang_Classification") + i;
		ClassificationItem classificationItem = new ClassificationItem();
		classificationItem.ClassificationName = classificationName;
		classificationItem.vmModule = vmModule;
		classificationItem.config = this;
		classificationItem.SetParentNode(this);
		ClassificationItems.Add(classificationItem);
		NotifySeq();
		UpdateOuputIO();
		SetClassification();
		SelectedClassificationItem = classificationItem;
	}

	public bool CheckGroupName(int seq)
	{
		foreach (ClassificationItem classificationItem in ClassificationItems)
		{
			string text = AppLocalizationService.GetLang("Lang_Classification") + seq;
			string text2 = "分类" + seq;
			string text3 = "Classification" + seq;
			if (classificationItem.ClassificationName == text || classificationItem.ClassificationName == text2 || classificationItem.ClassificationName == text3)
			{
				return false;
			}
		}
		return true;
	}

	private void PreviewMouseWheelCommandExcute(object obj)
	{
		if (hasRegistered)
		{
		}
		ScrollViewer sv;
		if ((sv = obj as ScrollViewer) != null)
		{
			sv.PreviewMouseWheel += (object sender, MouseWheelEventArgs e) =>
			{
				MouseWheelEventArgs e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
				{
					RoutedEvent = UIElement.MouseWheelEvent,
					Source = sender
				};
				sv.RaiseEvent(e2);
			};
			hasRegistered = true;
		}
	}

	public void UpdateOuputIO()
	{
		try
		{
			if (OwnerVm == null)
			{
				return;
			}
			VmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule;
			List<VmIO> list = vmModule.Inputs.ToList();
			List<VmIO> source = vmModule.Outputs.ToList();
			List<VmIO> list2 = vmModule.Outputs.Where((VmIO c) => c.Name != "ModuRunTime" && c.Name != "ModuStatus").ToList();
			List<Tuple<VmIO, string, VmIO>> list3 = new List<Tuple<VmIO, string, VmIO>>();
			List<VmIO> list4 = new List<VmIO>();
			foreach (VmIO item3 in list)
			{
				foreach (ClassificationItem classificationItem in ClassificationItems)
				{
					string name = classificationItem.ClassificationName + "_";
					if (item3.Name.Length > 2 && item3.Name[0] == '%' && item3.Name.LastOrDefault() == '%')
					{
						name += item3.Name.Substring(1, item3.Name.Length - 2);
					}
					VmIO vmIO = source.FirstOrDefault((VmIO c) => c.Name.Substring(1, c.Name.Length - 2) == name);
					list2.Remove(vmIO);
					if (vmIO == null)
					{
						FixName(ref name);
						DynamicVmIO item = new DynamicVmIO
						{
							Name = GetUniqueName(name),
							Root = OwnerVm,
							Parent = item3.Parent,
							IsResultShow = true,
							TypeName = item3.TypeName
						};
						list3.Add(new Tuple<VmIO, string, VmIO>(item, classificationItem.ClassificationName, item3));
					}
				}
			}
			foreach (ClassificationItem classificationItem2 in ClassificationItems)
			{
				string countName = classificationItem2.ClassificationName + "_Count";
				VmIO vmIO2 = source.FirstOrDefault((VmIO c) => c.Name == "%" + countName + "%");
				if (vmIO2 == null)
				{
					FixName(ref countName);
					DynamicVmIO item2 = new DynamicVmIO
					{
						Name = GetUniqueName(countName),
						Root = OwnerVm,
						Parent = OwnerVm,
						IsResultShow = true,
						TypeName = "int"
					};
					list4.Add(item2);
				}
				else
				{
					list2.Remove(vmIO2);
				}
			}
			foreach (VmIO item4 in list2)
			{
				item4.RemoveSelf();
			}
			int num = 0;
			foreach (ClassificationItem classItem in ClassificationItems)
			{
				foreach (VmIO ioItem in list)
				{
					Tuple<VmIO, string, VmIO> tuple = list3.FirstOrDefault((Tuple<VmIO, string, VmIO> c) => c.Item2 == classItem.ClassificationName && c.Item3 == ioItem);
					if (tuple != null)
					{
						vmModule.Outputs.Insert(num + 2, tuple.Item1);
						foreach (VmIO child in tuple.Item3.Children)
						{
							CopyIOAndUpdateName(child, tuple.Item2, tuple.Item1);
						}
					}
					num++;
				}
				string countName2 = classItem.ClassificationName + "_Count";
				VmIO vmIO3 = list4.FirstOrDefault((VmIO c) => c.Name == "%" + countName2 + "%");
				if (vmIO3 != null)
				{
					vmModule.Outputs.Insert(num + 2, vmIO3);
				}
				num++;
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void OutputIONameChange(VmIO inputio, string oldname, string newname)
	{
		VmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule;
		List<VmIO> list = vmModule.Inputs.ToList();
		List<VmIO> list2 = vmModule.Outputs.ToList();
		if (OwnerVm == null)
		{
			return;
		}
		foreach (ClassificationItem classificationItem in ClassificationItems)
		{
			string oldIOname = "%" + classificationItem.ClassificationName + "_";
			if (oldname.Length > 2 && oldname[0] == '%' && oldname.LastOrDefault() == '%')
			{
				oldIOname = oldIOname + oldname.Substring(1, oldname.Length - 2) + "%";
			}
			VmIO vmIO = list2.FirstOrDefault((VmIO c) => c.Name == oldIOname);
			if (vmIO == null)
			{
				break;
			}
			DynamicVmIO item = new DynamicVmIO
			{
				Name = GetUniqueName(newname),
				Root = OwnerVm,
				Parent = vmIO.Parent,
				IsResultShow = true,
				TypeName = vmIO.TypeName
			};
			int index = list2.IndexOf(vmIO);
			vmIO.RemoveSelf();
			vmModule.Outputs.Insert(index, item);
			foreach (VmIO child in inputio.Children)
			{
				CopyIOAndUpdateName(child, classificationItem.ClassificationName, inputio);
			}
		}
	}

	private void CopyIOAndUpdateName(VmIO sourceIO, string className, VmIO newParentIO)
	{
		if (OwnerVm == null)
		{
			return;
		}
		string name = className + "_" + sourceIO.Name.Replace("%", "");
		FixName(ref name);
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Name = GetUniqueName(name),
			Root = OwnerVm,
			Parent = newParentIO,
			IsResultShow = true,
			TypeName = sourceIO.TypeName
		};
		newParentIO.Children.Add(dynamicVmIO);
		foreach (VmIO child in sourceIO.Children)
		{
			CopyIOAndUpdateName(child, className, dynamicVmIO);
		}
	}

	private string GetUniqueName(string startName)
	{
		if (OwnerVm == null)
		{
			return "";
		}
		IEnumerable<string> source = from p in OwnerVm.Inputs.GetAllIO().Concat(OwnerVm.Outputs.GetAllIO())
			select p.Name;
		string text = startName;
		if (!source.Contains("%" + text + "%"))
		{
			return text;
		}
		int num = 0;
		while (true)
		{
			text = startName + num;
			if (!source.Contains("%" + text + "%"))
			{
				break;
			}
			num++;
		}
		return text;
	}

	public static void FixName(ref string name)
	{
		if (name.Length > 32)
		{
			name = name.Substring(0, 32);
		}
	}

	public void NotifySeq()
	{
		foreach (ClassificationItem classificationItem in ClassificationItems)
		{
			classificationItem.OnPropertyChanged("Seq");
			classificationItem.OnPropertyChanged("IsCanUp");
			classificationItem.OnPropertyChanged("IsCanDown");
		}
	}

	public void SetClassificationChange()
	{
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			string text = "";
			foreach (ClassificationItem classificationItem in ClassificationItems)
			{
				string text2 = classificationItem.ClassificationName + ":" + classificationItem.Seq + ";";
				text += text2;
			}
			((IUserBytesData)vmModule.Params)["ChangeClass"] = StringUtil.UTF8GetBytesPadZero(text);
			foreach (ClassificationItem classificationItem2 in ClassificationItems)
			{
				SetConditionGroup(classificationItem2);
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void SetClassification()
	{
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			string text = "";
			foreach (ClassificationItem classificationItem in ClassificationItems)
			{
				string text2 = classificationItem.ClassificationName + ":" + classificationItem.Seq + ";";
				text += text2;
			}
			((IUserBytesData)vmModule.Params)["ClassText"] = StringUtil.UTF8GetBytesPadZero(text);
			foreach (ClassificationItem classificationItem2 in ClassificationItems)
			{
				SetConditionGroup(classificationItem2);
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void SetConditionGroup(ClassificationItem classItem)
	{
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			string text = classItem.ClassificationName + ";";
			foreach (ConditionGroupItem conditionGroupItem in classItem.ConditionGroupItems)
			{
				string text2 = conditionGroupItem.ConditionGroupName + ":" + conditionGroupItem.ConditionGroupID + ";";
				text += text2;
			}
			((IUserBytesData)vmModule.Params)["GroupText"] = StringUtil.UTF8GetBytesPadZero(text);
			SetConditionItem(classItem);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void SetConditionItem(ClassificationItem classItem)
	{
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			string text = classItem.ClassificationName + ";";
			foreach (ConditionGroupItem conditionGroupItem in classItem.ConditionGroupItems)
			{
				if (conditionGroupItem == null)
				{
					continue;
				}
				foreach (ConditionItem conditionItem in conditionGroupItem.ConditionItems)
				{
					if (conditionItem != null)
					{
						string text2 = "0";
						string text3 = "0";
						if (conditionItem.TypeName == "int")
						{
							Integer integer = conditionItem.IntegerBettween.IntegersA[0] as Integer;
							Integer integer2 = conditionItem.IntegerBettween.IntegersA[1] as Integer;
							text2 = (integer.IsSubA ? integer.SubData.CurValue : integer.CurValue.ToString());
							text3 = (integer2.IsSubA ? integer2.SubData.CurValue : integer2.CurValue.ToString());
						}
						else
						{
							Float obj = conditionItem.ClassificationBettween.Floats[0] as Float;
							Float obj2 = conditionItem.ClassificationBettween.Floats[1] as Float;
							text2 = (obj.IsSubA ? obj.SubData.CurValue : obj.CurValue.ToString());
							text3 = (obj2.IsSubA ? obj2.SubData.CurValue : obj2.CurValue.ToString());
						}
						string text4 = $"%{conditionItem.ClassificationSelecter?.Value}%:{conditionItem.TypeName}:{text2}:{text3}:{conditionItem.ConditionGroupID}:{conditionItem.ConditionID};";
						text += text4;
					}
				}
			}
			((IUserBytesData)vmModule.Params)["ConditionText"] = StringUtil.UTF8GetBytesPadZero(text);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void SetIsIncompatible(bool bo)
	{
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				((IUserBytesData)vmModule.Params)["IsIncompatible"] = StringUtil.UTF8GetBytesPadZero((bo ? 1 : 0).ToString());
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void SetInputText()
	{
		try
		{
			string text = "";
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			DataClassificationParam dataClassificationParam = ((vmModule.Params.Data as AlgorithmTabRoot).Tabs[0] as Tab).Collection[0] as DataClassificationParam;
			foreach (object item in dataClassificationParam.ItemsA)
			{
				VmIODecorate vmIODecorate = item as VmIODecorate;
				string text2 = $"{vmIODecorate.Name}:{vmIODecorate.ValueType}:{vmIODecorate.ClassificationSelecter?.CurValue};";
				text += text2;
			}
			((IUserBytesData)vmModule.Params)["InputText"] = StringUtil.UTF8GetBytesPadZero(text);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}
}
