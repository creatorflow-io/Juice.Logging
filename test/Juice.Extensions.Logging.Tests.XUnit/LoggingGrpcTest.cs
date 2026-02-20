using Juice.Extensions.DependencyInjection;
using Juice.Extensions.MultiTenant;
using Juice.MultiTenant;
using Juice.Services;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.Extensions.Logging.Tests.XUnit
{

    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class LoggingGrpcTest
    {
        private readonly ITestOutputHelper _output;

        public LoggingGrpcTest(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "Log to gRPC"), TestPriority(999)]

        public async Task Log_should_write_GrpcAsync()
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
                        .AddGrpcLogger(configuration.GetSection("Logging:Grpc"))
                        .AddGrpcMetricsLogger(options =>
                        {
                            configuration.GetSection("Logging:GrpcMetrics").Bind(options);
                            options.SampleRate = TimeSpan.FromSeconds(3);
                        })
                        .AddConfiguration(configuration.GetSection("Logging"));
                    });

                    services.AddTestTenantRandom<TenantInfo>();

                });
                using var scope = resolver.ServiceProvider.CreateScope();
                await scope.ServiceProvider.TenantInvokeAsync(async context => {
                    var tenant = context.RequestServices.GetRequiredService<ITenant>();

                    var traceId = StringIdGenerator.Instance.GenerateRandomId(6);

                    var logger = context.RequestServices.GetRequiredService<ILogger<LoggingTests>>();

                    using (logger.BeginScope(new Dictionary<string, object> {
                        { "TraceId", traceId}
                    }))
                    {
                        logger.LogInformation("Test grpc log message {tenant} {traceId}", tenant.Id, traceId);
                        for (var j = 0; j < 3; j++)
                        {
                            await Task.Delay(300);
                            logger.LogInformation("Test grpc log message {j} {tenant} {traceId}", j, tenant.Id, traceId);
                        }
                    }

                });
                
                await Task.Delay(4000);
            });
            await Task.Delay(6000);
        }
    }
}
