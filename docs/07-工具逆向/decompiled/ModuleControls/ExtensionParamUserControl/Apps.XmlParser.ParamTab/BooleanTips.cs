using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Apps.XmlParser.ParamTab;

public class BooleanTips : Boolean
{
	private string _messageTips;

	public string SelecetedTips;

	public string UnSelecetedTips;

	public string MessageTips
	{
		get
		{
			return _messageTips;
		}
		set
		{
			SetProperty(ref _messageTips, value, "MessageTips");
		}
	}

	protected void SetProperty<T>(ref T prop, T value, [CallerMemberName] string propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(prop, value))
		{
			prop = value;
			OnPropertyChanged(propertyName);
		}
	}

	public BooleanTips()
	{
		PropertyChanged += BooleanTips_PropertyChanged;
	}

	private void BooleanTips_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "CurValueA")
		{
			MessageTips = (CurValueA ? SelecetedTips : UnSelecetedTips);
		}
	}
}
