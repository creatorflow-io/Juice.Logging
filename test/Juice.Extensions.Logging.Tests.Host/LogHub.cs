using Juice.Extensions.Logging.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace Juice.Extensions.Logging.Tests.Host
{
    public class LogHub : Hub<ILogClient>
    {
        public async Task LoggingAsync(Guid serviceId, string? traceId, string category, string message, LogLevel level, string? contextual, string[] scopes)
        {
            await Clients.Others.LoggingAsync(serviceId, traceId, category, message, level, contextual, scopes);
        }
        public async Task StateAsync(Guid serviceId, string? jobId, string state, string message)
        {
            await Clients.Others.StateAsync(serviceId, jobId, state, message);
        }
    }
}
