using System.Collections.Concurrent;
using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Juice.Extensions.Logging.Grpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Logging.GrpcMetrics
{
    [ProviderAlias("GrpcMetrics")]
    internal class MetricsLoggerProvider : LoggerProvider
    {
        private TimeSpan _sampleRate = TimeSpan.FromSeconds(5);
        private IOptionsMonitor<MetricsLoggerOptions> _optionsMonitor;
        private ILogger _logger;
        private ConcurrentDictionary<Guid, LogMetric> _services = new();
        private ConcurrentDictionary<string, LogMetric> _operations = new();
        private ConcurrentDictionary<string, LogMetric> _categories = new();
        private GrpcChannel? _channel;

        public MetricsLoggerProvider(IOptionsMonitor<MetricsLoggerOptions> optionsMonitor)
        {
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
                    _shutdown = !_shutdown.IsCancellationRequested ? _shutdown : new CancellationTokenSource();
                    _backgroundTask = Task.Run(ExecuteAsync);
                }
            });
            _logger = IsolatedLoggerHelper.BuildLogger("MetricsLogger", _optionsMonitor.CurrentValue);
            var endpoint = _optionsMonitor.CurrentValue.Endpoint;
            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogWarning("Grpc endpoint is not configured. Grpc logger is disabled.");
            }
            else
            {
                _channel = GrpcChannel.ForAddress(new Uri(endpoint));
            }

            if (_optionsMonitor.CurrentValue.SampleRate.HasValue)
            {
                _sampleRate = _optionsMonitor.CurrentValue.SampleRate.Value;
            }
            if (!_optionsMonitor.CurrentValue.Disabled && _channel != null)
            {
                _backgroundTask = Task.Run(ExecuteAsync);
            }
        }
        private bool IsEnabled => _channel != null
            && _backgroundTask != null && !_backgroundTask.IsCompleted;

        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
        {
            if (_backgroundTask != null && !_backgroundTask.IsCompleted)
            {
                Guid? serviceId = default;
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
                        if (props.Any(p => p.Key == "Operation"))
                        {
                            operation = props.First(p => p.Key == "Operation").Value.ToString();
                        }
                    }
                }, entry.State);
                #endregion

                if (serviceId.HasValue)
                {
                    _services.AddOrUpdate(serviceId.Value, new LogMetric(entry.LogLevel), (key, value) => value.Track(entry.LogLevel));
                }
                if (!string.IsNullOrEmpty(operation))
                {
                    _operations.AddOrUpdate(operation, new LogMetric(entry.LogLevel), (key, value) => value.Track(entry.LogLevel));
                }
                _categories.AddOrUpdate(entry.Category, new LogMetric(entry.LogLevel), (key, value) => value.Track(entry.LogLevel));
            }
        }


        #region Service

        protected CancellationTokenSource _shutdown = new CancellationTokenSource();
        protected Task? _backgroundTask;
        protected Stopwatch _clock = Stopwatch.StartNew();

        private TimeSpan GetWaitTime()
        {
            var waitTime = _sampleRate.Ticks - (DateTimeOffset.Now.Ticks % _sampleRate.Ticks);
            if (waitTime > 0)
            {
                return TimeSpan.FromTicks(waitTime);
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Processing logs queue
        /// </summary>
        /// <returns></returns>
        protected async Task ExecuteAsync()
        {
            var waitTime = GetWaitTime();
            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, _shutdown.Token);
            }
            var start = DateTimeOffset.Now.Truncate(_sampleRate);
            while (!_shutdown.IsCancellationRequested)
            {
                await CollectAsync(start);
                start = start.Add(_sampleRate);
                try
                {
                    await Task.Delay(GetWaitTime(), _shutdown.Token);
                }
                catch (TaskCanceledException) { }
            }

            await CollectAsync(start);
        }

        private async Task CollectAsync(DateTimeOffset timestamp)
        {
            var metrics = new ConcurrentDictionary<(string, GrpcMetricType), GrpcMetric>();
            var allService = new LogMetric(0, 0, 0, 0, 0);
            foreach (var service in _services)
            {
                var m = service.Value.GetValue();
                if (m.TotalCount > 0)
                {
                    metrics.AddOrUpdate((service.Key.ToString(), GrpcMetricType.Service), key => new GrpcMetric
                    {
                        Name = service.Key.ToString(),
                        ErrCount = m.ErrorCount,
                        WrnCount = m.WarningCount,
                        CriCount = m.CriticalCount,
                        DbgCount = m.DebugCount,
                        InfCount = m.InfoCount,
                        Type = GrpcMetricType.Service
                    }, (key, current) =>
                    {
                        current.InfCount += m.InfoCount;
                        current.ErrCount += m.ErrorCount;
                        current.WrnCount += m.WarningCount;
                        current.CriCount += m.CriticalCount;
                        current.DbgCount += m.DebugCount;
                        return current;
                    });
                    allService.Add(m);
                }
            }
            if (allService.TotalCount > 0)
            {
                metrics.AddOrUpdate((Guid.Empty.ToString(), GrpcMetricType.Service),
                    key => new GrpcMetric
                    {
                        Name = Guid.Empty.ToString(),
                        ErrCount = allService.ErrorCount,
                        WrnCount = allService.WarningCount,
                        CriCount = allService.CriticalCount,
                        DbgCount = allService.DebugCount,
                        InfCount = allService.InfoCount,
                        Type = GrpcMetricType.Service
                    }, (key, current) =>
                    {
                        current.ErrCount += allService.ErrorCount;
                        current.WrnCount += allService.WarningCount;
                        current.CriCount += allService.CriticalCount;
                        current.DbgCount += allService.DebugCount;
                        current.InfCount += allService.InfoCount;
                        return current;
                    });
            }
            var allOperation = new LogMetric(0, 0, 0, 0, 0);
            foreach (var operation in _operations)
            {
                var m = operation.Value.GetValue();
                if (m.TotalCount > 0)
                {
                    metrics.AddOrUpdate((operation.Key, GrpcMetricType.Operation), key => new GrpcMetric
                    {
                        Name = operation.Key,
                        ErrCount = m.ErrorCount,
                        WrnCount = m.WarningCount,
                        CriCount = m.CriticalCount,
                        DbgCount = m.DebugCount,
                        InfCount = m.InfoCount,
                        Type = GrpcMetricType.Operation
                    }, (key, current) =>
                    {
                        current.InfCount += m.InfoCount;
                        current.ErrCount += m.ErrorCount;
                        current.WrnCount += m.WarningCount;
                        current.CriCount += m.CriticalCount;
                        current.DbgCount += m.DebugCount;
                        return current;
                    });
                    allOperation.Add(m);
                }
            }
            if (allOperation.TotalCount > 0)
            {
                metrics.AddOrUpdate(("Total", GrpcMetricType.Operation), new GrpcMetric
                {
                    Name = "Total",
                    ErrCount = allOperation.ErrorCount,
                    WrnCount = allOperation.WarningCount,
                    CriCount = allOperation.CriticalCount,
                    DbgCount = allOperation.DebugCount,
                    InfCount = allOperation.InfoCount,
                    Type = GrpcMetricType.Operation
                }, (key, current) =>
                {
                    current.InfCount += allOperation.InfoCount;
                    current.ErrCount += allOperation.ErrorCount;
                    current.WrnCount += allOperation.WarningCount;
                    current.CriCount += allOperation.CriticalCount;
                    current.DbgCount += allOperation.DebugCount;
                    return current;
                });
            }
            var allCategory = new LogMetric(0, 0, 0, 0, 0);
            foreach (var category in _categories)
            {
                var m = category.Value.GetValue();
                if (m.TotalCount > 0)
                {
                    metrics.AddOrUpdate((category.Key, GrpcMetricType.Category), new GrpcMetric
                    {
                        Name = category.Key,
                        ErrCount = m.ErrorCount,
                        WrnCount = m.WarningCount,
                        CriCount = m.CriticalCount,
                        DbgCount = m.DebugCount,
                        InfCount = m.InfoCount,
                        Type = GrpcMetricType.Category
                    }, (key, current) =>
                    {
                        current.InfCount += m.InfoCount;
                        current.ErrCount += m.ErrorCount;
                        current.WrnCount += m.WarningCount;
                        current.CriCount += m.CriticalCount;
                        current.DbgCount += m.DebugCount;
                        return current;
                    });
                    allCategory.Add(m);
                }
            }
            if (allCategory.TotalCount > 0)
            {
                metrics.AddOrUpdate(("Total", GrpcMetricType.Category), new GrpcMetric
                {
                    Name = "Total",
                    ErrCount = allCategory.ErrorCount,
                    WrnCount = allCategory.WarningCount,
                    CriCount = allCategory.CriticalCount,
                    DbgCount = allCategory.DebugCount,
                    InfCount = allCategory.InfoCount,
                    Type = GrpcMetricType.Category
                }, (key, current) =>
                {
                    current.InfCount += allCategory.InfoCount;
                    current.ErrCount += allCategory.ErrorCount;
                    current.WrnCount += allCategory.WarningCount;
                    current.CriCount += allCategory.CriticalCount;
                    current.DbgCount += allCategory.DebugCount;
                    return current;
                });
            }

            try
            {
                _clock.Restart();

                if (metrics.Any())
                {
                    var client = new MetricsWriter.MetricsWriterClient(_channel);
                    var request = new GrpcMetricsRequest
                    {
                        Metrics = { metrics.Values },
                        Timestamp = timestamp.ToTimestamp()
                    };
                    await client.WriteAsync(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending metrics");
            }
            finally
            {
                _clock.Stop();
                _logger.LogInformation("Log metrics collected in {Elapsed}", _clock.Elapsed);
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
