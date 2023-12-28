using Juice.EF;
using Juice.EF.Migrations;
using Juice.Extensions.Logging.EF.LogEntries;
using Juice.Extensions.Logging.EF.LogMetrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.EF.DependencyInjection
{
    public static class LogDbServiceCollectionExtensions
    {
        public static IServiceCollection AddLogDbContext(this IServiceCollection services, IConfiguration configuration,
            Action<DbOptions>? configureOptions = default)
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<LogDbContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<LogDbContext>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName ??
                provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                };

            services.AddDbContext<LogDbContext>(options =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFLogMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Extensions.Logging.EF.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                            configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFLogMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Extensions.Logging.EF.SqlServer");
                            });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }
                options
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;
                options.UseLoggerFactory(LoggerFactory.Create(builder => { builder.ClearProviders(); }));
            });

            return services;
        }


        public static IServiceCollection AddLogMetricsDbContext(this IServiceCollection services, IConfiguration configuration,
            Action<DbOptions>? configureOptions = default)
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<LogMetricsDbContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<LogMetricsDbContext>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName ??
                provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                };

            services.AddDbContext<LogMetricsDbContext>((serviceProvider, options) =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFLogMetricsMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Extensions.Logging.EF.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                            configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFLogMetricsMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Extensions.Logging.EF.SqlServer");
                            });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }
                options
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;

                options.UseLoggerFactory(LoggerFactory.Create(builder => { builder.ClearProviders(); }));
            });

            return services;
        }
    }
}
