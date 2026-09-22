using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

/// <summary>
/// 并行调用所有已注册发现器，并按稳定 DeviceId 去重。
/// UI 不需要预先选择接口或品牌。
/// </summary>
public sealed class CameraDiscoveryCoordinator
{
    private readonly IReadOnlyList<ICameraDiscoveryProvider> _providers;

    public CameraDiscoveryCoordinator(IEnumerable<ICameraDiscoveryProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToArray();
    }

    public async Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(
        CancellationToken cancellationToken = default)
        => await DiscoverAsync(null, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(
        IReadOnlySet<CameraTransportKind>? transports,
        CancellationToken cancellationToken = default)
    {
        var tasks = _providers
            .Select(provider => DiscoverProviderAsync(provider, transports, cancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return results
            .SelectMany(result => result)
            .GroupBy(item => item.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static async Task<IReadOnlyList<CameraDescriptor>> DiscoverProviderAsync(
        ICameraDiscoveryProvider provider,
        IReadOnlySet<CameraTransportKind>? transports,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.DiscoverAsync(cancellationToken, transports).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // 一个品牌或协议发现器失败不能阻断其他发现器。
            return Array.Empty<CameraDescriptor>();
        }
    }
}
