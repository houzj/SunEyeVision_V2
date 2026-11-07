using System;
using System.Collections;
using System.ComponentModel;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VMControls.Interface;

namespace FeatureMatchCommonControl;

public class ModelItem : INotifyPropertyChanged
{
	protected NameValueItem OwnerModuleParam;

	protected IVMRun OwnerModule;

	public FeaturepatMatchDialogDataBase modelData = new FeaturepatMatchDialogDataBase();

	private int _Id = 1;

	private bool isCorrect = true;

	public bool isSelected = true;

	public string _Name = string.Empty;

	public const string ModelItemHeadStr = "SolModelFile_";

	private const string basicData = "_BasicData";

	private const string curImageData = "_CurImageData";

	private const string otherMask = "_OtherMask";

	private const string otherMask2 = "_OtherMask2";

	private const string pointsData = "_PointsData";

	private DelegateCommand<object> _ModelNameTextChangedCommand = null;

	private DelegateCommand<object> _ModelNameGotFocusCommand = null;

	public int Id
	{
		get
		{
			return _Id;
		}
		set
		{
			if (_Id != value)
			{
				_Id = value;
				NotifyPropertyChanged("Id");
				NotifyPropertyChanged("Index");
			}
		}
	}

	public int Index => _Id - 1;

	public bool IsCorrect
	{
		get
		{
			return isCorrect;
		}
		set
		{
			if (isCorrect != value)
			{
				isCorrect = value;
				NotifyPropertyChanged("IsCorrect");
			}
		}
	}

	public bool IsSelected
	{
		get
		{
			return isSelected;
		}
		set
		{
			if (isSelected != value)
			{
				isSelected = value;
				NotifyPropertyChanged("IsSelected");
			}
		}
	}

	public string Name
	{
		get
		{
			return _Name;
		}
		set
		{
			if (_Name != value)
			{
				if (!string.IsNullOrEmpty(value))
				{
					_Name = value;
				}
				NotifyPropertyChanged("Name");
			}
		}
	}

	public string BasicDataName => string.Format("{0}{1}{2}", "SolModelFile_", Index, "_BasicData");

	public string ImageDataName => string.Format("{0}{1}{2}", "SolModelFile_", Index, "_CurImageData");

	public string OtherMaskName => string.Format("{0}{1}{2}", "SolModelFile_", Index, "_OtherMask");

	public string OtherMask2Name => string.Format("{0}{1}{2}", "SolModelFile_", Index, "_OtherMask2");

	public string PointsDataName => string.Format("{0}{1}{2}", "SolModelFile_", Index, "_PointsData");

	public virtual ICommand ModelNameTextChangedCommand
	{
		get
		{
			if (_ModelNameTextChangedCommand == null)
			{
				_ModelNameTextChangedCommand = new DelegateCommand<object>((Action<object>)ModelNameTextChangedCommandExcute);
			}
			return (ICommand)_ModelNameTextChangedCommand;
		}
	}

