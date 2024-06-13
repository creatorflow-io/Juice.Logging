using Juice.Extensions.Logging.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace Juice.Extensions.Logging.Tests.Host
{
    public class LogHub : Hub<ILogClient1>
    {
        public async Task LoggingAsync(Guid serviceId, string? traceId, string category, string message, LogLevel level, string? contextual, object[] scopes)
        {
            await Clients.Others.LoggingAsync(serviceId, traceId, category, message, level, contextual, scopes);
        }

        //public async Task LoggingAsync(Guid senderId, string jobId, string message, LogLevel level, string? contextual)
        //{
        //    await Clients.Others.LoggingAsync(senderId, jobId, message, level, contextual);
        //}

        public async Task StateAsync(Guid serviceId, string? traceId, string state, string message)
        {
            await Clients.Others.StateAsync(serviceId, traceId, state, message);
        }
        public async Task BeginScopeAsync(Guid serviceId, string? traceId, string category, object scope)
        {
            await Clients.Others.BeginScopeAsync(serviceId, traceId, category, scope);
        }
        public async Task EndScopeAsync(Guid serviceId, string? traceId, string category, object scope)
        {
            await Clients.Others.EndScopeAsync(serviceId, traceId, category, scope);
        }
    }
    public interface ILogClient1: ILogClient
    {
        Task LoggingAsync(Guid senderId, string jobId, string message, LogLevel level, string? contextual);
    }
}
