namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class ServiceLogMetric
    {
        public ServiceLogMetric(Guid serviceId, uint errors, uint warnings, uint criticals, DateTimeOffset timestamp)
        {
            ServiceId = serviceId;
            Timestamp = timestamp;

            Errors = errors;
            Warnings = warnings;
            Criticals = criticals;
        }
        public Guid ServiceId { get; set; }
        public DateTimeOffset Timestamp { get; private set; }

        public uint Errors { get; private set; }
        public uint Warnings { get; private set; }
        public uint Criticals { get; private set; }
    }
}

