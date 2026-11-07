using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.WPF;

namespace FewShotCommonControl;

public partial class FewShotPanel : System.Windows.Controls.Control, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(FewShotPanel), new PropertyMetadata(null));

	public static readonly DependencyProperty HasModelProperty = DependencyProperty.Register("HasModel", typeof(bool), typeof(FewShotPanel), new PropertyMetadata(false));

	private Image defaultImage = null;

	private Grid viewGrid = null;

	private string className = null;

	public IVmModule OwnerModule { get; set; }

	public object ParentNode
	{
		get
		{
			return GetValue(ParentNodeProperty);
		}
		set
		{
			SetValue(ParentNodeProperty, value);
		}
	}

	public bool HasModel
	{
		get
		{
			return (bool)GetValue(HasModelProperty);
		}
		set
		{
			SetValue(HasModelProperty, value);
		}
	}

	public double RegisterButtonWidth => (AppLocalizationService.GetLang("Language") == "English") ? 135 : 100;

	public FewShotPanel()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		defaultImage = Template.FindName("defaultImage", this) as Image;
		viewGrid = Template.FindName("viewGrid", this) as Grid;
		HasModel = false;
		if (ParentNode is NameValueItem nameValueItem)
		{
			OwnerModule = (nameValueItem.GetTopBaseNode() as AlgorithmTabRoot)?.Owner as IVmModule;
			GetModel();
		}
	}

	private string GetModel()
	{
		if (OwnerModule != null)
		{
			string result = "";
			try
			{
				string text = ((IUserStringData)OwnerModule.Params)["GetCurClassNum"];
				if (text == "1" || text == "2")
				{
					FewShotData fewShotData = new FewShotData();
					fewShotData.SetOwnerModule(OwnerModule);
					fewShotData.UpdateThumbnailList(needPopTip: false);
					RefreshThumbnail(fewShotData);
					className = fewShotData.ClassName;
					HasModel = fewShotData.ThumbnailList.Count > 0;
				}
			}
			catch (VmException ex)
			{
				result = ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			}
			catch (Exception ex2)
			{
				int result2 = 0;
				string text2 = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex2.Message);
				result = text2;
			}
			return result;
		}
		return null;
	}

	private void RefreshThumbnail(FewShotData data)
	{
		viewGrid.Children.Clear();
		if (data != null && data.ThumbnailList.Count > 0)
		{
			defaultImage.Visibility = Visibility.Collapsed;
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < data.ThumbnailList.Count; i++)
			{
				ImageView imageView = new ImageView();
				imageView.Width = 70.0;
				imageView.Height = 70.0;
				imageView.DisplayImageWidth = 70;
				imageView.DisplayImageHeight = 70;
				imageView.ImageDataSource = data.ThumbnailList[i].ImageData;
				viewGrid.Children.Add(imageView);
				Grid.SetRow(imageView, num);
				Grid.SetColumn(imageView, num2);
				num2++;
				if (num2 == 5)
				{
					num2 = 0;
					num++;
				}
			}
		}
		else
		{
			defaultImage.Visibility = Visibility.Visible;
		}
	}

	private void btnAddTemplate_Click(object sender, RoutedEventArgs e)
	{
		FewShotModelDialog fewShotModelDialog = new FewShotModelDialog();
		FewShotData fewShotData = new FewShotData();
		fewShotData.HasModel = HasModel;
		fewShotData.Init(ParentNode);
		fewShotModelDialog.DataContext = fewShotData;
		System.Windows.Data.Binding binding = new System.Windows.Data.Binding("IsShowMask")
		{
			Source = fewShotData
		};
		fewShotModelDialog.SetBinding(FewShotModelDialog.IsShowMaskProperty, binding);
		System.Windows.Data.Binding binding2 = new System.Windows.Data.Binding("IsProcessEnd")
		{
			Source = fewShotData
		};
		fewShotModelDialog.SetBinding(FewShotModelDialog.IsNeedCloseProperty, binding2);
		System.Windows.Data.Binding binding3 = new System.Windows.Data.Binding("ShowMaskTip")
		{
			Source = fewShotData
		};
		fewShotModelDialog.SetBinding(FewShotModelDialog.ShowMaskTipProperty, binding3);
		fewShotModelDialog.ShowDialog();
		RefreshThumbnail(fewShotData);
		HasModel = fewShotData.HasModel;
		className = fewShotData.ClassName;
	}

	private void LoadModel_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule == null)
		{
			return;
		}
		if ((OwnerModule as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
			return;
		}
		string text = null;
		IVmModule ownerModule = OwnerModule;
		string text2 = ((ownerModule != null && ownerModule.ModuleTypeName?.Contains("IMVSCnnRegisterDetectModu") == true) ? ".regsm" : ".regfm");
		using (System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog())
		{
			openFileDialog.Title = AppLocalizationService.GetLang("Lang_OpenModelFile");
			openFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*" + text2;
			text = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule.ModuleDir);
			if (text != null)
			{
				openFileDialog.InitialDirectory = text;
			}
			if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return;
			}
			text = openFileDialog.FileName;
		}
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (Path.GetExtension(text) != text2)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("不支持的文件格式"), MessageButtons.Normal, MessageIcons.Error);
			return;
		}
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), OwnerModule.ModuleDir);
		string text3 = "";
		try
		{
			((IUserStringData)OwnerModule.Params)["LoadGalleryFilePath"] = text;
		}
		catch (VmException ex)
		{
			text3 = string.Format("{0}{1}({2}{3})", AppLocalizationService.GetLang("Lang_LoadFail"), Environment.NewLine, AppLocalizationService.GetLang("errInfo:"), ErrorCodeInfo.GetErrorInfo(ex.errorCode));
		}
		if (string.IsNullOrEmpty(text3))
		{
			text3 = GetModel();
		}
		if (!string.IsNullOrEmpty(text3))
		{
			try
			{
				RefreshThumbnail(null);
				HasModel = false;
				((IUserBytesData)OwnerModule.Params)["GalleryDelete"] = Encoding.UTF8.GetBytes(className);
			}
			catch (Exception)
			{
			}
			HikMessageBox.Show(text3, MessageButtons.Normal, MessageIcons.Error);
			LogHelper.Error(text3);
		}
	}

	private void SaveModel_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule == null)
		{
			return;
		}
		if ((OwnerModule as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
			return;
		}
		string text = null;
		IVmModule ownerModule = OwnerModule;
		string text2 = ((ownerModule != null && ownerModule.ModuleTypeName?.Contains("IMVSCnnRegisterDetectModu") == true) ? ".regsm" : ".regfm");
		using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
		{
			saveFileDialog.Title = AppLocalizationService.GetLang("Save Model File");
			saveFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*" + text2;
			text = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule.ModuleDir);
			if (text != null)
			{
				saveFileDialog.InitialDirectory = text;
			}
			if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return;
			}
			try
			{
				text = saveFileDialog.FileName;
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				LogHelper.moduleLog.Error("Get file name error: " + ex.Message);
				return;
			}
		}
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (Path.GetExtension(text) != text2)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("不支持的文件格式"), MessageButtons.Normal, MessageIcons.Error);
			return;
		}
		try
		{
			((IUserStringData)OwnerModule.Params)["SaveAsGalleryFile"] = text;
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ExportSuccess"), MessageIcons.Ok);
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), OwnerModule.ModuleDir);
		}
		catch (VmException ex2)
		{
			string text3 = AppLocalizationService.GetLang("Lang_ExportFailed") + "\r\n(";
			text3 += AppLocalizationService.GetLang("errInfo:");
			text3 += ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			text3 += ")";
			HikMessageBox.Show(text3, MessageIcons.Error);
			LogHelper.moduleLog.Error(text3);
		}
	}

	private void DeleteModel_Click(object sender, RoutedEventArgs e)
	{
		if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDelete"), MessageButtons.OkOrCancel, MessageIcons.Question) != VM.Controls.DialogResult.Ok || OwnerModule == null)
		{
			return;
		}
		try
		{
			((IUserBytesData)OwnerModule.Params)["GalleryDelete"] = Encoding.UTF8.GetBytes(className);
			RefreshThumbnail(null);
			HasModel = false;
		}
		catch (VmException ex)
		{
			HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode), MessageButtons.Normal, MessageIcons.Error);
		}
		catch (Exception ex2)
		{
			HikMessageBox.Show(ex2.Message, MessageButtons.Normal, MessageIcons.Error);
		}
	}
}
