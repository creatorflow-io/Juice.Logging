using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.Extensions.Logging.EF.LogMetrics;
using Juice.Extensions.Logging.File;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using FileAPI = System.IO.File;

namespace Juice.Extensions.Logging.Tests.XUnit
{
    public class LoggingTests
    {
        private readonly ITestOutputHelper _output;

        public LoggingTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "Log file should by TraceId"), TestPriority(999)]
        public async Task LogFile_should_by_jobIdAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var logOptions = new FileLoggerOptions();
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddFileLogger(options =>
                    {
                        options.Directory = "C:\\Workspace\\Services\\logs";
                        options.BufferTime = TimeSpan.FromSeconds(1);
                        options.IncludeScopes = true;
                    })
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                configuration.GetSection("Logging:File").Bind(logOptions);
            });
            var logPath = logOptions.Directory;
            var logTime = logOptions.BufferTime.Add(TimeSpan.FromMilliseconds(200));
            logPath.Should().NotBeNullOrEmpty();

            var serviceProvider = resolver.ServiceProvider;
            var logger = serviceProvider.GetRequiredService<ILogger<LoggingTests>>();

            var guid = Guid.NewGuid().ToString();
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = guid
            }))
            {
                logger.LogInformation("Trace1 #1 Test 1 {state}", "Start");
                logger.LogInformation("Trace1 #1 Test 1 {state}", "Procssing");
            }

            await Task.Delay(logTime);
            var logFile = Path.Combine(logPath!, "General", $"{guid}.log");
            _output.WriteLine(logFile);
            FileAPI.Exists(logFile).Should().BeTrue();

            var newid = Guid.NewGuid().ToString();

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = newid,
                ["Operation"] = "xUnit",
                ["OperationState"] = "Succeeded"
            }))
            {
                logger.LogInformation("Trace2 Test 2 {state}", "End");
            }

            await Task.Delay(logTime);
            var logFile2 = Path.Combine(logPath!, "General", $"{newid} - xUnit_Succeeded.log");
            _output.WriteLine(logFile2);
            FileAPI.Exists(logFile2).Should().BeTrue();
            FileAPI.Exists(logFile).Should().BeTrue();
            using (logger.BeginScope("Scoped log outside the file"))
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = guid,
                ["OperationState"] = "Succeeded"
            }))
            {
                using (logger.BeginScope("Nested log"))
                {
                    logger.LogInformation("Trace1 #1 Test 3 {state}", "Inside");
                }
                logger.LogInformation("Trace1 #1 Test 3 {state}", "End");
            }
            await Task.Delay(logTime);
            var logFile3 = Path.Combine(logPath!, "General", $"{guid}_Succeeded.log");
            _output.WriteLine(logFile3);
            FileAPI.Exists(logFile3).Should().BeTrue();
            FileAPI.Exists(logFile).Should().BeFalse();
            logger.LogInformation("Log outside operation Trace1 #1 Test 3");

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = guid,
                ["OperationState"] = "Succeeded"
            }))
            {
                logger.LogInformation("Trace1 #2 Test 4 {state}", "Rerun");
                logger.LogInformation("Trace1 #2 Test 4 {state}", "End");
            }
            await Task.Delay(logTime);
            var logFile4 = Path.Combine(logPath!, "General", $"{guid}_Succeeded (1).log");
            _output.WriteLine(logFile4);
            FileAPI.Exists(logFile4).Should().BeTrue();
            FileAPI.Exists(logFile3).Should().BeTrue();
            logger.LogInformation("Log outside operation Trace1 #2 Test 4");

        }

        [IgnoreOnCIFact(DisplayName = "Log file by OperationState inside TraceId scope"), TestPriority(999)]

        public async Task LogFile_should_rename_by_jobStateAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var logOptions = new FileLoggerOptions();
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddFileLogger(configuration.GetSection("Logging:File"))
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                configuration.GetSection("Logging:File").Bind(logOptions);
            });
            var logPath = logOptions.Directory;
            var logTime = logOptions.BufferTime.Add(TimeSpan.FromMilliseconds(200));
            logPath.Should().NotBeNullOrEmpty();

            var serviceProvider = resolver.ServiceProvider;
            var logger = serviceProvider.GetRequiredService<ILogger<LoggingTests>>();

            var guid = Guid.NewGuid().ToString();
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = guid,
                ["Operation"] = "xUnit",
            }))
            {
                logger.LogInformation("Test {state}", "Start");
                logger.LogInformation("Test {state}", "Procssing");

                using (logger.BeginScope(new Dictionary<string, object>
                {
                    ["OperationState"] = "Succeeded"
                }))
                {
                    logger.LogInformation("Test {state}", "End");
                }
            }
            await Task.Delay(logTime);
            var logFile = Path.Combine(logPath!, "General", $"{guid} - xUnit.log");
            _output.WriteLine(logFile);
            FileAPI.Exists(logFile).Should().BeFalse();

            var logFile2 = Path.Combine(logPath!, "General", $"{guid} - xUnit_Succeeded.log");
            _output.WriteLine(logFile2);
            FileAPI.Exists(logFile2).Should().BeTrue();
            logger.LogInformation("Log outside operation");
            await Task.Delay(logTime);
        }

        [IgnoreOnCIFact(DisplayName = "Log file by TraceId scope"), TestPriority(999)]

        public async Task LogFile_should_rename_by_jobAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var logOptions = new FileLoggerOptions();
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddFileLogger(configuration.GetSection("Logging:File"))
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                configuration.GetSection("Logging:File").Bind(logOptions);
            });
            var logPath = logOptions.Directory;
            var logTime = logOptions.BufferTime.Add(TimeSpan.FromMilliseconds(200));
            logPath.Should().NotBeNullOrEmpty();

            var serviceProvider = resolver.ServiceProvider;
            var logger = serviceProvider.GetRequiredService<ILogger<LoggingTests>>();

            var guid = Guid.NewGuid().ToString();
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = guid,
                ["Operation"] = "xUnit",
            }))
            {
                logger.LogInformation("Test {state}", "Start");
                logger.LogInformation("Test {state}", "Procssing");
                logger.LogInformation("Test {state}", "End without OperationState scope");

            }
            await Task.Delay(logTime);
            var logFile = Path.Combine(logPath!, "General", $"{guid} - xUnit.log");
            _output.WriteLine(logFile);
            FileAPI.Exists(logFile).Should().BeTrue();

            var logFile2 = Path.Combine(logPath!, "General", $"{guid} - xUnit_Succeeded.log");
            _output.WriteLine(logFile2);
            FileAPI.Exists(logFile2).Should().BeFalse();
            logger.LogInformation("Log outside operation");
            await Task.Delay(logTime);
        }


        [IgnoreOnCIFact(DisplayName = "Log should multiple scopes"), TestPriority(999)]

        public async Task LogFile_should_multiple_scopesAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var logOptions = new FileLoggerOptions();
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddFileLogger(options =>
                    {
                        options.Directory = "C:\\Workspace\\Services\\logs";
                        options.BufferTime = TimeSpan.FromSeconds(1);
                        options.IncludeScopes = true;
                    })
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                configuration.GetSection("Logging:File").Bind(logOptions);
            });
            var logPath = logOptions.Directory;
            var logTime = logOptions.BufferTime.Add(TimeSpan.FromMilliseconds(200));
            logPath.Should().NotBeNullOrEmpty();

            var serviceProvider = resolver.ServiceProvider;
            var logger = serviceProvider.GetRequiredService<ILogger<LoggingTests>>();

            using (logger.BeginScope("Scope 1"))
            {
                logger.LogInformation("Test {state}", "Start");
                logger.LogInformation("Test {state}", "Procssing");

                using (logger.BeginScope("Scope 1.1"))
                {
                    logger.LogInformation("Test {state}", "Child log");
                }

                using (logger.BeginScope("Scope 1.2"))
                {
                    logger.LogInformation("Test {state}", "Child log");
                    using (logger.BeginScope("Scope 1.2.1"))
                    {
                        logger.LogInformation("Test {state}", "Child log");
                    }
                }

                using (logger.BeginScope(new string[] { "Scope 1.3", "Scope 1.3.1" }))
                {
                    logger.LogInformation("Test {state}", "Child log");
                }
            }

            logger.LogInformation("Test {state}", "End");

        }

        [Fact]
        public async Task Datetime_should_truncateAsync()
        {
            for (var i = 0; i < 10; i++)
            {
                var now = DateTimeOffset.UtcNow;
                var now2 = now.Truncate(TimeSpan.FromSeconds(5));
                _output.WriteLine($"{now} => {now2}");
                now2.Should().Be(now.AddTicks(-(now.Ticks % TimeSpan.FromSeconds(5).Ticks)));
                await Task.Delay(1000);
            }
        }
    }
}
