using System.Collections.Concurrent;
using System.Diagnostics;
using Juice.Extensions.Logging.EF.LogMetrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Logging.Metrics
{
    [ProviderAlias("Metrics")]
    internal class MetricsLoggerProvider : LoggerProvider
    {
        private TimeSpan _sampleRate = TimeSpan.FromSeconds(5);
        private IServiceScopeFactory _serviceScopeFactory;
        private IOptionsMonitor<MetricsLoggerOptions> _optionsMonitor;
        private ILogger _logger;
        private ConcurrentDictionary<Guid, LogMetric> _services = new();
        private ConcurrentDictionary<string, LogMetric> _operations = new();
        private ConcurrentDictionary<string, LogMetric> _categories = new();

        public MetricsLoggerProvider(IServiceScopeFactory serviceScopeFactory, IOptionsMonitor<MetricsLoggerOptions> optionsMonitor)
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
            _logger = IsolatedLoggerHelper.BuildLogger("MetricsLogger", _optionsMonitor.CurrentValue);
            if (!_optionsMonitor.CurrentValue.Disabled)
            {
                _backgroundTask = Task.Run(ExecuteAsync);
            }
            if (_optionsMonitor.CurrentValue.SampleRate.HasValue)
            {
                _sampleRate = _optionsMonitor.CurrentValue.SampleRate.Value;
            }
        }


        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
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
            var services = new List<ServiceLogMetric>();
            var allService = new LogMetric(0, 0, 0, 0, 0);
            foreach (var service in _services)
            {
                var m = service.Value.GetValue();
                if (m.TotalCount > 0)
                {
                    services.Add(new ServiceLogMetric(service.Key, m.ErrorCount, m.WarningCount, m.CriticalCount,
                        m.DebugCount, m.InfoCount, timestamp));
                    allService.Add(m);
                }
            }
            if (allService.TotalCount > 0)
            {
                services.Add(new ServiceLogMetric(Guid.Empty, allService.ErrorCount, allService.WarningCount, allService.CriticalCount,
                                                            allService.DebugCount, allService.InfoCount, timestamp));
            }
            var operations = new List<OperationLogMetric>();
            var allOperation = new LogMetric(0, 0, 0, 0, 0);
            foreach (var operation in _operations)
            {
                var m = operation.Value.GetValue();
                if (m.TotalCount > 0)
                {
                    operations.Add(new OperationLogMetric(operation.Key, m.ErrorCount, m.WarningCount, m.CriticalCount,
                        m.DebugCount, m.InfoCount, timestamp));
                    allOperation.Add(m);
                }
            }
            if (allOperation.TotalCount > 0)
            {
                operations.Add(new OperationLogMetric("Total", allOperation.ErrorCount, allOperation.WarningCount, allOperation.CriticalCount,
                                                              allOperation.DebugCount, allOperation.InfoCount, timestamp));
            }
            var categories = new List<CategoryLogMetric>();
            var allCategory = new LogMetric(0, 0, 0, 0, 0);
            foreach (var category in _categories)
            {
                var m = category.Value.GetValue();
                if (m.TotalCount > 0)
                {
                    categories.Add(new CategoryLogMetric(category.Key, m.ErrorCount, m.WarningCount, m.CriticalCount,
                        m.DebugCount, m.InfoCount, timestamp));
                    allCategory.Add(m);
                }
            }
            if (allCategory.TotalCount > 0)
            {
                categories.Add(new CategoryLogMetric("Total", allCategory.ErrorCount, allCategory.WarningCount, allCategory.CriticalCount,
                                           allCategory.DebugCount, allCategory.InfoCount, timestamp));
            }

            try
            {
                _clock.Restart();

                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<LogMetricsDbContext>();
                context.Database.BeginTransaction();
                foreach (var service in services)
                {
                    var existingService = await context.ServiceMetrics.FirstOrDefaultAsync(o => o.ServiceId == service.ServiceId && o.Timestamp == timestamp);
                    if (existingService != null)
                    {
                        existingService.Add(service);
                    }
                    else
                    {
                        context.ServiceMetrics.Add(service);
                    }
                }

                var opNames = operations.Select(o => o.Operation).Distinct().ToList();
                var existingOperations = await context.OperationMetrics.Where(o => opNames.Contains(o.Operation) && o.Timestamp == timestamp).ToListAsync();

                foreach (var operation in existingOperations)
                {
                    operation.Add(operations.First(o => o.Operation == operation.Operation));
                }
                var newOperations = operations.Where(o => !existingOperations.Any(e => e.Operation == o.Operation)).ToList();
                context.OperationMetrics.AddRange(newOperations);

                var catNames = categories.Select(o => o.Category).Distinct().ToList();
                var existingCategories = await context.CategoryMetrics.Where(o => catNames.Contains(o.Category) && o.Timestamp == timestamp).ToListAsync();
                foreach (var category in existingCategories)
                {
                    category.Add(categories.First(o => o.Category == category.Category));
                }
                var newCategories = categories.Where(o => !existingCategories.Any(e => e.Category == o.Category)).ToList();
                context.CategoryMetrics.AddRange(newCategories);

                await context.SaveChangesAsync();
                await context.Database.CommitTransactionAsync();
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
