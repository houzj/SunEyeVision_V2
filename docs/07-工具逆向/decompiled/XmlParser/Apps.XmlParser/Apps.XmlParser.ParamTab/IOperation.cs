namespace Apps.XmlParser.ParamTab;

public interface IOperation
{
	Setter Executor { get; set; }

	bool Working { get; }

	void Operate(object[] Params, string[] ExtraParams);

	void ReOperate();

	void GetOriginalValue();
}
