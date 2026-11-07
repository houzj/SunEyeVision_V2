using System;
using System.Collections;
using System.Linq;
using Apps.XmlParser.ParamTab;

namespace DynamicObjectConfigControl;

public class CommonParam : NameValueItem
{
	public virtual IList CommonItems
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		foreach (IButtonSelecter item in CommonItems.OfType<IButtonSelecter>())
		{
			item.Selecter.InniterBusiness();
		}
	}
}
