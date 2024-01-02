using Juice.Extensions.Logging.File;

namespace Juice.Extensions.Logging.GrpcMetrics
{
    public class MetricsLoggerOptions : FileLoggerOptions
    {
        public string? Endpoint { get; set; }
        public TimeSpan? SampleRate { get; set; }
        public bool Disabled { get; set; }
    }
}
