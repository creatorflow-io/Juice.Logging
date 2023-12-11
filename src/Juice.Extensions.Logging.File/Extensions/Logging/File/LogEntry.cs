using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.File
{
    internal record LogEntry
    {
        public LogEntry(DateTimeOffset logTime, string cateogry, string message, LogLevel logLevel, Exception? exception)
        {
            Timestamp = logTime;
            Message = message;
            Category = cateogry;
            LogLevel = logLevel;
            Exception = exception;
        }
        public Guid ServiceId { get; set; }
        public DateTimeOffset Timestamp { get; init; }
        public string Message { get; init; }
        public string Category { get; init; }
        public LogLevel LogLevel { get; init; }
        public Exception? Exception { get; protected set; }
    }

}
