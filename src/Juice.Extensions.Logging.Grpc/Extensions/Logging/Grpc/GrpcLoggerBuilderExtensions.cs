using Juice.Extensions.Logging.Grpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging
{
    public static class GrpcLoggerBuilderExtensions
    {
        public static ILoggingBuilder AddGrpcLogger(this ILoggingBuilder builder, IConfigurationSection configuration)
        {
            builder.Services.Configure<GrpcLoggerOptions>(configuration);
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, GrpcLoggerProvider>());

            return builder;
        }

        public static ILoggingBuilder AddGrpcLogger(this ILoggingBuilder builder, Action<GrpcLoggerOptions> configure)
        {
            builder.Services.Configure(configure);
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, GrpcLoggerProvider>());

            return builder;
        }
    }
}
