namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class ServiceLogMetric
    {
        public ServiceLogMetric(Guid serviceId, uint errCount, uint wrnCount, uint criCount,
            uint dbgCount, uint infCount,
            DateTimeOffset timestamp)
        {
            ServiceId = serviceId;
            Timestamp = timestamp;

            DbgCount = dbgCount;
            InfCount = infCount;
            ErrCount = errCount;
            WrnCount = wrnCount;
            CriCount = criCount;
        }
        public Guid ServiceId { get; set; }
        public DateTimeOffset Timestamp { get; private set; }

        public uint DbgCount { get; private set; }
        public uint InfCount { get; private set; }
        public uint ErrCount { get; private set; }
        public uint WrnCount { get; private set; }
        public uint CriCount { get; private set; }

        public void Add(ServiceLogMetric metric)
        {
            DbgCount += metric.DbgCount;
            InfCount += metric.InfCount;
            ErrCount += metric.ErrCount;
            WrnCount += metric.WrnCount;
            CriCount += metric.CriCount;
        }
    }
}

