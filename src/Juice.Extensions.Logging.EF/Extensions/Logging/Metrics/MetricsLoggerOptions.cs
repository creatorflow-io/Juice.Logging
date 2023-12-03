using Juice.Extensions.Logging.File;

namespace Juice.Extensions.Logging.Metrics
{
    public class MetricsLoggerOptions : FileLoggerOptions
    {
        public TimeSpan? SampleRate { get; set; }
        public bool Disabled { get; set; }
    }
}
