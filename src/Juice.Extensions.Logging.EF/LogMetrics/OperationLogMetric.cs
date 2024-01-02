namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class OperationLogMetric : LogMetric, IEquatable<OperationLogMetric>
    {
        public OperationLogMetric(string operation, uint errCount, uint wrnCount, uint criCount,
            uint dbgCount, uint infCount, DateTimeOffset timestamp)
            : base(errCount, wrnCount, criCount, dbgCount, infCount, timestamp)
        {
            Operation = operation;
        }
        public string Operation { get; set; }

        public bool Equals(OperationLogMetric? other)
            => Operation.Equals(other?.Operation) && Timestamp.Equals(other?.Timestamp);
    }
}
