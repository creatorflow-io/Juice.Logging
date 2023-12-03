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
        public string? FileName { get; protected set; }
        public string? State { get; protected set; }
        public Exception? Exception { get; protected set; }
        public List<LogScope>? Scopes { get; protected set; }
        public void PushScope(LogScope scope)
        {
            if (Scopes == null)
            {
                Scopes = new List<LogScope>();
            }
            Scopes.Add(scope);
        }
        public void ForkNewFile(string name)
        {
            FileName = name;
        }
        public void SetState(string? state)
        {
            State = state;
        }

        public List<string> GetScopes()
        {
            return Scopes?.Where(s => !string.IsNullOrEmpty(s.Scope))
                 .Select(s => s.Scope!)?.ToList() ?? new List<string>();
        }
    }

    internal class LogScope
    {
        public string? Scope { get; init; }
        public Dictionary<string, object>? Properties { get; set; }
    }
}