	public ICommand ModelNameGotFocusCommand
	{
		get
		{
			if (_ModelNameGotFocusCommand == null)
			{
				_ModelNameGotFocusCommand = new DelegateCommand<object>((Action<object>)ModelNameGotFocusCommandExcute);
			}
			return (ICommand)_ModelNameGotFocusCommand;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public ModelItem(NameValueItem moduleParam, IVMRun module)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		OwnerModuleParam = moduleParam;
		OwnerModule = module;
	}

	public ModelItem(NameValueItem moduleParam, IVMRun module, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		OwnerModuleParam = moduleParam;
		OwnerModule = module;
		Id = index + 1;
	}

	public void DeleteUserData(NameValueItem nameValueItem)
	{
		string name = "SolModelFile_" + Index;
		DeleteUserData(nameValueItem, name);
	}

	private void DeleteUserData(NameValueItem nameValueItem, int index)
	{
		string name = "SolModelFile_" + index;
		DeleteUserData(nameValueItem, name);
	}

	private void DeleteUserData(NameValueItem nameValueItem, string name)
	{
		string key = name + "_BasicData";
		nameValueItem.InnerSetByteUserDataValue(key, null);
		string key2 = name + "_CurImageData";
		nameValueItem.InnerSetByteUserDataValue(key2, null);
		string key3 = name + "_OtherMask";
		nameValueItem.InnerSetByteUserDataValue(key3, null);
		string key4 = name + "_OtherMask2";
		nameValueItem.InnerSetByteUserDataValue(key4, null);
		string key5 = name + "_PointsData";
		nameValueItem.InnerSetByteUserDataValue(key5, null);
	}

	private void ExChangeUserData(NameValueItem nameValueItem, int indexA, int indexB)
	{
		string text = "SolModelFile_" + indexA;
		string text2 = "SolModelFile_" + indexB;
		if (OwnerModule is IVmModule)
		{
			string key = text + "_BasicData";
			string key2 = text2 + "_BasicData";
			byte[] value = nameValueItem.InnerGetByteUserDataValue(key2);
			nameValueItem.InnerSetByteUserDataValue(key2, nameValueItem.InnerGetByteUserDataValue(key));
			nameValueItem.InnerSetByteUserDataValue(key, value);
			string key3 = text + "_CurImageData";
			string key4 = text2 + "_CurImageData";
			value = nameValueItem.InnerGetByteUserDataValue(key4);
			nameValueItem.InnerSetByteUserDataValue(key4, nameValueItem.InnerGetByteUserDataValue(key3));
			nameValueItem.InnerSetByteUserDataValue(key3, value);
			string key5 = text + "_OtherMask";
			string key6 = text2 + "_OtherMask";
			value = nameValueItem.InnerGetByteUserDataValue(key6);
			nameValueItem.InnerSetByteUserDataValue(key6, nameValueItem.InnerGetByteUserDataValue(key5));
			nameValueItem.InnerSetByteUserDataValue(key5, value);
			string key7 = text + "_OtherMask2";
			string key8 = text2 + "_OtherMask2";
			value = nameValueItem.InnerGetByteUserDataValue(key8);
			nameValueItem.InnerSetByteUserDataValue(key8, nameValueItem.InnerGetByteUserDataValue(key7));
			nameValueItem.InnerSetByteUserDataValue(key7, value);
			string key9 = text + "_PointsData";
			string key10 = text2 + "_PointsData";
			value = nameValueItem.InnerGetByteUserDataValue(key10);
			nameValueItem.InnerSetByteUserDataValue(key10, nameValueItem.InnerGetByteUserDataValue(key9));
			nameValueItem.InnerSetByteUserDataValue(key9, value);
		}
	}

	private void ReplaceUserData(NameValueItem nameValueItem, int fromIndex, int toIndex)
	{
		string text = "SolModelFile_" + fromIndex;
		string text2 = "SolModelFile_" + toIndex;
		if (OwnerModule is IVmModule)
		{
			string key = text + "_BasicData";
			string key2 = text2 + "_BasicData";
			nameValueItem.InnerSetByteUserDataValue(key2, nameValueItem.InnerGetByteUserDataValue(key));
			string key3 = text + "_CurImageData";
			string key4 = text2 + "_CurImageData";
			nameValueItem.InnerSetByteUserDataValue(key4, nameValueItem.InnerGetByteUserDataValue(key3));
			string key5 = text + "_OtherMask";
			string key6 = text2 + "_OtherMask";
			nameValueItem.InnerSetByteUserDataValue(key6, nameValueItem.InnerGetByteUserDataValue(key5));
			string key7 = text + "_OtherMask2";
			string key8 = text2 + "_OtherMask2";
			nameValueItem.InnerSetByteUserDataValue(key8, nameValueItem.InnerGetByteUserDataValue(key7));
			string key9 = text + "_PointsData";
			string key10 = text2 + "_PointsData";
			nameValueItem.InnerSetByteUserDataValue(key10, nameValueItem.InnerGetByteUserDataValue(key9));
		}
	}

	internal virtual void DelCmdExecute(IList obj)
	{
		LogHelper.objLog.Info("Delete model start.");
		if (GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			LogHelper.objLog.Info("Process is running, abondon.");
			return;
		}
		if (obj != null)
		{
			if (OwnerModuleParam == null)
			{
				return;
			}
			try
			{
				string text = Index.ToString();
				LogHelper.objLog.Info($"Want to delete model, index={text}.");
				OwnerModuleParam.InnerSetByteParamValue("DeleteCandidateByIndex", Encoding.UTF8.GetBytes(text));
			}
			catch (Exception ex)
			{
				LogHelper.objLog.Error($"Delete model fail:{ex.Message}.");
				return;
			}
			obj.Remove(this);
			for (int i = Index; i < obj.Count; i++)
			{
				ModelItem modelItem = obj[i] as ModelItem;
				modelItem.Id = i + 1;
			}
			for (int j = Index; j < obj.Count; j++)
			{
				ReplaceUserData(OwnerModuleParam, j + 1, j);
			}
			DeleteUserData(OwnerModuleParam, obj.Count);
		}
		LogHelper.objLog.Info("Delete model end.");
	}

	internal virtual void UpCmdExecute(IList obj)
	{
		LogHelper.objLog.Info("Move up model start.");
		if (GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			LogHelper.objLog.Info("Process is running, abondon.");
			return;
		}
		if (obj != null && Index > 0)
		{
			if (modelData == null || OwnerModuleParam == null)
			{
				return;
			}
			try
			{
				string text = Index.ToString();
				LogHelper.objLog.Info($"Want to move up model, index={text}.");
				OwnerModuleParam.InnerSetByteParamValue("UpCandidate", Encoding.UTF8.GetBytes(text));
			}
			catch (Exception ex)
			{
				LogHelper.objLog.Error($"Move up error:{ex.Message}");
				return;
			}
			ModelItem modelItem = obj[Index - 1] as ModelItem;
			obj.Remove(this);
			obj.Insert(Index - 1, this);
			ExChangeUserData(OwnerModuleParam, Index, Index - 1);
			modelItem.Id = Id;
			Id--;
			if (!string.IsNullOrEmpty(modelItem.modelData.CurImageDataKeyStr))
			{
				modelItem.modelData.CurImageDataKeyStr = modelItem.ImageDataName;
			}
			if (!string.IsNullOrEmpty(modelData.CurImageDataKeyStr))
			{
				modelData.CurImageDataKeyStr = ImageDataName;
			}
		}
		LogHelper.objLog.Info("Move up model end.");
	}

	internal virtual void DownCmdExecute(IList obj)
	{
		LogHelper.objLog.Info("Move down model start.");
		if (GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			LogHelper.objLog.Info("Process is running, abondon.");
			return;
		}
		if (obj != null)
		{
			int id = Id;
			if (Index < obj.Count - 1)
			{
				if (modelData == null || OwnerModuleParam == null)
				{
					return;
				}
				try
				{
					string text = Index.ToString();
					LogHelper.objLog.Info($"Want to move down model, index={text}.");
					OwnerModuleParam.InnerSetByteParamValue("DownCandidate", Encoding.UTF8.GetBytes(text));
				}
				catch
				{
					return;
				}
				ModelItem modelItem = obj[Index + 1] as ModelItem;
				obj.Remove(this);
				obj.Insert(Index + 1, this);
				ExChangeUserData(OwnerModuleParam, Index, Index + 1);
				modelItem.Id = Id;
				Id++;
				if (!string.IsNullOrEmpty(modelItem.modelData.CurImageDataKeyStr))
				{
					modelItem.modelData.CurImageDataKeyStr = modelItem.ImageDataName;
				}
				if (!string.IsNullOrEmpty(modelData.CurImageDataKeyStr))
				{
					modelData.CurImageDataKeyStr = ImageDataName;
				}
			}
		}
		LogHelper.objLog.Info("Move down model end.");
	}

	internal virtual void SelectedCmdExecute(IList obj)
	{
		LogHelper.objLog.Info("Selected model start.");
		if (GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			LogHelper.objLog.Info("Process is running, abondon.");
			return;
		}
		if (obj != null)
		{
			if (modelData == null || OwnerModuleParam == null)
			{
				return;
			}
			try
			{
				string arg = (IsSelected ? "1" : "0");
				string text = $"{Index},{arg}";
				LogHelper.objLog.Info($"Want to selected model, selected={text}.");
				OwnerModuleParam.InnerSetStringParamValue("SelectedModule", text);
			}
			catch
			{
				LogHelper.objLog.Error("Selected model error.");
				return;
			}
		}
		LogHelper.objLog.Info("Selected model end.");
	}

	protected virtual void ModelNameTextChangedCommandExcute(object sender)
	{
		if (!(sender is TextBox))
		{
			return;
		}
		try
		{
			string text = (sender as TextBox).Text;
			if (text.Length > 32)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LengthLimit32"));
				return;
			}
			Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: true);
			if (modelData == null || OwnerModuleParam == null)
			{
				return;
			}
			try
			{
				LogHelper.objLog.Info($"Want to change model name,index={Id - 1}.");
				OwnerModuleParam.InnerSetStringParamValue("ChooseSpareModelIndex", (Id - 1).ToString());
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoEdit"), MessageIcons.Error);
				LogHelper.objLog.Error($"Choose model fail,errorcode={ex.Message}.");
				return;
			}
			try
			{
				if (string.IsNullOrWhiteSpace(text))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_Featur_ModelNameNotNull"), MessageIcons.Error);
					return;
				}
				LogHelper.objLog.Info($"Want to change model name, target name={text}.");
				OwnerModuleParam.InnerSetByteParamValue("ChangeSpareModelName", Encoding.UTF8.GetBytes(text));
			}
			catch (Exception ex2)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoEdit"), MessageIcons.Error);
				LogHelper.objLog.Error($"Set model name fail,errorcode={ex2.Message}.");
			}
		}
		finally
		{
			Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: false);
		}
	}

	protected virtual void ModelNameGotFocusCommandExcute(object sender)
	{
		if (sender is ListView)
		{
			ListView listView = sender as ListView;
			if (listView.Items.Contains(this) && listView.SelectedItem != this)
			{
				listView.SelectedItem = this;
			}
		}
	}

	protected virtual bool GetIsBusy()
	{
		if (OwnerModule == null)
		{
			return false;
		}
		return OwnerModule.IsRunning;
	}
}
