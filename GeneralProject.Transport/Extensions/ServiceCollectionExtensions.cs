using GeneralProject.Transport.Manager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GeneralProject.Transport.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册传输层框架服务
        /// </summary>
        public static IServiceCollection AddTransport(this IServiceCollection services)
        {
            // DeviceManager 单例
            services.TryAddSingleton<DeviceManager>(sp => DeviceManager.Instance);

            return services;
        }
    }
}