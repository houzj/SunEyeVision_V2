using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.BaseData;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using HikPcUI.ImageView;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.ModuleControl;
using VMControls.Interface;

namespace FeatureMatchCommonControl;

public class FeaturepatMatchConfigPanalDataDecorate : NotifyPropertyChangedBase, IDecorate
{
	private FeaturepatMatchConfigPanalDataBase _DecorateSource;

	private ObservableCollection<object> _ModelList;

	private ModelItem _SelectedModel = null;

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	private ImageSource _ImageSource = null;

	private DelegateCommand _LoadModuleCommand = null;

	private DelegateCommand _AddModelCommand = null;

	private ICommand _ChangeModelCommand;

	private ICommand _DeleteAllModelsCommand;

	private ICommand _UpCommand = null;

	private ICommand _DownCommand = null;

	private ICommand _DelCommand = null;

	private ICommand _selectedCommand = null;

	public FeaturepatMatchConfigPanalDataBase DecorateSource
	{
		get
		{
			return _DecorateSource;
		}
		set
		{
			if (value != _DecorateSource)
			{
				_DecorateSource = value;
			}
		}
	}

	object IDecorate.DecorateSource
	{
		get
		{
			return DecorateSource;
		}
		set
		{
			DecorateSource = value as FeaturepatMatchConfigPanalDataBase;
		}
	}

	public ObservableCollection<object> ModelList
	{
		get
		{
			if (_ModelList == null)
			{
				_ModelList = new ObservableCollection<object>();
				GetModuleList(_ModelList);
				if (_ModelList.Count > 0)
				{
					SelectedModel = _ModelList.Last() as ModelItem;
				}
				((NotifyPropertyChangedBase)this).OnPropertyChanged("HasUnCorrect");
			}
			return _ModelList;
		}
	}

	public bool HasUnCorrect
	{
		get
		{
			if (_ModelList == null)
			{
				return false;
			}
			return _ModelList.OfType<ModelItem>().Any((ModelItem p) => !p.IsCorrect);
		}
	}

	public ModelItem SelectedModel
	{
		get
		{
			return _SelectedModel;
		}
		set
		{
			if (_SelectedModel != value)
			{
				_SelectedModel = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("SelectedModel");
				_ImageSource = null;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("ImageSource");
			}
		}
	}

	public ImageSource ImageSource
	{
		get
		{
			if (_ImageSource == null)
			{
				if (_SelectedModel == null || !_SelectedModel.IsCorrect)
				{
					return null;
				}
				try
				{
					ImageData modelImageData = _SelectedModel.modelData.ModelImageData;
					if (modelImageData == null)
					{
						FeatureMatchHelper.RefreshModelImageData(SelectedModel.modelData, false);
						modelImageData = SelectedModel.modelData.ModelImageData;
					}
					if (modelImageData != null)
					{
						_ImageSource = b2SConvertor.BuiltImageSource(modelImageData.ImageBuffer, modelImageData.Width, modelImageData.Height, modelImageData.PixelFormat);
						return _ImageSource;
					}
					throw new Exception("Can't get Image");
				}
				catch (Exception ex)
				{
					LogHelper.objLog.Error(ex.Message);
					return new BitmapImage(new Uri("/Apps.XmlUI;component/Resources/特征模板_image.png", UriKind.RelativeOrAbsolute));
				}
			}
			return _ImageSource;
		}
	}

	protected virtual int MaxModelNum { get; } = 36;

