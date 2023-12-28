using Juice.EF;
using Juice.Extensions.Logging.EF.DependencyInjection;
using Juice.Extensions.Logging.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging
{
    public static class MetricsLoggerBuilderExtensions
    {
        public static ILoggingBuilder AddMetricsLogger(this ILoggingBuilder builder, IConfigurationSection configuration, IConfiguration configurationRoot)
        {
            builder.Services.AddLogMetricsDbContext(configurationRoot, options =>
            {
                if (!string.IsNullOrEmpty(configuration["DatabaseProvider"]))
                {
                    options.DatabaseProvider = configuration["DatabaseProvider"];
                }

                options.ConnectionName = configuration["ConnectionName"];
                options.Schema = configuration["Schema"];
            });

            builder.Services.Configure<MetricsLoggerOptions>(configuration);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, MetricsLoggerProvider>());

            return builder;
        }

        public static ILoggingBuilder AddMetricsLogger(this ILoggingBuilder builder, Action<MetricsLoggerOptions> configure,
            Action<DbOptions> dbConfigure, IConfiguration configurationRoot)
        {
            builder.Services.AddLogMetricsDbContext(configurationRoot, dbConfigure);

            builder.Services.Configure(configure);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, MetricsLoggerProvider>());

            return builder;
        }
    }
}
