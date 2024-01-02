namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public abstract class LogMetric
    {
        protected LogMetric(uint errCount, uint wrnCount, uint criCount,
            uint dbgCount, uint infCount,
            DateTimeOffset timestamp)
        {
            Timestamp = timestamp;
            DbgCount = dbgCount;
            InfCount = infCount;
            ErrCount = errCount;
            WrnCount = wrnCount;
            CriCount = criCount;
        }
        public DateTimeOffset Timestamp { get; private set; }

        public uint DbgCount { get; private set; }
        public uint InfCount { get; private set; }
        public uint ErrCount { get; private set; }
        public uint WrnCount { get; private set; }
        public uint CriCount { get; private set; }

        public void Add(LogMetric metric)
        {
            DbgCount += metric.DbgCount;
            InfCount += metric.InfCount;
            ErrCount += metric.ErrCount;
            WrnCount += metric.WrnCount;
            CriCount += metric.CriCount;
        }
    }
}
