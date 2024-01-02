using Juice.Extensions.Logging.GrpcMetrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging
{
    public static class GrpcMetricsLoggerBuilderExtensions
    {
        public static ILoggingBuilder AddGrpcMetricsLogger(this ILoggingBuilder builder, IConfigurationSection configuration)
        {
            builder.Services.Configure<MetricsLoggerOptions>(configuration);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, MetricsLoggerProvider>());

            return builder;
        }

        public static ILoggingBuilder AddGrpcMetricsLogger(this ILoggingBuilder builder, Action<MetricsLoggerOptions> configure)
        {
            builder.Services.Configure(configure);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, MetricsLoggerProvider>());

            return builder;
        }
    }
}
