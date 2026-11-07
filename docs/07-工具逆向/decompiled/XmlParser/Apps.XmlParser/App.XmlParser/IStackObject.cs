namespace App.XmlParser;

internal interface IStackObject
{
	string Symbol { get; set; }

	StackObjectType Type { get; set; }
}
