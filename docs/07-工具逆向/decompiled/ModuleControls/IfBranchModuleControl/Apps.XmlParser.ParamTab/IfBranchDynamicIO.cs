using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using Apps.Log;
using Mvvm.Commands;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class IfBranchDynamicIO : DynamicIO
{
	private FloatBettween _floatbetween;

	private Enumeration _state;

	public int _runModuleId = -1;

	private IList _EnumEntrys;

	private DelegateCommand _SelectedChangedCommand;

	public List<string> SelectValueBasicTypes => new List<string> { "int", "float" };

	public FloatBettween Floatbetween
	{
		get
		{
			if (_floatbetween == null)
			{
				_floatbetween = new FloatBettween();
				_floatbetween.SetParentNode(this);
				_floatbetween.SubscribeVisibleA = false;
				if (_floatbetween.Floats == null)
				{
					Float obj = null;
					Float obj2 = null;
					if (NameA.Contains("int"))
					{
						_floatbetween.MaxValueA = 999999999.0;
						_floatbetween.MinValueA = -999999999.0;
						obj = new Float();
						obj.MaxValueA = 999999999.0;
						obj.MinValueA = -999999999.0;
						obj.CurValueA = 1.0;
						obj2 = new Float();
						obj2.MaxValueA = 999999999.0;
						obj2.MinValueA = -999999999.0;
						obj2.CurValueA = 1000.0;
					}
					else if (NameA.Contains("float"))
					{
						_floatbetween.MaxValueA = 100000.0;
						_floatbetween.MinValueA = -100000.0;
						obj = new Float();
						obj.MaxValueA = 100000.0;
						obj.MinValueA = -100000.0;
						obj.CurValueA = 1.0;
						obj2 = new Float();
						obj2.MaxValueA = 100000.0;
						obj2.MinValueA = -100000.0;
						obj2.CurValueA = 1000.0;
					}
					obj.DecimalDigitsA = (GetParentNode() as IfBranchModuleDynamicIO).DecimalDigits;
					obj2.DecimalDigitsA = (GetParentNode() as IfBranchModuleDynamicIO).DecimalDigits;
					_floatbetween.DecimalDigitsA = (GetParentNode() as IfBranchModuleDynamicIO).DecimalDigits;
					obj.SetParentNode(_floatbetween);
					obj2.SetParentNode(_floatbetween);
					obj.OnCurValueChanged += CurValueChanged;
					obj2.OnCurValueChanged += CurValueChanged;
					_floatbetween.FloatsA = new List<object>();
					_floatbetween.FloatsA.Add(obj);
					_floatbetween.FloatsA.Add(obj2);
				}
			}
			return _floatbetween;
		}
	}

	public Enumeration State
	{
		get
		{
			if (_state == null)
			{
				_state = new Enumeration
				{
					AccessMode = "O",
					CurValue = 1,
					DefaultValue = 1
				};
				_state.EnumEntrysA = new ObservableCollection<object>
				{
					new EnumEntry
					{
						Description = "OK",
						DisplayName = "OK",
						Value = 1
					},
					new EnumEntry
					{
						Description = "NG",
						DisplayName = "NG",
						Value = 2
					}
				};
				_state.OnCurValueChanged += CurValueChanged;
			}
			return _state;
		}
	}

	public int RunModuleId
	{
		get
		{
			return _runModuleId;
		}
		set
		{
			if (_runModuleId != value)
			{
				_runModuleId = value;
				SetParamUnit();
				OnPropertyChanged("RunModuleId");
			}
		}
	}

	public virtual IList EnumEntrysA
	{
		get
		{
			_EnumEntrys = GetEnumEntrysList();
			return _EnumEntrys;
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand((Action)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
		}
	}

	private IList GetEnumEntrysList()
	{
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		StringBuilder stringBuilder = new StringBuilder();
		string strValue = string.Empty;
		try
		{
			ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(IoDataSource.Root.ID, "IdList", ref strValue);
			stringBuilder.Append(strValue);
		}
		catch (Exception ex)
		{
			LogHelper.Error("Get IdList Error:" + ex.Message);
			throw ex;
		}
		string[] array = stringBuilder.ToString().Split(';');
		if (array.Length != 0)
		{
			for (int i = 0; i < array.Length - 1; i++)
			{
				EnumEntry enumEntry = new EnumEntry();
				enumEntry.DisplayNameA = array[i];
				enumEntry.Value = int.Parse(array[i]);
				observableCollection.Add(enumEntry);
			}
		}
		return observableCollection;
	}

	private void SelectedChangedCmdExecute()
	{
	}

	private void CurValueChanged(object sender, EventArgs e)
	{
		SetParamUnit();
	}

	public override int SetParamUnit()
	{
		if ((GetParentNode() as IfBranchModuleDynamicIO).IsAllowSet)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (IfBranchDynamicIO item in (GetParentNode() as DynamicIOContainer<IfBranchDynamicIO>).ItemsA)
			{
				if (item.CustomVisibleA)
				{
					stringBuilder.Append(item.NameA);
					stringBuilder.Append(":");
					stringBuilder.Append((item.Floatbetween.FloatsA[0] as NameValueItem).Value);
					stringBuilder.Append(":");
					stringBuilder.Append((item.Floatbetween.FloatsA[1] as NameValueItem).Value);
					stringBuilder.Append(":");
					stringBuilder.Append(item.State.CurValue);
					stringBuilder.Append(":");
					stringBuilder.Append(item.RunModuleId);
					stringBuilder.Append(";");
				}
			}
			if (string.IsNullOrEmpty(stringBuilder.ToString()))
			{
				stringBuilder = stringBuilder.Append("0");
			}
			try
			{
				((IUserBytesData)IoDataSource.Root.Params)["IfBranchText"] = StringUtil.UTF8GetBytesPadZero(stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				LogHelper.Error("设置IfBranchText失败：" + ex.Message);
				return -1;
			}
		}
		return 0;
	}
}
