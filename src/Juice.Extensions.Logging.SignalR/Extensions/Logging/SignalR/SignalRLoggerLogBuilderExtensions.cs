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
            => builder.AddSignalRLogger<DefaultScopesFilter>(configuration);

        public static ILoggingBuilder AddSignalRLogger<TFilter>(this ILoggingBuilder builder, IConfigurationSection configuration)
            where TFilter : class, IScopesFilter
        {
            builder.Services.Configure<SignalRLoggerOptions>(configuration);
            return builder.AddSignalRLogger<TFilter>();
        }

        private static ILoggingBuilder AddSignalRLogger<TFilter>(this ILoggingBuilder builder)
            where TFilter : class, IScopesFilter
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SignalRLoggerProvider>());

            builder.Services.TryAddSingleton<IScopesFilter, TFilter>();
            return builder;
        }

        public static ILoggingBuilder AddSignalRLogger(this ILoggingBuilder builder, Action<SignalRLoggerOptions> configure)
            => builder.AddSignalRLogger<DefaultScopesFilter>(configure);
        public static ILoggingBuilder AddSignalRLogger<TFilter>(this ILoggingBuilder builder, Action<SignalRLoggerOptions> configure)
            where TFilter : class, IScopesFilter
        {
            builder.Services.Configure(configure);

            return builder.AddSignalRLogger<TFilter>();
        }
    }
}
