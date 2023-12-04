using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class LogMetricsDbContext : DbContext, ISchemaDbContext
    {
        public string? Schema { get; private set; }
        public LogMetricsDbContext(DbOptions<LogMetricsDbContext> dbOptions, DbContextOptions<LogMetricsDbContext> options) : base(options)
        {
            Schema = dbOptions.Schema;
        }

        public DbSet<ServiceLogMetric> ServiceMetrics { get; set; }
        public DbSet<CategoryLogMetric> CategoryMetrics { get; set; }
        public DbSet<OperationLogMetric> OperationMetrics { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<ServiceLogMetric>(builder =>
            {
                builder.ToTable("ServiceLogMetrics", Schema);
                builder.HasKey(x => new { x.ServiceId, x.Timestamp });

            });

            builder.Entity<CategoryLogMetric>(builder =>
            {
                builder.ToTable("CategoryLogMetrics", Schema);
                builder.HasKey(x => new { x.Category, x.Timestamp });
            });

            builder.Entity<OperationLogMetric>(builder =>
            {
                builder.ToTable("OperationLogMetrics", Schema);
                builder.HasKey(x => new { x.Operation, x.Timestamp });
            });
        }

    }

    /// <summary>
    /// For migrations
    /// </summary>
    public class LogMetricsDbContextFactory : IDesignTimeDbContextFactory<LogMetricsDbContext>
    {
        public LogMetricsDbContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                // Register DbContext class
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();

                var configuration = configService.GetConfiguration(args);

                var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";
                var connectionName =
                    provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    }
                ;
                var connectionString = configuration.GetConnectionString(connectionName);

                services.AddScoped(p =>
                {
                    var options = new DbOptions<LogMetricsDbContext> { Schema = "App" };
                    return options;
                });

                services.AddDbContext<LogMetricsDbContext>(options =>
                {
                    switch (provider)
                    {
                        case "PostgreSQL":
                            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                            options.UseNpgsql(
                               connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Extensions.Logging.EF.PostgreSQL");
                                });
                            break;

                        case "SqlServer":

                            options.UseSqlServer(
                                connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Extensions.Logging.EF.SqlServer");
                                });
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported provider: {provider}");
                    }

                });

            });

            return resolver.ServiceProvider.GetRequiredService<LogMetricsDbContext>();
        }
    }

}

