using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GroupOutputParamWeak : GroupOutputParam
{
	public override IList InputOutputTypes
	{
		get
		{
			if (_InputOutputTypes == null)
			{
				IEnumerable<Tuple<string, object>> first = from p in Enum.GetValues(typeof(LeafIOTypeEnum)).OfType<LeafIOTypeEnum>()
					select new Tuple<string, object>("%" + p.ToString().ToLower() + "%", p);
				List<Tuple<string, object>> second = new List<Tuple<string, object>>
				{
					new Tuple<string, object>("%" + CombinationIOTypeEnum.IMAGE.ToString() + "%", CombinationIOTypeEnum.IMAGE)
				};
				_InputOutputTypes = first.Concat(second).ToList();
			}
			return _InputOutputTypes;
		}
	}
}
