using System;
using System.Linq;
using SunEyeVision.Workflow;
using ConfiguredCommunication = SunEyeVision.Workflow.Communication;

namespace SunEyeVision.UI.Services.Communication;

public static class CommunicationResponseFormatter
{
    public static CommunicationSendEventDefinition Create(CommunicationResponseDefinition config,
        ConfiguredCommunication device, string content, string? externalId = null)
    {
        var type = device.IsRawProtocol ? "string" : config.DataType;
        if (string.IsNullOrWhiteSpace(type)) throw new FormatException("请选择有效的响应数据类型。");
        var text = content + (externalId == null ? "" : config.Separator + externalId);
        var message = new CommunicationSendEventDefinition { DeviceId = device.Id, TargetId = config.TargetId, Separator = config.Separator };
        if (type.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            // PLC strings carry their terminator in the encoded content; raw transports use the same bytes.
            message.Parameters.Add(new() { Name = "response", DataType = "string", Content = text + config.Terminator });
        }
        else
        {
            if (!string.IsNullOrEmpty(config.Terminator)) throw new FormatException("数值响应不能配置字符串结束符。");
            if (externalId != null && string.IsNullOrEmpty(config.Separator))
                throw new FormatException("数值响应附加编号时必须配置分隔符，不能拼接为另一个数字。");
            var parts = string.IsNullOrEmpty(config.Separator) ? new[] { text } : text.Split(config.Separator, StringSplitOptions.None);
            for (var i = 0; i < parts.Length; i++)
            {
                // Validate all segments before any write, with no implicit conversion or partial send.
                CommunicationSendEventFormatter.ParseLiteral(parts[i], type);
                message.Parameters.Add(new() { Name = "value" + i, DataType = type, Content = parts[i] });
            }
        }
        return message;
    }
}
