using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Juice.Extensions.Logging.Grpc.Server
{
    internal class MetricsService : MetricsWriter.MetricsWriterBase
    {
        private MetricsWriterService _writerService;

        public MetricsService(MetricsWriterService writerService)
        {
            _writerService = writerService;
        }

        public override async Task<Empty> Write(GrpcMetricsRequest request, ServerCallContext context)
        {
            if (request.Metrics.Any())
            {
                _writerService.AddMetrics(request);
            }
            return new Empty();
        }
    }
}
