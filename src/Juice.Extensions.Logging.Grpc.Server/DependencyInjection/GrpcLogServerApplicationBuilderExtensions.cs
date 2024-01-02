using Juice.Extensions.Logging.Grpc.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Logging
{
    public static class GrpcLogServerApplicationBuilderExtensions
    {
        /// <summary>
        /// Map logger gRPC services
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IEndpointRouteBuilder MapGrpcLogServices(this IEndpointRouteBuilder builder)
        {
            builder.MapGrpcService<LogService>();
            builder.MapGrpcService<MetricsService>();
            return builder;
        }

        public static IServiceCollection AddGrpcLogServices(this IServiceCollection services)
        {
            services.AddSingleton<MetricsWriterService>();
            services.AddHostedService(sp => sp.GetRequiredService<MetricsWriterService>());
            return services;
        }
    }
}
