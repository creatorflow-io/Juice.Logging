using Juice.Extensions.Logging.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Logging.Tests.Host
{
    internal class LogClient : ILogClient, IHostedService, IDisposable
    {
        private IOptionsMonitor<SignalRLoggerOptions> _optionsMonitor;
        private SignalRLoggerOptions Options => _optionsMonitor.CurrentValue;

        private IDictionary<string, HubConnection> _serverConnections = new Dictionary<string, HubConnection>();

        private ILogger _logger;
        private Task? _connectionTask;
        private CancellationTokenSource _shutdown = new CancellationTokenSource();
        private string _logMethod;
        private string _stateMethod;

        public LogClient(IOptionsMonitor<SignalRLoggerOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
            _logMethod = Options.LogMethod ?? nameof(ILogClient.LoggingAsync);
            _stateMethod = Options.StateMethod ?? nameof(ILogClient.StateAsync);
            _logger = IsolatedLoggerHelper.BuildLogger("SignalRLoggerClient", Options);
            optionsMonitor.OnChange((o, s) =>
            {
                if(o.Disabled && _connectionTask != null)
                {
                    _shutdown.Cancel();
                }else if(!o.Disabled && _connectionTask == null)
                {
                    _shutdown = new CancellationTokenSource();
                    _connectionTask = Task.Run(CheckConnectionAsync);
                }
            });
        }

        #region ILogClient
        public Task BeginScopeAsync(Guid serviceId, string? traceId, string category, object scope)
            => SendAsync(nameof(ILogClient.BeginScopeAsync), new object?[] { serviceId, traceId, category, scope });
        public Task EndScopeAsync(Guid serviceId, string? traceId, string category, object scope)
            => SendAsync(nameof(ILogClient.EndScopeAsync), new object?[] { serviceId, traceId, category, scope });
        public Task LoggingAsync(Guid serviceId, string? traceId, string category, string message, LogLevel level, string? contextual, object[] scopes)
            => Options.IncludeScopes ?
            SendAsync(_logMethod, new object?[] { serviceId, traceId, category, message, level, contextual, scopes })
            : SendAsync(_logMethod, new object?[] { serviceId, traceId, message, level, contextual });
        public Task StateAsync(Guid serviceId, string? traceId, string state, string message)
            => SendAsync(_stateMethod, new object?[] { serviceId, traceId, state, message });

        private async Task<bool> SendAsync(string method, object?[] args)
        {
            if(Options.Disabled)
            {
                return false;
            }
            var sent = false;
            
            foreach (var connection in _serverConnections.Values)
            {
                sent = await SendAsync(connection, method, args) || sent;
            }
            return sent;
        }
        private async Task<bool> SendAsync(HubConnection connection, string method, object?[] args)
        {
            if (connection != null && connection.State == HubConnectionState.Connected)
            {
                switch (args.Length)
                {
                    case 1:
                        await connection.SendAsync(method, args[0]);
                        break;
                    case 2:
                        await connection.SendAsync(method, args[0], args[1]);
                        break;
                    case 3:
                        await connection.SendAsync(method, args[0], args[1], args[2]);
                        break;
                    case 4:
                        await connection.SendAsync(method, args[0], args[1], args[2], args[3]);
                        break;
                    case 5:
                        await connection.SendAsync(method, args[0], args[1], args[2], args[3], args[4]);
                        break;
                    case 6:
                        await connection.SendAsync(method, args[0], args[1], args[2], args[3], args[4], args[5]);
                        break;
                    case 7:
                        await connection.SendAsync(method, args[0], args[1], args[2], args[3], args[4], args[5], args[6]);
                        break;
                    default:
                        await connection.SendAsync(method);
                        break;
                }
                return true;
            }
            return false;
        }
        #endregion

        #region IHostedService
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service is starting.");
            if (!Options.Disabled)
            {
                _connectionTask = Task.Run(CheckConnectionAsync);
            }
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service is stopping.");
            _shutdown.Cancel();
            if (_connectionTask != null)
            {
                return Task.WhenAny(_connectionTask,
                    Task.Delay(Timeout.Infinite, cancellationToken));
            }
            return Task.CompletedTask;
        }

        #endregion

        #region SignalR Connections

        private HubConnection BuildConnection(string endpoint)
        {
            return new HubConnectionBuilder()
                    .WithUrl(endpoint)
                    .AddJsonProtocol(options =>
                    {
                        options.PayloadSerializerOptions.PropertyNamingPolicy = null;
                    })
                    .ConfigureLogging(builder =>
                    {
                        IsolatedLoggerHelper.ConfigureLogger(builder, "SignalRLoggerHubConnection", Options, LogLevel.None);
                    })
                    .Build()
                    ;

        }
        private async Task EnsureConnectedAsync(string endpoint)
        {
            if (!_serverConnections.ContainsKey(endpoint))
            {
                _serverConnections[endpoint] = BuildConnection(endpoint);
                _logger.LogInformation($"Connecting to {endpoint}");
                await ConnectAsync(_serverConnections[endpoint]);
            }
            else if (_serverConnections[endpoint].State == HubConnectionState.Disconnected)
            {
                _logger.LogInformation($"Reconnecting to {endpoint}");
                await ConnectAsync(_serverConnections[endpoint]);
            }
        }
        private async Task ConnectAsync(HubConnection connection)
        {
            try
            {
                await connection.StartAsync();
                _logger.LogInformation($"Connected");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Connection error. {ex.Message}");
            }
        }
        protected async Task CheckConnectionAsync()
        {
            try
            {
                while (!_shutdown.IsCancellationRequested)
                {
                    try
                    {
                        var hubUrls = Options.HubUrl?.Split(';')?? Array.Empty<string>();
                        foreach (var endpoint in hubUrls)
                        {
                            await EnsureConnectedAsync(endpoint);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(5), _shutdown.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[SignalrLoggerClient] Service run error. {ex.Message}");
                        _logger.LogError($"[SignalrLoggerClient] Service run error. {ex.StackTrace}");
                    }

                }
                foreach (var connection in _serverConnections.Values)
                {
                    await connection.DisposeAsync();
                }
            }
            catch (Exception)
            {
                if (_shutdown.IsCancellationRequested)
                {
                    _logger.LogError($"Service stopped.");
                }
                else
                {
                    throw;
                }
            }
        }
        #endregion


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                    _logger.LogInformation("[SignalrLoggerClient] Disposing");
                    _shutdown.Cancel();
                }

                disposedValue = true;
            }
        }

        // override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~LogClient()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        #endregion
    }
}
