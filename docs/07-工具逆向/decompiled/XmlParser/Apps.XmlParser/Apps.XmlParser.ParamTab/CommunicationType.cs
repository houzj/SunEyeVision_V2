namespace Apps.XmlParser.ParamTab;

public enum CommunicationType
{
	None = 0,
	TCPClient = 1,
	TCPServer = 2,
	UDP = 4,
	Serial = 8,
	IO = 0x10,
	PLC = 0x20,
	Modbus = 0x40
}
