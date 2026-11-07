using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Apps.ColorFun;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Hik.Util;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class OpenDialog : NameValueItem
{
	public double DialogWidth;

	public double DialogHeight;

	private object CurValue;

	public string Param;

	private DelegateCommand _ClickCommand = null;

	public object CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public override bool NeedInit => false;

	public override string DisplayType => "Command";

	public ICommand ClickCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	private void ClickCommandCmdExecute()
	{
		if (CurValueA == null && !string.IsNullOrEmpty(Param))
		{
			try
			{
				string[] array = Param.Split(';');
				string ns = array[0].Remove(Param.LastIndexOf('.'));
				Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly p) => string.Equals(p.GetName().Name, ns));
				if (assembly == null)
				{
					LogHelper.Info("dllpath=myLibs\\" + ns + ".dll");
					if (File.Exists("myLibs\\" + ns + ".dll"))
					{
						LogHelper.Info("dllpath Exists");
						ActivatorWrap.LoadAssembly("myLibs\\" + ns + ".dll");
						assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly p) => string.Equals(p.GetName().Name, ns));
					}
				}
				int num = array.Length;
				string text = ((num > 1) ? array[0] : Param);
				if (assembly != null || ActivatorWrap.TypeTable.ContainsKey(text))
				{
					if (num > 1)
					{
						object[] array2 = new object[num - 1];
						for (int num2 = 1; num2 < num; num2++)
						{
							array2[num2 - 1] = array[num2];
						}
						CurValueA = ActivatorWrap.CreateInstance(text, array2);
					}
					else
					{
						CurValueA = ActivatorWrap.CreateInstance(text);
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex.Message);
				throw new Exception(AppLocalizationService.GetLang(Name) + AppLocalizationService.GetLang("生成") + AppLocalizationService.GetLang("Lang_Err"));
			}
		}
		DoOpenDialog();
	}

	private void DoOpenDialog()
	{
		if (CurValueA != null)
		{
			BaseWindow baseWindow = new BaseWindow
			{
				ShowInTaskbar = false
			};
			baseWindow.Width = DialogWidth;
			baseWindow.Height = DialogHeight;
			baseWindow.Title = AppLocalizationService.GetLang(DisplayNameA);
			baseWindow.FontSize = 14.0;
			baseWindow.FontWeight = FontWeights.Normal;
			baseWindow.MinButtonVisibility = System.Windows.Visibility.Collapsed;
			ColorThemesResourceDictionary.GetInstance(baseWindow);
			baseWindow.Content = CurValueA;
			(CurValueA as IOwner).Owner = baseWindow;
			if (CurValueA is IParentNode)
			{
				(CurValueA as IParentNode).SetParentNode(this);
			}
			if (CurValueA is IFixSize)
			{
				baseWindow.Width = (CurValueA as IFixSize).FixSize.Width;
				baseWindow.Height = (CurValueA as IFixSize).FixSize.Height;
			}
			baseWindow.ShowDialog();
			DeInite();
		}
	}

	protected virtual void DeInite()
	{
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
