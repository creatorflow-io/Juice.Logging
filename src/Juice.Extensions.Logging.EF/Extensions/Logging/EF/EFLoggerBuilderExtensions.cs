using Juice.EF;
using Juice.Extensions.Logging.EF;
using Juice.Extensions.Logging.EF.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging
{
    public static class EFLoggerBuilderExtensions
    {
        public static ILoggingBuilder AddDbLogger(this ILoggingBuilder builder, IConfigurationSection configuration, IConfigurationRoot configurationRoot)
        {
            builder.Services.AddLogDbContext(configurationRoot, options =>
            {
                if (!string.IsNullOrEmpty(configuration["DatabaseProvider"]))
                {
                    options.DatabaseProvider = configuration["DatabaseProvider"];
                }

                options.ConnectionName = configuration["ConnectionName"];
                options.Schema = configuration["Schema"];
            });

            builder.Services.Configure<EFLoggerOptions>(configuration);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, EFLoggerProvider>());

            return builder;
        }

        public static ILoggingBuilder AddDbLogger(this ILoggingBuilder builder, Action<EFLoggerOptions> configure,
            Action<DbOptions> dbConfigure, IConfigurationRoot configurationRoot)
        {
            builder.Services.AddLogDbContext(configurationRoot, dbConfigure);

            builder.Services.Configure(configure);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, EFLoggerProvider>());

            return builder;
        }
    }
}
