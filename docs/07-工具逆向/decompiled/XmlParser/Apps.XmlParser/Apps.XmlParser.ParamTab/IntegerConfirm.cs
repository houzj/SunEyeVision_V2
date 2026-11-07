using System.Collections.ObjectModel;
using System.Xml;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class IntegerConfirm : Integer, IConfirm, IDataType
{
	public ObservableCollection<object> ConfirmSetters;

	public bool IsModifyReStart;

	private bool _isChange = false;

	public string MessageText { get; set; } = null;

	public DataType CurrentType { get; set; } = DataType.INTTYPE;

	public void ConfirmData()
	{
		_isChange = false;
		if (ConfirmSetters == null)
		{
			return;
		}
		foreach (Setter confirmSetter in ConfirmSetters)
		{
			confirmSetter.Operate(null);
		}
	}

	public override void XmlLoadedTask()
	{
		base.XmlLoadedTask();
		if (ConfirmSetters == null)
		{
			return;
		}
		foreach (object confirmSetter in ConfirmSetters)
		{
			(confirmSetter as Setter)?.Init();
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (ConfirmSetters != null && ConfirmSetters.Count > 0)
		{
			foreach (Setter confirmSetter in ConfirmSetters)
			{
				confirmSetter.CreatOperateInstance();
			}
		}
		return this;
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		isInniter = true;
	}

	protected override void NotifyChanges()
	{
		_isChange = true;
	}

	public bool ModifyReStart()
	{
		return _isChange && IsModifyReStart;
	}
}
