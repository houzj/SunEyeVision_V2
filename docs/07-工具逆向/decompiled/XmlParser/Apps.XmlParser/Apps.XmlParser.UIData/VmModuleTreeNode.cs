using System.Collections.ObjectModel;
using System.Linq;
using Apps.Data;

namespace Apps.XmlParser.UIData;

public class VmModuleTreeNode : NotifyPropertyChangedBase
{
	private uint moduleID;

	private uint procedureID;

	private string moduleName;

	private string moduleFullName;

	private VmModuleTreeNode parentNode;

	private ObservableCollection<VmModuleTreeNode> childrenNodes;

	protected bool? isSelected = false;

	private bool isExpanded;

	private object extend;

	public uint ModuleID
	{
		get
		{
			return moduleID;
		}
		set
		{
			SetProperty(ref moduleID, value, "ModuleID");
		}
	}

	public uint ProcedureID
	{
		get
		{
			return procedureID;
		}
		set
		{
			SetProperty(ref procedureID, value, "ProcedureID");
		}
	}

	public string ModuleName
	{
		get
		{
			return moduleName;
		}
		set
		{
			SetProperty(ref moduleName, value, "ModuleName");
		}
	}

	public string ModuleFullName
	{
		get
		{
			return moduleFullName;
		}
		set
		{
			SetProperty(ref moduleFullName, value, "ModuleFullName");
		}
	}

	public VmModuleTreeNode ParentNode
	{
		get
		{
			return parentNode;
		}
		set
		{
			SetProperty(ref parentNode, value, "ParentNode");
		}
	}

	public ObservableCollection<VmModuleTreeNode> ChildrenNodes
	{
		get
		{
			return childrenNodes;
		}
		set
		{
			SetProperty(ref childrenNodes, value, "ChildrenNodes");
		}
	}

	public bool? IsSelected
	{
		get
		{
			return isSelected;
		}
		set
		{
			SetIsChecked(value, checkedChildren: true, checkedParent: true);
		}
	}

	public bool IsExpanded
	{
		get
		{
			return isExpanded;
		}
		set
		{
			SetProperty(ref isExpanded, value, "IsExpanded");
		}
	}

	public object Extend
	{
		get
		{
			return extend;
		}
		set
		{
			SetProperty(ref extend, value, "Extend");
		}
	}

	protected virtual void SetIsChecked(bool? value, bool checkedChildren, bool checkedParent)
	{
		if (isSelected == value)
		{
			return;
		}
		isSelected = value;
		if (checkedChildren && value.HasValue && childrenNodes != null)
		{
			foreach (VmModuleTreeNode childrenNode in childrenNodes)
			{
				childrenNode.SetIsChecked(value, checkedChildren: true, checkedParent: false);
			}
		}
		if (checkedParent && ParentNode != null)
		{
			bool? flag = null;
			for (int i = 0; i < Enumerable.Count(ParentNode.ChildrenNodes); i++)
			{
				bool? flag2 = ParentNode.ChildrenNodes[i].IsSelected;
				if (i == 0)
				{
					flag = flag2;
				}
				else if (flag != flag2)
				{
					flag = null;
				}
			}
			ParentNode.SetIsChecked(flag, checkedChildren: false, checkedParent: true);
		}
		OnPropertyChanged("IsSelected");
	}
}
