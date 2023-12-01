using Juice.Extensions.Logging.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging
{
    public static class SignalRLoggerLogBuilderExtensions
    {
        public static ILoggingBuilder AddSignalRLogger(this ILoggingBuilder builder, IConfigurationSection configuration)
        {
            builder.Services.Configure<SignalRLoggerOptions>(configuration);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SignalRLoggerProvider>());

            return builder;
        }

        public static ILoggingBuilder AddSignalRLogger(this ILoggingBuilder builder, Action<SignalRLoggerOptions> configure)
        {
            builder.Services.Configure(configure);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SignalRLoggerProvider>());

            return builder;
        }
    }
}
