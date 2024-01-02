namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class ServiceLogMetric : LogMetric, IEquatable<ServiceLogMetric>
    {
        public ServiceLogMetric(Guid serviceId, uint errCount, uint wrnCount, uint criCount,
            uint dbgCount, uint infCount,
            DateTimeOffset timestamp) : base(errCount, wrnCount, criCount, dbgCount, infCount, timestamp)
        {
            ServiceId = serviceId;
        }
        public Guid ServiceId { get; set; }

        public bool Equals(ServiceLogMetric? other)
            => ServiceId.Equals(other?.ServiceId) && Timestamp.Equals(other?.Timestamp);
    }
}

