namespace Juice.Extensions.Logging.Tests.Host
{
    public class LogService : IHostedService
    {
        private ILogger<LogService> _logger;
        private Task? _task;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public LogService(ILogger<LogService> logger)
        {
            _logger = logger;

        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>{
                { "ServiceId", Guid.NewGuid() },
                { "ServiceName", "LogService" }
            });
            _task = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    using (_logger.BeginScope(new Dictionary<string, object>
                    {
                        ["JobId"] = Guid.NewGuid(),
                        ["JobDescription"] = "xUnit",
                    }))
                    {
                        _logger.LogInformation("Test {state}", "Start");
                        _logger.LogInformation("Test {state}", "Procssing");
                        for (var i = 0; i < 10; i++)
                        {
                            if (i == 9)
                            {
                                using (_logger.BeginScope(new Dictionary<string, object>
                                {
                                    ["Contextual"] = "success"
                                }))
                                {
                                    _logger.LogInformation("Test {state}", "last task");
                                }
                            }
                            else if (new Random().Next(8) == i)
                            {
                                using (_logger.BeginScope(new Dictionary<string, object>
                                {
                                    ["Contextual"] = "danger"
                                }))
                                {
                                    _logger.LogInformation("Test {state}", "failure task");
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Test task {i}", i);
                            }
                            await Task.Delay(1000);
                        }

                        using (_logger.BeginScope(new Dictionary<string, object>
                        {
                            ["JobState"] = "Succeeded"
                        }))
                        {
                            _logger.LogInformation("Test {state}", "End");
                        }
                    }

                    await Task.Delay(5000);
                }
            });
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }
    }
}
