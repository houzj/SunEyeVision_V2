using System;
using Microsoft.Extensions.DependencyInjection;

namespace SunEyeVision.Foundation
{
    /// <summary>
    /// 最小服务宿主，补齐平台缺失的依赖注入入口。
    /// </summary>
    /// <remarks>
    /// 存在的原因：VisionMaster 原件的 <c>ILogService</c> 依靠其 UI 平台自带的 IoC（<c>H.Services.*</c> + <c>IocLog</c>）注入，
    /// 本平台此前没有 IoC，基础设施只能以静态单例形式存在（<c>VisionLogger.Instance</c>、<c>PluginLogger</c>、<c>LogManager</c>）。
    /// 本类只提供"能拿到服务"的最小能力，不作为最终形态：后续业务对象应改为构造函数注入，本宿主逐步退化为组合根。
    /// </remarks>
    public static class ServiceHost
    {
        private static readonly object SYNC_ROOT = new();
        private static IServiceProvider? _provider;

        /// <summary>是否已完成初始化。</summary>
        public static bool IsBuilt => _provider is not null;

        /// <summary>构建服务容器。重复构建视为错误，不做静默覆盖。</summary>
        /// <param name="configure">服务注册委托。</param>
        public static void Build(Action<IServiceCollection> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            lock (SYNC_ROOT)
            {
                if (_provider is not null)
                {
                    throw new InvalidOperationException("服务宿主已初始化，不允许重复构建。");
                }

                ServiceCollection services = new();
                configure(services);
                _provider = services.BuildServiceProvider();
            }
        }

        /// <summary>解析必需服务。未初始化或未注册时直接抛异常，不返回默认值。</summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        public static TService GetRequired<TService>()
            where TService : notnull
        {
            lock (SYNC_ROOT)
            {
                if (_provider is null)
                {
                    throw new InvalidOperationException($"服务宿主未初始化，无法解析 {typeof(TService).Name}。请先在应用启动处调用 {nameof(ServiceHost)}.{nameof(Build)}。");
                }

                return _provider.GetRequiredService<TService>();
            }
        }

        /// <summary>尝试解析服务，仅用于可缺省的可选依赖；必需依赖请用 <see cref="GetRequired{TService}"/>。</summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        public static TService? GetOptional<TService>()
            where TService : class
        {
            lock (SYNC_ROOT)
            {
                return _provider?.GetService<TService>();
            }
        }
    }
}
