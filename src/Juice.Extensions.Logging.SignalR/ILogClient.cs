using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.SignalR
{

    /// <summary>
    /// The client interface for the SignalR logger with default methods name.
    /// </summary>
    public interface ILogClient
    {
        Task BeginScopeAsync(Guid serviceId, string? traceId, string category, object scope);
        Task EndScopeAsync(Guid serviceId, string? traceId, string category, object scope);
        Task LoggingAsync(Guid serviceId, string? traceId, string category, string message, LogLevel level, string? contextual, object[] scopes);
        Task StateAsync(Guid serviceId, string? traceId, string state, string message);
    }
}
