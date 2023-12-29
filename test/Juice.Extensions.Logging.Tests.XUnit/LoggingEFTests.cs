using Finbuckle.MultiTenant;
using FluentAssertions;
using Juice.EF.Extensions;
using Juice.Extensions.DependencyInjection;
using Juice.Extensions.Logging.EF.LogEntries;
using Juice.Extensions.Logging.EF.LogMetrics;
using Juice.MultiTenant;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.Extensions.Logging.Tests.XUnit
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class LoggingEFTests
    {
        private readonly ITestOutputHelper _output;

        public LoggingEFTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCITheory(DisplayName = "Migrations"), TestPriority(999)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task LogDbContext_should_migrate_Async(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                // Register DbContext class

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddDbLogger(logOptions =>
                    {
                        configuration.GetSection("Logging:Db").Bind(logOptions);
                    }, dbOptions =>
                    {
                        configuration.GetSection("Logging:Db").Bind(dbOptions);
                        dbOptions.DatabaseProvider = provider;
                    }, configuration)
                    .AddMetricsLogger(logOptions =>
                    {
                        configuration.GetSection("Logging:Metrics").Bind(logOptions);
                    }, dbOptions =>
                    {
                        configuration.GetSection("Logging:Metrics").Bind(dbOptions);
                        dbOptions.DatabaseProvider = provider;
                    }, configuration)
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });
            using var scope = resolver.ServiceProvider.
                CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LogDbContext>();
            await context.MigrateAsync();

            var metricContext = scope.ServiceProvider.GetRequiredService<LogMetricsDbContext>();
            await metricContext.MigrateAsync();
        }

        [IgnoreOnCITheory(DisplayName = "LogDbContext should log to database")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task LogDbContext_should_log_to_databaseAsync(string provider)
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, 5), async (i, ct) =>
            {
                var resolver = new DependencyResolver
                {
                    CurrentDirectory = AppContext.BaseDirectory
                };
                resolver.ConfigureServices(services =>
                {
                    var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                    var configuration = configService.GetConfiguration();
                    // Register DbContext class

                    services.AddDefaultStringIdGenerator();
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
                        .AddDbLogger(logOptions =>
                        {
                            configuration.GetSection("Logging:Db").Bind(logOptions);
                            logOptions.BufferTime = TimeSpan.FromSeconds(1);
                        }, dbOptions =>
                        {
                            configuration.GetSection("Logging:Db").Bind(dbOptions);
                            dbOptions.DatabaseProvider = provider;
                        }, configuration)
                        .AddConfiguration(configuration.GetSection("Logging"));
                    });

                    services.AddScoped(sp =>
                    {
                        var id = i % 2 == 0 ? "TenantA" : "TenantB";
                        return new MultiTenant.TenantInfo { Id = id, Identifier = id };
                    });

                    services.AddScoped<ITenant>(sp => sp.GetRequiredService<MultiTenant.TenantInfo>());
                    services.AddScoped<ITenantInfo>(sp => sp.GetRequiredService<MultiTenant.TenantInfo>());

                });
                using var scope = resolver.ServiceProvider.
                    CreateScope();
                var serviceProvider = scope.ServiceProvider;
                var tenant = serviceProvider.GetRequiredService<ITenantInfo>();
                var context = serviceProvider.GetRequiredService<LogDbContext>();
                tenant.Id.Should().BeSameAs(context.TenantInfo.Id);
                var traceId = new DefaultStringIdGenerator().GenerateRandomId(6);

                var logger = serviceProvider.GetRequiredService<ILogger<LoggingTests>>();

                using (logger.BeginScope(new Dictionary<string, object> {
                    { "TraceId", traceId}
                }))
                {
                    logger.LogInformation("Test log message {tenant} {traceId}", tenant.Id, traceId);
                    for (var j = 0; j < 3; j++)
                    {
                        await Task.Delay(300);
                        logger.LogInformation("Test log message {j} {tenant} {traceId}", j, tenant.Id, traceId);
                    }
                }

                await Task.Delay(5000);
                var log = await context.Logs.Where(l => l.TraceId == traceId)
                    .FirstOrDefaultAsync();
                _output.WriteLine($"Log: {log?.Message} {traceId} {tenant.Id} {context.TenantInfo.Id}");
                log.Should().NotBeNull();
            });
            await Task.Delay(3000);
        }
    }
}
