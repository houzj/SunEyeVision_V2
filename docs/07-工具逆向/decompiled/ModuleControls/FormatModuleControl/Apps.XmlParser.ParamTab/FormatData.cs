using System.Collections.Generic;

namespace Apps.XmlParser.ParamTab;

public class FormatData
{
	public string DelimDelimiter { get; set; }

	public string ArrayDelimiter { get; set; }

	public List<FormatRowData> Rows { get; set; }
}
