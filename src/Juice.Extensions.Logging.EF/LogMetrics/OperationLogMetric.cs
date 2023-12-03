namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class OperationLogMetric
    {
        public OperationLogMetric(string operation, uint errors, uint warnings, uint criticals, DateTimeOffset timestamp)
        {
            Operation = operation;
            Timestamp = timestamp;

            Errors = errors;
            Warnings = warnings;
            Criticals = criticals;
        }
        public string Operation { get; set; }
        public DateTimeOffset Timestamp { get; private set; }

        public uint Errors { get; private set; }
        public uint Warnings { get; private set; }
        public uint Criticals { get; private set; }
    }
}
