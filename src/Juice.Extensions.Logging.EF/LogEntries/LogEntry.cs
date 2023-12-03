using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.EF.LogEntries
{
    public class LogEntry
    {
        public LogEntry(Guid serviceId, string traceId, string? operation, string category, string message, LogLevel level, string? exception)
        {
            Category = category;
            Message = message;
            Level = level;
            Exception = exception;
            ServiceId = serviceId;
            TraceId = traceId;
            Operation = operation;
        }

        public Guid Id { get; set; }
        public DateTimeOffset Timestamp { get; private set; } = DateTimeOffset.UtcNow;
        public string Category { get; private set; }
        public string Message { get; private set; }
        public LogLevel Level { get; private set; }
        public string? Exception { get; private set; }
        public Guid ServiceId { get; private set; }
        public string TraceId { get; private set; }
        public string? Operation { get; private set; }
    }
}
