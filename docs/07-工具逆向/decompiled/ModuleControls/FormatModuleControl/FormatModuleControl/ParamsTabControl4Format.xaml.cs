using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Apps.Localization;
using VM.Controls;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.WPF;

namespace FormatModuleControl;

public partial class ParamsTabControl4Format : TabWinBase, IComponentConnector
{
	public bool IsClosed = false;

	public override IParamsConfig ParamsConfig
	{
		get
		{
			return paramstab.ParamsConfig;
		}
		set
		{
			paramstab.ParamsConfig = value;
		}
	}

	public ParamsTabControl4Format()
	{
		InitializeComponent();
		SetBinding(FrameworkElement.WidthProperty, new Binding("DataContext.ConfigWindowWidth")
		{
			Source = paramstab
		});
		SetBinding(FrameworkElement.HeightProperty, new Binding("DataContext.ConfigWindowHeight")
		{
			Source = paramstab
		});
		Binding binding = new Binding("DataContext.ConfigMaxButtonVisibility")
		{
			Converter = new BooleanToVisibilityConverter(),
			Source = paramstab
		};
		SetBinding(BaseWindow.MaxButtonVisibilityProperty, binding);
		IsClosed = false;
	}

	private void BaseWindow_Closing(object sender, CancelEventArgs e)
	{
		if (ParamsConfig is IParent { Parent: IVmModule { NeedConfirm: not false } } && HikMessageBox.Show(AppLocalizationService.GetLang("是否放弃保存直接退出？"), "", MessageButtons.YesOrNo, MessageIcons.Question) != VM.Controls.DialogResult.Yes)
		{
			e.Cancel = true;
		}
		else
		{
			IsClosed = true;
		}
	}
}
