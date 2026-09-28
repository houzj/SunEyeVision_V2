using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.DeviceDriver.Communications;

/// <summary>Typed Modbus operations are driver-internal until a cross-layer consumer exists.</summary>
internal interface IModbusOperations
{
    Task<bool[]> ReadCoilsAsync(ushort address, ushort count, CancellationToken cancellationToken);
    Task<bool[]> ReadInputsAsync(ushort address, ushort count, CancellationToken cancellationToken);
    Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken);
    Task<ushort[]> ReadInputRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken);
    Task WriteCoilAsync(ushort address, bool value, CancellationToken cancellationToken);
    Task WriteRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken);
    Task WriteCoilsAsync(ushort address, bool[] values, CancellationToken cancellationToken);
    Task WriteRegistersAsync(ushort address, ushort[] values, CancellationToken cancellationToken);
}
