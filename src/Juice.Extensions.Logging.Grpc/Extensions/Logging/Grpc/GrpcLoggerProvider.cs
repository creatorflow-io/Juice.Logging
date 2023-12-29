using System.Collections.Concurrent;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Juice.MultiTenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Logging.Grpc
{
    [ProviderAlias("Grpc")]
    internal class GrpcLoggerProvider : LoggerProvider
    {
        private ConcurrentQueue<GrpcLogEntry> _logQueue = new();
        private TimeSpan _bufferTime = TimeSpan.FromSeconds(3);
        private ILogger _logger;
        private IOptionsMonitor<GrpcLoggerOptions> _optionsMonitor;
        private IHttpContextAccessor? _httpContextAccessor;
        private IServiceScopeFactory _serviceScopeFactory;
        private GrpcChannel? _channel;

        public GrpcLoggerProvider(
            IOptionsMonitor<GrpcLoggerOptions> optionsMonitor,
            IServiceScopeFactory serviceScopeFactory,
            IHttpContextAccessor? httpContextAccessor = default
            )
        {
            _serviceScopeFactory = serviceScopeFactory;
            _httpContextAccessor = httpContextAccessor;
            _optionsMonitor = optionsMonitor;
            _optionsMonitor.OnChange((options, _) =>
            {
                if (options.Disabled)
                {
                    _shutdown?.Cancel();
                    _backgroundTask?.Wait();
                    _backgroundTask = null;
                }
                else if (_backgroundTask == null)
                {
                    _bufferTime = options.BufferTime;
                    _shutdown = !_shutdown.IsCancellationRequested ? _shutdown : new CancellationTokenSource();
                    _backgroundTask = Task.Run(ExecuteAsync);
                }
            });
            _logger = IsolatedLoggerHelper.BuildLogger("GrpcLogger", _optionsMonitor.CurrentValue);
            _bufferTime = _optionsMonitor.CurrentValue.BufferTime;
            var endpoint = _optionsMonitor.CurrentValue.Endpoint;
            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogWarning("Grpc endpoint is not configured. Grpc logger is disabled.");
            }
            else
            {
                _channel = GrpcChannel.ForAddress(new Uri(endpoint));
            }
            if (!_optionsMonitor.CurrentValue.Disabled)
            {
                _backgroundTask = Task.Run(ExecuteAsync);
            }
        }

        private bool IsEnabled => _channel != null
            && _backgroundTask != null && !_backgroundTask.IsCompleted;

        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
        {
            if (IsEnabled)
            {
                Guid? serviceId = default;
                string? traceId = default;
                string? operation = default;

                #region Collect log scopes
                scopeProvider?.ForEachScope((value, loggingProps) =>
                {
                    if (value is IEnumerable<KeyValuePair<string, object>> props)
                    {
                        if (props.Any(p => p.Key == "ServiceId"))
                        {
                            serviceId = Guid.Parse(props.First(p => p.Key == "ServiceId").Value.ToString()!);
                        }
                        if (props.Any(p => p.Key == "TraceId"))
                        {
                            traceId = props.First(p => p.Key == "TraceId").Value.ToString();
                        }
                        if (props.Any(p => p.Key == "Operation"))
                        {
                            operation = props.First(p => p.Key == "Operation").Value.ToString();
                        }
                    }
                }, entry.State);
                #endregion

                var serviceProvider = _httpContextAccessor?.HttpContext?.RequestServices;
                if (serviceProvider == null)
                {
                    if (!string.IsNullOrEmpty(traceId))
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var tenantId = scope.ServiceProvider.GetService<ITenant>()?.Id ?? "";
                        _logQueue.Enqueue(new GrpcLogEntry
                        {
                            ServiceId = serviceId?.ToString(),
                            TraceId = traceId,
                            Operation = operation,
                            Category = entry.Category,
                            Message = formattedMessage,
                            Level = (int)entry.LogLevel,
                            Exception = entry.Exception?.StackTrace,
                            TenantId = tenantId,
                            Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
                        });
                    }
                }
                else
                {
                    traceId = traceId ?? _httpContextAccessor!.HttpContext!.TraceIdentifier;
                    var tenantId = serviceProvider.GetService<ITenant>()?.Id ?? "";
                    _logQueue.Enqueue(new GrpcLogEntry
                    {
                        ServiceId = serviceId?.ToString(),
                        TraceId = traceId,
                        Operation = operation,
                        Category = entry.Category,
                        Message = formattedMessage,
                        Level = (int)entry.LogLevel,
                        Exception = entry.Exception?.StackTrace,
                        TenantId = tenantId,
                        Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
                    });
                }
            }
        }


        #region Service


        protected CancellationTokenSource _shutdown = new CancellationTokenSource();
        protected Task? _backgroundTask;

        /// <summary>
        /// Processing logs queue
        /// </summary>
        /// <returns></returns>
        protected async Task ExecuteAsync()
        {

            while (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_bufferTime, _shutdown.Token);
                }
                catch (TaskCanceledException) { }

                await WriteFromQueueAsync();
            }

            await WriteFromQueueAsync();
        }

        /// <summary>
        /// Dequeue and write log entry to gRPC
        /// </summary>
        private async Task WriteFromQueueAsync()
        {
            var logs = new List<GrpcLogEntry>();
            while (_logQueue.TryDequeue(out var log))
            {
                logs.Add(log);
            }
            if (logs.Any() && _channel != null)
            {
                try
                {
                    var client = new LogWriter.LogWriterClient(_channel);
                    await client.LogAsync(new LogEntries
                    {
                        Entries = { logs }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while writing logs to grpc");
                }
            }
        }
        #endregion

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _shutdown?.Cancel();
                _backgroundTask?.Wait();
                _backgroundTask = null;
            }
        }
    }
}
