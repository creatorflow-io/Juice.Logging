using Juice.Extensions.Logging.File;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging
{
    public static class FileLoggerLogBuilderExtensions
    {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, IConfigurationSection configuration)
        {
            builder.Services.Configure<FileLoggerOptions>(configuration);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());

            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, Action<FileLoggerOptions> configure)
        {
            builder.Services.Configure(configure);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());

            return builder;
        }

    }
}