	public virtual ICommand LoadModuleCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_LoadModuleCommand == null)
			{
				_LoadModuleCommand = new DelegateCommand((Action)LoadModuleCommandExcute);
			}
			return (ICommand)_LoadModuleCommand;
		}
	}

	public virtual ICommand AddModelCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_AddModelCommand == null)
			{
				_AddModelCommand = new DelegateCommand((Action)AddModelCommandExecute);
			}
			return (ICommand)_AddModelCommand;
		}
	}

	public ICommand ChangeModelCommand => _ChangeModelCommand ?? (_ChangeModelCommand = (ICommand)new DelegateCommand<NonOfParamHandler>((Action<NonOfParamHandler>)ChangeModelCommandExecute));

	public ICommand DeleteAllModelsCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _DeleteAllModelsCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)DeleteAllModelsCommandExecute);
				ICommand command2 = (ICommand)val;
				_DeleteAllModelsCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public virtual ICommand UpCommand
	{
		get
		{
			if (_UpCommand == null)
			{
				_UpCommand = (ICommand)new DelegateCommand<object>((Action<object>)UpCmdExecute);
			}
			return _UpCommand;
		}
	}

	public virtual ICommand DownCommand
	{
		get
		{
			if (_DownCommand == null)
			{
				_DownCommand = (ICommand)new DelegateCommand<object>((Action<object>)DownCmdExecute);
			}
			return _DownCommand;
		}
	}

	public virtual ICommand DelCommand
	{
		get
		{
			if (_DelCommand == null)
			{
				_DelCommand = (ICommand)new DelegateCommand<object>((Action<object>)DelCmdExecute);
			}
			return _DelCommand;
		}
	}

	public virtual ICommand SelectedCommand
	{
		get
		{
			if (_selectedCommand == null)
			{
				_selectedCommand = (ICommand)new DelegateCommand<object>((Action<object>)SelectedCmdExecute);
			}
			return _selectedCommand;
		}
	}

	private void GetModuleList(IList ls)
	{
		int nIndex = 0;
		string empty;
		while (true)
		{
			empty = string.Empty;
			try
			{
				empty = _DecorateSource.InnerGetStringParamValue($"CandidateName{nIndex}");
			}
			catch
			{
				LogHelper.Info(string.Format("Get userdata:{0} fail,get end.", "CandidateName" + nIndex));
				return;
			}
			if (empty == null || empty == string.Empty)
			{
				break;
			}
			string text = "SolModelFile_" + nIndex;
			string key = text + "_BasicData";
			try
			{
				LogHelper.Info("Get basic data start.");
				byte[] array = _DecorateSource.InnerGetByteUserDataValue(key);
				LogHelper.Info("Get basic data end.");
				ModelItem modelItem = _DecorateSource.CreateItemData(empty, nIndex);
				if (modelItem == null)
				{
					LogHelper.Error("Model data empty.");
					return;
				}
				try
				{
					string text2 = _DecorateSource.InnerGetStringParamValue($"SelectedModule{nIndex}");
					modelItem.IsSelected = text2 == "1";
				}
				catch (Exception)
				{
					LogHelper.Info(string.Format("Get userdata IsSelected:{0} fail,get end.", "IsSelected" + nIndex));
				}
				if (array == null)
				{
					modelItem.IsCorrect = false;
					AddToLs(ls, modelItem, ref nIndex);
				}
				else if (!_DecorateSource.GetModelUIDataByModelName(array, modelItem, text))
				{
					modelItem.IsCorrect = false;
					AddToLs(ls, modelItem, ref nIndex);
				}
				else
				{
					AddToLs(ls, modelItem, ref nIndex);
				}
			}
			catch
			{
			}
		}
		LogHelper.Info($"ModelName:{empty} error,get end.");
	}

	private void AddToLs(IList ls, ModelItem itemData, ref int nIndex)
	{
		ls.Add(itemData);
		nIndex++;
	}

	private void EditModel(IList list)
	{
		_DecorateSource.CreateModel(isNew: false, list, _SelectedModel);
	}

	private void AddModel(IList list)
	{
		ModelItem modelItem = _DecorateSource.CreateModel(isNew: true, list, null);
		if (modelItem != null)
		{
			SelectedModel = modelItem;
			modelItem.modelData.ModelData = null;
			((NotifyPropertyChangedBase)this).OnPropertyChanged("HasUnCorrect");
		}
	}

	protected void LoadModuleCommandExcute()
	{
		if (_DecorateSource.GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			return;
		}
		if (_ModelList.Count >= MaxModelNum)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_FetNewErrtoSize"), MessageIcons.Error);
			return;
		}
		_DecorateSource.LoadModel(_ModelList);
		SelectedModel = _ModelList.OfType<ModelItem>().LastOrDefault();
	}

	protected virtual void AddModelCommandExecute()
	{
		if (_DecorateSource.GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
		}
		else if (_ModelList.Count >= MaxModelNum)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_FetNewErrtoSize"), MessageIcons.Error);
		}
		else
		{
			AddModel(_ModelList);
		}
	}

	private void ChangeModelCommandExecute(NonOfParamHandler openWinMethod)
	{
		if (_DecorateSource.GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
		}
		else if (SelectedModel != null)
		{
			_ImageSource = null;
			if (openWinMethod != null)
			{
				openWinMethod.Invoke();
			}
			EditModel(_ModelList);
			((NotifyPropertyChangedBase)this).OnPropertyChanged("ImageSource");
		}
	}

	private void DeleteAllModelsCommandExecute()
	{
		if (_DecorateSource.GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
		}
		else
		{
			DeleteAllModels();
		}
	}

	internal void DeleteAllModels(bool isShowWarn = true)
	{
		if (isShowWarn && HikMessageBox.Show(AppLocalizationService.GetLang("Lang_InfoTips"), AppLocalizationService.GetLang("Lang_ConfirmDeleteAll"), MessageButtons.YesOrNo, MessageIcons.Question) != DialogResult.Yes)
		{
			LogHelper.objLog.Info("User canceled.");
			return;
		}
		try
		{
			_DecorateSource.InnerSetByteParamValue("ClearCandidate", new byte[1]);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"Delete all models fail:{ex.Message}.");
			return;
		}
		if (_ModelList == null)
		{
			ObservableCollection<object> modelList = ModelList;
		}
		foreach (ModelItem item in _ModelList.OfType<ModelItem>())
		{
			item.DeleteUserData(_DecorateSource);
		}
		_ModelList.Clear();
		((NotifyPropertyChangedBase)this).OnPropertyChanged("HasUnCorrect");
	}

	protected virtual void UpCmdExecute(object data)
	{
		ModelItem modelItem = data as ModelItem;
		modelItem.UpCmdExecute(_ModelList);
		SelectedModel = modelItem;
	}

	protected virtual void DownCmdExecute(object data)
	{
		ModelItem modelItem = data as ModelItem;
		modelItem.DownCmdExecute(_ModelList);
		SelectedModel = modelItem;
	}

	protected virtual void DelCmdExecute(object data)
	{
		ModelItem modelItem = data as ModelItem;
		modelItem.DelCmdExecute(_ModelList);
		((NotifyPropertyChangedBase)this).OnPropertyChanged("HasUnCorrect");
		SelectedModel = _ModelList.FirstOrDefault() as ModelItem;
	}

	protected virtual void SelectedCmdExecute(object data)
	{
		ModelItem modelItem = data as ModelItem;
		modelItem.SelectedCmdExecute(_ModelList);
		SelectedModel = modelItem;
	}
}
