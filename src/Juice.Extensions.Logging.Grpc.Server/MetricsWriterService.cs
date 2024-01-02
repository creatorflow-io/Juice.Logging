using System.Collections.Concurrent;
using Juice.Extensions.Logging.EF.LogMetrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.Grpc.Server
{
    internal class MetricsWriterService : IHostedService
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _backgroundTask;
        private IServiceScopeFactory _scopeFactory;
        private ConcurrentQueue<GrpcMetricsRequest> _metrics = new();
        private ILogger<MetricsWriterService> _logger;
        public MetricsWriterService(IServiceScopeFactory scopeFactory, ILogger<MetricsWriterService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _backgroundTask = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        await ProcessMetricsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing metrics");
                    }
                    await Task.Delay(5000);
                }
                _logger.LogInformation("Metric writer stopped");
            });
            _logger.LogInformation("Metric writer started");
            return Task.CompletedTask;
        }

        private async Task ProcessMetricsAsync()
        {
            while (_metrics.TryDequeue(out var metric))
            {
                var cMetrics = new HashSet<CategoryLogMetric>();
                var oMetrics = new HashSet<OperationLogMetric>();
                var sMetrics = new HashSet<ServiceLogMetric>();

                var timestamp = metric.Timestamp.ToDateTimeOffset();

                foreach (var m in metric.Metrics.Where(m => m.Type == GrpcMetricType.Category))
                {
                    var cMetric = new CategoryLogMetric(
                            m.Name,
                            m.ErrCount, m.WrnCount, m.CriCount, m.DbgCount, m.InfCount,
                            timestamp
                            );
                    if (!cMetrics.Add(cMetric))
                    {
                        var current = cMetrics.First(c => c.Equals(cMetric));
                        current.Add(cMetric);
                    }
                }

                foreach (var m in metric.Metrics.Where(m => m.Type == GrpcMetricType.Operation))
                {
                    var oMetric = new OperationLogMetric(
                            m.Name,
                            m.ErrCount, m.WrnCount, m.CriCount, m.DbgCount, m.InfCount,
                            timestamp);

                    if (!oMetrics.Add(oMetric))
                    {
                        var current = oMetrics.First(o => o.Equals(oMetric));
                        current.Add(oMetric);
                    }
                }

                foreach (var m in metric.Metrics.Where(m => m.Type == GrpcMetricType.Service))
                {
                    var sMetric = new ServiceLogMetric(
                            Guid.Parse(m.Name),
                            m.ErrCount, m.WrnCount, m.CriCount, m.DbgCount, m.InfCount,
                            timestamp);

                    if (!sMetrics.Add(sMetric))
                    {
                        var current = sMetrics.First(s => s.Equals(sMetric));
                        current.Add(sMetric);
                    }
                }

                if (cMetrics.Count == 0 && oMetrics.Count == 0 && sMetrics.Count == 0)
                {
                    _logger.LogDebug("No metrics to write");
                    return;
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<LogMetricsDbContext>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<MetricsWriterService>>();
                    context.Database.BeginTransaction();
                    foreach (var service in sMetrics)
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

                    var opNames = oMetrics.Select(o => o.Operation).Distinct().ToList();
                    var existingOperations = await context.OperationMetrics.Where(o => opNames.Contains(o.Operation) && o.Timestamp == timestamp).ToListAsync();

                    foreach (var operation in existingOperations)
                    {
                        operation.Add(oMetrics.First(o => o.Operation == operation.Operation));
                    }
                    var newOperations = oMetrics.Where(o => !existingOperations.Any(e => e.Operation == o.Operation)).ToList();
                    context.OperationMetrics.AddRange(newOperations);

                    var catNames = cMetrics.Select(o => o.Category).Distinct().ToList();
                    var existingCategories = await context.CategoryMetrics.Where(o => catNames.Contains(o.Category) && o.Timestamp == timestamp).ToListAsync();
                    foreach (var category in existingCategories)
                    {
                        category.Add(cMetrics.First(o => o.Category == category.Category));
                    }
                    var newCategories = cMetrics.Where(o => !existingCategories.Any(e => e.Category == o.Category)).ToList();
                    context.CategoryMetrics.AddRange(newCategories);

                    var afftected = await context.SaveChangesAsync();
                    await context.Database.CommitTransactionAsync();
                    logger.LogInformation($"Wrote {afftected} metrics");
                }
                await Task.Delay(100);
            }

            _logger.LogDebug("No more metrics to write");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        public void AddMetrics(GrpcMetricsRequest metrics)
        {
            _metrics.Enqueue(metrics);
        }
    }
}
