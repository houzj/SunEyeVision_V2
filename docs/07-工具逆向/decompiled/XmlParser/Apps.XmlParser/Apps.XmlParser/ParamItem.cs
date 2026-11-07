using System.Windows;

namespace Apps.XmlParser;

public class ParamItem : BaseParamNode
{
	private bool _IsSelected;

	public virtual string ParamValueType { get; set; }

	public virtual bool HasRelative { get; set; }

	public virtual Visibility IndexVisible => Visibility.Collapsed;

	public bool IsSelected
	{
		get
		{
			return _IsSelected;
		}
		set
		{
			_IsSelected = value;
			OnPropertyChanged("IsSelected");
		}
	}

	public virtual void InniterBusiness()
	{
	}

	public virtual void OnInniterBusinessFinish()
	{
	}
}
