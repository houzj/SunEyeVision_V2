using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

namespace SunEyeVision.UI.Features.CommunicationManager;

internal static class SerialPortCatalog
{
    private static readonly int[] StandardBaudRates =
    {
        300, 600, 1200, 2400, 4800, 9600, 14400, 19200,
        38400, 56000, 57600, 115200, 230400, 460800, 921600
    };

    public static IReadOnlyList<int> GetBaudRates(int currentBaudRate = 0)
    {
        var values = StandardBaudRates.ToList();
        if (currentBaudRate > 0 && !values.Contains(currentBaudRate))
            values.Add(currentBaudRate);
        values.Sort();
        return values;
    }

    public static IReadOnlyList<string> GetPorts(string? currentPort = null)
    {
        IEnumerable<string> ports;
        try { ports = SerialPort.GetPortNames(); }
        catch { ports = Array.Empty<string>(); }

        var values = ports
            .Where(port => !string.IsNullOrWhiteSpace(port))
            .Select(port => port.Trim())
            .Where(port => port.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentPort) &&
            !values.Contains(currentPort.Trim(), StringComparer.OrdinalIgnoreCase))
            values.Insert(0, currentPort.Trim());

        return values;
    }
}
