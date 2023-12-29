using Juice.Extensions.Logging.Grpc.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

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
            return builder;
        }
    }
}
