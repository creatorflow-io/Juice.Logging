using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.SignalR
{

    /// <summary>
    /// The client interface for the SignalR logger with default methods name.
    /// </summary>
    public interface ILogClient
    {
        Task LoggingAsync(Guid serviceId, string? traceId, string category, string message, LogLevel level, string? contextual, string[] scopes);
        Task StateAsync(Guid serviceId, string? traceId, string state, string message);
    }
}
