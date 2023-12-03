namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class OperationLogMetric
    {
        public OperationLogMetric(string operation, uint errCount, uint wrnCount, uint criCount,
            uint dbgCount, uint infCount, DateTimeOffset timestamp)
        {
            Operation = operation;
            Timestamp = timestamp;

            DbgCount = dbgCount;
            InfCount = infCount;
            ErrCount = errCount;
            WrnCount = wrnCount;
            CriCount = criCount;
        }
        public string Operation { get; set; }
        public DateTimeOffset Timestamp { get; private set; }

        public uint DbgCount { get; private set; }
        public uint InfCount { get; private set; }
        public uint ErrCount { get; private set; }
        public uint WrnCount { get; private set; }
        public uint CriCount { get; private set; }

        public void Add(OperationLogMetric other)
        {
            DbgCount += other.DbgCount;
            InfCount += other.InfCount;
            ErrCount += other.ErrCount;
            WrnCount += other.WrnCount;
            CriCount += other.CriCount;
        }
    }
}
