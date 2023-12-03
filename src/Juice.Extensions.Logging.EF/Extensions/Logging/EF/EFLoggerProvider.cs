using System.Collections.Concurrent;
using Juice.Extensions.Logging.EF.LogEntries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Logging.EF
{
    [ProviderAlias("Db")]
    internal class EFLoggerProvider : LoggerProvider
    {
        private ConcurrentQueue<LogEntry> _logQueue = new();
        private TimeSpan _bufferTime = TimeSpan.FromSeconds(5);
        private IServiceScopeFactory _serviceScopeFactory;
        private IOptionsMonitor<EFLoggerOptions> _optionsMonitor;
        private ILogger _logger;

        public EFLoggerProvider(IServiceScopeFactory serviceScopeFactory, IOptionsMonitor<EFLoggerOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
            _serviceScopeFactory = serviceScopeFactory;
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
                    _shutdown = !_shutdown.IsCancellationRequested ? _shutdown : new CancellationTokenSource();
                    _backgroundTask = Task.Run(ExecuteAsync);
                }
            });
            _logger = IsolatedLoggerHelper.BuildLogger("DBLogger", _optionsMonitor.CurrentValue);

            if (!_optionsMonitor.CurrentValue.Disabled)
            {
                _backgroundTask = Task.Run(ExecuteAsync);
            }
            _bufferTime = _optionsMonitor.CurrentValue.BufferTime;
        }

        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
        {
            Guid? serviceId = default;
            string? traceId = default;
            string? operation = default;
            string? state = default;

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
                    if (props.Any(p => p.Key == "OperationState"))
                    {
                        state = props.First(p => p.Key == "OperationState").Value.ToString();
                    }
                }
            }, entry.State);
            #endregion

            if (serviceId.HasValue && !string.IsNullOrEmpty(traceId))
            {
                _logQueue.Enqueue(new LogEntry(serviceId.Value, traceId, operation, entry.Category, formattedMessage, entry.LogLevel, entry.Exception?.StackTrace));
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
                await WriteFromQueueAsync();
                try
                {
                    await Task.Delay(_bufferTime, _shutdown.Token);
                }
                catch (TaskCanceledException) { }
            }

            await WriteFromQueueAsync();
        }

        /// <summary>
        /// Dequeue and write log entry to DB
        /// </summary>
        private async Task WriteFromQueueAsync()
        {
            var logs = new List<LogEntry>();
            while (_logQueue.TryDequeue(out var log))
            {
                logs.Add(log);
            }
            if (logs.Any())
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<LogDbContext>();
                    context.Logs.AddRange(logs);
                    await context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while writing logs to database");
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
