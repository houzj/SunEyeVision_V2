using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using GlobalScriptUI;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.Editor;
using ICSharpCode.NRefactory.TypeSystem;
using VM.CodeCompletion;
using VM.CodeCompletion.DataItems;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;
using VM.Utility.SingleInstance;

namespace ShellModuleControl;

public class ShellCodeEditor : CodeTextEditor
{
	private delegate void AsyncTask();

	private readonly MenuItem _addModule = new MenuItem
	{
		Header = AppLocalizationService.GetLang("添加模块名")
	};

	private List<CompletionData> completionDatas_Shell;

	private List<CompletionData> completionDatas_Shell_In = new List<CompletionData>();

	private List<CompletionData> completionDatas_Shell_Out = new List<CompletionData>();

	private bool _branchSet;

	private ObservableCollection<string> _modulenames;

	public int nTextLen = 0;

	private List<Assembly> objListAssembly = null;

	private List<string> objListAssmblyName = null;

	private int ReplaceLeft { get; set; }

	private int ReplaceRight { get; set; }

	public bool IsBranchSet
	{
		get
		{
			return _branchSet;
		}
		set
		{
			_branchSet = value;
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs("IsBranchSet"));
			}
		}
	}

	public ObservableCollection<string> ModuleNames
	{
		set
		{
			_modulenames = value;
			if (_modulenames == null)
			{
				return;
			}
			_modulenames.CollectionChanged += (object s, NotifyCollectionChangedEventArgs e) =>
			{
				switch (e.Action)
				{
				case NotifyCollectionChangedAction.Add:
				{
					foreach (object newItem in e.NewItems)
					{
						MenuItem menuItem2 = new MenuItem
						{
							Header = newItem.ToString()
						};
						_addModule.Items.Add(menuItem2);
						menuItem2.Click += MenuItem_Click;
					}
					break;
				}
				case NotifyCollectionChangedAction.Remove:
				{
					foreach (object oldItem in e.OldItems)
					{
						for (int i = 0; i < _addModule.Items.Count; i++)
						{
							MenuItem menuItem = _addModule.Items[i] as MenuItem;
							if (menuItem == null || menuItem.Header == null || !(menuItem.Header.ToString() != oldItem.ToString()))
							{
								if (menuItem != null)
								{
									menuItem.Click -= MenuItem_Click;
									menuItem.RemoveEvents();
								}
								_addModule.Items.RemoveAt(i);
								break;
							}
						}
						oldItem.RemoveEvents();
					}
					break;
				}
				}
			};
		}
	}

	public List<CompletionData> CompletionDatas_Shell
	{
		get
		{
			if (completionDatas_Shell == null)
			{
				completionDatas_Shell = new List<CompletionData>();
			}
			completionDatas_Shell.Clear();
			completionDatas_Shell.AddRange(completionDatas_Shell_In);
			completionDatas_Shell.AddRange(completionDatas_Shell_Out);
			return completionDatas_Shell;
		}
		set
		{
			completionDatas_Shell = value;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public ShellCodeEditor()
	{
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Expected Obj, but got Unknown
		BeginAddVariable += ShellCodeEditor_BeginAddVariable;
		((TextEditor)this).TextArea.Caret.PositionChanged += Caret_PositionChanged;
		Completion = new CSharpCompletion(GetAssemblies());
		HikContextMenu contextMenu = new HikContextMenu
		{
			StaysOpen = true,
			Items = 
			{
				(object)new MenuItem
				{
					Command = ApplicationCommands.Copy
				},
				(object)new MenuItem
				{
					Command = ApplicationCommands.Cut
				},
				(object)new MenuItem
				{
					Command = ApplicationCommands.Paste
				},
				(object)new MenuItem
				{
					Command = ApplicationCommands.Undo
				},
				(object)new MenuItem
				{
					Command = ApplicationCommands.Redo
				},
				(object)new MenuItem
				{
					Command = cmdCtrlSpace,
					Header = AppLocalizationService.GetLang("智能感知")
				}
			}
		};
		((FrameworkElement)(object)((TextEditor)this).TextArea).ContextMenu = contextMenu;
		((TextEditor)this).TextArea.IndentationStrategy = (IIndentationStrategy)new CSharpIndentationStrategy(((TextEditor)this).Options);
		((UIElement)(object)((TextEditor)this).TextArea).PreviewKeyUp += TextArea_PreviewKeyUp;
	}

	private void TextArea_PreviewKeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return && CVmObjectBase.GetIsSDKMode())
		{
			string value = "\n";
			int num = 1;
			int selectionStart = ((TextEditor)this).SelectionStart;
			if (completionWindow != null && completionWindow.CompletionList.SelectedItem != null)
			{
				value = completionWindow.CompletionList.SelectedItem.Text;
				int count = ((CompletionWindowBase)completionWindow).EndOffset - ((CompletionWindowBase)completionWindow).StartOffset;
				string text = ((TextEditor)this).Text.Remove(((CompletionWindowBase)completionWindow).StartOffset, count);
				selectionStart = ((CompletionWindowBase)completionWindow).StartOffset;
				((TextEditor)this).Text = text.Insert(selectionStart, value);
				((TextEditor)this).SelectionStart = selectionStart + value.Length;
				((Window)(object)completionWindow).Close();
			}
			else if (nTextLen == ((TextEditor)this).Text.Length)
			{
				string text2 = ((TextEditor)this).Text.Insert(selectionStart, value);
				((TextEditor)this).Text = text2;
				((TextEditor)this).SelectionStart = selectionStart + num;
			}
		}
		nTextLen = ((TextEditor)this).Text.Length;
	}

	public bool UpdateAssembile(ShellRefrences[] assembilesName)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Invalid comparison between Unknown and I4
		try
		{
			if (assembilesName == null || assembilesName.Length < 1)
			{
				return false;
			}
			List<Assembly> list = new List<Assembly>();
			List<string> list2 = new List<string>();
			for (int i = 0; i < assembilesName.Length; i++)
			{
				if (string.IsNullOrEmpty(assembilesName[i].Name))
				{
					continue;
				}
				bool flag = false;
				string text = assembilesName[i].FullName();
				if ((int)assembilesName[i].refrencesType == 4)
				{
					if (File.Exists(text))
					{
						list2.Add(text);
					}
				}
				else
				{
					flag = true;
				}
				if (flag)
				{
					Assembly item = Assembly.LoadFrom(text);
					list.Add(item);
				}
			}
			Completion = new CSharpCompletion(list);
			AddAssemblyTask(list2);
		}
		catch (Exception)
		{
		}
		return true;
	}

	public string SetListAssembly(List<ShellRefrences> assembilesName)
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Invalid comparison between Unknown and I4
		string text = string.Empty;
		if (assembilesName == null || assembilesName.Count < 1)
		{
			return text;
		}
		if (objListAssembly == null)
		{
			objListAssembly = new List<Assembly>();
		}
		objListAssembly.Clear();
		objListAssmblyName.Clear();
		List<string> list = new List<string>();
		for (int i = 0; i < assembilesName.Count; i++)
		{
			if (string.IsNullOrEmpty(assembilesName[i].Name))
			{
				continue;
			}
			bool flag = false;
			string text2 = assembilesName[i].FullName();
			if ((int)assembilesName[i].refrencesType == 4)
			{
				if (File.Exists(text2))
				{
					list.Add(text2);
				}
				else
				{
					text = text + assembilesName[i].Name + "\r\n";
				}
			}
			else
			{
				flag = true;
			}
			if (flag)
			{
				Assembly item = Assembly.LoadFrom(text2);
				objListAssembly.Add(item);
			}
			objListAssmblyName.Add(assembilesName[i].Name);
		}
		Completion = new CSharpCompletion(objListAssembly);
		AddAssemblyTask(list);
		return text;
	}

	private void AddAssemblyTask(List<string> assemblyName)
	{
		AsyncTask asyncTask = () =>
		{
			Parallel.For(0, assemblyName.Count, (int i) =>
			{
				Completion.AddAssembly(assemblyName[i]);
			});
		};
		asyncTask.BeginInvoke(null, null);
	}

	public List<Assembly> GetAssemblies()
	{
		if (objListAssembly != null && objListAssembly.Count > 0)
		{
			return objListAssembly;
		}
		if (objListAssembly == null)
		{
			objListAssembly = new List<Assembly>();
		}
		objListAssembly.Clear();
		objListAssembly = new List<Assembly>
		{
			typeof(object).Assembly,
			typeof(Uri).Assembly,
			typeof(Enumerable).Assembly,
			typeof(MessageBox).Assembly
		};
		Assembly item = Assembly.LoadFrom(CurrentPathHelper.Instance.GetCurrentPath() + "\\Module(sp)\\x64\\Logic\\ShellModule\\Script.Methods.dll");
		objListAssembly.Add(item);
		if (objListAssmblyName == null)
		{
			objListAssmblyName = new List<string>();
		}
		objListAssmblyName.Clear();
		objListAssembly.ForEach((Assembly x) =>
		{
			objListAssmblyName.Add(x.FullName.Substring(0, x.FullName.IndexOf(",")) + ".dll");
		});
		return objListAssembly;
	}

	private void Caret_PositionChanged(object sender, EventArgs e)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		IsBranchSet = false;
		Caret caret = ((TextEditor)this).TextArea.Caret;
		TextViewPosition position = caret.Position;
		if (position.IsAtEndOfLine)
		{
			return;
		}
		position = caret.Position;
		TextLocation location = position.Location;
		if (location.IsEmpty)
		{
			return;
		}
		int column = caret.Column;
		if (column <= 1)
		{
			return;
		}
		DocumentLine lineByNumber = ((TextEditor)this).TextArea.TextView.Document.GetLineByNumber(caret.Line);
		string text = ((TextEditor)this).TextArea.TextView.Document.GetText((ISegment)(object)lineByNumber);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			string leftorg = text.Substring(0, column - 1);
			string rightorg = text.Substring(column - 1, text.Length - column + 1);
			VerifyBranchSet(leftorg, rightorg, column);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	private void ShellCodeEditor_BeginAddVariable(object sender, EventArgs e)
	{
		if (completionWindow == null)
		{
			return;
		}
		IList<ICompletionData> completionData = completionWindow.CompletionList.CompletionData;
		foreach (CompletionData item in CompletionDatas_Shell.OrderBy((CompletionData item) => item.Text))
		{
			int num = item.Description.IndexOf('.');
			string text = (string.IsNullOrWhiteSpace(CurrentClassName) ? "" : (CurrentClassName + "." + item.Text));
			if (!item.Description.EndsWith(item.Text))
			{
				item.Description += text;
			}
			completionData.Add((ICompletionData)(object)item);
		}
	}

	private void ReplaceLine(string param)
	{
		if (IsBranchSet)
		{
			DocumentLine lineByNumber = ((TextEditor)this).TextArea.TextView.Document.GetLineByNumber(((TextEditor)this).TextArea.Caret.Line);
			string text = ((TextEditor)this).TextArea.TextView.Document.GetText((ISegment)(object)lineByNumber);
			string arg = text.Substring(0, ReplaceLeft);
			string text2 = text.Substring(ReplaceRight - 1, text.Length - ReplaceRight + 1);
			string text3 = $"{arg}\"{param}\"{text2}";
			((TextEditor)this).TextArea.Document.Replace((ISegment)(object)lineByNumber, text3);
			Caret caret = ((TextEditor)this).TextArea.Caret;
			caret.Offset -= text2.Length;
		}
	}

	private void VerifyBranchSet(string leftorg, string rightorg, int column)
	{
		ReplaceLeft = 0;
		ReplaceRight = 0;
		string text = leftorg;
		int num = 0;
		if (text.Contains(" "))
		{
			num = text.LastIndexOf(" ", StringComparison.Ordinal) + 1;
		}
		text = text.Substring(num, text.Length - num);
		int num2 = text.IndexOf("Setbranch(", StringComparison.Ordinal);
		if (num2 < 0)
		{
			return;
		}
		if (num2 > 0)
		{
			num2 = text.LastIndexOf(";", StringComparison.Ordinal);
			if (num2 < 0)
			{
				return;
			}
			num = num + num2 + 1;
			text = leftorg.Substring(num, leftorg.Length - num);
			num2 = text.IndexOf("Setbranch(", StringComparison.Ordinal);
			if (num2 != 0)
			{
				return;
			}
		}
		num += "Setbranch(".Length;
		int num3 = rightorg.LastIndexOf(")", StringComparison.Ordinal);
		if (num3 >= 0)
		{
			ReplaceRight = column + num3;
			ReplaceLeft = num;
			IsBranchSet = true;
		}
	}

	public void AddShellVariable(DynamicIOContainer<ShellDynamicIO> category, int paramType)
	{
		if (category == null || category.ItemsA == null)
		{
			return;
		}
		List<CompletionData> list = new List<CompletionData>();
		foreach (ShellDynamicIO item in category.ItemsA)
		{
			string text = item.NameA;
			if (text.First() == '%' && text.Last() == '%' && text.Length > 1)
			{
				text = text.Substring(1, text.Length - 2);
			}
			CompletionData completionData = new CompletionData(text);
			completionData.Image = CompletionImage.GetImage("Property", (Accessibility)1);
			switch (paramType)
			{
			case 1:
				completionData.Description = AppLocalizationService.GetLang("输入变量") + "：" + item.DisplayValueTypeA + "  ";
				break;
			case 2:
				completionData.Description = AppLocalizationService.GetLang("输出变量") + "：" + item.DisplayValueTypeA + "  ";
				break;
			}
			list.Add(completionData);
		}
		switch (paramType)
		{
		case 1:
			completionDatas_Shell_In = list;
			break;
		case 2:
			completionDatas_Shell_Out = list;
			break;
		}
	}

	public void MenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { Header: not null } menuItem)
		{
			ReplaceLine(menuItem.Header.ToString());
		}
	}
}
