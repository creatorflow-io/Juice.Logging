﻿using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.SignalR
{
    internal class SignalRLogger : IDisposable
    {
        private HubConnection? _connection;
        private SignalRLoggerOptions _options;
        private string _channel;
        private ILogger _logger;

        public SignalRLogger(string channel, SignalRLoggerOptions options)
        {
            _options = options;
            if (!options.Disabled)
            {
                if (string.IsNullOrEmpty(options.HubUrl)) { throw new ArgumentException("SignalR:HubUrl is null or empty"); }
                if (options.Directory == null) { throw new ArgumentException("SignalR:Directory is null"); }

                _logger = IsolatedLoggerHelper.BuildLogger("SignalRLogger", _options);
            }
            _channel = channel;
        }

        private HubConnection BuildConnection()
        {
            return new HubConnectionBuilder()
                    .WithUrl(_options.HubUrl)
                    .WithAutomaticReconnect()
                    .AddJsonProtocol(options =>
                    {
                        options.PayloadSerializerOptions.PropertyNamingPolicy = null;
                    })
                    .ConfigureLogging(builder =>
                    {
                        IsolatedLoggerHelper.ConfigureLogger(builder, "SignalRLoggerHubConnection", _options);
                    })
                    .Build()
                    ;
        }

        public async Task BeginScopeAsync<TState>(Guid serviceId, string? traceId, string category, TState scope)
        {
            if (_options.Disabled || _connection == null || _connection.State != HubConnectionState.Connected)
            {
                return;
            }
            try
            {
                await _connection.SendAsync(nameof(ILogClient.BeginScopeAsync), serviceId, traceId, category, scope);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalRLogger: {message}", ex.Message);
            }
        }

        public async Task EndScopeAsync<TState>(Guid serviceId, string? traceId, string category, TState scope)
        {
            if (_options.Disabled || _connection == null || _connection.State != HubConnectionState.Connected)
            {
                return;
            }
            try
            {
                await _connection.SendAsync(nameof(ILogClient.EndScopeAsync), serviceId, traceId, category, scope);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalRLogger: {message}", ex.Message);
            }
        }

        public async Task LoggingAsync(Guid serviceId, string? traceId, string category, string message, LogLevel level, string? contextual, object[] scopes)
        {
            if (_options.Disabled || _connection == null || _connection.State != HubConnectionState.Connected)
            {
                return;
            }
            try
            {
                var method = _options.LogMethod ?? nameof(ILogClient.LoggingAsync);
                if (_options.IncludeScopes)
                {
                    await _connection.SendAsync(method, serviceId, traceId, category, message, level, contextual, scopes);
                }
                else
                {
                    await _connection.SendAsync(method, serviceId, traceId, message, level, contextual);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalRLogger: {message}", ex.Message);
            }
        }

        public async Task StateAsync(Guid serviceId, string? traceId, string state, string message)
        {
            if (_options.Disabled || _connection == null || _connection.State != HubConnectionState.Connected)
            {
                return;
            }
            try
            {
                var method = _options.StateMethod ?? nameof(ILogClient.StateAsync);

                await _connection.SendAsync(method, serviceId, traceId, state, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalRLogger: {message}", ex.Message);
            }
        }

        #region Service


        protected CancellationTokenSource? _shutdown;
        protected Task? _backgroundTask;

        /// <summary>
        /// Start background task to processing logs queue
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_options.Disabled)
            {
                return;
            }
            _logger.LogInformation("SignalRLogger: {state}", "Starting");

            _connection = BuildConnection();
            try
            {
                await _connection.StartAsync(cancellationToken);
                if (!string.IsNullOrEmpty(_options.JoinGroupMethod))
                {
                    await _connection.InvokeAsync(_options.JoinGroupMethod, _channel, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalRLogger: {message}", ex.Message);
            }

            // Create a linked token so we can trigger cancellation outside of this token's cancellation
            _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(ExecuteAsync);

        }

        /// <summary>
        /// Stop background task
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop called without start
            if (_backgroundTask == null || _backgroundTask.IsCompleted)
            {
                return;
            }
            _logger.LogInformation("SignalRLogger: {0}", "Stopping");
            // Signal cancellation to the executing method
            _shutdown?.Cancel();

            // Wait until the task completes or the stop token triggers

            await Task.WhenAny(_backgroundTask, Task.Delay(5000, cancellationToken));

            // Throw if cancellation triggered
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Monitor the connection state and reconnect if the connection is lost
        /// </summary>
        /// <returns></returns>
        protected async Task ExecuteAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), _shutdown.Token);
            }
            catch (TaskCanceledException) { }

            while (!_shutdown!.IsCancellationRequested)
            {
                try
                {
                    if (_connection!.State == HubConnectionState.Disconnected)
                    {
                        try
                        {
                            await _connection.StartAsync(_shutdown.Token);

                            if (!string.IsNullOrEmpty(_options.JoinGroupMethod))
                            {
                                await _connection.InvokeAsync(_options.JoinGroupMethod, _channel, _shutdown.Token);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "SignalRLogger: {message}", ex.Message);
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(60), _shutdown.Token);

                }
                catch (TaskCanceledException) { }
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
                    //  dispose managed state (managed objects).
                    try
                    {
                        if (_connection != null)
                        {
                            Task.WaitAny(_connection.DisposeAsync().AsTask());
                        }
                    }
                    catch { }
                }
                disposedValue = true;
            }
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
